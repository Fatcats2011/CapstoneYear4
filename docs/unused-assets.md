# Unused assets inventory

1115 files, 1237 MB (1.24 GB) under `Assets/`, as of 2026-10-06. To get the current list, run `python tools/unused-assets.py`: it writes every unused file, with its size, to `Logs/unused-assets.txt`, and prints a summary by folder.

**What "unused" means here:** no GUID reference to the file is reachable from the 4 build scenes (SplashScreen, Alex Player Testing, Design Scene(Main), FinalAreaScene), `Resources/`, `ProjectSettings`, scripts, or paths that code names directly. It is a list to review, not a delete list. Anything the artists may still want (source art, bake data, reference models) needs a human decision. Sizes are file sizes only, without `.meta` files. Nothing has been deleted or moved.

## By top folder

| Folder | Files | MB |
|---|---:|---:|
| `Textures` | 124 | 701.7 |
| `Scenes` | 225 | 255.2 |
| `Audio` | 63 | 140.3 |
| `Materials` | 204 | 59.9 |
| `Models` | 90 | 20.9 |
| `OToon- URP Toon Shading` | 243 | 20.0 |
| `FlowerPot` | 8 | 13.3 |
| `Fonts` | 6 | 6.5 |
| `Prefabs` | 43 | 5.5 |
| `Sprites` | 29 | 3.8 |
| `Rendering` | 41 | 3.0 |
| `Cinemachine` | 12 | 2.9 |
| `Terrain` | 7 | 2.2 |
| `(loose files in Assets/)` | 5 | 1.1 |
| `Podium` | 1 | 0.9 |
| `DoA-Mats-Ryan-DesignerProjFiles` | 4 | 0.2 |
| `Presets` | 7 | 0.1 |
| `Animations` | 3 | 0.0 |

## Per folder detail

### `Textures` (124 files, 701.7 MB)

Biggest subfolders:

- `Textures/Roofs`: 27 files, 277.1 MB
- `Textures/WallsTall`: 5 files, 96.9 MB
- `Textures/DoA-Grass`: 4 files, 96.1 MB
- `Textures/Walls end cap`: 5 files, 82.5 MB
- `Textures/Hats`: 12 files, 43.3 MB
- `Textures/DoA-RoadV2`: 7 files, 42.6 MB

10 biggest files:

- `Assets/Textures/WallsTall/Wall-Tall_Black_Normal.png`: 69.3 MB
- `Assets/Textures/Roofs/Roof_Tiles_basecolor.png`: 63.5 MB
- `Assets/Textures/Walls end cap/Wall-endcap_Black_Normal.png`: 60.1 MB
- `Assets/Textures/DoA-Grass/Grass, Alt.png`: 54.9 MB
- `Assets/Textures/DoA-Grass/DefaultMaterial_Normal.png`: 41.1 MB
- `Assets/Textures/Roofs/RoofGeneric3/RoofGeneric_03_DefaultMaterial_Normal.png`: 40.3 MB
- `Assets/Textures/Roofs/Roof_Tiles_normal.png`: 38.9 MB
- `Assets/Textures/Roofs/RoofGeneric1/DefaultMaterial_Normal_OpenGL.png`: 28.6 MB
- `Assets/Textures/DoA-Stone/Material_Normal_OpenGL.png`: 25.3 MB
- `Assets/Textures/DoA-RoadV2/Substance_graph_basecolor.png`: 24.2 MB

### `Scenes` (225 files, 255.2 MB)

Biggest subfolders:

- `Scenes/Outdated`: 13 files, 107.4 MB
- `Scenes/FinalAreaScene`: 97 files, 72.9 MB
- `Scenes/Design Scene(Main)`: 100 files, 68.4 MB
- `Scenes/Design Scene`: 1 files, 3.0 MB
- `Scenes/Design Scene-Ryan`: 1 files, 0.9 MB
- `Scenes/GrassTest`: 4 files, 0.2 MB

