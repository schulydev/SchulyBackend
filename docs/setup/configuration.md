# Configuration

Configuration comes from `appsettings.json`, environment-specific overrides
(`appsettings.Development.json`), user secrets, and environment variables, in the
standard ASP.NET Core precedence (env vars override user secrets, which override
`appsettings.{Environment}.json`, which overrides `appsettings.json`).

Secrets are deliberately **not** committed: the tracked `appsettings*.json` carry only
logging levels and `DevAuth`. Locally that gap is filled with
`dotnet user-secrets set` (see [Development setup](development.md)); in a container it
is filled with environment variables, using `__` as the section separator
(`ConnectionStrings__SchulyDatabase`).

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

## OpenAPI document

`Oidc:Authority` is also read when the OpenAPI document is generated, to advertise the
OAuth2 authorization-code flow. It is required even when `DevAuth` replaces the real
identity provider: without it `/openapi/v1.json` returns **500**
(`Oidc:Authority not configured`) and the Scalar UI comes up empty, since it renders
that document. `Oidc:ClientId` prefills the client id in the reference UI's
authorization dialog.

## Document storage (S3)

Document and avatar blobs go to an S3-compatible bucket (SeaweedFS in the bundled
stacks), configured under `S3:`:

| Key | Purpose |
|---|---|
| `S3:Endpoint` | S3 endpoint, e.g. `http://localhost:8333`. |
| `S3:Bucket` | Bucket name. |
| `S3:AccessKey` / `S3:SecretKey` | Credentials. Must match the SeaweedFS `s3-config.json`. |
| `S3:UsePathStyle` | `true` for SeaweedFS and most self-hosted S3 implementations. |

## Avatar URL signing

| Key | Purpose |
|---|---|
| `Avatar:SigningKey` | HMAC key for short-lived signed avatar URLs. Generate with `openssl rand -hex 32`. |

The database stores only a bare blob key; a signed capability URL is minted per access.
The key is read lazily, so a missing value surfaces as
`Avatar:SigningKey is not configured.` the first time an avatar URL is signed rather
than at startup.

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
