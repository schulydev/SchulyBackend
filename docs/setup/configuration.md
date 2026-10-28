# Configuration

Configuration comes from `appsettings.json`, environment-specific overrides
(`appsettings.Development.json`), user secrets, and environment variables, in the
standard ASP.NET Core precedence (env vars override user secrets, which override
`appsettings.{Environment}.json`, which overrides `appsettings.json`).

Secrets are deliberately **not** committed: the tracked `appsettings*.json` carry only
logging levels and non-secret defaults (sign-in issuer, local mail settings). Locally that gap is filled with
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

## Sign-in

Sign-in is built into the server with the
[Toamaisutaa](https://www.nuget.org/packages/Toamaisutaa.AspNetCore) library
(`AddSchulyAuthentication`). There is no separate identity provider. Its tables live
in the Schuly database (migration `AddToamaisutaa`).

Enabled features: password sign-in with self-registration, email verification,
password reset by email, TOTP two-factor with recovery codes, "remember this device"
(trusted devices), optional single sign-on through OIDC providers, and expired-token
cleanup. Passkeys, magic links, invitations and admin-created accounts are not enabled.

The endpoints live under `/auth`, for example `POST /auth/register`, `/auth/login`,
`/auth/refresh`, `/auth/logout`, `/auth/password`, `/auth/password/forgot`,
`/auth/password/reset`, `/auth/email`, `/auth/email/verify`, `/auth/2fa/*` and
`/auth/devices`, `/auth/providers` and `/auth/external/*`. The API under `/api/*` takes the access token as
`Authorization: Bearer`. Access tokens live 15 minutes. Refresh tokens rotate on
every use.

The web app (served by the server itself) sends the header `X-Schuly-Client: web`.
It then gets the refresh token and the remembered-device token in `HttpOnly`,
`SameSite=Strict` cookies scoped to `/auth`. Other clients, such as the mobile app,
get the tokens in the JSON body.

| Key | Purpose |
|---|---|
| `LocalLogin:SigningKey` | Key that signs access tokens. Base64, exactly 32 bytes. |
| `TwoFactor:EncryptionKey` | Key that encrypts the two-factor secrets. Base64, exactly 32 bytes. |
| `LocalLogin:Issuer` / `LocalLogin:Audience` | Token issuer and audience. `appsettings.json` sets both to `schuly`. |
| `LocalLogin:AllowSelfRegistration` | Whether anyone can register (`true` in `appsettings.json`). |
| `LocalLogin:RequireVerifiedEmailForPasswordReset` | Only send reset mails to verified addresses (`true` in `appsettings.json`). |
| `TwoFactor:Issuer` | Name shown in authenticator apps (`Schuly`). |
| `Auth:Administrators` | List of emails or usernames that get the `Administrator` role (`Auth__Administrators__0`, `__1`, ...). |

Generate both keys with `openssl rand -base64 32` and keep them stable. A new signing
key signs everybody out. A new encryption key breaks two-factor for every account
that has it set up.

Every account not listed in `Auth:Administrators` has no role. On the first valid
token for an unknown user, the API creates the matching Schuly user (`AddUserSync` /
`IUserService`).

### Single sign-on (OIDC providers)

Single sign-on is optional. Configure zero, one or several OIDC providers (Google,
Microsoft, Authentik, Pocket ID, Keycloak, any issuer with a discovery document). With
none configured, the web app and the mobile app show only the password form. Each
provider shows up as a "Continue with ..." button, and signed-in users link or unlink
providers under Settings → Security in the web app.

| Key | Purpose |
|---|---|
| `ExternalLogin:Providers:<key>:DisplayName` | Button label. Defaults to the key. |
| `ExternalLogin:Providers:<key>:IconUrl` | Optional icon shown on the button. |
| `ExternalLogin:Providers:<key>:Authority` | The issuer, e.g. `https://accounts.google.com`. Must be `https://` outside Development. |
| `ExternalLogin:Providers:<key>:ClientId` / `ClientSecret` | The client the provider issued. Leave the secret out for a public client. |
| `ExternalLogin:Providers:<key>:Scopes` | Defaults to `openid profile email`. |
| `ExternalLogin:Providers:<key>:AllowSignUp` | Whether a first sign-in may create a new account. `false` by default: only accounts that linked the provider can use it. |
| `ExternalLogin:Providers:<key>:LinkByVerifiedEmail` | Whether a first sign-in joins the account that already has the provider's verified address. Only turn this on for providers that really verify addresses. |
| `ExternalLogin:AllowedReturnUrls` | Exact URLs a finished sign-in may return to (`ExternalLogin__AllowedReturnUrls__0`, `__1`, ...). |
| `ExternalLogin:CallbackBaseUrl` | Public scheme and host of the server, when a proxy does not forward them. |

`AllowedReturnUrls` needs these entries:

- `https://<API_HOST>/login`: sign-in in the web app
- `https://<API_HOST>/settings`: linking a provider in the web app
- `schuly://auth/callback`: sign-in in the mobile app (`schuly-dev://auth/callback` for the dev build)

At each provider, register `https://<API_HOST>/auth/external/<key>/callback` as the
redirect URI. The key is stored with every link, so don't rename it later.

### Email (SMTP)

Verification and password-reset mails go out over SMTP:

| Key | Purpose |
|---|---|
| `Email:Smtp:Host` / `Email:Smtp:Port` | Mail server. |
| `Email:Smtp:User` / `Email:Smtp:Password` | Mail server credentials. |
| `Email:Smtp:From` | Sender address. |
| `Email:Smtp:PasswordResetLinkTemplate` | Link in the reset mail, e.g. `https://<API_HOST>/reset-password?token={token}`. |
| `Email:Smtp:EmailVerificationLinkTemplate` | Link in the verification mail, e.g. `https://<API_HOST>/verify-email?token={token}`. |

In Development, `appsettings.Development.json` points mail at a local SMTP catcher on
`localhost:1025` and builds links to `http://localhost:4200`. See
[Development setup](development.md).

## OpenAPI document

The OpenAPI document uses an HTTP bearer security scheme. In the Scalar UI you paste
an access token; there is no OAuth2 flow.

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

Users upload a profile picture with `PUT /api/me/avatar` (multipart, PNG, JPEG or WebP,
max 2 MB) and remove it with `DELETE /api/me/avatar`. The file goes to the document
storage (S3) and is served through short-lived signed URLs under
`/api/avatars/users/{id}`.

The database stores only a bare blob key; a signed capability URL is minted per access.
The key is read lazily, so a missing value surfaces as
`Avatar:SigningKey is not configured.` the first time an avatar URL is signed rather
than at startup.

## Retention

A hosted service sweeps expired data once a day, five minutes after startup and every 24
hours after that. It reuses the same purge code as `DELETE /api/auth/me`, so a swept row
leaves nothing behind in Postgres or S3.

| Key | Purpose |
|---|---|
| `Retention:Enabled` | Whether the sweep runs at all (default `true`). |
| `Retention:MonthsAfterLeave` | Cached school data is dropped for school users whose `LeaveDate` is older than this (default `6`). |
| `Retention:MonthsInactive` | Accounts with no authenticated request for this long are deleted (default `12`). |

"Inactive" is measured with `ApplicationUser.LastSeenAt`, stamped from the JWT validation
pipeline at most once per hour so a busy client does not write on every request. An
account that has never been stamped falls back to its `CreatedAt`.

The sweep deletes only the Schuly data, not the sign-in account, so an account swept
for inactivity can sign in again and resync from scratch.

## Push notifications (Firebase)

The notification outbox (grades, absences, agenda changes) is drained by a background
service and delivered through Firebase Cloud Messaging.

| Key | Purpose |
|---|---|
| `Firebase:ServiceAccountJson` | Base64 of the Firebase service-account JSON. |
| `Firebase:ServiceAccountPath` | Path to the service-account JSON file, as an alternative to the inline value. |

With neither set, the backend logs once at startup that push notifications are
disabled and no-ops from then on, so deployments without Firebase keep working. The
outbox is still drained in that case, so the table stays bounded even with push
turned off.

## Authorization policy

The default (fallback) policy **requires an authenticated user for every endpoint**
(`AddSchulyAuthorization`). Endpoints opt out with `[AllowAnonymous]`. The anonymous
`/api/app` surface (e.g. the school-systems catalog and static logo assets under
`wwwroot`) is reachable without auth so the app can load its login catalog. Register,
login and the other public sign-in endpoints under `/auth` need no token either. Admin
plugin endpoints under `/api/plugins` require the `Administrator` role.

## Plugins

Plugin host configuration (registry URL, desired-plugin file, directories) is
described in [Plugin management](../plugin-management.md).

## Logging

Log levels are set under `Logging:LogLevel` in `appsettings.json`. The Development
override raises HTTP logging to `Information` for request tracing.
