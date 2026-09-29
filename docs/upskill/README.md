# NetCalendy — Upskill Curriculum

## What This Is

A structured training lab that uses the NetCalendy codebase as the vehicle to move a learner from junior through mid-level and toward senior engineer. Every concept is anchored to a real file in this repo. The curriculum teaches transferable judgment, not just "here is where the thing lives."

## The Repo in 8 Sentences

NetCalendy is a Calendly-style booking SaaS: hosts define event types (e.g., "30-minute chat"), publish scheduling links, and invitees book slots without creating an account. It is built on **.NET 9 / ASP.NET Core** with **React + Vite + TypeScript** on the frontend. The backend follows **Clean Architecture** — Domain, Application, Infrastructure, and API layers that form a strict inward dependency cone. Business logic lives in the **Domain** layer as aggregates with enforced invariants and domain events; commands and queries live in the **Application** layer using **CQRS via MediatR**. All writes are validated by **FluentValidation** injected as a MediatR pipeline behavior. Persistence uses **EF Core 9 + PostgreSQL** (Npgsql), with tenant isolation enforced per-row by `TenantId` columns. **Multi-tenancy** is resolved from the `X-Tenant-Slug` HTTP header or subdomain, added to the JWT, and read back by `ICurrentTenantService`. The test suite spans three layers: pure domain unit tests, application handler tests with NSubstitute mocks, and full integration tests using `WebApplicationFactory` + **Testcontainers** (a real Postgres instance).

## Learning Stages

| Stage | Mental Question | Observable Behavior |
|---|---|---|
| **Orientation** | "Can I run it and name the parts?" | Boot app, call one endpoint, trace one request |
| **Junior** | "How do I make this work?" | Add a field, add a handler, copy existing pattern |
| **Mid-level** | "Is this the right pattern?" | Design cross-layer change, write integration tests, debug from logs |
| **Senior** | "What does this commit us to? Who pays? How do we reduce risk?" | Critique boundaries, identify latent bugs, propose migrations, write RFCs |

Senior mindset line: **a junior ships it; a mid-level asks if it's correct; a senior asks what breaks three months later and whether there's a cheaper way to find out.**

## Recommended Entry Points

| You are… | Start here |
|---|---|
| Brand-new junior (C# unfamiliar) | `00-fast-track.md` → `02-stack-and-language-mastery/01-dotnet-runtime-model.md` → `01-codebase-cartography/` in order |
| Junior who knows C# but not ASP.NET Core | `00-fast-track.md` → `02-stack-and-language-mastery/02-framework-mental-models.md` → `01-codebase-cartography/05-key-flows.md` |
| Mid-level, new to this repo | `00-fast-track.md` → `01-codebase-cartography/01-system-map.md` → `03-architecture-and-patterns/` in order → contribution tickets |
| Senior doing architecture review | `01-codebase-cartography/01-system-map.md` → `03-architecture-and-patterns/06-architecture-critique.md` → `08-reference/risk-register.md` → senior build projects |

## Conventions

- **File anchors** — every claim points to a real path + line: `[Booking.cs:44](../../src/NetCalendy.Domain/Bookings/Booking.cs#L44)`. If a claim has no anchor, treat it as unverified.
- **Fake code** — invented code that doesn't exist in the repo is labeled `// Illustrative fake code: not from this repo`.
- **Drills** — look for the 🔧 symbol; these are hands-on exercises, not reading.
- **Self-grading** — every drill ends with Basic / Solid / Strong rubric.
- **Verification notes** — see `08-reference/verification-log.md` for what was inspected and what was inferred.
- **"Investigate"** — labels a claim that may be wrong or incomplete and should be verified before teaching.

## Curriculum Map

```
00-fast-track.md                  ← Start here. Weekend-sized.
01-codebase-cartography/          ← Know the map before you run.
02-stack-and-language-mastery/    ← Runtime, framework, type system, tooling.
03-architecture-and-patterns/     ← Why this shape? What could go wrong?
04-code-reading-gym/              ← Active practice: annotate, trace, review.
05-quality-engineering/           ← Test, debug, measure, secure.
06-contribution-practice/         ← Real tickets, projects, katas.
07-career-and-collaboration/      ← Reviews, PRs, communication, interviews.
08-reference/                     ← Cheatsheets, risks, rubrics, log.
```

## Time Estimates

| Track | Duration | Path |
|---|---|---|
| Weekend win | 6–8 hrs | `00-fast-track` + one key flow + one drill |
| Two-week sprint | ~30 hrs | `00` → `01` → `02` → `03` → 3 tickets from `06-01` |
| Eight-week course | ~80 hrs | Full curriculum, one senior project, interview prep |
| Ongoing reference | Unbounded | Revisit risk register, add tickets, iterate on critique |
