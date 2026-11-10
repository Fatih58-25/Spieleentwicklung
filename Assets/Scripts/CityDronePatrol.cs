using UnityEngine;

/// <summary>
/// Sehir ustunde devriye gezen helikopter/drone robot.
///
/// - Kisa, gecerli waypoint'ler secer (hedef bir binanin icinde olamaz, yol acik olmak zorunda)
/// - "Whisker" SphereCast'leriyle duvarlardan, zeminden ve catilardan yumusakca kacinir
/// - Her adimda SphereCast ile "collide and slide" yapar: fiziksel olarak collider'in icine GIREMEZ
/// - Hareket hiz (velocity) tabanli: ataletli, helikopter gibi suzulur; govde hiza gore yatar
/// </summary>
public class CityDronePatrol : MonoBehaviour
{
    [Header("Devriye Alani")]
    [Tooltip("Bos birakirsan dogdugu nokta merkez olur.")]
    public Transform patrolCenter;
    [Tooltip("Merkezden en fazla bu kadar yatay uzaklasir.")]
    public float patrolRadius = 35f;
    [Tooltip("Ucacagi min / max yukseklik (dunya Y ekseni).")]
    public float minHeight = 5f;
    public float maxHeight = 15f;

    [Header("Hareket")]
    public float moveSpeed = 6f;
    [Tooltip("Hizin hedef hiza yaklasma keskinligi. Buyuk = keskin, kucuk = agir ve kayarak (helikopter hissi).")]
    public float acceleration = 2.5f;
    [Tooltip("Hedefe bu kadar yaklasinca yeni hedef secilir.")]
    public float arriveDistance = 3f;
    [Tooltip("Her yeni hedef, mevcut konumdan bu aralikta secilir.")]
    public float waypointMinDistance = 10f;
    public float waypointMaxDistance = 30f;
    [Tooltip("Yeni hedefte yukseklik en fazla bu kadar degisir (ani dikey ziplamalar olmasin).")]
    public float maxAltitudeChange = 5f;

    [Header("Govde & Cevre Algilama")]
    [Tooltip("Binalar, duvarlar, zemin. Drone'un KENDI layer'ini buradan cikar (kendi collider'lari zaten filtrelenir ama temiz olsun).")]
    public LayerMask obstacleLayers = ~0;
    [Tooltip("Drone'un fiziksel yaricapi. Modelin yarisi kadar ya da biraz fazlasi olsun.")]
    public float bodyRadius = 1f;
    [Tooltip("Whisker (algilama isini) uzunlugu. Hiz arttikca artir.")]
    public float lookAheadDistance = 10f;
    [Tooltip("Engelden kacis sertligi.")]
    public float avoidanceStrength = 2f;
    [Tooltip("Altinda catı / sokak varsa en az bu kadar yukarida kalir.")]
    public float groundClearance = 3f;
    [Tooltip("Duvarla drone arasinda birakilan pay.")]
    public float wallSkin = 0.15f;

    [Header("Donus & Egilme")]
    public float turnSmoothTime = 0.4f;
    [Tooltip("Hiz yonune gore one/yana egilme (derece).")]
    public float maxTiltAngle = 12f;
    public float tiltSmoothTime = 0.3f;
    [Tooltip("Donuse girerken yana yatma miktari.")]
    public float bankFromTurn = 0.04f;

    [Header("Hover (Havada Suzulme)")]
    public float hoverAmplitude = 0.4f;
    public float hoverFrequency = 2f;

    [Header("Takilma")]
    [Tooltip("Bu sureden uzun yavas kalirsa yeni hedef secer.")]
    public float stuckTime = 1.5f;

    // --- Durum ---
    private Vector3 fixedCenter;
    private Vector3 targetPosition;
    private Vector3 velocity;
    private Vector3 lastSafePosition;

    private float yaw, yawVelocity;
    private float pitch, pitchVelocity;
    private float roll, rollVelocity;

    private float hoverTimer;
    private float lastHover;

    private float pathCheckTimer;
    private float stuckTimer;
    private float avoidSide = 1f;

    private float pausedTimer;
    private bool stopped;

    // Buffer'lar (her karede bellek ayirmamak icin)
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private readonly Collider[] overlapBuffer = new Collider[16];
    private readonly Vector3[] whiskerDirs = new Vector3[6];
    private readonly float[] whiskerLens = new float[6];

    private Vector3 CenterPos => patrolCenter != null ? patrolCenter.position : fixedCenter;

    // ------------------------------------------------------------
    //  DISARIDAN KONTROL (EnemyHitReceiver'in UnityEvent'lerine baglanabilir)
    // ------------------------------------------------------------

