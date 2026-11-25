using System.Collections.Generic;
using UnityEngine;

public class ZugSystem : MonoBehaviour
{
    public GameObject zugVorlage;
    public Transform spieler;
    public KeyCode knopfTaste = KeyCode.E;
    public KeyCode zielTaste = KeyCode.Q;
    public float hoechstTempo = 25f;
    public float beschleunigung = 4f;
    public float haltezeit = 20f;
    public float knopfReichweite = 3.5f;

    class Linie
    {
        public string name;
        public bool ring;
        public float th;
        public float seite;
        public float z;
        public float r0;
        public float r1;
        public float rc;
        public float rho;
        public float laenge;
        public float[] halte;
        public string[] haltName;
    }

    class Zug
    {
        public Linie linie;
        public Transform wurzel;
        public Transform knopf;
        public Transform raum;
        public GameObject waendeLinks;
        public GameObject waendeRechts;
        public float u;
        public float tempo;
        public int haltIndex;
        public int zielIndex;
        public int richtung = 1;
        public float wartezeit;
        public bool faehrt;
        public bool spielerDrin;
        public Vector3 vorwaerts = Vector3.forward;
        public Vector3 rechts = Vector3.right;
        public float lf;
        public float lr;
        public float lh;
    }

    readonly List<Linie> linien = new List<Linie>();
    readonly List<Zug> zuege = new List<Zug>();
    Zug aktuellerZug;
    string meldung = "";
    float meldungBis;

