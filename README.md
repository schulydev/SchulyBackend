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

- `src/Schuly.API` — ASP.NET Core entry point + controllers
- `src/Schuly.Application` — CQRS commands/queries + handlers
- `src/Schuly.Domain` — entities (`School`, `Class`, `Exam`, `Grade`, `Absence`, `AgendaEntry`, ...)
- `src/Schuly.Infrastructure` — EF Core, OIDC, plugin host
- `src/Schuly.Tests` — unit + integration tests

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
# Requires PostgreSQL — see compose.dev.yml
cd src/Schuly.API
dotnet run --urls=http://localhost:5033
```

OpenAPI / Swagger: `http://localhost:5033/swagger`

## Migrations

```sh
./scripts/migration.sh    # or migration.ps1 on Windows
```
