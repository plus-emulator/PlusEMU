# PlusEMU

PlusEMU is a Habbo Hotel server emulator written in C# (.NET 10). It is a community continuation of PlusEMU by Sledmore et al. and targets the protocol of the official Habbo AIR client (WIN63-202610 builds). It pairs with the [Volt](https://github.com/plus-emulator/Volt) client and [Volt-Renderer](https://github.com/plus-emulator/Volt-Renderer), which connect over WebSocket.

## Features

- **Wired** - a stack engine modelled on the official client's editors: triggers, conditions, effects, selectors and add-ons, including the October 2026 boxes, WIRED Label, WIRED Link and Room Linker, Wired leaderboards, Wired variables with variable FX, and Wired chests (transactional contracts).
- **Wired Web API** - read and write keys generated in the Variables Web API add-on editor open Habbo's public Wired Variables Web API for the room (`/api/public/rooms/{roomId}/variables...` and `.../variables_profile/...`, keys in the `X-Wired-Read-Key` / `X-Wired-Write-Key` headers). The reverse proxy has to send those paths to the web server.
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

Each archive contains the single-file executable, `Config/`, `Database/FreshInstall.sql`, `Database/Migrations/`, `LICENSE` and this README. `SHA256SUMS` is attached to every release.

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
| `AirClient` | What the web server serves the AIR client: `UrlPrefix`, gamedata source files and variable overrides; see [Flash gamedata](#flash-gamedata) | origin of the request, generated files |
| `Encryption` | `RsaPrivateKeyFile`: the PEM key of the Flash/AIR handshake; see [AIR client](#air-client) | built-in legacy key |

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

## AIR client

The official Habbo AIR client connects to the `Flash` TCP listener. Its handshake checks the server's Diffie-Hellman values with an RSA public key whose exponent it hard-codes as `65537`, so the emulator needs its own 2048-bit key with that exponent. The private key is never part of the repository:

```sh
./"Plus Emulator" --generate-rsa-key Config/rsa-private.pem   # or: openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out Config/rsa-private.pem
```

The command writes the key (mode 0600) and prints the public modulus, which the client must be given (`rsa.modulus` in `air.json`). Then point the emulator at the key with one of:

- `"Encryption": { "RsaPrivateKeyFile": "rsa-private.pem" }` (relative to `Config/`)
- `PLUS_RSA_PRIVATE_KEY_FILE=/run/secrets/rsa-private.pem`
- `PLUS_RSA_PRIVATE_KEY` with the PEM text itself

Without a key the emulator falls back to the old built-in 1024-bit key with exponent 3 and logs a warning. The AIR client cannot log in with it. Volt on the `Nitro` listener does not run this handshake, so no current client relies on the old key.

The `Flash` listener binds `Flash.Hostname` (default `127.0.0.1`). For players outside the host it has to listen on a public address (for example `0.0.0.0`) behind a hostname that is not proxied over HTTP.

### Flash gamedata

After login the AIR client loads `${url.prefix}/gamedata/hashes` and the four files it lists, each from `<url>/<hash>`. The web server serves them from the hotel's own data:

| Endpoint | Content |
|----------|---------|
| `GET /gamedata/hashes` | `{"hashes":[{name,url,hash}]}` for `external_texts`, `external_variables`, `furnidata` and `productdata` |
| `GET /gamedata/external_flash_texts/<hash>` | `key=value` texts merged from `AirClient.ExternalTextsFiles` (`key=value` files, or JSON objects such as Volt's `ExternalTexts.json`) |
| `GET /gamedata/external_variables/<hash>` | `key=value` variables merged from `AirClient.ExternalVariablesFiles`, then `furnidata.load.url` and `productdata.load.url`, then `AirClient.Variables` |
| `GET /gamedata/furnidata_xml/<hash>` | the generated furnidata (see [Generating furnidata](#generating-furnidata)) as Habbo's XML |
| `GET /gamedata/productdata/<hash>` | `AirClient.ProductDataFile` (XML or text as is, `ProductData.json` converted to XML), or the furnidata names |

Any hash returns the current file; only the current one is cached for good. Set `AirClient.UrlPrefix` to the hotel's public origin (for example `https://example.com`); when it is empty the origin of the request is used (`X-Forwarded-Proto` and `Host`). The client asset roots (`flash.dynamic.download.url` and the like) belong in an external variables file or in `AirClient.Variables`. Route `/gamedata/hashes`, `/gamedata/external_flash_texts/`, `/gamedata/external_variables/`, `/gamedata/furnidata_xml/` and `/gamedata/productdata/` from the public site to the web server.

### Launcher contract and tickets

The AIR launcher (custom hotel domain) reads `https://<domain>/api/client/air.json` and patches the client with it. The web server serves it once `AirClient.GameHost` is set and a handshake key is configured; otherwise it answers 404.

| Endpoint | Purpose |
|----------|---------|
| `GET /api/client/air.json` | `environment`, `game.host`/`game.ports`, `urls`, `rsa.modulus` (the configured key), `ticket.url`, and `client` when `AirClient.ClientUrl` is set |
| `POST /api/client/air-ticket` | `Authorization: Bearer <launcher token>` returns `{"ticket": "..."}`: a game ticket in `users.auth_ticket` that logs in once and expires after `AirClient.TicketLifetimeSeconds` (default 60) |
| `POST /api/client/air-launcher-token` | `{"ticket": "<game ticket>"}` uses up a live game ticket, the kind the website issues to a signed-in user, and returns `{"token", "expiresAt"}` |
| `DELETE /api/client/air-launcher-token` | revokes the Bearer token |

Launcher tokens live in `user_launcher_tokens` (migration 88) as SHA-256 digests. They expire after `AirClient.LauncherTokenLifetimeDays` (default 30) and stop working when the user's `credential_generation` changes, which a password change does. The website can also skip tokens and link a signed-in user to `habbo://hab?server=<domain>&token=<game ticket>`.

`AirClient.Environment` must be one of `en`, `pt`, `de`, `es`, `fi`, `fr`, `it`, `nl`, `tr` or `s2`; the client drops any other. Without `AirClient.ClientUrl` the launcher downloads Habbo's current client, whose build must match the emulator's packet headers.

## Branches and releases

- `dev` is the integration branch; open pull requests against `dev`.
- `dev` is merged into `master` for a release. Each push to `master` builds, tests and publishes a release tagged `v<yyyy.mm.dd>-<short sha>`. The date comes from the commit timestamp in UTC, so retries keep the same tag.

Release runs queue instead of replacing pending merges (up to GitHub's 100-run queue limit). Assets upload to a draft before publication; rerunning a failed workflow resumes the draft, and an already published tag is left unchanged. To retry manually, run **Release** on `master`, or rerun the original failed workflow for an older commit.

## Contributing

- Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat(wired): ...`, `fix(movement): ...`, `docs: ...`).
- CI runs the code-style check, a Release build and the tests on every push and pull request. Run them locally first:

```sh
dotnet run --project tools/Plus.CodeStyle -- check   # use "fix" to apply formatting
dotnet build "Plus Emulator.sln" -c Release
dotnet test Plus.Tests
```

## Licence

PlusEMU is licensed under the **GNU General Public License, version 3 only** (`GPL-3.0-only`). See [LICENSE](LICENSE) for the full terms.

You may redistribute and modify this program under GPL version 3. It is provided without any warranty, including the implied warranties of merchantability or fitness for a particular purpose.

Copyright remains with the respective PlusEMU authors and contributors. Third-party dependencies and assets retain their own copyrights and licence terms; this licence does not relicense them or grant rights to Habbo trademarks or assets.
