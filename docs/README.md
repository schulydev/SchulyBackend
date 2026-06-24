# SchulyBackend documentation

SchulyBackend is the ASP.NET Core 10 API behind Schuly. It follows clean
architecture with CQRS (via [Mediator](https://github.com/martinothamar/Mediator)),
uses EF Core on PostgreSQL, authenticates with OIDC, and hosts plugins loaded at
runtime from a registry.

## Index

- [Architecture](architecture.md) - layering, project responsibilities, the
  add-an-entity flow, and the plugin host.
- Setup
  - [Development](setup/development.md) - run the API and Postgres locally.
  - [Self-hosting](setup/self-hosting.md) - step-by-step: run the full stack on your own server.
  - [Configuration](setup/configuration.md) - settings, OIDC, roles, connection strings.
  - [Production](setup/production.md) - Docker image, release flow, startup migrations.
- [Migrations](migrations.md) - EF Core migration scripts and startup behavior.
- [Plugin management](plugin-management.md) - runtime plugin registry, hot-swap, admin endpoints.
- [Contributing](contributing.md) - issue → branch → PR workflow and conventions.
