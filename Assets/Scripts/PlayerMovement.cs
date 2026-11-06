using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    // Her vurusun kendi ayari: klipler farkli uzunlukta oldugu icin ayri ayri ayarlanir
    [System.Serializable]
    public class AttackStep
    {
        [Tooltip("Animator'daki state ismi (Base Layer)")]
        public string stateName = "Attack1";

        [Tooltip("Vurusun hangi noktasindan (0-1) sonra bir sonraki vurusa gecilebilir")]
        [Range(0.1f, 1f)] public float comboWindowStart = 0.45f;

        [Tooltip("Vurusun hangi noktasinda (0-1) bitmis sayilip idle/yurumeye donulur")]
        [Range(0.1f, 1f)] public float endTime = 0.85f;

        [Tooltip("Bu vurusa girerken kullanilan yumusak gecis suresi (saniye)")]
        public float crossFadeIn = 0.08f;

        [Header("Hasar Penceresi (vurusun normalize zamani)")]
        [Tooltip("Kilic bu noktadan itibaren hasar verebilir")]
        [Range(0f, 1f)] public float hitWindowStart = 0.25f;
        [Tooltip("Kilic bu noktadan sonra hasar vermez")]
        [Range(0f, 1f)] public float hitWindowEnd = 0.5f;

        [Header("Hasar & Geri Tepme")]
        public float damage = 10f;
        [Tooltip("Dusmanin dengesini (poise) bozma gucu")]
        public float poiseDamage = 15f;
        [Tooltip("Direnci 0 olan bir dusmanin metre cinsinden itilecegi mesafe")]
        public float knockbackDistance = 0.8f;

        [Header("Vurus Hissi")]
        [Tooltip("Vurus aninda oyuncu + dusman animasyonunun donma suresi (sn)")]
        public float hitStopTime = 0.06f;
        [Range(0f, 1f)] public float cameraShake = 0.18f;
    }

    private CharacterController controller;
    private Animator animator;
    private Transform cameraTransform;
    private TPSCamera tpsCamera;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 12f;

    [Header("Gravity & Jump")]
    public float gravity = -20f;
    public float jumpHeight = 1.5f;
    private float verticalVelocity;

    [Header("Combo Settings")]
    public AttackStep[] attacks = new AttackStep[]
    {
        new AttackStep { stateName = "Attack1", comboWindowStart = 0.45f, endTime = 0.85f, crossFadeIn = 0.08f,
                         hitWindowStart = 0.25f, hitWindowEnd = 0.50f, damage = 10f, poiseDamage = 15f, knockbackDistance = 0.8f, hitStopTime = 0.06f, cameraShake = 0.18f },
        new AttackStep { stateName = "Attack2", comboWindowStart = 0.45f, endTime = 0.85f, crossFadeIn = 0.08f,
                         hitWindowStart = 0.25f, hitWindowEnd = 0.50f, damage = 12f, poiseDamage = 18f, knockbackDistance = 1.0f, hitStopTime = 0.07f, cameraShake = 0.20f },
        new AttackStep { stateName = "Attack3", comboWindowStart = 0.50f, endTime = 0.90f, crossFadeIn = 0.08f,
                         hitWindowStart = 0.30f, hitWindowEnd = 0.60f, damage = 20f, poiseDamage = 40f, knockbackDistance = 2.5f, hitStopTime = 0.12f, cameraShake = 0.40f },
    };

    [Tooltip("Hareket blend tree state'inin ismi. Starter Assets icin: Idle Walk Run Blend")]
    public string locomotionState = "Idle Walk Run Blend";
    [Tooltip("Kombo bitince idle/yurumeye donus suresi")]
    public float exitCrossFade = 0.2f;
    [Tooltip("Erken basilan tik bu sure boyunca hatirlanir")]
    public float inputBufferTime = 0.5f;

    [Header("Attack Movement")]
    [Tooltip("Vurus basinda karakterin hedefe/yone donme hizi")]
    public float attackTurnSpeed = 20f;
    [Tooltip("Vurus basinda donusun surdugu sure")]
    public float attackTurnDuration = 0.2f;
    [Tooltip("Kilic klipleri root motion (ileri atilma) iceriyorsa ac")]
    public bool useAttackRootMotion = false;

    // --- KOMBO DURUMU ---
    private bool isAttacking = false;
    private int comboIndex = -1;
    private float bufferTimer = 0f;
    private float attackStartTime = 0f;
    private float turnTimer = 0f;
    private Vector3 attackFacing = Vector3.forward;
    private int[] attackHashes;
    private int locomotionHash;

    public bool IsAttacking => isAttacking;
    public int ComboIndex => comboIndex;

    // Her yeni vurusta artar: SwordHitbox "bu savurmada kime vurdum" listesini buna gore sifirlar
    private int swingId = 0;
    public int SwingId => swingId;

    // Kilic hitbox'i icin: su an hasar penceresi acik mi? Aciksa o vurusun ayarlarini verir.
    public bool TryGetActiveHit(out AttackStep step)
    {
        step = null;
        if (!isAttacking || comboIndex < 0) return false;

        float progress;
        if (!GetAttackProgress(out progress)) return false;

        AttackStep s = attacks[comboIndex];
        if (progress < s.hitWindowStart || progress > s.hitWindowEnd) return false;

        step = s;
        return true;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            tpsCamera = Camera.main.GetComponent<TPSCamera>();
        }

        // State isimlerini onceden hash'le ve Animator'da var mi diye dogrula
        attackHashes = new int[attacks.Length];
        for (int i = 0; i < attacks.Length; i++)
        {
            attackHashes[i] = Animator.StringToHash(attacks[i].stateName);
            if (!animator.HasState(0, attackHashes[i]))
            {
                Debug.LogError($"[PlayerMovement] Animator'da '{attacks[i].stateName}' state'i bulunamadi (Base Layer).", this);
            }
        }

        locomotionHash = Animator.StringToHash(locomotionState);
        if (!animator.HasState(0, locomotionHash))
        {
            Debug.LogError($"[PlayerMovement] Animator'da '{locomotionState}' state'i bulunamadi. 'locomotionState' alanini duzelt.", this);
        }
    }

    void Update()
    {
        if (controller == null || !controller.enabled) return;

        // --- GIRIS ---
        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical += 1f;
        }

        Vector3 inputDir = new Vector3(horizontal, 0f, vertical).normalized;

        // Kamera yonune gore hedef hareket yonu
        Vector3 desiredDir = Vector3.zero;
        if (inputDir.magnitude >= 0.1f)
        {
            Vector3 camForward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;

            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            desiredDir = (camForward * inputDir.z) + (camRight * inputDir.x);
        }

        // --- KOMBO ---
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            bufferTimer = inputBufferTime;
        }
        if (bufferTimer > 0f) bufferTimer -= Time.deltaTime;

        HandleCombo(desiredDir);

        // --- ZIPLAMA / KOSMA ---
        bool isRunning = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        float currentSpeed = isRunning ? moveSpeed * 1.5f : moveSpeed;

        // Vurus sirasinda zıplama yok
        bool jumpPressed = !isAttacking && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool jumpAnimationState = false;

        if (controller.isGrounded)
        {
            verticalVelocity = -2f;

            if (jumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpAnimationState = true;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        // --- HAREKET & DONUS ---
        Vector3 moveDirection = Vector3.zero;

        if (isAttacking)
        {
            // Vurus basinda kisa sure hedefe/yone hizla don, sonra sabit kal
            if (turnTimer > 0f)
            {
                turnTimer -= Time.deltaTime;
                Quaternion attackRot = Quaternion.LookRotation(attackFacing);
                transform.rotation = Quaternion.Slerp(transform.rotation, attackRot, attackTurnSpeed * Time.deltaTime);
            }
            // Yatay hareket yok (root motion aciksa OnAnimatorMove halleder)
        }
        else if (desiredDir.sqrMagnitude > 0.01f)
        {
            moveDirection = desiredDir;
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        Vector3 finalMove = moveDirection * currentSpeed;
        finalMove.y = verticalVelocity;

        controller.Move(finalMove * Time.deltaTime);

        // --- ANIMATOR PARAMETRELERI ---
        float animationSpeed = inputDir.magnitude * (isRunning ? 6.0f : 2.0f);

        animator.SetFloat("Speed", animationSpeed, 0.1f, Time.deltaTime);
        animator.SetFloat("MotionSpeed", 1f);
        animator.SetBool("Grounded", controller.isGrounded);
        animator.SetBool("Jump", jumpAnimationState);
        animator.SetBool("FreeFall", !controller.isGrounded && verticalVelocity < 0f && !isAttacking);
    }

    // ------------------------------------------------------------
    //  KOMBO MANTIGI
    // ------------------------------------------------------------
    private void HandleCombo(Vector3 desiredDir)
    {
        if (attacks == null || attacks.Length == 0) return;

        // 1) Bufferlanmis bir tik varsa degerlendir
        if (bufferTimer > 0f)
        {
            if (!isAttacking)
            {
                // Yerdeyken yeni kombo baslat
                if (controller.isGrounded) StartAttack(0, desiredDir);
            }
            else
            {
                // Pencere acildiysa bir sonraki vurusa gec
                bool hasNext = comboIndex < attacks.Length - 1;
                float progress;
                if (hasNext && GetAttackProgress(out progress) && progress >= attacks[comboIndex].comboWindowStart)
                {
                    StartAttack(comboIndex + 1, desiredDir);
                }
            }
        }

        // 2) Vurus bitis kontrolu
        if (isAttacking)
        {
            float progress;
            if (GetAttackProgress(out progress))
            {
                if (progress >= attacks[comboIndex].endTime) EndCombo();
            }
            else if (Time.time - attackStartTime > 0.5f)
            {
                // Guvenlik: state hic girilmediyse (yanlis isim vb.) takili kalma
                EndCombo();
            }
        }
    }

    private void StartAttack(int index, Vector3 desiredDir)
    {
        comboIndex = index;
        swingId++;
        isAttacking = true;
        bufferTimer = 0f;
        attackStartTime = Time.time;

        // Yuz yonu: kilitli dusman varsa ona, yoksa girilen yone
        Vector3 face = Vector3.zero;
        if (tpsCamera != null && tpsCamera.IsLockedOn && tpsCamera.LockedTarget != null)
        {
            face = tpsCamera.LockedTarget.position - transform.position;
            face.y = 0f;
        }
        else if (desiredDir.sqrMagnitude > 0.01f)
        {
            face = desiredDir;
        }

        if (face.sqrMagnitude > 0.01f)
        {
            attackFacing = face.normalized;
            turnTimer = attackTurnDuration;
        }
        else
        {
            turnTimer = 0f;
        }

        animator.CrossFadeInFixedTime(attackHashes[index], attacks[index].crossFadeIn, 0, 0f);
    }

    private void EndCombo()
    {
        isAttacking = false;
        comboIndex = -1;
        bufferTimer = 0f;
        animator.CrossFadeInFixedTime(locomotionHash, exitCrossFade, 0, 0f);
    }

    // Su an oynayan (veya gecisle girilmekte olan) vurusun ilerlemesi (0-1).
    // Istenen vurus state'ine henuz girilmediyse false doner.
    private bool GetAttackProgress(out float normalizedTime)
    {
        normalizedTime = 0f;
        if (comboIndex < 0) return false;

        AnimatorStateInfo info = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);

        if (info.shortNameHash != attackHashes[comboIndex]) return false;

        normalizedTime = info.normalizedTime;
        return true;
    }

    // Bu fonksiyon tanimli oldugu icin Unity root motion'i otomatik uygulamaz;
    // sadece vurus sirasinda ve secenek aciksa biz uygulariz.
    private void OnAnimatorMove()
    {
        if (useAttackRootMotion && isAttacking && controller != null && controller.enabled)
        {
            Vector3 delta = animator.deltaPosition;
            delta.y = 0f;
            controller.Move(delta);
        }
    }
}