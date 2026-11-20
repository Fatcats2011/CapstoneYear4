using System.Collections;
using DG.Tweening;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Not a check: films every ghost emote (GhostMoves.All, in order) as numbered PNG frames in Logs/ghost-video/frames (run by name).
    /// In the parcel moves the ghost carries a medium package where the game floats it, beside the scooter at the first order slot (as GhostEmoteCapture), so they show it. Time.timeScale is 0
    /// while it films: the Animator, the emote and the package's tweens move only by the hand-made 1/FPS steps.
    /// Each move: 0.4 s rest, the move, 0.3 s rest, at 60 fps (1280x720). Each frame is two views side by side (left behindzoom,
    /// right front3q, wide enough for the whole scooter).
    /// frames.csv lists the frame range of every move; a script then labels the frames and encodes the video
    /// </summary>
    public class GhostEmoteVideo
    {
        const string AVATAR = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";
        const int VW = 640, VH = 720, FPS = 60;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (Application.isPlaying)
                yield return new ExitPlayMode();
            EditorSceneManager.playModeStartScene = null;
        }

        [UnityTest, Explicit]
        public IEnumerator GhostEmoteVideo_FilmsAllMoves()
        {
            // Nothing else here: Play Mode reloads the scripts, and only a bare test method survives that
            EditorSceneManager.playModeStartScene = null;
            yield return new EnterPlayMode();
            // Only Film's own steps move anything: on each yield the Animator would otherwise add the real frame time
            // (large, as rendering and PNG encoding are slow), so the hand clips ran fast and popped every 30 frames
            Time.timeScale = 0f;
            yield return Film();
        }

        static IEnumerator Film()
        {
            LogAssert.ignoreFailingMessages = true;
            string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "ghost-video");
            string folder = Path.Combine(root, "frames");
            if (Directory.Exists(root))
                Directory.Delete(root, true);
            Directory.CreateDirectory(folder);

            GameObject lightObject = new GameObject("Capture Sun");
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f, .55f, .6f);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Capture Ground";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            ground.GetComponent<Renderer>().sharedMaterial = new Material(lit) { color = new Color(.5f, .5f, .5f) };

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR);
            TitleActor actor = TitleActor.Create(prefab, null, "Capture Ghost");
            PlayerHatInformationSO hat = AssetDatabase.FindAssets("t:PlayerHatInformationSO")
                .Select(g => AssetDatabase.LoadAssetAtPath<PlayerHatInformationSO>(AssetDatabase.GUIDToAssetPath(g)))
                .First(h => h.displayHat);
            PlayerColorInformationSO colour = AssetDatabase.FindAssets("t:PlayerColorInformationSO")
                .Select(g => AssetDatabase.LoadAssetAtPath<PlayerColorInformationSO>(AssetDatabase.GUIDToAssetPath(g))).First();
            CompanyInformation company = AssetDatabase.FindAssets("t:CompanyInformation")
                .Select(g => AssetDatabase.LoadAssetAtPath<CompanyInformation>(AssetDatabase.GUIDToAssetPath(g))).First();
            actor.Wear(hat, colour, company);

            Animator animator = actor.Model.Find("Scooter/" + ScooterLook.MODEL + "/Player Model").GetComponent<Animator>();
            GhostEmotes emotes = GhostEmotes.On(animator, new System.Random(1));
            // A medium package at the game's first order slot (beside the scooter, the ghost's left), as GhostEmoteCapture's,
            // so the parcel moves hold it; its float and bob tweens are stepped by hand with the rest. Only the parcel moves
            // carry it (as in a match: the rest mostly play with nothing carried)
            Transform package = GhostEmoteCapture.Package(actor, true);
            GhostEmoteCapture.Carried carried = new GhostEmoteCapture.Carried { mesh = package };
            // Stepped by hand at exactly 1/FPS: its own LateUpdate would add the real frame time on each yield
            emotes.enabled = false;
            // A skinned mesh is skinned once a frame: several renders in one frame would all show the first pose
            foreach (SkinnedMeshRenderer skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.forceMatrixRecalculationPerRender = true;

            GameObject cameraObject = new GameObject("Capture Camera");
            cameraObject.tag = "MainCamera";
            Camera cam = cameraObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.12f, .16f, .24f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            RenderTexture target = new RenderTexture(VW, VH, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = target;
            Texture2D half = new Texture2D(VW, VH, TextureFormat.RGBA32, false);
            Texture2D full = new Texture2D(VW * 2, VH, TextureFormat.RGB24, false);

            // Left: the in-game camera spot (Player - Cinemachine.prefab) magnified. Right: a closer front three-quarter view
            Vector3 behind = new Vector3(0f, 6.3f, -9.7f), behindAim = new Vector3(0f, 1.6f, 0f);
            Vector3 frontAim = new Vector3(0f, 1.3f, 0f);
            Vector3 front = new Vector3(-0.5f, 0.3f, 0.8f).normalized * 6f + frontAim;

            yield return null;
            yield return null;
            yield return null;

            Transform[] all = animator.GetComponentsInChildren<Transform>(true);
            Vector3[] positions = all.Select(t => t.localPosition).ToArray(), scales = all.Select(t => t.localScale).ToArray();
            Quaternion[] rotations = all.Select(t => t.localRotation).ToArray();
            float dt = 1f / FPS;
            int frameNumber = 0, index = 0, count = GhostMoves.All.Count();
            System.Text.StringBuilder csv = new System.Text.StringBuilder("frame_start,frame_end,index,action,name\n");
            foreach (GhostMove move in GhostMoves.All.ToList())
            {
                index++;
                emotes.Stop();
                package.parent.gameObject.SetActive(move.HoldsParcel);
                emotes.Carry = move.HoldsParcel ? carried : null;
                for (int i = 0; i < all.Length; i++)
                {
                    all[i].localPosition = positions[i];
                    all[i].localRotation = rotations[i];
                    all[i].localScale = scales[i];
                }
                animator.Update(0f);
                int start = frameNumber + 1;
                int rest = Mathf.RoundToInt(.4f * FPS), moveFrames = Mathf.CeilToInt(move.Length * FPS), tail = Mathf.RoundToInt(.3f * FPS);
                for (int f = 0; f < rest + moveFrames + tail; f++)
                {
                    if (f == rest)
                        emotes.Play(move, 1f);
                    DOTween.ManualUpdate(dt, dt);
                    animator.Update(dt);
                    emotes.Step(dt);
                    cam.fieldOfView = 18f;
                    Shoot(cam, actor, behind, behindAim, target, half, full, 0);
                    cam.fieldOfView = 36f;
                    Shoot(cam, actor, front, frontAim, target, half, full, 1);
                    frameNumber++;
                    File.WriteAllBytes(Path.Combine(folder, "f" + frameNumber.ToString("00000") + ".png"), full.EncodeToPNG());
                    if (f % 30 == 29)
                        yield return null;
                }
                csv.Append(start + "," + frameNumber + "," + index + "," + move.Action + "," + move.Name.Replace(',', ';') + "\n");
                emotes.Stop();
                yield return null;
            }
            File.WriteAllText(Path.Combine(root, "frames.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(root, "info.txt"), "fps " + FPS + ", moves " + count + ", frames " + frameNumber);

            Object.Destroy(half);
            Object.Destroy(full);
            Object.Destroy(target);
            yield return null;
        }

        // Renders the view into one half of the frame (the animator and the emote's offsets are already applied)
        static void Shoot(Camera cam, TitleActor actor, Vector3 at, Vector3 aim, RenderTexture target, Texture2D half, Texture2D full, int slot)
        {
            cam.transform.position = actor.transform.TransformPoint(at);
            cam.transform.rotation = Quaternion.LookRotation(actor.transform.TransformPoint(aim) - cam.transform.position, Vector3.up);
            cam.Render();
            RenderTexture before = RenderTexture.active;
            RenderTexture.active = target;
            half.ReadPixels(new Rect(0, 0, VW, VH), 0, 0);
            RenderTexture.active = before;
            full.SetPixels(slot * VW, 0, VW, VH, half.GetPixels());
        }
    }
}
