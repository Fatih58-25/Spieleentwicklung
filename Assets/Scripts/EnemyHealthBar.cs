using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tek bir dusmanin can bari. Kendi Update/LateUpdate'i YOK:
/// EnemyHealthBarManager her kare Tick() cagirir (tek dongu, tek kontrol noktasi).
///
/// Can degisimini event yerine dogrudan hedeften okur (polling). Boylece hasar, iyilesme,
/// respawn gibi her durum ayni mekanizmayla calisir; abone olma/cikma (leak) derdi yok.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class EnemyHealthBar : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("On plandaki kirmizi bar")]
    public Image mainFill;
    [Tooltip("Arkadaki sari 'kayip can' bari")]
    public Image damageFill;
    public CanvasGroup canvasGroup;

    public EnemyHitReceiver Target { get; private set; }
    public int TargetId { get; private set; }

    /// <summary>Dusuk deger = once elden cikarilmaya aday (kapasite dolunca en eskiyi atmak icin).</summary>
    public float Priority => hiding ? -1f : hideTimer;

    private RectTransform rect;

    private float mainValue;       // kirmizi bar (anlik)
    private float damageValue;     // sari bar (gecikmeli)
    private float damageVelocity;

    private float delayTimer;
    private float hideTimer;
    private float deathTimer;
    private bool hiding;

    private float alpha;
    private float punch;

    private float occlusionTimer;
    private bool occluded;

    // ------------------------------------------------------------
    //  YASAM DONGUSU (Manager cagirir)
    // ------------------------------------------------------------
    public void Setup(EnemyHitReceiver target, float startPercent, float hideDelay)
    {
        EnsureInit();

        Target = target;
        TargetId = target.GetInstanceID();

        mainValue = startPercent;
        damageValue = startPercent;
        damageVelocity = 0f;

        delayTimer = 0f;
        hideTimer = hideDelay;
        deathTimer = 0f;
        hiding = false;

        alpha = 0f;
        punch = 0f;
        occlusionTimer = 0f;
        occluded = false;

        canvasGroup.alpha = 0f;
        SetFill(mainFill, mainValue);
        SetFill(damageFill, damageValue);

        gameObject.SetActive(true);
    }

    public void Release()
    {
        Target = null;
        gameObject.SetActive(false);
    }

    /// <summary>Bar'i yumusakca kaybolmaya baslat (kaybolunca manager havuza geri alir).</summary>
    public void RequestHide()
    {
        hiding = true;
    }

    /// <summary>false donerse bar isini bitirmistir, manager havuza almalidir.</summary>
    public bool Tick(float dt, Camera cam, EnemyHealthBarManager m)
    {
        // Dusman yok edilmis
        if (Target == null) return false;

        // 1) CAN DEGISIMI: azalma = hasar -> sari bar bekler, bar gorunur, punch animasyonu
        float p = Target.HealthPercentage;
        if (p < mainValue - 0.0001f)
        {
            delayTimer = m.damageDelay;
            hideTimer = m.hideDelay;
            damageVelocity = 0f;
            punch = 1f;
            hiding = false;
        }
        mainValue = p;
        if (damageValue < mainValue) damageValue = mainValue; // iyilesmede sari bar kirmizinin altinda kalmasin

        // 2) SARI BAR: once bekle, sonra yumusakca kirmiziya in
        if (delayTimer > 0f)
        {
            delayTimer -= dt;
        }
        else if (damageValue > mainValue)
        {
            damageValue = Mathf.SmoothDamp(damageValue, mainValue, ref damageVelocity, m.drainSmoothTime, Mathf.Infinity, dt);
            if (damageValue - mainValue < 0.002f)
            {
                damageValue = mainValue;
                damageVelocity = 0f;
            }
        }

        SetFill(mainFill, mainValue);
        SetFill(damageFill, damageValue);

        // 3) GIZLENME: hasar almayinca bir sure sonra / olunce son hasar dolduktan sonra
        if (Target.IsDead)
        {
            deathTimer += dt;
            if (deathTimer >= m.deathLingerTime) hiding = true;
        }
        else if (!hiding)
        {
            hideTimer -= dt;
            if (hideTimer <= 0f) hiding = true;
        }

        // 4) EKRAN POZISYONU + GORUNURLUK
        Vector3 worldPos = Target.HealthBarWorldPosition;
        Vector3 sp = cam.WorldToScreenPoint(worldPos);
        float dist = sp.z;
        bool behind = dist <= 0.1f;

        bool visible = !behind
                       && dist < m.maxVisibleDistance
                       && sp.x > -m.screenEdgePadding && sp.x < Screen.width + m.screenEdgePadding
                       && sp.y > -m.screenEdgePadding && sp.y < Screen.height + m.screenEdgePadding;

        // Duvar arkasindaysa gizle (opsiyonel, 0.1 sn'de bir kontrol)
        if (visible && m.occlusionMask.value != 0)
        {
            occlusionTimer -= dt;
            if (occlusionTimer <= 0f)
            {
                occlusionTimer = 0.1f;
                occluded = Physics.Linecast(cam.transform.position, worldPos, m.occlusionMask, QueryTriggerInteraction.Ignore);
            }
            if (occluded) visible = false;
        }

        // 5) ALPHA: gorunurken hizli ac, gizlenirken yavas kapat; uzakta mesafeye gore sol
        float targetAlpha = (hiding || !visible)
            ? 0f
            : 1f - Mathf.InverseLerp(m.fadeStartDistance, m.maxVisibleDistance, dist);

        float speed = targetAlpha > alpha ? m.fadeInSpeed : m.fadeOutSpeed;
        alpha = behind ? 0f : Mathf.MoveTowards(alpha, targetAlpha, speed * dt);
        canvasGroup.alpha = alpha;

        if (hiding && alpha <= 0.001f) return false;

        // 6) POZISYON + OLCEK (punch efekti dahil)
        if (!behind && alpha > 0.001f)
        {
            rect.position = new Vector3(sp.x, sp.y, 0f);

            punch = Mathf.MoveTowards(punch, 0f, dt * m.punchSpeed);

            float s = m.scaleWithDistance
                ? Mathf.Clamp(m.referenceDistance / dist, m.minScale, m.maxScale)
                : 1f;
            s *= 1f + punch * m.punchScale;

            rect.localScale = new Vector3(s, s, 1f);
        }

        return true;
    }

    // ------------------------------------------------------------
    //  YARDIMCILAR
    // ------------------------------------------------------------
    private void EnsureInit()
    {
        if (rect != null) return;

        rect = (RectTransform)transform;

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    // Hem 'Filled' tipi Image (kendi sprite'inla yaptigin bar) hem de duz renkli bar'i destekler
    private static void SetFill(Image img, float value)
    {
        if (img == null) return;
        value = Mathf.Clamp01(value);

        if (img.type == Image.Type.Filled)
        {
            img.fillAmount = value;
        }
        else
        {
            RectTransform r = img.rectTransform;
            Vector2 max = r.anchorMax;
            max.x = value;
            r.anchorMax = max;
        }
    }
}