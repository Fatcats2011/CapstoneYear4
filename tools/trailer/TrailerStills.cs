// Store screenshots: never in the game's own project. tools/trailer/build.py copies this into the private snapshot beside
// TrailerDirector.cs. See tools/trailer/README.md
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Saves PNG stills of a bot-played take (TrailerDirector, -stills 1) instead of a video. Two kinds:
/// view: the screen as the players see it, HUD and split screen included;
/// photo: a second, free camera in the same live match, no HUD, at -photowidth x -photoheight. It is placed around the
/// scooters (close-ups, pairs, wides) and only fires with a clear line to its subject.
/// Both fire on a timer, when scooters come close together, and in a short burst after every logged event
/// </summary>
[DefaultExecutionOrder(10000)]
public class TrailerStills : MonoBehaviour
{
    struct Angle
    {
        public string Name;
        public Vector3 Offset; // side, up, forward from the subject, in its heading frame
        public float LookUp, LookAhead, Fov;
        public Angle(string name, float side, float up, float forward, float lookUp, float lookAhead, float fov)
        {
            Name = name;
            Offset = new Vector3(side, up, forward);
            LookUp = lookUp;
            LookAhead = lookAhead;
            Fov = fov;
        }
    }

    static readonly Angle[] SOLO =
    {
        new Angle("front", 1.4f, 0.8f, 4.2f, 0.7f, 0f, 38f),
        new Angle("frontclose", 0.9f, 0.9f, 2.6f, 0.9f, 0f, 45f),
        new Angle("front34", 2.8f, 1.1f, 3.2f, 0.7f, 0f, 40f),
        new Angle("side", 4.2f, 0.9f, 0.8f, 0.7f, 0.5f, 38f),
        new Angle("rear", 1.8f, 1.5f, -4.2f, 0.8f, 3f, 50f),
        new Angle("frontlow", -1.0f, 0.3f, 3.0f, 0.8f, 0f, 50f),
        new Angle("high", 3f, 7f, -8f, 0.5f, 4f, 45f),
        new Angle("sideleft", -4.4f, 1.2f, 1.6f, 0.7f, 0.5f, 38f),
        new Angle("wide", 8f, 13f, -22f, 0f, 18f, 55f),
        new Angle("scenic", -9f, 4f, 15f, 1f, -6f, 55f),
        new Angle("frontfar", -2.5f, 1.2f, 8f, 0.8f, 0f, 30f),
        new Angle("skyline", 4f, 22f, -38f, 0f, 25f, 50f),
        new Angle("street", 0.8f, 2.2f, -9f, 1.5f, 14f, 60f),
    };

    struct Request
    {
        public float Due;
        public string Tag;
        public int Seat, Other, AngleAt;
    }

    struct Job
    {
        public byte[] Pixels;
        public int Width, Height;
        public bool Flip;
        public string Path;
    }

    string folder, take;
    int photoWidth, photoHeight, rendererIndex;
    bool flipView, flipPhoto;
    float viewEvery, photoEvery, started;
    int photoMask;

    Camera photoCam;
    UniversalAdditionalCameraData photoData;
    RenderTexture photoTarget, viewTarget;
    readonly List<Request> photos = new List<Request>();
    readonly List<float> views = new List<float>();
    readonly List<string> viewTags = new List<string>();
    string photoPath;
    float nextView, nextPhoto, nextNear, nearCooldown;
    int soloSeat, soloAngle, inFlight, saved, dropped;
    bool running;

    readonly BlockingCollection<Job> jobs = new BlockingCollection<Job>(10);
    readonly List<Thread> writers = new List<Thread>();
    readonly RaycastHit[] hits = new RaycastHit[32];

    // Which way each seat is travelling: the model's own forward isn't the way it drives
    readonly Vector3[] lastAt = new Vector3[4];
    readonly Vector3[] heading = { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };

    void Track(List<Transform> scooters)
    {
        for (int seat = 0; seat < 4; seat++)
        {
            if (scooters[seat] == null)
                continue;
            Vector3 moved = scooters[seat].position - lastAt[seat];
            moved.y = 0f;
            if (moved.magnitude > 0.25f)
            {
                if (moved.magnitude < 5f)
                    heading[seat] = moved.normalized;
                lastAt[seat] = scooters[seat].position;
            }
        }
    }

