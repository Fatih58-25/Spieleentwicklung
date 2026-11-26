using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class TPSCamera : MonoBehaviour
{
    [Header("References")]
    public Transform target;

    [Header("Distance & Shoulder Offset")]
    public float distance = 3.5f;
    public float height = 1.8f;
    public float cameraXOffset = 0.6f;
    [Tooltip("Daempft nur das Auf und Ab der Kamera (Huegel, Treppen). 0 = aus, 0.05 bis 0.15 ist ueblich.")]
    public float verticalSmoothTime = 0.08f;

    [Header("Collision Settings (Duvar Engelleyici)")]
    public bool enableCollision = true;
    [Tooltip("Oyuncunun KENDI layer'ini (Player) ve istersen Enemy layer'ini buradan CIKAR.")]
    public LayerMask collisionLayers = ~0;
    [Tooltip("Kameranin pivot'a yaklasabilecegi en kucak mesafe. Kucuk tut (0.1 civari).")]
    public float minDistance = 0.1f;
    [Tooltip("Near clip plane'den buyuk olmali. 0.2 - 0.3 ideal.")]
    public float cameraRadius = 0.25f;
    [Tooltip("Duvar ile kamera arasinda birakilan ekstra pay.")]
    public float collisionBuffer = 0.05f;
    [Tooltip("Duvardan uzaklasirken kameranin geri donus hizi.")]
    public float collisionSmoothSpeed = 8f;

    [Header("Camera Shake (Vurus Sarsintisi)")]
    [Tooltip("En yuksek sarsintida kameranin kayabilecegi mesafe (metre)")]
    public float shakeMaxOffset = 0.12f;
    [Tooltip("En yuksek sarsintida yatma acisi (derece)")]
    public float shakeMaxRoll = 2.5f;
    public float shakeFrequency = 25f;
    [Tooltip("Saniyede ne kadar trauma azalir. Buyudukce sarsinti daha kisa surer.")]
    public float traumaDecay = 2.2f;

    private float trauma = 0f;

    [Header("Rotation Settings")]
    public float mouseSensitivity = 2.0f;
    public float yMinLimit = -15f;
    public float yMaxLimit = 65f;

    [Header("Lock-On Settings")]
    public string enemyTag = "Enemy";
    public float maxLockOnDistance = 15f;
    public RectTransform lockOnUI;
    public float lockOnHeightOffset = 1.2f;
    public string customLockPointName = "Lock";

    private float currentRotationY = 0f;
    private float currentRotationX = 15f;

    // Carpisma icin dinamik takip (omuz + mesafe)
    private float currentDistance;
    private float currentShoulder;

    private Transform lockedTarget = null;
    private Transform actualLockPoint = null;
    private bool isLockedOn = false;

    private Camera cam;
    private Vector3 lockUIVelocity = Vector3.zero;
    private float smoothedPivotY;
    private float pivotYVelocity;
    private bool pivotInit;
    private Transform eigenerKoerper;
    private static readonly RaycastHit[] castPuffer = new RaycastHit[24];

    public bool IsLockedOn => isLockedOn;
    public Transform LockedTarget => lockedTarget;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        if (target)
        {
            currentRotationY = target.eulerAngles.y;
            var cc = target.GetComponentInParent<CharacterController>();
            eigenerKoerper = cc != null ? cc.transform : target;
        }

        currentDistance = distance;
        currentShoulder = cameraXOffset;

        if (lockOnUI != null) lockOnUI.gameObject.SetActive(false);
    }

    void Update()
    {
        bool lockPressed = Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;

        if (lockPressed)
        {
            if (isLockedOn) UnlockTarget();
            else FindBestTarget();
        }

        if (isLockedOn)
        {
            if (lockedTarget == null || Vector3.Distance(target.position, lockedTarget.position) > maxLockOnDistance)
            {
                UnlockTarget();
            }
        }
    }

    void LateUpdate()
    {
        if (!target) return;

        if (isLockedOn && lockedTarget != null)
        {
            Vector3 aimPosition = actualLockPoint != null ? actualLockPoint.position : (lockedTarget.position + Vector3.up * lockOnHeightOffset);
            Vector3 dirToEnemy = aimPosition - target.position;
            dirToEnemy.y = 0;

            if (dirToEnemy != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(dirToEnemy);
                currentRotationY = Mathf.LerpAngle(currentRotationY, lookRot.eulerAngles.y, 15f * Time.deltaTime);
            }

            if (lockOnUI != null && cam != null)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(aimPosition);
                bool inFront = screenPos.z > 0f;
                if (lockOnUI.gameObject.activeSelf != inFront) lockOnUI.gameObject.SetActive(inFront);
                if (inFront)
                {
                    lockOnUI.position = Vector3.SmoothDamp(lockOnUI.position, screenPos, ref lockUIVelocity, 0.02f);
                }
            }
        }
        else
        {
            float mouseX = 0f;
            float mouseY = 0f;

            if (Mouse.current != null)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                mouseX = mouseDelta.x;
                mouseY = mouseDelta.y;
            }

            currentRotationY += mouseX * mouseSensitivity;
            currentRotationX -= mouseY * mouseSensitivity;
            currentRotationX = Mathf.Clamp(currentRotationX, yMinLimit, yMaxLimit);
        }

        // 1. ROTASYON + PIVOT
        Quaternion rotation = Quaternion.Euler(currentRotationX, currentRotationY, 0);
        Vector3 pivot = target.position + Vector3.up * height;
        if (!pivotInit)
        {
            smoothedPivotY = pivot.y;
            pivotInit = true;
        }
        if (verticalSmoothTime > 0.0001f) smoothedPivotY = Mathf.SmoothDamp(smoothedPivotY, pivot.y, ref pivotYVelocity, verticalSmoothTime);
        else smoothedPivotY = pivot.y;
        pivot.y = smoothedPivotY;

        // 2. DUVAR CARPISMA HESABI
        float desiredShoulder = cameraXOffset;
        float desiredDistance = distance;

        if (enableCollision)
        {
            ResolveCollision(pivot, rotation, out desiredShoulder, out desiredDistance);
        }

        // Duvar yaklasirken ANINDA, uzaklasirken YUMUSAK (kare hizindan bagimsiz)
        float t = 1f - Mathf.Exp(-collisionSmoothSpeed * Time.deltaTime);
        currentShoulder = Approach(currentShoulder, desiredShoulder, t);
        currentDistance = Approach(currentDistance, desiredDistance, t);

        // 3. POZISYON UYGULAMA
        Vector3 localOffset = new Vector3(currentShoulder, 0f, -currentDistance);
        Vector3 position = pivot + (rotation * localOffset);

        // 4. SON GUVENLIK AGI: pivot -> kamera arasinda hala bir sey varsa kamerayi one cek
        if (enableCollision)
        {
            RaycastHit safetyHit;
            if (CastGefiltert(pivot, 0f, (position - pivot).normalized, Vector3.Distance(pivot, position), out safetyHit))
            {
                position = safetyHit.point + safetyHit.normal * cameraRadius;
            }
        }

        transform.rotation = rotation;
        transform.position = position;

        ApplyShake();
    }

    // ------------------------------------------------------------
    //  KAMERA SARSINTISI (trauma tabanli)
    // ------------------------------------------------------------

    /// <summary>0-1 arasi deger ekler. Sarsinti siddeti trauma^2 ile artar, yani buyuk vuruslar belirgin sert hisseder.</summary>
    public void AddShake(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    private void ApplyShake()
    {
        if (trauma <= 0f) return;

        float shake = trauma * trauma;
        float t = Time.unscaledTime * shakeFrequency;

        float x = (Mathf.PerlinNoise(t, 0.0f) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(0.0f, t) - 0.5f) * 2f;
        float r = (Mathf.PerlinNoise(t, t) - 0.5f) * 2f;

        transform.position += (transform.right * x + transform.up * y) * (shakeMaxOffset * shake);
        transform.rotation *= Quaternion.Euler(0f, 0f, r * shakeMaxRoll * shake);

        trauma = Mathf.Max(0f, trauma - traumaDecay * Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Iki asamali cast:
    /// A) pivot'tan omuz tarafina (kamera yana kaymis durumda, duvar varsa omzu daralt)
    /// B) o guvenli omuz noktasindan arkaya (duvar varsa mesafeyi kisalt)
    /// </summary>
    private void ResolveCollision(Vector3 pivot, Quaternion rotation, out float shoulder, out float dist)
    {
        shoulder = cameraXOffset;
        dist = distance;
        RaycastHit hit;

        // A) Omuz offseti kontrolu
        float absX = Mathf.Abs(cameraXOffset);
        if (absX > 0.001f)
        {
            float sign = Mathf.Sign(cameraXOffset);
            Vector3 sideDir = rotation * Vector3.right * sign;

            if (CastGefiltert(pivot, cameraRadius, sideDir, absX + collisionBuffer, out hit))
            {
                shoulder = sign * Mathf.Max(hit.distance - collisionBuffer, 0f);
            }
        }

        // B) Geri mesafe kontrolu (guvenli omuz noktasindan)
        Vector3 shoulderPivot = pivot + rotation * Vector3.right * shoulder;
        Vector3 backDir = rotation * Vector3.back;

        if (CastGefiltert(shoulderPivot, cameraRadius, backDir, distance + collisionBuffer, out hit))
        {
            dist = Mathf.Clamp(hit.distance - collisionBuffer, minDistance, distance);
        }
    }

    private bool CastGefiltert(Vector3 start, float radius, Vector3 richtung, float entfernung, out RaycastHit treffer)
    {
        int n = radius > 0f
            ? Physics.SphereCastNonAlloc(start, radius, richtung, castPuffer, entfernung, collisionLayers, QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(start, richtung, castPuffer, entfernung, collisionLayers, QueryTriggerInteraction.Ignore);
        treffer = default(RaycastHit);
        float nah = float.MaxValue;
        bool gefunden = false;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = castPuffer[i];
            if (eigenerKoerper != null && h.transform.IsChildOf(eigenerKoerper)) continue;
            if (h.distance < nah)
            {
                nah = h.distance;
                treffer = h;
                gefunden = true;
            }
        }
        return gefunden;
    }

    // Hedef daha yakinsa (duvar yaklasti) aninda atla, daha uzaksa yumusakca don
    private static float Approach(float current, float desired, float t)
    {
        if (Mathf.Abs(desired) < Mathf.Abs(current)) return desired;
        return Mathf.Lerp(current, desired, t);
    }

    private void FindBestTarget()
    {
        if (cam == null) return;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        Transform bestTarget = null;
        Transform bestLockPoint = null;
        float bestScore = Mathf.Infinity;

        foreach (GameObject enemy in enemies)
        {
            Transform childLockPoint = FindChildLockPoint(enemy.transform);
            Transform aimTransform = (childLockPoint != null) ? childLockPoint : enemy.transform;

            float physicalDist = Vector3.Distance(target.position, aimTransform.position);
            if (physicalDist > maxLockOnDistance) continue;

            Vector3 screenPos = cam.WorldToViewportPoint(aimTransform.position);
            if (screenPos.z < 0) continue;

            if (screenPos.x < 0 || screenPos.x > 1 || screenPos.y < 0 || screenPos.y > 1) continue;

            float distanceScore = physicalDist / maxLockOnDistance;
            float screenScore = Vector2.Distance(new Vector2(0.5f, 0.5f), new Vector2(screenPos.x, screenPos.y)) * 2f;
            float finalScore = (distanceScore * 0.6f) + (screenScore * 0.4f);

            if (finalScore < bestScore)
            {
                bestScore = finalScore;
                bestTarget = enemy.transform;
                bestLockPoint = childLockPoint;
            }
        }

        if (bestTarget != null)
        {
            lockedTarget = bestTarget;
            actualLockPoint = bestLockPoint;
            isLockedOn = true;
            if (lockOnUI != null) lockOnUI.gameObject.SetActive(true);
        }
    }

    private Transform FindChildLockPoint(Transform parent)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>())
        {
            if (child != parent && child.name.ToLower().Contains(customLockPointName.ToLower()))
            {
                return child;
            }
        }
        return null;
    }

    private void UnlockTarget()
    {
        lockedTarget = null;
        actualLockPoint = null;
        isLockedOn = false;
        lockUIVelocity = Vector3.zero;
        if (lockOnUI != null) lockOnUI.gameObject.SetActive(false);
    }
}