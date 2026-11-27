using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[RequireComponent(typeof(MaschinenPatrouille))]
public class MaschinenLaufanimation : MonoBehaviour
{
    public enum Modus
    {
        Automatisch,
        AnimatorZustaende,
        Clip,
        Prozedural
    }

    [Header("Modus")]
    public Modus modus = Modus.Automatisch;

    [Header("Clip aus dem Modell")]
    public Transform animationsWurzel;
    public AnimationClip laufClip;
    public AnimationClip stehClip;
    public float schrittLaenge = 3f;
    public float ueberblendZeit = 0.25f;

    [Header("Animator-Zustaende")]
    public string stehZustand = "Idle";
    public string gehZustand = "Walk";
    public string laufZustand = "Run";
    public float laufAbTempo = 6f;
    public float animatorReferenzTempo = 3f;

    [Header("Prozedural")]
    public float wippHoehe = 0.08f;
    public float wankWinkel = 4f;
    public float neigeWinkel = 6f;

    [Header("Rotoren")]
    public Transform[] rotoren;
    public bool rotorenAutomatischSuchen = true;
    public Vector3 rotorAchse = Vector3.up;
    public float rotorDrehzahl = 720f;

    MaschinenPatrouille patrouille;
    Animator animator;
    Modus aktiv;
    bool bereit;
    float phase;
    float uhr;
    float gewicht;
    float tempoGlatt;
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable laufSpieler;
    AnimationClipPlayable stehSpieler;
    int stehHash;
    int gehHash;
    int laufHash;
    int aktuellerHash;
    Transform drehpunkt;

    public Modus AktiverModus
    {
        get { Vorbereiten(); return aktiv; }
    }

    public void Vorbereiten()
    {
        if (bereit) return;
        bereit = true;
        patrouille = GetComponent<MaschinenPatrouille>();
        aktiv = Modus.Prozedural;
        bool clipMoeglich = laufClip != null;
        if (modus == Modus.Clip || (modus == Modus.Automatisch && clipMoeglich))
        {
            if (clipMoeglich)
            {
                ClipAufbauen();
                aktiv = Modus.Clip;
            }
        }
        if (aktiv != Modus.Clip && (modus == Modus.AnimatorZustaende || modus == Modus.Automatisch))
        {
            if (AnimatorPruefen()) aktiv = Modus.AnimatorZustaende;
        }
        if (aktiv == Modus.Prozedural) DrehpunktBauen();
        if (aktiv != Modus.Clip && rotorenAutomatischSuchen && (rotoren == null || rotoren.Length == 0)) RotorenSuchen();
    }

    bool AnimatorPruefen()
    {
        var a = GetComponentInChildren<Animator>(true);
        if (a == null || a.runtimeAnimatorController == null) return false;
        stehHash = Animator.StringToHash(stehZustand);
        gehHash = Animator.StringToHash(gehZustand);
        laufHash = Animator.StringToHash(laufZustand);
        if (!a.HasState(0, gehHash) && !a.HasState(0, laufHash)) return false;
        if (!a.HasState(0, gehHash)) gehHash = laufHash;
        if (!a.HasState(0, laufHash)) laufHash = gehHash;
        if (!a.HasState(0, stehHash)) stehHash = 0;
        animator = a;
        aktuellerHash = 0;
        return true;
    }

