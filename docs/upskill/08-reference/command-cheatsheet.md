# Command Cheatsheet

Commands you'll use regularly. Status: ✅ verified (run and confirmed working), ⚠️ inferred (from configuration/code, not run during doc authoring — .NET SDK not available on this machine).

---

## .NET — Build and Test

| Command | What it does | Status |
|---|---|---|
| `dotnet build` | Compile all projects in solution | ⚠️ inferred |
| `dotnet test` | Run all tests in all test projects | ⚠️ inferred |
| `dotnet test --filter "Category=Integration"` | Run only integration tests | ⚠️ inferred |
| `dotnet test --filter "Category=Unit"` | Run only unit tests | ⚠️ inferred |
| `dotnet run --project src/NetCalendy.Api` | Start the API | ⚠️ inferred |
| `dotnet watch run --project src/NetCalendy.Api` | Start with hot reload | ⚠️ inferred |

---

## .NET — EF Core Migrations

⚠️ All migration commands require .NET SDK + `dotnet-ef` tool.

Install tool (once):
```bash
dotnet tool install --global dotnet-ef
```

| Command | What it does |
|---|---|
| `dotnet ef migrations add <Name> -p src/NetCalendy.Infrastructure -s src/NetCalendy.Api` | Create a new migration |
| `dotnet ef database update -p src/NetCalendy.Infrastructure -s src/NetCalendy.Api` | Apply pending migrations |
| `dotnet ef database update <MigrationName> -p src/NetCalendy.Infrastructure -s src/NetCalendy.Api` | Roll back to a specific migration |
| `dotnet ef migrations list -p src/NetCalendy.Infrastructure -s src/NetCalendy.Api` | List all migrations and their applied status |
| `dotnet ef migrations script -p src/NetCalendy.Infrastructure -s src/NetCalendy.Api` | Generate SQL script from migrations |

**Notes:**
- `-p` is the Infrastructure project (has `AppDbContext`)
- `-s` is the API project (has `appsettings.json` with connection string)
- Requires a running PostgreSQL on the connection string in `appsettings.Development.json`

---

## .NET — User Secrets (never commit real secrets)

```bash
# Initialize (once per project)
dotnet user-secrets init --project src/NetCalendy.Api

# Set a secret
dotnet user-secrets set "Jwt:Key" "your-real-secret-key-32-chars-min" --project src/NetCalendy.Api

# Set database connection string
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Database=netcalendy_dev;Username=postgres;Password=yourpassword" --project src/NetCalendy.Api

# List all secrets
dotnet user-secrets list --project src/NetCalendy.Api
```

---

## Docker

| Command | What it does |
|---|---|
| `docker run --rm -e POSTGRES_PASSWORD=password -p 5432:5432 postgres:16-alpine` | Start a local PostgreSQL container |
| `docker run --rm -e ACCEPT_EULA=Y -p 5341:80 datalust/seq` | Start Seq log viewer at http://localhost:5341 |
| `docker ps` | List running containers |
| `docker stop <container-id>` | Stop a container |

---

## React / Frontend

Run from `web/` directory:

| Command | What it does | Status |
|---|---|---|
| `npm install` | Install dependencies | ⚠️ inferred |
| `npm run dev` | Start Vite dev server (usually port 5173) | ⚠️ inferred |
| `npm run build` | Production build | ⚠️ inferred |
| `npm run typecheck` | TypeScript type check (no emit) | ⚠️ inferred |
| `npm test` | Run Vitest (if configured) | ⚠️ inferred — Vitest not yet in package.json |

---

## Git

```bash
# Create a feature branch
git checkout -b feature/ticket-001-update-event-type-validator

# Stage specific files (not all)
git add src/NetCalendy.Application/EventTypes/Commands/UpdateEventType/UpdateEventTypeValidator.cs

# Commit with a meaningful message
git commit -m "Add UpdateEventTypeValidator to prevent invalid name/duration input"

# Push branch
git push -u origin feature/ticket-001-update-event-type-validator
```

---

## PostgreSQL (direct query — useful for debugging)

Connect to local DB:
```bash
psql -h localhost -U postgres -d netcalendy_dev
```

Useful queries:
```sql
-- Check migration history
SELECT * FROM "__EFMigrationsHistory" ORDER BY "MigrationId";

-- See all tenants
SELECT id, name, slug FROM tenants;

-- Count bookings by status
SELECT status, COUNT(*) FROM bookings GROUP BY status;

-- Find bookings for a specific host
SELECT id, start_time, end_time, status FROM bookings WHERE host_id = '<guid>';

-- Check for overlapping bookings (conflict check)
SELECT id, start_time, end_time FROM bookings
WHERE host_id = '<guid>'
  AND status != 'Cancelled'
  AND start_time < '<end>' AND end_time > '<start>';
```
