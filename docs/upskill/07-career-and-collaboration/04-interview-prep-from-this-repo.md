# 04 — Interview Prep from This Repo

Each question shows what a junior answer sounds like, what mid-level adds, and what a senior includes. Use the codebase as your evidence base — interviewers are impressed by engineers who cite specifics, not vague principles.

---

## Section 1: C# / .NET Runtime

---

### Q: What is the difference between `async/await` and synchronous code? When would you choose one over the other?

**Junior answer:**  
`async/await` is used for I/O operations. It lets the thread do other work while waiting. You use it for database calls, HTTP calls, and file reads.

**Mid-level adds:**  
`async/await` is not about threads — it's about freeing the calling thread to be returned to the thread pool while I/O is in progress. In ASP.NET Core, this means a single server can handle many more concurrent requests than if each blocked a thread. In this codebase, every handler accepts `CancellationToken ct` and passes it to every `await` call (e.g., [`CreateBookingHandler.cs`](../../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs)) — if the client disconnects, the token is cancelled and EF Core abandons the query.

**Senior includes:**  
`ConfigureAwait(false)` behavior in library vs. application code. The classic deadlock in non-Core ASP.NET when mixing `.Result` and `async`. The cost of state machine allocation for `async` methods called in tight loops (where `ValueTask` may be preferable). Also: `CancellationToken` is not free — it registers a callback; passing it to every call is correct but adds a small allocation.

---

### Q: What is garbage collection in .NET and how does it affect server performance?

**Junior answer:**  
.NET manages memory automatically. GC runs periodically and frees objects that are no longer referenced. You don't call `free()`.

**Mid-level adds:**  
GC pauses the application during collection. Gen 0 collections are fast and frequent; Gen 2 (large object heap) collections are slow and cause longer pauses. In an API, this means: allocating many short-lived objects per request is fine (Gen 0 handles this well), but allocating large arrays or loading 10,000 EF entities per request can trigger Gen 2 GC. In this codebase, `GetAvailableSlotsHandler` loads all bookings into memory — on a busy server with many concurrent requests, this could pressure the GC.

**Senior includes:**  
`IDisposable` / `using` patterns. `Span<T>` and `Memory<T>` for zero-allocation processing. Recognizing GC pressure from a heap profiler (JetBrains dotMemory, `dotnet-counters`). The difference between Gen 0/1/2 and when to worry about each. The large object heap threshold (85KB) and why loading large arrays frequently is a problem.

---

### Q: Explain the difference between `record`, `class`, and `struct` in C#.

**Junior answer:**  
`class` is a reference type. `struct` is a value type copied on assignment. `record` is like a class but with value-based equality.

**Mid-level adds:**  
In this codebase, commands and queries are `record` types (e.g., `CreateBookingCommand`). `record` gives structural equality — two commands with the same field values are equal. This matters for MediatR's deduplication and for test assertions. Value objects like `Email` or `Duration` are `readonly record struct` or `record class` with private constructors. The choice: `record struct` for small immutable values (avoids heap allocation), `record class` for larger or nullable values.

**Senior includes:**  
When `struct` causes performance bugs (large structs passed by value; prefer `ref struct` for stack-confined types). `record struct` equality and its interaction with `Span<T>`. The difference between `with` expressions on records and mutation. When immutability guarantees matter for thread safety (value objects in domain models are naturally thread-safe; entities with private setters are not if two threads touch the same instance).

---

## Section 2: ASP.NET Core / EF Core / React

---

### Q: How does dependency injection work in ASP.NET Core? What are the three lifetimes?

**Junior answer:**  
DI lets you inject services instead of `new`-ing them. Lifetimes: Transient (new each time), Scoped (one per request), Singleton (one for the app lifetime).

**Mid-level adds:**  
In this codebase ([`src/NetCalendy.Infrastructure/DependencyInjection.cs`](../../../src/NetCalendy.Infrastructure/DependencyInjection.cs)), repositories and `ICurrentTenantService` are Scoped — one instance per HTTP request, which is correct because they depend on `AppDbContext` (also Scoped). `AppDbContext` as Scoped is important: EF Core's change tracker tracks entities per DbContext instance. If it were Singleton, entity changes from one request would bleed into another. `IPublisher` (MediatR) is Scoped to allow resolution of Scoped handlers.

**Senior includes:**  
Captive dependency problem: a Singleton that depends on a Scoped service will hold the Scoped service for the app's lifetime — a subtle, hard-to-debug bug. In ASP.NET Core, this causes a startup error in development mode (`ValidateScopes: true`). How to resolve Scoped services from Singletons: create a child scope with `IServiceScopeFactory`. When background services (`IHostedService`) need Scoped services, they must create a scope explicitly — not inject Scoped services directly into the Singleton hosted service.