10 biggest files:

- `Assets/Scenes/Outdated/Design Scene - Backup.unity`: 15.4 MB
- `Assets/Scenes/Outdated/Design Scene(Working).unity`: 13.1 MB
- `Assets/Scenes/Outdated/Design Scene.unity`: 13.1 MB
- `Assets/Scenes/Outdated/Design Scene(Main) 1.unity`: 10.5 MB
- `Assets/Scenes/Outdated/CUBED.unity`: 10.2 MB
- `Assets/Scenes/Outdated/SQUARE.unity`: 10.1 MB
- `Assets/Scenes/Outdated/Alternis City.unity`: 9.8 MB
- `Assets/Scenes/Outdated/NewGraveyard.unity`: 8.9 MB
- `Assets/Scenes/Outdated/Design Scene-Ryan.unity`: 8.3 MB
- `Assets/Scenes/Outdated/Design Scene(Alternate City).unity`: 6.5 MB

### `Audio` (63 files, 140.3 MB)

Biggest subfolders:

- `Audio/Music`: 3 files, 81.6 MB
- `Audio/TEST`: 32 files, 35.7 MB
- `Audio/Closed Playtest`: 24 files, 20.0 MB
- `Audio/SFX`: 4 files, 3.0 MB

10 biggest files:

- `Assets/Audio/Music/March 11 Final.wav`: 28.5 MB
- `Assets/Audio/Music/March 11 Main.wav`: 28.2 MB
- `Assets/Audio/Music/April 13 Main.wav`: 24.9 MB
- `Assets/Audio/TEST/78651__justinbw__moped-motor.wav`: 5.3 MB
- `Assets/Audio/TEST/28. Coconut Mall.mp3`: 5.1 MB
- `Assets/Audio/Closed Playtest/B2/b2_menu.mp3`: 4.2 MB
- `Assets/Audio/Closed Playtest/B1/b1_final.mp3`: 4.2 MB
- `Assets/Audio/TEST/432304__richwise__xsr700-tickover-loop.wav`: 4.1 MB
- `Assets/Audio/TEST/29. Coconut Mall (Final Lap).mp3`: 3.3 MB
- `Assets/Audio/TEST/Drink  One Piece  Official Soundtrack  Netflix.mp3`: 3.2 MB

### `Materials` (204 files, 59.9 MB)

Biggest subfolders:

- `Materials/Placeholder colours`: 70 files, 47.0 MB
- `Materials/Textures & Meshes`: 73 files, 7.5 MB
- `Materials/Skybox`: 5 files, 2.0 MB
- `Materials/Flat Colours`: 4 files, 0.0 MB

10 biggest files:

- `Assets/Materials/Placeholder colours/boostpad(New)3 1.png`: 5.5 MB
- `Assets/Materials/Placeholder colours/stone2.png`: 5.3 MB
- `Assets/Materials/Placeholder colours/edges/edges9.png`: 5.2 MB
- `Assets/Materials/Placeholder colours/edges/edges7.png`: 4.9 MB
- `Assets/Materials/Placeholder colours/edges/edges6.png`: 2.8 MB
- `Assets/Materials/Placeholder colours/edges/edges5.png`: 2.8 MB
- `Assets/Materials/Placeholder colours/edges/edges4.png`: 2.6 MB
- `Assets/Materials/Placeholder colours/Ledge2.png`: 2.1 MB
- `Assets/Materials/Placeholder colours/Ledge.png`: 2.1 MB
- `Assets/Materials/TCom_Rock_BoulderMossy_1K_albedo.tif`: 1.9 MB

### `Models` (90 files, 20.9 MB)

Biggest subfolders:

- `Models/Modular Kit`: 11 files, 15.6 MB
- `Models/Final Area Models`: 6 files, 1.3 MB
- `Models/City Center Models`: 10 files, 1.1 MB
- `Models/Ghost Models`: 4 files, 0.9 MB
- `Models/Stilt-Pirate Models`: 27 files, 0.7 MB
- `Models/Vehicle Models`: 6 files, 0.4 MB

