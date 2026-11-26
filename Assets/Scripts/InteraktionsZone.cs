using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class InteraktionsZone : MonoBehaviour
{
    [Header("Hinweis")]
    public TextMeshProUGUI promptText;
    public CanvasGroup promptGruppe;
    public string spielerTag = "Player";

    public static bool GlobalGesperrt;

    static readonly List<InteraktionsZone> inReichweite = new List<InteraktionsZone>();
    static Transform spielerTransform;

    protected bool imBereich;
    string meldung;
    float meldungBis;
    GUIStyle stil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zuruecksetzen()
    {
        inReichweite.Clear();
        spielerTransform = null;
        GlobalGesperrt = false;
    }

    protected abstract string HinweisText();
    protected abstract void Interagieren();

    protected virtual bool KannInteragieren()
    {
        return true;
    }

    protected void Meldung(string text, float sekunden)
    {
        meldung = text;
        meldungBis = Time.unscaledTime + sekunden;
    }

    protected void Beenden()
    {
        imBereich = false;
        inReichweite.Remove(this);
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    bool IstNaechste()
    {
        if (inReichweite.Count <= 1) return true;
        var cam = Camera.main;
        InteraktionsZone beste = null;
        float wert = float.MaxValue;
        foreach (var z in inReichweite)
        {
            if (z == null) continue;
            float w;
            if (cam != null) w = Vector3.Angle(cam.transform.forward, z.transform.position - cam.transform.position);
            else if (spielerTransform != null) w = (z.transform.position - spielerTransform.position).sqrMagnitude;
            else w = 0f;
            if (w < wert)
            {
                wert = w;
                beste = z;
            }
        }
        return beste == this;
    }

    protected virtual float MindestReichweite
    {
        get { return 0f; }
    }

    protected virtual void Awake()
    {
        float m = MindestReichweite;
        if (m <= 0f) return;
        var sc = GetComponent<SphereCollider>();
        if (sc == null || !sc.isTrigger) return;
        var ls = transform.lossyScale;
        float s = Mathf.Max(ls.x, Mathf.Max(ls.y, ls.z));
        if (s <= 0.0001f) return;
        if (sc.radius * s < m) sc.radius = m / s;
    }

    protected virtual void Update()
    {
        if (GlobalGesperrt) return;
        if (!imBereich) return;
        if (!KannInteragieren()) return;
        if (!IstNaechste()) return;
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) Interagieren();
    }

    void LateUpdate()
    {
        if (promptText == null && promptGruppe == null) return;
        string text = AktuellerText();
        if (promptText != null && text != null) promptText.text = text;
        if (promptGruppe != null)
        {
            promptGruppe.alpha = text != null ? 1f : 0f;
            promptGruppe.blocksRaycasts = text != null;
        }
    }

    protected string AktuellerText()
    {
        if (Time.unscaledTime < meldungBis) return meldung;
        if (GlobalGesperrt) return null;
        if (!imBereich) return null;
        if (!KannInteragieren()) return null;
        if (!IstNaechste()) return null;
        return HinweisText();
    }

    protected virtual void OnGUI()
    {
        if (promptText != null || promptGruppe != null) return;
        string text = AktuellerText();
        if (string.IsNullOrEmpty(text)) return;
        if (stil == null)
        {
            stil = new GUIStyle(GUI.skin.label);
            stil.alignment = TextAnchor.MiddleCenter;
            stil.fontSize = 24;
            stil.fontStyle = FontStyle.Bold;
            stil.wordWrap = true;
        }
        var r = new Rect(Screen.width * 0.5f - 450f, Screen.height * 0.72f, 900f, 90f);
        var schatten = new Rect(r.x + 2f, r.y + 2f, r.width, r.height);
        stil.normal.textColor = Color.black;
        GUI.Label(schatten, text, stil);
        stil.normal.textColor = Color.white;
        GUI.Label(r, text, stil);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(spielerTag)) return;
        spielerTransform = other.transform;
        imBereich = true;
        if (!inReichweite.Contains(this)) inReichweite.Add(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(spielerTag)) return;
        imBereich = false;
        inReichweite.Remove(this);
    }

    protected virtual void OnDisable()
    {
        imBereich = false;
        inReichweite.Remove(this);
    }
}
