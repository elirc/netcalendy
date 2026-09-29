# 01 — Testing Strategy

## The Test Pyramid in This Repo

```
         /\
        /  \
       / E2E \ ← Not present. Use Playwright when the frontend matures.
      /--------\
     / Integration \ ← NetCalendy.Integration.Tests — real DB, real HTTP
    /--------------\
   /   Unit Tests   \ ← NetCalendy.Domain.Tests, NetCalendy.Application.Tests
  /------------------\
```

| Layer | Project | What it tests | Speed | Isolation |
|---|---|---|---|---|
| Unit | `NetCalendy.Domain.Tests` | Domain logic: aggregates, value objects, domain events | ~ms | No I/O |
| Unit | `NetCalendy.Application.Tests` | Handler logic with mocked repositories | ~ms | NSubstitute mocks |
| Integration | `NetCalendy.Integration.Tests` | Full HTTP → DB round-trip | ~s | Testcontainers PostgreSQL |

---

## What Belongs in Each Layer

### Domain unit tests — fast, pure, no dependencies

**Test here:**
- Aggregate factory invariants: `Booking.Create` with invalid start time, past start, missing invitee
- Value object validation: `Email.Create` with malformed emails
- State machine transitions: `Booking.Cancel` when already cancelled
- Domain event raising: does `Booking.Create` raise exactly one `BookingCreatedEvent`?
- `Schedule.GetAvailableSlots` output for known inputs

**Do NOT test here:**
- Database persistence (no EF Core)
- Handler orchestration
- HTTP status codes

**Example:** [`tests/NetCalendy.Domain.Tests/BookingTests/BookingAggregateTests.cs`](../../tests/NetCalendy.Domain.Tests/BookingTests/BookingAggregateTests.cs)

---

### Application unit tests — handler logic with mocked repos

**Test here:**
- Handler returns the right `Result<T>` shape for each outcome
- Correct repository methods are called with correct arguments
- Domain event dispatch flows (did the aggregate's event get raised?)
- The "not found" early-exit: handler returns `Error.NotFound` when repository returns null
- Ownership checks: handler returns `Error.Validation("EventType.Forbidden")` when owner mismatch

**Do NOT test here:**
- Database SQL correctness (no real DB)
- HTTP routing, middleware, auth

**Mocking library:** NSubstitute. Repositories return `Task.FromResult(...)` via `.Returns(...)`.

**Example pattern:**
```csharp
// illustrative — not real file
var mockRepo = Substitute.For<IBookingRepository>();
mockRepo.GetByIdAsync(Arg.Any<BookingId>(), Arg.Any<CancellationToken>())
    .Returns(Task.FromResult<Booking?>(null));

var handler = new GetBookingHandler(mockRepo, currentTenant);
var result = await handler.Handle(new GetBookingQuery(Guid.NewGuid()), CancellationToken.None);

result.IsFailure.Should().BeTrue();
result.Error.Code.Should().Be("Booking.NotFound");
```

---

### Integration tests — full stack, real database

**Test here:**
- HTTP 201/400/409/401/403 from real endpoints with real JWT
- Persistence: after a POST, does the DB actually contain the record?
- Multi-step flows: create then cancel, create then retrieve
- Auth: does a missing/invalid JWT return 401?
- Tenant isolation: does Tenant A's booking appear in Tenant B's list?

**Do NOT test here:**
- Edge cases in domain logic (too slow, too expensive — cover those in unit tests)

**Key files:**
- [`tests/NetCalendy.Integration.Tests/Fixtures/IntegrationTestWebAppFactory.cs`](../../tests/NetCalendy.Integration.Tests/Fixtures/IntegrationTestWebAppFactory.cs) — Testcontainers setup
- [`tests/NetCalendy.Integration.Tests/Fixtures/JwtTokenFactory.cs`](../../tests/NetCalendy.Integration.Tests/Fixtures/JwtTokenFactory.cs) — JWT generation

---

## Fixture Design: `IntegrationTestWebAppFactory`

```
xUnit discovers test class
    └── IClassFixture<IntegrationTestWebAppFactory> called
        └── InitializeAsync
            └── PostgreSqlContainer.StartAsync()   ← ~3-10s cold start
            └── WebApplicationFactory configured
                └── Removes real DbContextOptions
                └── Adds test-container connection string
                └── Runs EF migrations via MigrateDatabaseAsync
        └── Test method executes (in-process HTTP, shared DB)
    └── IAsyncDisposable.DisposeAsync
        └── Container stopped
```

**Shared fixture vs. per-test isolation:** The container is shared across all tests in a class (via `IClassFixture`). Each test is responsible for its own seed data. If tests share data, ordering effects can cause flakiness — use unique slugs, emails, and IDs in each test (Bogus `.NewGuid()` or `.Random.AlphaNumeric(8)` suffixes).

---

## Test Isolation Techniques

### 1. Unique data per test
Use Bogus to generate data with a random suffix per test run:
```csharp
var slug = $"test-{Faker.Random.AlphaNumeric(8)}";
```

### 2. Transaction rollback (not implemented here, but common)
Wrap each test in a transaction that rolls back. Not used in this repo — Testcontainers is reset per test class, not per test.

### 3. Seed data in test setup
Each test seeds what it needs:
```csharp
// In test setup
await _factory.SeedTenantAsync("acme", TenantId.New());
await _factory.SeedUserAsync(userId, tenantId, "test@example.com");
```

---

## What This Repo Is Missing

| Gap | Impact | Suggested Fix |
|---|---|---|
| No `NetCalendy.Application.Tests` project exists | Handler logic tested only via integration tests (slow) | Add project, mock with NSubstitute |
| No frontend tests | React components untested | Add Vitest + React Testing Library |
| No E2E tests | Full user flows untested | Add Playwright when API stabilizes |
| Integration tests share DB state across test methods | Test order can matter | Use `TransactionScope` rollback or unique seed data per test |
| `JwtTokenFactory` uses hardcoded key from appsettings | If appsettings key changes, tests break silently | Load key from config in factory |
