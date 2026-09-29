# 03 — Fake Code Contrasts

Each contrast shows a bad version and a better version. The bad version is illustrative (not real repo code) but resembles mistakes commonly made in codebases like this one. The better version uses actual patterns from this repo. Your job: explain *why* the better version is better before reading the explanation.

---

## Contrast 1: Domain Logic in the Controller

**Bad:**
```csharp
// illustrative — not from this repo
[HttpPost]
public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
{
    if (request.StartTime < DateTimeOffset.UtcNow)
        return BadRequest("Start time must be in the future");

    var booking = new Booking
    {
        Id = Guid.NewGuid(),
        StartTime = request.StartTime,
        EndTime = request.StartTime.AddMinutes(30),
        InviteeEmail = request.Email
    };
    _context.Bookings.Add(booking);
    await _context.SaveChangesAsync();
    return Ok(booking);
}
```

**Better (actual pattern in this repo):**
```csharp
// src/NetCalendy.Api/Controllers/BookingsController.cs:19
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateBookingRequest request, CancellationToken ct)
{
    var result = await _mediator.Send(request.ToCommand(), ct);
    return result.ToActionResult();
}
```

Domain validation in `Booking.Create()`, business rules in `CreateBookingHandler`, format validation in `CreateBookingValidator`.

**Why the bad version fails:**
1. The `startTime < UtcNow` check lives in the controller — if you add a background import that creates bookings, it bypasses this check entirely.
2. The controller knows about `AddMinutes(30)` — a hardcoded duration that belongs to the event type.
3. Direct `_context` access in the controller couples HTTP and persistence layers.
4. `new Booking { ... }` skips the `Booking.Create()` factory — no domain events raised, no invariant checking.

**Pattern reference:** [`src/NetCalendy.Domain/Bookings/Booking.cs`](../../src/NetCalendy.Domain/Bookings/Booking.cs), [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs)

---

## Contrast 2: Throwing Exceptions vs. Returning Results

**Bad:**
```csharp
// illustrative
public async Task<BookingDto> Handle(GetBookingQuery query, CancellationToken ct)
{
    var booking = await _bookings.GetByIdAsync(query.Id, ct);
    if (booking is null)
        throw new NotFoundException($"Booking {query.Id} not found");

    return new BookingDto(booking);
}
```

**Better:**
```csharp
// pattern from GetBookingHandler
public async Task<Result<BookingDto>> Handle(GetBookingQuery query, CancellationToken ct)
{
    var booking = await _bookings.GetByIdAsync(query.Id, ct);
    if (booking is null)
        return Result.Failure<BookingDto>(Error.NotFound("Booking", query.Id));

    return Result.Success(new BookingDto(booking));
}
```

**Why the bad version fails:**
1. Exceptions are expensive — creating a stack trace for expected "not found" cases is wasted CPU on every 404.
2. The caller can't distinguish "business failure" from "programming error" without inspecting exception type.
3. Exception-based control flow hides intent. `Result<T>` makes the success/failure contract explicit in the method signature.
4. Pipeline behaviors can't introspect exception types easily; they can introspect `Result.IsFailure`.

**Bonus question:** What does `ExceptionHandlingMiddleware` do with `NotFoundException`? Does it exist in this repo? (Answer: No — `NotFound` returns `Result.Failure`, never throws. The middleware handles only `ValidationException` and unexpected exceptions.)

**Pattern reference:** [`src/NetCalendy.Domain/Common/Result.cs`](../../src/NetCalendy.Domain/Common/Result.cs), [`src/NetCalendy.Application/Bookings/Queries/GetBooking/GetBookingHandler.cs`](../../src/NetCalendy.Application/Bookings/Queries/GetBooking/GetBookingHandler.cs)

---

## Contrast 3: Anemic Domain Model

**Bad:**
```csharp
// illustrative — public setters, no behavior
public class Booking
{
    public Guid Id { get; set; }
    public BookingStatus Status { get; set; }
    public DateTimeOffset CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

// In a service:
booking.Status = BookingStatus.Cancelled;
booking.CancelledAt = DateTimeOffset.UtcNow;
booking.CancellationReason = reason;
```

