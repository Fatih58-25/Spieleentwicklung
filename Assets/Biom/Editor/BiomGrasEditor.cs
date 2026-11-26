#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BiomGrasEditor
{
    const string Root = "Assets/Biom";

    [MenuItem("Biom/Gras einrichten (einzelne Halme, Instancing)")]
    public static void Einrichten()
    {
        var protoFbx = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Modelle/Biom_Gras.fbx");
        if (protoFbx == null)
        {
            Debug.LogWarning("Biom_Gras.fbx fehlt in " + Root + "/Modelle");
            return;
        }
        var go = new GameObject("Biom_Gras");
        Undo.RegisterCreatedObjectUndo(go, "Biom Gras einrichten");
        var gras = go.AddComponent<BiomGras>();
        var liste = new List<BiomGras.Typ>();
        float[] anteile = { 0.55f, 0.33f, 0.12f };
        string[] namen = { "gras_kurz", "gras_mittel", "gras_hoch" };
        for (int i = 0; i < namen.Length; i++)
        {
            var vorlage = protoFbx.transform.Find(namen[i]);
            if (vorlage == null) continue;
            var mf = vorlage.GetComponent<MeshFilter>();
            var mr = vorlage.GetComponent<MeshRenderer>();
            if (mf == null || mr == null) continue;
            var typ = new BiomGras.Typ
            {
                name = namen[i],
                mesh = mf.sharedMesh,
                materialien = mr.sharedMaterials,
                anteil = anteile[i]
            };
            for (int k = 0; k < typ.materialien.Length; k++)
            {
                var quelle = typ.materialien[k];
                if (quelle == null) continue;
                var echt = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materialien/" + quelle.name + ".mat");
                if (echt != null) typ.materialien[k] = echt;
                if (typ.materialien[k] != null) typ.materialien[k].enableInstancing = true;
            }
            liste.Add(typ);
        }
        gras.typen = liste.ToArray();
        Selection.activeGameObject = go;
        Debug.Log("Biom: Gras eingerichtet (" + liste.Count + " Typen). Das Gras entsteht im Spiel dicht um die Kamera, nur auf Objekten mit 'Boden' im Namen. Dafuer vorher 'Szene aufbauen (mit Collidern)' ausfuehren.");
    }
}
#endif
