using System.Collections.Generic;
using UnityEngine;

public class HandFaust : MonoBehaviour
{
    [System.Serializable]
    public class NeuerKnochen
    {
        public string name;
        public string eltern;
        public Vector3 lokalePosition;
    }

    class Zustand
    {
        public Quaternion basis;
        public Quaternion geschrieben;
        public bool bereit;
    }

    [Header("Neues Netz mit Daumenknochen")]
    public Mesh faustNetz;
    public NeuerKnochen[] neueKnochen = new NeuerKnochen[]
    {
        new NeuerKnochen { name = "mixamorig:RightHandThumb1", eltern = "mixamorig:RightHand", lokalePosition = new Vector3(-0.01353f, 0.00849f, 0.04101f) },
        new NeuerKnochen { name = "mixamorig:RightHandThumb2", eltern = "mixamorig:RightHandThumb1", lokalePosition = new Vector3(-0.05113f, 0.05436f, 0.05013f) },
        new NeuerKnochen { name = "mixamorig:RightHandThumb3", eltern = "mixamorig:RightHandThumb2", lokalePosition = new Vector3(-0.07678f, 0.07316f, 0.08096f) }
    };
    public string handName = "mixamorig:RightHand";

    [Header("Finger (Zeigefingerkette bewegt alle vier Finger)")]
    public string[] fingerKette = { "mixamorig:RightHandIndex1", "mixamorig:RightHandIndex2", "mixamorig:RightHandIndex3" };
    public Vector3 fingerAchse = Vector3.right;
    public float[] fingerWinkel = { 75f, 95f, 65f };

    [Header("Daumen")]
    public string[] daumenKette = { "mixamorig:RightHandThumb1", "mixamorig:RightHandThumb2", "mixamorig:RightHandThumb3" };
    public Vector3 daumenAchse = new Vector3(-1f, 0f, -0.5f);
    public float[] daumenWinkel = { 40f, 40f, 40f };

    [Header("Ablauf")]
    public float dauer = 0.45f;
    [Range(0f, 1f)] public float vorschau;

    SkinnedMeshRenderer smr;
    Transform hand;
    Transform[] finger;
    Transform[] daumen;
    Zustand[] fingerZustand;
    Zustand[] daumenZustand;
    float aktuell;
    float ziel;
    bool bereit;

    public static HandFaust Holen(string spielerTag)
    {
        var spieler = GameObject.FindGameObjectWithTag(spielerTag);
        if (spieler == null) return null;
        return spieler.GetComponentInChildren<HandFaust>(true);
    }

    static Transform Suchen(Transform wurzel, string name)
    {
        foreach (var t in wurzel.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }

    void Awake()
    {
        Vorbereiten();
    }

    public void Vorbereiten()
    {
        if (bereit) return;
        smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
        hand = Suchen(transform, handName);
        if (smr == null || hand == null)
        {
            Debug.LogWarning("HandFaust: Netz oder Hand-Knochen nicht gefunden.", this);
            return;
        }
        if (faustNetz != null && neueKnochen != null && neueKnochen.Length > 0 && smr.sharedMesh != faustNetz)
        {
            var knochenListe = new List<Transform>(smr.bones);
            foreach (var nk in neueKnochen)
            {
                var vorhanden = Suchen(transform, nk.name);
                if (vorhanden == null)
                {
                    var eltern = Suchen(transform, nk.eltern);
                    var go = new GameObject(nk.name);
                    go.transform.SetParent(eltern != null ? eltern : hand, false);
                    vorhanden = go.transform;
                    vorhanden.localRotation = Quaternion.identity;
                    vorhanden.localScale = Vector3.one;
                }
                knochenListe.Add(vorhanden);
            }
            for (int i = 0; i < neueKnochen.Length; i++)
            {
                var t = knochenListe[knochenListe.Count - neueKnochen.Length + i];
                var eltern = Suchen(transform, neueKnochen[i].eltern);
                if (eltern == null) eltern = hand;
                if (eltern == hand) t.localPosition = neueKnochen[i].lokalePosition;
                else
                {
                    Vector3 elternPos = Vector3.zero;
                    foreach (var nk2 in neueKnochen)
                    {
                        if (nk2.name == eltern.name) elternPos = nk2.lokalePosition;
                    }
                    t.localPosition = neueKnochen[i].lokalePosition - elternPos;
                }
            }
            smr.sharedMesh = faustNetz;
            smr.bones = knochenListe.ToArray();
        }
        finger = new Transform[fingerKette.Length];
        fingerZustand = new Zustand[fingerKette.Length];
        for (int i = 0; i < finger.Length; i++)
        {
            finger[i] = Suchen(hand, fingerKette[i]);
            fingerZustand[i] = new Zustand();
        }
        daumen = new Transform[daumenKette.Length];
        daumenZustand = new Zustand[daumenKette.Length];
        for (int i = 0; i < daumen.Length; i++)
        {
            daumen[i] = Suchen(hand, daumenKette[i]);
            daumenZustand[i] = new Zustand();
        }
        bereit = true;
    }

    public void Schliessen(float sekunden)
    {
        Vorbereiten();
        ziel = 1f;
        if (sekunden > 0f) dauer = sekunden;
        else aktuell = 1f;
    }

    public void Oeffnen(float sekunden)
    {
        Vorbereiten();
        ziel = 0f;
        if (sekunden > 0f) dauer = sekunden;
        else aktuell = 0f;
    }

    public bool IstGeschlossen
    {
        get { return ziel > 0.5f; }
    }

    void Anwenden(Transform t, Zustand z, Vector3 achseWelt, float winkel)
    {
        if (t == null) return;
        var cur = t.localRotation;
        if (!z.bereit || Quaternion.Angle(cur, z.geschrieben) > 0.05f)
        {
            z.basis = cur;
            z.bereit = true;
        }
        t.localRotation = z.basis;
        if (Mathf.Abs(winkel) > 0.001f) t.rotation = Quaternion.AngleAxis(winkel, achseWelt) * t.rotation;
        z.geschrieben = t.localRotation;
    }

    public void PoseSetzen(float staerke)
    {
        Vorbereiten();
        if (!bereit) return;
        float w = staerke * staerke * (3f - 2f * staerke);
        Vector3 fAchse = hand.TransformDirection(fingerAchse.normalized);
        for (int i = 0; i < finger.Length; i++)
        {
            float winkel = i < fingerWinkel.Length ? fingerWinkel[i] * w : 0f;
            Anwenden(finger[i], fingerZustand[i], fAchse, winkel);
        }
        Vector3 dAchse = hand.TransformDirection(daumenAchse.normalized);
        for (int i = 0; i < daumen.Length; i++)
        {
            float winkel = i < daumenWinkel.Length ? daumenWinkel[i] * w : 0f;
            Anwenden(daumen[i], daumenZustand[i], dAchse, winkel);
        }
    }

    void LateUpdate()
    {
        if (!bereit) return;
        float tempo = dauer > 0.0001f ? 1f / dauer : 1000f;
        float gewollt = Application.isPlaying ? ziel : vorschau;
        aktuell = Mathf.MoveTowards(aktuell, gewollt, tempo * Time.unscaledDeltaTime);
        PoseSetzen(aktuell);
    }
}
