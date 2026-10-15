# PlusEMU

PlusEMU is a Habbo Hotel server emulator written in C# (.NET 10). It is a community continuation of PlusEMU by Sledmore et al. and targets the protocol of the official Habbo AIR client (WIN63-202610 builds). It pairs with the [Volt](https://github.com/plus-emulator/Volt) client and [Volt-Renderer](https://github.com/plus-emulator/Volt-Renderer), which connect over WebSocket.

## Features

- **Wired** - a stack engine modelled on the official client's editors: triggers, conditions, effects, selectors and add-ons, including the October 2026 boxes, WIRED Label, WIRED Link and Room Linker, Wired leaderboards, Wired variables with variable FX, and Wired chests (transactional contracts).
- **Wired Web API** - read and write keys generated in the Variables Web API add-on editor, used by an HTTP API for the room's variables (`GET/PUT/DELETE /api/wired/variables`, bearer-key authenticated).
- **Games** - Battle Banzai, Freeze, Football, Ice Tag and SnowStorm.
- **Economy** - catalogue, marketplace, trading, recycler, crafting, vouchers, club offers and multiple currencies.
- **Community** - groups and group forums, friends and messenger, navigator, room polls, word quiz, achievements and talent tracks, quests, campaigns, moderation tools.
- **Camera** - room photos and thumbnails rendered by a separate trusted renderer service (see [`CameraRenderer/README.md`](CameraRenderer/README.md)).
- **Plugins** - assemblies placed in `plugins/` are loaded at startup (see `PluginExample/`).

## Requirements

- .NET SDK 10.0.401 or a later patch (pinned in `global.json`) - only needed to build from source
- MariaDB or MySQL

## Setup

### Option 1: download a release