    void Start()
    {
        StadtRaum.Lesen();
        LinienBauen();
        foreach (var l in linien) ZugErzeugen(l);
        BahnsteigeBauen();
        TreppenColliderBauen();
        if (spieler == null)
        {
            try
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) spieler = p.transform;
            }
            catch { }
        }
        if (spieler == null && Camera.main != null) spieler = Camera.main.transform;
    }

    void LinienBauen()
    {
        float rEnde = StadtRaum.RR0 + StadtRaum.DR * StadtRaum.NB - 60f;
        var l1 = new Linie { name = "Linie 1", ring = false, th = StadtRaum.TH0 + Mathf.PI / 6f, seite = -2.2f, z = 40.9f, r0 = 275f, r1 = rEnde };
        l1.halte = new[] { 300f - 275f, 560f - 275f, 790f - 275f };
        l1.haltName = new[] { "Zentrum-Nord", "Mittelring", "Aussenring" };
        l1.laenge = l1.r1 - l1.r0;
        var l2 = new Linie { name = "Linie 2", ring = false, th = StadtRaum.TH0 + 7f * Mathf.PI / 6f, seite = -2.2f, z = 40.9f, r0 = 275f, r1 = rEnde };
        l2.halte = l1.halte;
        l2.haltName = new[] { "Zentrum-Sued", "Turmwald", "Hafenkante" };
        l2.laenge = l2.r1 - l2.r0;
        var l3 = new Linie { name = "Ringlinie", ring = true, rc = StadtRaum.RR0 + StadtRaum.DR * 4f, rho = -4f, z = 26f };
        float R = l3.rc + l3.rho;
        l3.laenge = 2f * Mathf.PI * R;
        l3.halte = new[] { Mathf.Deg2Rad * 90f * R, Mathf.Deg2Rad * 210f * R, Mathf.Deg2Rad * 330f * R };
        l3.haltName = new[] { "Ring Nord", "Ring West", "Ring Ost" };
        linien.Add(l1);
        linien.Add(l2);
        linien.Add(l3);
    }

    void BahnsteigeBauen()
    {
        var wurzel = new GameObject("Bahnsteig_Collider");
        wurzel.transform.SetParent(transform, false);
        foreach (var l in linien)
        {
            foreach (float u in l.halte)
            {
                Vector3 pos, vor;
                Pose(l, u, out pos, out vor);
                float hd = l.ring ? (u / (l.rc + l.rho)) + Mathf.PI / 2f : l.th;
                Vector3 n = StadtRaum.Vektor(-Mathf.Sin(hd), Mathf.Cos(hd), 0f);
                float bp = l.ring ? -5.95f : 5.9f;
                var go = new GameObject("Bahnsteig");
                go.transform.SetParent(wurzel.transform, false);
                go.transform.SetPositionAndRotation(pos + n * bp + StadtRaum.ez * 0.55f, StadtRaum.Drehung(vor.normalized));
                var bc = go.AddComponent<BoxCollider>();
                bc.size = StadtRaum.BetragVektor(StadtRaum.ex) * 5.8f + StadtRaum.BetragVektor(StadtRaum.ey) * 72.1f + StadtRaum.BetragVektor(StadtRaum.ez) * 1.1f;
            }
        }
    }

    void TreppenColliderBauen()
    {
        foreach (var mf in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (!mf.name.StartsWith("Aufzug_Bruecke_")) continue;
            if (mf.GetComponent<MeshCollider>() == null) mf.gameObject.AddComponent<MeshCollider>();
        }
    }

    void Pose(Linie l, float u, out Vector3 pos, out Vector3 vor)
    {
        if (l.ring)
        {
            float R = l.rc + l.rho;
            float a = u / R;
            pos = StadtRaum.Pos(StadtRaum.CX + R * Mathf.Cos(a), StadtRaum.CY + R * Mathf.Sin(a), l.z);
            float h = a + Mathf.PI / 2f;
            vor = StadtRaum.Vektor(Mathf.Cos(h), Mathf.Sin(h), 0f);
            return;
        }
        float r = l.r0 + u;
        float ct = Mathf.Cos(l.th);
        float st = Mathf.Sin(l.th);
        pos = StadtRaum.Pos(StadtRaum.CX + r * ct - l.seite * st, StadtRaum.CY + r * st + l.seite * ct, l.z);
        vor = StadtRaum.Vektor(ct, st, 0f);
    }

    void ZugErzeugen(Linie l)
    {
        if (zugVorlage == null) return;
        var go = Instantiate(zugVorlage, transform);
        go.name = "Zug_" + l.name;
        var z = new Zug { linie = l, wurzel = go.transform, haltIndex = 0, zielIndex = 0, u = Wrap(l, l.halte[0]) };
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Zugknopf")) z.knopf = t;
            if (t.name.StartsWith("Fahrgastraum")) z.raum = t;
        }
        if (z.raum != null)
        {
            foreach (var mr in z.raum.GetComponentsInChildren<MeshRenderer>(true)) mr.enabled = false;
            foreach (var c in z.raum.GetComponentsInChildren<Collider>(true)) Destroy(c);
        }
        BodenBauen(z);
        z.wartezeit = haltezeit * 0.5f;
        zuege.Add(z);
        Setzen(z);
    }

    void BodenBauen(Zug z)
    {
        var boden = new GameObject("Boden");
        boden.transform.SetParent(z.wurzel, false);
        boden.transform.localPosition = StadtRaum.Vektor(0f, 0f, 1.0f);
        var bc = boden.AddComponent<BoxCollider>();
        bc.size = StadtRaum.BetragVektor(StadtRaum.ex) * 5.6f + StadtRaum.BetragVektor(StadtRaum.ey) * 58f + StadtRaum.BetragVektor(StadtRaum.ez) * 0.3f;
        z.waendeLinks = WandBauen(z, -1);
        z.waendeRechts = WandBauen(z, 1);
        WandBauen(z, 0);
    }

    GameObject WandBauen(Zug z, int seite)
    {
        var w = new GameObject(seite == 0 ? "Stirnwaende" : "Wand");
        w.transform.SetParent(z.wurzel, false);
        if (seite == 0)
        {
            foreach (int sg in new[] { -1, 1 })
            {
                var e = new GameObject("Stirn");
                e.transform.SetParent(w.transform, false);
                e.transform.localPosition = StadtRaum.Vektor(0f, sg * 29.5f, 2.8f);
                var bce = e.AddComponent<BoxCollider>();
                bce.size = StadtRaum.BetragVektor(StadtRaum.ex) * 5.8f + StadtRaum.BetragVektor(StadtRaum.ey) * 0.3f + StadtRaum.BetragVektor(StadtRaum.ez) * 3.2f;
            }
            return w;
        }
        w.transform.localPosition = StadtRaum.Vektor(seite * 2.9f, 0f, 2.8f);
        var bc = w.AddComponent<BoxCollider>();
        bc.size = StadtRaum.BetragVektor(StadtRaum.ex) * 0.2f + StadtRaum.BetragVektor(StadtRaum.ey) * 58f + StadtRaum.BetragVektor(StadtRaum.ez) * 3.2f;
        return w;
    }

    float Wrap(Linie l, float u)
    {
        if (l.ring) return Mathf.Repeat(u, l.laenge);
        return Mathf.Clamp(u, 0f, l.laenge);
    }

    float Restweg(Zug z, float ziel)
    {
        var l = z.linie;
        if (l.ring) return Mathf.Repeat(ziel - z.u, l.laenge);
        return Mathf.Abs(ziel - z.u);
    }

    void Setzen(Zug z)
    {
        Vector3 pos, vor;
        Pose(z.linie, z.u, out pos, out vor);
        if (!z.linie.ring && z.richtung < 0) vor = -vor;
        z.vorwaerts = vor.normalized;
        z.rechts = Vector3.Cross(StadtRaum.ez, z.vorwaerts).normalized;
        z.wurzel.SetPositionAndRotation(pos, StadtRaum.Drehung(z.vorwaerts));
    }

    bool SpielerDrin(Zug z)
    {
        if (spieler == null) return false;
        Vector3 d = spieler.position - z.wurzel.position;
        float lf = Vector3.Dot(d, z.vorwaerts);
        float lr = Vector3.Dot(d, z.rechts);
        float lh = Vector3.Dot(d, StadtRaum.ez.normalized);
        return Mathf.Abs(lf) < 29f && Mathf.Abs(lr) < 2.7f && lh > 0.7f && lh < 5.2f;
    }

    void Update()
    {
        Zug naechster = null;
        foreach (var z in zuege)
        {
            bool drin = SpielerDrin(z);
            z.spielerDrin = drin;
            if (drin) naechster = z;
        }
        aktuellerZug = naechster;
        foreach (var z in zuege) Fahren(z);
        if (aktuellerZug != null) Bedienung(aktuellerZug);
    }

    static bool Gedrueckt(KeyCode k)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;
        UnityEngine.InputSystem.Key key;
        if (!System.Enum.TryParse(k.ToString(), out key)) return false;
        return kb[key].wasPressedThisFrame;
