// Trailer capture: never in the game's own project. tools/trailer/build.py copies this into a private snapshot of the
// game and builds a Windows player from it. See tools/trailer/README.md
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

/// <summary>
/// Plays one take with bots at every wheel and records it. Launched with -trailer and:
/// -out folder -name take -seats 1|2|4 -mode GoldRush -speed 100 -waves 3 -length Normal -round 3 -lowgravity 0|1
/// -golden 0|1 -hud 0|1 -menus 0|1 -seconds 420 -ffmpeg path -qp 16
/// Seats 1 hosts alone (full screen, three CPU rivals); 2 and 4 are local split screen.
/// Recorded the way a capture program does: real time, a 60 fps video filled from the wall clock (a slow frame repeats, and
/// the log counts how many), and the sound tapped from the mix, silently, so nothing plays out of the PC's speakers
/// </summary>
public class TrailerDirector : MonoBehaviour
{
    const int FPS = 60;
    const float RESULTS_HOLD = 16f;
    const float PRESS_EVERY = 0.5f;

    string folder, take, ffmpeg;
    int seats, qp, frames;
    float maxSeconds;
    bool hud, menus;
    bool recording;
    HouseRules rules;

    RenderTexture target;
    Process encoder;
    Stream encoderIn;
    Thread writer;
    readonly BlockingCollection<byte[]> frameQueue = new BlockingCollection<byte[]>(90);
    readonly Queue<int> frameCopies = new Queue<int>();
    byte[] lastFrame;
    int repeated, failedReadbacks, inFlight;
    float recordStart;
    AudioListener tapped;
    StreamWriter events;
    Order lastLode;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-trailer") < 0)
            return;
        GameObject go = new GameObject(nameof(TrailerDirector));
        DontDestroyOnLoad(go);
        go.AddComponent<TrailerDirector>();
    }

    static string Arg(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, "-" + name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : fallback;
    }

    static int Int(string name, int fallback) => int.Parse(Arg(name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

    IEnumerator Start()
    {
        folder = Arg("out", ".");
        take = Arg("name", "take");
        ffmpeg = Arg("ffmpeg", "ffmpeg");
        seats = Int("seats", 1);
        qp = Int("qp", 16);
        maxSeconds = Int("seconds", 420);
        hud = Int("hud", 1) == 1;
        menus = Int("menus", 0) == 1;
        rules = new HouseRules((MatchLength)Enum.Parse(typeof(MatchLength), Arg("length", "Normal")), true, Int("golden", 1) == 1, true,
            (GameMode)Enum.Parse(typeof(GameMode), Arg("mode", "Standard")), Int("waves", 3), Int("speed", 100), Int("lowgravity", 0) == 1, Int("round", 3));
        Directory.CreateDirectory(folder);
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        Debug.Log("TRAILER start " + take);

        GameState wanted = GameState.Menu;
        while (State() != wanted)
            yield return null;

        // The display's own settings file doesn't decide the footage
        int width = Int("width", 2560), height = Int("height", 1440);
        Screen.SetResolution(width, height, FullScreenMode.Windowed);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 144;
        yield return null;
        yield return null;
        GraphicsQuality.Apply(GraphicsQuality.HIGH);

        if (menus)
            yield return Menus();
        else if (seats == 1)
            yield return HostAlone();
        else
            yield return JoinLocal();

        // Into the round; everything from Begin is recorded unless the menus already are
        float deadline = Time.realtimeSinceStartup + 240f;
        while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
        {
            PressWhileLoading();
            yield return null;
        }
        if (!recording)
            BeginRecording();

        float started = Time.time;
        float resultsAt = -1f;
        int every = 0;
        while (Time.time - started < maxSeconds)
        {
            if (++every % 30 == 1)
            {
                PutBotsAtTheWheels();
                if (!hud)
                    HideHud();
            }
            PressWhileLoading();
            WatchLode();
            if (State() == GameState.Results && resultsAt < 0f)
                resultsAt = Time.time;
            if (resultsAt >= 0f && Time.time - resultsAt > RESULTS_HOLD)
                break;
            yield return null;
        }

        Debug.Log("TRAILER finished " + take + " state " + State() + " seconds " + (Time.time - started));
        yield return FinishRecording();
        Application.Quit();
    }

    static GameState State() => GameManager.Instance != null ? GameManager.Instance.MainState : GameState.Default;

    IEnumerator WaitPlayers(int count)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (PlayerInstantiate.Instance.PlayerCount < count && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    IEnumerator HostAlone()
    {
        TestPlayers.Add();
        yield return WaitPlayers(1);
        HouseRules.Current = rules;
        OnlineSession host = OnlineSession.Create(Application.version);
        host.FillsWithBots = true;
        OnlineGame.Attach(host);
        if (!host.HostDirect("127.0.0.1", 7899))
            Debug.LogError("TRAILER couldn't host");
        GameManager.Instance.SetGameState(GameState.PlayerSelect);
        float deadline = Time.realtimeSinceStartup + 15f;
        while (host.Match == null && Time.realtimeSinceStartup < deadline)
            yield return null;
        SceneFlow.Current.LoadGameScene();
    }

    IEnumerator JoinLocal()
    {
        TestPlayers.Add();
        yield return WaitPlayers(1);
        GameManager.Instance.SetGameState(GameState.PlayerSelect);
        yield return null;
        for (int joined = 2; joined <= seats; joined++)
        {
            TestPlayers.Add();
            yield return WaitPlayers(joined);
        }
        HouseRules.Current = rules;
        TutorialManager.SkipNextTutorial = true;
        SceneFlow.Current.Rematch();
    }

    // The way a player comes in: the title screen, Local, player select, everyone joining, MATCH RULES, ready, the loading
    // screen. Recorded from the title screen
    IEnumerator Menus()
    {
        TestPlayers.Add();
        yield return WaitPlayers(1);
        BeginRecording();
        yield return Hold(3f);
        Gamepad first = TestPlayers.Pads[0];
        float deadline = Time.realtimeSinceStartup + 40f;
        while (State() != GameState.PlayerSelect && Time.realtimeSinceStartup < deadline)
        {
            Press(first, MainMenu.Instance.Selected == TitleEntry.Local ? GamepadButton.South : GamepadButton.DpadDown);
            yield return Hold(PRESS_EVERY);
        }
        yield return Hold(2f);
        for (int joined = 2; joined <= seats; joined++)
        {
            TestPlayers.Add();
            yield return WaitPlayers(joined);
            yield return Hold(1.2f);
        }

        // MATCH RULES, nothing saved: round the modes, then the rows a mode shows
        RulesPanel panel = RulesPanel.Instance;
        panel.Kept = null;
        HouseRules.Current = HouseRules.Normal;
        panel.Open(HouseRules.Normal);
        yield return Hold(1.5f);
        foreach (GameMode mode in GameModes.Playable)
        {
            if (mode != GameMode.Standard)
                panel.Step(true);
            yield return Hold(1.4f);
        }
        for (int i = GameModes.Playable.Length - 1; i > 0; i--)
        {
            panel.Step(false);
            yield return Hold(0.25f);
        }
        for (int row = 0; row < 9; row++)
        {
            panel.Move(true);
            yield return Hold(0.45f);
        }
        panel.Close();
        HouseRules.Current = rules;
        yield return Hold(1f);

        TutorialManager.SkipNextTutorial = true;
        deadline = Time.realtimeSinceStartup + 40f;
        while (State() == GameState.PlayerSelect && Time.realtimeSinceStartup < deadline)
        {
            TutorialManager.SkipNextTutorial = true;
            foreach (Gamepad pad in TestPlayers.Pads)
                Press(pad, GamepadButton.South);
            yield return Hold(PRESS_EVERY);
        }
    }

    static IEnumerator Hold(float seconds)
    {
        for (float until = Time.time + seconds; Time.time < until;)
            yield return null;
    }

    float nextPress;

    // A loading screen waits for everyone's A
    void PressWhileLoading()
    {
        if (State() != GameState.Loading || Time.time < nextPress)
            return;
        nextPress = Time.time + PRESS_EVERY;
        foreach (Gamepad pad in TestPlayers.Pads)
            Press(pad, GamepadButton.South);
    }

    static void Press(Gamepad pad, GamepadButton button)
    {
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
        InputSystem.QueueStateEvent(pad, new GamepadState());
    }

    void PutBotsAtTheWheels()
    {
        if (PlayerInstantiate.Instance == null)
            return;
        foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
        {
            if (slot.Player == null || slot.IsBot)
                continue;
            BallDriving scooter = slot.Player.GetComponentInChildren<BallDriving>(true);
            if (scooter == null || scooter.DriveInput is BotDriver)
                continue;
            BotDriver bot = scooter.gameObject.AddComponent<BotDriver>();
            bot.OnlyInRound = true;
            bot.Kind = (BotKind)(slot.Index % 3);
            bot.Drive(scooter);
            Debug.Log("TRAILER bot in seat " + slot.Index);
        }
    }

    static void HideHud()
    {
        // The live scoreboard turns its own canvas back on every frame
        foreach (Scoreboard board in FindObjectsOfType<Scoreboard>())
            board.enabled = false;
        foreach (Canvas canvas in FindObjectsOfType<Canvas>())
            canvas.enabled = false;
    }

    void BeginRecording()
    {
        if (recording)
            return;
        recording = true;

        target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        ProcessStartInfo info = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = "-y -hide_banner -loglevel error -f rawvideo -pix_fmt rgba -s " + Screen.width + "x" + Screen.height + " -r " + FPS
                + " -i - " + Arg("flip", "") + " -c:v h264_nvenc -preset p5 -tune hq -rc constqp -qp " + qp
                + " -g " + (FPS / 2) + " -bf 0 -pix_fmt yuv420p -movflags +faststart \"" + Path.Combine(folder, take + ".video.mp4") + "\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            CreateNoWindow = true,
        };
        encoder = Process.Start(info);
        encoderIn = encoder.StandardInput.BaseStream;
        writer = new Thread(WriteFrames) { IsBackground = true };
        writer.Start();

        events = new StreamWriter(Path.Combine(folder, take + ".events.csv"));
        events.WriteLine("seconds,event,seat,other,detail");
        Listen(true);

        File.WriteAllText(Path.Combine(folder, take + ".audio.txt"), AudioSettings.outputSampleRate + " " + ChannelCount());
        AudioTap.Begin(Path.Combine(folder, take + ".f32le"), ChannelCount());
        recordStart = Time.realtimeSinceStartup;
        StartCoroutine(CaptureFrames());
    }

    // The sound comes from whichever listener is on: cutscenes and scenes change it
    void TapTheListener()
    {
        AudioListener listener = FindObjectOfType<AudioListener>();
        if (listener == null || listener == tapped)
            return;
        tapped = listener;
        AudioTap tap = listener.GetComponent<AudioTap>();
        AudioTap.Current = tap != null ? tap : listener.gameObject.AddComponent<AudioTap>();
    }

    static int ChannelCount()
    {
        switch (AudioSettings.speakerMode)
        {
            case AudioSpeakerMode.Mono: return 1;
            case AudioSpeakerMode.Quad: return 4;
            case AudioSpeakerMode.Surround: return 5;
            case AudioSpeakerMode.Mode5point1: return 6;
            case AudioSpeakerMode.Mode7point1: return 8;
            default: return 2;
        }
    }

    IEnumerator CaptureFrames()
    {
        WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
        int tapCheck = 0;
        while (recording)
        {
            yield return endOfFrame;
            if (!recording)
                break;
            if (++tapCheck % 15 == 1)
                TapTheListener();
            if (Screen.width != target.width || Screen.height != target.height)
                continue; // never a frame of another size in the video

            int due = Mathf.FloorToInt((Time.realtimeSinceStartup - recordStart) * FPS) + 1 - frames;
            if (due <= 0)
                continue; // drawn faster than the video's 60: this one isn't needed
            repeated += due - 1;
            frames += due;
            ScreenCapture.CaptureScreenshotIntoRenderTexture(target);
            frameCopies.Enqueue(due);
            inFlight++;
            AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, OnFrame);
            if (inFlight > 8)
                AsyncGPUReadback.WaitAllRequests();
        }
    }

    // Requests finish in order. A failed one repeats the last frame, so the picture stays in time with the sound
    void OnFrame(AsyncGPUReadbackRequest request)
    {
        inFlight--;
        int copies = frameCopies.Dequeue();
        if (request.hasError)
            failedReadbacks++;
        else
            lastFrame = request.GetData<byte>().ToArray();
        if (lastFrame == null)
            return;
        for (int i = 0; i < copies; i++)
            frameQueue.Add(lastFrame);
    }

    void WriteFrames()
    {
        foreach (byte[] frame in frameQueue.GetConsumingEnumerable())
            encoderIn.Write(frame, 0, frame.Length);
    }

    IEnumerator FinishRecording()
    {
        Listen(false);
        recording = false;
        yield return null;
        AsyncGPUReadback.WaitAllRequests();
        frameQueue.CompleteAdding();
        writer.Join();
        encoderIn.Close();
        encoder.WaitForExit();
        AudioTap.End();
        events.Close();
        Debug.Log("TRAILER wrote " + frames + " frames, " + repeated + " repeated, " + failedReadbacks + " failed; sound "
            + AudioTap.SecondsWritten.ToString("0.0") + " s; encoder exit " + encoder.ExitCode);
    }

    void Listen(bool on)
    {
        if (on)
        {
            FeatSync.Struck += OnStruck;
            FeatSync.Bumped += OnBumped;
            FeatSync.Delivered += OnDelivered;
            FeatSync.Sank += OnSank;
            ModeMatch.Happened += OnMode;
            GameManager.Instance.StateApplied += OnState;
        }
        else
        {
            FeatSync.Struck -= OnStruck;
            FeatSync.Bumped -= OnBumped;
            FeatSync.Delivered -= OnDelivered;
            FeatSync.Sank -= OnSank;
            ModeMatch.Happened -= OnMode;
            if (GameManager.Instance != null)
                GameManager.Instance.StateApplied -= OnState;
        }
    }

    void Log(string name, int seat, int other, string detail)
    {
        events.WriteLine((Time.realtimeSinceStartup - recordStart).ToString("0.00", CultureInfo.InvariantCulture) + "," + name + "," + seat + "," + other + "," + detail);
        events.Flush();
    }

    void OnStruck(int attacker, int victim, bool steal, bool golden) => Log(!steal ? "clash" : golden ? "golden_steal" : "steal", attacker, victim, "");
    void OnBumped(int bumper, int victim, float strength) { if (strength > 0.5f) Log("bump", bumper, victim, strength.ToString("0.00", CultureInfo.InvariantCulture)); }
    void OnDelivered(int seat, Constants.OrderValue value) => Log("delivery", seat, -1, value.ToString());
    void OnSank(int seat) => Log("fell_in_water", seat, -1, "");
    void OnMode(ModeMoment moment) => Log(moment.Kind.ToString(), moment.Seat, moment.Other, moment.Strength.ToString("0.00", CultureInfo.InvariantCulture));
    void OnState(GameState state) => Log("state", -1, -1, state.ToString());

    void WatchLode()
    {
        Order lode = Order.CurrentLode;
        if (lode != null && lode != lastLode)
            Log("mother_lode", -1, -1, "");
        lastLode = lode;
    }
}