    public static TrailerStills Begin(GameObject on, string folder, string take, Func<string, string, string> arg)
    {
        TrailerStills stills = on.AddComponent<TrailerStills>();
        stills.folder = Path.Combine(folder, take);
        stills.take = take;
        CultureInfo inv = CultureInfo.InvariantCulture;
        stills.photoWidth = int.Parse(arg("photowidth", "3840"), inv);
        stills.photoHeight = int.Parse(arg("photoheight", "2160"), inv);
        stills.rendererIndex = int.Parse(arg("photorenderer", "0"), inv);
        stills.flipView = arg("flipview", "1") == "1";
        stills.flipPhoto = arg("flipphoto", "0") == "1";
        stills.viewEvery = float.Parse(arg("viewevery", "3"), inv);
        stills.photoEvery = float.Parse(arg("photoevery", "1.5"), inv);
        stills.photoMask = int.Parse(arg("photomask", "0"), inv);
        stills.StartUp();
        return stills;
    }

    void StartUp()
    {
        Directory.CreateDirectory(Path.Combine(folder, "view"));
        Directory.CreateDirectory(Path.Combine(folder, "photo"));
        AudioListener.volume = 0f;
        started = Time.realtimeSinceStartup;
        running = true;
        for (int i = 0; i < 3; i++)
        {
            Thread thread = new Thread(Write) { IsBackground = true };
            thread.Start();
            writers.Add(thread);
        }
        StartCoroutine(EndOfFrames());
    }

    /// <summary>A logged event: a burst of both kinds over the next second</summary>
    public void OnEvent(string name, int seat, int other)
    {
        if (!running || name == "state")
            return;
        float now = Time.time;
        foreach (float after in new[] { 0.05f, 0.35f, 0.8f })
        {
            views.Add(now + after);
            viewTags.Add(name);
        }
        if (seat < 0)
            return;
        int at = 0;
        foreach (float after in new[] { 0.02f, 0.12f, 0.25f, 0.45f, 0.7f, 1.0f })
            photos.Add(new Request { Due = now + after, Tag = name, Seat = seat, Other = other, AngleAt = at++ });
    }

    static bool InRound() => GameManager.Instance != null && GameManager.Instance.MainState == GameState.Begin;

    void LateUpdate()
    {
        if (!running)
            return;
        float now = Time.time;
        if (now >= nextView)
        {
            nextView = now + viewEvery;
            views.Add(now);
            viewTags.Add("tick");
        }
        if (!InRound())
        {
            photos.Clear();
            return;
        }

        List<Transform> scooters = Scooters();
        Track(scooters);
        if (now >= nextPhoto)
        {
            nextPhoto = now + photoEvery;
            soloSeat = (soloSeat + 1) % 4;
            soloAngle = (soloAngle + 1) % SOLO.Length;
            photos.Add(new Request { Due = now, Tag = "tick", Seat = soloSeat, Other = -1, AngleAt = soloAngle });
        }
        if (now >= nextNear)
        {
            nextNear = now + 0.2f;
            if (now >= nearCooldown && Nearest(scooters, out int a, out int b) < 9f)
            {
                nearCooldown = now + 1.2f;
                for (int i = 0; i < 3; i++)
                    photos.Add(new Request { Due = now + i * 0.1f, Tag = "near", Seat = a, Other = b, AngleAt = i == 0 ? 0 : UnityEngine.Random.Range(0, 9) });
                views.Add(now);
                viewTags.Add("near");
            }
        }

        // One photo a frame: the camera draws with the rest of this frame, and is read back at its end
        photoPath = null;
        if (inFlight > 5)
            return;
        for (int i = 0; i < photos.Count; i++)
        {
            Request request = photos[i];
            if (request.Due > now)
                continue;
            photos.RemoveAt(i);
            if (Place(request, scooters, out string angle))
            {
                photoPath = Path.Combine(folder, "photo", Stamp() + "_" + request.Tag + "_" + angle + "_s" + request.Seat + ".png");
                PhotoCamera().enabled = true;
            }
            break;
        }
    }

    string Stamp() => take + "_" + ((Time.realtimeSinceStartup - started) * 10f).ToString("00000", CultureInfo.InvariantCulture);

    // Seat order, null for an empty seat
    static List<Transform> Scooters()
    {
        var found = new List<Transform> { null, null, null, null };
        if (PlayerInstantiate.Instance == null)
            return found;
        foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
        {
            if (slot.Player == null || slot.Index < 0 || slot.Index > 3)
                continue;
            BallDriving scooter = slot.Player.GetComponentInChildren<BallDriving>(true);
            if (scooter != null && scooter.ScooterModel != null && scooter.ScooterModel.gameObject.activeInHierarchy)
                found[slot.Index] = scooter.ScooterModel;
        }
        return found;
    }