**Better:**
```csharp
// src/NetCalendy.Domain/Bookings/Booking.cs:55
public Result Cancel(string? reason)
{
    if (Status == BookingStatus.Cancelled)
        return Result.Failure(Error.Conflict("Booking.AlreadyCancelled", "..."));

    Status = BookingStatus.Cancelled;
    CancelledAt = DateTimeOffset.UtcNow;
    CancellationReason = reason;
    RaiseDomainEvent(new BookingCancelledEvent(Id, TenantId, reason));
    return Result.Success();
}
```

**Why the bad version fails:**
1. Nothing prevents calling code from setting `Status = Cancelled` without setting `CancelledAt`. The object can be in an inconsistent state.
2. Invariant "can't cancel an already-cancelled booking" must be checked everywhere that touches this code — it will be forgotten somewhere.
3. The domain event (`BookingCancelledEvent`) is never raised in the bad version — downstream side effects silently don't happen.
4. `public set` means EF Core doesn't need a private setter — but it also means *any code in any layer* can mutate state without going through business logic.

**Pattern reference:** [`src/NetCalendy.Domain/Bookings/Booking.cs`](../../src/NetCalendy.Domain/Bookings/Booking.cs), [`src/NetCalendy.Domain/Common/AggregateRoot.cs`](../../src/NetCalendy.Domain/Common/AggregateRoot.cs)

---

## Contrast 4: Primitive Obsession

**Bad:**
```csharp
// illustrative
public class Booking
{
    public Guid TenantId { get; }
    public Guid HostUserId { get; }
    public Guid EventTypeId { get; }
}

// At a call site — easy to mix up:
var booking = Booking.Create(tenantId, eventTypeId, hostUserId, ...); // wrong order, compiles fine
```

**Better:**
```csharp
// actual pattern in this repo
public class Booking : AggregateRoot<BookingId>
{
    public TenantId TenantId { get; }
    public UserId HostId { get; }
    public EventTypeId EventTypeId { get; }
}

// At a call site — compiler catches wrong type:
var booking = Booking.Create(tenantId, eventTypeId, hostUserId, ...); // CS: can't pass EventTypeId where UserId expected
```

**Why the bad version fails:**
1. All three IDs are `Guid` — the compiler cannot catch accidentally swapped arguments.
2. A raw `Guid` can be any ID. A `UserId` is self-documenting and type-safe.
3. Strong IDs allow repository methods to be typed correctly: `GetByIdAsync(UserId id)` won't accidentally accept an `EventTypeId`.

**Pattern reference:** [`src/NetCalendy.Domain/Users/UserId.cs`](../../src/NetCalendy.Domain/Users/UserId.cs), [`src/NetCalendy.Domain/Bookings/BookingId.cs`](../../src/NetCalendy.Domain/Bookings/BookingId.cs)

---

## Contrast 5: No-Tenant Query

**Bad:**
```csharp
// illustrative — missing tenant scope
public async Task<EventType?> GetByIdAsync(EventTypeId id, CancellationToken ct)
    => await _db.EventTypes.FirstOrDefaultAsync(e => e.Id == id, ct);
```

**Better:**
```csharp
// pattern from EventTypeRepository
public async Task<EventType?> GetByIdAsync(EventTypeId id, CancellationToken ct)
    => await _db.EventTypes
        .Where(e => e.TenantId == _currentTenant.TenantId && e.Id == id)
        .FirstOrDefaultAsync(ct);
```

**Why the bad version fails:**
1. IDOR: Tenant A can read Tenant B's event type by guessing the GUID.
2. For SaaS, tenant isolation is a **security property**, not a performance optimization. Missing it is a data breach waiting to happen.
3. The bad version looks correct until you test with two tenants.

**Pattern reference:** [`src/NetCalendy.Infrastructure/Persistence/Repositories/EventTypeRepository.cs`](../../src/NetCalendy.Infrastructure/Persistence/Repositories/EventTypeRepository.cs), `03-validation-auth-and-permissions.md` IDOR section

---