    void ClipAufbauen()
    {
        var wurzel = animationsWurzel != null ? animationsWurzel : transform;
        animator = wurzel.GetComponent<Animator>();
        if (animator == null) animator = wurzel.gameObject.AddComponent<Animator>();
        animator.applyRootMotion = false;
        graph = PlayableGraph.Create("Laufanimation_" + name);
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var ausgabe = AnimationPlayableOutput.Create(graph, "Ausgabe", animator);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        stehSpieler = AnimationClipPlayable.Create(graph, stehClip != null ? stehClip : laufClip);
        laufSpieler = AnimationClipPlayable.Create(graph, laufClip);
        stehSpieler.SetSpeed(0.0);
        laufSpieler.SetSpeed(0.0);
        graph.Connect(stehSpieler, 0, mixer, 0);
        graph.Connect(laufSpieler, 0, mixer, 1);
        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);
        ausgabe.SetSourcePlayable(mixer);
        graph.Play();
    }

    void DrehpunktBauen()
    {
        var kinder = new List<Transform>();
        foreach (Transform k in transform) kinder.Add(k);
        if (kinder.Count == 0) return;
        var go = new GameObject("Laufanimation");
        drehpunkt = go.transform;
        drehpunkt.SetParent(transform, false);
        foreach (var k in kinder) k.SetParent(drehpunkt, true);
    }

    void RotorenSuchen()
    {
        var liste = new List<Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("blade") || n.Contains("rotor") || n.Contains("propell")) liste.Add(t);
        }
        var gefiltert = new List<Transform>();
        foreach (var t in liste)
        {
            bool elternDabei = false;
            foreach (var u in liste)
            {
                if (u != t && t.IsChildOf(u)) elternDabei = true;
            }
            if (!elternDabei) gefiltert.Add(t);
        }
        rotoren = gefiltert.ToArray();
    }

    void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
    }

    void Update()
    {
        Schritt(Time.deltaTime);
    }

    public void Schritt(float dt)
    {
        Vorbereiten();
        if (patrouille == null) return;
        uhr += dt;
        float tempo = patrouille.AktuellesTempo;
        tempoGlatt = Mathf.Lerp(tempoGlatt, tempo, Mathf.Clamp01(10f * dt));
        if (aktiv == Modus.Clip) ClipSchritt(dt, tempo);
        else if (aktiv == Modus.AnimatorZustaende) AnimatorSchritt(tempo);
        else ProzeduralSchritt(dt, tempo);
        if (aktiv != Modus.Clip) RotorenDrehen(dt, tempo);
    }

    void ClipSchritt(float dt, float tempo)
    {
        if (!graph.IsValid()) return;
        phase = Mathf.Repeat(phase + tempo / Mathf.Max(0.1f, schrittLaenge) * dt, 1f);
        float ziel = tempo > 0.15f ? 1f : 0f;
        gewicht = Mathf.MoveTowards(gewicht, ziel, dt / Mathf.Max(0.01f, ueberblendZeit));
        mixer.SetInputWeight(0, 1f - gewicht);
        mixer.SetInputWeight(1, gewicht);
        laufSpieler.SetTime(phase * laufClip.length);
        if (stehClip != null) stehSpieler.SetTime(Mathf.Repeat(uhr, Mathf.Max(0.05f, stehClip.length)));
        else stehSpieler.SetTime(0.0);
        graph.Evaluate(0f);
    }

    void AnimatorSchritt(float tempo)
    {
        if (animator == null) return;
        int ziel = stehHash;
        if (tempo > 0.15f) ziel = tempo >= laufAbTempo ? laufHash : gehHash;
        if (ziel != aktuellerHash)
        {
            aktuellerHash = ziel;
            if (ziel != 0) animator.CrossFadeInFixedTime(ziel, ueberblendZeit);
        }
        if (ziel == stehHash) animator.speed = 1f;
        else
        {
            float referenz = ziel == laufHash && laufHash != gehHash ? laufAbTempo : animatorReferenzTempo;
            animator.speed = Mathf.Clamp(tempo / Mathf.Max(0.1f, referenz), 0.4f, 2.5f);
        }
    }

    void ProzeduralSchritt(float dt, float tempo)
    {
        if (drehpunkt == null) return;
        phase = Mathf.Repeat(phase + tempo / Mathf.Max(0.1f, schrittLaenge) * dt, 1f);
        float s = Mathf.Clamp01(tempoGlatt / Mathf.Max(0.1f, patrouille.geschwindigkeit));
        float wippen = Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 2f)) * wippHoehe * s;
        float wanken = Mathf.Sin(phase * Mathf.PI * 2f) * wankWinkel * s;
        float neigen = neigeWinkel * s + Mathf.Sin(phase * Mathf.PI * 4f) * neigeWinkel * 0.15f * s;
        drehpunkt.localPosition = new Vector3(0f, wippen, 0f);
        drehpunkt.localRotation = Quaternion.Euler(neigen, 0f, wanken);
    }

    void RotorenDrehen(float dt, float tempo)
    {
        if (rotoren == null || rotoren.Length == 0) return;
        float s = Mathf.Clamp01(tempo / Mathf.Max(0.1f, patrouille.geschwindigkeit));
        float grad = rotorDrehzahl * dt * (0.5f + 0.5f * s);
        for (int i = 0; i < rotoren.Length; i++)
        {
            if (rotoren[i] == null) continue;
            rotoren[i].Rotate(rotorAchse, (i % 2 == 0 ? 1f : -1f) * grad, Space.Self);
        }
    }
}
