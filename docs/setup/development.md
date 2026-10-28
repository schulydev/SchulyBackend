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

Sign-in mails (verification, password reset) go to a local SMTP catcher on
`localhost:1025`, as set in `appsettings.Development.json`. It is not part of
`compose.dev.yml`; start one with, for example:

```sh
docker run -p 1025:1025 -p 8025:8025 axllent/mailpit
```

The inbox is at <http://localhost:8025>. Links in the mails point at
`http://localhost:4200`.

## 2. Point the API at them

No connection string ships in the repo, so a fresh clone stops on startup with
`The ConnectionString property has not been initialized.` Store the local values in
[user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), which
keeps them out of git:

```sh
cd src/Schuly.API
dotnet user-secrets set "ConnectionStrings:SchulyDatabase" "Host=localhost;Port=2406;Database=schuly-dev;Username=postgres;Password=d4vpas8w0rd13!!!"
dotnet user-secrets set "LocalLogin:SigningKey" "$(openssl rand -base64 32)"
dotnet user-secrets set "TwoFactor:EncryptionKey" "$(openssl rand -base64 32)"
```

The connection string is just `compose.dev.yml` restated. The two keys sign access
tokens and encrypt two-factor secrets. Keep them once set: a new signing key signs
everybody out, and a new encryption key breaks two-factor. Environment variables are an
equivalent alternative - see [Configuration](configuration.md).

## 3. Run the API

```sh
cd src/Schuly.API
dotnet run --urls=http://localhost:5033
```

On startup the API applies EF Core migrations (`ApplyMigrations()` in `Program.cs`)
and seeds the school-systems catalog, so the database is ready on first run.

In Development, request logging and the API reference UI are enabled.

## Getting a token

Register through the web app, or call the sign-in endpoints directly:

```sh
curl -X POST http://localhost:5033/auth/register -H "Content-Type: application/json" \
  -d '{"userName":"dev","email":"dev@example.com","password":"<password>"}'
curl -X POST http://localhost:5033/auth/login -H "Content-Type: application/json" \
  -d '{"identifier":"dev","password":"<password>"}'
```

The login response carries the access token. Send it as `Authorization: Bearer` to
`/api/*`, or paste it into the Scalar UI. See [Configuration](configuration.md#sign-in).

## API reference

- **Scalar UI**: <http://localhost:5033/scalar>
- **OpenAPI 3.0 document**: <http://localhost:5033/openapi/v1.json> (the Dart client is
  generated from this document)

The OpenAPI doc is produced by the built-in `Microsoft.AspNetCore.OpenApi`.

## Tests

```sh
dotnet test
```

The suite is [TUnit](https://tunit.dev/), which runs on Microsoft.Testing.Platform.
The .NET 10 SDK no longer runs those projects through the old VSTest path, so
`global.json` opts the repo into MTP mode - without it `dotnet test` fails with
*"Testing with VSTest target is no longer supported"*. To run the project directly
instead:

```sh
dotnet run --project src/Schuly.Tests
```