10 biggest files:

- `Assets/Models/Modular Kit/Market01.fbx`: 15.0 MB
- `Assets/Models/Final Area Models/FinalArea_Track.fbx`: 0.8 MB
- `Assets/Models/Ghost Models/Pothole_Anim.fbx`: 0.5 MB
- `Assets/Models/City Center Models/ClockTower_Center.fbx`: 0.3 MB
- `Assets/Models/DOA_Trees/DOA_Tree_Swirl/Tree_Swirl_D_Normal.png`: 0.3 MB
- `Assets/Models/Final Area Models/Mansion_building_Building_AlbedoTransparency 1.jpg`: 0.3 MB
- `Assets/Models/Ghost Models/host_GhOST_UppiesOST_Uppies.anim`: 0.2 MB
- `Assets/Models/Modular Kit/World Props/CanalLedge_InnerCorner.fbx`: 0.2 MB
- `Assets/Models/City Center Models/MarketCart-large_Center.fbx`: 0.2 MB
- `Assets/Models/Modular Kit/Landmark01.fbx`: 0.2 MB

### `OToon- URP Toon Shading` (243 files, 20.0 MB)

Biggest subfolders:

- `OToon- URP Toon Shading/Demo(Can be delete)`: 219 files, 8.2 MB
- `OToon- URP Toon Shading/Shaders`: 20 files, 1.9 MB

10 biggest files:

- `Assets/OToon- URP Toon Shading/OToon Manual.pdf`: 9.8 MB
- `Assets/OToon- URP Toon Shading/Demo(Can be delete)/Scenes/1 Tutorials/7 AnimeStyle FaceShadowMap.unity`: 1.1 MB
- `Assets/OToon- URP Toon Shading/Demo(Can be delete)/Models/shaderBall.fbx`: 1.0 MB
- `Assets/OToon- URP Toon Shading/Demo(Can be delete)/Scenes/WebGLShowcase/ShowcaseDemo.unity`: 0.7 MB
- `Assets/OToon- URP Toon Shading/Shaders/OToonCustomLit.shadergraph`: 0.7 MB
- `Assets/OToon- URP Toon Shading/Demo(Can be delete)/Scenes/1 Tutorials/1 Lighting.unity`: 0.5 MB
- `Assets/OToon- URP Toon Shading/Demo(Can be delete)/Scenes/1 Tutorials/3 Haftone.unity`: 0.5 MB
- `Assets/OToon- URP Toon Shading/Demo(Can be delete)/Scenes/3 NPRStyles ShaderBall/Ball.unity`: 0.4 MB
- `Assets/OToon- URP Toon Shading/Demo(Can be delete)/Scenes/4 Stylized Character Ellen/Ellen.unity`: 0.3 MB
- `Assets/OToon- URP Toon Shading/Demo(Can be delete)/Scenes/1 Tutorials/8 Dithering.unity`: 0.3 MB

### `FlowerPot` (8 files, 13.3 MB)

10 biggest files:

- `Assets/FlowerPot/FlowerPot.png`: 6.1 MB
- `Assets/FlowerPot/Flowerpot4.png`: 1.1 MB
- `Assets/FlowerPot/Flowerpot4 1.png`: 1.1 MB
- `Assets/FlowerPot/Flowerpot3.png`: 1.1 MB
- `Assets/FlowerPot/Flowerpot3 1.png`: 1.1 MB
- `Assets/FlowerPot/Flowerpot2.png`: 1.0 MB
- `Assets/FlowerPot/Flowerpot2 2.png`: 1.0 MB
- `Assets/FlowerPot/Flowerpot2 1.png`: 1.0 MB

### `Fonts` (6 files, 6.5 MB)

10 biggest files:

