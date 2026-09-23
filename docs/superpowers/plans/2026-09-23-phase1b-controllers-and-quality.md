# Phase 1B — Controllers, Test Players & Graphics Quality Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the code side of roadmap Tasks 1.3 (controllers) and 1.4 (settings & performance) while the Steam App ID is pending: no controller problem can soft-lock a match, one developer can test 2–4 player split-screen with one controller, and players get Low / Medium / High graphics.

**Architecture:** Everything is built from scripts, so nothing touches the scenes, prefabs or Project Settings the open editor holds. On-screen controller messages come from a self-building overlay canvas (`ControllerPrompts`). While a player's controller is missing, a small listener (`ReplacementControllerListener`) catches presses on unused controllers and pairs one with that player instead of letting it join as a new player. Decisions stay in small static classes that EditMode tests call directly (`ControllerDisconnectPolicy`, `GraphicsQuality`, `GameSettings`), and the glue in the existing MonoBehaviours stays thin.

**Tech Stack:** Unity 2022.3.62f3, C# 9, Input System 1.14.0 (`InputUser`, `PlayerInputManager`), URP 14.0.12, TextMesh Pro 3.0.7, Steamworks.NET 2025.164.1, Unity Test Framework 1.1.33 (NUnit 3.5).

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` (roadmap: Phase 1, Tasks 1.3–1.5, §R checklist). Builds on `docs/superpowers/plans/2026-09-22-phase1a-release-hardening.md` (Tasks 1–8, 35 tests).

**Not in this plan (and why):** keyboard as a player and PlayStation / Nintendo button prompts (need new UI art in the team's style); resolution, window mode, VSync and frame-cap options (the build already runs borderless at desktop resolution with VSync on; each new option needs a new row of menu art); Steamworks dashboard items (need the App ID); scene fixes from Phase 1A (dead *Player Left* listener, extra AudioListeners, version/icon — editor steps for the partner).

## Global Constraints

- Unity 2022.3.62f3 LTS; no package upgrades or additions.
- 1–4 players, gamepad-first (`Constants.MAX_PLAYERS = 4`); local split-screen must keep passing the roadmap's §R checklist.
- Do not edit scenes, prefabs or `ProjectSettings/` — the Unity editor has the project open. Scripts, tests and docs only; steps that need the editor go to the partner as short bullet instructions.
- No git commits without explicit approval from your human partner; each task ends with a checkpoint listing its files.
- EditMode tests live in `Assets/Tests/Editor/` (compiles into `Assembly-CSharp-Editor`), namespace `DoA.Tests`; reuse `TestObjects` / `Reflect` from `TestSupport.cs`.
- Every new script gets a `.meta` file with a fresh GUID: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/newmeta.sh <path>`.
- Run tests only through `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh [filter]` (it syncs the project into the mirror and runs Unity in batch mode there).
- Change existing files with the Edit tool only (never `sed -i`: it rewrites CRLF files as LF). Scripts committed with CRLF (`git ls-files --eol` shows `i/crlf`, e.g. `PlayerInstantiate.cs`, `BallDriving.cs`, `PlayerCameraResizer.cs`) must stay CRLF or every line shows as changed; new files may be LF (git normalizes them on commit). Check line endings with byte counts or `git ls-files --eol`, not `grep $'\r'` (git-bash's grep matches every line).
- Developer-only features go through `DevTools` (on in the editor and development builds, off in release builds).

## Review Focus

1. A controller dies and a **different** controller is pressed, including with four players when nobody can join → that controller takes the missing player's place and never joins as a new player. Tests: `ControllerDisconnectPolicyTests.PickSlotForReplacement_*`, `ReplacementControllerListenerTests.WhileListening_NewPlayersCannotJoin` (Task 2).
2. Someone bumps the keyboard mid-race (or presses a debug hotkey) → no "connect a controller" message pops up; on the title screen and in the lobby it does. Tests: `ControllerPromptsTests.AddPlayerReference_KeyboardDuringAMatch_ShowsNoHint`, `…_BeforeAnyoneJoins_ShowsTheConnectHint` (Task 1).
3. A `settings.cfg` saved before quality existed (no `quality=` line), or a hand-edited `quality=9` → the default quality or the nearest valid one, and nothing throws. Tests: `GameSettingsTests.SettingsFromBeforeQualityExisted_KeepTheDefaultQuality`, `OutOfRangeQuality_IsClamped` (Task 5).
4. A developer switches quality in Play Mode → after Play Mode the project's URP asset is exactly as authored, so testing never changes the project by accident. Test: `GraphicsQualityTests.Restore_PutsTheUrpAssetBackAsAuthored` (Task 5).
5. Minimised window or a zero-size viewport → the phase camera's texture is never 0×0 (Unity refuses to create one). Test: `PlayerCameraResizerTests.PhaseTexture_ZeroSizeViewport_IsAtLeastOnePixel` (Task 4).

---

## Task 1: Controller prompts overlay and the "connect a controller" hint

Roadmap 1.3: keyboard presses on the title screen are silently thrown away today (`PlayerInstantiate.cs:120-125`).

**Files:**
- Create: `Assets/Scripts/UI/ControllerPrompts.cs` (+ `.meta`)
- Create: `Assets/Tests/Editor/ControllerPromptsTests.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Player/PlayerInstantiate.cs:118-125` (`AddPlayerReference`), plus a new `ShowUnsupportedDeviceHint` method next to `OnPlayerControllerLost`

**Interfaces:**
- Produces: `ControllerPrompts` — `static bool Exists`, `static ControllerPrompts Instance` (built on first use, `DontDestroyOnLoad` in Play Mode), `const string CONNECT_CONTROLLER_HINT`, `const string UNSUPPORTED_CONTROLLER_HINT`, `const float HINT_SECONDS = 3f`, `static string ReconnectMessage(int slot)`, `static string HintForDevice(InputDevice device)`, `void ShowReconnect(int slot, Rect viewport)`, `void HideReconnect(int slot)`, `bool IsReconnectShown(int slot)`, `Rect ReconnectArea(int slot)`, `void ShowHint(string message, float seconds)`, `bool IsHintShown`, `string HintText`.
- Produces: `private void PlayerInstantiate.ShowUnsupportedDeviceHint(InputDevice device)` (Task 2 reuses it).

- [ ] **Step 1: Write the failing tests** — `Assets/Tests/Editor/ControllerPromptsTests.cs`

```csharp
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    public class ControllerPromptsTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
            if (ControllerPrompts.Exists)
                Object.DestroyImmediate(ControllerPrompts.Instance.gameObject);
        }

        [Test]
        public void ShowReconnect_CoversOnlyThatPlayersViewport()
        {
            Rect secondPlayersView = new Rect(0.5f, 0.5f, 0.5f, 0.5f);

            ControllerPrompts.Instance.ShowReconnect(1, secondPlayersView);

            Assert.IsTrue(ControllerPrompts.Instance.IsReconnectShown(1));
            Assert.AreEqual(secondPlayersView, ControllerPrompts.Instance.ReconnectArea(1));
            Assert.IsFalse(ControllerPrompts.Instance.IsReconnectShown(0));
        }

        [Test]
        public void HideReconnect_HidesTheMessage()
        {
            ControllerPrompts.Instance.ShowReconnect(2, new Rect(0f, 0f, 0.5f, 0.5f));

            ControllerPrompts.Instance.HideReconnect(2);

            Assert.IsFalse(ControllerPrompts.Instance.IsReconnectShown(2));
        }

        [Test]
        public void ReconnectMessage_NamesThePlayer()
        {
            StringAssert.StartsWith("P3 ", ControllerPrompts.ReconnectMessage(2));
        }

        [Test]
        public void Hint_DisappearsWhenItsTimeIsUp()
        {
            ControllerPrompts prompts = ControllerPrompts.Instance;
            prompts.ShowHint(ControllerPrompts.CONNECT_CONTROLLER_HINT, 0f);

            Reflect.Invoke(prompts, "Update");

            Assert.IsFalse(prompts.IsHintShown);
        }

        [Test]
        public void Hint_StaysUntilItsTimeIsUp()
        {
            ControllerPrompts prompts = ControllerPrompts.Instance;
            prompts.ShowHint(ControllerPrompts.CONNECT_CONTROLLER_HINT, 60f);

            Reflect.Invoke(prompts, "Update");

            Assert.IsTrue(prompts.IsHintShown);
        }

        [Test]
        public void HintForDevice_KeyboardAsksForAController()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Assert.AreEqual(ControllerPrompts.CONNECT_CONTROLLER_HINT, ControllerPrompts.HintForDevice(keyboard));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [Test]
        public void HintForDevice_UnknownControllerIsUnsupported()
        {
            Joystick joystick = InputSystem.AddDevice<Joystick>();
            try
            {
                Assert.AreEqual(ControllerPrompts.UNSUPPORTED_CONTROLLER_HINT, ControllerPrompts.HintForDevice(joystick));
            }
            finally
            {
                InputSystem.RemoveDevice(joystick);
            }
        }

        [Test]
        public void AddPlayerReference_BeforeAnyoneJoins_ShowsTheConnectHint()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            PlayerInput playerWithoutController = objects.Add<PlayerInput>();
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            instantiate.AddPlayerReference(playerWithoutController);

            Assert.IsTrue(ControllerPrompts.Instance.IsHintShown);
            Assert.AreEqual(ControllerPrompts.CONNECT_CONTROLLER_HINT, ControllerPrompts.Instance.HintText);
            Assert.AreEqual(0, instantiate.PlayerCount);
        }

        [Test]
        public void AddPlayerReference_KeyboardDuringAMatch_ShowsNoHint()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            Reflect.SetField(instantiate, "allowPlayerSpawn", false); // spawning is off from the loading screen on
            Reflect.SetField(instantiate, "playerCount", 2);
            PlayerInput playerWithoutController = objects.Add<PlayerInput>();
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            instantiate.AddPlayerReference(playerWithoutController);

            Assert.IsFalse(ControllerPrompts.Exists && ControllerPrompts.Instance.IsHintShown);
        }
    }
}
```

Stub so it compiles — `Assets/Scripts/UI/ControllerPrompts.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class ControllerPrompts : MonoBehaviour
{
    public const string CONNECT_CONTROLLER_HINT = "Connect a controller to play";
    public const string UNSUPPORTED_CONTROLLER_HINT = "That controller isn't supported - try an Xbox, PlayStation or Switch Pro controller";
    public const float HINT_SECONDS = 3f;

    public static bool Exists => throw new NotImplementedException();
    public static ControllerPrompts Instance => throw new NotImplementedException();
    public bool IsHintShown => throw new NotImplementedException();
    public string HintText => throw new NotImplementedException();
    public static string ReconnectMessage(int slot) => throw new NotImplementedException();
    public static string HintForDevice(InputDevice device) => throw new NotImplementedException();
    public void ShowReconnect(int slot, Rect viewport) => throw new NotImplementedException();
    public void HideReconnect(int slot) => throw new NotImplementedException();
    public bool IsReconnectShown(int slot) => throw new NotImplementedException();
    public Rect ReconnectArea(int slot) => throw new NotImplementedException();
    public void ShowHint(string message, float seconds) => throw new NotImplementedException();
    private void Update() => throw new NotImplementedException();
}
```

Then `newmeta.sh` for both new files.

- [ ] **Step 2: Run to watch them fail**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh ControllerPromptsTests`
Expected: `9 total, 0 passed, 9 failed`, all `System.NotImplementedException` (the two `AddPlayerReference` tests reach the stub after today's code destroys the player).

- [ ] **Step 3: Implement** — replace the stub with the full `ControllerPrompts.cs`:

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Controller messages drawn on top of every player's view: a "reconnect" panel over a player's viewport while their
/// controller is missing, and a short hint along the bottom when someone presses a key or an unsupported controller.
/// Builds its own canvas the first time it's used, so no scene or prefab has to contain it.
/// </summary>
public class ControllerPrompts : MonoBehaviour
{
    public const string CONNECT_CONTROLLER_HINT = "Connect a controller to play";
    public const string UNSUPPORTED_CONTROLLER_HINT = "That controller isn't supported - try an Xbox, PlayStation or Switch Pro controller";
    public const float HINT_SECONDS = 3f;

    static ControllerPrompts instance;

    readonly GameObject[] reconnectPanels = new GameObject[Constants.MAX_PLAYERS];
    readonly TMP_Text[] reconnectTexts = new TMP_Text[Constants.MAX_PLAYERS];
    GameObject hintPanel;
    TMP_Text hintText;
    float hintHideTime;

    /// <summary>
    /// True once the prompts canvas has been built
    /// </summary>
    public static bool Exists => instance != null;

    /// <summary>
    /// The prompts canvas, built the first time it's needed
    /// </summary>
    public static ControllerPrompts Instance
    {
        get
        {
            if (instance == null)
                instance = Create();
            return instance;
        }
    }

    public bool IsHintShown => hintPanel.activeSelf;
    public string HintText => hintText.text;

    ///<summary>
    /// The message shown over a player's view while their controller is missing
    ///</summary>
    public static string ReconnectMessage(int slot)
    {
        return $"P{slot + 1} controller disconnected\nPress a button on any controller to continue";
    }

    ///<summary>
    /// What to tell someone who pressed a device that can't play: keyboards and mice need a controller, anything else isn't supported
    ///</summary>
    public static string HintForDevice(InputDevice device)
    {
        return device == null || device is Keyboard || device is Mouse ? CONNECT_CONTROLLER_HINT : UNSUPPORTED_CONTROLLER_HINT;
    }

    ///<summary>
    /// Covers a player's viewport with the reconnect message
    ///</summary>
    public void ShowReconnect(int slot, Rect viewport)
    {
        RectTransform area = (RectTransform)reconnectPanels[slot].transform;
        area.anchorMin = viewport.min;
        area.anchorMax = viewport.max;

        reconnectTexts[slot].text = ReconnectMessage(slot);
        reconnectPanels[slot].SetActive(true);
    }

    ///<summary>
    /// Hides a player's reconnect message
    ///</summary>
    public void HideReconnect(int slot)
    {
        reconnectPanels[slot].SetActive(false);
    }

    public bool IsReconnectShown(int slot)
    {
        return reconnectPanels[slot].activeSelf;
    }

    ///<summary>
    /// The part of the screen a reconnect message covers, as a 0-1 rect like a camera viewport
    ///</summary>
    public Rect ReconnectArea(int slot)
    {
        RectTransform area = (RectTransform)reconnectPanels[slot].transform;
        return Rect.MinMaxRect(area.anchorMin.x, area.anchorMin.y, area.anchorMax.x, area.anchorMax.y);
    }

    ///<summary>
    /// Shows a message along the bottom of the screen for a few seconds
    ///</summary>
    public void ShowHint(string message, float seconds)
    {
        hintText.text = message;
        hintPanel.SetActive(true);
        hintHideTime = Time.unscaledTime + seconds;
    }

    private void Update()
    {
        // Unscaled time, so hints still go away while the game is paused
        if (hintPanel.activeSelf && Time.unscaledTime >= hintHideTime)
            hintPanel.SetActive(false);
    }

    ///<summary>
    /// Builds the overlay canvas: one reconnect panel per player and the hint bar, all hidden
    ///</summary>
    static ControllerPrompts Create()
    {
        GameObject root = new GameObject(nameof(ControllerPrompts), typeof(Canvas), typeof(CanvasScaler));
        if (Application.isPlaying)
            DontDestroyOnLoad(root);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // above every menu and HUD canvas

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        ControllerPrompts prompts = root.AddComponent<ControllerPrompts>();
        for (int i = 0; i < Constants.MAX_PLAYERS; i++)
        {
            prompts.reconnectPanels[i] = prompts.CreatePanel("Reconnect P" + (i + 1), 0.75f, 40f, out prompts.reconnectTexts[i]);
        }

        prompts.hintPanel = prompts.CreatePanel("Hint", 0.6f, 36f, out prompts.hintText);
        RectTransform hintArea = (RectTransform)prompts.hintPanel.transform;
        hintArea.anchorMin = new Vector2(0f, 0.05f);
        hintArea.anchorMax = new Vector2(1f, 0.15f);

        return prompts;
    }

    ///<summary>
    /// A hidden, dark, full-screen panel with centred white text
    ///</summary>
    GameObject CreatePanel(string panelName, float backgroundAlpha, float fontSize, out TMP_Text text)
    {
        GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        Stretch((RectTransform)panel.transform);

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, backgroundAlpha);
        background.raycastTarget = false;

        GameObject label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(panel.transform, false);
        Stretch((RectTransform)label.transform);

        text = label.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        panel.SetActive(false);
        return panel;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
```

`PlayerInstantiate.cs` — in `AddPlayerReference`, the control-scheme check becomes:

```csharp
        if(playerInput.currentControlScheme != "Gamepad")
        {
            // Only controllers can play: outside a match, tell whoever pressed a key or an unsupported controller why nothing happened
            if (allowPlayerSpawn || playerCount == 0)
                ShowUnsupportedDeviceHint(playerInput.devices.Count > 0 ? playerInput.devices[0] : null);

            Destroy(playerInput.gameObject);
            return;
        }
```

and add, right above `OnPlayerControllerLost`:

```csharp
    ///<summary>
    /// Tells whoever pressed a keyboard, mouse or unsupported controller that they need a controller
    ///</summary>
    private void ShowUnsupportedDeviceHint(InputDevice device)
    {
        ControllerPrompts.Instance.ShowHint(ControllerPrompts.HintForDevice(device), ControllerPrompts.HINT_SECONDS);
    }
```

- [ ] **Step 4: Run to watch them pass, then the whole suite**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh ControllerPromptsTests` → Expected: `9 total, 9 passed, 0 failed`.
Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh` → Expected: `44 total, 44 passed, 0 failed`.

- [ ] **Step 5: Checkpoint** — files: `Assets/Scripts/UI/ControllerPrompts.cs(.meta)`, `Assets/Tests/Editor/ControllerPromptsTests.cs(.meta)`, `Assets/Scripts/Player/PlayerInstantiate.cs`.

---

## Task 2: Any controller can take over a disconnected player

Roadmap 1.3 + Review Focus 1: today only the same controller coming back gets the player back. A different controller joins as a new player (or, with four players, can't join at all) and the disconnected player is stuck; in the lobby that also blocks the ready-up.

**Files:**
- Modify: `Assets/Scripts/Player/ControllerDisconnectPolicy.cs` (add `PickSlotForReplacement`)
- Create: `Assets/Scripts/Player/ReplacementControllerListener.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Player/PlayerInstantiate.cs` (usings, fields, `OnDisable`, `AddPlayerReference` listeners, `RemovePlayerRef`, `ClearPlayerArray`, `OnPlayerControllerLost`, new handlers)
- Modify: `Assets/Scripts/Player/BallDriving.cs` (add `SetGamepad`, after `UnfreezeBallForGameState`)
- Test: `Assets/Tests/Editor/ControllerDisconnectPolicyTests.cs` (3 tests), create `Assets/Tests/Editor/ReplacementControllerListenerTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `ControllerPrompts.Exists`, `.Instance.ShowReconnect/HideReconnect` (Task 1); `PlayerInstantiate.ShowUnsupportedDeviceHint(InputDevice)` (Task 1).
- Produces: `static int ControllerDisconnectPolicy.PickSlotForReplacement(bool[] slotIsMissingController)` (-1 when nobody is missing one); `ReplacementControllerListener(Action<Gamepad> onGamepadPressed, Action<InputDevice> onOtherDevicePressed)` with `bool IsListening` and `void SetListening(bool listen)`; `void BallDriving.SetGamepad(Gamepad gamepad)`.

- [ ] **Step 1: Write the failing tests**

Append to `ControllerDisconnectPolicyTests`:

```csharp
        [Test]
        public void PickSlotForReplacement_GoesToThePlayerMissingAController()
        {
            Assert.AreEqual(2, ControllerDisconnectPolicy.PickSlotForReplacement(new[] { false, false, true, false }));
        }

        [Test]
        public void PickSlotForReplacement_SeveralMissing_LowestPlayerFirst()
        {
            Assert.AreEqual(1, ControllerDisconnectPolicy.PickSlotForReplacement(new[] { false, true, false, true }));
        }

        [Test]
        public void PickSlotForReplacement_NobodyMissing_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, ControllerDisconnectPolicy.PickSlotForReplacement(new[] { false, false, false, false }));
        }
