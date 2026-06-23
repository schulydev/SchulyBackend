# Plugin management

Plugins are loaded into the backend at runtime from a registry — no rebuilds, no
manual DLL drops, no restart.

## How it works

- **Registry** (`Plugins:Registry`, default the SchulyPlugins `repo` branch) serves an
  `index.min.json` and artifacts under `dll/`: `dll/<name>-v<ver>.dll` plus a
  `-deps.zip` of the plugin's private dependencies.
- **Desired set** lives in `plugins.yml` (`Plugins:File`). On startup the backend
  reconciles it against the registry + the plugins directory: downloads missing or
  out-of-date plugins, removes ones no longer listed, then loads them.
- **Hot-swap**: each plugin runs in its own collectible `AssemblyLoadContext` with its
  own child service container. Its controllers, minimal-API endpoints, and background
  tasks are wired in on load and torn down on unload — the running process never
  restarts. Plugin requests execute inside the plugin's own DI scope (falling back to
  the host's services).

## Configuration

| Key | Default | Purpose |
|---|---|---|
| `Plugins:Registry` | SchulyPlugins `repo` branch | Base URL of the registry. |
| `Plugins:File` | `<app>/plugins.yml` | Declarative desired plugin set. |
| `Plugins:Directory` | `<app>/plugins` | Where plugin DLLs are stored. |
| `Plugins:ConfigDirectory` | `<app>/plugins-config` | Per-plugin `<AssemblyName>.yml` config. |

## Admin endpoints (`Administrator`)

| Method | Route | Action |
|---|---|---|
| `GET` | `/api/plugins` | Loaded plugins. |
| `GET` | `/api/plugins/registry` | Plugins available in the registry. |
| `POST` | `/api/plugins/install` | `{ "name": "...", "version": "latest" }` — download + load. |
| `POST` | `/api/plugins/{name}/update` | Update to the registry's latest. |
| `DELETE` | `/api/plugins/{name}` | Unload + delete. |

Every change is applied in-process and persisted to `plugins.yml`, so it survives a
restart. Shared dependency DLLs are reference-counted: removing one plugin never
deletes a dependency another still uses.