- `Assets/Fonts/Distress SDF.asset`: 2.1 MB
- `Assets/Fonts/Lazy Sans SDF.asset`: 2.1 MB
- `Assets/Fonts/Yagitudeh SDF.asset`: 2.1 MB
- `Assets/Fonts/Yagitudeh.otf`: 0.1 MB
- `Assets/Fonts/Lazy Sans.ttf`: 0.0 MB
- `Assets/Fonts/Distress.otf`: 0.0 MB

### `Prefabs` (43 files, 5.5 MB)

Biggest subfolders:

- `Prefabs/Design_level`: 4 files, 2.1 MB
- `Prefabs/Secret Beacon Folder`: 3 files, 0.4 MB
- `Prefabs/Buildings`: 16 files, 0.2 MB
- `Prefabs/Player Prefabs`: 4 files, 0.1 MB
- `Prefabs/Trail Objects`: 5 files, 0.0 MB
- `Prefabs/Market`: 1 files, 0.0 MB

10 biggest files:

- `Assets/Prefabs/Tutorial_Old.prefab`: 1.9 MB
- `Assets/Prefabs/Design_level/Market Square.prefab`: 1.5 MB
- `Assets/Prefabs/Particles.prefab`: 0.7 MB
- `Assets/Prefabs/Design_level/Taria_Shit.prefab`: 0.5 MB
- `Assets/Prefabs/Design_level/Stilts.prefab`: 0.2 MB
- `Assets/Prefabs/Secret Beacon Folder/Particle System (2).prefab`: 0.1 MB
- `Assets/Prefabs/Secret Beacon Folder/Particle System.prefab`: 0.1 MB
- `Assets/Prefabs/Secret Beacon Folder/Particle System (1).prefab`: 0.1 MB
- `Assets/Prefabs/Player Prefabs/Player.prefab`: 0.1 MB
- `Assets/Prefabs/Buildings/WallGates (3).prefab`: 0.0 MB

### `Sprites` (29 files, 3.8 MB)

Biggest subfolders:

- `Sprites/UI`: 4 files, 1.7 MB
- `Sprites/Emotes`: 17 files, 1.1 MB
- `Sprites/Customization Icons`: 2 files, 0.0 MB

10 biggest files:

- `Assets/Sprites/UI/DOA_LoadingScreen_WhiteLines.png`: 1.6 MB
- `Assets/Sprites/TipsDay_UI_sheet_1.png`: 0.7 MB
- `Assets/Sprites/Control_Mock.png`: 0.2 MB
- `Assets/Sprites/Emotes/placeholder/MoneyGhost.png`: 0.2 MB
- `Assets/Sprites/Emotes/Cool/giggity.png`: 0.1 MB
- `Assets/Sprites/Emotes/placeholder/MadGhost.png`: 0.1 MB
- `Assets/Sprites/Emotes/placeholder/No_Bitches_Ghost.png`: 0.1 MB
- `Assets/Sprites/Emotes/placeholder/No Bitches Ghost.png`: 0.1 MB
- `Assets/Sprites/Emotes/placeholder/SadGhost2.png`: 0.1 MB
- `Assets/Sprites/Emotes/placeholder/HappyGhost.png`: 0.1 MB

### `Rendering` (41 files, 3.0 MB)

Biggest subfolders:

- `Rendering/PackageBeacon`: 11 files, 0.9 MB
- `Rendering/Shaders`: 18 files, 0.7 MB
- `Rendering/DeathWisp`: 2 files, 0.6 MB
- `Rendering/Boost Trail`: 2 files, 0.5 MB
- `Rendering/Package Effects`: 1 files, 0.2 MB
- `Rendering/ObjectOutlineShader`: 2 files, 0.1 MB

10 biggest files:

