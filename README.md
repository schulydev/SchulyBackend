# <p align="center">SchulyBackend</p>
<p align="center">
  <img src="./assets/app_icon.png" width="160" alt="Schuly Logo">
</p> 
<p align="center">
  <strong>ASP.NET Core backend powering the Schuly ecosystem</strong>
</p>
<p align="center">
  <a href="https://github.com/schulydev/SchulyBackend/stargazers"><img src="https://img.shields.io/github/stars/schulydev/SchulyBackend?style=flat&color=3da8ff" alt="GitHub stars"/></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-9.0-3da8ff" alt=".NET"/></a>
  <a href="https://schuly.dev"><img src="https://img.shields.io/badge/site-schuly.dev-3da8ff" alt="Website"/></a>
</p>

Clean-architecture C# API serving the Schuly mobile app. Built on ASP.NET Core with CQRS via Mediator, EF Core on PostgreSQL, OIDC authentication, and an extensible plugin runtime.

## What's in this repo

- `src/Schuly.API` - ASP.NET Core entry point + controllers
- `src/Schuly.Application` - CQRS commands/queries + handlers
- `src/Schuly.Domain` - entities (`School`, `Class`, `Exam`, `Grade`, `Absence`, `AgendaEntry`, ...)
- `src/Schuly.Infrastructure` - EF Core, OIDC, plugin host
- `src/Schuly.Tests` - unit + integration tests

## The Schuly ecosystem

| Repo | Purpose |
|---|---|
| [**Schuly**](https://github.com/schulydev/Schuly) | Flutter mobile app |
| [**SchulyBackend**](https://github.com/schulydev/SchulyBackend) | ASP.NET Core API backend *(this repo)* |
| [**SchulyPluginAbstractions**](https://github.com/schulydev/SchulyPluginAbstractions) | Plugin contract (NuGet) |
| [**SchulyPlugins**](https://github.com/schulydev/SchulyPlugins) | Official plugins monorepo |
| [**SchulyWebsite**](https://github.com/schulydev/SchulyWebsite) | Landing site ([schuly.dev](https://schuly.dev)) |

## Run

```sh
# Spin up Postgres + SeaweedFS (document storage):
docker compose -f compose.dev.yml up -d

# Configure secrets once (see "Secrets" below)
cd src/Schuly.API
dotnet run --urls=http://localhost:5033
```

API reference (Scalar): `http://localhost:5033/scalar` · OpenAPI document: `http://localhost:5033/openapi/v1.json`

## Secrets

Sensitive config (OIDC client, DB connection, S3 credentials) lives in
`dotnet user-secrets`, **not** in `appsettings.*.json`. Set these once per
machine:

```sh
cd src/Schuly.API

# OIDC (Pocket ID etc.)
dotnet user-secrets set "Oidc:Authority" "https://your-oidc-provider"
dotnet user-secrets set "Oidc:ClientId" "your-client-id"

# Database
dotnet user-secrets set "ConnectionStrings:SchulyDatabase" \
    "Host=localhost;Port=2406;Database=schuly-dev;Username=postgres;Password=…"

# S3 / SeaweedFS (document storage) - matches compose.dev.yml defaults
dotnet user-secrets set "S3:Endpoint"     "http://localhost:8333"
dotnet user-secrets set "S3:Bucket"       "schuly-documents"
dotnet user-secrets set "S3:AccessKey"    "schuly-dev-access"
dotnet user-secrets set "S3:SecretKey"    "schuly-dev-secret"
dotnet user-secrets set "S3:UsePathStyle" "true"
```

`dotnet user-secrets list` shows everything currently set.

## Document storage (SeaweedFS)

Student documents are stored in a self-hosted
**SeaweedFS** instance - S3-compatible, Apache 2.0 licensed, single binary.
The default `compose.dev.yml` runs it locally on `:8333` with the credentials
from the section above.

Static dev credentials live in `scripts/seaweedfs/s3-config.json`; blob data
is bind-mounted to `./data/seaweedfs/` on the host (gitignored) so it
persists across container rebuilds.

The backend **proxies all bytes** through itself - clients never see S3 URLs
and never connect to SeaweedFS directly. Upload: `POST /api/students/{id}/documents`
(multipart). Download: `GET /api/documents/{id}` (file response).

For production, swap the `S3:*` user-secrets to point at AWS S3, Cloudflare R2,
or a hardened SeaweedFS deployment - no code change.

## School systems catalog

The CRM is provider-agnostic: which login systems the app offers is **supplied by
the loaded plugins**, not baked into the backend. Each plugin describes the system
it serves via its `IPluginLogin.SchoolSystem` descriptor; on load the backend
collects these and seeds the catalog (seed-if-missing by `Key`, so anything an
admin edits afterwards is left untouched). Install a plugin and its system appears
in the picker - no operator config required.

A descriptor carries everything the app needs to render the system and its login
form. `PrivateAuthStrategy` tells the app how private mode fetches data: `"token"`
(a headless login mints a bearer token + refreshable session) or `"scrape"`
(credentials replayed per fetch).

```csharp
// in the plugin's IPluginLogin implementation
public SchoolSystemDescriptor SchoolSystem => new()
{
    Key = "schulnetz",                  // must match SystemKey
    DisplayName = "Schulnetz",
    LoginMethod = "credentials",        // or "oauth-webview"
    PrivateAuthStrategy = "token",      // "token" | "scrape"
    StatelessBasePath = "/api/plugins/schulware/stateless",
    PluginBasePath = "/api/plugins/schulware",
    SortOrder = 0,
    LoginFields =
    [
        new() { Key = "baseUrl",  Label = "Schulnetz URL", Type = "url",      Required = true },
        new() { Key = "email",    Label = "Email",         Type = "text",     Required = true },
        new() { Key = "password", Label = "Password",      Type = "password", Required = true },
    ],
};
```

An operator can still add or override **custom** (non-plugin) systems via a
`SchoolSystems` config section (`appsettings.Production.json`, environment
variables, or user-secrets). It seeds first, so a config entry with the same `Key`
wins over a plugin's default; the shipped `appsettings.json` contains none.

## Migrations

```sh
./scripts/migration.sh    # or migration.ps1 on Windows
```
