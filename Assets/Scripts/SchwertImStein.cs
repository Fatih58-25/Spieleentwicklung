using System.Collections;
using UnityEngine;

public class SchwertImStein : InteraktionsZone
{
    [Header("Schwert")]
    public GameObject schwertObjekt;
    public Transform spielerHand;
    public GameObject ringAmFinger;
    public float hubHoehe = 0.7f;
    public float ziehDauer = 1.4f;

    [Header("Sitz in der Hand (nach dem Ziehen)")]
    public Vector3 sitzPosition = new Vector3(0.123f, -0.02f, 0.024f);
    public Vector3 sitzRotation = new Vector3(-10.8f, -0.76f, 30.5f);
    public float sitzSkalierung = 0.01678161f;

    [Header("Texte")]
    public string hinweis = "[E] Schwert ziehen";
    public string fehlMeldung = "Das Schwert sitzt fest. Dir fehlt etwas, das dich würdig macht.";
    public string erfolgsMeldung = "Der Ring leuchtet auf. Das Schwert gibt nach.";

    [Header("Ton (optional)")]
    public AudioClip festSound;
    public AudioClip ziehSound;

    bool beschaeftigt;
    bool gezogen;
    Vector3 startPosition;

    void Start()
    {
        if (schwertObjekt != null) startPosition = schwertObjekt.transform.position;
    }

    protected override string HinweisText()
    {
        return hinweis;
    }

    protected override bool KannInteragieren()
    {
        return !gezogen && !beschaeftigt;
    }

    protected override void Interagieren()
    {
        if (schwertObjekt == null)
        {
            Debug.LogWarning("SchwertImStein: Feld Schwert Objekt ist leer.", this);
            return;
        }
        bool amFinger = ringAmFinger != null && ringAmFinger.activeInHierarchy;
        Debug.Log("SchwertImStein: E gedrueckt. RingTraeger vorhanden: " + (RingTraeger.Instanz != null) + ", Traegt Ring: " + RingTraeger.HatRing + ", Ring am Finger aktiv: " + amFinger, this);
        if (RingTraeger.HatRing || amFinger) StartCoroutine(Ziehen());
        else StartCoroutine(Verweigern());
    }

    IEnumerator Verweigern()
    {
        beschaeftigt = true;
        Meldung(Inventar.Hat("Ring") ? "Du trägst den Ring noch nicht. Öffne mit I dein Inventar und ziehe ihn an." : fehlMeldung, 3.5f);
        if (festSound != null) AudioSource.PlayClipAtPoint(festSound, schwertObjekt.transform.position);
        var t = schwertObjekt.transform;
        float zeit = 0f;
        while (zeit < 0.45f)
        {
            zeit += Time.deltaTime;
            float ausschlag = Mathf.Sin(zeit * 70f) * 0.012f * (1f - zeit / 0.45f);
            t.position = startPosition + new Vector3(ausschlag, 0f, ausschlag * 0.5f);
            yield return null;
        }
        t.position = startPosition;
        beschaeftigt = false;
    }

    IEnumerator Ziehen()
    {
        beschaeftigt = true;
        Meldung(erfolgsMeldung, 3f);
        if (ziehSound != null) AudioSource.PlayClipAtPoint(ziehSound, schwertObjekt.transform.position);
        var faust = HandFaust.Holen(spielerTag);
        if (faust != null) faust.Schliessen(0.6f);
        var t = schwertObjekt.transform;
        Vector3 von = startPosition;
        Vector3 nach = von + Vector3.up * hubHoehe;
        float zeit = 0f;
        while (zeit < ziehDauer)
        {
            zeit += Time.deltaTime;
            float p = Mathf.Clamp01(zeit / ziehDauer);
            float weich = p * p * (3f - 2f * p);
            float zittern = Mathf.Sin(zeit * 55f) * 0.006f * (1f - p);
            t.position = Vector3.Lerp(von, nach, weich) + new Vector3(zittern, 0f, zittern);
            yield return null;
        }
        t.position = nach;
        gezogen = true;
        beschaeftigt = false;
        var hand = spielerHand != null ? spielerHand : HandSuchen();
        if (hand != null)
        {
            t.SetParent(hand);
            t.localPosition = sitzPosition;
            t.localRotation = Quaternion.Euler(sitzRotation);
            t.localScale = Vector3.one * sitzSkalierung;
        }
        foreach (var rb in schwertObjekt.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
        foreach (var c in schwertObjekt.GetComponentsInChildren<Collider>()) c.enabled = false;
        Beenden();
    }

    Transform HandSuchen()
    {
        var spieler = GameObject.FindGameObjectWithTag(spielerTag);
        if (spieler == null) return null;
        foreach (var t in spieler.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.ToLowerInvariant() == "playerhand") return t;
        }
        foreach (var t in spieler.GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("hand") && (n.Contains("right") || n.EndsWith("_r") || n.EndsWith(".r") || n.Contains("rechts"))) return t;
        }
        return null;
    }
}