    /// <summary>Hasar alinca cagir: drone 'seconds' saniye durup sarsilir, sonra devriyeye devam eder.</summary>
    public void PauseMovement(float seconds)
    {
        pausedTimer = Mathf.Max(pausedTimer, seconds);
    }

    /// <summary>Olunce cagir: devriyeyi tamamen durdurur.</summary>
    public void StopPatrol()
    {
        stopped = true;
        velocity = Vector3.zero;
    }

    public void ResumePatrol()
    {
        stopped = false;
        pausedTimer = 0f;
    }

    // ------------------------------------------------------------

    void Start()
    {
        fixedCenter = transform.position;
        yaw = transform.eulerAngles.y;
        hoverTimer = Random.value * Mathf.PI * 2f;
        avoidSide = Random.value < 0.5f ? -1f : 1f;

        lastSafePosition = transform.position;
        PickNewDestination();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || stopped) return;

        // Hasar alma / durdurma: yerinde sonumle, hover ve egilme devam etsin
        if (pausedTimer > 0f)
        {
            pausedTimer -= dt;
            velocity = Vector3.Lerp(velocity, Vector3.zero, 1f - Mathf.Exp(-6f * dt));
            UpdateRotation(dt);
            ApplyHover();
            return;
        }

        // Bir sekilde collider'in icine girdiyse (spawn, disaridan itilme) son guvenli noktaya don
        if (IsPointBlocked(transform.position, bodyRadius * 0.8f))
        {
            transform.position = lastSafePosition + Vector3.up * lastHover;
            velocity = Vector3.zero;
            PickNewDestination();
            return;
        }
        lastSafePosition = transform.position - Vector3.up * lastHover;

        UpdateTarget(dt);

        Vector3 desired = ComputeDesiredVelocity();
        velocity = Vector3.Lerp(velocity, desired, 1f - Mathf.Exp(-acceleration * dt));

        MoveWithCollision(velocity * dt);
        CheckStuck(dt);

