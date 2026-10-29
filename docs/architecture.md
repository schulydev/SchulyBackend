# Architecture

SchulyServer is a clean-architecture solution with CQRS. Requests flow into thin
controllers, which dispatch commands/queries through [Mediator](https://github.com/martinothamar/Mediator)
to handlers in the application layer; persistence and external integrations live in
infrastructure.

## Projects

The solution (`Schuly.API.slnx`) is split into the following projects:

| Project | Role |
|---|---|
| `Schuly.API` | Entry point. Controllers, sign-in wiring and `/auth` endpoints, OpenAPI/Scalar, startup migrations, plugin host registration. Owns the `Dockerfile`. |
| `Schuly.Application` | CQRS commands/queries + Mediator handlers, DTOs, mappers, authorization and pipeline behaviors. **Must not** reference Infrastructure. |
| `Schuly.Domain` | Pure entities (`School`, `Class`, `Exam`, `Grade`, `Absence`, `AgendaEntry`, `ApplicationUser`, `SchoolUser`, `Teacher`, `SchoolSystem`, `SemesterReport`, `StudentDocument`, …). Each inherits `Base` (`Id`, `CreatedAt`, `UpdatedAt`). |
| `Schuly.Infrastructure` | `SchulyDbContext` (including the sign-in tables), user services, storage and the plugin secrets vault, the plugin contract services (`Plugins/`), repositories. |
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

## Document storage

Student documents and avatars are kept in an S3-compatible bucket - SeaweedFS in the
bundled dev and self-hosting stacks, though any S3 implementation works without a code
change. See [Configuration](setup/configuration.md#document-storage-s3) for the settings.

The backend **proxies every byte itself**: clients never receive S3 URLs and never
connect to the storage backend directly. Uploads go to
`POST /api/students/{id}/documents` (multipart) and downloads come back from
`GET /api/documents/{id}` as a file response. Avatars are the one exception - they are
uploaded with `PUT /api/me/avatar`, the database holds only a bare blob key, and a
short-lived HMAC-signed capability URL under `/api/avatars/users/{id}` is minted per
access (see [Avatar URL signing](setup/configuration.md#avatar-url-signing)).

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
`Schuly.Plugin.Abstractions`. The plugin runtime lives in `Schuly.API/Plugins`:
plugins are downloaded at runtime from a registry into `/app/plugins`, each loaded
into its own collectible `AssemblyLoadContext` with a child DI container, and can
register controllers, minimal-API endpoints, and recurring background tasks -
run by the host's own scheduler and dispatched back into the owning plugin's DI scope by name.
Plugin requests execute inside the owning plugin's DI scope via
`PluginScopeMiddleware`. See [Plugin management](plugin-management.md) for the
registry, hot-swap, and background tasks.

### What a plugin can reach

Plugins never get `SchulyDbContext` or the backend's entities. The host registers the
contract services in each plugin's container:

| Service | Backed by |
|---|---|
| `ISchoolData` | `PluginSchoolData` over `SchulyDbContext`, stamped with the plugin's assembly name as `Source` |
| `IPluginVault` | The persistent vault, namespace `plugin:<Name>` |
| `IPluginFileStore` | `IDocumentStorage` (encrypted S3) |
| `IPluginUserContext` | The current request's user |

Synced rows (`AgendaEntry`, `Absence`, `Grade`, `StudentDocument`) carry `Source` and
`ExternalId`, unique per student; `Source` null means the row was entered by hand and no
plugin touches it. A plugin may only write to students whose `SchoolUser.Source` is its own;
a student with no source yet is adopted by the first plugin that provisions or syncs it.

### Hand-entered data

Schuly works without any plugin. Under `api/me` a signed-in user creates a school of their
own (`POST api/me/schools`, they become its student) and adds, changes and removes their own
grades (`api/me/grades`; the subject and exam are created as needed). Absences and agenda
entries use the regular endpoints. Hand-entered rows have `Source` null, so no sync touches
them; a manual change or delete of a synced row is refused with the plugin's name. Times on
manual writes must carry a UTC offset (`...Z` or `+02:00`); offset-less times are rejected.

### Contract compatibility

Before instantiating a plugin, `PluginContract` checks the `Schuly.Plugin.Abstractions`
version the plugin was built against. Before 1.0 the MAJOR.MINOR must match; from 1.0 the
MAJOR must match and the plugin's MINOR may not be newer than the host's. A plugin that
references `Schuly.Domain`, `Schuly.Infrastructure`, `Schuly.Application` or `Schuly.API` is
refused too. Refusals name both versions in the error.