    static float Nearest(List<Transform> scooters, out int a, out int b)
    {
        float best = float.MaxValue;
        a = b = -1;
        for (int i = 0; i < scooters.Count; i++)
        {
            for (int j = i + 1; j < scooters.Count; j++)
            {
                if (scooters[i] == null || scooters[j] == null)
                    continue;
                float distance = Vector3.Distance(scooters[i].position, scooters[j].position);
                if (distance < best)
                {
                    best = distance;
                    a = i;
                    b = j;
                }
            }
        }
        return best;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    bool Place(Request request, List<Transform> scooters, out string angle)
    {
        angle = null;
        Transform subject = request.Seat >= 0 && request.Seat < 4 ? scooters[request.Seat] : null;
        if (subject == null)
            return false;
        Transform other = request.Other >= 0 && request.Other < 4 && request.Other != request.Seat ? scooters[request.Other] : null;

        if (other != null && Vector3.Distance(subject.position, other.position) < 25f)
        {
            Vector3 a = subject.position, b = other.position;
            Vector3 middle = (a + b) * 0.5f + Vector3.up * 0.7f;
            Vector3 axis = Flat(b - a);
            Vector3 across = Vector3.Cross(Vector3.up, axis);
            float gap = Vector3.Distance(a, b);
            float back = Mathf.Max(4f, gap * 0.9f + 3f);
            // Square on from either side, three-quarter from either end, then from above
            // In front of them first: the faces are the picture
            Vector3 ahead = Flat(heading[request.Seat] + heading[request.Other]);
            Vector3[] from =
            {
                (ahead * 0.85f + across * 0.5f) * back + Vector3.up * 0.4f,
                (ahead * 0.85f - across * 0.5f) * back + Vector3.up * 0.4f,
                ahead * (back * 1.2f) + Vector3.up * 1.6f,
                across * back + Vector3.up * 0.5f,
                -across * back + Vector3.up * 0.5f,
                (across * 0.75f + axis * 0.65f) * back + Vector3.up * 0.9f,
                (-across * 0.75f - axis * 0.65f) * back + Vector3.up * 0.9f,
                (across * 0.6f - axis * 0.6f) * back + Vector3.up * 4f,
                (-across * 0.5f + axis * 0.7f) * (back * 1.6f) + Vector3.up * 7f,
            };
            string[] names = { "pairfront", "pairfront2", "pairahead", "pairside", "pairside2", "pair34", "pair34b", "pairhigh", "pairwide" };
            for (int i = 0; i < from.Length; i++)
            {
                int at = (request.AngleAt + i) % from.Length;
                Vector3 position = Grounded(middle + from[at], 0.3f);
                if (Clear(position, a + Vector3.up * 0.7f, scooters) && Clear(position, b + Vector3.up * 0.7f, scooters))
                {
                    Aim(position, middle, at >= 7 ? 45f : 40f);
                    angle = names[at];
                    return true;
                }
            }
            return false;
        }

        Vector3 forward = heading[request.Seat];
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        for (int i = 0; i < SOLO.Length; i++)
        {
            Angle solo = SOLO[(request.AngleAt + i) % SOLO.Length];
            Vector3 position = Grounded(subject.position + right * solo.Offset.x + Vector3.up * solo.Offset.y + forward * solo.Offset.z, 0.25f);
            Vector3 body = subject.position + Vector3.up * 0.7f;
            if (!Clear(position, body, scooters))
                continue;
            Aim(position, subject.position + Vector3.up * solo.LookUp + forward * solo.LookAhead, solo.Fov);
            angle = solo.Name;
            return true;
        }
        return false;
    }

    // Never under the ground
    static Vector3 Grounded(Vector3 position, float above)
    {
        if (Physics.Raycast(position + Vector3.up * 6f, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore))
            position.y = Mathf.Max(position.y, hit.point.y + above);
        return position;
    }

    // Nothing solid but scooters between the camera and the point, and the camera isn't inside anything
    bool Clear(Vector3 from, Vector3 to, List<Transform> scooters)
    {
        if (Physics.CheckSphere(from, 0.25f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        Vector3 along = to - from;
        int count = Physics.RaycastNonAlloc(from, along.normalized, hits, along.magnitude - 0.8f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = hits[i].collider.transform;
            bool player = false;
            foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
                player |= slot.Player != null && hit.IsChildOf(slot.Player.transform);
            if (!player)
                return false;
        }
        return true;
    }

    void Aim(Vector3 position, Vector3 at, float fov)
    {
        Camera cam = PhotoCamera();
        cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(at - position, Vector3.up));
        cam.fieldOfView = fov;
    }

    Camera PhotoCamera()
    {
        if (photoCam != null)
            return photoCam;
        GameObject holder = new GameObject("Stills Camera");
        DontDestroyOnLoad(holder);
        photoCam = holder.AddComponent<Camera>();
        photoCam.enabled = false;
        photoCam.clearFlags = CameraClearFlags.Skybox;
        photoCam.nearClipPlane = 0.1f;
        photoCam.farClipPlane = 2000f;
        if (photoMask != 0)
            photoCam.cullingMask = photoMask;
        photoTarget = new RenderTexture(photoWidth, photoHeight, 24, RenderTextureFormat.ARGB32);
        photoCam.targetTexture = photoTarget;
        photoData = holder.AddComponent<UniversalAdditionalCameraData>();
        photoData.renderPostProcessing = true;
        photoData.renderShadows = true;
        photoData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        photoData.antialiasingQuality = AntialiasingQuality.High;
        photoData.SetRenderer(rendererIndex);

        // Graded the way a player's view is
        foreach (PlayerCameraResizer view in FindObjectsOfType<PlayerCameraResizer>())
        {
            Camera reference = view.PlayerReferenceCamera;
            if (reference == null)
                continue;
            UniversalAdditionalCameraData data = reference.GetUniversalAdditionalCameraData();
            photoData.volumeLayerMask = data.volumeLayerMask;
            photoData.volumeTrigger = data.volumeTrigger;
            photoCam.farClipPlane = reference.farClipPlane;
            // What a player's camera draws (so nothing that only shows while phasing), less what only that one player is shown
            if (photoMask == 0)
                photoCam.cullingMask = reference.cullingMask & ~LayerMask.GetMask("Player 1 World", "Player 2 World", "Player 3 World",
                    "Player 4 World", "Player1Cam", "Player2Cam", "Player3Cam", "Player4Cam");
            Debug.Log("STILLS camera from " + reference.name + " mask " + reference.cullingMask + " volumes " + data.volumeLayerMask.value
                + " post " + data.renderPostProcessing + " aa " + data.antialiasing + " far " + reference.farClipPlane);
            break;
        }
        return photoCam;
    }

    IEnumerator EndOfFrames()
    {
        WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
        while (running)
        {
            yield return endOfFrame;
            if (!running)
                break;

            if (photoPath != null && photoCam != null)
            {
                photoCam.enabled = false;
                Read(photoTarget, photoPath, flipPhoto);
                photoPath = null;
            }

            float now = Time.time;
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i] > now)
                    continue;
                string tag = viewTags[i];
                views.RemoveAt(i);
                viewTags.RemoveAt(i);
                if (inFlight > 5)
                    break;
                if (viewTarget == null || viewTarget.width != Screen.width || viewTarget.height != Screen.height)
                {
                    if (viewTarget != null)
                        viewTarget.Release();
                    viewTarget = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
                }
                ScreenCapture.CaptureScreenshotIntoRenderTexture(viewTarget);
                Read(viewTarget, Path.Combine(folder, "view", Stamp() + "_" + tag + ".png"), flipView);
                break;
            }
        }
    }

    void Read(RenderTexture texture, string path, bool flip)
    {
        int width = texture.width, height = texture.height;
        inFlight++;
        AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBA32, request =>
        {
            inFlight--;
            if (request.hasError || jobs.IsAddingCompleted)
                return;
            if (!jobs.TryAdd(new Job { Pixels = request.GetData<byte>().ToArray(), Width = width, Height = height, Flip = flip, Path = path }))
                dropped++;
        });
    }

    void Write()
    {
        foreach (Job job in jobs.GetConsumingEnumerable())
        {
            // Opaque, and bottom row first, which is what the encoder wants
            int width = job.Width, height = job.Height;
            byte[] rgb = new byte[width * height * 3];
            for (int y = 0; y < height; y++)
            {
                int from = (job.Flip ? height - 1 - y : y) * width * 4;
                int to = y * width * 3;
                for (int x = 0; x < width; x++)
                {
                    rgb[to++] = job.Pixels[from];
                    rgb[to++] = job.Pixels[from + 1];
                    rgb[to++] = job.Pixels[from + 2];
                    from += 4;
                }
            }
            byte[] png = ImageConversion.EncodeArrayToPNG(rgb, GraphicsFormat.R8G8B8_SRGB, (uint)width, (uint)height, (uint)(width * 3));
            File.WriteAllBytes(job.Path, png);
            Interlocked.Increment(ref saved);
        }
    }

    public IEnumerator Finish()
    {
        running = false;
        yield return null;
        AsyncGPUReadback.WaitAllRequests();
        yield return null;
        jobs.CompleteAdding();
        foreach (Thread thread in writers)
            thread.Join();
        Debug.Log("STILLS saved " + saved + ", dropped " + dropped);
    }
}
