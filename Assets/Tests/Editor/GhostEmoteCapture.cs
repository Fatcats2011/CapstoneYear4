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
    /// Not a check: films every ghost emote (GhostMoves.All) to Logs/ghost-emotes for a look (run by name). One sheet per
    /// move and camera: the ghost at rest, then at 10, 30, 50, 70 and 90 percent of the move. Three cameras: "behind"
    /// (where the in-game player camera sits), "behindzoom" (the same spot and aim at FOV 18) and "front3q" (front-left three-quarter)
    /// A move with a hand clip plays it on the Animator's Emotes layer: the capture runs the Animator (animator.Update) before
    /// each Step, so the layer's weight from the last Step is what that Update evaluates.
    /// The ghost carries a medium order's package on its rack (Order.prefab's mesh, size and turn, floating and bobbing),
    /// so the parcel moves hold it; the crown moves show the crown. props.txt logs where the props and the hands are
    /// </summary>
    public class GhostEmoteCapture
    {
        const string AVATAR = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";
        const string ORDER = "Assets/Prefabs/Packages/Order.prefab";

        sealed class Carried : IGhostCarry
        {
            public Transform mesh;
            public Transform Parcel() { return mesh; }
            public bool Carries(Transform held) { return held != null && held == mesh; }
        }
        const int W = 400, H = 300;
        static readonly float[] SHARES = { .1f, .3f, .5f, .7f, .9f };

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (Application.isPlaying)
                yield return new ExitPlayMode();
            EditorSceneManager.playModeStartScene = null;
        }

        [UnityTest, Explicit]
        public IEnumerator GhostEmoteCapture_FilmsEveryMove()
        {
            // Nothing else here: Play Mode reloads the scripts, and only a bare test method survives that
            EditorSceneManager.playModeStartScene = null;
            yield return new EnterPlayMode();
            yield return Film();
        }

        static IEnumerator Film()
        {
            LogAssert.ignoreFailingMessages = true;
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "ghost-emotes");
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
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
            Material grey = new Material(lit) { color = new Color(.5f, .5f, .5f) };
            ground.GetComponent<Renderer>().sharedMaterial = grey;

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
            File.WriteAllText(Path.Combine(folder, "info.txt"), "hat " + hat.name + ", colour " + colour.name + ", company " + company.name);

            Animator animator = actor.Model.Find("Scooter/" + ScooterLook.MODEL + "/Player Model").GetComponent<Animator>();
            GhostEmotes emotes = GhostEmotes.On(animator, new System.Random(1));
            emotes.Carry = new Carried { mesh = Package(actor) };
            // A skinned mesh is skinned once a frame: several renders in one frame would all show the first pose
            foreach (SkinnedMeshRenderer skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.forceMatrixRecalculationPerRender = true;

            GameObject cameraObject = new GameObject("Capture Camera");
            cameraObject.tag = "MainCamera";
            Camera cam = cameraObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.12f, .16f, .24f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            RenderTexture target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = target;
            Texture2D frame = new Texture2D(W, H, TextureFormat.RGBA32, false);

            // Where the in-game camera sits (Player - Cinemachine.prefab: follow offset 0, 6.3, -9.7, FOV 60), and a
            // front-left three-quarter view at about the same distance
            Vector3 behind = new Vector3(0f, 6.3f, -9.7f);
            Vector3 front = new Vector3(-6f, 4.5f, 8f);
            Vector3 aim = new Vector3(0f, 1.6f, 0f);

            yield return null;
            yield return null;
            yield return null;

            // Rest sheets: the driving pose
            animator.Update(0f);
            Texture2D one = new Texture2D(W, H, TextureFormat.RGBA32, false);
            Shoot(cam, actor, behind, aim, target, frame, one, 0);
            File.WriteAllBytes(Path.Combine(folder, "00_rest_behind.png"), one.EncodeToPNG());
            Shoot(cam, actor, front, aim, target, frame, one, 0);
            File.WriteAllBytes(Path.Combine(folder, "00_rest_front3q.png"), one.EncodeToPNG());
            Object.Destroy(one);
            cam.fieldOfView = 60f;

            // Every bone as it stands at rest: put back before each sheet, so a move that doesn't restore itself fully
            // (see leaks.txt) can't spoil the sheets after it
            Transform[] all = animator.GetComponentsInChildren<Transform>(true);
            Vector3[] positions = all.Select(t => t.localPosition).ToArray(), scales = all.Select(t => t.localScale).ToArray();
            Quaternion[] rotations = all.Select(t => t.localRotation).ToArray();
            int index = 0;
            foreach (GhostMove move in GhostMoves.All.ToList())
            {
                index++;
                string stem = index.ToString("00") + "_" + move.Action + "_" + move.Name.Replace(' ', '_');
                foreach (string view in new[] { "behind", "behindzoom", "front3q" })
                {
                    bool frontView = view == "front3q";
                    cam.fieldOfView = view == "behindzoom" ? 18f : 60f;
                    Vector3 at = frontView ? front : behind;
                    Texture2D strip = new Texture2D(W * 6, H, TextureFormat.RGBA32, false);
                    emotes.Stop();
                    int leaked = 0;
                    for (int i = 0; i < all.Length; i++)
                    {
                        if ((all[i].localPosition - positions[i]).sqrMagnitude > 1e-8f || (all[i].localScale - scales[i]).sqrMagnitude > 1e-8f)
                        {
                            leaked++;
                            if (leaked <= 3)
                                File.AppendAllText(Path.Combine(folder, "leaks.txt"), stem + " " + view + ": " + all[i].name
                                    + " pos " + all[i].localPosition.ToString("F3") + " vs " + positions[i].ToString("F3")
                                    + ", scale " + all[i].localScale.ToString("F3") + " vs " + scales[i].ToString("F3") + System.Environment.NewLine);
                        }
                        all[i].localPosition = positions[i];
                        all[i].localRotation = rotations[i];
                        all[i].localScale = scales[i];
                    }
                    animator.Update(0f);
                    File.AppendAllText(Path.Combine(folder, "head.txt"), stem + " f0 head " + emotes.Rig.Head.position.ToString("F2") + " torso " + emotes.Rig.Torso.position.ToString("F2") + System.Environment.NewLine);
                    Shoot(cam, actor, at, aim, target, frame, strip, 0);
                    emotes.Play(move, 1f);
                    float time = 0f;
                    for (int i = 0; i < SHARES.Length; i++)
                    {
                        float goal = SHARES[i] * move.Length;
                        while (time < goal - 1e-4f)
                        {
                            float dt = Mathf.Min(0.05f, goal - time);
                            animator.Update(dt);
                            emotes.Step(dt);
                            time += dt;
                        }
                        File.AppendAllText(Path.Combine(folder, "head.txt"), stem + " f" + (i + 1) + " head " + emotes.Rig.Head.position.ToString("F2") + " hat " + (emotes.Rig.Hat != null ? emotes.Rig.Hat.position.ToString("F2") : "-") + " torso " + emotes.Rig.Torso.position.ToString("F2") + " clip weight " + emotes.ClipWeight.ToString("F2") + (emotes.Rig.Hat != null ? " hat scale " + emotes.Rig.Hat.lossyScale.x.ToString("F3") + " hat off " + (emotes.Rig.Hat.GetComponent<Renderer>() != null && emotes.Rig.Hat.GetComponent<Renderer>().forceRenderingOff) + " hat active " + emotes.Rig.Hat.gameObject.activeInHierarchy : "") + System.Environment.NewLine);
                        if (move.Prop != GhostProp.None)
                            File.AppendAllText(Path.Combine(folder, "props.txt"), stem + " " + view + " f" + (i + 1)
                                + " hands " + ((emotes.Rig.HandLeft.position + emotes.Rig.HandRight.position) * 0.5f).ToString("F2")
                                + " right " + emotes.Rig.HandRight.position.ToString("F2") + " left " + emotes.Rig.HandLeft.position.ToString("F2")
                                + " parcel " + (emotes.Props.Parcel != null ? emotes.Props.Parcel.position.ToString("F2") + " w " + emotes.Props.ParcelWeight.ToString("F2") : "-")
                                + " crown " + (emotes.Props.CrownShown ? emotes.Props.Crown.transform.position.ToString("F2") + " wide " + emotes.Props.Crown.bounds.size.x.ToString("F2") : "-")
                                + System.Environment.NewLine);
                        Shoot(cam, actor, at, aim, target, frame, strip, i + 1);
                    }
                    File.WriteAllBytes(Path.Combine(folder, stem + "_" + view + ".png"), strip.EncodeToPNG());
                    Object.Destroy(strip);
                    emotes.Stop();
                    yield return null;
                }
            }

            Object.Destroy(frame);
            Object.Destroy(target);
            yield return null;
        }

        // A medium order's package (Order.prefab's mesh, material, size and turn), one down under its root as
        // Order.SetMeshPosition puts it, floating and bobbing as Order.Pickup sets it, on the rack (TitleActor.Carry: the
        // capture's scooter is the title's, whose OrderHandler slots aren't where its model is)
        static Transform Package(TitleActor actor)
        {
            Order prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ORDER).GetComponent<Order>();
            GameObject root = new GameObject("Capture Order");
            root.transform.localScale = prefab.transform.localScale;
            GameObject mesh = new GameObject("Order Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            mesh.GetComponent<MeshFilter>().sharedMesh = prefab.MeshOf(1);
            mesh.GetComponent<MeshRenderer>().sharedMaterial = prefab.MaterialOf(1);
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localRotation = prefab.orderMeshObject.transform.localRotation;
            mesh.transform.localScale = Vector3.one * prefab.meshScale[1];
            mesh.transform.localPosition = -Vector3.up;
            actor.Carry(root.transform);
            File.AppendAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "ghost-emotes", "info.txt"),
                System.Environment.NewLine + "package on the rack, world size " + mesh.GetComponent<Renderer>().bounds.size.ToString("F2"));
            mesh.transform.DOLocalRotate(new Vector3(0f, 0f, 90f), 2f, RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Incremental).SetRelative().SetEase(Ease.Linear);
            mesh.transform.DOLocalMove(new Vector3(0f, 0.1f, 0f), 1f).SetLoops(-1, LoopType.Yoyo).SetRelative().SetEase(Ease.Linear);
            return mesh.transform;
        }

        // Renders the view into a slot of the sheet (the animator and the emote's offsets are already applied)
        static void Shoot(Camera cam, TitleActor actor, Vector3 at, Vector3 aim, RenderTexture target, Texture2D frame, Texture2D sheet, int slot)
        {
            cam.transform.position = actor.transform.TransformPoint(at);
            cam.transform.rotation = Quaternion.LookRotation(actor.transform.TransformPoint(aim) - cam.transform.position, Vector3.up);
            cam.Render();
            RenderTexture before = RenderTexture.active;
            RenderTexture.active = target;
            frame.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            frame.Apply();
            RenderTexture.active = before;
            sheet.SetPixels(slot * W, 0, W, H, frame.GetPixels());
        }
    }
}
