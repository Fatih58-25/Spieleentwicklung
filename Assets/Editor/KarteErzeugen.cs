#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class KarteErzeugen
{
    const string Ordner = "Assets/Karte/Resources";
    const int Aufloesung = 2048;

    static readonly string[] Szenen =
    {
        "Assets/Scenes/TEST SCENE.unity",
        "Assets/Scenes/Samo.unity",
        "Assets/Scenes/Stadt.unity",
        "Assets/Scenes/Forest.unity",
        "Assets/Scenes/Wueste.unity",
        "Assets/Altar_Ruins_FREE/Demo_Scenes/DemoScene.unity"
    };

    static readonly string[] Ausblenden = { "Stars", "Moon", "Luftverkehr", "Sky", "Himmel", "Dunst", "Fernboden" };

    [MenuItem("Spiel/Karte erstellen (Weltkarte von oben)")]
    public static void Erstellen()
    {
        Ausfuehren();
    }

    public static void Batch()
    {
        Ausfuehren();
    }

    static bool Geladen(string pfad)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).path == pfad) return true;
        }
        return false;
    }

    static bool Ausgeblendet(string name)
    {
        foreach (var a in Ausblenden)
        {
            if (name == a || name.StartsWith(a)) return true;
        }
        return false;
    }

    static Bounds MeshGrenzen(MeshFilter mf)
    {
        var mb = mf.sharedMesh.bounds;
        var t = mf.transform;
        var b = new Bounds(t.TransformPoint(mb.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var ecke = mb.center + new Vector3(((i & 1) == 0 ? -1 : 1) * mb.extents.x, ((i & 2) == 0 ? -1 : 1) * mb.extents.y, ((i & 4) == 0 ? -1 : 1) * mb.extents.z);
            b.Encapsulate(t.TransformPoint(ecke));
        }
        return b;
    }

    static bool SzenenGrenzen(Scene s, string bevorzugt, out Bounds b)
    {
        b = new Bounds();
        bool erste = true;
        if (!string.IsNullOrEmpty(bevorzugt))
        {
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mf.gameObject.scene != s || mf.sharedMesh == null || mf.gameObject.name != bevorzugt) continue;
                var g = MeshGrenzen(mf);
                if (erste)
                {
                    b = g;
                    erste = false;
                }
                else b.Encapsulate(g);
            }
            if (!erste) return true;
        }
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r.gameObject.scene != s || Ausgeblendet(r.gameObject.name)) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (r.bounds.size.magnitude > 3000f) continue;
            if (erste)
            {
                b = r.bounds;
                erste = false;
            }
            else b.Encapsulate(r.bounds);
        }
        return !erste;
    }

    static void Ausfuehren()
    {
        foreach (var pfad in Szenen)
        {
            if (File.Exists(pfad) && !Geladen(pfad)) EditorSceneManager.OpenScene(pfad, OpenSceneMode.Additive);
        }

        var regionen = new List<KartenRegion>();
        var gesamt = new Bounds();
        bool ersteRegion = true;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            string name = null;
            string grenze = null;
            Color farbe = Color.white;
            if (s.name == "Wueste")
            {
                name = "Wüste";
                grenze = "Wueste_Grenze";
                farbe = new Color(1f, 0.7f, 0.3f);
            }
            else if (s.name == "Forest")
            {
                name = "Forest-Biom";
                grenze = "Biom_Grenze";
                farbe = new Color(0.45f, 1f, 0.45f);
            }
            else if (s.name == "Stadt")
            {
                name = "Stadt";
                farbe = new Color(0.45f, 0.75f, 1f);
            }
            if (name == null) continue;
            Bounds b;
            if (!SzenenGrenzen(s, grenze, out b))
            {
                Debug.LogWarning("Karte: Für " + name + " wurden keine Objekte gefunden.");
                continue;
            }
            var r = new KartenRegion();
            r.name = name;
            r.mitte = new Vector2(b.center.x, b.center.z);
            r.groesse = new Vector2(b.size.x, b.size.z);
            r.farbe = farbe;
            regionen.Add(r);
            if (ersteRegion)
            {
                gesamt = b;
                ersteRegion = false;
            }
            else gesamt.Encapsulate(b);
            Debug.Log("Karte: Region " + name + " Mitte " + b.center + " Größe " + b.size);
        }
        if (regionen.Count == 0)
        {
            Debug.LogWarning("Karte: Es wurde keine Region gefunden. Sind die Szenen Wueste, Forest und Stadt geladen?");
            return;
        }

        float hoechster = gesamt.max.y;
        float tiefster = gesamt.min.y;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (Ausgeblendet(r.gameObject.name) || r.bounds.size.magnitude > 3000f) continue;
            hoechster = Mathf.Max(hoechster, r.bounds.max.y);
            tiefster = Mathf.Min(tiefster, r.bounds.min.y);
        }

        float seite = Mathf.Max(gesamt.size.x, gesamt.size.z) * 1.08f + 40f;
        Vector3 mitte = new Vector3(gesamt.center.x, 0f, gesamt.center.z);

        var versteckt = new List<GameObject>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t != null && t.gameObject.activeSelf && Ausgeblendet(t.name))
            {
                versteckt.Add(t.gameObject);
                t.gameObject.SetActive(false);
            }
        }
        bool nebel = RenderSettings.fog;
        RenderSettings.fog = false;

        var lichtGo = new GameObject("KartenLicht");
        var licht = lichtGo.AddComponent<Light>();
        licht.type = LightType.Directional;
        licht.intensity = 1.1f;
        licht.shadows = LightShadows.None;
        lichtGo.transform.rotation = Quaternion.Euler(80f, 20f, 0f);

        var kamGo = new GameObject("KartenKamera");
        var kam = kamGo.AddComponent<Camera>();
        var kd = kamGo.AddComponent<UniversalAdditionalCameraData>();
        kd.renderPostProcessing = false;
        kam.orthographic = true;
        kam.orthographicSize = seite * 0.5f;
        kam.clearFlags = CameraClearFlags.SolidColor;
        kam.backgroundColor = new Color(0.05f, 0.07f, 0.09f, 1f);
        kam.nearClipPlane = 1f;
        kam.farClipPlane = (hoechster - tiefster) + 600f;
        kamGo.transform.position = new Vector3(mitte.x, hoechster + 300f, mitte.z);
        kamGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        kam.allowHDR = false;

        var rt = new RenderTexture(Aufloesung, Aufloesung, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        kam.targetTexture = rt;
        kam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(Aufloesung, Aufloesung, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Aufloesung, Aufloesung), 0, 0);
        tex.Apply();
        var png = tex.EncodeToPNG();
        RenderTexture.active = null;
        kam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(kamGo);
        Object.DestroyImmediate(lichtGo);
        foreach (var g in versteckt)
        {
            if (g != null) g.SetActive(true);
        }
        RenderSettings.fog = nebel;

        if (!AssetDatabase.IsValidFolder("Assets/Karte")) AssetDatabase.CreateFolder("Assets", "Karte");
        if (!AssetDatabase.IsValidFolder(Ordner)) AssetDatabase.CreateFolder("Assets/Karte", "Resources");
        string pngPfad = Ordner + "/Weltkarte.png";
        File.WriteAllBytes(pngPfad, png);
        AssetDatabase.ImportAsset(pngPfad, ImportAssetOptions.ForceUpdate);
        var ti = AssetImporter.GetAtPath(pngPfad) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SaveAndReimport();
        }
        var karte = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPfad);
        string assetPfad = Ordner + "/Weltkarte.asset";
        var daten = AssetDatabase.LoadAssetAtPath<WeltkartenDaten>(assetPfad);
        if (daten == null)
        {
            daten = ScriptableObject.CreateInstance<WeltkartenDaten>();
            AssetDatabase.CreateAsset(daten, assetPfad);
        }
        daten.karte = karte;
        daten.ursprung = new Vector2(mitte.x - seite * 0.5f, mitte.z - seite * 0.5f);
        daten.groesse = seite;
        daten.regionen = regionen.ToArray();
        EditorUtility.SetDirty(daten);
        AssetDatabase.SaveAssets();
        Debug.Log("Karte: Weltkarte erstellt (" + Mathf.RoundToInt(seite) + " m Seitenlänge, " + regionen.Count + " Regionen) in " + Ordner);
    }
}
#endif
