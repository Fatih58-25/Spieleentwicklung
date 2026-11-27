using System.Collections.Generic;
using UnityEngine;

public class FahrzeugVerkehr : MonoBehaviour
{
    public GameObject transporter;
    public GameObject muldenkipper;
    public GameObject raupe;
    [Range(0.1f, 2f)] public float dichte = 0.8f;
    [Range(0.2f, 3f)] public float tempo = 1f;
    public int zufallswert = 7;
    public bool raddrehungUmkehren = false;
    public bool kollision = true;

    const float CX = StadtRaum.CX;
    const float CY = StadtRaum.CY;
    const float RR0 = StadtRaum.RR0;
    const float DR = StadtRaum.DR;
    const int NB = StadtRaum.NB;
    const float TH0 = StadtRaum.TH0;

    class Bahn
    {
        public bool ring;
        public float th;
        public float a;
        public float r0;
        public float r1;
        public float rc;
        public float rho;
        public float laenge;
        public float tempo;
    }

    Vector3 c;
    Vector3 ex;
    Vector3 ey;
    Vector3 ez;
    readonly List<Bahn> bahnen = new List<Bahn>();
    readonly List<Transform> fahrzeuge = new List<Transform>();
    readonly List<int> bahnIndex = new List<int>();
    readonly List<float> weg = new List<float>();
    readonly List<float> strecke = new List<float>();
    readonly List<Transform[]> raeder = new List<Transform[]>();
    readonly List<float[]> radien = new List<float[]>();
    readonly List<Quaternion[]> startRot = new List<Quaternion[]>();
    Quaternion modellKorrektur;
    float drehVorzeichen = 1f;

    void Start()
    {
        StadtRaum.Lesen();
        c = StadtRaum.c;
        ex = StadtRaum.ex;
        ey = StadtRaum.ey;
        ez = StadtRaum.ez;
        modellKorrektur = StadtRaum.modellKorrektur;
        float haendig = Vector3.Dot(Vector3.Cross(ex, ey), ez);
        drehVorzeichen = (haendig < 0f ? 1f : -1f) * (raddrehungUmkehren ? -1f : 1f);
        BahnenBauen();
        FahrzeugeErzeugen();
    }

    static float Laenge(Bahn b)
    {
        if (b.ring) return 2f * Mathf.PI * (b.rc + b.rho);
        return 2f * (b.r1 - b.r0) + 2f * Mathf.PI * b.a;
    }

    void BahnenBauen()
    {
        var rnd = new System.Random(zufallswert);
        float[] tAv = { 8f, 10f, 12f };
        float[] tRing = { 10f, 12f, 14f };
        float rEnde = RR0 + DR * NB - 30f;
        for (int m = 0; m < 12; m++)
        {
            float th = TH0 + m * Mathf.PI / 6f;
            foreach (float a in new[] { 8f, 15f })
            {
                var b = new Bahn { ring = false, th = th, a = a, r0 = 262f, r1 = rEnde, tempo = tAv[rnd.Next(3)] };
                b.laenge = Laenge(b);
                bahnen.Add(b);
            }
        }
        for (int k = 0; k <= NB; k++)
        {
            foreach (float rho in new[] { -7f, -3.2f, 3.2f, 7f })
            {
                var b = new Bahn { ring = true, rc = RR0 + DR * k, rho = rho, tempo = tRing[rnd.Next(3)] };
                b.laenge = Laenge(b);
                bahnen.Add(b);
            }
        }
    }

