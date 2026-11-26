using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GegenstandArt
{
    Sonstiges,
    Brief,
    Ring,
    Schluessel
}

public class InventarEintrag
{
    public string id;
    public string anzeigeName;
    public string beschreibung;
    public GegenstandArt art;
    public GameObject modell;
    public string briefTitel;
    public string briefText;
}

public class Inventar : MonoBehaviour
{
    enum Ansicht
    {
        Liste,
        Lesen,
        Betrachten
    }

    const int VorschauEbene = 31;

    static Inventar instanz;

    public static Inventar Instanz
    {
        get
        {
            if (instanz == null)
            {
                var go = new GameObject("Inventar");
                if (Application.isPlaying) DontDestroyOnLoad(go);
                instanz = go.AddComponent<Inventar>();
            }
            return instanz;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zuruecksetzen()
    {
        instanz = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Starten()
    {
        if (Instanz == null) return;
    }

    readonly List<InventarEintrag> eintraege = new List<InventarEintrag>();
    bool offen;
    bool hinweisGezeigt;
    Ansicht ansicht;
    InventarEintrag aktuell;
    float vorherigeZeit = 1f;
    CursorLockMode vorherigeSperre;
    bool vorherigSichtbar;
    PlayerMovement spielerBewegung;
    TPSCamera kamera;
    bool bewegungWarAn;
    bool kameraWarAn;

    Camera vorschauKamera;
    RenderTexture vorschauTextur;
    GameObject vorschauWurzel;
    GameObject vorschauKlon;
    bool ziehen;

    GUIStyle titelStil;
    GUIStyle textStil;
    GUIStyle knopfStil;
    GUIStyle kleinStil;
    GUIStyle briefTitelStil;
    GUIStyle briefTextStil;
    GUIStyle hinweisEcke;

    public static bool Hat(string id)
    {
        return instanz != null && instanz.Suche(id) != null;
    }

    public static string MitHinweis(string meldung)
    {
        var i = Instanz;
        if (i.hinweisGezeigt) return meldung;
        i.hinweisGezeigt = true;
        return meldung + " [I] öffnet das Inventar.";
    }

    public static void StandardInfo(string id, out string name, out string beschreibung)
    {
        name = id;
        beschreibung = "";
        if (id == "Schluessel_A")
        {
            name = "Alter Schlüssel (A)";
            beschreibung = "Ein schwerer, alter Schlüssel. Er öffnet die Tore der Stadt.";
        }
        else if (id == "Schluessel_B")
        {
            name = "Alter Schlüssel (B)";
            beschreibung = "Der zweite Schlüssel. Zusammen mit dem dritten kann er die Maschinen abschalten.";
        }
        else if (id == "Schluessel_C")
        {
            name = "Alter Schlüssel (C)";
            beschreibung = "Der dritte Schlüssel. Zusammen mit dem zweiten kann er die Maschinen abschalten.";
        }
    }

    public static void Hinzufuegen(string id, string name, string beschreibung, GegenstandArt art, GameObject vorlage, string briefTitel = null, string briefText = null)
    {
        var inv = Instanz;
        if (inv.Suche(id) != null) return;
        var e = new InventarEintrag();
        e.id = id;
        e.anzeigeName = name;
        e.beschreibung = beschreibung;
        e.art = art;
        e.briefTitel = briefTitel;
        e.briefText = briefText;
        if (vorlage != null) e.modell = inv.KlonVorbereiten(vorlage);
        inv.eintraege.Add(e);
        SpielStand.Setze(id);
    }

    InventarEintrag Suche(string id)
    {
        foreach (var e in eintraege)
        {
            if (e.id == id) return e;
        }
        return null;
    }

    GameObject KlonVorbereiten(GameObject vorlage)
    {
        var k = Instantiate(vorlage);
        k.name = vorlage.name + "_Inventar";
        k.transform.SetParent(transform, false);
        foreach (var c in k.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var s in k.GetComponentsInChildren<SchluesselSchweben>(true)) Destroy(s);
        foreach (var l in k.GetComponentsInChildren<Light>(true)) Destroy(l);
        k.SetActive(false);
        return k;
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;
        if (k.iKey.wasPressedThisFrame)
        {
            if (offen) Schliessen();
            else if (!InteraktionsZone.GlobalGesperrt) Oeffnen();
            return;
        }
        if (!offen) return;
        if (k.escapeKey.wasPressedThisFrame)
        {
            if (ansicht == Ansicht.Liste) Schliessen();
            else ZurListe();
            return;
        }
        if (ansicht == Ansicht.Betrachten && vorschauKlon != null && !ziehen)
        {
            vorschauKlon.transform.Rotate(Vector3.up, 30f * Time.unscaledDeltaTime, Space.World);
        }
    }

    void Oeffnen()
    {
        offen = true;
        ansicht = Ansicht.Liste;
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
    }

    void Schliessen()
    {
        if (!offen) return;
        VorschauBeenden();
        offen = false;
        aktuell = null;
        InteraktionsZone.GlobalGesperrt = false;
        Time.timeScale = vorherigeZeit;
        Cursor.lockState = vorherigeSperre;
        Cursor.visible = vorherigSichtbar;
        if (spielerBewegung != null) spielerBewegung.enabled = bewegungWarAn;
        if (kamera != null) kamera.enabled = kameraWarAn;
    }

    void ZurListe()
    {
        VorschauBeenden();
        ansicht = Ansicht.Liste;
        aktuell = null;
    }

    void OnDestroy()
    {
        if (offen) Schliessen();
        if (instanz == this) instanz = null;
    }

    RingTraeger RingTraegerHolen()
    {
        var rt = RingTraeger.Instanz;
        if (rt != null) return rt;
        var spieler = GameObject.FindGameObjectWithTag("Player");
        if (spieler == null) return null;
        rt = spieler.GetComponent<RingTraeger>();
        if (rt == null) rt = spieler.AddComponent<RingTraeger>();
        return rt;
    }

    string AktionText(InventarEintrag e)
    {
        if (e.art == GegenstandArt.Brief) return "Linksklick: lesen";
        if (e.art == GegenstandArt.Ring) return RingTraeger.HatRing ? "Linksklick: ausziehen (wird getragen)" : "Linksklick: anziehen";
        return "Linksklick: ansehen";
    }

    void Aktion(InventarEintrag e)
    {
        if (e.art == GegenstandArt.Brief)
        {
            aktuell = e;
            ansicht = Ansicht.Lesen;
            SpielStand.Setze("BriefGelesen");
        }
        else if (e.art == GegenstandArt.Ring)
        {
            var rt = RingTraegerHolen();
            if (rt == null) return;
            if (rt.traegtRing) rt.RingAblegen();
            else rt.RingAnlegen();
        }
        else
        {
            aktuell = e;
            ansicht = Ansicht.Betrachten;
            VorschauStarten(e);
        }
    }

    static void EbeneSetzen(GameObject go, int ebene)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ebene;
    }

    void VorschauStarten(InventarEintrag e)
    {
        VorschauBeenden();
        if (e.modell == null) return;
        vorschauWurzel = new GameObject("InventarVorschau");
        vorschauWurzel.transform.position = new Vector3(0f, -6000f, 0f);
        vorschauKlon = Instantiate(e.modell, vorschauWurzel.transform);
        vorschauKlon.transform.localPosition = Vector3.zero;
        vorschauKlon.transform.localRotation = Quaternion.Euler(20f, 30f, 0f);
        vorschauKlon.SetActive(true);
        EbeneSetzen(vorschauKlon, VorschauEbene);
        var rs = vorschauKlon.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        vorschauKlon.transform.position -= b.center - vorschauWurzel.transform.position;
        float radius = Mathf.Max(0.01f, b.extents.magnitude);
        vorschauTextur = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
        var kgo = new GameObject("InventarKamera");
        kgo.transform.SetParent(vorschauWurzel.transform, false);
        vorschauKamera = kgo.AddComponent<Camera>();
        vorschauKamera.clearFlags = CameraClearFlags.SolidColor;
        vorschauKamera.backgroundColor = new Color(0.1f, 0.09f, 0.08f, 1f);
        vorschauKamera.cullingMask = 1 << VorschauEbene;
        vorschauKamera.fieldOfView = 30f;
        vorschauKamera.nearClipPlane = 0.005f;
        vorschauKamera.farClipPlane = 100f;
        vorschauKamera.targetTexture = vorschauTextur;
        float abstand = radius / Mathf.Sin(15f * Mathf.Deg2Rad) * 1.05f;
        kgo.transform.position = vorschauWurzel.transform.position + new Vector3(0f, 0f, -abstand);
        kgo.transform.LookAt(vorschauWurzel.transform.position);
    }

    void VorschauBeenden()
    {
        ziehen = false;
        if (vorschauKamera != null) vorschauKamera.targetTexture = null;
        if (vorschauTextur != null)
        {
            vorschauTextur.Release();
            Destroy(vorschauTextur);
            vorschauTextur = null;
        }
        if (vorschauWurzel != null) Destroy(vorschauWurzel);
        vorschauWurzel = null;
        vorschauKlon = null;
        vorschauKamera = null;
    }

    void StileErzeugen()
    {
        if (titelStil != null) return;
        titelStil = new GUIStyle(GUI.skin.label);
        titelStil.alignment = TextAnchor.MiddleCenter;
        titelStil.fontSize = 32;
        titelStil.fontStyle = FontStyle.Bold;
        titelStil.normal.textColor = new Color(0.95f, 0.85f, 0.6f);
        textStil = new GUIStyle(GUI.skin.label);
        textStil.alignment = TextAnchor.UpperLeft;
        textStil.fontSize = 22;
        textStil.wordWrap = true;
        textStil.normal.textColor = Color.white;
        kleinStil = new GUIStyle(GUI.skin.label);
        kleinStil.alignment = TextAnchor.MiddleCenter;
        kleinStil.fontSize = 18;
        kleinStil.normal.textColor = new Color(0.75f, 0.7f, 0.6f);
        knopfStil = new GUIStyle(GUI.skin.button);
        knopfStil.fontSize = 22;
        knopfStil.alignment = TextAnchor.MiddleLeft;
        knopfStil.padding = new RectOffset(24, 12, 6, 6);
        briefTitelStil = new GUIStyle(GUI.skin.label);
        briefTitelStil.alignment = TextAnchor.MiddleCenter;
        briefTitelStil.fontSize = 30;
        briefTitelStil.fontStyle = FontStyle.Bold;
        briefTitelStil.normal.textColor = new Color(0.25f, 0.15f, 0.08f);
        hinweisEcke = new GUIStyle(GUI.skin.label);
        hinweisEcke.alignment = TextAnchor.MiddleRight;
        hinweisEcke.fontSize = 18;
        hinweisEcke.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        briefTextStil = new GUIStyle(GUI.skin.label);
        briefTextStil.alignment = TextAnchor.UpperLeft;
        briefTextStil.fontSize = 22;
        briefTextStil.wordWrap = true;
        briefTextStil.normal.textColor = new Color(0.2f, 0.12f, 0.06f);
    }

    static Rect Mitte(float breite, float hoehe)
    {
        float b = Mathf.Min(breite, Screen.width - 40f);
        float h = Mathf.Min(hoehe, Screen.height - 40f);
        return new Rect((Screen.width - b) * 0.5f, (Screen.height - h) * 0.5f, b, h);
    }

    static void Flaeche(Rect r, Color farbe)
    {
        var alt = GUI.color;
        GUI.color = farbe;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = alt;
    }

    void OnGUI()
    {
        StileErzeugen();
        if (!offen)
        {
            if (!InteraktionsZone.GlobalGesperrt) GUI.Label(new Rect(Screen.width - 260f, Screen.height - 44f, 240f, 30f), "[I] Inventar", hinweisEcke);
            return;
        }
        Flaeche(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.7f));
        if (ansicht == Ansicht.Liste) ListeZeichnen();
        else if (ansicht == Ansicht.Lesen) LesenZeichnen();
        else BetrachtenZeichnen();
    }

    void ListeZeichnen()
    {
        var r = Mitte(820f, 580f);
        Flaeche(r, new Color(0.13f, 0.11f, 0.09f, 0.98f));
        GUI.Label(new Rect(r.x + 30f, r.y + 16f, r.width - 60f, 50f), "Inventar", titelStil);
        InventarEintrag hover = null;
        if (eintraege.Count == 0)
        {
            GUI.Label(new Rect(r.x + 40f, r.y + 90f, r.width - 80f, 40f), "Dein Inventar ist leer.", textStil);
        }
        for (int i = 0; i < eintraege.Count; i++)
        {
            var e = eintraege[i];
            var zeile = new Rect(r.x + 30f, r.y + 80f + i * 68f, r.width - 60f, 60f);
            if (zeile.Contains(Event.current.mousePosition)) hover = e;
            if (GUI.Button(zeile, e.anzeigeName + "   (" + AktionText(e) + ")", knopfStil)) Aktion(e);
        }
        string info = hover != null ? hover.beschreibung : "Klicke einen Gegenstand mit der linken Maustaste an.";
        GUI.Label(new Rect(r.x + 40f, r.y + 80f + 5 * 68f + 6f, r.width - 80f, 80f), info, textStil);
        GUI.Label(new Rect(r.x, r.yMax - 44f, r.width, 36f), "[I] oder [Esc] schließen", kleinStil);
    }

    void LesenZeichnen()
    {
        var blatt = Mitte(Mathf.Min(Screen.width * 0.7f, 900f), Screen.height * 0.84f);
        Flaeche(blatt, new Color(0.93f, 0.86f, 0.7f, 1f));
        string titel = aktuell != null && !string.IsNullOrEmpty(aktuell.briefTitel) ? aktuell.briefTitel : "Brief";
        string text = aktuell != null ? aktuell.briefText : "";
        GUI.Label(new Rect(blatt.x + 30f, blatt.y + 20f, blatt.width - 60f, 50f), titel, briefTitelStil);
        GUI.Label(new Rect(blatt.x + 40f, blatt.y + 85f, blatt.width - 80f, blatt.height - 150f), text, briefTextStil);
        GUI.Label(new Rect(blatt.x, blatt.yMax - 50f, blatt.width, 40f), "[Linksklick] oder [Esc] zurück", new GUIStyle(kleinStil) { normal = { textColor = new Color(0.35f, 0.25f, 0.15f) } });
        var ev = Event.current;
        if (ev.type == EventType.MouseDown && ev.button == 0)
        {
            ev.Use();
            ZurListe();
        }
    }

    void BetrachtenZeichnen()
    {
        var r = Mitte(900f, 560f);
        Flaeche(r, new Color(0.13f, 0.11f, 0.09f, 0.98f));
        string name = aktuell != null ? aktuell.anzeigeName : "";
        GUI.Label(new Rect(r.x + 30f, r.y + 16f, r.width - 60f, 50f), name, titelStil);
        var bild = new Rect(r.x + 30f, r.y + 80f, 400f, 400f);
        Flaeche(bild, new Color(0.1f, 0.09f, 0.08f, 1f));
        if (vorschauTextur != null) GUI.DrawTexture(bild, vorschauTextur, ScaleMode.ScaleToFit, false);
        else GUI.Label(bild, "Keine Vorschau verfügbar.", kleinStil);
        var ev = Event.current;
        if (ev.type == EventType.MouseDown && ev.button == 0 && bild.Contains(ev.mousePosition)) ziehen = true;
        if (ev.type == EventType.MouseUp) ziehen = false;
        if (ev.type == EventType.MouseDrag && ziehen && vorschauKlon != null)
        {
            vorschauKlon.transform.Rotate(Vector3.up, -ev.delta.x * 0.8f, Space.World);
            vorschauKlon.transform.Rotate(Vector3.right, ev.delta.y * 0.8f, Space.World);
            ev.Use();
        }
        string beschr = aktuell != null ? aktuell.beschreibung : "";
        GUI.Label(new Rect(r.x + 460f, r.y + 90f, r.width - 490f, 300f), beschr + "\n\nZiehe mit gedrückter linker Maustaste, um den Gegenstand zu drehen.", textStil);
        if (GUI.Button(new Rect(r.x + 460f, r.yMax - 90f, r.width - 490f, 56f), "Zurück", knopfStil)) ZurListe();
    }
}