#else
        return Input.GetKeyDown(k);
#endif
    }

    void Bedienung(Zug z)
    {
        var l = z.linie;
        if (!z.faehrt)
        {
            if (Gedrueckt(zielTaste))
            {
                z.zielIndex = (z.zielIndex + 1) % l.halte.Length;
                if (z.zielIndex == z.haltIndex) z.zielIndex = (z.zielIndex + 1) % l.halte.Length;
            }
            if (z.zielIndex == z.haltIndex) z.zielIndex = (z.haltIndex + 1) % l.halte.Length;
            bool nah = true;
            if (z.knopf != null) nah = Vector3.Distance(spieler.position, z.knopf.position) < knopfReichweite;
            if (nah && Gedrueckt(knopfTaste))
            {
                Losfahren(z, z.zielIndex);
            }
        }
    }

    void Losfahren(Zug z, int ziel)
    {
        var l = z.linie;
        z.zielIndex = ziel;
        if (!l.ring)
        {
            z.richtung = l.halte[ziel] >= z.u ? 1 : -1;
        }
        z.faehrt = true;
        z.wartezeit = 0f;
        Meldung("Abfahrt nach " + l.haltName[ziel]);
    }

    void Fahren(Zug z)
    {
        var l = z.linie;
        float dt = Time.deltaTime;
        Vector3 altPos = z.wurzel.position;
        Vector3 altVor = z.vorwaerts;
        Vector3 altRechts = z.rechts;
        float lf = 0f, lr = 0f, lh = 0f;
        if (z.spielerDrin && spieler != null)
        {
            Vector3 d = spieler.position - altPos;
            lf = Vector3.Dot(d, altVor);
            lr = Vector3.Dot(d, altRechts);
            lh = Vector3.Dot(d, StadtRaum.ez.normalized);
        }
        if (!z.faehrt)
        {
            z.tempo = 0f;
            if (z.waendeLinks != null) z.waendeLinks.SetActive(false);
            if (z.waendeRechts != null) z.waendeRechts.SetActive(false);
            if (!z.spielerDrin)
            {
                z.wartezeit += dt;
                if (z.wartezeit >= haltezeit)
                {
                    int ziel = NaechstesZiel(z);
                    Losfahren(z, ziel);
                }
            }
            return;
        }
        if (z.waendeLinks != null) z.waendeLinks.SetActive(true);
        if (z.waendeRechts != null) z.waendeRechts.SetActive(true);
        float zielU = l.halte[z.zielIndex];
        float rest = Restweg(z, zielU);
        float soll = Mathf.Min(hoechstTempo, Mathf.Sqrt(2f * beschleunigung * Mathf.Max(0f, rest)) + 0.5f);
        z.tempo = Mathf.MoveTowards(z.tempo, soll, beschleunigung * dt);
        float schritt = z.tempo * dt;
        if (rest <= schritt + 0.02f)
        {
            z.u = zielU;
            z.faehrt = false;
            z.haltIndex = z.zielIndex;
            z.tempo = 0f;
            z.wartezeit = 0f;
            Setzen(z);
            Mitnehmen(z, altPos, altVor, altRechts, lf, lr, lh);
            if (z.spielerDrin) Meldung("Angekommen: " + l.haltName[z.haltIndex]);
            return;
        }
        if (l.ring) z.u = Mathf.Repeat(z.u + schritt, l.laenge);
        else z.u += schritt * z.richtung;
        Setzen(z);
        Mitnehmen(z, altPos, altVor, altRechts, lf, lr, lh);
    }

    void Mitnehmen(Zug z, Vector3 altPos, Vector3 altVor, Vector3 altRechts, float lf, float lr, float lh)
    {
        if (!z.spielerDrin || spieler == null) return;
        Vector3 neu = z.wurzel.position + z.vorwaerts * lf + z.rechts * lr + StadtRaum.ez.normalized * lh;
        Vector3 delta = neu - spieler.position;
        var cc = spieler.GetComponent<CharacterController>();
        if (cc != null && cc.enabled) cc.Move(delta);
        else spieler.position = neu;
        float winkel = Vector3.SignedAngle(altVor, z.vorwaerts, StadtRaum.ez);
        if (Mathf.Abs(winkel) > 0.0001f) spieler.Rotate(StadtRaum.ez, winkel, Space.World);
    }

    int NaechstesZiel(Zug z)
    {
        var l = z.linie;
        if (l.ring) return (z.haltIndex + 1) % l.halte.Length;
        if (z.haltIndex == 0) return 1;
        if (z.haltIndex == l.halte.Length - 1) return l.halte.Length - 2;
        return z.richtung >= 0 ? z.haltIndex + 1 : z.haltIndex - 1;
    }

    void Meldung(string text)
    {
        meldung = text;
        meldungBis = Time.time + 4f;
    }

    void OnGUI()
    {
        var stil = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        stil.normal.textColor = Color.white;
        if (Time.time < meldungBis)
        {
            GUI.Label(new Rect(0, 40, Screen.width, 40), meldung, stil);
        }
        if (aktuellerZug == null) return;
        var z = aktuellerZug;
        var l = z.linie;
        string text;
        if (z.faehrt)
        {
            text = l.name + "  -  Fahrt nach " + l.haltName[z.zielIndex] + "  (" + Mathf.RoundToInt(z.tempo * 3.6f) + " km/h)";
        }
        else
        {
            text = l.name + "  -  Halt: " + l.haltName[z.haltIndex] + "\nZiel: " + l.haltName[z.zielIndex] + "   [" + zielTaste + "] Ziel wechseln   [" + knopfTaste + "] Knopf druecken";
        }
        GUI.Box(new Rect(Screen.width / 2 - 260, Screen.height - 120, 520, 80), "");
        GUI.Label(new Rect(Screen.width / 2 - 260, Screen.height - 120, 520, 80), text, stil);
    }
}
