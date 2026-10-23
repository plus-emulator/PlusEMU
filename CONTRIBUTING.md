# Contributing to PlusEMU

## Build from source

You need the .NET SDK 10.0.401 or a later patch (pinned in `global.json`).

```sh
git clone https://github.com/plus-emulator/PlusEMU.git
cd PlusEMU
dotnet build "Plus Emulator.sln" -c Release
dotnet test Plus.Tests
```

Some tests read a database connection string from an environment variable, for example `ROOM_COMPONENT_DATABASE`. See the test files.

To run from source, start it in a folder that contains `Config/`:

```sh
dotnet run --project "Plus Emulator.csproj" -c Release
```

## Commits and pull requests

- Open pull requests against `master`.
- Use [Conventional Commits](https://www.conventionalcommits.org/), for example `feat(wired): ...`, `fix(movement): ...`, `docs: ...`.
- CI runs the code-style check, a Release build and the tests on every push and pull request. Run them first:

  ```sh
  dotnet run --project tools/Plus.CodeStyle -- check   # use "fix" to apply formatting
  dotnet build "Plus Emulator.sln" -c Release
  dotnet test Plus.Tests
  ```

## Write a database migration

1. Add the file `Database/Migrations/<next number>_<Name>.sql`. It is MariaDB SQL, and `DELIMITER` lines are allowed.
2. Make it safe to run again after a partial run (`IF NOT EXISTS`, `INSERT IGNORE`, guarded updates). MariaDB commits schema changes at once, and a failed migration runs again from the top.
3. Regenerate `Database/FreshInstall.sql` with `scripts/build-fresh-install-sql.sh`.

Operators: see [Database migrations](https://docs.plusemu.dev/getting-started/migrations/).

## Releases

Publish from **Actions > Release > Run workflow** on `master`. The workflow builds and tests before it publishes.

Choose the version change when you run the workflow:

| Choice | Use for | Example |
|--------|---------|---------|
| `patch` (default) | Fixes | `v1.2.3` to `v1.2.4` |
| `minor` | Compatible features | `v1.2.3` to `v1.3.0` |
| `major` | Breaking changes | `v1.2.3` to `v2.0.0` |

Rules:

- The first semantic release is `v1.0.0`. Later releases increase the highest stable version.
- Old date-based tags and prereleases do not change the next version.
- Release runs queue. They do not replace pending runs.
- Assets upload to a draft first. A retry for the same commit reuses its version and its draft. An already published release stays unchanged.
- If an unfinished draft belongs to another commit, rerun that draft's original workflow before you release newer changes.
- A new release must descend from the highest stable release.
