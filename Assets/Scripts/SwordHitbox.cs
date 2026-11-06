using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kilica tak. PlayerMovement'in hasar penceresi (hitWindowStart/End) acikken
/// kilic boyunca bir kapsul tarar. Hizli savurmalarda bile "icinden gecme" olmaz,
/// cunku onceki kare ile simdiki kare arasi alt adimlara bolunerek taranir.
/// </summary>
public class SwordHitbox : MonoBehaviour
{
    [Header("References")]
    public PlayerMovement player;
    [Tooltip("Kilicin kabzasina yakin nokta (bos bir child obje)")]
    public Transform bladeBase;
    [Tooltip("Kilicin ucu (bos bir child obje)")]
    public Transform bladeTip;

    [Header("Detection")]
    public float radius = 0.15f;
    [Tooltip("Sadece dusman layer'larini sec")]
    public LayerMask hitLayers = ~0;
    [Range(1, 10)] public int substeps = 4;
    public bool showGizmos = true;

    private Animator playerAnimator;
    private TPSCamera cameraRig;

    private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();
    private readonly Collider[] overlapBuffer = new Collider[16];
    private int currentSwingId = -1;
    private Vector3 prevBase;
    private Vector3 prevTip;
    private bool hasPrev;

    void Start()
    {
        if (player == null) player = GetComponentInParent<PlayerMovement>();
        if (player != null) playerAnimator = player.GetComponent<Animator>();

        if (Camera.main != null) cameraRig = Camera.main.GetComponent<TPSCamera>();
    }

    // Animator kemikleri Update'ten sonra gunceller, bu yuzden kilic konumunu LateUpdate'te okuruz
    void LateUpdate()
    {
        if (player == null || bladeBase == null || bladeTip == null) return;

        Vector3 currBase = bladeBase.position;
        Vector3 currTip = bladeTip.position;

        if (!hasPrev)
        {
            prevBase = currBase;
            prevTip = currTip;
            hasPrev = true;
        }

        PlayerMovement.AttackStep step;
        if (player.TryGetActiveHit(out step))
        {
            // Yeni savurma basladiysa "bu savurmada vurulanlar" listesini sifirla
            if (player.SwingId != currentSwingId)
            {
                currentSwingId = player.SwingId;
                hitThisSwing.Clear();
            }

            Sweep(step, currBase, currTip);
        }

        prevBase = currBase;
        prevTip = currTip;
    }

    private void Sweep(PlayerMovement.AttackStep step, Vector3 currBase, Vector3 currTip)
    {
        // Kilicin ucunun hareket yonu (yatay): yan vuruslarda dusman o tarafa dogru da savrulur
        Vector3 swingDir = currTip - prevTip;
        swingDir.y = 0f;
        swingDir = swingDir.sqrMagnitude > 0.0001f ? swingDir.normalized : Vector3.zero;

        for (int i = 1; i <= substeps; i++)
        {
            float f = i / (float)substeps;
            Vector3 b = Vector3.Lerp(prevBase, currBase, f);
            Vector3 t = Vector3.Lerp(prevTip, currTip, f);

            int count = Physics.OverlapCapsuleNonAlloc(b, t, radius, overlapBuffer, hitLayers, QueryTriggerInteraction.Ignore);

            for (int c = 0; c < count; c++)
            {
                Collider col = overlapBuffer[c];

                // Kendi collider'larimiza vurma
                if (col.transform.IsChildOf(player.transform)) continue;

                IDamageable target = col.GetComponentInParent<IDamageable>();
                if (target == null || hitThisSwing.Contains(target)) continue;

                hitThisSwing.Add(target); // ayni savurmada bir kez
                ProcessHit(target, col, step, b, t, swingDir);
            }
        }
    }

    private void ProcessHit(IDamageable target, Collider col, PlayerMovement.AttackStep step, Vector3 b, Vector3 t, Vector3 swingDir)
    {
        // Temas noktasi ve normal
        Vector3 segMid = (b + t) * 0.5f;
        Vector3 point = ClosestPointOnCollider(col, segMid);
        Vector3 onBlade = ClosestPointOnSegment(b, t, point);

        // Geri tepme yonu: agirlikli olarak oyuncudan uzaga + biraz savurma yonunde
        Vector3 away = col.bounds.center - player.transform.position;
        away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : player.transform.forward;

        Vector3 dir = swingDir != Vector3.zero ? (away * 0.7f + swingDir * 0.3f).normalized : away;

        Vector3 normal = onBlade - point;
        normal = normal.sqrMagnitude > 0.000001f ? normal.normalized : -dir;

        HitInfo info = new HitInfo
        {
            damage = step.damage,
            poiseDamage = step.poiseDamage,
            knockbackDistance = step.knockbackDistance,
            hitStopTime = step.hitStopTime,
            point = point,
            normal = normal,
            direction = dir,
            attacker = player.gameObject
        };

        if (!target.TakeHit(info)) return; // olu dusmana tepki yok

        // Vurus hissi: oyuncu + dusman animasyonu saliselik donar, kamera sarsilir
        HitStopManager.Freeze(playerAnimator, step.hitStopTime);
        HitStopManager.Freeze(target.HitAnimator, step.hitStopTime);

        if (cameraRig != null) cameraRig.AddShake(step.cameraShake);
    }

    private static Vector3 ClosestPointOnCollider(Collider col, Vector3 p)
    {
        // ClosestPoint, convex olmayan MeshCollider'da calismaz
        MeshCollider mc = col as MeshCollider;
        if (mc != null && !mc.convex) return col.bounds.ClosestPoint(p);
        return col.ClosestPoint(p);
    }

    private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.00000001f) return a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        return a + ab * t;
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmos || bladeBase == null || bladeTip == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(bladeBase.position, radius);
        Gizmos.DrawWireSphere(bladeTip.position, radius);
        Gizmos.DrawLine(bladeBase.position, bladeTip.position);
    }
}