```

`Assets/Tests/Editor/ReplacementControllerListenerTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace DoA.Tests
{
    public class ReplacementControllerListenerTests
    {
        readonly TestObjects objects = new TestObjects();
        ReplacementControllerListener listener;
        PlayerInputManager joinManager;

        [SetUp]
        public void SetUp()
        {
            listener = new ReplacementControllerListener(pad => { }, device => { });
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                listener.SetListening(false);
            }
            finally
            {
                // Clears PlayerInputManager.instance, so later tests start without one
                if (joinManager != null)
                    Reflect.Invoke(joinManager, "OnDisable");
                joinManager = null;
                objects.DestroyAll();
            }
        }

        PlayerInputManager CreateJoinManager()
        {
            joinManager = objects.Add<PlayerInputManager>();
            Reflect.Invoke(joinManager, "OnEnable"); // becomes PlayerInputManager.instance and allows joining, like the game's
            return joinManager;
        }

        [Test]
        public void Listening_WatchesControllersNobodyIsUsing()
        {
            int before = InputUser.listenForUnpairedDeviceActivity;

            listener.SetListening(true);

            Assert.AreEqual(before + 1, InputUser.listenForUnpairedDeviceActivity);
        }

        [Test]
        public void StartingTwiceThenStopping_LeavesNothingListening()
        {
            int before = InputUser.listenForUnpairedDeviceActivity;

            listener.SetListening(true);
            listener.SetListening(true);
            listener.SetListening(false);

            Assert.AreEqual(before, InputUser.listenForUnpairedDeviceActivity);
            Assert.IsFalse(listener.IsListening);
        }

        [Test]
        public void WhileListening_NewPlayersCannotJoin()
        {
            PlayerInputManager manager = CreateJoinManager();

            listener.SetListening(true);

            Assert.IsFalse(manager.joiningEnabled);
        }

        [Test]
        public void AfterListening_NewPlayersCanJoinAgain()
        {
            PlayerInputManager manager = CreateJoinManager();

            listener.SetListening(true);
            listener.SetListening(false);

            Assert.IsTrue(manager.joiningEnabled);
        }
    }
}
```

Stubs so it compiles: in `ControllerDisconnectPolicy` add `public static int PickSlotForReplacement(bool[] slotIsMissingController) { throw new System.NotImplementedException(); }`; create `ReplacementControllerListener.cs` with the constructor (stores nothing), `public bool IsListening { get; private set; }` and `public void SetListening(bool listen) { throw new System.NotImplementedException(); }`; `newmeta.sh` for the two new files.

- [ ] **Step 2: Run to watch them fail**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh "ControllerDisconnectPolicyTests|ReplacementControllerListenerTests"`
Expected: `13 total, 6 passed, 7 failed` — the 6 existing policy tests pass; the 7 new ones fail with `NotImplementedException`.

