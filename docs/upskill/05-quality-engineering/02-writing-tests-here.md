# 02 — Writing Tests Here

Seven concrete recipes using the actual test infrastructure in this repo. Each recipe shows the pattern, what to import, and what to assert.

---

## Recipe 1: Domain Unit Test — Aggregate Invariant

**Goal:** Test that `Booking.Create` rejects a past start time.

**File to create:** `tests/NetCalendy.Domain.Tests/BookingTests/BookingAggregateTests.cs` (add test method)

**No extra setup needed.** Domain tests have zero external dependencies.

```csharp
[Fact]
public void Create_WithPastStartTime_ShouldFail()
{
    // Arrange
    var tenantId = TenantId.New();
    var eventTypeId = EventTypeId.New();
    var hostId = UserId.New();
    var invitee = Invitee.Create("Alice", "alice@example.com", "UTC", null).Value;
    var pastStart = DateTimeOffset.UtcNow.AddHours(-1);
    var end = pastStart.AddMinutes(30);

    // Act
    var result = Booking.Create(tenantId, eventTypeId, hostId, invitee, pastStart, end);

    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Contain("PastStartTime");
}
```

**What to add:**
```xml
<!-- tests/NetCalendy.Domain.Tests/NetCalendy.Domain.Tests.csproj -->
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="FluentAssertions" Version="6.12.2" />
<ProjectReference Include="..\..\src\NetCalendy.Domain\NetCalendy.Domain.csproj" />
```

**Key: No `await`, no mocks, no containers. Runs in < 1ms.**

---

## Recipe 2: Domain Unit Test — Domain Event Raised

**Goal:** Verify `Booking.Create` raises exactly one `BookingCreatedEvent`.

```csharp
[Fact]
public void Create_WithValidData_ShouldRaiseBookingCreatedEvent()
{
    // Arrange
    var tenantId = TenantId.New();
    var eventTypeId = EventTypeId.New();
    var hostId = UserId.New();
    var invitee = Invitee.Create("Alice", "alice@example.com", "UTC", null).Value;
    var start = DateTimeOffset.UtcNow.AddDays(1);
    var end = start.AddMinutes(30);

    // Act
    var result = Booking.Create(tenantId, eventTypeId, hostId, invitee, start, end);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.DomainEvents.Should().HaveCount(1);
    result.Value.DomainEvents.Single().Should().BeOfType<BookingCreatedEvent>();
    ((BookingCreatedEvent)result.Value.DomainEvents.Single()).BookingId.Should().Be(result.Value.Id);
}
```

**Self-check:** `DomainEvents` comes from `AggregateRoot<TId>` via the `IHasDomainEvents` interface. See [`src/NetCalendy.Domain/Common/AggregateRoot.cs`](../../src/NetCalendy.Domain/Common/AggregateRoot.cs).

---

## Recipe 3: Domain Unit Test — Value Object Validation

**Goal:** Test `Email.Create` rejects malformed emails.

```csharp
[Theory]
[InlineData("notanemail")]
[InlineData("@example.com")]
[InlineData("user@")]
[InlineData("")]
public void Email_Create_WithInvalidFormat_ShouldFail(string input)
{
    var result = Email.Create(input);
    result.IsFailure.Should().BeTrue();
}

[Theory]
[InlineData("alice@example.com")]
[InlineData("ALICE@EXAMPLE.COM")] // Normalized to lowercase
public void Email_Create_WithValidEmail_ShouldNormalizeLowercase(string input)
{
    var result = Email.Create(input);
    result.IsSuccess.Should().BeTrue();
    result.Value.Value.Should().Be(input.ToLowerInvariant());
}
```

**Pattern reference:** [`src/NetCalendy.Domain/Users/Email.cs`](../../src/NetCalendy.Domain/Users/Email.cs)

---

## Recipe 4: Integration Test — POST returns 201 and persists

**Goal:** Verify the `POST /api/bookings` endpoint creates a booking in the database.

**Uses:** `IntegrationTestWebAppFactory`, `JwtTokenFactory`, and the existing `HttpClient`.

