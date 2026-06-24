# Architecture

SchulyBackend is a clean-architecture solution with CQRS. Requests flow into thin
controllers, which dispatch commands/queries through [Mediator](https://github.com/martinothamar/Mediator)
to handlers in the application layer; persistence and external integrations live in
infrastructure.

## Projects

The solution (`Schuly.API.slnx`) is split into the following projects:

| Project | Role |
|---|---|
| `Schuly.API` | Entry point. Controllers, OIDC wiring, OpenAPI/Scalar, startup migrations, plugin host registration. Owns the `Dockerfile`. |
| `Schuly.Application` | CQRS commands/queries + Mediator handlers, DTOs, mappers, authorization and pipeline behaviors. **Must not** reference Infrastructure. |
| `Schuly.Domain` | Pure entities (`School`, `Class`, `Exam`, `Grade`, `Absence`, `AgendaEntry`, `ApplicationUser`, `SchoolUser`, `Teacher`, `SchoolSystem`, `SemesterReport`, `StudentDocument`, …). Each inherits `Base` (`Id`, `CreatedAt`, `UpdatedAt`). |
| `Schuly.Infrastructure` | `SchulyDbContext`, OIDC/user services, storage and vault, repositories, plugin runtime (`PluginBackgroundTaskHost`). |
| `Schuly.Tests` / `Schuly.Tests.Plugin` | Test projects (TUnit). |

`Schuly.Plugin.Abstractions` is consumed as a **NuGet `PackageReference`**, not a
project reference. The abstractions and the plugin implementations live in separate
repositories.

## Layering rules

- Dependencies point inward: `API → Application → Domain`, and `Infrastructure →
  Application/Domain`.
- **`Schuly.Application` must not reference `Schuly.Infrastructure`.** Handlers depend
  on abstractions; the API project composes the concrete infrastructure services into
  the DI container at startup (`Program.cs`).
- `Schuly.Domain` has no project dependencies - entities stay pure.

## Request pipeline

Controllers are thin and delegate to Mediator. Two pipeline behaviors are registered
explicitly in `Program.cs` and run in registration order:

1. `AuthorizationBehavior` - enforces role gates before the handler runs.
2. `PluginEventBehavior` - dispatches backend commands to plugin event handlers.

Mediator handlers are registered automatically via source generation, so a new
command/query and its handler are wired up just by adding the classes.

## Adding an entity + endpoint

1. **Entity** in `Schuly.Domain` (inherits `Base`).
2. **DbSet + configuration** in `Schuly.Infrastructure/SchulyDbContext.cs`.
3. **Migration** - see [Migrations](migrations.md).
4. **Command/Query** in `Schuly.Application/Commands/<Entity>/` or
   `Queries/<Entity>/`.
5. **Handler** alongside the command/query (auto-registered via Mediator source-gen).
6. **Controller** in `Schuly.API/Controllers/` - thin, delegates to Mediator.

## Plugin host

The backend hosts plugins implementing `ISchulyPlugin` from
`Schuly.Plugin.Abstractions`. Plugins are downloaded at runtime from a registry into
`/app/plugins`, each loaded into its own collectible `AssemblyLoadContext` with a
child DI container, and can register controllers, minimal-API endpoints, and
recurring background tasks (run by `PluginBackgroundTaskHost`). Plugin requests
execute inside the owning plugin's DI scope via `PluginScopeMiddleware`. See
[Plugin management](plugin-management.md) for the registry, hot-swap, and admin
endpoints.