    void RaederSammeln(GameObject go)
    {
        var liste = new List<Transform>();
        var rad = new List<float>();
        var rot = new List<Quaternion>();
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Rad_r")) continue;
            var teile = t.name.Split('_');
            float r = 100f;
            if (teile.Length > 1 && teile[1].Length > 1)
            {
                float.TryParse(teile[1].Substring(1), out r);
            }
            liste.Add(t);
            rad.Add(Mathf.Max(0.1f, r / 100f));
            rot.Add(t.localRotation);
        }
        raeder.Add(liste.ToArray());
        radien.Add(rad.ToArray());
        startRot.Add(rot.ToArray());
    }

    void FahrzeugeErzeugen()
    {
        var rnd = new System.Random(zufallswert + 1);
        for (int i = 0; i < bahnen.Count; i++)
        {
            var b = bahnen[i];
            float abstand = b.ring ? 420f : 260f;
            int anzahl = Mathf.Max(1, Mathf.RoundToInt(b.laenge / abstand * dichte));
            float phase = (float)rnd.NextDouble();
            for (int k = 0; k < anzahl; k++)
            {
                double r = rnd.NextDouble();
                GameObject vorlage = transporter;
                if (!b.ring && b.a > 12f && r < 0.18) vorlage = raupe;
                else if (r > 0.6) vorlage = muldenkipper;
                if (vorlage == null) continue;
                var go = Instantiate(vorlage, transform);
                go.name = "Fz_" + i + "_" + k;
                if (kollision) StadtKollision.BoxHinzufuegen(go);
                fahrzeuge.Add(go.transform);
                bahnIndex.Add(i);
                weg.Add(((k + phase) / anzahl) * b.laenge);
                strecke.Add(0f);
                RaederSammeln(go);
            }
        }
    }

    void Pose(Bahn b, float u, out float x, out float y, out float heading)
    {
        if (b.ring)
        {
            float R = b.rc + b.rho;
            if (b.rho > 0f)
            {
                float ang = u / R;
                x = CX + R * Mathf.Cos(ang);
                y = CY + R * Mathf.Sin(ang);
                heading = ang + Mathf.PI / 2f;
            }
            else
            {
                float ang = -u / R;
                x = CX + R * Mathf.Cos(ang);
                y = CY + R * Mathf.Sin(ang);
                heading = ang - Mathf.PI / 2f;
            }
            return;
        }
        float s1 = b.r1 - b.r0;
        float s2 = Mathf.PI * b.a;
        float r, s, h;
        if (u < s1)
        {
            r = b.r0 + u;
            s = -b.a;
            h = b.th;
        }
        else if (u < s1 + s2)
        {
            float t = (u - s1) / b.a;
            r = b.r1 + b.a * Mathf.Sin(t);
            s = -b.a * Mathf.Cos(t);
            h = b.th + t;
        }
        else if (u < 2f * s1 + s2)
        {
            r = b.r1 - (u - s1 - s2);
            s = b.a;
            h = b.th + Mathf.PI;
        }
        else
        {
            float t = (u - 2f * s1 - s2) / b.a;
            r = b.r0 - b.a * Mathf.Sin(t);
            s = b.a * Mathf.Cos(t);
            h = b.th + Mathf.PI + t;
        }
        float ct = Mathf.Cos(b.th);
        float st = Mathf.Sin(b.th);
        x = CX + r * ct - s * st;
        y = CY + r * st + s * ct;
        heading = h;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < fahrzeuge.Count; i++)
        {
            var b = bahnen[bahnIndex[i]];
            float schritt = b.tempo * tempo * dt;
            float u = weg[i] + schritt;
            if (u >= b.laenge) u -= b.laenge;
            weg[i] = u;
            strecke[i] += schritt;
            float x, y, h;
            Pose(b, u, out x, out y, out h);
            Vector3 pos = c + ex * (x - CX) + ey * (y - CY);
            Vector3 vor = ex * Mathf.Cos(h) + ey * Mathf.Sin(h);
            fahrzeuge[i].SetPositionAndRotation(pos, Quaternion.LookRotation(vor, ez) * modellKorrektur);
            var rs = raeder[i];
            var rr = radien[i];
            var sr = startRot[i];
            for (int k = 0; k < rs.Length; k++)
            {
                float winkel = drehVorzeichen * (strecke[i] / rr[k]) * Mathf.Rad2Deg;
                rs[k].localRotation = sr[k] * Quaternion.Euler(winkel, 0f, 0f);
            }
        }
    }
}
