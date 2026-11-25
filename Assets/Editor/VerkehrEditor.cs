#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class VerkehrEditor
{
    static GameObject Laden(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Model"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name)
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
        }
        Debug.LogWarning("FBX nicht gefunden: " + name);
        return null;
    }

    [MenuItem("Stadt/Bodenverkehr erstellen")]
    public static void Bodenverkehr()
    {
        var go = new GameObject("Verkehr");
        var v = go.AddComponent<FahrzeugVerkehr>();
        v.transporter = Laden("Fahrzeug_Transporter");
        v.muldenkipper = Laden("Fahrzeug_Muldenkipper");
        v.raupe = Laden("Fahrzeug_Raupe");
        Selection.activeGameObject = go;
    }

    [MenuItem("Stadt/Luftverkehr erstellen")]
    public static void Luftverkehr()
    {
        var go = new GameObject("Luftverkehr");
        var v = go.AddComponent<LuftVerkehr>();
        v.kreuzer = Laden("Flug_Kreuzer");
        v.frachter = Laden("Flug_Frachter");
        v.luftbus = Laden("Flug_Luftbus");
        v.drohne = Laden("Flug_Drohne");
        Selection.activeGameObject = go;
    }

    [MenuItem("Stadt/Zuege erstellen")]
    public static void Zuege()
    {
        var go = new GameObject("Zuege");
        var z = go.AddComponent<ZugSystem>();
        z.zugVorlage = Laden("Zug");
        var spieler = GameObject.FindWithTag("Player");
        if (spieler != null) z.spieler = spieler.transform;
        Selection.activeGameObject = go;
    }

    [MenuItem("Stadt/Aufzuege erstellen")]
    public static void Aufzuege()
    {
        var go = new GameObject("Aufzuege");
        var a = go.AddComponent<AufzugSystem>();
        var spieler = GameObject.FindWithTag("Player");
        if (spieler != null) a.spieler = spieler.transform;
        Selection.activeGameObject = go;
    }

    [MenuItem("Stadt/Alles erstellen (Boden, Luft, Zuege, Aufzuege)")]
    public static void Alles()
    {
        Bodenverkehr();
        Luftverkehr();
        Zuege();
        Aufzuege();
        Debug.Log("Verkehr, Luftverkehr, Zuege und Aufzuege erstellt. Spieler-Objekt mit Tag Player wird fuer Zug und Aufzug benutzt.");
    }
}
#endif