Every push to `master` publishes self-contained builds on [GitHub Releases](https://github.com/plus-emulator/PlusEMU/releases), so no .NET install is needed:

| Asset | Platform |
|-------|----------|
| `plusemu-linux-x64.tar.gz` | Linux x64 |
| `plusemu-linux-arm64.tar.gz` | Linux ARM64 |
| `plusemu-win-x64.zip` | Windows x64 |

Each archive contains the single-file executable, `Config/`, `Database/FreshInstall.sql` and `Database/Migrations/`. `SHA256SUMS` is attached to every release.

### Option 2: build from source

```sh
git clone https://github.com/plus-emulator/PlusEMU.git
cd PlusEMU
dotnet build "Plus Emulator.sln" -c Release
dotnet test Plus.Tests
```

Some integration tests read a database connection string from an environment variable (for example `ROOM_COMPONENT_DATABASE`); see the individual test files.

### Database

1. Create an empty database (default name `plus`, utf8mb4).
2. Import `Database/FreshInstall.sql` (schema plus game content such as catalogue, furniture and achievements; no users or rooms). It already contains every migration in `Database/Migrations/`; `scripts/build-fresh-install-sql.sh` regenerates it from a fully migrated database whenever a migration is added.
3. When upgrading an existing database instead, apply the files in `Database/Migrations/` that are newer than your database, in numeric order (for example `mysql -u root -p plus < Database/Migrations/81_WiredOctoberBoxes.sql`).

```sh
mysql -u root -p -e "CREATE DATABASE plus CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci"
mysql -u root -p plus < Database/FreshInstall.sql
```

### Configuration

Settings are read from `Config/config.json` relative to the working directory. Logging is configured in `Config/nlog.config`.

| Section | Purpose | Default |
|---------|---------|---------|
| `Database` | MariaDB/MySQL host, port, user, password, database name, pool size | `127.0.0.1:3306`, database `plus` |
| `Flash` | TCP game server | `127.0.0.1:1232` |
| `Nitro` | WebSocket game server used by the Volt client | `127.0.0.1:2096` |
| `Rcon` | RCON listener and the addresses allowed to use it | `127.0.0.1:30001` |
| `WebServer` | HTTP endpoints (Wired variables API, furnidata, badge leaderboard); see [Generating furnidata](#generating-furnidata) | `127.0.0.1:8080` |
| `Camera` | Camera renderer URL and bearer, photo output directory, timeouts, effect allowlist | renderer at `127.0.0.1:3921` |

Set your own database password and camera bearer; do not commit them.

### Running

Run the emulator from the directory that contains `Config/`:

```sh
./"Plus Emulator"                      # release archive (Linux)
dotnet run --project "Plus Emulator.csproj" -c Release   # from source
```

Point the client's `socket.url` at the `Nitro` host and port (for example `ws://127.0.0.1:2096`); put a TLS proxy in front of it for `wss://` in production.

Point the client's `furnidata.url` at the emulator's web server as described in [Generating furnidata](#generating-furnidata).

## Generating furnidata

The emulator generates `FurnitureData.json` itself, so there is no static file to keep in sync with the catalogue. It is built from the `furniture` rows with `has_furnidata` set, plus the purchase fields of the catalogue offers that sell them, and is rebuilt whenever the catalogue reloads. The web server serves it at:

| Endpoint | Returns |
|----------|---------|
| `GET /api/gamedata/furnidata` | `FurnitureData.json` (revalidated with an `ETag`; a `?v=` matching the current version is cached for good) |
| `GET /api/gamedata/furnidata/version` | `{"version": "..."}`, the SHA-1 of the current file |

The web server listens on plain HTTP at `WebServer.Hostname`/`WebServer.Port` in `Config/config.json` (default `127.0.0.1:8080`). If that port is taken, set another one; the startup log prints `Web server listening on ...` once it is up, and the line is missing when the port could not be bound.

```json
"WebServer": {
    "Hostname": "127.0.0.1",
    "Port": 8080
}
```

The web server has no TLS. To use it from Volt, either:

- proxy `/api/` from the web server that serves the client to the emulator, and point `furnidata.url` in Volt's `renderer-config.json` at `https://<your host>/api/gamedata/furnidata`. With nginx:

  ```nginx
  location /api/ {
      proxy_pass http://127.0.0.1:8080;
  }
  ```

- or, for local testing with the client served over plain `http://`, point `furnidata.url` straight at `http://127.0.0.1:8080/api/gamedata/furnidata`. Browsers block this from a client loaded over `https://` (mixed content).

Check it with `curl http://127.0.0.1:8080/api/gamedata/furnidata`. If the client gets a 404:

- If curl returns JSON, the client's request does not reach the emulator: `furnidata.url` points at a host or port without the `/api/` proxy (for example `https://localhost/...` goes to port 443, not the emulator).
- If curl also returns a 404 (`{"error":"Not found.","code":"not_found"}`), no furniture row has furnidata. `Database/FreshInstall.sql` ships with it filled in. An upgraded database gets it from `Database/Migrations/60_FurnitureFurnidata.sql` only when that migration runs with the hotel's current `FurnitureData.json` in `@furnidata`; without it the migration only adds the columns. Run it again with the file, then restart the emulator:

  ```sh
  { printf "SET @furnidata = FROM_BASE64('%s');\n" "$(base64 -w0 FurnitureData.json)"; cat Database/Migrations/60_FurnitureFurnidata.sql; } | mysql -u root -p plus
  ```

## Branches and releases

- `dev` is the integration branch; open pull requests against `dev`.
- `dev` is merged into `master` for a release. Each push to `master` builds, tests and publishes a release tagged `v<yyyy.mm.dd>-<short sha>`.

## Contributing

- Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat(wired): ...`, `fix(movement): ...`, `docs: ...`).
- CI runs the code-style check, a Release build and the tests on every push and pull request. Run them locally first:

```sh
dotnet run --project tools/Plus.CodeStyle -- check   # use "fix" to apply formatting
dotnet build "Plus Emulator.sln" -c Release
dotnet test Plus.Tests
```

## Licence

The repository does not contain a licence file.
