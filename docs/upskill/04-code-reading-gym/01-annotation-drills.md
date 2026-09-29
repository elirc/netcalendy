# 01 — Annotation Drills

For each drill: read the file, then annotate it mentally (or in a copy) with: inputs, outputs, dependencies, invariants, side effects, and failure modes. Use the self-grading rubric at the bottom of each drill.

---

## Drill 1: `Booking.Create` factory method

**File:** [`src/NetCalendy.Domain/Bookings/Booking.cs:37-55`](../../src/NetCalendy.Domain/Bookings/Booking.cs#L37)

Read lines 37–55. For each, annotate:
- What are the **inputs**? What types?
- What **invariants** does it enforce? (What state transitions are impossible after this call?)
- What **side effects** occur? (Hint: one is non-obvious)
- What are the **failure modes**? (When does it return `IsFailure`?)
- What **contract** does the caller depend on?

**After annotating, answer:**
1. If `startTime == endTime`, what is returned?
2. If `invitee.Email` is `"ALICE@EXAMPLE.COM"` — was it already normalized before reaching here?
3. What is `CancellationToken` initialized to? Where?
4. When does `BookingCreatedEvent` fire?

**Self-grade:**
- Basic: Identified 2+ failure modes and the domain event
- Solid: Named all invariants, explained why `startTime < UtcNow` check belongs here and not only in the validator
- Strong: Identified the re-entrancy safety question (can `Create` be called twice for the same slot before one saves?)

---

## Drill 2: `ValidationBehavior<TRequest, TResponse>`

**File:** [`src/NetCalendy.Application/Common/Behaviors/ValidationBehavior.cs`](../../src/NetCalendy.Application/Common/Behaviors/ValidationBehavior.cs)

Annotate:
- **Inputs:** What does the behavior receive?
- **Dependencies:** What is injected? What is the type?
- **Control flow:** When does it call `next()`? When doesn't it?
- **Side effects:** What does it throw and why?
- **Failure modes:** What happens if no validators are registered for a command?

**Questions:**
1. What happens if two validators are registered for the same command?
2. Where does the thrown `ValidationException` get caught?
3. Is it possible for `ValidationBehavior` to run and then the handler to also validate the same rule? Is that a problem?

**Self-grade:**
- Basic: Named what gets thrown and where it's caught
- Solid: Explained why `!validators.Any()` short-circuits, and what would happen without it
- Strong: Identified that double-validation (FV + domain) is intentional defense-in-depth and can articulate the tradeoff

---

## Drill 3: `AppDbContext.SaveChangesAsync`

**File:** [`src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs:31-50`](../../src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs#L31)

Annotate:
- **Inputs:** What entities are in scope? (Hint: everything the change tracker knows about)
- **Outputs:** `int` — what does this number represent?
- **Side effects:** Two distinct side effects happen here. Name them in order.
- **Invariants:** What ordering guarantee matters here?
- **Failure modes:** What happens if `publisher.Publish` throws?

**Questions:**
1. Can a domain event be raised twice for the same aggregate if `SaveChangesAsync` is called twice in one request?
2. Why does `ClearDomainEvents()` happen before `base.SaveChangesAsync`, not after?
3. What happens to an entity that has no domain events?

**Self-grade:**
- Basic: Named the two side effects (DB write, event dispatch)
- Solid: Explained why events are cleared before saving
- Strong: Identified the "publish throws post-save" reliability gap and proposed a fix

---

## Drill 4: `GetAvailableSlotsHandler`

**File:** [`src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs`](../../src/NetCalendy.Application/Schedules/Queries/GetAvailableSlots/GetAvailableSlotsHandler.cs)

Annotate:
- **Dependencies:** Which repositories are injected?
- **Happy path:** Trace the data shape at each step (EventType → Schedule → Bookings → Slots)
- **Empty cases:** How many ways can this return an empty list vs. a failure result?
- **Side effects:** Read-only query — any writes?
- **Performance characteristics:** How many database queries does this handler execute?

**Questions:**
1. What timezone does `slot.ToUtc(tz)` convert from and to?
2. If the host has 3 schedules, which one is used?
3. What does `startUtc < DateTimeOffset.UtcNow` filter remove?

**Self-grade:**
- Basic: Named the database queries and the three early-exit paths
- Solid: Traced the timezone conversion correctly
- Strong: Identified the O(n×m) conflict check and proposed a SQL alternative

---

## Drill 5: `TenantResolutionMiddleware`

**File:** [`src/NetCalendy.Api/Middleware/TenantResolutionMiddleware.cs`](../../src/NetCalendy.Api/Middleware/TenantResolutionMiddleware.cs)

Annotate:
- **When it runs:** Before or after auth middleware?
- **Inputs:** Two possible tenant resolution paths. What are they?
- **Side effects:** What does it write to? Is this accessible to handlers?
- **Failure modes:** What happens if the slug doesn't match any tenant?

**Questions:**
1. If the tenant slug in the header doesn't match any tenant, what does the middleware do? What does the handler see?
2. Why does this run before authentication?
3. There is a gap between what this middleware stores (`HttpContext.Items`) and what `ICurrentTenantService` reads (JWT claim). Describe the scenario where these could diverge.

**Self-grade:**
- Basic: Named the two resolution paths and what happens on failure
- Solid: Explained why it runs before auth and what `HttpContext.Items` is
- Strong: Identified the Items vs. JWT claim divergence and its security implication

---

## Drill 6: `ResultExtensions.ToActionResult`

**File:** [`src/NetCalendy.Api/Extensions/ResultExtensions.cs`](../../src/NetCalendy.Api/Extensions/ResultExtensions.cs)

Annotate:
- **Input:** `Result` and `Result<T>` — what's the difference?
- **Mapping logic:** What property of `Error` drives the status code?
- **Contract:** What HTTP status codes can this produce?
- **Gaps:** Are there error codes that might not map to an expected status code?

**Questions:**
1. What status code does `Error.Conflict("Booking.AlreadyCancelled", "...")` map to?
2. What status code does `Error.Validation("EventType.Forbidden", "...")` map to? Is 400 the right code for a permissions failure?
3. What happens to an error code like `"Unknown.Error"` that doesn't match any suffix?

**Self-grade:**
- Basic: Traced two specific error codes through to their HTTP status
- Solid: Identified the `Forbidden` mapping problem (400 instead of 403)
- Strong: Proposed a more systematic mapping approach (e.g., `Error` carrying a `HttpStatusCode` hint)

---

## Drill 7: `BookingAggregateTests`

**File:** [`tests/NetCalendy.Domain.Tests/BookingTests/BookingAggregateTests.cs`](../../tests/NetCalendy.Domain.Tests/BookingTests/BookingAggregateTests.cs)

Annotate:
- **Test naming:** Describe the naming convention used. What does each test name tell you?
- **Arrange-Act-Assert:** Identify each section in `Create_WithValidData_ShouldSucceedAndRaiseEvent`
- **What is tested:** What invariant does each test verify?
- **What is NOT tested:** What behaviors of `Booking` are not covered?

**Questions:**
1. The test uses `DateTimeOffset.UtcNow.AddDays(1)`. What happens if this test runs exactly at midnight and daylight saving time shifts the clock? Is it flaky?
2. Why doesn't the test need an EF Core database context?
3. Write one more test method name (just the name) that would increase meaningful coverage.

**Self-grade:**
- Basic: Identified Arrange/Act/Assert and named untested behaviors
- Solid: Spotted the timing sensitivity and explained why domain tests need no infrastructure
- Strong: Named a missing test that covers a real production risk

---

## Drill 8: `BookingPage.tsx` Form Submission

**File:** [`web/src/components/booking/BookingPage.tsx:28-42`](../../web/src/components/booking/BookingPage.tsx#L28)

Annotate:
- **State managed:** List each `useState` variable and its type
- **Side effects:** What does `handleSubmit` do beyond setting state?
- **Error handling:** What errors can occur? How are they surfaced?
- **Invariants:** What state transitions are possible? (step machine)

**Questions:**
1. If the API returns a 409 (time conflict), what does the user see?
2. What happens if `selected` is null when `handleSubmit` is called?
3. Is `submitting` correctly reset in both the success and failure paths?

**Self-grade:**
- Basic: Named all state variables and the three steps
- Solid: Traced the error handling path and spotted the `selected` null guard
- Strong: Identified that `getCancellationToken` is stored in local component state — what's lost if the user refreshes the confirmation page?
