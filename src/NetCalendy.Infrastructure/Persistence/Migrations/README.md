# Migrations

Generate the first migration after setting up the database:

```bash
dotnet ef migrations add InitialCreate \
  --project src/NetCalendy.Infrastructure \
  --startup-project src/NetCalendy.Api \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/NetCalendy.Infrastructure \
  --startup-project src/NetCalendy.Api
```