- `Assets/Rendering/PackageBeacon/BeamTexture.png`: 0.7 MB
- `Assets/Rendering/DeathWisp/vornoi_deathwisp.png`: 0.6 MB
- `Assets/Rendering/Boost Trail/SylizedTrail_02.png`: 0.3 MB
- `Assets/Rendering/Boost Trail/SylizedTrail_01.png`: 0.3 MB
- `Assets/Rendering/Package Effects/vfx_PackageGlow.vfx`: 0.2 MB
- `Assets/Rendering/Shaders/ComicStyle/skybox.png`: 0.2 MB
- `Assets/Rendering/Shaders/CustomShadow.shadergraph`: 0.1 MB
- `Assets/Rendering/Shaders/ShaderGraph_Composite.shadergraph`: 0.1 MB
- `Assets/Rendering/Shaders/BoostBar.shadergraph`: 0.1 MB
- `Assets/Rendering/Shaders/Outline.shadergraph`: 0.1 MB

### `Cinemachine` (12 files, 2.9 MB)

Biggest subfolders:

- `Cinemachine/Presets`: 8 files, 0.0 MB

10 biggest files:

- `Assets/Cinemachine/CINEMACHINE_install.pdf`: 1.7 MB
- `Assets/Cinemachine/CinemachineAPI.chm`: 1.2 MB
- `Assets/Cinemachine/ReleaseNotes.txt`: 0.0 MB
- `Assets/Cinemachine/LICENSE`: 0.0 MB
- `Assets/Cinemachine/Presets/Noise/Handheld_tele_strong.asset`: 0.0 MB
- `Assets/Cinemachine/Presets/Noise/Handheld_wideangle_strong.asset`: 0.0 MB
- `Assets/Cinemachine/Presets/Noise/Handheld_wideangle_mild.asset`: 0.0 MB
- `Assets/Cinemachine/Presets/Noise/Handheld_normal_strong.asset`: 0.0 MB
- `Assets/Cinemachine/Presets/Noise/Handheld_tele_mild.asset`: 0.0 MB
- `Assets/Cinemachine/Presets/Noise/Handheld_normal_mild.asset`: 0.0 MB

### `Terrain` (7 files, 2.2 MB)

10 biggest files:

- `Assets/Terrain/TerrainData_02c94c5a-62b9-4bc2-b7b1-c1fb9f18e99a.asset`: 0.6 MB
- `Assets/Terrain/New Terrain 1 - Copy.asset`: 0.6 MB
- `Assets/Terrain/New Terrain 1.asset`: 0.6 MB
- `Assets/Terrain/New Terrain.asset`: 0.6 MB
- `Assets/Terrain/NewLayer.terrainlayer`: 0.0 MB
- `Assets/Terrain/NewLayer - Copy.terrainlayer`: 0.0 MB
- `Assets/Terrain/New Terrain Layer.terrainlayer`: 0.0 MB

### `(loose files in Assets/)` (5 files, 1.1 MB)

10 biggest files:

- `Assets/TerrainData_36fe901f-2d6b-4b97-855f-a5ab3d340f75.asset`: 0.6 MB
- `Assets/GrassTest.asset`: 0.6 MB
- `Assets/.DS_Store`: 0.0 MB
- `Assets/DefaultNetworkPrefabs.asset`: 0.0 MB
- `Assets/NormalCracker.png`: 0.0 MB

### `Podium` (1 files, 0.9 MB)

10 biggest files:

- `Assets/Podium/DOA_Podium2d.png`: 0.9 MB

### `DoA-Mats-Ryan-DesignerProjFiles` (4 files, 0.2 MB)

10 biggest files:

- `Assets/DoA-Mats-Ryan-DesignerProjFiles/DoA_-_Dirt.sbs`: 0.1 MB
- `Assets/DoA-Mats-Ryan-DesignerProjFiles/DoA_Grass1.sbs`: 0.0 MB
- `Assets/DoA-Mats-Ryan-DesignerProjFiles/DoA-Road.sbs`: 0.0 MB
- `Assets/DoA-Mats-Ryan-DesignerProjFiles/Viking thumbnail.png`: 0.0 MB

### `Presets` (7 files, 0.1 MB)

