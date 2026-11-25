#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class StadtMaterialien
{
    const string Root = "Assets/Stadt";
    const string MatDir = Root + "/Materialien";

    class Spec
    {
        public string Albedo;
        public string Normal;
        public string Emission;
        public Color Base = Color.white;
        public Color Tint = Color.white;
        public Color EmissionColor = Color.black;
        public float Emissive = 0f;
        public float Smooth = 0.5f;
        public float Metal = 0f;
        public bool Transparent = false;
    }

    static void Solid(Dictionary<string, Spec> d, string name, Color linear, float smooth, float metal)
    {
        d[name] = new Spec { Base = linear.gamma, Smooth = smooth, Metal = metal };
    }

    static void Glow(Dictionary<string, Spec> d, string name, Color color, float intensity)
    {
        d[name] = new Spec { Base = new Color(0.05f, 0.05f, 0.05f), EmissionColor = color, Emissive = intensity, Smooth = 0.3f };
    }

    static void Foto(Dictionary<string, Spec> d, string name, string asset, string res, float smooth, float metal, Color tint)
    {
        d[name] = new Spec { Albedo = asset + "_" + res + "_Color", Normal = asset + "_" + res + "_NormalGL", Smooth = smooth, Metal = metal, Tint = tint };
    }

    static Dictionary<string, Spec> Build()
    {
        var d = new Dictionary<string, Spec>();
        string[] fam = { "Dunkel", "Perl", "Bronze" };
        float[] metal = { 0.92f, 0.85f, 0.92f };
        for (int f = 0; f < 3; f++)
        {
            for (int s = 0; s < 6; s++)
            {
                string tn = fam[f].ToLower();
                d["Haut_" + fam[f] + "_" + s] = new Spec
                {
                    Albedo = "haut_" + tn + "_" + s + "_albedo",
                    Emission = "haut_" + tn + "_" + s + "_licht",
                    Emissive = 1.0f,
                    Smooth = 0.82f,
                    Metal = metal[f]
                };
            }
        }
        Color weiss = Color.white;
        Color bronze = new Color(1f, 0.78f, 0.6f);
        Foto(d, "Foto_F18A", "Facade018A", "1K-JPG", 0.85f, 0.9f, weiss);
        Foto(d, "Foto_F19A", "Facade019A", "1K-JPG", 0.85f, 0.9f, weiss);
        Foto(d, "Foto_F20A", "Facade020A", "1K-JPG", 0.85f, 0.9f, weiss);
        Foto(d, "Foto_F5", "Facade005", "1K-JPG", 0.9f, 0.9f, weiss);
        Foto(d, "Foto_F2", "Facade002", "1K-JPG", 0.9f, 0.9f, weiss);
        Foto(d, "Foto_F3", "Facade003", "1K-JPG", 0.9f, 0.9f, weiss);
        Foto(d, "Foto_F19A_b", "Facade019A", "1K-JPG", 0.85f, 0.9f, new Color(1f, 0.82f, 0.68f));
        Foto(d, "Foto_P15A", "MetalPlates015A", "1K-JPG", 0.8f, 0.9f, weiss);
        Foto(d, "Foto_P16A", "MetalPlates016A", "1K-JPG", 0.8f, 0.9f, weiss);
        Foto(d, "Foto_P17A", "MetalPlates017A", "1K-JPG", 0.8f, 0.9f, weiss);
        Foto(d, "Foto_M50A", "Metal050A", "1K-JPG", 0.85f, 1f, weiss);
        Foto(d, "Foto_P16A_b", "MetalPlates016A", "1K-JPG", 0.8f, 0.9f, bronze);
        Foto(d, "Foto_P15A_b", "MetalPlates015A", "1K-JPG", 0.8f, 0.9f, bronze);
        d["Fassade_9"] = new Spec { Albedo = "Facade001_2K-JPG_Color", Normal = "Facade001_2K-JPG_NormalGL", Smooth = 0.9f, Metal = 0.95f };
        d["Fassade_10"] = new Spec { Albedo = "Facade006_2K-JPG_Color", Normal = "Facade006_2K-JPG_NormalGL", Smooth = 0.85f, Metal = 0.95f };
        d["Dach"] = new Spec { Albedo = "Concrete034_2K-JPG_Color", Normal = "Concrete034_2K-JPG_NormalGL", Smooth = 0.6f, Metal = 0.9f };
        d["Asphalt"] = new Spec { Albedo = "Asphalt033_1K-JPG_Color", Normal = "Asphalt033_1K-JPG_NormalGL", Smooth = 0.5f, Metal = 0.7f };
        d["Gehweg"] = new Spec { Albedo = "Tiles141_2K-JPG_Color", Normal = "Tiles141_2K-JPG_NormalGL", Smooth = 0.55f, Metal = 0.9f };
        Solid(d, "Chrom", new Color(0.85f, 0.87f, 0.9f), 0.92f, 1f);
        Solid(d, "Perlweiss", new Color(0.86f, 0.87f, 0.89f), 0.85f, 0.92f);
        Solid(d, "Gold", new Color(0.62f, 0.38f, 0.28f), 0.85f, 1f);
        Solid(d, "Markierung_weiss", new Color(0.75f, 0.75f, 0.72f), 0.6f, 0.9f);
        Solid(d, "Markierung_gelb", new Color(0.55f, 0.72f, 0.8f), 0.6f, 0.9f);
        Solid(d, "Technik", new Color(0.35f, 0.37f, 0.4f), 0.65f, 1f);
        Solid(d, "Antenne", new Color(0.07f, 0.07f, 0.08f), 0.65f, 0.9f);
        Solid(d, "Rahmen", new Color(0.02f, 0.02f, 0.025f), 0.7f, 0.8f);
        Solid(d, "Mast", new Color(0.45f, 0.48f, 0.52f), 0.7f, 1f);
        Solid(d, "Maschine_bronze", new Color(0.48f, 0.3f, 0.2f), 0.8f, 0.95f);
        Solid(d, "Maschine_grau", new Color(0.5f, 0.54f, 0.6f), 0.78f, 1f);
        Solid(d, "Lack_Perlmutt", new Color(0.88f, 0.9f, 0.94f), 0.85f, 0.9f);
        Solid(d, "Rippe", new Color(0.3f, 0.33f, 0.38f), 0.7f, 1f);
        Solid(d, "Reifen", new Color(0.04f, 0.04f, 0.05f), 0.65f, 0.5f);
        d["Kuppelglas"] = new Spec { Base = new Color(0.6f, 0.85f, 1f, 0.25f), Smooth = 0.98f, Metal = 0f, Transparent = true };
        d["Fahrzeug_Glas"] = new Spec { Base = new Color(0.55f, 0.75f, 0.95f, 0.35f), Smooth = 0.97f, Metal = 0f, Transparent = true };
        d["Kristall"] = new Spec { Base = new Color(0.35f, 0.8f, 0.95f, 0.7f), EmissionColor = new Color(0.1f, 0.75f, 0.95f), Emissive = 1.5f, Smooth = 0.95f, Transparent = true };
        Glow(d, "Energiekern", new Color(0.2f, 0.9f, 1f), 3.5f);
        Glow(d, "Laterne_Licht", new Color(0.85f, 0.92f, 1f), 6f);
        Glow(d, "Auto_Licht_rot", new Color(1f, 0.03f, 0.02f), 3f);
        Glow(d, "Auto_Licht_weiss", new Color(1f, 0.95f, 0.85f), 4f);
        Glow(d, "Warnlicht_rot", new Color(1f, 0.02f, 0.02f), 5f);
        Glow(d, "Warnlicht_orange", new Color(1f, 0.2f, 0.08f), 4f);
        Glow(d, "LED_Warm", new Color(1f, 0.86f, 0.78f), 2f);
        Glow(d, "LED_Cyan", new Color(0.1f, 0.8f, 1f), 3f);
        Glow(d, "LED_Weiss", new Color(1f, 0.95f, 0.9f), 2.5f);
        return d;
    }

    static Texture2D Tex(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name)
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
        }
        return null;
    }

    static void MarkNormal(Texture2D tex)
    {
        if (tex == null) return;
        string p = AssetDatabase.GetAssetPath(tex);
        var ti = AssetImporter.GetAtPath(p) as TextureImporter;
        if (ti != null && ti.textureType != TextureImporterType.NormalMap)
        {
            ti.textureType = TextureImporterType.NormalMap;
            ti.SaveAndReimport();
        }
    }

    static void SetFloat(Material m, string prop, float v)
    {
        if (m.HasProperty(prop)) m.SetFloat(prop, v);
    }

    static void Apply(Material m, Spec s)
    {
        var albedo = Tex(s.Albedo);
        if (albedo != null)
        {
            m.mainTexture = albedo;
            m.color = s.Tint;
        }
        else
        {
            m.color = s.Base;
        }
        var normal = Tex(s.Normal);
        if (normal != null)
        {
            MarkNormal(normal);
            m.SetTexture("_BumpMap", normal);
            m.EnableKeyword("_NORMALMAP");
        }
        SetFloat(m, "_Smoothness", s.Smooth);
        SetFloat(m, "_Glossiness", s.Smooth);
        SetFloat(m, "_Metallic", s.Metal);
        if (s.Transparent)
        {
            SetFloat(m, "_Surface", 1f);
            SetFloat(m, "_Blend", 0f);
            SetFloat(m, "_Mode", 3f);
            SetFloat(m, "_SrcBlend", 5f);
            SetFloat(m, "_DstBlend", 10f);
            SetFloat(m, "_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = 3000;
        }
        var em = Tex(s.Emission);
        if (em != null)
        {
            m.SetTexture("_EmissionMap", em);
            m.SetColor("_EmissionColor", Color.white * s.Emissive);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        else if (s.Emissive > 0f)
        {
            m.SetColor("_EmissionColor", s.EmissionColor * s.Emissive);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, i));
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }

    [MenuItem("Stadt/Materialien erzeugen und zuweisen")]
    public static void Run()
    {
        EnsureFolder(MatDir);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        int count = 0;
        foreach (var kv in Build())
        {
            string path = MatDir + "/" + kv.Key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            Apply(mat, kv.Value);
            EditorUtility.SetDirty(mat);
            count++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        int models = 0;
        var ordner = new List<string>();
        foreach (var o in new[] { "Viertel", "Fahrzeuge", "Luftverkehr", "Zug" })
        {
            if (AssetDatabase.IsValidFolder(Root + "/" + o)) ordner.Add(Root + "/" + o);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Model", ordner.ToArray()))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp == null) continue;
            imp.indexFormat = ModelImporterIndexFormat.UInt32;
            imp.isReadable = true;
            imp.importCameras = false;
            imp.importLights = false;
            imp.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
            imp.SaveAndReimport();
            models++;
        }
        Debug.Log("Stadt: " + count + " Materialien erzeugt, " + models + " Modelle zugewiesen.");
    }
}
#endif
