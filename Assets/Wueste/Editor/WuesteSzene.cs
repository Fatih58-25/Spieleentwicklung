#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class WuesteSzene
{
    const string Root = "Assets/Wueste";
    const string MatDir = Root + "/Materialien";
    const string TexDir = Root + "/Texturen";
    const string ModDir = Root + "/Modelle";
    const string ScanDir = Root + "/Scans";
    const string ScanTexDir = ScanDir + "/Texturen";
    const string HimmelHdr = Root + "/Himmel/himmel.hdr";

    const float SonneAzimut = 205f;
    const float SonneHoehe = 30f;
    const float HimmelDrehung = 0f;
    const float WindWinkelBlender = 28f;

    static Vector3 VonBlender(Vector3 b)
    {
        return new Vector3(-b.x, b.z, -b.y);
    }

    class Spec
    {
        public string Albedo;
        public Color Base = Color.white;
        public float Smooth = 0.1f;
        public bool Cutout = false;
        public bool TwoSided = false;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, i));
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }

    static Texture2D Laden(string datei)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + datei);
    }

    static void TexturEinstellen(string datei, bool srgb, bool normal, int maxGroesse, bool klemmen)
    {
        string pfad = TexDir + "/" + datei;
        var ti = AssetImporter.GetAtPath(pfad) as TextureImporter;
        if (ti == null) return;
        bool neu = false;
        var typ = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (ti.textureType != typ)
        {
            ti.textureType = typ;
            neu = true;
        }
        if (!normal && ti.sRGBTexture != srgb)
        {
            ti.sRGBTexture = srgb;
            neu = true;
        }
        if (ti.maxTextureSize != maxGroesse)
        {
            ti.maxTextureSize = maxGroesse;
            neu = true;
        }
        var wrap = klemmen ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        if (ti.wrapMode != wrap)
        {
            ti.wrapMode = wrap;
            neu = true;
        }
        if (ti.anisoLevel < 8)
        {
            ti.anisoLevel = 8;
            neu = true;
        }
        if (ti.textureCompression != TextureImporterCompression.CompressedHQ)
        {
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            neu = true;
        }
        if (neu) ti.SaveAndReimport();
    }

    static Material GelaendeMaterial()
    {
        EnsureFolder(MatDir);
        var shader = Shader.Find("Wueste/Gelaende");
        if (shader == null)
        {
            Debug.LogError("Wueste: Shader 'Wueste/Gelaende' nicht gefunden (Assets/Wueste/Shader/WuesteGelaende.shader).");
            return null;
        }
        TexturEinstellen("makro.png", false, false, 4096, true);
        TexturEinstellen("splat.png", false, false, 4096, true);
        TexturEinstellen("sand.png", true, false, 2048, false);
        TexturEinstellen("sand_n.png", false, true, 2048, false);
        TexturEinstellen("welle_n.png", false, true, 2048, false);
        TexturEinstellen("fels.jpg", true, false, 2048, false);
        TexturEinstellen("fels_n.jpg", false, true, 2048, false);
        TexturEinstellen("ton.jpg", true, false, 2048, false);
        TexturEinstellen("ton_n.jpg", false, true, 2048, false);
        TexturEinstellen("kies.jpg", true, false, 2048, false);
        TexturEinstellen("kies_n.jpg", false, true, 2048, false);
        string pfad = MatDir + "/Wueste_Gelaende.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(pfad);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, pfad);
        }
        mat.shader = shader;
        mat.SetTexture("_SplatMap", Laden("splat.png"));
        mat.SetTexture("_MacroMap", Laden("makro.png"));
        mat.SetTexture("_SandMap", Laden("sand.png"));
        mat.SetTexture("_SandNormal", Laden("sand_n.png"));
        mat.SetTexture("_RippleNormal", Laden("welle_n.png"));
        mat.SetTexture("_RockMap", Laden("fels.jpg"));
        mat.SetTexture("_RockNormal", Laden("fels_n.jpg"));
        mat.SetTexture("_ClayMap", Laden("ton.jpg"));
        mat.SetTexture("_ClayNormal", Laden("ton_n.jpg"));
        mat.SetTexture("_GravelMap", Laden("kies.jpg"));
        mat.SetTexture("_GravelNormal", Laden("kies_n.jpg"));
        Vector3 w = VonBlender(new Vector3(Mathf.Cos(WindWinkelBlender * Mathf.Deg2Rad), Mathf.Sin(WindWinkelBlender * Mathf.Deg2Rad), 0f));
        mat.SetFloat("_WindAngle", Mathf.Atan2(w.z, w.x));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture2D ScanTex(string name)
    {
        if (string.IsNullOrEmpty(name) || !AssetDatabase.IsValidFolder(ScanTexDir)) return null;
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Texture2D", new[] { ScanTexDir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        return null;
    }

    static void ScanTexturEinstellen(Texture2D t, bool alpha, bool normal)
    {
        if (t == null) return;
        var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) as TextureImporter;
        if (ti == null) return;
        bool neu = false;
        if (normal && ti.textureType != TextureImporterType.NormalMap)
        {
            ti.textureType = TextureImporterType.NormalMap;
            neu = true;
        }
        if (alpha && (!ti.alphaIsTransparency || !ti.mipMapsPreserveCoverage))
        {
            ti.alphaIsTransparency = true;
            ti.mipMapsPreserveCoverage = true;
            ti.alphaTestReferenceValue = 0.5f;
            ti.wrapMode = TextureWrapMode.Clamp;
            neu = true;
        }
        if (ti.anisoLevel < 4)
        {
            ti.anisoLevel = 4;
            neu = true;
        }
        if (neu) ti.SaveAndReimport();
    }

    static int ScanMaterialien(Shader lit)
    {
        string csv = ScanDir + "/scan_materialien.csv";
        if (!File.Exists(csv) || lit == null) return 0;
        int n = 0;
        foreach (var zeile in File.ReadAllLines(csv))
        {
            var t = zeile.Split(';');
            if (t.Length < 4 || string.IsNullOrEmpty(t[0])) continue;
            bool alpha = !string.IsNullOrEmpty(t[3]);
            string pfad = MatDir + "/" + t[0] + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(pfad);
            if (mat == null)
            {
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, pfad);
            }
            var alb = ScanTex(string.IsNullOrEmpty(t[1]) ? null : Path.GetFileNameWithoutExtension(t[1]));
            ScanTexturEinstellen(alb, alpha, false);
            if (alb != null)
            {
                mat.mainTexture = alb;
                mat.color = Color.white;
            }
            if (!string.IsNullOrEmpty(t[2]))
            {
                var nm = ScanTex(Path.GetFileNameWithoutExtension(t[2]));
                if (nm != null)
                {
                    ScanTexturEinstellen(nm, false, true);
                    mat.SetTexture("_BumpMap", nm);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", alpha ? 0f : 2f);
            if (alpha)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.renderQueue = 2450;
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            n++;
        }
        return n;
    }

    [MenuItem("Wueste/1 Materialien erzeugen und zuweisen")]
    public static void Materialien()
    {
        EnsureFolder(MatDir);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) lit = Shader.Find("Standard");
        var gel = GelaendeMaterial();
        int count = gel != null ? 1 : 0;
        count += ScanMaterialien(lit);
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
                imp.importNormals = ModelImporterNormals.Import;
                imp.importTangents = ModelImporterTangents.None;
                if (p.EndsWith("Wueste_Gelaende.fbx") || p.EndsWith("Wueste_Grenze.fbx")) imp.isReadable = true;
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
        Debug.Log("Wueste: " + count + " Materialien erzeugt, " + models + " Modelle bearbeitet, " + zuordnungen + " Materialien zugeordnet.");
    }

    [MenuItem("Wueste/Materialien in der Szene erzwingen")]
    public static void SzeneErzwingen()
    {
        int geaendert = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var mats = r.sharedMaterials;
            bool dirty = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                var echt = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + m.name + ".mat");
                if (echt == null || echt == m) continue;
                mats[i] = echt;
                dirty = true;
                geaendert++;
            }
            if (dirty)
            {
                Undo.RecordObject(r, "Wueste Materialien erzwingen");
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Wueste: " + geaendert + " Materialien in der Szene ersetzt.");
    }

    static int Streuen(Transform eltern)
    {
        var scans = AssetDatabase.LoadAssetAtPath<GameObject>(ModDir + "/Wueste_Scans.fbx");
        var punkte = AssetDatabase.LoadAssetAtPath<GameObject>(ModDir + "/Wueste_Streuung.fbx");
        if (scans == null || punkte == null) return 0;
        var wurzel = new GameObject("Wueste_Scans");
        wurzel.transform.SetParent(eltern, false);
        var temp = (GameObject)PrefabUtility.InstantiatePrefab(punkte);
        int n = 0;
        foreach (var t in temp.GetComponentsInChildren<Transform>(true))
        {
            string name = t.name;
            if (!name.StartsWith("E_")) continue;
            int i = name.LastIndexOf("__");
            if (i < 3) continue;
            string proto = name.Substring(2, i - 2);
            var vorlage = scans.transform.Find(proto);
            if (vorlage == null) continue;
            var go = Object.Instantiate(vorlage.gameObject, t.position, t.rotation);
            go.name = name.Substring(2);
            go.transform.localScale = t.lossyScale;
            go.transform.SetParent(wurzel.transform, true);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            if (proto.Contains("boulder") && go.GetComponent<MeshFilter>() != null && go.GetComponent<Collider>() == null) go.AddComponent<MeshCollider>();
            n++;
        }
        Object.DestroyImmediate(temp);
        return n;
    }

    [MenuItem("Wueste/2 Szene aufbauen (mit Collidern)")]
    public static void Aufbauen()
    {
        var root = new GameObject("Wueste");
        Undo.RegisterCreatedObjectUndo(root, "Wueste aufbauen");
        int collider = 0;
        foreach (var name in new[] { "Wueste_Gelaende", "Wueste_Grenze" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModDir + "/" + name + ".fbx");
            if (prefab == null)
            {
                Debug.LogWarning("Wueste: " + name + ".fbx fehlt in " + ModDir);
                continue;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(root.transform, false);
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                if (name == "Wueste_Grenze")
                {
                    var mr = mf.GetComponent<MeshRenderer>();
                    if (mr != null) mr.enabled = false;
                }
                if (mf.GetComponent<Collider>() == null)
                {
                    mf.gameObject.AddComponent<MeshCollider>();
                    collider++;
                }
            }
        }
        int scans = Streuen(root.transform);
        SzeneErzwingen();
        Selection.activeGameObject = root;
        Debug.Log("Wueste: aufgebaut, " + collider + " Mesh-Collider, " + scans + " Scans verteilt.");
    }

    [MenuItem("Wueste/3 Himmel, Sonne und Nebel einrichten")]
    public static void Himmel()
    {
        var ti = AssetImporter.GetAtPath(HimmelHdr) as TextureImporter;
        if (ti != null && (ti.textureShape != TextureImporterShape.Texture2D || ti.mipmapEnabled))
        {
            ti.textureShape = TextureImporterShape.Texture2D;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture>(HimmelHdr);
        var shader = Shader.Find("Skybox/Panoramic");
        if (tex != null && shader != null)
        {
            EnsureFolder(MatDir);
            string mp = MatDir + "/Wueste_Himmel.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, mp);
            }
            mat.shader = shader;
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Mapping", 1f);
            mat.SetFloat("_ImageType", 0f);
            mat.SetFloat("_Exposure", 1.0f);
            mat.SetFloat("_Rotation", HimmelDrehung);
            EditorUtility.SetDirty(mat);
            RenderSettings.skybox = mat;
        }
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0009f;
        RenderSettings.fogColor = new Color(0.78f, 0.72f, 0.62f);
        Light sonne = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional)
            {
                sonne = l;
                break;
            }
        }
        if (sonne == null)
        {
            var go = new GameObject("Sonne");
            sonne = go.AddComponent<Light>();
            sonne.type = LightType.Directional;
        }
        float az = SonneAzimut * Mathf.Deg2Rad;
        float el = SonneHoehe * Mathf.Deg2Rad;
        Vector3 zurSonne = VonBlender(new Vector3(Mathf.Cos(az) * Mathf.Cos(el), Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el)));
        sonne.transform.rotation = Quaternion.LookRotation(-zurSonne);
        sonne.color = new Color(1f, 0.95f, 0.87f);
        sonne.intensity = 1.4f;
        sonne.shadows = LightShadows.Soft;
        RenderSettings.sun = sonne;
        Effekte();
        AssetDatabase.SaveAssets();
        DynamicGI.UpdateEnvironment();
        Debug.Log("Wueste: Himmel, Sonne und Nebel eingerichtet.");
    }

    static void Effekte()
    {
        EnsureFolder(MatDir);
        string pfad = MatDir + "/Wueste_Effekte.asset";
        var profil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(pfad);
        if (profil == null)
        {
            profil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profil, pfad);
        }
        Tonemapping tm;
        if (!profil.TryGet(out tm)) tm = profil.Add<Tonemapping>(true);
        tm.active = true;
        tm.mode.overrideState = true;
        tm.mode.value = TonemappingMode.Neutral;
        ColorAdjustments ca;
        if (!profil.TryGet(out ca)) ca = profil.Add<ColorAdjustments>(true);
        ca.active = true;
        ca.postExposure.overrideState = true;
        ca.postExposure.value = -0.5f;
        EditorUtility.SetDirty(tm);
        EditorUtility.SetDirty(ca);
        EditorUtility.SetDirty(profil);
        var vorhanden = GameObject.Find("Wueste_Effekte");
        if (vorhanden == null)
        {
            vorhanden = new GameObject("Wueste_Effekte");
            vorhanden.AddComponent<Volume>();
        }
        var vol = vorhanden.GetComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 10;
        vol.sharedProfile = profil;
    }

    [MenuItem("Wueste/Alles auf einmal einrichten")]
    public static void Alles()
    {
        Materialien();
        Aufbauen();
        Himmel();
    }
}
#endif
