#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BiomHimmel
{
    const string Hdr = "Assets/Biom/Himmel/forest_slope_2k.hdr";

    [MenuItem("Biom/Himmel, Sonne und Nebel einrichten")]
    public static void Einrichten()
    {
        var ti = AssetImporter.GetAtPath(Hdr) as TextureImporter;
        if (ti != null && ti.textureShape != TextureImporterShape.Texture2D)
        {
            ti.textureShape = TextureImporterShape.Texture2D;
            ti.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture>(Hdr);
        var shader = Shader.Find("Skybox/Panoramic");
        if (tex != null && shader != null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Biom/Materialien"))
            {
                AssetDatabase.CreateFolder("Assets/Biom", "Materialien");
            }
            string mp = "Assets/Biom/Materialien/Biom_Himmel.mat";
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
            EditorUtility.SetDirty(mat);
            RenderSettings.skybox = mat;
        }
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0025f;
        RenderSettings.fogColor = new Color(0.62f, 0.7f, 0.68f);
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
        sonne.color = new Color(1f, 0.88f, 0.72f);
        sonne.intensity = 2.2f;
        sonne.shadows = LightShadows.Soft;
        sonne.transform.rotation = Quaternion.Euler(30f, 35f, 0f);
        RenderSettings.sun = sonne;
        AssetDatabase.SaveAssets();
        DynamicGI.UpdateEnvironment();
        Debug.Log("Biom: Himmel, Sonne und Nebel eingerichtet.");
    }

    [MenuItem("Biom/Himmel zuruecksetzen (ohne Hintergrundbild)")]
    public static void Zuruecksetzen()
    {
        var shader = Shader.Find("Skybox/Procedural");
        if (shader == null)
        {
            Debug.LogWarning("Shader Skybox/Procedural nicht gefunden.");
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Biom/Materialien"))
        {
            AssetDatabase.CreateFolder("Assets/Biom", "Materialien");
        }
        string mp = "Assets/Biom/Materialien/Biom_Himmel_Prozedural.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, mp);
        }
        mat.shader = shader;
        EditorUtility.SetDirty(mat);
        RenderSettings.skybox = mat;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        AssetDatabase.SaveAssets();
        DynamicGI.UpdateEnvironment();
        Debug.Log("Biom: Der Himmel ist wieder ein einfacher Standardhimmel. Das Hintergrundbild ist weg.");
    }
}
#endif