- [ ] **Step 3: Implement**

`ControllerDisconnectPolicy.cs` — replace the stub:

```csharp
    ///<summary>
    /// Which player a newly pressed controller belongs to: the first one missing a controller, or -1 if nobody is
    ///</summary>
    public static int PickSlotForReplacement(bool[] slotIsMissingController)
    {
        for (int i = 0; i < slotIsMissingController.Length; i++)
        {
            if (slotIsMissingController[i])
                return i;
        }

        return -1;
    }
```

`ReplacementControllerListener.cs` — full file:

```csharp
using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

/// <summary>
/// While a player's controller is missing, catches button presses on controllers nobody is using, so any controller
/// can take that player's place. New players can't join meanwhile, otherwise the press would add a new player
/// instead (and with four players nobody could join at all).
/// </summary>
public class ReplacementControllerListener
{
    readonly Action<Gamepad> onGamepadPressed;
    readonly Action<InputDevice> onOtherDevicePressed;

    public bool IsListening { get; private set; }

    public ReplacementControllerListener(Action<Gamepad> onGamepadPressed, Action<InputDevice> onOtherDevicePressed)
    {
        this.onGamepadPressed = onGamepadPressed;
        this.onOtherDevicePressed = onOtherDevicePressed;
    }

    ///<summary>
    /// Starts or stops listening; asking for the current state again does nothing
    ///</summary>
    public void SetListening(bool listen)
    {
        if (listen == IsListening)
            return;

        IsListening = listen;

        if (listen)
        {
            InputUser.onUnpairedDeviceUsed += OnUnpairedDeviceUsed;
            InputUser.listenForUnpairedDeviceActivity++;
        }
        else
        {
            InputUser.onUnpairedDeviceUsed -= OnUnpairedDeviceUsed;
            InputUser.listenForUnpairedDeviceActivity--;
        }

        // The game always allows joining (PlayerInstantiate decides who may spawn), so stopping turns it back on
        PlayerInputManager joinManager = PlayerInputManager.instance;
        if (joinManager != null)
        {
            if (listen)
                joinManager.DisableJoining();
            else
                joinManager.EnableJoining();
        }
    }

    private void OnUnpairedDeviceUsed(InputControl control, InputEventPtr eventPtr)
    {
        // Only button presses count, not stick movement
        if (!(control is ButtonControl))
            return;

        if (control.device is Gamepad gamepad)
            onGamepadPressed(gamepad);
        else
            onOtherDevicePressed(control.device);
    }
}
```

