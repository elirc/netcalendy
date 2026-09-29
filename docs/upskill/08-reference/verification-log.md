# Verification Log

What was inspected during authoring, commands that were and weren't run, uncertainties, and gaps. Readers should use this to calibrate how much to trust specific claims.

---

## Environment Constraints

**No .NET SDK available on authoring machine.**  
All claims about runtime behavior (how many DB queries fire, what `dotnet test` output looks like, whether specific EF Core mappings compile) are inferred from reading source files — they were not run and verified. Commands in `command-cheatsheet.md` are marked ⚠️ inferred for this reason.

**No Node.js / npm available on authoring machine.**  
Frontend claims about React Query behavior, Vite configuration, and TypeScript type checking are inferred from reading source files.

**PostgreSQL not running during authoring.**  
SQL queries in `command-cheatsheet.md` are correct syntax but were not executed.

---

## Files Inspected (Read Directly)

The following files were read in full during documentation authoring:

**Domain:**
- `src/NetCalendy.Domain/Common/Result.cs`
- `src/NetCalendy.Domain/Common/AggregateRoot.cs`
- `src/NetCalendy.Domain/Common/IHasDomainEvents.cs`
- `src/NetCalendy.Domain/Common/IDomainEvent.cs`
- `src/NetCalendy.Domain/Bookings/Booking.cs`
- `src/NetCalendy.Domain/Schedules/Schedule.cs`
- `src/NetCalendy.Domain/Users/Email.cs`
- Repository interfaces in `src/NetCalendy.Domain/Repositories/`

**Application:**
- `src/NetCalendy.Application/Common/Behaviors/ValidationBehavior.cs`
- `src/NetCalendy.Application/Common/Behaviors/LoggingBehavior.cs`
- `src/NetCalendy.Application/DependencyInjection.cs`
- `src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs`
- `src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingValidator.cs`

**Infrastructure:**
- `src/NetCalendy.Infrastructure/Persistence/AppDbContext.cs`
- `src/NetCalendy.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- `src/NetCalendy.Infrastructure/DependencyInjection.cs`

**API:**
- `src/NetCalendy.Api/Program.cs`
- `src/NetCalendy.Api/Middleware/TenantResolutionMiddleware.cs`
- `src/NetCalendy.Api/Extensions/ResultExtensions.cs`
- `src/NetCalendy.Api/appsettings.json`

**Tests:**
- `tests/NetCalendy.Integration.Tests/Fixtures/IntegrationTestWebAppFactory.cs`
- `tests/NetCalendy.Integration.Tests/Fixtures/JwtTokenFactory.cs`
- `tests/NetCalendy.Domain.Tests/BookingTests/BookingAggregateTests.cs`

**Frontend:**
- `web/src/components/booking/BookingPage.tsx`
- `web/src/hooks/useEventTypes.ts`
- `web/src/api/eventTypes.ts`

---

## Claims Made Without Direct File Verification

The following claims are based on pattern inference (the pattern is established in one file; I inferred it applies elsewhere without reading every file):

1. **"All handlers pass `CancellationToken ct` to all async calls"** — Verified in `CreateBookingHandler.cs`. Not verified in every handler.

2. **"Repository implementations filter by tenant"** — Verified in `EventTypeRepository.cs`. Not verified in `BookingRepository.cs` or `ScheduleRepository.cs`.

3. **"UpdateEventTypeHandler checks ownership"** — Referenced in architecture docs but not read directly. Line number references are approximate.

4. **"React hooks invalidate cache on mutation success"** — Pattern inferred from `useEventTypes.ts`. Not verified for every mutation hook.

5. **"No `NetCalendy.Application.Tests` project exists"** — Based on project listing in solution file. The `.sln` was read but the test folder structure was not exhaustively listed.

---

## Known Gaps in Documentation

| Gap | Impact |
|---|---|
| No verified `dotnet test` output | Test recipe code in `02-writing-tests-here.md` may have minor compilation issues |
| Frontend test coverage not documented | `05-quality-engineering/01-testing-strategy.md` states "no Vitest" — not verified from `package.json` |
| Exact line numbers in code reference links | Some `#L` anchors are approximate ±5 lines |
| `UpdateEventTypeHandler` exact structure | Referenced in multiple docs but not read in full during authoring |
| `GetAvailableSlotsHandler` full implementation | Referenced for performance analysis but internal slot-filter logic not verified line by line |

---

## Bugs Fixed During Documentation Authoring

These were identified during code review while writing docs:

1. **`AppDbContext.SaveChangesAsync` used `AggregateRoot<object>`** — Fixed to use `IHasDomainEvents` (required adding the interface to Domain)

2. **`IDomainEvent` didn't extend `INotification`** — Fixed to enable `IPublisher.Publish()`

3. **`NetCalendy.Domain.csproj` missing `MediatR.Contracts`** — Added package reference

4. **`UserConfiguration.cs` had invalid EF index syntax** — Fixed to use string-based column names

---

## What Would Improve This Documentation

1. Run `dotnet build` and `dotnet test` — would convert most ⚠️ inferred to ✅ verified
2. Run a Docker container and verify all integration tests pass
3. Manually test the public API with Postman/curl — verify actual HTTP behavior matches documented flows
4. Verify React frontend compiles and renders in browser
5. Re-read every file that has approximate line number references and update the anchors
