using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools → Dead on Arrival → Online → Save Network Prefab IDs: writes each online prefab's Netcode id into its file.
/// The editor works an id out when it loads a prefab but doesn't save it, and a prefab built by a script can keep a
/// stale one: a build would use that. OnlinePrefabsTests checks the saved ids. Edit Mode only. See docs/online.md
/// </summary>
public static class NetworkPrefabIds
{
    const string ITEM = "Tools/Dead on Arrival/Online/Save Network Prefab IDs";
    public const string FOLDER = "Assets/Prefabs/Online";

    [MenuItem(ITEM, false, 40)]
    public static void Save()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { FOLDER }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
            if (networkObject == null)
                continue;

            // Loading worked the id out; marking the prefab changed is what gets it saved
            EditorUtility.SetDirty(networkObject);
            AssetDatabase.SaveAssetIfDirty(prefab);
        }
    }

    [MenuItem(ITEM, true)]
    static bool CanSave()
    {
        return !Application.isPlaying;
    }
}
