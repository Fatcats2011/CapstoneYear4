# Uploading a build with SteamPipe

Templates for when the Steam App ID is ready. `Builds/` is already git-ignored.

## One-time setup

- Download the Steamworks SDK from the Steamworks partner site and unzip it **outside** this project (inside it, Unity would import the SDK's plugins). `steamcmd.exe` is in `sdk/tools/ContentBuilder/builder/`.
- Don't copy anything from the SDK into `Assets/`: Steamworks.NET brings its own `steam_api64.dll`, built for the SDK it wraps (1.64). The 1.65 DLL no longer has the Steam Deck check that `SteamManager.IsSteamDeck` calls.
- Run `steamcmd.exe` once: it updates itself, then at the `Steam>` prompt type `login YOUR_STEAM_USERNAME` (password + Steam Guard code), then `quit`. Later runs reuse that login.
- In Steamworks → your app → SteamPipe → Depots, note the Windows depot ID (usually App ID + 1).
- In `app_build.vdf` replace `APP_ID` and `WINDOWS_DEPOT_ID`.

## Every upload

- Build in Unity: File → Build Settings → Windows x86_64 → Build into `Builds/Windows/` (untick *Development Build* for Steam).
- Run from this folder, with the path to your unzipped SDK:

```bat
"C:\path\to\sdk\tools\ContentBuilder\builder\steamcmd.exe" +login YOUR_STEAM_USERNAME +run_app_build "%CD%\app_build.vdf" +quit
```

- If the saved login has expired, it asks for your password and Steam Guard code again.
- In Steamworks → SteamPipe → Builds, set the new build live on a `beta` branch, test it, then promote it to `default`.
