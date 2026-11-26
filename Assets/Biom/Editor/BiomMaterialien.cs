#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BiomMaterialien
{
    const string Root = "Assets/Biom";
    const string MatDir = Root + "/Materialien";
    const string TexDir = Root + "/Texturen";
    const string ModDir = Root + "/Modelle";
    const string ScanDir = Root + "/Scans";
    const string ScanTexDir = ScanDir + "/Texturen";

    class Spec
    {
        public string Albedo;
        public Color Base = Color.white;
        public float Smooth = 0.1f;
        public float Metal = 0f;
        public bool Cutout = false;
        public bool TwoSided = false;
        public bool Transparent = false;
    }

    static Dictionary<string, Spec> Build()
    {
        var d = new Dictionary<string, Spec>();
        d["Biom_Ziegel"] = new Spec { Albedo = "ziegel", Smooth = 0.12f };
        d["Biom_Putz"] = new Spec { Albedo = "putz", Smooth = 0.1f };
        d["Biom_Putz_Moos"] = new Spec { Albedo = "putz_m", Smooth = 0.1f };
        d["Biom_Ziegel_Moos"] = new Spec { Albedo = "ziegel_m", Smooth = 0.12f };
        d["Biom_Stein"] = new Spec { Albedo = "stein", Smooth = 0.15f };
        d["Biom_Quader"] = new Spec { Albedo = "quader", Smooth = 0.15f };
        d["Biom_Holz"] = new Spec { Albedo = "holz", Smooth = 0.2f };
        d["Biom_Dach"] = new Spec { Albedo = "dach", Smooth = 0.15f, TwoSided = true };
        d["Biom_Pflaster"] = new Spec { Albedo = "pflaster", Smooth = 0.2f, TwoSided = true };
        d["Biom_Gras"] = new Spec { Albedo = "gras", Smooth = 0.05f };
        d["Biom_Erde"] = new Spec { Albedo = "erde", Smooth = 0.05f };
        d["Biom_Moos"] = new Spec { Albedo = "moos", Smooth = 0.05f };
        d["Biom_Rinde"] = new Spec { Albedo = "rinde", Smooth = 0.1f };
        d["Biom_Rost"] = new Spec { Base = new Color(0.45f, 0.27f, 0.2f), Smooth = 0.4f, Metal = 0.5f };
        d["Biom_Laub"] = new Spec { Albedo = "laub", Smooth = 0.15f, Cutout = true, TwoSided = true };
        d["Biom_Efeu"] = new Spec { Albedo = "efeu", Smooth = 0.2f, Cutout = true, TwoSided = true };
        d["Biom_Grastuft"] = new Spec { Albedo = "grastuft", Smooth = 0.1f, Cutout = true, TwoSided = true };
        d["Biom_Schilf"] = new Spec { Albedo = "schilf", Smooth = 0.1f, Cutout = true, TwoSided = true };
        d["Biom_Halm_Gruen"] = new Spec { Albedo = "halm_gruen", Smooth = 0.25f, TwoSided = true };
        d["Biom_Halm_Gemischt"] = new Spec { Albedo = "halm_gemischt", Smooth = 0.25f, TwoSided = true };
        d["Biom_Halm_Trocken"] = new Spec { Albedo = "halm_trocken", Smooth = 0.2f, TwoSided = true };
        d["Biom_Streu"] = new Spec { Albedo = "streu", Smooth = 0.1f, Cutout = true, TwoSided = true };
        d["Biom_Moosfleck"] = new Spec { Albedo = "moosfleck", Smooth = 0.05f, Cutout = true, TwoSided = true };
        d["Biom_Schaum"] = new Spec { Albedo = "schaum", Smooth = 0.2f, Cutout = true, TwoSided = true };
        d["Biom_Gischt"] = new Spec { Albedo = "gischt", Smooth = 0.1f, Transparent = true, TwoSided = true };
        d["Biom_Wasserfall"] = new Spec { Base = new Color(0.85f, 0.92f, 0.96f, 0.65f), Smooth = 0.9f, Transparent = true, TwoSided = true };
        d["Biom_Wasser"] =new Spec { Base = new Color(0.17f, 0.3f, 0.27f, 0.8f), Smooth = 0.95f, Transparent = true, TwoSided = true };
        return d;
    }

    static Texture2D Tex(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var suche = AssetDatabase.IsValidFolder(ScanTexDir) ? new[] { TexDir, ScanTexDir } : new[] { TexDir };
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Texture2D", suche))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        return null;
    }

    static void TexturEinstellen(string name, bool alpha)
    {
        var t = Tex(name);
        if (t == null) return;
        var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) as TextureImporter;
        if (ti == null) return;
        bool aendern = false;
        if (alpha && (!ti.alphaIsTransparency || !ti.mipMapsPreserveCoverage))
        {
            ti.alphaIsTransparency = true;
            ti.mipMapsPreserveCoverage = true;
            ti.alphaTestReferenceValue = 0.5f;
            ti.wrapMode = TextureWrapMode.Clamp;
            aendern = true;
        }
        if (ti.anisoLevel < 4)
        {
            ti.anisoLevel = 4;
            aendern = true;
        }
        if (aendern) ti.SaveAndReimport();
    }

    static void NormalMarkieren(Texture2D tex)
    {
        var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
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
            m.color = Color.white;
            var normal = Tex(s.Albedo + "_n");
            if (normal != null)
            {
                NormalMarkieren(normal);
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
        }
        else
        {
            m.color = s.Base;
        }
        SetFloat(m, "_Smoothness", s.Smooth);
        SetFloat(m, "_Glossiness", s.Smooth);
        SetFloat(m, "_Metallic", s.Metal);
        SetFloat(m, "_Cull", s.TwoSided ? 0f : 2f);
        if (s.Cutout)
        {
            SetFloat(m, "_AlphaClip", 1f);
            SetFloat(m, "_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = 2450;
        }
        if (s.Transparent)
        {
            SetFloat(m, "_Surface", 1f);
            SetFloat(m, "_Blend", 0f);
            SetFloat(m, "_SrcBlend", 5f);
            SetFloat(m, "_DstBlend", 10f);
            SetFloat(m, "_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, i));
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }

    [MenuItem("Biom/Materialien erzeugen und zuweisen")]
    public static void Run()
    {
        EnsureFolder(MatDir);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        int count = 0;
        foreach (var kv in Build())
        {
            TexturEinstellen(kv.Value.Albedo, kv.Value.Cutout);
            string path = MatDir + "/" + kv.Key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            Apply(mat, kv.Value);
            if (kv.Key.StartsWith("Biom_Halm_")) mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            count++;
        }
        count += ScanMaterialien(shader);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        int models = 0;
        int zuordnungen = 0;
        if (AssetDatabase.IsValidFolder(ModDir))
        {
            var vorhanden = new Dictionary<string, Material>();
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { MatDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m != null) vorhanden[m.name] = m;
            }
            var pfade = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModDir })) pfade.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var p in pfade)
            {
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) continue;
                imp.indexFormat = ModelImporterIndexFormat.UInt32;
                imp.importCameras = false;
                imp.importLights = false;
                imp.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
                imp.SaveAndReimport();
            }
            foreach (var p in pfade)
            {
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                {
                    var fm = o as Material;
                    if (fm == null) continue;
                    Material ziel;
                    if (!vorhanden.TryGetValue(fm.name, out ziel)) continue;
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), fm.name), ziel);
                    zuordnungen++;
                }
                imp.SaveAndReimport();
                models++;
            }
        }
        Debug.Log("Biom: " + count + " Materialien erzeugt, " + models + " Modelle bearbeitet, " + zuordnungen + " Materialien zugeordnet.");
    }

    static int ScanMaterialien(Shader shader)
    {
        string csv = ScanDir + "/scan_materialien.csv";
        if (!File.Exists(csv)) return 0;
        int n = 0;
        foreach (var zeile in File.ReadAllLines(csv))
        {
            var t = zeile.Split(';');
            if (t.Length < 4 || string.IsNullOrEmpty(t[0])) continue;
            var spec = new Spec
            {
                Albedo = string.IsNullOrEmpty(t[1]) ? null : Path.GetFileNameWithoutExtension(t[1]),
                Smooth = 0.15f,
                Cutout = !string.IsNullOrEmpty(t[3]),
                TwoSided = !string.IsNullOrEmpty(t[3])
            };
            string path = MatDir + "/" + t[0] + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            Apply(mat, spec);
            if (!string.IsNullOrEmpty(t[2]))
            {
                var nm = Tex(Path.GetFileNameWithoutExtension(t[2]));
                if (nm != null)
                {
                    NormalMarkieren(nm);
                    mat.SetTexture("_BumpMap", nm);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            n++;
        }
        return n;
    }

    [MenuItem("Biom/Materialien in der Szene erzwingen")]
    public static void SzeneErzwingen()
    {
        int renderer = 0;
        int geaendert = 0;
        var namen = new HashSet<string>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var mats = r.sharedMaterials;
            bool dirty = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                var echt = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + m.name + ".mat");
                if (echt == null) continue;
                namen.Add(m.name);
                if (echt != m)
                {
                    mats[i] = echt;
                    dirty = true;
                    geaendert++;
                }
            }
            renderer++;
            if (dirty)
            {
                Undo.RecordObject(r, "Biom Materialien erzwingen");
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Biom: " + renderer + " Renderer geprueft, " + geaendert + " Materialien ersetzt, " + namen.Count + " verschiedene Biom-Materialien gefunden.");
        foreach (var n in new[] { "Biom_Efeu", "Biom_Streu", "Biom_Moosfleck", "Biom_Laub", "Biom_Quader" })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + n + ".mat");
            if (m == null)
            {
                Debug.LogWarning("Biom: Material fehlt: " + n);
                continue;
            }
            var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
            Debug.Log("Biom: " + n + " | Shader=" + m.shader.name + " | BaseMap=" + (tex != null ? tex.name : "KEINE") + " | Cull=" + (m.HasProperty("_Cull") ? m.GetFloat("_Cull").ToString() : "-") + " | AlphaClip=" + (m.HasProperty("_AlphaClip") ? m.GetFloat("_AlphaClip").ToString() : "-") + " | Fall=" + (m.IsKeywordEnabled("_ALPHATEST_ON") ? "AlphaTest an" : "AlphaTest aus"));
        }
    }

    [MenuItem("Biom/Szene aufbauen (mit Collidern)")]
    public static void Aufbauen()
    {
        var root = new GameObject("Biom");
        Undo.RegisterCreatedObjectUndo(root, "Biom aufbauen");
        int teile = 0;
        int collider = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModDir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.EndsWith("Biom_Scans.fbx") || p.EndsWith("Biom_Streuung.fbx") || p.EndsWith("Biom_Gras.fbx")) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (prefab == null) continue;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(root.transform, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            teile++;
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.gameObject.name;
                if (n.Contains("Grenze"))
                {
                    var mr = mf.GetComponent<MeshRenderer>();
                    if (mr != null) mr.enabled = false;
                    if (mf.GetComponent<Collider>() == null)
                    {
                        mf.gameObject.AddComponent<MeshCollider>();
                        collider++;
                    }
                    continue;
                }
                if (n.Contains("Pflanzen") || n.Contains("Wasser") || n.Contains("Baeume")) continue;
                if (mf.GetComponent<Collider>() != null) continue;
                mf.gameObject.AddComponent<MeshCollider>();
                collider++;
            }
        }
        Selection.activeGameObject = root;
        Debug.Log("Biom: " + teile + " Teile aufgebaut, " + collider + " Mesh-Collider (ohne Pflanzen und Wasser).");
    }
}
#endif