        UpdateRotation(dt);
        ApplyHover();
    }

    // ------------------------------------------------------------
    //  HEDEF SECIMI
    // ------------------------------------------------------------
    private void UpdateTarget(float dt)
    {
        Vector3 pos = transform.position;

        if (Vector3.Distance(pos, targetPosition) < arriveDistance)
        {
            PickNewDestination();
            return;
        }

        // Yol sonradan kapandiysa (duvara dogru kaydiysa) bosuna zorlama, yeni hedef sec
        pathCheckTimer -= dt;
        if (pathCheckTimer <= 0f)
        {
            pathCheckTimer = 0.4f;
            if (!HasClearPath(pos, targetPosition, bodyRadius * 0.8f))
            {
                PickNewDestination();
            }
        }
    }

    private void PickNewDestination()
    {
        Vector3 pos = transform.position;

        // Mevcut yone yakin hedefler tercih edilir (ani U donusleri olmasin), basarisiz olursa aci acilir
        Vector3 baseDir = velocity.sqrMagnitude > 0.5f ? velocity : transform.forward;
        baseDir.y = 0f;
        if (baseDir.sqrMagnitude < 0.0001f) baseDir = Vector3.forward;
        baseDir.Normalize();
        float baseYaw = Mathf.Atan2(baseDir.x, baseDir.z) * Mathf.Rad2Deg;

        const int maxAttempts = 16;
        for (int i = 0; i < maxAttempts; i++)
        {
            float spread = Mathf.Lerp(100f, 180f, i / (float)(maxAttempts - 1));
            float angle = (baseYaw + Random.Range(-spread, spread)) * Mathf.Deg2Rad;
            float dist = Random.Range(waypointMinDistance, waypointMaxDistance);
            float y = Mathf.Clamp(pos.y + Random.Range(-maxAltitudeChange, maxAltitudeChange), minHeight, maxHeight);

            Vector3 candidate = new Vector3(pos.x + Mathf.Sin(angle) * dist, y, pos.z + Mathf.Cos(angle) * dist);

            // 1) Devriye alani icinde mi?
            Vector3 flat = candidate - CenterPos;
            flat.y = 0f;
            if (flat.magnitude > patrolRadius * 0.95f) continue;

            // 2) Hedef noktanin kendisi bir binanin / duvarin icinde mi?
            if (IsPointBlocked(candidate, bodyRadius * 1.5f)) continue;

            // 3) Oraya giden yol acik mi?
            if (!HasClearPath(pos, candidate, bodyRadius)) continue;

            targetPosition = candidate;
            pathCheckTimer = 0.4f;
            return;
        }

        // Hicbiri uygun degil: merkeze dogru kisa bir adim at (ya da yuksel), sonra tekrar dene
        Vector3 toCenter = CenterPos - pos;
        toCenter.y = 0f;
        Vector3 fallbackDir = toCenter.sqrMagnitude > 4f ? toCenter.normalized : Vector3.up;

        Vector3 fallback = pos + fallbackDir * 6f;
        fallback.y = Mathf.Clamp(fallback.y, minHeight, maxHeight);
        targetPosition = fallback;
        pathCheckTimer = 1f;
        avoidSide = -avoidSide;
    }

    // ------------------------------------------------------------
    //  HIZ HESABI (hedef + kacinma + sinirlar)
    // ------------------------------------------------------------
    private Vector3 ComputeDesiredVelocity()
    {
        Vector3 pos = transform.position;

        Vector3 toTarget = targetPosition - pos;
        Vector3 dir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;

        Vector3 steer = dir;

        // Algilama yonu: hizliysak gittigimiz yon, degilse hedef yonu
        Vector3 lookDir = velocity.sqrMagnitude > 1f ? velocity.normalized : dir;
        Vector3 avoid = ComputeAvoidance(lookDir);
        steer += avoid * avoidanceStrength;

        // Devriye alani siniri: kenara yaklasinca yumusakca merkeze cek
        Vector3 fromCenter = pos - CenterPos;
        fromCenter.y = 0f;
        float distFromCenter = fromCenter.magnitude;
        float edgeStart = patrolRadius * 0.85f;
        if (distFromCenter > edgeStart && distFromCenter > 0.001f)
        {
            float w = Mathf.InverseLerp(edgeStart, patrolRadius, distFromCenter); // 0..1
            steer += (-fromCenter / distFromCenter) * (w * 2f);
        }

        // Yukseklik sinirlari
        if (pos.y < minHeight) steer.y += Mathf.Clamp((minHeight - pos.y) * 0.5f, 0f, 2f);
        else if (pos.y > maxHeight) steer.y -= Mathf.Clamp((pos.y - maxHeight) * 0.5f, 0f, 2f);

        steer = steer.sqrMagnitude > 0.0001f ? steer.normalized : dir;

        // Duvara yakinken yavasla
        float speedFactor = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(avoid.magnitude));
        return steer * (moveSpeed * speedFactor);
    }

    // 6 isin: ileri, sag/sol capraz, yukari/asagi capraz, tam asagi (zemin/cati)
    private Vector3 ComputeAvoidance(Vector3 lookDir)
    {
        Vector3 pos = transform.position;

        Vector3 flatFwd = new Vector3(lookDir.x, 0f, lookDir.z);
        if (flatFwd.sqrMagnitude < 0.0001f)
        {
            flatFwd = transform.forward;
            flatFwd.y = 0f;
        }
        flatFwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, flatFwd);

        whiskerDirs[0] = lookDir;                                            whiskerLens[0] = lookAheadDistance;
        whiskerDirs[1] = (lookDir + right * 0.7f).normalized;                whiskerLens[1] = lookAheadDistance * 0.8f;
        whiskerDirs[2] = (lookDir - right * 0.7f).normalized;                whiskerLens[2] = lookAheadDistance * 0.8f;
        whiskerDirs[3] = (lookDir + Vector3.up * 0.6f).normalized;           whiskerLens[3] = lookAheadDistance * 0.7f;
        whiskerDirs[4] = (lookDir - Vector3.up * 0.6f).normalized;           whiskerLens[4] = lookAheadDistance * 0.7f;
        whiskerDirs[5] = Vector3.down;                                       whiskerLens[5] = groundClearance + bodyRadius;

        Vector3 avoid = Vector3.zero;

        for (int i = 0; i < whiskerDirs.Length; i++)
        {
            Vector3 d = whiskerDirs[i];
            float len = whiskerLens[i];

            RaycastHit hit;
            if (!SphereCastIgnoreSelf(pos, d, len, bodyRadius * 0.9f, out hit)) continue;

            float weight = 1f - Mathf.Clamp01(hit.distance / len);
            Vector3 n = hit.distance <= 0.001f ? -d : hit.normal;

            // 1) Yuzeyden uzaklas
            avoid += n * weight;

            // 2) Yuzey boyunca kay (tam karsidan geliyorsak bir yana sap)
            Vector3 slide = Vector3.ProjectOnPlane(lookDir, n);
            if (slide.sqrMagnitude < 0.05f) slide = Vector3.Cross(Vector3.up, n) * avoidSide;
            avoid += slide.normalized * (weight * 0.8f);
        }

        return avoid;
    }

    // ------------------------------------------------------------
    //  CARPISMA: collide and slide (collider'in icine girmeyi engeller)
    // ------------------------------------------------------------
    private void MoveWithCollision(Vector3 delta)
    {
        Vector3 remaining = delta;

        for (int i = 0; i < 3; i++)
        {
            float dist = remaining.magnitude;
            if (dist < 0.0001f) break;

            Vector3 dir = remaining / dist;

            RaycastHit hit;
            if (SphereCastIgnoreSelf(transform.position, dir, dist + wallSkin, bodyRadius, out hit))
            {
                float allowed = Mathf.Min(Mathf.Max(0f, hit.distance - wallSkin), dist);
                transform.position += dir * allowed;

                // Kalan hareketi duvar yuzeyine yansit, hizi da yuzeyle hizala
                Vector3 leftover = dir * (dist - allowed);
                remaining = Vector3.ProjectOnPlane(leftover, hit.normal);
                velocity = Vector3.ProjectOnPlane(velocity, hit.normal);
            }
            else
            {
                transform.position += remaining;
                break;
            }
        }
    }

    private void CheckStuck(float dt)
    {
        if (velocity.magnitude < moveSpeed * 0.2f) stuckTimer += dt;
        else stuckTimer = 0f;

        if (stuckTimer > stuckTime)
        {
            stuckTimer = 0f;
            avoidSide = -avoidSide;
            PickNewDestination();
        }
    }

    // ------------------------------------------------------------
    //  DONUS, EGILME, HOVER
    // ------------------------------------------------------------
    private void UpdateRotation(float dt)
    {
        // Yaw: sadece yatay hiz yonune don (burun yukari/asagi bakmaz)
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        if (flat.sqrMagnitude > 0.25f)
        {
            float targetYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, turnSmoothTime);
        }
        else
        {
            yawVelocity = Mathf.Lerp(yawVelocity, 0f, 1f - Mathf.Exp(-5f * dt));
        }

        // Pitch / roll: hiza gore egilme (helikopter gibi ileri gittikce burun hafif asagi)
        Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * velocity;
        float speedRef = Mathf.Max(0.01f, moveSpeed);

        float targetPitch = Mathf.Clamp(local.z / speedRef, -1f, 1f) * maxTiltAngle;
        float targetRoll = -Mathf.Clamp(local.x / speedRef, -1f, 1f) * maxTiltAngle;
        targetRoll -= Mathf.Clamp(yawVelocity * bankFromTurn, -maxTiltAngle, maxTiltAngle); // donuse yatis

        pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, tiltSmoothTime);
        roll = Mathf.SmoothDamp(roll, targetRoll, ref rollVelocity, tiltSmoothTime);

        transform.rotation = Quaternion.Euler(pitch, yaw, roll);
    }

    // Sinus salinimi her karede ofset FARKI olarak uygulanir: kayma (drift) olmaz,
    // dis kuvvetlerle (geri tepme vb.) cakismaz.
    private void ApplyHover()
    {
        hoverTimer += Time.deltaTime * hoverFrequency;
        float offset = Mathf.Sin(hoverTimer) * hoverAmplitude;

        transform.position += Vector3.up * (offset - lastHover);
        lastHover = offset;
    }

    // ------------------------------------------------------------
    //  YARDIMCILAR (kendi collider'larini yok sayan sorgular)
    // ------------------------------------------------------------
    private bool SphereCastIgnoreSelf(Vector3 origin, Vector3 dir, float distance, float radius, out RaycastHit best)
    {
        best = default(RaycastHit);
        if (distance <= 0f) return false;

        int count = Physics.SphereCastNonAlloc(origin, radius, dir, hitBuffer, distance, obstacleLayers, QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;

            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
                found = true;
            }
        }

        return found;
    }

    private bool HasClearPath(Vector3 from, Vector3 to, float radius)
    {
        Vector3 d = to - from;
        float len = d.magnitude;
        if (len < 0.01f) return true;

        RaycastHit hit;
        return !SphereCastIgnoreSelf(from, d / len, len, radius, out hit);
    }

    private bool IsPointBlocked(Vector3 point, float radius)
    {
        int n = Physics.OverlapSphereNonAlloc(point, radius, overlapBuffer, obstacleLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (!overlapBuffer[i].transform.IsChildOf(transform)) return true;
        }
        return false;
    }

    // ------------------------------------------------------------
    //  GIZMOS
    // ------------------------------------------------------------
    void OnDrawGizmosSelected()
    {
        Vector3 center = patrolCenter != null ? patrolCenter.position : (Application.isPlaying ? fixedCenter : transform.position);

        Gizmos.color = Color.cyan;
        DrawCircle(center, patrolRadius, minHeight);
        DrawCircle(center, patrolRadius, maxHeight);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, bodyRadius);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, targetPosition);
            Gizmos.DrawWireSphere(targetPosition, 0.5f);
        }
    }

    private static void DrawCircle(Vector3 center, float radius, float y)
    {
        const int segments = 48;
        Vector3 prev = new Vector3(center.x + radius, y, center.z);
        for (int i = 1; i <= segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 next = new Vector3(center.x + Mathf.Cos(a) * radius, y, center.z + Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}