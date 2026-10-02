# Worked example: remove the conflict check

Exercise 1.1 performed for real, once, as a model of the
predict → break → observe → revert loop. Do your own run before reading past
"Predict first" — the point is that your prediction gets graded.

Environment: Windows 11, .NET SDK 10.0.302; no .NET 9 runtime installed, so
the xUnit command below runs the net9.0 test host with
`DOTNET_ROLL_FORWARD=LatestMajor`. All output in this file is pasted from
runs on 2026-10-02 against a clean clone.

## Baseline, captured before touching anything

`DOTNET_ROLL_FORWARD=LatestMajor dotnet test tests/NetCalendy.Domain.Tests`:

```
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14, Duration: 2 s - NetCalendy.Domain.Tests.dll (net9.0)
```

`dotnet run --project tests/NetCalendy.ScopeHarness --configuration Release`:

```
# tests 11
# pass 11
# fail 0
# skipped 0
```

Not part of this baseline, stated so nobody assumes otherwise:
`tests/NetCalendy.Application.Tests` does not compile on a clean clone
(missing `FluentValidation.DependencyInjectionExtensions` — exercise 1.4),
and `tests/NetCalendy.Integration.Tests` needs Docker and was not run.

## Predict first

The edit removes the only enforcement point named in
[CODE-TOUR.md](../CODE-TOUR.md) §4 — the check-then-refuse in
`CreateBookingHandler`. The plausible review comment it mimics: *"availability
already filters out conflicting slots before the client ever sees them, so
re-checking at booking time is redundant — simplify."*

Write down: which of the two green suites fails, how many cases, and why the
other one stays green. Spoiler break below.

---

## The break

One deletion in
`src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs`
(lines 33-35):

```diff
         var endTime = cmd.StartTime.AddMinutes(eventType.Duration.Minutes);

-        var conflicts = await bookings.GetConflictingBookingsAsync(hostId, cmd.StartTime, endTime, ct);
-        if (conflicts.Count > 0)
-            return Result.Failure<CreateBookingResponse>(Error.Conflict("Booking.TimeConflict", "This time slot is no longer available."));
-
         var inviteeResult = Invitee.Create(cmd.InviteeName, cmd.InviteeEmail, cmd.InviteeTimezone, cmd.Notes);
```

## What actually happened

ScopeHarness, with the break in place:

```
ok - matching active event creates and saves the correctly scoped booking
not ok - matching active event retains existing conflict rejection: System.Exception: existing conflict behavior changed
...
# tests 11
# pass 10
# fail 1
# skipped 0
```

Exactly one case failed — the harness seeds an existing booking for the same
host and time, posts a second one, and asserts the handler refuses. With the
check gone, the handler happily saved the duplicate.

Domain tests, with the break still in place:

```
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14, Duration: 822 ms - NetCalendy.Domain.Tests.dll (net9.0)
```

Green. That is the second half of the lesson: `Booking.ConflictsWith` — the
overlap math itself — is intact and fully pinned, but the math only matters
if someone calls it. The domain layer cannot know it stopped being consulted.
`CreateBookingHandlerTests` would have caught this too
(`Handle_WithConflictingBooking_ShouldReturnConflict`), but that suite does
not compile on a clean clone, so on this machine the ScopeHarness was the
*only* runnable guard on the enforcement point. One shallow build break
silently halved the coverage of the system's most important promise — worth a
moment before moving on.

## What this run does not show

The deliberate break was sequential: one request after another. No suite here
fires two *simultaneous* requests, so this run says nothing about the race in
CODE-TOUR §4 — a healthy, unedited handler still has that window. The
two-process test that would show it is exercise 3.1, and until it exists,
"the harness is green" must never be reported as "double booking is
impossible."

## Restore and confirm

```
git checkout -- src/NetCalendy.Application/Bookings/Commands/CreateBooking/CreateBookingHandler.cs
dotnet run --project tests/NetCalendy.ScopeHarness --configuration Release
git status --short
```

Captured after reverting:

```
# tests 11
# pass 11
# fail 0
# skipped 0
```

`git status --short` showed nothing under `src/`. Never leave the break in,
and never commit a run with it present.

## Doing this yourself

1. Confirm a clean tree: `git status --short` shows nothing under `src/`.
2. Capture both baselines; record the counts, not "it passed".
3. Write your prediction down.
4. Make the one deletion. Run both suites; record which cases failed, with
   the suite's own wording.
5. Revert, rerun, confirm 11/11 and 14/14 and a clean tree.
6. Grade your prediction in writing — especially if you predicted the domain
   tests would catch it.