`BallDriving.cs` — after `UnfreezeBallForGameState`:

```csharp
    /// <summary>
    /// Sends this player's rumble to another controller (after it takes over from a disconnected one)
    /// </summary>
    public void SetGamepad(Gamepad gamepad)
    {
        pad = gamepad;
    }
```

`PlayerInstantiate.cs`:
- Add `using UnityEngine.InputSystem.Users;`.
- Under `[Header("Other")]`, after `PlayerGamepads`:

```csharp
    ReplacementControllerListener replacementListener;
    ReplacementControllerListener ReplacementListener =>
        replacementListener ??= new ReplacementControllerListener(GiveControllerToMissingPlayer, ShowUnsupportedDeviceHint);
```

- End of `OnDisable`:

```csharp
        // Stops waiting for replacement controllers
        replacementListener?.SetListening(false);
```

- In `AddPlayerReference`, the device-lost block becomes:

```csharp
        // Pauses the match if this player's controller disconnects, and clears the reconnect message when it comes back
        playerInput.deviceLostEvent.AddListener(OnPlayerControllerLost);
        playerInput.deviceRegainedEvent.AddListener(OnPlayerControllerRegained);
```

- In `RemovePlayerRef`, after `UpdatePlayerCameraRects();` and at the end of `ClearPlayerArray` add `UpdateControllerPrompts();` (a player who leaves while disconnected takes their reconnect message with them; the viewports of the rest move).
- In `OnPlayerControllerLost`, right after the `if (lostSlot < 0) return;` guard:

```csharp
        // Shows "reconnect" on their view and lets any controller take their place
        UpdateControllerPrompts();
```

- New methods after `OnPlayerControllerLost`:

```csharp
    ///<summary>
    /// Clears the reconnect message when a player's own controller comes back
    ///</summary>
    private void OnPlayerControllerRegained(PlayerInput player)
    {
        UpdateControllerPrompts();
    }

    ///<summary>
    /// A controller nobody was using was pressed while a player's controller is missing: it becomes that player's controller
    ///</summary>
    private void GiveControllerToMissingPlayer(Gamepad gamepad)
    {
        int slot = ControllerDisconnectPolicy.PickSlotForReplacement(SlotsMissingController());
        if (slot < 0)
            return;

        PlayerInput player = availiblePlayerInputs[slot];

        // Replaces the lost controller too, so it won't also drive this player if it comes back later
        InputUser.PerformPairingWithDevice(gamepad, player.user, InputUserPairingOptions.UnpairCurrentDevicesFromUser);

        // Rumble follows the new controller
        playerGamepads[slot] = gamepad;
        player.GetComponentInChildren<BallDriving>().SetGamepad(gamepad);

        UpdateControllerPrompts();
    }

    ///<summary>
    /// Which joined players have no working controller right now
    ///</summary>
    private bool[] SlotsMissingController()
    {
        bool[] missing = new bool[availiblePlayerInputs.Length];
        for (int i = 0; i < availiblePlayerInputs.Length; i++)
        {
            missing[i] = availiblePlayerInputs[i] != null && availiblePlayerInputs[i].hasMissingRequiredDevices;
        }
        return missing;
    }
    // FINAL REVIEW FIX (2026-09-23): hasMissingRequiredDevices is stale while DeviceLost / DeviceRegained fire (the Input
    // System updates it right after notifying). Shipped code uses PlayerInstantiate.IsMissingController(user), which checks
    // user.lostDevices.Count > 0 — pinned by MissingControllerTests.

    ///<summary>
    /// Shows "reconnect" over every player missing a controller and waits for a controller to replace theirs; clears it all once nobody is missing one
    ///</summary>
    private void UpdateControllerPrompts()
    {
        bool[] missing = SlotsMissingController();
        bool anyMissing = ControllerDisconnectPolicy.PickSlotForReplacement(missing) >= 0;

        // Nothing to show and nothing on screen to hide
        if (!anyMissing && !ControllerPrompts.Exists)
        {
            ReplacementListener.SetListening(false);
            return;
        }

        for (int i = 0; i < missing.Length; i++)
        {
            if (missing[i])
                ControllerPrompts.Instance.ShowReconnect(i, availiblePlayerInputs[i].camera.rect);
            else
                ControllerPrompts.Instance.HideReconnect(i);
        }

        ReplacementListener.SetListening(anyMissing);
    }
```

- [ ] **Step 4: Run to watch them pass, then the whole suite**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh "ControllerDisconnectPolicyTests|ReplacementControllerListenerTests"` → Expected: `13 total, 13 passed, 0 failed`.
Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh` → Expected: `51 total, 51 passed, 0 failed` (including `PlayerSlotTests`, whose `RemovePlayerRef` now also reaches `UpdateControllerPrompts`).

- [ ] **Step 5: Checkpoint** — files: `ControllerDisconnectPolicy.cs`, `ReplacementControllerListener.cs(.meta)`, `PlayerInstantiate.cs`, `BallDriving.cs`, `ControllerDisconnectPolicyTests.cs`, `ReplacementControllerListenerTests.cs(.meta)`. The pairing itself needs a real Play Mode session: verified by the partner with Task 3's test players (F1, F3, F1 — see Task 6).

---

## Task 3: Test players (developer tool)

Roadmap §R needs 1–4 players and 1.4 needs a 4-player profile; this lets one developer do both with one controller.

