using System.Collections.Generic;
using UnityEngine;

public class AufzugSystem : MonoBehaviour
{
    public Transform spieler;
    public KeyCode taste = KeyCode.E;
    public float tempo = 5f;
    public float reichweite = 8f;

    class Aufzug
    {
        public Transform kabine;
        public Vector3 start;
        public float hub;
        public float hoehe;
        public float ziel;
        public bool faehrt;
    }

    readonly List<Aufzug> aufzuege = new List<Aufzug>();
    Aufzug naechster;
    bool naechsterDrin;

    void Start()
    {
        StadtRaum.Lesen();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("Aufzug_Kabine_")) continue;
            int idx;
            if (!int.TryParse(t.name.Substring("Aufzug_Kabine_".Length), out idx)) continue;
            float schiene = idx < 6 ? 40.9f : 26f;
            var a = new Aufzug { kabine = t, start = t.position, hub = schiene + 1.1f - 0.3f };
            KollisionBauen(t);
            aufzuege.Add(a);
        }
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

    static void Box(Transform eltern, string name, float cx, float cy, float cz, float sx, float sy, float sz)
    {
        var go = new GameObject(name);
        go.transform.SetParent(eltern, false);
        go.transform.localPosition = StadtRaum.Vektor(cx, cy, cz);
        var bc = go.AddComponent<BoxCollider>();
        bc.size = StadtRaum.BetragVektor(StadtRaum.ex) * sx + StadtRaum.BetragVektor(StadtRaum.ey) * sy + StadtRaum.BetragVektor(StadtRaum.ez) * sz;
    }

    void KollisionBauen(Transform k)
    {
        Box(k, "Boden", 0f, 0f, 0.15f, 4.8f, 4.8f, 0.3f);
        Box(k, "Rueckwand", -2.3f, 0f, 1.7f, 0.15f, 4.8f, 2.8f);
        Box(k, "SeiteL", 0f, -2.3f, 1.7f, 4.8f, 0.15f, 2.8f);
        Box(k, "SeiteR", 0f, 2.3f, 1.7f, 4.8f, 0.15f, 2.8f);
        Box(k, "Dach", 0f, 0f, 3.2f, 4.8f, 4.8f, 0.3f);
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

    bool Drin(Aufzug a)
    {
        if (spieler == null) return false;
        Vector3 d = spieler.position - a.kabine.position;
        Vector3 vor = a.kabine.TransformDirection(StadtRaum.Vektor(1f, 0f, 0f));
        Vector3 seite = a.kabine.TransformDirection(StadtRaum.Vektor(0f, 1f, 0f));
        float h = Vector3.Dot(d, StadtRaum.ez);
        return Mathf.Abs(Vector3.Dot(d, vor)) < 2.3f && Mathf.Abs(Vector3.Dot(d, seite)) < 2.3f && h > 0.1f && h < 3.2f;
    }

    void Update()
    {
        naechster = null;
        naechsterDrin = false;
        float best = reichweite;
        if (spieler != null)
        {
            foreach (var a in aufzuege)
            {
                Vector3 d = spieler.position - a.start;
                float hor = (d - StadtRaum.ez * Vector3.Dot(d, StadtRaum.ez)).magnitude;
                bool drin = Drin(a);
                if (drin) hor = 0f;
                if (hor < best)
                {
                    best = hor;
                    naechster = a;
                    naechsterDrin = drin;
                }
            }
        }
        if (naechster != null && !naechster.faehrt && Gedrueckt(taste))
        {
            if (naechsterDrin) naechster.ziel = naechster.hoehe < naechster.hub * 0.5f ? naechster.hub : 0f;
            else
            {
                float pl = Vector3.Dot(spieler.position - naechster.start, StadtRaum.ez);
                naechster.ziel = pl > naechster.hub * 0.5f ? naechster.hub : 0f;
            }
            if (Mathf.Abs(naechster.ziel - naechster.hoehe) > 0.05f) naechster.faehrt = true;
        }
        foreach (var a in aufzuege) Bewegen(a);
    }

    void Bewegen(Aufzug a)
    {
        if (!a.faehrt) return;
        bool drin = Drin(a);
        float schritt = tempo * Time.deltaTime;
        float neu = Mathf.MoveTowards(a.hoehe, a.ziel, schritt);
        float delta = neu - a.hoehe;
        a.hoehe = neu;
        a.kabine.position = a.start + StadtRaum.ez * a.hoehe;
        if (drin && spieler != null)
        {
            Vector3 bewegung = StadtRaum.ez * delta;
            var cc = spieler.GetComponent<CharacterController>();
            if (cc != null && cc.enabled) cc.Move(bewegung);
            else spieler.position += bewegung;
        }
        if (Mathf.Abs(a.ziel - a.hoehe) < 0.001f) a.faehrt = false;
    }

    void OnGUI()
    {
        if (naechster == null) return;
        var stil = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        stil.normal.textColor = Color.white;
        string text;
        if (naechster.faehrt) text = "Aufzug faehrt ...";
        else if (naechsterDrin) text = "[" + taste + "] " + (naechster.hoehe < naechster.hub * 0.5f ? "Nach oben fahren" : "Nach unten fahren");
        else text = "[" + taste + "] Aufzug rufen";
        GUI.Box(new Rect(Screen.width / 2 - 200, Screen.height - 180, 400, 50), "");
        GUI.Label(new Rect(Screen.width / 2 - 200, Screen.height - 180, 400, 50), text, stil);
    }
}
