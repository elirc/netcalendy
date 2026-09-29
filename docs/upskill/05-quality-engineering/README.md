# Quality Engineering

This section covers what separates software that works from software that can be maintained, debugged, and trusted in production.

## Contents

| File | Topic |
|---|---|
| [01-testing-strategy.md](01-testing-strategy.md) | Test pyramid, what belongs where, fixtures and isolation |
| [02-writing-tests-here.md](02-writing-tests-here.md) | 7 concrete recipes using existing helpers |
| [03-systematic-debugging.md](03-systematic-debugging.md) | 5 debugging scenarios specific to this codebase |
| [04-performance-thinking.md](04-performance-thinking.md) | Hotspots, N+1 queries, unbounded memory loads |
| [05-security-checklist.md](05-security-checklist.md) | IDOR, auth, CSRF, rate limiting tied to real code |
| [06-observability-and-operations.md](06-observability-and-operations.md) | Serilog, health checks, deploy, rollback |

## Why This Matters at Each Level

**Junior:** Write tests that pass. Know that tests exist and why they run.

**Mid-level:** Know *which test type* to write for each scenario. Write tests that catch real bugs, not just tests that pass. Diagnose a failing test with a strategy, not random trial and error.

**Senior:** Design the test infrastructure. Identify coverage gaps that matter. Recognize when the cost of a test exceeds its value. Own observability — know what you need to see when something breaks at 3am.