**Files:**
- Create: `Assets/Scripts/Management/TestPlayers.cs` (+ `.meta`)
- Create: `Assets/Tests/Editor/TestPlayersTests.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Management/HotKeys.cs` (`Update`)

**Interfaces:**
- Produces: `TestPlayers` — `const string PAD_NAME`, `IReadOnlyList<Gamepad> Pads`, `Gamepad Add()`, `void PressSouthOnAll()`, `void ToggleNewest()`, `void RemoveAll()`. Hotkeys (editor/development builds): F1 add, F2 press A on all, F3 unplug/replug the newest.

- [ ] **Step 1: Write the failing tests** — `Assets/Tests/Editor/TestPlayersTests.cs`

```csharp
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    public class TestPlayersTests
    {
        [TearDown]
        public void TearDown()
        {
            TestPlayers.RemoveAll();
        }

        [Test]
        public void Add_PlugsInAVirtualController()
        {
            Gamepad pad = TestPlayers.Add();

            Assert.IsTrue(pad.added);
            Assert.AreEqual(TestPlayers.PAD_NAME, pad.name);
            CollectionAssert.Contains(InputSystem.devices, pad);
        }

        [Test]
        public void ToggleNewest_UnplugsOnlyTheNewestController()
        {
            Gamepad first = TestPlayers.Add();
            Gamepad newest = TestPlayers.Add();

            TestPlayers.ToggleNewest();

            Assert.IsTrue(first.added);
            Assert.IsFalse(newest.added);
        }

        [Test]
        public void ToggleNewest_Twice_PlugsItBackIn()
        {
            Gamepad pad = TestPlayers.Add();

            TestPlayers.ToggleNewest();
            TestPlayers.ToggleNewest();

            Assert.IsTrue(pad.added);
        }

        [Test]
        public void RemoveAll_UnplugsEveryTestController()
        {
            Gamepad first = TestPlayers.Add();
            Gamepad second = TestPlayers.Add();

            TestPlayers.RemoveAll();

            Assert.IsFalse(first.added);
            Assert.IsFalse(second.added);
            Assert.AreEqual(0, TestPlayers.Pads.Count);
        }
    }
}
```

Stub: `TestPlayers` static class with `PAD_NAME` and every member throwing `NotImplementedException` (`Pads` included); `newmeta.sh` for both files.

- [ ] **Step 2: Run to watch them fail**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh TestPlayersTests`
Expected: `4 total, 0 passed, 4 failed` (`NotImplementedException`).

- [ ] **Step 3: Implement** — `TestPlayers.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Developer tool: virtual controllers that join as extra players, so 2-4 player split-screen, controller disconnects
/// and 4-player performance can be tested with one real controller. Driven by HotKeys (editor and development builds only).
/// </summary>
public static class TestPlayers
{
    public const string PAD_NAME = "DoA Test Pad";

    static readonly List<Gamepad> pads = new List<Gamepad>();
    static bool removeOnQuitHooked;

    /// <summary>
    /// Test controllers added so far, oldest first (unplugged ones included)
    /// </summary>
    public static IReadOnlyList<Gamepad> Pads => pads;

    ///<summary>
    /// Plugs in a virtual controller and presses A on it, which joins it as the next player
    ///</summary>
    public static Gamepad Add()
    {
        if (!removeOnQuitHooked)
        {
            // Devices outlive Play Mode in the editor, so test controllers are removed when it ends
            Application.quitting += RemoveAll;
            removeOnQuitHooked = true;
        }

        Gamepad pad = InputSystem.AddDevice<Gamepad>(PAD_NAME);
        pads.Add(pad);
        PressSouth(pad);
        return pad;
    }

    ///<summary>
    /// Presses A on every plugged-in test controller (ready up, confirm the loading screen)
    ///</summary>
    public static void PressSouthOnAll()
    {
        foreach (Gamepad pad in pads)
        {
            if (pad.added)
                PressSouth(pad);
        }
    }

    ///<summary>
    /// Unplugs the newest test controller, or plugs it back in if it's unplugged
    ///</summary>
    public static void ToggleNewest()
    {
        if (pads.Count == 0)
            return;

        Gamepad newest = pads[pads.Count - 1];
        if (newest.added)
            InputSystem.RemoveDevice(newest);
        else
            InputSystem.AddDevice(newest);
    }

    ///<summary>
    /// Unplugs and forgets every test controller
    ///</summary>
    public static void RemoveAll()
    {
        foreach (Gamepad pad in pads)
        {
            if (pad.added)
                InputSystem.RemoveDevice(pad);
        }
        pads.Clear();

        Application.quitting -= RemoveAll;
        removeOnQuitHooked = false;
    }

    ///<summary>
    /// Queues a press and release of A; the next input update sees a full button press
    ///</summary>
    static void PressSouth(Gamepad pad)
    {
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
        InputSystem.QueueStateEvent(pad, new GamepadState());
    }
}
```

`HotKeys.cs` — at the end of `Update`:

```csharp

        // Test players: F1 adds one, F2 presses A on all of them, F3 unplugs / replugs the newest
        if (DevTools.GetKeyDown(KeyCode.F1))
        {
            TestPlayers.Add();
        }

        if (DevTools.GetKeyDown(KeyCode.F2))
        {
            TestPlayers.PressSouthOnAll();
        }

        if (DevTools.GetKeyDown(KeyCode.F3))
        {
            TestPlayers.ToggleNewest();
        }
```

- [ ] **Step 4: Run to watch them pass, then the whole suite**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh TestPlayersTests` → Expected: `4 total, 4 passed, 0 failed`.
Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh` → Expected: `55 total, 55 passed, 0 failed` (`DevToolsTests.GameplayScripts_ReadDebugKeysOnlyThroughDevTools` still passes: the new keys go through `DevTools`).

- [ ] **Step 5: Checkpoint** — files: `TestPlayers.cs(.meta)`, `TestPlayersTests.cs(.meta)`, `HotKeys.cs`.

---

## Task 4: Phase camera texture sized to the player's view

Roadmap 1.4: `PlayerCameraResizer.cs:88` renders every player's phase transition into a fixed 1920×1080 texture — 4× the pixels a 4-player view needs, stretched on 16:10 (Steam Deck) and 21:9 screens, and never freed.

**Files:**
- Modify: `Assets/Scripts/Player/PlayerCameraResizer.cs` (`Start`, `Update`, new `ResizePhaseTexture` / `ReleasePhaseTexture` / `OnDestroy`)
- Test: `Assets/Tests/Editor/PlayerCameraResizerTests.cs` (5 tests)

**Interfaces:**
- Produces: nothing public; the phase texture always matches `referenceCam.pixelWidth × pixelHeight` (at least 1×1).

- [ ] **Step 1: Write the failing tests** — in `PlayerCameraResizerTests`, add `using System.Collections.Generic;` and `using UnityEngine.UI;`, replace the field + `TearDown` with:

```csharp
        readonly TestObjects objects = new TestObjects();
        readonly List<Object> textures = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object texture in textures)
            {
                if (texture != null)
                    Object.DestroyImmediate(texture);
            }
            textures.Clear();
            objects.DestroyAll();
        }
