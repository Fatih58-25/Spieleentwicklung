#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildVorbereiten
{
    static readonly string[] Szenen =
    {
        "Assets/Scenes/TEST SCENE.unity",
        "Assets/Scenes/Samo.unity",
        "Assets/Scenes/Stadt.unity",
        "Assets/Scenes/Forest.unity",
        "Assets/Scenes/Wueste.unity",
        "Assets/Altar_Ruins_FREE/Demo_Scenes/DemoScene.unity"
    };

    [MenuItem("Spiel/Build vorbereiten (alle Szenen eintragen)")]
    public static void Vorbereiten()
    {
        var liste = new List<EditorBuildSettingsScene>();
        foreach (var pfad in Szenen)
        {
            if (!File.Exists(pfad))
            {
                Debug.LogWarning("Build: Die Szene " + pfad + " fehlt und wird übersprungen.");
                continue;
            }
            liste.Add(new EditorBuildSettingsScene(pfad, true));
        }
        EditorBuildSettings.scenes = liste.ToArray();
        Debug.Log("Build: " + liste.Count + " Szenen eingetragen. Die Startszene ist TEST SCENE, die anderen lädt das Spiel beim Start dazu.");
    }
}
#endif