---

### Q: What is an EF Core owned type and when do you use it?

**Junior answer:**  
Owned types let you store a complex object as columns on the parent table instead of a separate table.

**Mid-level adds:**  
In this codebase, `Invitee` is an EF Core owned type on `Booking` ([`BookingConfiguration.cs`](../../../src/NetCalendy.Infrastructure/Persistence/Configurations/BookingConfiguration.cs)). The `Invitee_Name`, `Invitee_Email`, `Invitee_Timezone`, `Invitee_Notes` columns live on the `bookings` table. This is correct for a value object that always loads with its aggregate root (no lazy loading of owned types). The alternative — a separate `invitees` table — would require a JOIN for every booking read, for no gain, since an invitee never exists independently of its booking.

**Senior includes:**  
Owned types have a `HasColumnName` mapping for the shadow property naming. EF always loads owned types eagerly — you can't select a booking without also loading all Invitee columns. For very wide tables this matters. `OwnsMany` (for collections of owned types) has different semantics — they get a FK-linked table unless you use JSON columns. In EF 9, owned entities can be stored as JSON columns natively: `builder.OwnsOne(b => b.Invitee, inv => inv.ToJson())`.

---

### Q: How does React Query (TanStack Query) manage cache invalidation?

**Junior answer:**  
`useQuery` caches the result. After a mutation, you call `queryClient.invalidateQueries(key)` to mark the cache stale, which triggers a re-fetch.

**Mid-level adds:**  
In this codebase (see `05-key-flows.md` React Booking UI flow), `useDeleteEventType` calls `queryClient.invalidateQueries({ queryKey: ['event-types'] })` on success. This marks all queries with that key stale and triggers background re-fetch if the component is mounted. The key detail: invalidation is not a cache clear — it's a staleness marker. If the component unmounts before the re-fetch, the stale data sits in cache until next mount. This is the correct behavior for the UX (no blank screen on navigation).

**Senior includes:**  
`staleTime` vs. `gcTime` (cacheTime): `staleTime` controls when data is considered stale (default 0 — immediately); `gcTime` controls when unused data is garbage collected (default 5 minutes). Setting `staleTime: 30000` means the cache is fresh for 30 seconds — no re-fetch within that window. Cache key design: `['bookings', hostId]` means host-specific cache keys; invalidating `['bookings']` (prefix) invalidates all of them. The difference between `invalidateQueries` (soft stale + re-fetch if mounted) vs. `removeQueries` (hard delete from cache).

---

## Section 3: Architecture and Design

---

### Q: What is Clean Architecture and why would you use it?

**Junior answer:**  
It's a way of organizing code into layers: Domain, Application, Infrastructure, and UI/API. The inner layers don't know about the outer layers.

**Mid-level adds:**  
The dependency rule: code can only depend inward. Domain has no dependencies. Application depends only on Domain. Infrastructure and API depend on both. In this codebase, `IBookingRepository` is defined in the Domain layer but implemented in Infrastructure. The Application handler depends on the interface, not the EF Core implementation — this is why handlers can be unit tested with mocked repos without an actual database. The value is testability and replaceability.

**Senior includes:**  
The cost: more boilerplate. For a small CRUD app, Clean Architecture is overkill. The question is: does the domain have enough complexity to justify the separation? In this booking system, the answer is yes — booking invariants (time conflict, past start, cancellation state machine) are genuine domain logic that belongs separate from HTTP and SQL concerns. The test to apply: "Could I swap from EF Core to Dapper without touching the Application layer?" In this repo, yes — because repositories are interfaces and the Application layer has no `using Microsoft.EntityFrameworkCore`.

---

### Q: What is CQRS and what problem does it solve?

**Junior answer:**  
CQRS means you have separate models for reads (Queries) and writes (Commands). Commands change state; queries read state.

**Mid-level adds:**  
In this codebase, CQRS is implemented via MediatR. `CreateBookingCommand` → `CreateBookingHandler` (writes). `GetBookingQuery` → `GetBookingHandler` (reads). This separation enables the pipeline behavior pattern — `LoggingBehavior` and `ValidationBehavior` run for all commands and queries via MediatR's pipeline. Without CQRS, you'd need to wire validation into a service base class or use middleware, which is messier. The practical benefit: handlers are small, single-responsibility, and independently testable.