```

and add after `CreateResizer`:

```csharp
        /// <summary>
        /// A resizer whose reference camera draws into a screen-sized texture, so viewport sizes in pixels don't depend on the test machine
        /// </summary>
        PlayerCameraResizer CreateResizerWithPhaseCamera(int screenWidth, int screenHeight, out Camera reference, out Camera phase)
        {
            PlayerCameraResizer resizer = CreateResizer(out reference, out _);
            reference.targetTexture = NewScreen(screenWidth, screenHeight);

            phase = objects.Add<Camera>();
            GameObject phaseRender = objects.NewGameObject("Phase Render");
            phaseRender.AddComponent<Image>();
            Material transition = new Material(Shader.Find("UI/Default"));
            textures.Add(transition);

            Reflect.SetField(resizer, "phaseCamera", phase);
            Reflect.SetField(resizer, "phaseRender", phaseRender);
            Reflect.SetField(resizer, "phaseTransitionMaterial", transition);
            return resizer;
        }

        RenderTexture NewScreen(int width, int height)
        {
            RenderTexture screen = new RenderTexture(width, height, 0);
            textures.Add(screen);
            return screen;
        }

        Vector2Int PhaseTextureSize(Camera phase)
        {
            textures.Add(phase.targetTexture);
            return new Vector2Int(phase.targetTexture.width, phase.targetTexture.height);
        }

        [Test]
        public void PhaseTexture_FourPlayersAt1080p_IsAQuarterOfTheScreen()
        {
            PlayerCameraResizer resizer = CreateResizerWithPhaseCamera(1920, 1080, out Camera reference, out Camera phase);
            reference.rect = new Rect(0.5f, 0f, 0.5f, 0.5f);

            Reflect.Invoke(resizer, "Start");

            Assert.AreEqual(new Vector2Int(960, 540), PhaseTextureSize(phase));
        }

        [Test]
        public void PhaseTexture_TwoPlayersOnSteamDeck_KeepsTheScreensShape()
        {
            PlayerCameraResizer resizer = CreateResizerWithPhaseCamera(1280, 800, out Camera reference, out Camera phase);
            reference.rect = new Rect(0.25f, 0.5f, 0.5f, 0.5f);

            Reflect.Invoke(resizer, "Start");

            Assert.AreEqual(new Vector2Int(640, 400), PhaseTextureSize(phase));
        }

        [Test]
        public void PhaseTexture_ShrinksWhenMorePlayersJoin()
        {
            PlayerCameraResizer resizer = CreateResizerWithPhaseCamera(1920, 1080, out Camera reference, out Camera phase);
            Reflect.Invoke(resizer, "Start");
            textures.Add(phase.targetTexture);

            reference.rect = new Rect(0f, 0f, 0.5f, 0.5f); // a 3rd and 4th player joined
            Reflect.Invoke(resizer, "Update");

            Assert.AreEqual(new Vector2Int(960, 540), PhaseTextureSize(phase));
        }

        [Test]
        public void PhaseTexture_FollowsTheWindowSize()
        {
            PlayerCameraResizer resizer = CreateResizerWithPhaseCamera(1920, 1080, out Camera reference, out Camera phase);
            Reflect.Invoke(resizer, "Start");
            textures.Add(phase.targetTexture);

            reference.targetTexture = NewScreen(2560, 1080); // moved to an ultrawide monitor
            Reflect.Invoke(resizer, "Update");

            Assert.AreEqual(new Vector2Int(2560, 1080), PhaseTextureSize(phase));
        }

        [Test]
        public void PhaseTexture_ZeroSizeViewport_IsAtLeastOnePixel()
        {
            PlayerCameraResizer resizer = CreateResizerWithPhaseCamera(1920, 1080, out Camera reference, out Camera phase);
            reference.rect = new Rect(0f, 0f, 0f, 0f);

            Reflect.Invoke(resizer, "Start");

            Assert.AreEqual(new Vector2Int(1, 1), PhaseTextureSize(phase));
        }
```

- [ ] **Step 2: Run to watch them fail**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh PlayerCameraResizerTests`
Expected: `7 total, 2 passed, 5 failed` — the 2 follower-camera tests pass; each new test reports `Expected: (…) But was: (1920, 1080)`.

- [ ] **Step 3: Implement** — `PlayerCameraResizer.cs`

`Start` becomes:

```csharp
    private void Start()
    {
        phaseTransitionMaterialMain = new Material(phaseTransitionMaterial);

        ResizePhaseTexture();

        phaseRender.GetComponent<Image>().material = phaseTransitionMaterialMain;
    }
```

In `Update`, between the phase-camera follow block and `// Waits until the player is given their first split-screen viewport`:

```csharp
        // Keeps the phase transition texture the size of this player's view (players joining or leaving, window resized)
        if (phaseCameraRT != null)
            ResizePhaseTexture();

```

New methods after `Update`:

```csharp
    ///<summary>
    /// Renders the phase camera at this player's view size in pixels, so the transition isn't stretched or drawn bigger than the view
    ///</summary>
    private void ResizePhaseTexture()
    {
        int width = Mathf.Max(1, referenceCam.pixelWidth);
        int height = Mathf.Max(1, referenceCam.pixelHeight);

        if (phaseCameraRT != null && phaseCameraRT.width == width && phaseCameraRT.height == height)
            return;

        ReleasePhaseTexture();

        phaseCameraRT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        phaseCamera.targetTexture = phaseCameraRT;
        phaseTransitionMaterialMain.SetTexture(HashReference._mainTexProperty, phaseCameraRT);
    }

    ///<summary>
    /// Frees the phase camera's texture
    ///</summary>
    private void ReleasePhaseTexture()
    {
        if (phaseCameraRT == null)
            return;

        if (phaseCamera != null)
            phaseCamera.targetTexture = null;

        phaseCameraRT.Release();

        if (Application.isPlaying)
            Destroy(phaseCameraRT);
        else
            DestroyImmediate(phaseCameraRT);

        phaseCameraRT = null;
    }

    private void OnDestroy()
    {
        ReleasePhaseTexture();
    }
```

- [ ] **Step 4: Run to watch them pass, then the whole suite**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh PlayerCameraResizerTests` → Expected: `7 total, 7 passed, 0 failed`.
Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh` → Expected: `60 total, 60 passed, 0 failed`.

- [ ] **Step 5: Checkpoint** — files: `PlayerCameraResizer.cs`, `PlayerCameraResizerTests.cs`.

---

## Task 5: Low / Medium / High graphics

Roadmap 1.4: the URP asset uses MSAA 8×, 500 m shadows and 4 cascades, and each of the 4 split-screen cameras renders its own shadow maps. High stays exactly as authored; Medium and Low cap those settings. The Steam Deck starts on Medium. The options screen gets an optional Quality row (the partner adds its UI, Task 6); until then the saved or default level applies, and F4 cycles levels for profiling.

**Files:**
- Create: `Assets/Scripts/Management/GraphicsQuality.cs`, `Assets/Tests/Editor/GraphicsQualityTests.cs` (+ `.meta`s)
- Modify: `Assets/Scripts/Menu/GameSettings.cs`, `Assets/Tests/Editor/GameSettingsTests.cs` (3 tests)
- Modify: `Assets/Scripts/Management/SteamManager.cs` (add `IsSteamDeck`), `Assets/Tests/Editor/SteamStartupTests.cs` (1 test)
- Modify: `Assets/Scripts/Menu/OptionsMenu.cs` (quality row + apply on load), `Assets/Scripts/Management/HotKeys.cs` (F4)

