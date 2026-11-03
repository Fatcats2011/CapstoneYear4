# Uploading a build with SteamPipe

Templates for when the Steam App ID is ready. `Builds/` is already git-ignored.

## One-time setup

- Download the Steamworks SDK from the Steamworks partner site and unzip it **outside** this project (inside it, Unity would import the SDK's plugins). `steamcmd.exe` is in `sdk/tools/ContentBuilder/builder/`.
- Don't copy anything from the SDK into `Assets/`: Steamworks.NET brings its own `steam_api64.dll`, built for the SDK it wraps (1.64). The 1.65 DLL no longer has the Steam Deck check that `SteamManager.IsSteamDeck` calls.
- Run `steamcmd.exe` once: it updates itself, then at the `Steam>` prompt type `login YOUR_STEAM_USERNAME` (password + Steam Guard code), then `quit`. Later runs reuse that login.
- In Steamworks → your app → SteamPipe → Depots: the Windows depot exists (usually App ID + 1). Add a macOS depot and a Linux depot, set each one's Operating System (macOS, Linux + SteamOS), and note all three IDs.
- In Steamworks → Installation → General: add a launch option per OS: Windows `DeadOnArrival.exe`, macOS `Dead on Arrival.app`, Linux `DeadOnArrival.x86_64`.
- Run `python tools/set-app-id.py <APP_ID> <WINDOWS_DEPOT> <MAC_DEPOT> <LINUX_DEPOT>` (left out, the depots default to App ID + 1, + 2, + 3). It sets the App ID in `SteamStartup.cs`, `steam_appid.txt` and `app_build.vdf`, plus the three depots in `app_build.vdf`. `--check` shows what each one holds. These are Steam API files, so they go on `main` too (the branch policy).

## Every upload

- Build in Unity: Tools → Dead on Arrival → Build → All Three. It writes `Builds/Windows/`, `Builds/macOS/` and `Builds/Linux/` (release builds; the Mac one is Universal). Each platform switch reimports assets, so the first run is slow.
- Or in batch mode, with the editor closed: `Unity.exe -batchmode -quit -projectPath <project> -buildTarget Linux64 -executeMethod PlatformBuilds.BuildLinux` (`OSXUniversal` + `BuildMac`, `Win64` + `BuildWindows`).
- The upload sends all three depots; a missing folder fails it.
- Run from this folder, with the path to your unzipped SDK:

```bat
"C:\path\to\sdk\tools\ContentBuilder\builder\steamcmd.exe" +login YOUR_STEAM_USERNAME +run_app_build "%CD%\app_build.vdf" +quit
```

- If the saved login has expired, it asks for your password and Steam Guard code again.
- In Steamworks → SteamPipe → Builds, set the new build live on a `beta` branch, test it, then promote it to `default`.
