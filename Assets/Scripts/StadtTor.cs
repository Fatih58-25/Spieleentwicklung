using System.Collections;
using UnityEngine;

public class StadtTor : InteraktionsZone
{
    [Header("Tor")]
    public Transform[] fluegel;
    public string benoetigterSchluessel = "Schluessel_A";
    public bool schieben;
    public float winkel = 100f;
    public bool umkehren;
    public float dauer = 7f;
    public Collider[] sperrenNachOeffnen;
    public StadtTor[] mitOeffnen;

    [Header("Rumpeln")]
    public float ruetteln = 0.03f;
    public float ruettelReichweite = 80f;

    [Header("Texte")]
    public string hinweis = "[E] Tor öffnen";
    public string verschlossenMeldung = "Das Tor ist verschlossen. Du brauchst einen Schlüssel.";
    public string offenMeldung = "Der Schlüssel dreht sich. Das Tor öffnet sich.";

    [Header("Ton (optional)")]
    public AudioClip verschlossenSound;
    public AudioClip oeffnenSound;

    bool offen;
    bool beschaeftigt;
    bool vorbereitet;
    Vector3[] start;
    Quaternion[] startRot;
    Vector3[] richtung;
    Vector3[] scharnier;
    Vector3[] startMitte;
    float[] weg;
    float[] vorzeichen;

    protected override string HinweisText()
    {
        return hinweis;
    }

    protected override bool KannInteragieren()
    {
        return !offen && !beschaeftigt;
    }

    protected override void Interagieren()
    {
        if (fluegel == null || fluegel.Length == 0)
        {
            Debug.LogWarning("StadtTor: Es sind keine Torflügel eingetragen.", this);
            return;
        }
        if (!string.IsNullOrEmpty(benoetigterSchluessel) && !SpielStand.Hat(benoetigterSchluessel))
        {
            Meldung(verschlossenMeldung, 3f);
            if (verschlossenSound != null) AudioSource.PlayClipAtPoint(verschlossenSound, transform.position);
            return;
        }
        Meldung(offenMeldung, 3f);
        Starten();
        if (mitOeffnen != null)
        {
            foreach (var t in mitOeffnen)
            {
                if (t != null && t != this) t.Starten();
            }
        }
    }

    public void Starten()
    {
        if (offen || beschaeftigt) return;
        StartCoroutine(Oeffnen());
    }

    static Bounds FluegelGrenzen(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
        var cs = t.GetComponentsInChildren<Collider>();
        if (cs.Length > 0)
        {
            var b = cs[0].bounds;
            foreach (var c in cs) b.Encapsulate(c.bounds);
            return b;
        }
        return new Bounds(t.position, Vector3.one);
    }

    public void Vorbereiten()
    {
        if (vorbereitet || fluegel == null || fluegel.Length == 0) return;
        int n = fluegel.Length;
        start = new Vector3[n];
        startRot = new Quaternion[n];
        richtung = new Vector3[n];
        scharnier = new Vector3[n];
        startMitte = new Vector3[n];
        weg = new float[n];
        vorzeichen = new float[n];
        Vector3 mitte = Vector3.zero;
        var grenzen = new Bounds[n];
        for (int i = 0; i < n; i++)
        {
            grenzen[i] = FluegelGrenzen(fluegel[i]);
            mitte += grenzen[i].center;
        }
        mitte /= n;
        Vector3 bezug = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            Vector3 d = grenzen[i].center - mitte;
            d.y = 0f;
            if (n == 1 || d.sqrMagnitude < 0.0001f)
            {
                d = fluegel[i].right;
                d.y = 0f;
            }
            d.Normalize();
            richtung[i] = d;
            if (i == 0) bezug = d;
            vorzeichen[i] = Vector3.Dot(d, bezug) >= 0f ? 1f : -1f;
            if (n == 1) vorzeichen[i] = 1f;
            Vector3 ext = grenzen[i].extents;
            float halbe = Mathf.Abs(d.x) * ext.x + Mathf.Abs(d.z) * ext.z;
            weg[i] = halbe * 2f;
            scharnier[i] = grenzen[i].center + d * halbe;
            scharnier[i].y = grenzen[i].center.y;
            start[i] = fluegel[i].position;
            startMitte[i] = grenzen[i].center;
            startRot[i] = fluegel[i].rotation;
        }
        vorbereitet = true;
    }

    public void PoseSetzen(float p)
    {
        Vorbereiten();
        if (!vorbereitet) return;
        float weich = p * p * (3f - 2f * p);
        float faktor = umkehren ? -1f : 1f;
        for (int i = 0; i < fluegel.Length; i++)
        {
            if (schieben)
            {
                fluegel[i].position = start[i] + richtung[i] * weg[i] * weich;
            }
            else
            {
                float w = winkel * weich * vorzeichen[i] * faktor;
                var q = Quaternion.AngleAxis(w, Vector3.up);
                fluegel[i].position = scharnier[i] + q * (start[i] - scharnier[i]);
                fluegel[i].rotation = q * startRot[i];
            }
        }
    }

    public void NachAussenOeffnen(Vector3 stadtMitte)
    {
        Vorbereiten();
        if (!vorbereitet || schieben) return;
        Vector3 torMitte = Vector3.zero;
        for (int i = 0; i < fluegel.Length; i++) torMitte += startMitte[i];
        torMitte /= fluegel.Length;
        Vector3 nachAussen = torMitte - stadtMitte;
        nachAussen.y = 0f;
        Vector3 bewegung = Vector3.zero;
        for (int i = 0; i < fluegel.Length; i++)
        {
            var q = Quaternion.AngleAxis(winkel * vorzeichen[i], Vector3.up);
            Vector3 neu = scharnier[i] + q * (startMitte[i] - scharnier[i]);
            bewegung += neu - startMitte[i];
        }
        umkehren = Vector3.Dot(bewegung, nachAussen) < 0f;
    }

    IEnumerator Oeffnen()
    {
        beschaeftigt = true;
        if (oeffnenSound != null) AudioSource.PlayClipAtPoint(oeffnenSound, transform.position);
        Vorbereiten();
        TPSCamera kamera = null;
        if (ruetteln > 0f) kamera = FindFirstObjectByType<TPSCamera>();
        float zeit = 0f;
        while (zeit < dauer)
        {
            zeit += Time.deltaTime;
            PoseSetzen(Mathf.Clamp01(zeit / dauer));
            if (kamera != null && Vector3.Distance(kamera.transform.position, transform.position) < ruettelReichweite) kamera.AddShake(ruetteln);
            yield return null;
        }
        PoseSetzen(1f);
        offen = true;
        beschaeftigt = false;
        if (sperrenNachOeffnen != null)
        {
            foreach (var c in sperrenNachOeffnen)
            {
                if (c != null) c.enabled = false;
            }
        }
        Beenden();
    }
}