**Interfaces:**
- Produces: `GraphicsQuality` — `LOW = 0`, `MEDIUM = 1`, `HIGH = 2`, `LEVEL_COUNT = 3`, `string[] LevelNames`, `struct Preset { int msaaSamples; float shadowDistance; int shadowCascades; Preset(int, float, int) }`, `Preset PresetFor(int level, Preset authored)`, `int DefaultLevel(bool isSteamDeck)`, `Preset Read(UniversalRenderPipelineAsset)`, `void Apply(int level)`, `void Apply(int level, UniversalRenderPipelineAsset asset)`, `void Restore()`, `int CurrentLevel`.
- Produces: `GameSettings.qualityLevel` and `GameSettings(int bgm, int sfx, int fullscreen, int qualityLevel = GraphicsQuality.HIGH)`; saved as a `quality=` line.
- Produces: `static bool SteamManager.IsSteamDeck`.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/GraphicsQualityTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DoA.Tests
{
    public class GraphicsQualityTests
    {
        // The project's URP asset today: MSAA 8x, 500 m shadows, 4 cascades
        static readonly GraphicsQuality.Preset Authored = new GraphicsQuality.Preset(8, 500f, 4);

        UniversalRenderPipelineAsset asset;

        [SetUp]
        public void SetUp()
        {
            asset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            asset.msaaSampleCount = 8;
            asset.shadowDistance = 500f;
            asset.shadowCascadeCount = 4;
        }

        [TearDown]
        public void TearDown()
        {
            GraphicsQuality.Restore();
            Object.DestroyImmediate(asset);
        }

        static void AssertPreset(int msaa, float shadowDistance, int cascades, GraphicsQuality.Preset actual)
        {
            Assert.AreEqual(msaa, actual.msaaSamples, "MSAA samples");
            Assert.AreEqual(shadowDistance, actual.shadowDistance, "shadow distance");
            Assert.AreEqual(cascades, actual.shadowCascades, "shadow cascades");
        }

        [Test]
        public void High_IsTheUrpAssetAsAuthored()
        {
            AssertPreset(8, 500f, 4, GraphicsQuality.PresetFor(GraphicsQuality.HIGH, Authored));
        }

        [Test]
        public void Medium_CapsAntiAliasingAndShadows()
        {
            AssertPreset(4, 200f, 2, GraphicsQuality.PresetFor(GraphicsQuality.MEDIUM, Authored));
        }

        [Test]
        public void Low_TurnsOffAntiAliasingAndShortensShadows()
        {
            AssertPreset(1, 100f, 1, GraphicsQuality.PresetFor(GraphicsQuality.LOW, Authored));
        }

        [Test]
        public void Presets_NeverRaiseSettingsAboveTheAuthoredAsset()
        {
            GraphicsQuality.Preset modest = new GraphicsQuality.Preset(2, 80f, 1);

            AssertPreset(2, 80f, 1, GraphicsQuality.PresetFor(GraphicsQuality.MEDIUM, modest));
            AssertPreset(1, 80f, 1, GraphicsQuality.PresetFor(GraphicsQuality.LOW, modest));
        }

        [Test]
        public void DefaultLevel_SteamDeckStartsOnMedium()
        {
            Assert.AreEqual(GraphicsQuality.MEDIUM, GraphicsQuality.DefaultLevel(isSteamDeck: true));
            Assert.AreEqual(GraphicsQuality.HIGH, GraphicsQuality.DefaultLevel(isSteamDeck: false));
        }

        [Test]
        public void Apply_ChangesTheUrpAsset()
        {
            GraphicsQuality.Apply(GraphicsQuality.LOW, asset);

            AssertPreset(1, 100f, 1, GraphicsQuality.Read(asset));
        }

        [Test]
        public void Apply_HighAfterLow_BringsBackTheAuthoredSettings()
        {
            GraphicsQuality.Apply(GraphicsQuality.LOW, asset);
            GraphicsQuality.Apply(GraphicsQuality.HIGH, asset);

            AssertPreset(8, 500f, 4, GraphicsQuality.Read(asset));
        }

        [Test]
        public void Restore_PutsTheUrpAssetBackAsAuthored()
        {
            GraphicsQuality.Apply(GraphicsQuality.LOW, asset);

            GraphicsQuality.Restore(); // what exiting Play Mode does in the editor

            AssertPreset(8, 500f, 4, GraphicsQuality.Read(asset));
        }

        [Test]
        public void Apply_UnknownLevel_UsesTheNearestOne()
        {
            GraphicsQuality.Apply(7, asset);

            Assert.AreEqual(GraphicsQuality.HIGH, GraphicsQuality.CurrentLevel);
        }
    }
}
```

Append to `GameSettingsTests`:

```csharp
        [Test]
        public void SavedQuality_LoadsBack()
        {
            GameSettings saved = new GameSettings(3, 9, 0, GraphicsQuality.LOW);

            Assert.AreEqual(GraphicsQuality.LOW, GameSettings.Parse(saved.ToText(), Defaults).qualityLevel);
        }

        [Test]
        public void SettingsFromBeforeQualityExisted_KeepTheDefaultQuality()
        {
            GameSettings deckDefaults = new GameSettings(5, 6, 1, GraphicsQuality.MEDIUM);

            Assert.AreEqual(GraphicsQuality.MEDIUM, GameSettings.Parse("bgm=3\nsfx=9\nfullscreen=0\n", deckDefaults).qualityLevel);
        }

        [Test]
        public void OutOfRangeQuality_IsClamped()
        {
            GameSettings mediumDefaults = new GameSettings(5, 6, 1, GraphicsQuality.MEDIUM);

            Assert.AreEqual(GraphicsQuality.HIGH, GameSettings.Parse("quality=9", mediumDefaults).qualityLevel);
            Assert.AreEqual(GraphicsQuality.LOW, GameSettings.Parse("quality=-5", mediumDefaults).qualityLevel);
        }
```

Append to `SteamStartupTests`:

```csharp
        [Test]
        public void IsSteamDeck_WithoutSteam_IsFalse()
        {
            // No SteamManager runs in Edit Mode, like a player without Steam
            Assert.IsFalse(SteamManager.IsSteamDeck);
        }
```

Stubs so it compiles:
- `GraphicsQuality.cs`: the constants, `LevelNames`, the `Preset` struct with its constructor, `CurrentLevel { get; private set; } = HIGH`, and every method throwing `NotImplementedException`.
- `GameSettings.cs`: add `public int qualityLevel;` and the optional constructor parameter (`this.qualityLevel = qualityLevel;`) only — `ToText`/`Parse` unchanged.
- `SteamManager.cs`: `public static bool IsSteamDeck => throw new NotImplementedException();` under `Initialized`.
- `newmeta.sh` for the two new files.

- [ ] **Step 2: Run to watch them fail**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh "GraphicsQualityTests|GameSettingsTests|SteamStartupTests"`
Expected: `21 total, 8 passed, 13 failed` — 9 `GraphicsQualityTests` and `IsSteamDeck_WithoutSteam_IsFalse` (`NotImplementedException`); the 3 new `GameSettingsTests` report `Expected: 0 But was: 2` (saved Low not read back), `Expected: 1 But was: 2` (default Medium ignored) and `Expected: 0 But was: 2` (`quality=-5` not clamped to Low).

- [ ] **Step 3: Implement**

`GraphicsQuality.cs` — full file:

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Low / Medium / High graphics. High is the project's URP asset as authored; the lower levels cap anti-aliasing
/// and shadows, which every split-screen camera pays for separately each frame.
/// </summary>
public static class GraphicsQuality
{
    public const int LOW = 0;
    public const int MEDIUM = 1;
    public const int HIGH = 2;
    public const int LEVEL_COUNT = 3;

    public static readonly string[] LevelNames = { "Low", "Medium", "High" };

    /// <summary>
    /// The URP settings a quality level changes
    /// </summary>
    public struct Preset
    {
        public int msaaSamples;
        public float shadowDistance;
        public int shadowCascades;

        public Preset(int msaaSamples, float shadowDistance, int shadowCascades)
        {
            this.msaaSamples = msaaSamples;
            this.shadowDistance = shadowDistance;
            this.shadowCascades = shadowCascades;
        }
    }

    static UniversalRenderPipelineAsset changedAsset;
    static Preset authoredSettings;

    /// <summary>
    /// The level applied last (High until another one is applied)
    /// </summary>
    public static int CurrentLevel { get; private set; } = HIGH;

    ///<summary>
    /// A level's settings, never above what the project's URP asset uses
    ///</summary>
    public static Preset PresetFor(int level, Preset authored)
    {
        switch (level)
        {
            case LOW:
                return new Preset(1, Mathf.Min(authored.shadowDistance, 100f), 1);
            case MEDIUM:
                return new Preset(Mathf.Min(authored.msaaSamples, 4), Mathf.Min(authored.shadowDistance, 200f), Mathf.Min(authored.shadowCascades, 2));
            default:
                return authored;
        }
    }

