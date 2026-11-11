# Trusted room camera

The emulator supplies a room snapshot. The browser receives only a viewpoint and effect selections from the player; it never submits PNG bytes or room objects. A shutter snapshot stays immutable for editor renders. Checkout accepts only a media GUID minted by that session while it is still in the original room.

Apply `Resources/SQLs/Updates/15_TrustedCamera.sql` once before enabling the camera. It is safe to rerun and preserves existing settings. Configure `Camera` in `Config/config.json`; keep its case, especially bearer and effect names. An empty bearer rejects captures. Prices use credits and duckets; other point types fail closed. Competition defaults to disabled and fails closed if email verification is required because Plus has no verified-email flag.

Release builds include this service in their `CameraRenderer` folder. The page that draws photos is `camera.html` from the Volt client build (`src/camera` in Volt), so photos always use the same renderer as the client and a client deploy updates the camera too. Point `CAMERA_PAGE_ROOT` at that build: the deployed client directory, or `../../Volt/dist` (the default) for a sibling checkout built with `yarn vite build`. Restart the service after a client deploy so its warm pages load the new build.

```sh
cd CameraRenderer
npm ci
npx playwright install chromium
CAMERA_RENDERER_SECRET='<a private random secret of at least 32 characters>' node server.mjs
```

Keep the service private. It defaults to `127.0.0.1:3921`; use a private Compose hostname for containers. `Camera.RendererUrl` must be an HTTP private endpoint ending in `/render`. Redirects and public DNS addresses are refused. The emulator bearer must match `CAMERA_RENDERER_SECRET` and must never enter client configuration.

Set `CAMERA_RENDERER_CONFIG` and `CAMERA_UI_CONFIG` to renderer and UI configuration files. The catalogue comes from `camera.available.effects`; names must also appear in the emulator's `Camera.Effects` allowlist. Empty catalogues permit no-effect captures. The service replaces all asset URLs with its private routes and blocks game sockets and outside requests. Missing scene assets reject the render.

Optional environment variables: `CAMERA_ASSET_LAYOUT` (`nitro`, the default, or `hotel-files` for the installer's hotel files pack with flat `assets/<kind>/*.hab` and `gamedata/*.json` folders), `CAMERA_PAGE_ROOT`, `CAMERA_CHROMIUM_SANDBOX` (set `0` where Chromium's sandbox cannot run, such as proot), `CAMERA_ASSET_ROOT`, `CAMERA_IMAGES_ROOT`, `CAMERA_MEDIA_ROOT`, `CAMERA_CHROMIUM_PATH`, `CAMERA_RENDERER_HOST`, `CAMERA_RENDERER_PORT`, `CAMERA_RENDERER_ORIGIN`, `AVATAR_IMAGER_SECRET`, and `AVATAR_IMAGER_ROOT`. Asset defaults match the sibling hotel assets. Point `CAMERA_MEDIA_ROOT` at the emulator's `Camera.OutputDirectory` if minted media is shared.

Serve that output directory at `/camera/` with GET/HEAD only, no directory listing and `X-Content-Type-Options: nosniff`. Photos are `<guid>.png` (320×320) and `<guid>_small.png` (110×110). Room thumbnails are `thumbnail/<room-id>.png` (110×110); configure the client's `thumbnails.url` as `/camera/thumbnail/%thumbnail%.png`. Thumbnail responses must allow refresh after replacement. Public image origins and paths cannot be chosen by camera packets.

## Avatar images

The same service draws avatar images for the hotel website, with the client's own renderer. Custom clothing, dances, actions and effects look as they do in the hotel. It is off until both settings are present:

- `AVATAR_IMAGER_SECRET`: a private random secret of at least 32 characters. The website signs every figure with it, so nobody else can ask for new images.
- `AVATAR_IMAGER_ROOT`: the folder the images are stored in. The web server serves this folder at `/avatars/`.

A request looks like `/avatars/v<version>/<figure>/<signature>.<variant>.png`. The signature is the first 16 hex characters of HMAC-SHA256 over `v<version>/<figure>`. The variant has twelve fields joined by `-`:

| Field | Values |
| --- | --- |
| gender | `m`, `f` |
| size | `s` (half), `m`, `l` (double) |
| part | `full`, `head` |
| direction, head direction | `0` to `7` |
| posture | `std`, `sit`, `lay`, `wlk` |
| expression | `none`, `wave`, `kiss`, `laugh` |
| gesture | `nrm`, `sml`, `sad`, `agr`, `srp` |
| dance | `0` to `4` |
| effect, hand item | the id, or `0` |
| motion | `still`, `anim` |

For example `m-m-full-2-3-std-wave-sml-0-0-0-still`. A head is always `std-none-<gesture>-0-0-0-still` with the same value for both directions. `anim` gives an animated PNG that loops.

The web server serves a stored image itself and passes only a missing one to this service, without the camera password:

```nginx
location /avatars/ {
    root /var/www/hotel;
    try_files $uri @avatar;
    add_header Cache-Control "public, max-age=31536000, immutable";
    add_header Cross-Origin-Resource-Policy same-site;
}
location @avatar {
    proxy_pass http://127.0.0.1:3921;
    proxy_intercept_errors on;
    error_page 404 502 503 504 = /avatar-missing.png;
}
```

The service writes the image to `AVATAR_IMAGER_ROOT` and answers with it. Images already stored stay available while the service is down. Raise the version on the website after the art of existing clothing changes, and delete the old `v<version>` folder.

Checks: `npm test`, `dotnet test Plus.Tests`. Camera database facts require `PLUS_CAMERA_TEST_CONNECTION_STRING` pointing at a disposable `task_camera_tests_` schema containing the camera migration and minimal user/item tables. They verify purchases, rollback, publication/competition idempotency and persistent quotas.
