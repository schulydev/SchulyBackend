# Plugin management

Plugins are loaded into the backend at runtime from a registry - no rebuilds, no
manual DLL drops.

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
`ExecuteAsync` body. The backend runs these on its own scheduler: it checks every second
which tasks are due, resolves the task by plugin and task name inside the plugin's own
DI scope, and retries a failed run as often as the schedule allows (30 seconds apart
unless `RetryIntervals` says otherwise). A task never overlaps with itself; a run that
is still going when the next one is due delays that run until it finishes.

Every task's cron, next run, last run and run counters are stored in the main database
(`PluginTasks` table), so the history survives a restart - and a run that was due
during downtime runs once right after the next start.

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

## Configuration

| Key | Default | Purpose |
|---|---|---|
| `Plugins:Registry` | SchulyPlugins `repo` branch | Base URL of the registry. |
| `Plugins:File` | `<app>/plugins.yml` | Declarative desired plugin set. |
| `Plugins:Directory` | `<app>/plugins` | Where plugin DLLs are stored. |
| `Plugins:ConfigDirectory` | `<app>/plugins-config` | Per-plugin `<AssemblyName>.yml` config. |

## Managing plugins

There is no runtime admin API. Edit `plugins.yml` and restart the backend: it installs
what is declared, removes what is not, and loads the result. Shared dependency DLLs are
reference-counted: removing one plugin never deletes a dependency another still uses.
