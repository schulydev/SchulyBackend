# Schuly staging stack

A single `docker compose` that runs the whole Schuly backend stack from published
GHCR images, fronted by Caddy with automatic HTTPS. Intended for a staging server
with a real domain.

## What's in it

| Service | Image | Exposed |
|---|---|---|
| `caddy` | `caddy:2` | **80 / 443** (the only public ports) |
| `backend` | `ghcr.io/schulydev/schuly` | via Caddy → `https://${API_HOST}` |
| `keycloak` | `ghcr.io/schulydev/schulykeycloak` | via Caddy → `https://${AUTH_HOST}` |
| `schulware` | `ghcr.io/pianonic/schulwareapi` | internal |
| `postgres` | `postgres:18.1` | internal (DBs: `schuly`, `keycloak`, `schuly_plugin_*`) |
| `seaweedfs` | `chrislusf/seaweedfs` | internal (S3 document storage) |

The backend validates OIDC tokens against the Keycloak `schuly` realm, and loads
the **Schulware plugin** declared in `config/plugins.yml` on startup (downloaded
from the registry - no DLL baked into the image).

## Setup

1. **DNS** - point `API_HOST` and `AUTH_HOST` at this server (Caddy needs them to
   issue Let's Encrypt certs).
2. **Secrets** - `cp .env.example .env` and fill it in. Set `S3_SECRET_KEY` to the
   same value in `config/seaweedfs/s3-config.json` (`S3_ACCESS_KEY` too).
3. **Run**:
   ```sh
   docker compose -f compose.staging.yml up -d
   docker compose -f compose.staging.yml logs -f backend
   ```

## Verify end-to-end

- `https://${AUTH_HOST}` → Keycloak admin console (master realm, `KC_ADMIN_*`); the
  `schuly` realm is imported automatically.
- `https://${API_HOST}/api/app/school-systems` → anonymous catalog (proves the API
  is up).
- Loaded plugins: `GET https://${API_HOST}/api/plugins` (after an `Administrator`
  login). Manage at runtime with `POST /api/plugins/install`, `DELETE /api/plugins/{name}`.
- Point the Schuly app at `https://${API_HOST}`; its login flow drives Keycloak
  (`schuly-app` client). Because the backend and the app both use
  `https://${AUTH_HOST}` as the authority, the token issuer matches and validation
  passes.

## Notes

- The `schuly` realm ships a starter `schuly-app` PKCE client and the
  Student/Teacher/Administrator groups (mapped to the `groups` claim the backend
  reads). Replace it with a real export for production use.
- Plugin changes made via the API are persisted back to `config/plugins.yml`.
- All state is **bind-mounted to host folders under `./data`** (recommended over
  named volumes - visible and easy to back up): `data/postgres`, `data/seaweedfs`,
  `data/plugins`, `data/caddy*`. They're created on first `up`; the one-shot
  `init-perms` service makes `data/plugins` writable by the backend automatically, so
  it works first run with no manual `chown`. To wipe, stop the stack and delete `./data`.
