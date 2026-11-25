#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class KollisionHelfer
{
    [MenuItem("Stadt/Mesh-Collider an Auswahl und Kinder")]
    public static void MeshColliderHinzufuegen()
    {
        int neu = 0;
        foreach (var go in Selection.gameObjects)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (mf.GetComponent<Collider>() != null) continue;
                var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                mc.sharedMesh = mf.sharedMesh;
                neu++;
            }
        }
        Debug.Log("Mesh-Collider hinzugefuegt: " + neu);
    }

    [MenuItem("Stadt/Mesh-Collider an Auswahl und Kinder", true)]
    static bool MeshColliderPruefen()
    {
        return Selection.gameObjects.Length > 0;
    }
}
#endif
