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
    public float dauer = 3.5f;
    public Collider[] sperrenNachOeffnen;

    [Header("Texte")]
    public string hinweis = "[E] Tor öffnen";
    public string verschlossenMeldung = "Das Tor ist verschlossen. Du brauchst einen Schlüssel.";
    public string offenMeldung = "Der Schlüssel dreht sich. Das Tor öffnet sich.";

    [Header("Ton (optional)")]
    public AudioClip verschlossenSound;
    public AudioClip oeffnenSound;

    bool offen;
    bool beschaeftigt;

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

    IEnumerator Oeffnen()
    {
        beschaeftigt = true;
        Meldung(offenMeldung, 3f);
        if (oeffnenSound != null) AudioSource.PlayClipAtPoint(oeffnenSound, transform.position);

        int n = fluegel.Length;
        var start = new Vector3[n];
        var startRot = new Quaternion[n];
        var richtung = new Vector3[n];
        var scharnier = new Vector3[n];
        var weg = new float[n];
        var vorzeichen = new float[n];

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
            startRot[i] = fluegel[i].rotation;
        }

        float zeit = 0f;
        float richtungsFaktor = umkehren ? -1f : 1f;
        while (zeit < dauer)
        {
            zeit += Time.deltaTime;
            float p = Mathf.Clamp01(zeit / dauer);
            float weich = p * p * (3f - 2f * p);
            for (int i = 0; i < n; i++)
            {
                if (schieben)
                {
                    fluegel[i].position = start[i] + richtung[i] * weg[i] * weich;
                }
                else
                {
                    float w = winkel * weich * vorzeichen[i] * richtungsFaktor;
                    var q = Quaternion.AngleAxis(w, Vector3.up);
                    fluegel[i].position = scharnier[i] + q * (start[i] - scharnier[i]);
                    fluegel[i].rotation = q * startRot[i];
                }
            }
            yield return null;
        }

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