**Senior includes:**  
CQRS ≠ Event Sourcing (they're often confused). Full CQRS with separate read and write databases is a scaling strategy — the read model is denormalized for query performance while the write model maintains consistency. This repo uses "lightweight CQRS" — same DB for reads and writes, just separate handler classes. The tradeoff: simpler to maintain, but you don't get separate scaling of read and write paths. When would you go "full CQRS"? When read performance and write consistency have fundamentally different scaling needs — e.g., millions of reads per second but thousands of writes per second.

---

### Q: How would you handle a race condition where two users try to book the same time slot simultaneously?

**Junior answer:**  
Check if the slot is available before creating the booking.

**Mid-level adds:**  
A check-then-act is not atomic — two requests can both pass the check before either inserts. In this codebase ([`CreateBookingHandler.cs`](../../../src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs)), `GetConflictingBookingsAsync` runs before `Booking.Create` and `SaveChangesAsync`. Under concurrent load, both requests see no conflicts and both insert. This is RISK-003 in [`06-architecture-critique.md`](../03-architecture-and-patterns/06-architecture-critique.md). Fix: a database-level constraint (PostgreSQL exclusion constraint on overlapping time ranges) or a pessimistic lock (advisory lock) that serializes access per host.

**Senior includes:**  
Three strategies: optimistic locking (version column, detect conflict on UPDATE), pessimistic locking (SELECT FOR UPDATE or advisory locks), and constraint-based (DB enforces the invariant). For booking systems, constraint-based is most reliable: `EXCLUDE USING gist (host_id WITH =, tstzrange(start_time, end_time) WITH &&) WHERE (status != 'Cancelled')`. The tradeoff: DB lock vs. application-level coordination. Which is right depends on expected write throughput. At 100 bookings/second, a host-level advisory lock is a bottleneck. At 10/second, it's fine.

---

## Section 4: Debugging

---

### Q: How do you debug a 500 error in production?

**Junior answer:**  
Look at the logs.

**Mid-level adds:**  
Specifically: find the request in Serilog/Seq by time window and endpoint. Look for `@Level = 'Error'`. Find the exception type and message. Then reproduce locally with the same input. In this codebase, `ExceptionHandlingMiddleware` catches all unhandled exceptions and logs them before writing a 500 response. If it's a `ValidationException`, it became a 400, not 500 — so 500s are unexpected exceptions.

**Senior includes:**  
What to do when logs aren't enough: add structured context to narrow the problem (tenant ID, request body shape). Use correlation IDs to trace the full request path. If the exception is a DB error, check `__EFMigrationsHistory` — a missing migration causes column-not-found errors. If the 500 is intermittent, check for thread safety issues (Singleton holding Scoped state). If it happens under load, check GC pressure, connection pool exhaustion (`MaxPoolSize` in connection string), or long-held DB transactions blocking others.

---

## Section 5: System Design (Senior Focus)

---

### Q: How would you design a booking system that supports 10 million users?

**Junior answer:**  
Use a fast database and cache responses.

**Mid-level adds:**  
Identify the hotspots: available slot queries (read-heavy, can be cached), booking creation (write, must be consistent), and listing bookings (read-heavy). Cache available slots in Redis with a short TTL (60 seconds) — slight staleness is acceptable. Put booking creation behind a queue if you need to smooth write spikes. Use read replicas for listing queries.

**Senior includes:**  
At 10M users, multi-tenancy data isolation becomes a real concern — row-level security in PostgreSQL or schema-per-tenant trade different scaling/isolation tradeoffs. The double-booking problem requires a distributed lock or a write-serialized constraint at the DB level. Eventual consistency vs. strong consistency for slot availability: Calendly uses "available at time of display, first to commit wins" — the UX handles conflicts with a retry message rather than a hard lock. Observability at scale: per-tenant metrics (one busy tenant shouldn't look like a system problem), per-host slot query latency, booking creation p99. CDN for the booking page (the public-facing URL) — the slot availability query hits the API, but the page assets are static.

---

## Quick-Fire Review Round

Use these to check your depth on specific patterns. For each, identify the exact file and line where this is demonstrated:

1. Where does the `ValidationException` get caught and turned into a 400?
2. What interface makes EF Core's `ChangeTracker` find all aggregates, regardless of generic type parameter?
3. What claim key does `ICurrentTenantService` read from the JWT?
4. How many SQL queries does a successful `POST /api/bookings` execute?
5. What is the ordering guarantee in `AppDbContext.SaveChangesAsync` — does the DB write happen before or after domain event dispatch?
6. In the React UI, what happens to the `event-types` cache when a user deletes an event type?
7. What makes `Invitee.Email` lowercase — where does normalization happen?
8. Why does `IDomainEvent` extend `INotification`?
