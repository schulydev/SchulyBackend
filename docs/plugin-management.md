# Plugin management

Plugins are loaded into the backend at runtime from a registry - no rebuilds, no
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
  tasks are wired in on load and torn down on unload - the running process never
  restarts. Plugin requests execute inside the plugin's own DI scope (falling back to
  the host's services).

## Background tasks

A plugin declares a recurring job by registering an `IPluginBackgroundTask`: a name, a
`PluginSchedule` (a cron expression plus optional retries and `RunOnStartup`), and an
`ExecuteAsync` body. The backend never lets a plugin talk to TickerQ directly - it owns
a single host-side ticker function that dispatches into the plugin's own DI scope by
plugin and task name. That indirection exists because a plugin assembly is loaded at
runtime into a collectible `AssemblyLoadContext`; a `[TickerFunction]` declared inside
it would never be seen by TickerQ's source generator, which only runs at host compile
time.

Every task's schedule is persisted to the main database (TickerQ's `ticker` schema),
not held in memory, so the schedule, next run, and run history all survive a restart -
and a run that was due during downtime is not silently skipped.

An operator can override a task's cadence per plugin in
`plugins-config/<AssemblyName>.yml`:

```yaml
Schedules:
  schulware.sync-timetable:
    Cron: "0 6 * * *"
    Retries: 3
    RunOnStartup: false
```

An override that fails to parse (a typo'd cron expression, say) is ignored in favour
of the plugin's own default rather than taking the task offline. The same keys work as
`SCHULY_PLUGIN_<NAME>_` environment variables, like the rest of the plugin config.

`GET /api/plugins/scheduler` reports each task's cron, last run, next run, and failure
counts. The TickerQ dashboard is mounted at `/tickerq` in Development only.

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
| `GET` | `/api/plugins/scheduler` | Background task schedules, last/next run, failure counts. |
| `GET` | `/api/plugins/registry` | Plugins available in the registry. |
| `POST` | `/api/plugins/install` | `{ "name": "...", "version": "latest" }` - download + load. |
| `POST` | `/api/plugins/{name}/update` | Update to the registry's latest. |
| `DELETE` | `/api/plugins/{name}` | Unload + delete. |

Every change is applied in-process and persisted to `plugins.yml`, so it survives a
restart. Shared dependency DLLs are reference-counted: removing one plugin never
deletes a dependency another still uses.