Biggest subfolders:

- `Presets/DepthShader`: 3 files, 0.0 MB
- `Presets/OrderManager`: 2 files, 0.0 MB
- `Presets/DrivingIndicators`: 1 files, 0.0 MB
- `Presets/Volume`: 1 files, 0.0 MB

10 biggest files:

- `Assets/Presets/DepthShader/Option2.preset`: 0.0 MB
- `Assets/Presets/DepthShader/Original.preset`: 0.0 MB
- `Assets/Presets/DepthShader/Option1.preset`: 0.0 MB
- `Assets/Presets/OrderManager/Quick.preset`: 0.0 MB
- `Assets/Presets/OrderManager/OrderManagerMax.preset`: 0.0 MB
- `Assets/Presets/DrivingIndicators/Main.preset`: 0.0 MB
- `Assets/Presets/Volume/Volume.preset`: 0.0 MB

### `Animations` (3 files, 0.0 MB)

Biggest subfolders:

- `Animations/Menu Animations`: 2 files, 0.0 MB
- `Animations/UI`: 1 files, 0.0 MB

10 biggest files:

- `Assets/Animations/Menu Animations/ScreenAnim/Start.anim`: 0.0 MB
- `Assets/Animations/UI/Results/Default.anim`: 0.0 MB
- `Assets/Animations/Menu Animations/CharacterSelect.anim`: 0.0 MB

## Scenes not in the build

Scenes under `Assets/Scenes/` that are not in the build, and are listed as unreached:

| Scene | MB |
|---|---:|
| `Assets/Scenes/Outdated/Design Scene - Backup.unity` | 15.35 |
| `Assets/Scenes/Outdated/Design Scene(Working).unity` | 13.07 |
| `Assets/Scenes/Outdated/Design Scene.unity` | 13.07 |
| `Assets/Scenes/Outdated/Design Scene(Main) 1.unity` | 10.53 |
| `Assets/Scenes/Outdated/CUBED.unity` | 10.20 |
| `Assets/Scenes/Outdated/SQUARE.unity` | 10.11 |
| `Assets/Scenes/Outdated/Alternis City.unity` | 9.83 |
| `Assets/Scenes/Outdated/NewGraveyard.unity` | 8.93 |
| `Assets/Scenes/Outdated/Design Scene-Ryan.unity` | 8.30 |
| `Assets/Scenes/Outdated/Design Scene(Alternate City).unity` | 6.50 |
| `Assets/Scenes/NewDrive Test.unity` | 1.04 |
| `Assets/Scenes/Art Showcase.unity` | 0.98 |
| `Assets/Scenes/Outdated/TempDesignScene.unity` | 0.78 |
| `Assets/Scenes/Outdated/Test Arena.unity` | 0.56 |
| `Assets/Scenes/Shader Work.unity` | 0.22 |
| `Assets/Scenes/Outdated/Map.unity` | 0.17 |
| `Assets/Scenes/Programming Scene.unity` | 0.07 |
| `Assets/Scenes/PedestrianTesting.unity` | 0.02 |
| `Assets/Scenes/GrassTest.unity` | 0.02 |

Total: 19 scenes, 109.8 MB.

Other scene files outside `Assets/Scenes/` (third-party demos): 16 scenes, 5.1 MB (OToon demos, Anime Speed Lines).

Not listed as unreached although not one of the 4 build scenes: `Assets/Scenes/Tutorial scene.unity` (something still reaches it, probably a scene-name string; check before touching).

Lighting/bake data folders that belong to those scenes (`Assets/Scenes/<name>/` and `*.lighting`):

