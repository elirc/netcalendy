# 04 — Runtime and Tooling Map

## .NET SDK and Project System

**SDK version:** .NET 9.0 — inferred from `<TargetFramework>net9.0</TargetFramework>` in all `.csproj` files.

**No `global.json`:** There is no `global.json` in the repo root. This means the SDK version is unspecified at the solution level — any installed .NET 9 SDK will be used. Risk: a developer with .NET 8 would get a build error. Best practice: add `global.json` to pin the SDK version.

**Build system:** MSBuild invoked via the .NET SDK (`dotnet build`). Each `.csproj` is a self-contained build description. No `Directory.Build.props` or `Directory.Packages.props` — package versions are specified per project.

**Central package management:** Not used. Each project pins its own versions. Drift risk: `MediatR` version in Application (12.4.1) must match `MediatR.Contracts` in Domain (2.0.1) — these are designed to be compatible but could drift.

**Package restore:** `dotnet restore` downloads NuGet packages to the global cache (`~/.nuget/packages/`). First restore is slow; subsequent builds use the cache.

---

## Runtime Boundaries

| Boundary | Process | Entrypoint | Lifetime |
|---|---|---|---|
| API server | .NET CLR process | `Program.cs → WebApplication.Run()` | Long-running service |
| Integration test host | .NET CLR process | xUnit test runner + `WebApplicationFactory<Program>` | One process per test assembly run |
| Testcontainers Postgres | Docker container | `PostgreSqlContainer.StartAsync()` | One per `IntegrationTestWebAppFactory` lifecycle |
| React dev server | Node.js process | `vite` | Development only |
| React prod | nginx (Docker) | nginx.conf | Production |

---

## Dependency Injection (DI) System

ASP.NET Core's built-in DI container. Registrations split across:

| File | What it registers |
|---|---|
| [`src/NetCalendy.Application/DependencyInjection.cs`](../../src/NetCalendy.Application/DependencyInjection.cs) | MediatR (assembly scan), FluentValidation (assembly scan), pipeline behaviors |
| [`src/NetCalendy.Infrastructure/DependencyInjection.cs`](../../src/NetCalendy.Infrastructure/DependencyInjection.cs) | `AppDbContext`, repositories (scoped), `ICurrentUserService` (scoped), `ICurrentTenantService` (scoped), `IDateTimeProvider` (singleton) |
| [`src/NetCalendy.Api/Program.cs`](../../src/NetCalendy.Api/Program.cs) | JWT auth, Serilog, CORS, Swagger, controllers |

**Lifetime rules:** `AppDbContext` → scoped. Repositories → scoped (correct — they hold DbContext). `ICurrentTenantService` → scoped (correct — reads `IHttpContextAccessor` which is scoped per request). `IDateTimeProvider` → singleton (correct — no per-request state).

---

## Configuration System

**Files:**
- [`src/NetCalendy.Api/appsettings.json`](../../src/NetCalendy.Api/appsettings.json) — base config
- [`src/NetCalendy.Api/appsettings.Development.json`](../../src/NetCalendy.Api/appsettings.Development.json) — dev overrides (more verbose logging)

**Secrets in config:** `appsettings.json` contains a placeholder JWT key: `"REPLACE_WITH_A_SECRET_KEY_AT_LEAST_32_CHARS"`. This must be overridden via User Secrets (`dotnet user-secrets`) or environment variables in production. **Never commit a real secret to this file.**

**Reading config in Program.cs:** `builder.Configuration.GetSection("Jwt")` at line ~26. `builder.Configuration.GetConnectionString("DefaultConnection")` in Infrastructure DI.

---

## npm / Vite Frontend Tooling

**Package manager:** npm (no `pnpm-lock.yaml` or `yarn.lock`). `package.json` at `web/package.json`.

**Bundler:** Vite 6.x. Config at [`web/vite.config.ts`](../../web/vite.config.ts). TypeScript paths aliased: `@/*` → `src/*`.

**Dev proxy:** Vite proxies `/api/*` to `http://localhost:5000` — no CORS issues in development.

**TypeScript:** Strict mode enabled. `noUnusedLocals` and `noUnusedParameters` are `true` — the compiler will error on unused imports. `moduleResolution: bundler` requires TypeScript 5.0+.

**Scripts:**
```bash
npm run dev        # Vite dev server
npm run build      # tsc + vite build → dist/
npm run preview    # Preview production build
npm run lint       # ESLint
npm run type-check # tsc --noEmit (no emit, just type errors)
```

---

## Authentication / JWT Flow

JWT Bearer tokens issued externally (no auth server in this repo). Claims required:
- `sub` — user ID (GUID)
- `tenant_id` — tenant ID (GUID)
- `tenant_slug` — tenant slug string

Config keys: `Jwt:Key` (HS256 symmetric), `Jwt:Issuer`, `Jwt:Audience`.

**Test token factory:** [`tests/NetCalendy.Integration.Tests/Fixtures/JwtTokenFactory.cs`](../../tests/NetCalendy.Integration.Tests/Fixtures/JwtTokenFactory.cs) — uses the same hardcoded key as `appsettings.json`. This key **must match** between tests and the running app.

---

## Docker

**Development:** `docker-compose.dev.yml` — only Postgres and Seq. No API or web container.

**Production:** `docker-compose.yml` — Postgres, Seq, API (multi-stage Dockerfile), nginx/web. Requires `JWT_KEY` env var from `.env`.

**API Dockerfile:** `src/NetCalendy.Api/Dockerfile` — standard multi-stage: `sdk:9.0` to restore/build/publish, `aspnet:9.0` as final base.
