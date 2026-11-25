using System.Collections.Generic;
using UnityEngine;

public class LuftVerkehr : MonoBehaviour
{
    public GameObject kreuzer;
    public GameObject frachter;
    public GameObject luftbus;
    public GameObject drohne;
    [Range(0.2f, 3f)] public float tempo = 1f;

    class Flugbahn
    {
        public float R;
        public float alt;
        public float amp;
        public int kk;
        public float ph;
        public int richtung;
        public float geschwindigkeit;
        public float laenge;
        public int art;
    }

    readonly List<Flugbahn> bahnen = new List<Flugbahn>();
    readonly List<Transform> objekte = new List<Transform>();
    readonly List<int> bahnIndex = new List<int>();
    readonly List<float> anteil = new List<float>();

    void Start()
    {
        StadtRaum.Lesen();
        for (int i = 0; i < 14; i++)
        {
            int m = i % 5;
            var b = new Flugbahn
            {
                R = 230f + 48f * i,
                alt = 300f + 45f * (i % 6),
                amp = 18f + 6f * (i % 4),
                kk = 2 + (i % 3),
                ph = i * 0.9f,
                richtung = i % 2 == 0 ? 1 : -1,
                geschwindigkeit = m == 0 ? 7f : 14f + 3f * (i % 4)
            };
            b.laenge = 2f * Mathf.PI * b.R;
            b.art = m == 0 ? 0 : (m == 1 || m == 3 ? 1 : (m == 2 ? 2 : 3));
            bahnen.Add(b);
            Erzeugen(i, b);
        }
    }

    void Erzeugen(int li, Flugbahn b)
    {
        int anzahl;
        GameObject vorlage;
        if (b.art == 0)
        {
            anzahl = b.R < 700f ? 2 : 3;
            vorlage = kreuzer;
        }
        else if (b.art == 3)
        {
            anzahl = Mathf.Max(3, (int)(b.laenge / 320f));
            vorlage = drohne;
        }
        else
        {
            anzahl = Mathf.Max(3, (int)(b.laenge / (b.art == 1 ? 420f : 480f)));
            vorlage = b.art == 1 ? frachter : luftbus;
        }
        if (vorlage == null) return;
        for (int i = 0; i < anzahl; i++)
        {
            Transform t;
            if (b.art == 3)
            {
                var halter = new GameObject("Schwarm_" + li + "_" + i);
                halter.transform.SetParent(transform, false);
                for (int k = 0; k < 6; k++)
                {
                    var d = Instantiate(vorlage, halter.transform);
                    d.name = "Dr_" + k;
                    d.transform.localPosition = StadtRaum.Vektor(((k % 3) - 1) * 5f, ((k / 3) - 0.5f) * 7f, Mathf.Sin(k * 2.1f) * 3f);
                    d.transform.localRotation = Quaternion.identity;
                }
                t = halter.transform;
            }
            else
            {
                var go = Instantiate(vorlage, transform);
                go.name = "Fl_" + li + "_" + i;
                t = go.transform;
            }
            float f = (i + 0.37f * (li % 3)) / anzahl;
            objekte.Add(t);
            bahnIndex.Add(li);
            anteil.Add(Mathf.Repeat(-f, 1f));
        }
    }

    static Vector3 Punkt(Flugbahn b, float u)
    {
        float th = u * b.richtung;
        float x = StadtRaum.CX + b.R * Mathf.Cos(th);
        float y = StadtRaum.CY + b.R * Mathf.Sin(th);
        float z = b.alt + b.amp * Mathf.Sin(b.kk * th + b.ph);
        return StadtRaum.Pos(x, y, z);
    }

    void Update()
    {
        float dt = Time.deltaTime * tempo;
        for (int i = 0; i < objekte.Count; i++)
        {
            var b = bahnen[bahnIndex[i]];
            float a = Mathf.Repeat(anteil[i] + b.geschwindigkeit * dt / b.laenge, 1f);
            anteil[i] = a;
            float u = a * 2f * Mathf.PI;
            Vector3 p = Punkt(b, u);
            Vector3 q = Punkt(b, u + 0.01f);
            Vector3 vor = q - p;
            if (vor.sqrMagnitude < 1e-6f) vor = StadtRaum.ey;
            objekte[i].SetPositionAndRotation(p, StadtRaum.Drehung(vor.normalized));
        }
    }
}
