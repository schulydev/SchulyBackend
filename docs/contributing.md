# Contributing

A short, enforced workflow keeps history clean and releasable.

## Workflow

1. **Open a labeled issue** describing the change. Apply a label from the taxonomy
   below.
2. **Branch off `main`**: `feature/<issue#>_PascalCase` or `fix/<issue#>_PascalCase`.
   Never commit directly to `main`.
3. **Open a PR** (also labeled) targeting `main`. The PR body is **Summary** plus
   `Closes #<issue>` - nothing else (no test plans).
4. **Squash-merge** and delete the branch.

## Commit messages

- Short, imperative subject (e.g. `Add absences endpoint`).
- No noise in the body; keep it focused on the what/why.

## Labels

Use a label consistent with the org taxonomy:

| Label | Use for |
|---|---|
| `bug` | A defect / incorrect behavior. |
| `enhancement` | Improvement to existing behavior. |
| `feature` | New functionality. |
| `refactor` | Internal restructuring, no behavior change. |
| `CI/CD` | Build, release, and workflow changes. |
| `dependencies` | Dependency bumps. |
| `documentation` | Docs-only changes. |

## Versioning

`application.properties` is the single source of truth for the version and is synced
from the release tag automatically when a release is published - see
[Production](setup/production.md). Don't bump it by hand in feature PRs.

## Code expectations

- Respect the [layering rules](architecture.md): `Schuly.Application` must not
  reference `Schuly.Infrastructure`.
- Controllers stay thin and delegate to Mediator; handlers hold the logic.
- Add tests in `Schuly.Tests` where it makes sense.
