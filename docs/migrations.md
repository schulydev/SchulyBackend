# Migrations

EF Core migrations live in `Schuly.Infrastructure`, with `Schuly.API` as the startup
project. Helper scripts in `scripts/` wrap the `dotnet ef` commands.

## Add / manage migrations (bash)

`scripts/migration.sh` uses subcommands:

```sh
./scripts/migration.sh add <Name>      # add a new migration
./scripts/migration.sh remove          # remove the last migration
./scripts/migration.sh list            # list all migrations
./scripts/migration.sh update [Name]   # apply migrations (optionally up to <Name>)
./scripts/migration.sh drop            # drop the database
```

Each subcommand targets `--project src/Schuly.Infrastructure --startup-project
src/Schuly.API`.

## Windows

- `scripts/migration.ps1` / `scripts/migration.bat` — PowerShell / batch equivalents.
- `scripts/DbScript.ps1` — wraps DB lifecycle helpers (start/stop/recreate the dev
  database, add migration, delete migrations, full reset). Run with `-help` for the
  command list.

## Applied at startup

Migrations are applied automatically when the API starts: `ApplyMigrations()` in
`Program.cs` calls `db.Database.Migrate()`. The school-systems catalog is seeded
afterward (seed-if-missing). No manual apply step is needed in development or
production.

## Prerequisites

- The `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`.
- A running Postgres reachable via the `SchulyDatabase` connection string (see
  [Configuration](setup/configuration.md)). For local work, start it with
  `docker compose -f compose.dev.yml up -d`.