```csharp
// tests/NetCalendy.Integration.Tests/Endpoints/BookingEndpointTests.cs
public class BookingEndpointTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly HttpClient _client;
    private readonly string _token;

    public BookingEndpointTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _token = JwtTokenFactory.GenerateToken(
            userId: Guid.NewGuid(),
            tenantId: _factory.DefaultTenantId);
    }

    [Fact]
    public async Task POST_bookings_Returns201_AndPersists()
    {
        // Arrange: seed an active event type
        var eventType = await _factory.SeedActiveEventTypeAsync();
        var request = new CreateBookingRequest(
            EventTypeId: eventType.Id.Value,
            HostUserId: eventType.OwnerId.Value,
            StartTime: DateTimeOffset.UtcNow.AddDays(1),
            InviteeName: "Alice",
            InviteeEmail: "alice@example.com",
            InviteeTimezone: "UTC");

        // Act
        var response = await _client.PostAsJsonAsync("/api/bookings", request,
            headers: new() { ["Authorization"] = $"Bearer {_token}" });

        // Assert: HTTP
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateBookingResponse>();
        body!.BookingId.Should().NotBeEmpty();

        // Assert: DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Bookings.FindAsync(BookingId.From(body.BookingId));
        saved.Should().NotBeNull();
        saved!.Invitee.Email.Should().Be("alice@example.com");
    }
}
```

**Key imports:**
```csharp
using Microsoft.Extensions.DependencyInjection;
using NetCalendy.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
```

---

## Recipe 5: Integration Test — 409 on Double Booking

**Goal:** Second booking for same host+time returns HTTP 409.

```csharp
[Fact]
public async Task POST_bookings_WhenSlotTaken_Returns409()
{
    var eventType = await _factory.SeedActiveEventTypeAsync();
    var startTime = DateTimeOffset.UtcNow.AddDays(1).Date.AddHours(10); // deterministic time
    var request = BookingRequest(eventType, startTime);

    // First booking
    var first = await _client.PostAsJsonAsync("/api/bookings", request, bearerToken: _token);
    first.StatusCode.Should().Be(HttpStatusCode.Created);

    // Second booking — same slot
    var second = await _client.PostAsJsonAsync("/api/bookings", request, bearerToken: _token);
    second.StatusCode.Should().Be(HttpStatusCode.Conflict); // 409
}
```

**Why this matters:** This test would catch a regression if the conflict-check query changes or `ConflictsWith()` logic is modified.

---

## Recipe 6: Integration Test — Auth Required

**Goal:** Endpoints that require `[Authorize]` return 401 without a token.

```csharp
[Theory]
[InlineData("POST", "/api/bookings")]
[InlineData("GET", "/api/event-types")]
[InlineData("POST", "/api/event-types")]
public async Task AuthorizedEndpoints_WithoutToken_Return401(string method, string path)
{
    var request = new HttpRequestMessage(new HttpMethod(method), path);
    // Note: no Authorization header added

    var response = await _client.SendAsync(request);

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

**Why this matters:** Auth regression tests. Easy to accidentally remove `[Authorize]` during refactoring.

---

## Recipe 7: Unit Test — Validation Pipeline Behavior

**Goal:** `ValidationBehavior` throws `ValidationException` when validator finds errors.

**Requires:** `NetCalendy.Application.Tests` project (must be created) with NSubstitute.

```csharp
[Fact]
public async Task ValidationBehavior_WhenValidationFails_ThrowsValidationException()
{
    // Arrange
    var validator = new CreateBookingValidator();
    var validators = new List<IValidator<CreateBookingCommand>> { validator };
    var behavior = new ValidationBehavior<CreateBookingCommand, Result<CreateBookingResponse>>(validators);

    var invalidCommand = new CreateBookingCommand(
        EventTypeId: Guid.NewGuid(),
        HostUserId: Guid.NewGuid(),
        StartTime: DateTimeOffset.UtcNow.AddDays(-1), // past — fails validation
        InviteeName: "", // empty — fails validation
        InviteeEmail: "not-an-email", // invalid — fails validation
        InviteeTimezone: "UTC");

    RequestHandlerDelegate<Result<CreateBookingResponse>> next =
        () => Task.FromResult(Result.Success(new CreateBookingResponse(/* ... */)));

    // Act & Assert
    var ex = await Assert.ThrowsAsync<ValidationException>(
        () => behavior.Handle(invalidCommand, next, CancellationToken.None));

    ex.Errors.Should().HaveCountGreaterThan(1); // multiple failures collected
}
```

**Pattern reference:** [`src/NetCalendy.Application/Common/Behaviors/ValidationBehavior.cs`](../../src/NetCalendy.Application/Common/Behaviors/ValidationBehavior.cs), [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingValidator.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingValidator.cs)
