using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WeltLader : MonoBehaviour
{
    const string Startszene = "TEST SCENE";
    static readonly string[] Weitere = { "Samo", "Stadt", "Forest", "Wueste", "DemoScene" };

    bool laedt = true;
    float fortschritt;
    float vorherigeZeit = 1f;
    GUIStyle stil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Starten()
    {
        if (Application.isEditor) return;
        if (SceneManager.GetActiveScene().name != Startszene) return;
        var go = new GameObject("WeltLader");
        DontDestroyOnLoad(go);
        go.AddComponent<WeltLader>();
    }

    IEnumerator Start()
    {
        vorherigeZeit = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        for (int i = 0; i < Weitere.Length; i++)
        {
            if (!SceneManager.GetSceneByName(Weitere[i]).isLoaded)
            {
                var op = SceneManager.LoadSceneAsync(Weitere[i], LoadSceneMode.Additive);
                if (op == null)
                {
                    Debug.LogWarning("WeltLader: Die Szene " + Weitere[i] + " ist nicht in den Build-Einstellungen.");
                    continue;
                }
                while (!op.isDone)
                {
                    fortschritt = (i + op.progress) / Weitere.Length;
                    yield return null;
                }
            }
            fortschritt = (i + 1f) / Weitere.Length;
        }
        yield return null;
        Time.timeScale = vorherigeZeit;
        laedt = false;
        Destroy(gameObject);
    }

    void OnGUI()
    {
        if (!laedt) return;
        if (stil == null)
        {
            stil = new GUIStyle(GUI.skin.label);
            stil.alignment = TextAnchor.MiddleCenter;
            stil.fontSize = 32;
            stil.normal.textColor = Color.white;
        }
        var alt = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = alt;
        GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, 60f), "Die Welt wird geladen ...", stil);
        float b = Screen.width * 0.4f;
        var rahmen = new Rect((Screen.width - b) * 0.5f, Screen.height * 0.42f + 70f, b, 14f);
        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        GUI.DrawTexture(rahmen, Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.85f, 0.6f);
        GUI.DrawTexture(new Rect(rahmen.x, rahmen.y, rahmen.width * Mathf.Clamp01(fortschritt), rahmen.height), Texture2D.whiteTexture);
        GUI.color = alt;
    }
}