/// <summary>Sits on the audio listener: copies the final mix to a file, padded to stay in time, and silences the speakers</summary>
public class AudioTap : MonoBehaviour
{
    public static AudioTap Current;
    public static float SecondsWritten => rate > 0 ? written / (float)(rate * channelCount) : 0f;

    static FileStream file;
    static Thread thread;
    static BlockingCollection<float[]> blocks;
    static long written;
    static int rate, channelCount;
    static double dspStart;

    public static void Begin(string path, int channels)
    {
        file = new FileStream(path, FileMode.Create);
        blocks = new BlockingCollection<float[]>();
        rate = AudioSettings.outputSampleRate;
        channelCount = channels;
        written = 0;
        dspStart = AudioSettings.dspTime;
        thread = new Thread(Write) { IsBackground = true };
        thread.Start();
    }

    public static void End()
    {
        Current = null;
        blocks.CompleteAdding();
        thread.Join();
        file.Close();
    }

    static void Write()
    {
        foreach (float[] block in blocks.GetConsumingEnumerable())
        {
            byte[] bytes = new byte[block.Length * sizeof(float)];
            Buffer.BlockCopy(block, 0, bytes, 0, bytes.Length);
            file.Write(bytes, 0, bytes.Length);
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (Current == this && blocks != null && !blocks.IsAddingCompleted && channels == channelCount)
        {
            // A gap while the listener changed becomes silence, so what follows stays in time
            long expected = (long)((AudioSettings.dspTime - dspStart) * rate) * channels;
            long behind = expected - written;
            if (behind > rate * channels / 10)
            {
                behind -= behind % channels;
                blocks.Add(new float[behind]);
                written += behind;
            }
            blocks.Add((float[])data.Clone());
            written += data.Length;
        }
        Array.Clear(data, 0, data.Length);
    }
}
