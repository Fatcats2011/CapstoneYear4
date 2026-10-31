// Temporary one-off: turns the leaf sprites in Assets/Terrain/GraveyardLeaves into
// flat detail meshes and scatters them at random over the dirt on the graveyard
// terrains. Triggered by Claude writing Temp/claude_leaves.txt
// ("<leaves per 100 m2> <seed>"); result in Temp/claude_leaves_result.txt. Delete when done.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class ClaudeLeafScatter
{
    const string Trigger = "Temp/claude_leaves.txt";
    const string Result = "Temp/claude_leaves_result.txt";
    const string Folder = "Assets/Terrain/GraveyardLeaves";
    const string DirtLayer = "NewLayer 5";
    const float PixelsPerMetre = 2048f; // the dirt layer tiles 4096 px over 2 m
    static float minScale = 0.7f, maxScale = 1.6f;
    static bool use3D;
    const string ModelFolder = "Assets/Models/GraveyardLeaves";

    // one prefab per cluster mesh in GraveyardLeaves.fbx, sharing a leaf-green URP Lit material
    static GameObject[] Make3DLeaves()
    {
        var matPath = $"{ModelFolder}/GraveyardLeaf.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.SetColor("_BaseColor", new Color32(22, 82, 22, 255));
        mat.SetFloat("_Smoothness", 0.15f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);

        return AssetDatabase.LoadAllAssetsAtPath($"{ModelFolder}/GraveyardLeaves.fbx").OfType<Mesh>().OrderBy(m => m.name).Select(mesh =>
        {
            var go = new GameObject(mesh.name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{ModelFolder}/{mesh.name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }).ToArray();
    }

    static ClaudeLeafScatter() => EditorApplication.update += () =>
    {
        // Temp/claude_look.txt: "<x> <y> <z> <yaw> <pitch> <size>" aims the Scene view
        if (File.Exists("Temp/claude_look.txt"))
        {
            var v = File.ReadAllText("Temp/claude_look.txt").Split(' ').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            File.Delete("Temp/claude_look.txt");
            var sv = SceneView.lastActiveSceneView;
            if (sv) { sv.LookAt(new Vector3(v[0], v[1], v[2]), Quaternion.Euler(v[4], v[3], 0), v[5], false, true); sv.Repaint(); }
        }
        // Temp/claude_grass.txt: point Grass.prefab (used by every Dense patch) at GrassClump.fbx
        if (File.Exists("Temp/claude_grass.txt"))
        {
            var which = File.ReadAllText("Temp/claude_grass.txt").Trim();
            File.Delete("Temp/claude_grass.txt");
            AssetDatabase.Refresh();
            var mesh = AssetDatabase.LoadAllAssetsAtPath(which == "old" ? "Assets/Grass/Grass.fbx" : "Assets/Grass/GrassClump.fbx").OfType<Mesh>().First();
            var root = PrefabUtility.LoadPrefabContents("Assets/Grass/Grass.prefab");
            var mf = root.GetComponentInChildren<MeshFilter>();
            var old = mf.sharedMesh ? mf.sharedMesh.name : "none";
            mf.sharedMesh = mesh;
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Grass/Grass.prefab");
            PrefabUtility.UnloadPrefabContents(root);
            File.WriteAllText("Temp/claude_grass_result.txt", $"Grass.prefab mesh {old} -> {mesh.name} ({mesh.triangles.Length / 3} tris, bounds {mesh.bounds.size})\nDONE\n");
        }
        // Temp/claude_find.txt: "<name>" frames the Scene view on the first object with that name and
        // reports its renderer's materials and mesh bounds
        if (File.Exists("Temp/claude_find.txt"))
        {
            var n = File.ReadAllText("Temp/claude_find.txt").Trim();
            File.Delete("Temp/claude_find.txt");
            var r = Object.FindObjectsOfType<MeshRenderer>().FirstOrDefault(x => x.name == n);
            var msg = "not found";
            if (r)
            {
                var b = r.bounds;
                var sv = SceneView.lastActiveSceneView;
                sv.LookAt(b.center, Quaternion.Euler(20, 210, 0), b.extents.magnitude * 1.4f, false, true); sv.Repaint();
                var mf = r.GetComponent<MeshFilter>();
                msg = $"{n}: bounds {b.center} size {b.size}, mesh {mf.sharedMesh.name} {mf.sharedMesh.bounds.size}, mats [{string.Join(", ", r.sharedMaterials.Select(m => m ? m.name : "null"))}]";
                var importMats = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(mf.sharedMesh)).OfType<GameObject>()
                    .SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).Where(x => x.GetComponent<MeshFilter>().sharedMesh == mf.sharedMesh)
                    .Select(x => string.Join(", ", x.sharedMaterials.Select(m => m ? m.name : "null"))).FirstOrDefault();
                msg += $"\nfbx slot names [{importMats}]";
                for (int i = 0; i < mf.sharedMesh.subMeshCount; i++)
                    msg += $"\nsubmesh {i}: {mf.sharedMesh.GetSubMesh(i).indexCount / 3} tris";
            }
            File.WriteAllText("Temp/claude_find_result.txt", msg + "\nDONE\n");
        }
        // Temp/claude_ghostmats.txt: fill all six material slots of the fountain ghost in Foutain.prefab
        if (File.Exists("Temp/claude_ghostmats.txt"))
        {
            File.Delete("Temp/claude_ghostmats.txt");
            Material M(string p) => AssetDatabase.LoadAssetAtPath<Material>(p);
            var gold = M("Assets/Materials/GhostGold.mat");
            var mats = new[] { M("Assets/Materials/Fountain.mat"), gold, gold,
                               M("Assets/Materials/Ghost Materials/GhostEyes.mat"),
                               M("Assets/Materials/Ghost Materials/Ghost Eyes Black.mat"),
                               M("Assets/Materials/Ghost Materials/GhostEyes.mat") };
            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Foutain.prefab");
            var r = root.GetComponentsInChildren<MeshRenderer>(true).First(x => x.GetComponent<MeshFilter>().sharedMesh.subMeshCount == 6);
            var before = string.Join(", ", r.sharedMaterials.Select(m => m ? m.name : "null"));
            r.sharedMaterials = mats;
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Foutain.prefab");
            PrefabUtility.UnloadPrefabContents(root);
            File.WriteAllText("Temp/claude_ghostmats_result.txt", $"{r.name}: [{before}] -> [{string.Join(", ", mats.Select(m => m.name))}]\nDONE\n");
        }
        // Temp/claude_near.txt: list mesh objects around the Scene view's pivot and what they come from
        if (File.Exists("Temp/claude_near.txt"))
        {
            File.Delete("Temp/claude_near.txt");
            var sv = SceneView.lastActiveSceneView;
            var c = sv.pivot;
            var rows = Object.FindObjectsOfType<MeshRenderer>()
                .Where(r => Vector3.Distance(r.bounds.center, c) < 6f)
                .Select(r =>
                {
                    var mf = r.GetComponent<MeshFilter>();
                    var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(r.gameObject);
                    var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject);
                    return $"{r.name} | mesh={(mf && mf.sharedMesh ? mf.sharedMesh.name : "-")} | mat={(r.sharedMaterial ? r.sharedMaterial.name : "-")} | prefab={(src ? AssetDatabase.GetAssetPath(src) : "-")} | in={(outer ? outer.name : "-")}";
                })
                .GroupBy(s => s).Select(g => $"{g.Count()}x {g.Key}");
            File.WriteAllText("Temp/claude_near_result.txt", $"pivot {c}\n" + string.Join("\n", rows) + "\nDONE\n");
        }
        // Temp/claude_details.txt: list every terrain's detail prototypes ("instance" first turns
        // instancing on for prototypes that use Grass.prefab)
        if (File.Exists("Temp/claude_details.txt"))
        {
            var cmd = File.ReadAllText("Temp/claude_details.txt").Trim();
            File.Delete("Temp/claude_details.txt");
            if (cmd == "hide" || cmd == "show")
                foreach (var t in Object.FindObjectsOfType<Terrain>(true).Where(t => t.name == "Backgroundterrain"))
                    t.drawTreesAndFoliage = cmd == "show";
            if (cmd == "instance")
                foreach (var t in Object.FindObjectsOfType<Terrain>(true))
                {
                    var protos = t.terrainData.detailPrototypes;
                    bool changed = false;
                    foreach (var p in protos)
                        if (p.prototype && AssetDatabase.GetAssetPath(p.prototype) == "Assets/Grass/Grass.prefab")
                        {
                            // instanced, so the grass shader sees each clump on its own; and a narrow,
                            // matching size range so clumps aren't squashed flat
                            p.useInstancing = true;
                            p.minWidth = p.minHeight = 0.9f; p.maxWidth = p.maxHeight = 1.3f;
                            changed = true;
                        }
                    if (changed) { t.terrainData.detailPrototypes = protos; EditorUtility.SetDirty(t.terrainData); }
                }
            AssetDatabase.SaveAssets();
            var lines = Object.FindObjectsOfType<Terrain>(true).SelectMany(t => t.terrainData.detailPrototypes.Select((p, i) =>
            {
                var d = t.terrainData;
                var layer = d.GetDetailLayer(0, 0, d.detailWidth, d.detailHeight, i);
                long count = 0; foreach (var v in layer) count += v;
                return $"{t.name} [{AssetDatabase.GetAssetPath(d)}] #{i}: {(p.prototype ? AssetDatabase.GetAssetPath(p.prototype) : p.prototypeTexture ? AssetDatabase.GetAssetPath(p.prototypeTexture) : "-")} mode={p.renderMode} mesh={p.usePrototypeMesh} inst={p.useInstancing} w={p.minWidth}-{p.maxWidth} h={p.minHeight}-{p.maxHeight} healthy={p.healthyColor} dry={p.dryColor} count={count}";
            }));
            File.WriteAllText("Temp/claude_details_result.txt", string.Join("\n", lines) + "\nDONE\n");
        }
        // Temp/claude_flatten.txt: "undo" steps back through only this script's grass undo groups;
        // "tilt" straightens tilted clumps in the scene (yaw kept, nothing moved)
        if (File.Exists("Temp/claude_flatten.txt"))
        {
            var mode = File.ReadAllText("Temp/claude_flatten.txt").Trim();
            File.Delete("Temp/claude_flatten.txt");
            string log = "";
            if (mode == "undo")
            {
                for (int i = 0; i < 10; i++)
                {
                    var g = Undo.GetCurrentGroupName();
                    if (g != "Flatten grass" && g != "Drop grass off furniture") { log += $"stopped at undo group '{g}'\n"; break; }
                    Undo.PerformUndo();
                    log += $"undid '{g}'\n";
                }
            }
            else if (mode == "tilt")
            {
                int n = 0;
                foreach (var t in Object.FindObjectsOfType<MeshFilter>().Where(mf => mf.sharedMesh && mf.sharedMesh.name == "GrassClump").Select(mf => mf.transform))
                {
                    if (Vector3.Angle(t.up, Vector3.up) <= 0.5f) continue;
                    Undo.RecordObject(t, "Straighten grass");
                    t.rotation = YawOnly(t.rotation);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    n++;
                }
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                log += $"straightened {n} clumps\n";
            }
            else log = Flatten();
            File.WriteAllText("Temp/claude_flatten_result.txt", log + "DONE\n");
        }
        // Temp/claude_flatcheck.txt: what is each grass clump standing on?
        if (File.Exists("Temp/claude_flatcheck.txt"))
        {
            var fix = File.ReadAllText("Temp/claude_flatcheck.txt").Trim() == "fix";
            File.Delete("Temp/claude_flatcheck.txt");
            Physics.SyncTransforms();
            if (fix)
            {
                // clumps that landed on furniture drop through to the first non-furniture surface below
                var furniture = new System.Text.RegularExpressions.Regex("bench|chair|table|pot", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var all = Object.FindObjectsOfType<MeshFilter>().Where(mf => mf.sharedMesh && mf.sharedMesh.name == "GrassClump").Select(mf => mf.transform).ToList();
                bool OnFurniture(Transform t)
                {
                    var h = Physics.RaycastAll(t.position + Vector3.up * 0.05f, Vector3.down, 0.3f, ~0, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance).FirstOrDefault();
                    return h.collider && furniture.IsMatch(h.collider.name);
                }
                var stuck = all.Where(OnFurniture).ToList();
                foreach (var t in stuck)
                {
                    var hits = Physics.RaycastAll(t.position + Vector3.up * 0.05f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).ToArray();
                    var ground = hits.FirstOrDefault(h => !furniture.IsMatch(h.collider.name));
                    float? y = ground.collider ? ground.point.y : (float?)null;
                    if (y == null)
                    {
                        // no collider under it: use the height of the nearby grass that isn't on furniture
                        var near = all.Where(o => !stuck.Contains(o) && Vector3.Distance(Flat(o.position), Flat(t.position)) < 3f).Select(o => o.position.y).OrderBy(v => v).ToList();
                        if (near.Count > 0) y = near[near.Count / 2];
                    }
                    if (y == null) continue;
                    Undo.RecordObject(t, "Drop grass off furniture");
                    var p = t.position; p.y = y.Value; t.position = p;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                }
                Physics.SyncTransforms();
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            }
            var rows = Object.FindObjectsOfType<MeshFilter>().Where(mf => mf.sharedMesh && mf.sharedMesh.name == "GrassClump").Select(mf =>
            {
                var t = mf.transform;
                var hit = Physics.RaycastAll(t.position + Vector3.up * 0.05f, Vector3.down, 0.3f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).FirstOrDefault();
                return hit.collider ? hit.collider.name : "(nothing within 0.3 m)";
            }).GroupBy(n => n).OrderByDescending(g => g.Count()).Select(g => $"{g.Count(),5}  {g.Key}");
            File.WriteAllText("Temp/claude_flatcheck_result.txt", string.Join("\n", rows) + "\nDONE\n");
        }
        if (!File.Exists(Trigger)) return;
        var args = File.ReadAllText(Trigger).Split(' ');
        File.Delete(Trigger);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (args.Length > 3) { minScale = float.Parse(args[2], inv); maxScale = float.Parse(args[3], inv); }
        use3D = args.Length > 4 && args[4].Trim() == "3d";
        try { File.WriteAllText(Result, Run(float.Parse(args[0], inv), int.Parse(args[1])) + "DONE\n"); }
        catch (System.Exception e) { File.WriteAllText(Result, "error: " + e + "\nDONE\n"); }
    };

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

    static Quaternion YawOnly(Quaternion q)
    {
        var f = Vector3.ProjectOnPlane(q * Vector3.forward, Vector3.up);
        if (f.sqrMagnitude < 1e-6f) f = Vector3.ProjectOnPlane(q * Vector3.up, Vector3.up);
        return f.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(f.normalized, Vector3.up);
    }

    static string Flatten()
    {
        var log = "";
        // 1) the prefab assets: Grass.prefab and the clumps inside each Dense patch prefab
        foreach (var path in new[] { "Assets/Grass/Grass.prefab", "Assets/Grass/Dense patch.prefab", "Assets/Grass/Dense patch(medium).prefab", "Assets/Grass/Dense patch(large).prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            int n = 0; float worst = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var t = mf.transform;
                worst = Mathf.Max(worst, Vector3.Angle(t.up, Vector3.up));
                t.rotation = YawOnly(t.rotation);
                var p = t.localPosition; if (t != root.transform) { p.y = 0; t.localPosition = p; }
                n++;
            }
            root.transform.rotation = YawOnly(root.transform.rotation);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            log += $"{Path.GetFileName(path)}: {n} clump(s), worst tilt was {worst:F1} deg\n";
        }

        // 2) the scene: anything still tilted (scene overrides, tilted patch roots), then drop onto the ground
        Physics.SyncTransforms();
        var clumps = Object.FindObjectsOfType<MeshFilter>().Where(mf => mf.sharedMesh && mf.sharedMesh.name == "GrassClump").Select(mf => mf.transform).ToList();
        int fixedTilt = 0, snapped = 0; float worstScene = 0;
        foreach (var t in clumps)
        {
            float tilt = Vector3.Angle(t.up, Vector3.up);
            worstScene = Mathf.Max(worstScene, tilt);
            Undo.RecordObject(t, "Flatten grass");
            if (tilt > 0.5f) { t.rotation = YawOnly(t.rotation); fixedTilt++; }
            // settle on whatever solid ground is under the clump's base
            var hits = Physics.RaycastAll(t.position + Vector3.up * 1.5f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => !h.collider.transform.IsChildOf(t)).OrderBy(h => h.distance).ToArray();
            if (hits.Length > 0 && Mathf.Abs(hits[0].point.y - t.position.y) > 0.02f)
            {
                var p = t.position; p.y = hits[0].point.y; t.position = p; snapped++;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        log += $"scene: {clumps.Count} clumps, worst tilt {worstScene:F1} deg, {fixedTilt} straightened, {snapped} moved onto the ground\n";
        return log;
    }

    static string Run(float per100m2, int seed)
    {
        var log = "";
        AssetDatabase.Refresh();
        var protos = use3D ? Make3DLeaves() : AssetDatabase.FindAssets("t:Texture2D", new[] { Folder })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileName(p).StartsWith("Leaf")).OrderBy(p => p)
            .Select(MakeLeaf).ToArray();
        log += $"leaf prefabs: {protos.Length}\n";

        var rnd = new System.Random(seed);
        foreach (var t in Object.FindObjectsOfType<Terrain>(true).Where(t => t.transform.parent && t.transform.parent.name == "NewGraveyardAssets"))
        {
            var d = t.terrainData;
            int dirt = System.Array.FindIndex(d.terrainLayers, l => l && l.name == DirtLayer);
            if (dirt < 0) continue;

            d.detailPrototypes = protos.Select(p => new DetailPrototype
            {
                prototype = p,
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = minScale, maxWidth = maxScale,
                minHeight = minScale, maxHeight = maxScale,
                noiseSpread = 0.3f,
                alignToGround = 1f,
                positionJitter = 1f,
            }).ToArray();
            d.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            int res = d.alphamapResolution;
            d.SetDetailResolution(res, 16);

            var alpha = d.GetAlphamaps(0, 0, d.alphamapWidth, d.alphamapHeight);
            float cellArea = d.size.x / res * (d.size.z / res);
            float chance = per100m2 / 100f * cellArea;
            var layers = protos.Select(_ => new int[res, res]).ToArray();
            int placed = 0;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float w = alpha[y * d.alphamapHeight / res, x * d.alphamapWidth / res, dirt];
                    if (w < 0.6f) continue;
                    if (rnd.NextDouble() >= chance * w) continue;
                    layers[rnd.Next(layers.Length)][y, x] = 1;
                    placed++;
                }
            for (int i = 0; i < layers.Length; i++) d.SetDetailLayer(0, 0, i, layers[i]);
            EditorUtility.SetDirty(d);
            log += $"{t.name}: {placed} leaves on {d.size.x:F0}x{d.size.z:F0} m (detail res {res})\n";
        }
        AssetDatabase.SaveAssets();
        return log;
    }

    // one flat, alpha-clipped quad prefab per leaf sprite, sized to its size on the old texture
    static GameObject MakeLeaf(string texPath)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = 1024;
        imp.SaveAndReimport();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        var name = Path.GetFileNameWithoutExtension(texPath);

        var matPath = $"{Folder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.SetTexture("_BaseMap", tex);
        mat.SetFloat("_AlphaClip", 1); mat.SetFloat("_Cutoff", 0.5f); mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Smoothness", 0); mat.SetFloat("_Cull", 0);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);

        // leaf size from the importer's source size, not the (possibly capped) imported size
        imp.GetSourceTextureWidthAndHeight(out int pw, out int ph);
        float hw = pw / PixelsPerMetre / 2, hh = ph / PixelsPerMetre / 2, lift = 0.02f;
        var meshPath = $"{Folder}/{name}_Mesh.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (!mesh) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, meshPath); }
        mesh.Clear();
        mesh.vertices = new[] { new Vector3(-hw, lift, -hh), new Vector3(hw, lift, -hh), new Vector3(-hw, lift, hh), new Vector3(hw, lift, hh) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.normals = Enumerable.Repeat(Vector3.up, 4).ToArray();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);

        var prefabPath = $"{Folder}/{name}.prefab";
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }
}
