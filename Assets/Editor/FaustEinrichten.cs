#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class FaustEinrichten
{
    const string NetzPfad = "Assets/Player/Faust/Mesh_Faust.asset";

    [MenuItem("Spiel/9 Faust-Hand am Spieler einrichten")]
    public static void Einrichten()
    {
        var spieler = GameObject.FindGameObjectWithTag("Player");
        if (spieler == null)
        {
            Debug.LogWarning("Kein Objekt mit dem Tag Player in der Szene gefunden.");
            return;
        }
        var netz = AssetDatabase.LoadAssetAtPath<Mesh>(NetzPfad);
        if (netz == null)
        {
            Debug.LogWarning("Das Netz " + NetzPfad + " fehlt.");
            return;
        }
        var hf = spieler.GetComponent<HandFaust>();
        if (hf == null) hf = Undo.AddComponent<HandFaust>(spieler);
        Undo.RecordObject(hf, "Faust-Hand einrichten");
        hf.faustNetz = netz;
        EditorUtility.SetDirty(hf);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Spiel: Faust-Hand auf " + spieler.name + " eingerichtet. Das Schwert-Ziehen ballt jetzt die rechte Hand zur Faust.");
    }
}
#endif