## Contrast 6: Missing CancellationToken Propagation

**Bad:**
```csharp
// illustrative
public async Task<Result<BookingDto>> Handle(GetBookingQuery query, CancellationToken ct)
{
    var booking = await _bookings.GetByIdAsync(query.Id); // ct not passed
    var eventType = await _eventTypes.GetByIdAsync(booking.EventTypeId); // ct not passed
    return Result.Success(new BookingDto(booking, eventType));
}
```

**Better:**
```csharp
// actual pattern in all handlers
var booking = await _bookings.GetByIdAsync(query.Id, ct);
var eventType = await _eventTypes.GetByIdAsync(booking.EventTypeId, ct);
```

**Why the bad version fails:**
1. If the client disconnects mid-request, the server still completes both database queries and allocates all objects. On a busy server, abandoned in-flight requests can exhaust connection pool.
2. EF Core respects `CancellationToken` — passing it allows the query to be abandoned. Not passing it means the full query always runs.
3. The pattern is "pass `ct` to every async call" — one miss breaks the whole chain.

**Pattern reference:** [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs) — all 4 async calls pass `ct`.

---

## Contrast 7: Validator in the Wrong Layer

**Bad:**
```csharp
// illustrative — validator on a domain entity
public class BookingValidator : AbstractValidator<Booking>
{
    public BookingValidator()
    {
        RuleFor(b => b.StartTime).GreaterThan(DateTimeOffset.UtcNow);
    }
}
```

**Better:**
```csharp
// src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingValidator.cs
public class CreateBookingValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingValidator()
    {
        RuleFor(c => c.StartTime).GreaterThan(DateTimeOffset.UtcNow)
            .WithMessage("Start time must be in the future.");
    }
}
```

**Why the bad version fails:**
1. Validators belong in the Application layer, with commands — they validate *intent* (the command), not *state* (the entity).
2. Validating a domain entity means the entity must be constructible in an invalid state (you need an instance to validate). The better design makes invalid instances impossible via factory methods.
3. `DateTimeOffset.UtcNow` in a `AbstractValidator` constructor is evaluated once at startup — the validator would always use the start time from when the app launched.

**Pattern reference:** [`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingValidator.cs`](../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingValidator.cs), [`src/NetCalendy.Application/DependencyInjection.cs`](../../src/NetCalendy.Application/DependencyInjection.cs)

---

## Contrast 8: Test That Doesn't Test

**Bad:**
```csharp
// illustrative — tests the framework, not the behavior
[Fact]
public async Task CreateBooking_Returns201()
{
    var response = await _client.PostAsync("/api/bookings", someContent);
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}
```

**Better:**
```csharp
// pattern from BookingEndpointTests
[Fact]
public async Task POST_bookings_WithValidSlot_Returns201AndCreatesBooking()
{
    var request = _faker.ValidBookingRequest();
    var response = await _client.PostAsync("/api/bookings", request.ToJson(), _token);

    response.Should().HaveStatusCode(HttpStatusCode.Created);
    var body = await response.ReadAs<CreateBookingResponse>();
    body.BookingId.Should().NotBeEmpty();
    body.CancellationToken.Should().NotBeNullOrEmpty();
    body.StartTime.Should().Be(request.StartTime);

    // Verify persistence:
    var db = _factory.GetDbContext();
    db.Bookings.Should().ContainSingle(b => b.Id == BookingId.From(body.BookingId));
}
```

**Why the bad version fails:**
1. The test only checks the status code — a stub that always returns 201 would pass.
2. No assertion on the response body — the returned IDs could be wrong.
3. No assertion on side effects — the booking might not be saved.
4. The test name `Creates201` doesn't describe what scenario was tested — a new developer can't tell what this test covers without reading the body.

**Pattern reference:** [`tests/NetCalendy.Integration.Tests/`](../../tests/NetCalendy.Integration.Tests/), [`tests/NetCalendy.Integration.Tests/Fixtures/IntegrationTestWebAppFactory.cs`](../../tests/NetCalendy.Integration.Tests/Fixtures/IntegrationTestWebAppFactory.cs)
