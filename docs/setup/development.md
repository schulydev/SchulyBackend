# Development setup

Run the API and its dependencies locally.

## Prerequisites

- **.NET 10 SDK**
- **Docker** (for Postgres; the dev compose file also starts SeaweedFS and a local
  SchulwareAPI instance)
- Optional: `dotnet-ef` global tool for [migrations](../migrations.md)
  (`dotnet tool install --global dotnet-ef`)

## 1. Bring up the dependencies

```sh
docker compose -f compose.dev.yml up -d
```

`compose.dev.yml` starts:

- **Postgres** (`postgres:18.1`) on host port `2406`, database `schuly-dev`.
- **SeaweedFS** S3 (document storage) on `8333`.
- **SchulwareAPI** (Schulnetz bridge used by the Schulware plugin) on `8000`.

## 2. Run the API

```sh
cd src/Schuly.API
dotnet run --urls=http://localhost:5033
```

On startup the API applies EF Core migrations (`ApplyMigrations()` in `Program.cs`)
and seeds the school-systems catalog, so the database is ready on first run.

In Development, request logging and the API reference UI are enabled, and an opt-in
fake-OIDC path (`DevAuth`) lets you mint local tokens via `/api/dev/token` instead of
contacting a real identity provider. See [Configuration](configuration.md).

## API reference

- **Scalar UI**: <http://localhost:5033/scalar>
- **OpenAPI 3.0 document**: <http://localhost:5033/openapi/v1.json> (the Dart client is
  generated from this document)

The OpenAPI doc is produced by the built-in `Microsoft.AspNetCore.OpenApi`.

## Tests

```sh
dotnet test
```
