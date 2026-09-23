# Uploading a build with SteamPipe

Templates for when the Steam App ID is ready. `Builds/` is already git-ignored.

## One-time setup

- Download the Steamworks SDK from the Steamworks partner site; `steamcmd.exe` is in `sdk/tools/ContentBuilder/builder/`.
- In Steamworks → your app → SteamPipe → Depots, note the Windows depot ID (usually App ID + 1).
- In `app_build.vdf` replace `APP_ID` and `WINDOWS_DEPOT_ID`.

## Every upload

- Build in Unity: File → Build Settings → Windows x86_64 → Build into `Builds/Windows/` (untick *Development Build* for Steam).
- Run from this folder:

```bat
steamcmd.exe +login YOUR_STEAM_USERNAME +run_app_build "%CD%\app_build.vdf" +quit
```

- First login asks for your password and Steam Guard code; later logins reuse the cached session.
- In Steamworks → SteamPipe → Builds, set the new build live on a `beta` branch, test it, then promote it to `default`.
