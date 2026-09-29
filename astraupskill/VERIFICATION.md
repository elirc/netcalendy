# Verification scope and reproduction

The focused command is `dotnet run --project tests/NetCalendy.ScopeHarness
--configuration Release` from this project root. The harness targets .NET 10,
the SDK installed during this task. The application and its existing test projects
continue targeting .NET 9. No target-framework upgrade is part of the production
change. The captured run completed successfully: eleven tests passed, zero failed
and zero skipped. The linked record contains the exact command and timestamps.

Read [the harness project](../tests/NetCalendy.ScopeHarness/NetCalendy.ScopeHarness.csproj)
to see the linked production files. It compiles the domain, the two handlers,
their command/query records and the current-tenant/unit-of-work interfaces.
The [mediator contracts](../tests/NetCalendy.ScopeHarness/MediatorContracts.cs)
are minimal test-only signatures. They do not implement MediatR behavior or
its validation pipeline. The [program](../tests/NetCalendy.ScopeHarness/Program.cs)
uses explicit repository and unit-of-work doubles and exits nonzero on an
assertion failure. Its printed totals count runtime cases, not restore or build
steps. Compiler success is useful additional evidence for this selected slice.

Eight negative cases pair missing, foreign-host, foreign-tenant and inactive
events with booking and availability. Three positive or neighboring cases cover
correctly scoped persistence, existing booking conflict handling and empty
availability when a valid event has no schedule. Inspect both the returned error
and downstream call counters. This checks that rejection happens deliberately
before calendar reads or writes instead of relying on a later dependency error.

The check does not execute the original xUnit suites, ASP.NET controllers,
authentication, tenant middleware, FluentValidation, EF Core, PostgreSQL or a
browser. It does not prove booking availability membership or simultaneous-write
safety. The original handler snapshots permit before/after review, and the
captured command record identifies the environment used. A future integration
run should restore the original project's dependencies and use its intended
runtime and disposable database; report those results independently rather
than folding them into these focused handler cases.

## Accepted execution

Read the [eleven-case output](evidence/crud-b02-scope-harness.log) and [command record with limits](evidence/results.json). The command compiled the selected source and exited zero. No dependency-install step is counted as a test.
