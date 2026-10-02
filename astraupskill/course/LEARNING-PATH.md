# NetCalendy course: time plus concurrency

Your central question: **two invitees book the host's last open slot at the
same moment, from two different time zones, on the night the clocks change —
who gets it, what does the loser see, and how would you prove neither one got
a phantom booking?**

This repo cannot fully answer that question yet, and that is the point. The
course walks you to the exact line where the answer currently lives
(an application-level check with a race window — see the
[code tour](CODE-TOUR.md)), shows you a real break caught by a real suite
(the [worked example](worked-examples/remove-the-conflict-check.md)), and then
hands you the missing pieces as [exercises](EXERCISES.md), including the
two-process race test the codebase does not have.

This course pairs with the three-rebuild curriculum: it is stockade's
last-unit race, pointed at time instead of inventory, with gitjira's
compare-and-swap discipline as the fix you will be asked to design.

## What already exists — read it, do not redo it

- [../README.md](../README.md) and the chapters next to it: the
  relationship-validation lab (event type must belong to the resolved tenant
  **and** the requested host). It built the
  [ScopeHarness](../../tests/NetCalendy.ScopeHarness/Program.cs) this course
  reuses, and [../VERIFICATION.md](../VERIFICATION.md) records what that
  harness does and does not prove.
- [../../docs/upskill/README.md](../../docs/upskill/README.md): the general
  curriculum — architecture, cartography, testing strategy, career ladder.
  Its `05-quality-engineering/` chapters are the background reading for
  everything here.

Neither covers what this course covers: **how this codebase handles time
(UTC boundaries, DST, time zones) and simultaneous writes**, and where those
two guarantees actually stop.

## Sequence

1. **Trace the promise.** Read [CODE-TOUR.md](CODE-TOUR.md) with the files
   open. It follows "no host is double-booked" from the React client to the
   Postgres schema, and names the step where the promise is thinner than the
   README's "production-grade" framing suggests. Before reading §4, write
   down where *you* believe double-booking is prevented.
2. **Establish the baseline.** Run the three commands in EXERCISES.md's
   baseline block. Two suites run on this machine; one does not compile and
   one needs Docker — the exercises treat both facts as material, not noise.
3. **Break it on purpose.** Exercises Tier 1. Do 1.1 yourself before reading
   the [worked example](worked-examples/remove-the-conflict-check.md); the
   model run is for calibrating how you observe and report, not for spoiling
   your prediction.
4. **Extend inside the boundaries.** Tier 2: buffer times, a cancellation
   window, an .ics export with a correct `TZID`. Tests first, in the style of
   the existing suites.
5. **Move a boundary.** Tier 3: the two-process booking race test (stockade's
   method, this domain's stakes), recurring availability overrides, and
   calendar sync as an external-effect problem (relay-rebuild's provider
   lesson).

## Honesty rules (inherited from the rebuild course)

- Every quoted test count or error in these documents comes from a run that
  actually happened on this machine; the environment is named next to the
  numbers.
- "Exercise" means the code does not contain it yet. Nothing below promises
  the repo does something it does not.
- A found gap outranks a flattering tour. This course contains several.
