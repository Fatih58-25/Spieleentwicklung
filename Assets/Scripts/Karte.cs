using UnityEngine;
using UnityEngine.InputSystem;

public class Karte : MonoBehaviour
{
    static Karte instanz;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zuruecksetzen()
    {
        instanz = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Starten()
    {
        if (instanz != null) return;
        var go = new GameObject("Karte");
        DontDestroyOnLoad(go);
        instanz = go.AddComponent<Karte>();
    }

    WeltkartenDaten daten;
    bool datenGeladen;
    bool offen;
    float vorherigeZeit = 1f;
    CursorLockMode vorherigeSperre;
    bool vorherigSichtbar;
    PlayerMovement spielerBewegung;
    TPSCamera kamera;
    bool bewegungWarAn;
    bool kameraWarAn;
    Transform spieler;
    GegenstandAufheben[] schluessel;
    Texture2D pfeil;

    GUIStyle titelStil;
    GUIStyle regionStil;
    GUIStyle textStil;
    GUIStyle ecke;

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;
        if (k.mKey.wasPressedThisFrame)
        {
            if (offen) Schliessen();
            else if (!InteraktionsZone.GlobalGesperrt) Oeffnen();
            return;
        }
        if (offen && k.escapeKey.wasPressedThisFrame) Schliessen();
    }

    void Oeffnen()
    {
        if (!datenGeladen)
        {
            daten = Resources.Load<WeltkartenDaten>("Weltkarte");
            datenGeladen = true;
        }
        offen = true;
        InteraktionsZone.GlobalGesperrt = true;
        vorherigeZeit = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        vorherigeSperre = Cursor.lockState;
        vorherigSichtbar = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        spielerBewegung = FindFirstObjectByType<PlayerMovement>();
        if (spielerBewegung != null)
        {
            bewegungWarAn = spielerBewegung.enabled;
            spielerBewegung.enabled = false;
        }
        kamera = FindFirstObjectByType<TPSCamera>();
        if (kamera != null)
        {
            kameraWarAn = kamera.enabled;
            kamera.enabled = false;
        }
        var s = GameObject.FindGameObjectWithTag("Player");
        spieler = s != null ? s.transform : null;
        schluessel = FindObjectsByType<GegenstandAufheben>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    void Schliessen()
    {
        if (!offen) return;
        offen = false;
        InteraktionsZone.GlobalGesperrt = false;
        Time.timeScale = vorherigeZeit;
        Cursor.lockState = vorherigeSperre;
        Cursor.visible = vorherigSichtbar;
        if (spielerBewegung != null) spielerBewegung.enabled = bewegungWarAn;
        if (kamera != null) kamera.enabled = kameraWarAn;
    }

    void OnDestroy()
    {
        if (offen) Schliessen();
        if (instanz == this) instanz = null;
    }

    void StileErzeugen()
    {
        if (titelStil != null) return;
        titelStil = new GUIStyle(GUI.skin.label);
        titelStil.alignment = TextAnchor.MiddleCenter;
        titelStil.fontSize = 30;
        titelStil.fontStyle = FontStyle.Bold;
        titelStil.normal.textColor = new Color(0.95f, 0.85f, 0.6f);
        regionStil = new GUIStyle(GUI.skin.label);
        regionStil.alignment = TextAnchor.MiddleCenter;
        regionStil.fontSize = 26;
        regionStil.fontStyle = FontStyle.Bold;
        textStil = new GUIStyle(GUI.skin.label);
        textStil.alignment = TextAnchor.MiddleLeft;
        textStil.fontSize = 20;
        textStil.normal.textColor = Color.white;
        ecke = new GUIStyle(GUI.skin.label);
        ecke.alignment = TextAnchor.MiddleRight;
        ecke.fontSize = 18;
        ecke.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        pfeil = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        var px = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float breite = (31 - y) * 0.5f;
                bool innen = Mathf.Abs(x - 15.5f) <= breite && y >= 3;
                px[y * 32 + x] = innen ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        }
        pfeil.SetPixels(px);
        pfeil.Apply();
    }

    static void Flaeche(Rect r, Color farbe)
    {
        var alt = GUI.color;
        GUI.color = farbe;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = alt;
    }

    static void Rahmen(Rect r, Color farbe, float dicke)
    {
        Flaeche(new Rect(r.x, r.y, r.width, dicke), farbe);
        Flaeche(new Rect(r.x, r.yMax - dicke, r.width, dicke), farbe);
        Flaeche(new Rect(r.x, r.y, dicke, r.height), farbe);
        Flaeche(new Rect(r.xMax - dicke, r.y, dicke, r.height), farbe);
    }