| Folder | Files | MB | Scene reached by build? |
|---|---:|---:|---|
| `Assets/Scenes/Outdated` | 13 | 107.4 | no (whole folder unreached) |
| `Assets/Scenes/FinalAreaScene` | 97 | 72.9 | no (whole folder unreached) |
| `Assets/Scenes/Design Scene(Main)` | 100 | 68.4 | no (whole folder unreached) |
| `Assets/Scenes/Design Scene` | 1 | 3.0 | no (whole folder unreached) |
| `Assets/Scenes/Design Scene-Ryan` | 1 | 0.9 | no (whole folder unreached) |
| `Assets/Scenes/GrassTest` | 4 | 0.2 | no (whole folder unreached) |
| `Assets/Scenes/PedestrianTesting` | 1 | 0.1 | no (whole folder unreached) |

- `Assets/OToon- URP Toon Shading/Demo(Can be delete)/Scenes/1 Tutorials/Texture/New Lighting Settings.lighting`: 0.00 MB

- `Assets/Scenes/Bake Low Res.lighting`: 0.00 MB

- `Assets/Scenes/Baking Test.lighting`: 0.00 MB

## Likely safe to delete

Clearly old or test-only by name or folder. Still confirm with the team first.

| Path | Files | MB | Why |
|---|---:|---:|---|
| `Assets/Scenes/Outdated/` | 13 | 107.4 | old scenes, folder is named for it |
| `Assets/OToon- URP Toon Shading/Demo(Can be delete)/` | 219 | 8.2 | third-party demo, folder says it can be deleted |
| `Assets/Scenes/GrassTest` | 5 | 0.2 | test scene, its bake data and `Assets/GrassTest.asset` |
| `Assets/Scenes/PedestrianTesting` | 2 | 0.2 | test scene |
| `Assets/Scenes/Shader Work` | 1 | 0.2 | shader test scene and data |
| `Assets/Scenes/NewDrive Test` | 1 | 1.0 | driving test scene |
| `Assets/Scenes/Programming Scene` | 1 | 0.1 | programmer scratch scene |
| `Assets/Scenes/Design Scene-Ryan` | 1 | 0.9 | old per-person copy of the design scene |
| `Assets/Scenes/Design Scene/` | 1 | 3.0 | old copy of the design scene's bake data |

## Check with the artists

Real art or data where only the artists know whether it is still wanted:

| Path | Files | MB |
|---|---:|---:|
| `Assets/Models/` (3D models) | 90 | 20.9 |
| `Assets/Textures/` (textures) | 124 | 701.7 |
| `Assets/Materials/` (materials) | 204 | 59.9 |
| `Assets/Animations/` (animations) | 3 | 0.0 |
| `Assets/Audio/` (audio) | 63 | 140.3 |
| `Assets/Prefabs/` (prefabs) | 43 | 5.5 |
| `Assets/Terrain/` (terrain data) | 7 | 2.2 |
| `Assets/Sprites/` (sprites) | 29 | 3.8 |
| `Assets/DoA-Mats-Ryan-DesignerProjFiles/` (designer project files) | 4 | 0.2 |
| `Assets/Scenes/Art Showcase` (art showcase scene) | 1 | 1.0 |
| `Assets/Scenes/Design Scene(Main)/` (bake data of the main scene: check it is the one in use) | 100 | 68.4 |

### Loose files directly in `Assets/`

| File | In unreached list | Referenced by |
|---|---|---|
| `New Terrain.asset` | no | FinalAreaScene (build) |
| `New Terrain 1.asset` | no | Design Scene(Main) (build) |
| `New Terrain 2.asset` | no | Design Scene(Main) (build) |
| `New Terrain 3.asset` | no | FinalAreaScene (build) |
| `New Terrain 4.asset` | no | FinalAreaScene (build) |
| `GrassTest.asset` | yes | only the GrassTest scene (not in build) |
| `TerrainData_36fe901f-...asset` | yes | nothing |
| `NormalCracker.png` | yes | nothing |
| `DoA-Final Area Start.mat` | no | FinalAreaScene (build) |
| `DoA-Final Area.mat` | no | FinalAreaScene (build) |
| `DefaultNetworkPrefabs.asset` | no | Netcode's own file, never touch |
