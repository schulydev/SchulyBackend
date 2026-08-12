# <p align="center">SchulyBackend</p>
<p align="center">
  <img src="./assets/app_icon.png" width="160" alt="Schuly Logo">
</p> 
<p align="center">
  <strong>ASP.NET Core backend powering the Schuly ecosystem</strong>
</p>
<p align="center">
  <a href="https://github.com/schulydev/SchulyBackend/stargazers"><img src="https://img.shields.io/github/stars/schulydev/SchulyBackend?style=flat&color=3da8ff" alt="GitHub stars"/></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-3da8ff" alt=".NET"/></a>
  <a href="https://docs.schuly.dev/SchulyBackend/"><img src="https://img.shields.io/badge/docs-docs.schuly.dev-3da8ff" alt="Documentation"/></a>
  <a href="https://schuly.dev"><img src="https://img.shields.io/badge/site-schuly.dev-3da8ff" alt="Website"/></a>
</p>

Clean-architecture C# API serving the Schuly mobile app. Built on ASP.NET Core with CQRS via Mediator, EF Core on PostgreSQL, OIDC authentication, and an extensible plugin runtime.

## What's in this repo

- `src/Schuly.API` - ASP.NET Core entry point + controllers
- `src/Schuly.Application` - CQRS commands/queries + handlers
- `src/Schuly.Domain` - entities (`School`, `Class`, `Exam`, `Grade`, `Absence`, `AgendaEntry`, ...)
- `src/Schuly.Infrastructure` - EF Core, OIDC, plugin host
- `src/Schuly.Tests` - unit + integration tests

## Quick start

```sh
docker compose -f compose.dev.yml up -d          # Postgres + SeaweedFS + SchulwareAPI
cd src/Schuly.API
dotnet user-secrets set "ConnectionStrings:SchulyDatabase" "Host=localhost;Port=2406;Database=schuly-dev;Username=postgres;Password=d4vpas8w0rd13!!!"
dotnet user-secrets set "Oidc:Authority" "http://localhost:8080/realms/schuly"
dotnet run --urls=http://localhost:5033
```

API reference (Scalar): `http://localhost:5033/scalar` · OpenAPI document: `http://localhost:5033/openapi/v1.json`

The connection string and `Oidc:Authority` are both required - the API stops on startup without the first, and the OpenAPI document fails without the second. [Development setup](https://docs.schuly.dev/SchulyBackend/setup/development) walks through it properly.

## Documentation

Full documentation lives at **[docs.schuly.dev/SchulyBackend](https://docs.schuly.dev/SchulyBackend/)**.

| Guide | What it covers |
|---|---|
| [Development setup](https://docs.schuly.dev/SchulyBackend/setup/development) | Run the API and its dependencies locally, and the tests. |
| [Configuration](https://docs.schuly.dev/SchulyBackend/setup/configuration) | Every setting: connection string, OIDC, S3 storage, avatar signing, logging. |
| [Self-hosting](https://docs.schuly.dev/SchulyBackend/setup/self-hosting) | Stand up the full stack (Caddy, Keycloak, Postgres, SeaweedFS) from published images. |
| [Production](https://docs.schuly.dev/SchulyBackend/setup/production) | Image tags, releases, and deployment notes. |
| [Architecture](https://docs.schuly.dev/SchulyBackend/architecture) | Projects, layering rules, request pipeline, document storage. |
| [Plugin management](https://docs.schuly.dev/SchulyBackend/plugin-management) | How plugins are declared, downloaded, and managed at runtime. |
| [Migrations](https://docs.schuly.dev/SchulyBackend/migrations) | Create and apply EF Core migrations. |
| [Contributing](https://docs.schuly.dev/SchulyBackend/contributing) | Workflow, branch and PR conventions. |

## School systems catalog

Which login systems the app offers is **supplied by the loaded plugins**, not baked into the backend: each plugin describes the system it serves via its `IPluginLogin.SchoolSystem` descriptor, and the backend seeds the catalog from those on load (seed-if-missing by `Key`, so admin edits survive). Install a plugin and its system appears in the picker with no operator config.

Operators can still add or override custom systems through a `SchoolSystems` config section, which seeds first and therefore wins on a matching `Key`.

See [the plugin contract](https://docs.schuly.dev/SchulyPluginAbstractions/contract) for the descriptor and a worked example.

## The Schuly ecosystem

| Repo | Purpose |
|---|---|
| [**Schuly**](https://github.com/schulydev/Schuly) | Flutter mobile app |
| [**SchulyBackend**](https://github.com/schulydev/SchulyBackend) | ASP.NET Core API backend *(this repo)* |
| [**SchulyKeycloak**](https://github.com/schulydev/SchulyKeycloak) | Keycloak image + the `schuly` realm |
| [**SchulyPluginAbstractions**](https://github.com/schulydev/SchulyPluginAbstractions) | Plugin contract (NuGet) |
| [**SchulyPlugins**](https://github.com/schulydev/SchulyPlugins) | Official plugins monorepo |
| [**SchulyWebsite**](https://github.com/schulydev/SchulyWebsite) | Landing site ([schuly.dev](https://schuly.dev)) |
| [**SchulyDocs**](https://github.com/schulydev/SchulyDocs) | Documentation site ([docs.schuly.dev](https://docs.schuly.dev)) |
