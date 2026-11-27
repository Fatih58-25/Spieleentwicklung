using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class MaschinenPatrouille : MonoBehaviour
{
    public enum Routenart
    {
        Zufall,
        Wegpunkte
    }

    [Header("Route")]
    public Routenart route = Routenart.Zufall;
    public Transform[] wegpunkte;
    public bool hinUndZurueck;
    public float radius = 60f;
    public Vector2 wartezeit = new Vector2(1f, 4f);
    public float ankunftsAbstand = 2f;

    [Header("Bewegung")]
    public float geschwindigkeit = 3f;
    public float beschleunigung = 4f;
    public float drehgeschwindigkeit = 120f;
    public bool fliegend;
    public float flughoehe = 8f;
    public float bodenAbstand;
    public bool navMeshNutzen = true;

    [Header("Hindernisse")]
    public LayerMask hindernisse = ~0;
    public float koerperRadius = 1f;
    public float koerperHoehe = 2f;
    public float sichtweite = 12f;
    public float sicherheitsabstand = 3f;
    public float ausweichwinkel = 35f;
    public float festhaengZeit = 4f;
    public float stufenHoehe = 0.6f;
    public bool bewegteHindernisse = true;

    [Header("Animation (optional)")]
    public string geschwindigkeitParameter = "Speed";

    static readonly List<MaschinenPatrouille> alle = new List<MaschinenPatrouille>();

    readonly RaycastHit[] puffer = new RaycastHit[24];
    readonly Collider[] sammel = new Collider[32];
    readonly Dictionary<int, Vector3> letztePositionen = new Dictionary<int, Vector3>();
    NavMeshAgent agent;
    Animator animator;
    int animatorParameter;
    bool animatorHatParameter;
    bool bereit;
    bool registriert;
    Vector3 heimat;
    Vector3 ziel;
    bool hatZiel;
    int wegIndex = -1;
    int wegRichtung = 1;
    float uhr;
    float wartenBis;
    float tempoAktuell;
    float blockiertSeit;
    float lenkSeite = 1f;
    Vector3 letzteRichtung = Vector3.forward;

    public string Zustandstext()
    {
        bool b;
        float f;
        Vector3 w = hatZiel ? (ziel - transform.position) : transform.forward;
        w.y = 0f;
        Lenken(w.normalized, out b, out f);
        return "hatZiel=" + hatZiel + " ziel=" + ziel.ToString("F1") + " tempo=" + tempoAktuell.ToString("F2") + " blockiert=" + b + " frei=" + f.ToString("F1") + " blockiertSeit=" + blockiertSeit.ToString("F1") + " uhr=" + uhr.ToString("F1") + " wartenBis=" + wartenBis.ToString("F1") + " eingeklemmt=" + Eingeklemmt(Koerpermitte()) + " vor=" + transform.forward.ToString("F2") + " freiVorn=" + FreieStrecke(transform.forward).ToString("F1") + " ueberlapp=" + UeberlappNamen(Koerpermitte() + transform.forward * 0.3f);
    }

    string UeberlappNamen(Vector3 mitte)
    {
        int n = Physics.OverlapSphereNonAlloc(mitte, koerperRadius * 0.92f, sammel, hindernisse, QueryTriggerInteraction.Ignore);
        string r = "";
        for (int i = 0; i < n; i++)
        {
            var c = sammel[i];
            if (c == null || Eigenes(c)) continue;
            r += c.name + "(maxY " + c.bounds.max.y.ToString("F1") + ") ";
        }
        return r;
    }

    public float AktuellesTempo
    {
        get { return tempoAktuell; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zuruecksetzen()
    {
        alle.Clear();
    }

    public void Vorbereiten()
    {
        if (bereit) return;
        bereit = true;
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null && !string.IsNullOrEmpty(geschwindigkeitParameter))
        {
            foreach (var p in animator.parameters)
            {
                if (p.name == geschwindigkeitParameter && p.type == AnimatorControllerParameterType.Float)
                {
                    animatorParameter = Animator.StringToHash(geschwindigkeitParameter);
                    animatorHatParameter = true;
                }
            }
        }
        heimat = transform.position;
        letzteRichtung = transform.forward;
        if (!registriert)
        {
            alle.Add(this);
            registriert = true;
        }
        if (agent != null && agent.enabled)
        {
            agent.avoidancePriority = 20 + Mathf.Abs(GetInstanceID()) % 60;
            agent.updateRotation = true;
        }
    }

    void Start()
    {
        Vorbereiten();
        wartenBis = Random.Range(0f, 1.5f);
    }

    void OnDisable()
    {
        if (registriert)
        {
            alle.Remove(this);
            registriert = false;
        }
        bereit = false;
    }

    void Update()
    {
        Schritt(Time.deltaTime);
    }

    bool NavMeshAktiv()
    {
        return navMeshNutzen && !fliegend && agent != null && agent.enabled && agent.isOnNavMesh;
    }

    Vector3 Koerpermitte()
    {
        return transform.position + Vector3.up * Mathf.Max(koerperRadius, koerperHoehe * 0.5f);
    }

    bool Eigenes(Collider c)
    {
        return c.transform == transform || c.transform.IsChildOf(transform);
    }

    float FreieStrecke(Vector3 richtungWelt)
    {
        Vector3 mitte = Koerpermitte();
        float frei = sichtweite;
        int n = Physics.SphereCastNonAlloc(mitte, koerperRadius * 0.88f, richtungWelt, puffer, sichtweite, hindernisse, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = puffer[i];
            if (h.collider == null || Eigenes(h.collider)) continue;
            if (h.distance <= 0f) continue;
            if (!fliegend && h.normal.y > 0.6f && h.point.y < transform.position.y + stufenHoehe + koerperRadius) continue;
            if (h.distance < frei) frei = h.distance;
        }
        int meineId = GetInstanceID();
        foreach (var m in alle)
        {
            if (m == null || m == this || !m.isActiveAndEnabled) continue;
            Vector3 d = m.transform.position - transform.position;
            d.y = 0f;
            float entlang = Vector3.Dot(d, richtungWelt);
            if (entlang <= 0f || entlang > sichtweite + m.koerperRadius) continue;
            float seitlich = (d - richtungWelt * entlang).magnitude;
            if (seitlich > koerperRadius + m.koerperRadius + 0.9f) continue;
            if (meineId < m.GetInstanceID() && m.tempoAktuell > 0.3f) continue;
            float strecke = Mathf.Max(0.01f, entlang - m.koerperRadius);
            if (strecke < frei) frei = strecke;
        }
        return frei;
    }

    Vector3 Lenken(Vector3 wunsch, out bool blockiert, out float freiVoraus)
    {
        blockiert = false;
        freiVoraus = FreieStrecke(wunsch);
        if (freiVoraus >= sicherheitsabstand * 2f) return wunsch;
        Vector3 beste = wunsch;
        float besteStrecke = freiVoraus;
        for (int k = 1; k <= 4; k++)
        {
            for (int s = 0; s < 2; s++)
            {
                float seite = (s == 0 ? lenkSeite : -lenkSeite);
                Vector3 kandidat = Quaternion.AngleAxis(ausweichwinkel * k * seite, Vector3.up) * wunsch;
                float strecke = FreieStrecke(kandidat);
                if (strecke > besteStrecke + 0.5f)
                {
                    besteStrecke = strecke;
                    beste = kandidat;
                    if (strecke >= sicherheitsabstand * 2.5f)
                    {
                        lenkSeite = seite;
                        freiVoraus = strecke;
                        return beste;
                    }
                }
            }
        }
        freiVoraus = besteStrecke;
        if (besteStrecke < sicherheitsabstand) blockiert = true;
        return beste;
    }

    bool Gefahr(float dt, out Vector3 flucht)
    {
        flucht = Vector3.zero;
        if (!bewegteHindernisse || dt <= 0f) return false;
        if (letztePositionen.Count > 300) letztePositionen.Clear();
        Vector3 mitte = Koerpermitte();
        int n = Physics.OverlapSphereNonAlloc(mitte, sichtweite * 1.5f + 12f, sammel, hindernisse, QueryTriggerInteraction.Ignore);
        bool gefahr = false;
        for (int i = 0; i < n; i++)
        {
            var c = sammel[i];
            if (c == null || Eigenes(c)) continue;
            var rb = c.attachedRigidbody;
            if (rb == null || rb.GetComponentInParent<MaschinenPatrouille>() != null) continue;
            int id = rb.GetInstanceID();
            Vector3 pos = c.bounds.center;
            Vector3 v = Vector3.zero;
            Vector3 alt;
            if (letztePositionen.TryGetValue(id, out alt)) v = (pos - alt) / dt;
            letztePositionen[id] = pos;
            v.y = 0f;
            if (v.magnitude < 0.5f) continue;
            Vector3 p = pos - mitte;
            p.y = 0f;
            float t = Mathf.Clamp(-Vector3.Dot(p, v) / v.sqrMagnitude, 0f, 4f);
            float d = (p + v * t).magnitude;
            Vector3 gr = c.bounds.size;
            float breite = Mathf.Min(gr.x, gr.z) * 0.5f;
            float laenge = Mathf.Max(gr.x, gr.z) * 0.5f;
            float grenze = koerperRadius + breite + 3f;
            bool naehert = Vector3.Dot(p, v) < 0f;
            bool nah = p.magnitude < koerperRadius + laenge + 3f;
            if ((naehert && d < grenze) || (nah && p.magnitude < koerperRadius + laenge + 1f))
            {
                Vector3 quer = Vector3.Cross(Vector3.up, v).normalized;
                if (Vector3.Dot(quer, -p) < 0f) quer = -quer;
                flucht += quer / (t + 0.5f);
                gefahr = true;
            }
        }
        if (gefahr) flucht = flucht.normalized;
        return gefahr;
    }

    bool Eingeklemmt(Vector3 neueMitte)
    {
        int n = Physics.OverlapSphereNonAlloc(neueMitte, koerperRadius * 0.92f, sammel, hindernisse, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = sammel[i];
            if (c == null || Eigenes(c)) continue;
            if (c.bounds.max.y < transform.position.y + stufenHoehe + 0.3f) continue;
            return true;
        }
        return false;
    }

    bool Befreien(float dt)
    {
        Vector3 mitte = Koerpermitte();
        int n = Physics.OverlapSphereNonAlloc(mitte, koerperRadius * 0.92f, sammel, hindernisse, QueryTriggerInteraction.Ignore);
        Vector3 weg = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            var c = sammel[i];
            if (c == null || Eigenes(c)) continue;
            if (c.bounds.max.y < transform.position.y + stufenHoehe + 0.3f) continue;
            Vector3 nahe = c.ClosestPoint(mitte);
            Vector3 d = mitte - nahe;
            if (d.sqrMagnitude < 0.0001f) d = mitte - c.bounds.center;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) weg += d.normalized;
        }
        if (weg.sqrMagnitude < 0.0001f) return false;
        weg.Normalize();
        transform.position += weg * Mathf.Max(1f, geschwindigkeit * 0.6f) * dt;
        tempoAktuell = 0f;
        return true;
    }

    bool BodenSuchen(Vector3 start, float laenge, out float hoehe)
    {
        hoehe = 0f;
        int n = Physics.RaycastNonAlloc(start, Vector3.down, puffer, laenge, hindernisse, QueryTriggerInteraction.Ignore);
        float beste = float.MaxValue;
        bool gefunden = false;
        for (int i = 0; i < n; i++)
        {
            var h = puffer[i];
            if (h.collider == null || Eigenes(h.collider)) continue;
            if (h.distance < beste)
            {
                beste = h.distance;
                hoehe = h.point.y;
                gefunden = true;
            }
        }
        return gefunden;
    }

    void NeuesZiel()
    {
        hatZiel = false;
        if (route == Routenart.Wegpunkte && wegpunkte != null && wegpunkte.Length > 0)
        {
            if (wegIndex < 0)
            {
                float besteDistanz = float.MaxValue;
                for (int i = 0; i < wegpunkte.Length; i++)
                {
                    if (wegpunkte[i] == null) continue;
                    float d = (wegpunkte[i].position - transform.position).sqrMagnitude;
                    if (d < besteDistanz)
                    {
                        besteDistanz = d;
                        wegIndex = i;
                    }
                }
                if (wegIndex < 0) wegIndex = 0;
            }
            else
            {
                wegIndex += wegRichtung;
                if (wegIndex >= wegpunkte.Length)
                {
                    if (hinUndZurueck)
                    {
                        wegRichtung = -1;
                        wegIndex = Mathf.Max(0, wegpunkte.Length - 2);
                    }
                    else wegIndex = 0;
                }
                else if (wegIndex < 0)
                {
                    wegRichtung = 1;
                    wegIndex = Mathf.Min(1, wegpunkte.Length - 1);
                }
            }
            if (wegpunkte[wegIndex] == null) return;
            ziel = wegpunkte[wegIndex].position;
            hatZiel = true;
        }
        else
        {
            for (int versuch = 0; versuch < 14; versuch++)
            {
                float winkel = Random.Range(0f, 360f);
                float entfernung = Mathf.Sqrt(Random.value) * radius;
                Vector3 kandidat = heimat + Quaternion.Euler(0f, winkel, 0f) * Vector3.forward * entfernung;
                float hoehe;
                if (!fliegend)
                {
                    if (!BodenSuchen(new Vector3(kandidat.x, heimat.y + 5f, kandidat.z), 30f, out hoehe)) continue;
                    kandidat.y = hoehe;
                    if (Physics.CheckSphere(kandidat + Vector3.up * (koerperRadius + stufenHoehe), koerperRadius, hindernisse, QueryTriggerInteraction.Ignore)) continue;
                    if (navMeshNutzen && agent != null && agent.enabled && agent.isOnNavMesh)
                    {
                        NavMeshHit nh;
                        if (!NavMesh.SamplePosition(kandidat, out nh, 6f, NavMesh.AllAreas)) continue;
                        kandidat = nh.position;
                    }
                }
                else kandidat.y = transform.position.y;
                if (Vector3.Distance(new Vector3(kandidat.x, 0f, kandidat.z), new Vector3(transform.position.x, 0f, transform.position.z)) < ankunftsAbstand * 2f) continue;
                ziel = kandidat;
                hatZiel = true;
                break;
            }
        }
        if (hatZiel && NavMeshAktiv()) agent.SetDestination(ziel);
    }

    void AnimatorSetzen()
    {
        if (animatorHatParameter) animator.SetFloat(animatorParameter, tempoAktuell);
    }

    public void Schritt(float dt)
    {
        Vorbereiten();
        uhr += dt;
        bool nav = NavMeshAktiv();
        if (!nav && !fliegend && Befreien(dt))
        {
            AnimatorSetzen();
            return;
        }
        if (!hatZiel)
        {
            tempoAktuell = Mathf.MoveTowards(tempoAktuell, 0f, beschleunigung * 3f * dt);
            if (nav) agent.isStopped = true;
            AnimatorSetzen();
            if (uhr >= wartenBis)
            {
                NeuesZiel();
                if (!hatZiel) wartenBis = uhr + 1f;
            }
            return;
        }
        Vector3 pos = transform.position;
        Vector3 zuZiel = ziel - pos;
        zuZiel.y = 0f;
        float entfernung = zuZiel.magnitude;
        if (entfernung < ankunftsAbstand)
        {
            hatZiel = false;
            wartenBis = uhr + Random.Range(wartezeit.x, Mathf.Max(wartezeit.x, wartezeit.y));
            return;
        }
        Vector3 wunsch = zuZiel / entfernung;
        if (nav && agent.hasPath && agent.steeringTarget != Vector3.zero)
        {
            Vector3 w = agent.steeringTarget - pos;
            w.y = 0f;
            if (w.sqrMagnitude > 0.01f) wunsch = w.normalized;
        }
        Vector3 flucht;
        bool fluechtet = Gefahr(dt, out flucht);
        if (fluechtet) wunsch = flucht;
        bool blockiert;
        float frei;
        Vector3 richtung = Lenken(wunsch, out blockiert, out frei);
        float zielTempo = geschwindigkeit;
        if (blockiert) zielTempo = 0f;
        else if (frei < sicherheitsabstand * 2f) zielTempo = geschwindigkeit * Mathf.Clamp01((frei - sicherheitsabstand * 0.8f) / (sicherheitsabstand * 1.2f));
        if (fluechtet && !blockiert) zielTempo = Mathf.Max(zielTempo, geschwindigkeit * 1.6f);
        float rate = zielTempo < tempoAktuell ? beschleunigung * 4f : beschleunigung;
        tempoAktuell = Mathf.MoveTowards(tempoAktuell, zielTempo, rate * dt);
        if (blockiert)
        {
            blockiertSeit += dt;
            if (blockiertSeit > festhaengZeit)
            {
                blockiertSeit = 0f;
                lenkSeite = -lenkSeite;
                hatZiel = false;
                wartenBis = uhr + 0.3f;
                return;
            }
        }
        else blockiertSeit = 0f;

        if (nav)
        {
            agent.speed = Mathf.Max(0.05f, tempoAktuell);
            agent.isStopped = blockiert || tempoAktuell < 0.05f;
        }
        else
        {
            Vector3 flach = new Vector3(richtung.x, 0f, richtung.z);
            if (flach.sqrMagnitude > 0.0001f)
            {
                Quaternion zielRot = Quaternion.LookRotation(flach.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, zielRot, drehgeschwindigkeit * dt);
                letzteRichtung = flach.normalized;
            }
            Vector3 vor = transform.forward;
            vor.y = 0f;
            vor.Normalize();
            float ausrichtung = Mathf.Clamp01(Vector3.Dot(vor, letzteRichtung) * 1.3f - 0.3f);
            float freiVorn = FreieStrecke(vor);
            float erlaubt = freiVorn < koerperRadius * 0.7f ? 0f : 1f;
            Vector3 neuePos = transform.position + vor * tempoAktuell * ausrichtung * erlaubt * dt;
            if (!fliegend && Eingeklemmt(neuePos + (Koerpermitte() - transform.position)))
            {
                neuePos = transform.position;
                tempoAktuell = 0f;
            }
            float hoehe;
            if (fliegend)
            {
                float vonHoehe = neuePos.y + flughoehe + 2f;
                if (BodenSuchen(new Vector3(neuePos.x, vonHoehe, neuePos.z), flughoehe + 200f, out hoehe)) neuePos.y = Mathf.Lerp(transform.position.y, hoehe + flughoehe, Mathf.Clamp01(3f * dt));
            }
            else if (BodenSuchen(new Vector3(neuePos.x, neuePos.y + stufenHoehe + 1f, neuePos.z), stufenHoehe + 3f, out hoehe))
            {
                neuePos.y = Mathf.Lerp(transform.position.y, hoehe + bodenAbstand, Mathf.Clamp01(12f * dt));
            }
            transform.position = neuePos;
        }
        AnimatorSetzen();
    }

    void OnDrawGizmosSelected()
    {
        Vector3 mitte = Application.isPlaying ? heimat : transform.position;
        if (route == Routenart.Zufall)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            const int teile = 48;
            Vector3 vorher = mitte + Vector3.forward * radius;
            for (int i = 1; i <= teile; i++)
            {
                float a = i * Mathf.PI * 2f / teile;
                Vector3 jetzt = mitte + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                Gizmos.DrawLine(vorher, jetzt);
                vorher = jetzt;
            }
        }
        else if (wegpunkte != null)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            for (int i = 0; i < wegpunkte.Length; i++)
            {
                if (wegpunkte[i] == null) continue;
                Gizmos.DrawSphere(wegpunkte[i].position, 0.6f);
                int n = (i + 1) % wegpunkte.Length;
                if (wegpunkte[n] != null && (hinUndZurueck ? i < wegpunkte.Length - 1 : true)) Gizmos.DrawLine(wegpunkte[i].position, wegpunkte[n].position);
            }
        }
        if (hatZiel)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(ziel, 0.8f);
            Gizmos.DrawLine(transform.position, ziel);
        }
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.7f);
        Gizmos.DrawWireSphere(Koerpermitte() + transform.forward * sicherheitsabstand, koerperRadius);
        Gizmos.DrawWireSphere(Koerpermitte() + transform.forward * sichtweite, koerperRadius);
    }
}
