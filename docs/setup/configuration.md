# Configuration

Configuration comes from `appsettings.json`, environment-specific overrides
(`appsettings.Development.json`), and environment variables, in the standard ASP.NET
Core precedence (env vars and `appsettings.{Environment}.json` override
`appsettings.json`).

## Database connection string

The `SchulyDbContext` reads the connection string named **`SchulyDatabase`**
(`ConnectionStrings:SchulyDatabase`), wired up in `DatabaseExtensions`. Retry-on-failure
is enabled for transient Postgres errors.

Provide it via configuration or an environment variable, e.g.:

```sh
export ConnectionStrings__SchulyDatabase="Host=localhost;Port=2406;Database=schuly-dev;Username=postgres;Password=..."
```

## OIDC authentication

JWT bearer authentication validates tokens against the configured OIDC authority
(`AddSchulyAuthentication`):

| Key | Purpose |
|---|---|
| `Oidc:Authority` | OIDC authority (Keycloak) used to validate bearer tokens. |
| `Oidc:RequireHttpsMetadata` | Whether HTTPS metadata is required (default `true`). |

Token claim mapping:

- `name` → display name (`NameClaimType`).
- **`groups` claim → role** (`RoleClaimType`). Group values map to the application
  roles **`Student`**, **`Teacher`**, and **`Administrator`**.
- Audience validation is disabled (`ValidateAudience = false`).

On the first valid token for an unknown user, the API syncs the user from the token
(`AddUserSync` / `IUserService`).

### Development auth (DevAuth)

In Development you can enable a local fake-OIDC path instead of a real IdP. With
`DevAuth:Enabled = true` (see `appsettings.Development.json`), the API trusts tokens
minted by `/api/dev/token`, signed with a symmetric key, using `DevAuth:Issuer`
(default `schuly-dev`). No external identity provider is contacted. **Do not enable
DevAuth in production.**

## Authorization policy

The default (fallback) policy **requires an authenticated user for every endpoint**
(`AddSchulyAuthorization`). Endpoints opt out with `[AllowAnonymous]`. The anonymous
`/api/app` surface (e.g. the school-systems catalog and static logo assets under
`wwwroot`) is reachable without auth so the app can load its login catalog. Admin
plugin endpoints under `/api/plugins` require the `Administrator` role.

## Plugins

Plugin host configuration (registry URL, desired-plugin file, directories) is
described in [Plugin management](../plugin-management.md).

## Logging

Log levels are set under `Logging:LogLevel` in `appsettings.json`. The Development
override raises HTTP logging to `Information` for request tracing.
