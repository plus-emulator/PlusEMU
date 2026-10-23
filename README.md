# PlusEMU

PlusEMU is a Habbo Hotel server (an "emulator") written in C#. It works with the official Habbo AIR client and with the [Volt](https://github.com/plus-emulator/Volt) web client.

[![Join our Discord](https://img.shields.io/badge/Discord-Join%20our%20server-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://discord.gg/D7B42G7Rxk)

**Need help or have a question? [Join our Discord](https://discord.gg/D7B42G7Rxk).**

Full guides are on the documentation site: <https://docs.plusemu.dev>

## Setup

The easiest way is the [installer](https://docs.plusemu.dev/getting-started/installation/). It sets up the emulator, the client, the website and the database together.

To set up only the emulator by hand:

1. Install MariaDB or MySQL, then create an empty database:

   ```sh
   mysql -u root -p -e "CREATE DATABASE plus CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci"
   ```

2. Download the archive for your system from [Releases](https://github.com/plus-emulator/PlusEMU/releases) and extract it. You do not need to install .NET.
3. Open `Config/config.json` and set your database password under `Database`.
4. Start the emulator from the folder that contains `Config/`:

   ```sh
   ./"Plus Emulator"
   ```

   On Windows, run `Plus Emulator.exe`.

On an empty database, the emulator creates all tables by itself.

To connect a client, set its `socket.url` to `ws://127.0.0.1:2096`. See [Configuration](https://docs.plusemu.dev/getting-started/configuration/) for all settings.

## Update

If you used the installer, run `sudo plusemu-update` (Linux) or `plusemu-update` (Windows).

To update by hand:

1. Stop the emulator.
2. Back up your database and your `Config/` folder.
3. Download the new release. Replace the old executable with the new one. Keep your `Config/` folder.
4. Compare your `Config/config.json` with the new one, and add any new settings.
5. Start the emulator. It updates the database by itself and makes a backup first.

See [Update PlusEMU](https://docs.plusemu.dev/getting-started/updates/) for details.

## Contribute

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Licence

[GPL-3.0-only](LICENSE). The licence does not cover third-party code, or Habbo trademarks and assets.