    void Beschriften(Rect r, string text, GUIStyle stil, Color farbe)
    {
        var alt = stil.normal.textColor;
        stil.normal.textColor = Color.black;
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, stil);
        stil.normal.textColor = farbe;
        GUI.Label(r, text, stil);
        stil.normal.textColor = alt;
    }

    Vector2 ZuGui(Rect karte, Vector3 welt)
    {
        float u = (welt.x - daten.ursprung.x) / daten.groesse;
        float v = (welt.z - daten.ursprung.y) / daten.groesse;
        return new Vector2(karte.x + u * karte.width, karte.yMax - v * karte.height);
    }

    void OnGUI()
    {
        StileErzeugen();
        if (!offen)
        {
            if (!InteraktionsZone.GlobalGesperrt) GUI.Label(new Rect(Screen.width - 260f, Screen.height - 74f, 240f, 30f), "[M] Karte", ecke);
            return;
        }
        Flaeche(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.85f));
        Beschriften(new Rect(0f, 12f, Screen.width, 44f), "Weltkarte", titelStil, new Color(0.95f, 0.85f, 0.6f));
        if (daten == null || daten.karte == null)
        {
            Beschriften(new Rect(0f, Screen.height * 0.45f, Screen.width, 80f), "Es gibt noch keine Karte. Im Menü Spiel > Karte erstellen wählen.", textStil, Color.white);
            return;
        }
        float seite = Mathf.Min(Screen.width - 40f, Screen.height - 140f);
        var karte = new Rect((Screen.width - seite) * 0.5f, 62f, seite, seite);
        Flaeche(karte, new Color(0.05f, 0.07f, 0.09f, 1f));
        GUI.DrawTexture(karte, daten.karte, ScaleMode.StretchToFill, false);
        Rahmen(karte, new Color(0.95f, 0.85f, 0.6f, 0.9f), 3f);

        if (daten.regionen != null)
        {
            foreach (var r in daten.regionen)
            {
                var a = ZuGui(karte, new Vector3(r.mitte.x - r.groesse.x * 0.5f, 0f, r.mitte.y - r.groesse.y * 0.5f));
                var b = ZuGui(karte, new Vector3(r.mitte.x + r.groesse.x * 0.5f, 0f, r.mitte.y + r.groesse.y * 0.5f));
                var rechteck = new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
                Rahmen(rechteck, new Color(r.farbe.r, r.farbe.g, r.farbe.b, 0.9f), 3f);
                Beschriften(new Rect(rechteck.x, rechteck.y + rechteck.height * 0.5f - 20f, rechteck.width, 40f), r.name, regionStil, r.farbe);
            }
        }

        if (SpielStand.Hat("BriefGelesen") && schluessel != null)
        {
            foreach (var g in schluessel)
            {
                if (g == null || g.Genommen) continue;
                if (g.gegenstandName == null || !g.gegenstandName.StartsWith("Schluessel_")) continue;
                var p = ZuGui(karte, g.transform.position);
                if (!karte.Contains(p)) continue;
                var alt = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, p);
                Flaeche(new Rect(p.x - 11f, p.y - 11f, 22f, 22f), Color.black);
                Flaeche(new Rect(p.x - 8f, p.y - 8f, 16f, 16f), new Color(1f, 0.82f, 0.2f));
                GUI.matrix = alt;
                string buchstabe = g.gegenstandName.Substring(g.gegenstandName.Length - 1);
                Beschriften(new Rect(p.x + 14f, p.y - 14f, 200f, 28f), "Schlüssel " + buchstabe, textStil, new Color(1f, 0.9f, 0.4f));
            }
        }

        if (spieler != null)
        {
            var p = ZuGui(karte, spieler.position);
            var alt = GUI.matrix;
            GUIUtility.RotateAroundPivot(spieler.eulerAngles.y, p);
            var alteFarbe = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(p.x - 17f, p.y - 17f, 34f, 34f), pfeil);
            GUI.color = new Color(0.3f, 0.9f, 1f);
            GUI.DrawTexture(new Rect(p.x - 13f, p.y - 13f, 26f, 26f), pfeil);
            GUI.color = alteFarbe;
            GUI.matrix = alt;
        }

        float y = karte.yMax + 10f;
        Beschriften(new Rect(karte.x, y, 400f, 28f), "Pfeil: Du", textStil, new Color(0.3f, 0.9f, 1f));
        if (SpielStand.Hat("BriefGelesen")) Beschriften(new Rect(karte.x + 200f, y, 500f, 28f), "Raute: gesuchter Schlüssel", textStil, new Color(1f, 0.9f, 0.4f));
        else Beschriften(new Rect(karte.x + 200f, y, 600f, 28f), "Lies den Brief, um die Schlüssel zu finden.", textStil, new Color(0.8f, 0.8f, 0.8f));
        Beschriften(new Rect(karte.xMax - 320f, y, 320f, 28f), "[M] oder [Esc] schließen", ecke, new Color(0.9f, 0.9f, 0.9f));
    }
}
