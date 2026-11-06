using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// Dusman ana objesine tak (robot, orumcek, dinozor...). Hasar, denge (poise/stagger),
/// geri tepme, prosedurel sarsilma, vurus parlamasi ve efektleri yonetir.
///
/// NavMeshAgent, CharacterController ve Rigidbody ile calisir (hangisi varsa).
/// AI scriptin 'IsBusy' true iken hareket / saldiri kararini durdurmali.
/// </summary>
[DisallowMultipleComponent]
public class EnemyHitReceiver : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 100f;
    [Tooltip("0 = yok etme. Oldukten kac saniye sonra obje silinsin.")]
    public float destroyDelayAfterDeath = 0f;

    [Header("Poise / Stagger (Horizon tarzi denge)")]
    [Tooltip("Dengeyi bozmak icin gereken toplam 'poiseDamage'. Buyuk dusman = yuksek deger.")]
    public float maxPoise = 60f;
    public float poiseRegenDelay = 2.5f;
    public float poiseRegenRate = 20f;
    public float staggerDuration = 1.2f;
    [Tooltip("Dengesiz (stagger) halindeyken alinan hasar carpani")]
    public float staggerDamageMultiplier = 1.5f;
    [Tooltip("Stagger / olum aninda geri tepme carpani")]
    public float staggerKnockbackMultiplier = 1.6f;

    [Header("Knockback")]
    [Tooltip("0 = hafif (orumcek ~0.1), 1 = hic itilmez. Robot ~0.4, dinozor ~0.75")]
    [Range(0f, 1f)] public float knockbackResistance = 0.3f;
    [Tooltip("Surtunme: buyudukce kisa ve sert, kuculdukce uzun ve kayarak iter.")]
    public float knockbackDrag = 7f;
    [Tooltip("Sadece non-kinematic Rigidbody icin yukari sicrama orani")]
    public float rigidbodyLift = 0.15f;

    [Header("Flinch (prosedurel sarsilma)")]
    [Tooltip("Modelin bulundugu CHILD obje. Animator'un kendi kokunu degil, bir wrapper ('Visual') ver.")]
    public Transform visualRoot;
    public float flinchDistance = 0.2f;
    public float flinchTiltAngle = 10f;
    public float flinchRecoverTime = 0.12f;
    [Tooltip("Hit stop sirasinda modelin titremesi")]
    public float shudderAmount = 0.025f;

    [Header("Hit Flash")]
    public Color flashColor = Color.white;
    public float flashDuration = 0.1f;

    [Header("Effects")]
    [Tooltip("Kivilcim vb. Temas noktasinda, yuzey normaline bakacak sekilde olusur.")]
    public GameObject hitVfxPrefab;
    public float vfxLifetime = 2f;
    public AudioClip hitSfx;
    public bool slowMoOnKill = true;
    public float killSlowScale = 0.3f;
    public float killSlowDuration = 0.35f;

    [Header("Animator Triggers (opsiyonel, yoksa atlanir)")]
    public string hitTrigger = "Hit";
    public string staggerTrigger = "Stagger";
    public string deathTrigger = "Die";

    [Header("Events")]
    public UnityEvent onHit;
    public UnityEvent onStagger;
    public UnityEvent onDeath;

    // --- Disaridan okunabilir durum ---
    public float CurrentHealth => currentHealth;
    public float CurrentPoise => currentPoise;
    public bool IsDead => isDead;
    public bool IsStaggered => isStaggered;
    /// <summary>true iken AI hareket etmemeli / saldirmamali.</summary>
    public bool IsBusy => isDead || isStaggered || hitStopTimer > 0f || knockbackVelocity.sqrMagnitude > 0.01f || rigidbodyBusyTimer > 0f;
    public Animator HitAnimator => animator;

    // --- Cache ---
    private Animator animator;
    private NavMeshAgent agent;
    private CharacterController characterController;
    private Rigidbody body;

    private float currentHealth;
    private float currentPoise;
    private bool isDead;
    private bool isStaggered;
    private float staggerEndTime;
    private float lastHitTime = -999f;

    private float hitStopTimer;
    private Vector3 pendingKnockback;
    private Vector3 knockbackVelocity;
    private float rigidbodyBusyTimer;

    private bool wasBusy;
    private bool agentWasStopped;

    // Flash
    private class FlashTarget
    {
        public Renderer renderer;
        public int property;
        public Color original;
    }
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP / HDRP
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in
    private readonly List<FlashTarget> flashTargets = new List<FlashTarget>();
    private MaterialPropertyBlock propertyBlock;
    private float flashTimer;

    // Flinch
    private Vector3 baseLocalPos;
    private Quaternion baseLocalRot;
    private Vector3 flinchOffset;
    private Vector3 flinchOffsetVelocity;
    private float tiltAngle;
    private float tiltVelocity;
    private Vector3 tiltAxis = Vector3.right;
    private bool flinchApplied;

    private readonly HashSet<string> triggerNames = new HashSet<string>();

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        characterController = GetComponent<CharacterController>();
        body = GetComponent<Rigidbody>();

        currentHealth = maxHealth;
        currentPoise = maxPoise;

        CacheTriggers();
        CacheRenderers();

        if (visualRoot != null)
        {
            baseLocalPos = visualRoot.localPosition;
            baseLocalRot = visualRoot.localRotation;
        }
    }

    private void CacheTriggers()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger) triggerNames.Add(p.name);
        }
    }

    private void CacheRenderers()
    {
        propertyBlock = new MaterialPropertyBlock();

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

            Material m = r.sharedMaterial;
            if (m == null) continue;

            int prop;
            if (m.HasProperty(BaseColorId)) prop = BaseColorId;
            else if (m.HasProperty(ColorId)) prop = ColorId;
            else continue;

            flashTargets.Add(new FlashTarget { renderer = r, property = prop, original = m.GetColor(prop) });
        }
    }

    // ------------------------------------------------------------
    //  HASAR ALMA
    // ------------------------------------------------------------
    public bool TakeHit(HitInfo hit)
    {
        if (isDead) return false;

        // Hasar (dengesizken daha fazla)
        float damage = hit.damage * (isStaggered ? staggerDamageMultiplier : 1f);
        currentHealth -= damage;
        lastHitTime = Time.time;

        // Efektler: temas aninda hemen
        SpawnVfx(hit);
        if (hitSfx != null) AudioSource.PlayClipAtPoint(hitSfx, hit.point);

        // Denge (poise)
        bool staggeredNow = false;
        if (!isStaggered)
        {
            currentPoise -= hit.poiseDamage;
            if (currentPoise <= 0f)
            {
                StartStagger();
                staggeredNow = true;
            }
        }

        bool dies = currentHealth <= 0f;
        if (dies) Die();

        // Gorsel tepkiler
        flashTimer = flashDuration;
        ApplyFlinch(hit);
        onHit?.Invoke();
        if (!dies && !staggeredNow) TriggerAnimator(hitTrigger);

        // Geri tepme: hit stop BITINCE patlasin (once donma, sonra firlama)
        Vector3 dir = new Vector3(hit.direction.x, 0f, hit.direction.z);
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : -transform.forward;

        float distance = hit.knockbackDistance * (1f - knockbackResistance);
        if (isStaggered || isDead) distance *= staggerKnockbackMultiplier;

        // Mesafeyi baslangic hizina cevir: ustel sonumde toplam yol = v0 / drag
        pendingKnockback += dir * distance * knockbackDrag;
        hitStopTimer = Mathf.Max(hitStopTimer, hit.hitStopTime);
        if (hit.hitStopTime <= 0f) FireKnockback();

        return true;
    }

    private void StartStagger()
    {
        isStaggered = true;
        staggerEndTime = Time.time + staggerDuration;
        TriggerAnimator(staggerTrigger);
        onStagger?.Invoke();
    }

    private void Die()
    {
        isDead = true;
        isStaggered = false;
        TriggerAnimator(deathTrigger);
        onDeath?.Invoke();

        if (slowMoOnKill) HitStopManager.SlowMotion(killSlowScale, killSlowDuration);
        if (destroyDelayAfterDeath > 0f) Destroy(gameObject, destroyDelayAfterDeath);
    }

    // ------------------------------------------------------------
    //  UPDATE
    // ------------------------------------------------------------
    void Update()
    {
        // Hit stop (gercek zaman) bitince bekleyen geri tepmeyi uygula
        if (hitStopTimer > 0f)
        {
            hitStopTimer -= Time.unscaledDeltaTime;
            if (hitStopTimer <= 0f) FireKnockback();
        }

        if (rigidbodyBusyTimer > 0f) rigidbodyBusyTimer -= Time.deltaTime;

        ApplyKnockbackMovement();
        UpdateStaggerAndPoise();
        UpdateAgentState();
        UpdateFlash();
    }

    private void FireKnockback()
    {
        if (pendingKnockback.sqrMagnitude < 0.000001f) return;

        Vector3 v = pendingKnockback;
        pendingKnockback = Vector3.zero;

        if (agent == null && body != null && !body.isKinematic)
        {
            // Fizik motoru kendi surtunmesiyle yavaslatir (Rigidbody drag ~5-8 iyi)
            body.AddForce(v + Vector3.up * (v.magnitude * rigidbodyLift), ForceMode.VelocityChange);
            rigidbodyBusyTimer = 0.4f;
        }
        else
        {
            knockbackVelocity += v;
        }
    }

    private void ApplyKnockbackMovement()
    {
        if (knockbackVelocity.sqrMagnitude <= 0.01f)
        {
            knockbackVelocity = Vector3.zero;
            return;
        }

        Vector3 delta = knockbackVelocity * Time.deltaTime;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.Move(delta);                       // NavMesh kenarlarini asmaz
        else if (characterController != null && characterController.enabled)
            characterController.Move(delta);         // Collider'lara carpar
        else
            transform.position += delta;             // Yedek: duz tasima

        knockbackVelocity *= Mathf.Exp(-knockbackDrag * Time.deltaTime);
    }

    private void UpdateStaggerAndPoise()
    {
        if (isDead) return;

        if (isStaggered)
        {
            if (Time.time >= staggerEndTime)
            {
                isStaggered = false;
                currentPoise = maxPoise;
            }
        }
        else if (currentPoise < maxPoise && Time.time - lastHitTime > poiseRegenDelay)
        {
            currentPoise = Mathf.Min(maxPoise, currentPoise + poiseRegenRate * Time.deltaTime);
        }
    }

    // Busy iken NavMeshAgent'i durdur, bitince eski haline dondur
    private void UpdateAgentState()
    {
        bool busy = IsBusy;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (busy && !wasBusy)
            {
                agentWasStopped = agent.isStopped;
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            else if (!busy && wasBusy)
            {
                agent.isStopped = agentWasStopped;
            }
        }

        wasBusy = busy;
    }

    // ------------------------------------------------------------
    //  GORSEL: FLASH + FLINCH
    // ------------------------------------------------------------
    private void UpdateFlash()
    {
        if (flashTimer <= 0f || flashTargets.Count == 0) return;

        flashTimer -= Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(flashTimer / Mathf.Max(0.0001f, flashDuration));

        for (int i = 0; i < flashTargets.Count; i++)
        {
            FlashTarget ft = flashTargets[i];
            if (ft.renderer == null) continue;

            if (flashTimer <= 0f)
            {
                ft.renderer.SetPropertyBlock(null); // orijinal materyale don
            }
            else
            {
                ft.renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(ft.property, Color.Lerp(ft.original, flashColor, t));
                ft.renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }

    private void ApplyFlinch(HitInfo hit)
    {
        if (visualRoot == null) return;

        Vector3 dirWorld = new Vector3(hit.direction.x, 0f, hit.direction.z);
        dirWorld = dirWorld.sqrMagnitude > 0.0001f ? dirWorld.normalized : -transform.forward;

        Transform parent = visualRoot.parent;
        Vector3 dirLocal = parent != null ? parent.InverseTransformDirection(dirWorld) : dirWorld;

        float scale = 1f - knockbackResistance * 0.5f;
        flinchOffset = dirLocal * flinchDistance * scale;
        flinchOffsetVelocity = Vector3.zero;

        // Govde vurus yonune dogru yatsin: eksen = yukari x yon
        Vector3 axisWorld = Vector3.Cross(Vector3.up, dirWorld);
        tiltAxis = parent != null ? parent.InverseTransformDirection(axisWorld) : axisWorld;
        tiltAngle = flinchTiltAngle * scale;
        tiltVelocity = 0f;
    }

    void LateUpdate()
    {
        if (visualRoot == null) return;

        bool frozen = hitStopTimer > 0f;

        // Hit stop sirasinda tepkiyi tepe noktada tut, bitince toparlansin
        if (!frozen)
        {
            flinchOffset = Vector3.SmoothDamp(flinchOffset, Vector3.zero, ref flinchOffsetVelocity, flinchRecoverTime);
            tiltAngle = Mathf.SmoothDamp(tiltAngle, 0f, ref tiltVelocity, flinchRecoverTime * 1.5f);
        }

        bool active = frozen || flinchOffset.sqrMagnitude > 0.000001f || Mathf.Abs(tiltAngle) > 0.01f;

        if (!active)
        {
            if (flinchApplied)
            {
                visualRoot.localPosition = baseLocalPos;
                visualRoot.localRotation = baseLocalRot;
                flinchApplied = false;
            }
            return;
        }

        Vector3 jitter = frozen ? Random.insideUnitSphere * shudderAmount : Vector3.zero;

        visualRoot.localPosition = baseLocalPos + flinchOffset + jitter;
        visualRoot.localRotation = Quaternion.AngleAxis(tiltAngle, tiltAxis) * baseLocalRot;
        flinchApplied = true;
    }

    // ------------------------------------------------------------
    //  YARDIMCILAR
    // ------------------------------------------------------------
    private void SpawnVfx(HitInfo hit)
    {
        if (hitVfxPrefab == null) return;

        Quaternion rot = hit.normal.sqrMagnitude > 0.001f ? Quaternion.LookRotation(hit.normal) : Quaternion.identity;
        GameObject fx = Instantiate(hitVfxPrefab, hit.point, rot);
        Destroy(fx, vfxLifetime);
    }

    private void TriggerAnimator(string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName)) return;
        if (!triggerNames.Contains(triggerName)) return;
        animator.SetTrigger(triggerName);
    }
}