    ///<summary>
    /// The level used until the player picks one: Medium on a Steam Deck, High everywhere else
    ///</summary>
    public static int DefaultLevel(bool isSteamDeck)
    {
        return isSteamDeck ? MEDIUM : HIGH;
    }

    ///<summary>
    /// The quality-related settings a URP asset has now
    ///</summary>
    public static Preset Read(UniversalRenderPipelineAsset asset)
    {
        return new Preset(asset.msaaSampleCount, asset.shadowDistance, asset.shadowCascadeCount);
    }

    static void Write(UniversalRenderPipelineAsset asset, Preset preset)
    {
        asset.msaaSampleCount = preset.msaaSamples;
        asset.shadowDistance = preset.shadowDistance;
        asset.shadowCascadeCount = preset.shadowCascades;
    }

    ///<summary>
    /// Applies a level to the URP asset the game renders with
    ///</summary>
    public static void Apply(int level)
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)
            Apply(level, asset);
    }

    ///<summary>
    /// Applies a level to a URP asset, remembering its authored settings the first time so High can bring them back
    ///</summary>
    public static void Apply(int level, UniversalRenderPipelineAsset asset)
    {
        if (changedAsset != asset)
        {
            Restore();
            authoredSettings = Read(asset);
            changedAsset = asset;

            // In the editor the URP asset is a project file: exiting Play Mode puts it back as authored
            Application.quitting += Restore;
        }

        CurrentLevel = Mathf.Clamp(level, 0, LEVEL_COUNT - 1);
        Write(asset, PresetFor(CurrentLevel, authoredSettings));
    }

    ///<summary>
    /// Puts the changed URP asset back as authored
    ///</summary>
    public static void Restore()
    {
        if (changedAsset != null)
            Write(changedAsset, authoredSettings);

        changedAsset = null;
        CurrentLevel = HIGH;
        Application.quitting -= Restore;
    }
}
```

`GameSettings.cs` — the constructor keeps its first three parameters and gains `int qualityLevel = GraphicsQuality.HIGH`; `ToText` returns `$"bgm={bgmPosition}\nsfx={sfxPosition}\nfullscreen={fullscreenPosition}\nquality={qualityLevel}\n"`; `Parse` starts from `new GameSettings(defaults.bgmPosition, defaults.sfxPosition, defaults.fullscreenPosition, defaults.qualityLevel)` and gains:

```csharp
                case "quality":
                    settings.qualityLevel = Mathf.Clamp(value, 0, GraphicsQuality.LEVEL_COUNT - 1);
                    break;
```

`SteamManager.cs` — replace the stub:

```csharp
    /// <summary>
    /// True on a Steam Deck (only known once Steam is running)
    /// </summary>
    public static bool IsSteamDeck
    {
        get
        {
#if !DISABLESTEAMWORKS
            return Initialized && SteamUtils.IsSteamRunningOnSteamDeck();
#else
            return false;
#endif
        }
    }
```

`OptionsMenu.cs`:
- `OptionSelected` gains `QUALITY = 3`.
- New fields after the Fullscreen block:

```csharp
    [Header("Graphics Quality Info")]
    [Tooltip("Optional row: until the options screen has a Quality row, quality stays at its saved or default level")]
    [SerializeField] GameObject qualitySelector;
    [SerializeField] GameObject[] qualitySelectorPositions = new GameObject[0];
    int qualityPosition;

    bool QualityRowExists => qualitySelector != null && qualitySelectorPositions != null && qualitySelectorPositions.Length >= GraphicsQuality.LEVEL_COUNT;
```

- `SaveOptions`: `new GameSettings(bgmPosition, sfxPosition, fullscreenPosition, qualityPosition)`.
- `LoadOptions`: defaults become `new GameSettings(bgmPosition, sfxPosition, fullscreenPosition, GraphicsQuality.DefaultLevel(SteamManager.IsSteamDeck))`; after `fullscreenPosition = settings.fullscreenPosition;` add `qualityPosition = settings.qualityLevel;`; after `UpdateFullscreen(fullscreenPosition);` add `GraphicsQuality.Apply(qualityPosition);`.
- `UpdateSelectors`, at the end:

```csharp
        if (QualityRowExists)
            qualitySelector.transform.position = new Vector3(qualitySelectorPositions[qualityPosition].transform.position.x,
                qualitySelector.transform.position.y, qualitySelector.transform.position.z);
```

- `ScrollMenuLeftRight`, a branch after the FULLSCREEN one:

```csharp
        else if (optionSelected == OptionSelected.QUALITY && QualityRowExists)
        {
            // Positive Scroll
            if (direction)
            {
                if (qualityPosition < GraphicsQuality.LEVEL_COUNT - 1)
                    qualityPosition = qualityPosition + 1;
            }
            // Negative Scroll
            else
            {
                if (qualityPosition > 0)
                    qualityPosition = qualityPosition - 1;
            }

            qualitySelector.transform.position = new Vector3(qualitySelectorPositions[qualityPosition].transform.position.x,
                qualitySelector.transform.position.y, qualitySelector.transform.position.z);

            GraphicsQuality.Apply(qualityPosition);
        }
```

`HotKeys.cs` — after the test-player keys:

```csharp

        // F4 cycles graphics quality (not saved), to compare levels in the Profiler
        if (DevTools.GetKeyDown(KeyCode.F4))
        {
            GraphicsQuality.Apply((GraphicsQuality.CurrentLevel + 1) % GraphicsQuality.LEVEL_COUNT);
            Debug.Log("Graphics quality: " + GraphicsQuality.LevelNames[GraphicsQuality.CurrentLevel]);
        }
```

- [ ] **Step 4: Run to watch them pass, then the whole suite**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh "GraphicsQualityTests|GameSettingsTests|SteamStartupTests"` → Expected: `21 total, 21 passed, 0 failed`.
Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh` → Expected: `73 total, 73 passed, 0 failed`.

- [ ] **Step 5: Checkpoint** — files: `GraphicsQuality.cs(.meta)`, `GraphicsQualityTests.cs(.meta)`, `GameSettings.cs`, `GameSettingsTests.cs`, `SteamManager.cs`, `SteamStartupTests.cs`, `OptionsMenu.cs`, `HotKeys.cs`.

---

## Task 6: Roadmap, developer hotkeys and partner steps

No code; no tests (documentation).

**Files:**
- Modify: `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`
- Create: `docs/dev-hotkeys.md`

- [ ] **Step 1: Roadmap** — tick what Phase 1A and 1B finished (with the file that did it), leave editor-only items open with a pointer to the partner steps, and correct Task 1.5's Steam Cloud pattern from `settings.json` to `settings.cfg` (Phase 1A's file). In §R, note that F1–F3 stand in for missing controllers.
- [ ] **Step 2: `docs/dev-hotkeys.md`** — every key `DevTools.GetKeyDown` reads (file, key, effect), including F1–F4, and that release builds ignore them all.
- [ ] **Step 3: Partner steps** (in the roadmap, under Task 1.3/1.4):
  - Reconnect check: Play from `SplashScreen` → F1 twice (P2 joins) → F2 → start a match → F3 (P2's pad unplugged: pause + "P2 controller disconnected") → F1 (a new pad takes over P2; nobody new joins) → Resume.
  - Quality row: in `Alex Player Testing` → *Options Canvas*, duplicate the Fullscreen row as "Quality" with 3 selector positions (Low, Medium, High) → add the row's label to `selectorObjects` as the 4th entry → assign *Quality Selector* and the 3 positions on **OptionsMenu**.
  - Profiling: 4 players (F1 ×3) in the golden-order scene → Window ▸ Analysis ▸ Profiler → F4 through Low / Medium / High → note the frame time of each.
- [ ] **Step 4: Checkpoint** — files: the roadmap, `docs/dev-hotkeys.md`.
