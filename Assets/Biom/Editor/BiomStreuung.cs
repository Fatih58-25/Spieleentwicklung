#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BiomStreuung
{
    const string Root = "Assets/Biom";

    [MenuItem("Biom/Scans verteilen (Gras, Felsen, Wurzeln, Farne)")]
    public static void Verteilen()
    {
        var scans = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Modelle/Biom_Scans.fbx");
        var punkte = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Modelle/Biom_Streuung.fbx");
        if (scans == null || punkte == null)
        {
            Debug.LogWarning("Biom_Scans.fbx oder Biom_Streuung.fbx fehlt in " + Root + "/Modelle");
            return;
        }
        var wurzel = new GameObject("Biom_Scans");
        Undo.RegisterCreatedObjectUndo(wurzel, "Biom Scans verteilen");
        var temp = (GameObject)PrefabUtility.InstantiatePrefab(punkte);
        int n = 0;
        int fehlt = 0;
        foreach (var t in temp.GetComponentsInChildren<Transform>(true))
        {
            string name = t.name;
            if (!name.StartsWith("E_")) continue;
            int i = name.LastIndexOf("__");
            if (i < 3) continue;
            string proto = name.Substring(2, i - 2);
            var vorlage = scans.transform.Find(proto);
            if (vorlage == null)
            {
                fehlt++;
                continue;
            }
            var go = Object.Instantiate(vorlage.gameObject, t.position, t.rotation);
            go.name = name.Substring(2);
            go.transform.localScale = t.lossyScale;
            go.transform.SetParent(wurzel.transform, true);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            n++;
        }
        Object.DestroyImmediate(temp);
        Selection.activeGameObject = wurzel;
        Debug.Log("Biom: " + n + " Scans verteilt" + (fehlt > 0 ? ", " + fehlt + " ohne Vorlage" : ""));
    }
}
#endif
