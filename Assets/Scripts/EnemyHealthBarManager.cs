using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tum dusman can barlarini TEK canvas ve TEK dongu ile yonetir.
///
/// - Kurulum gerektirmez: sahnede yoksa oyun baslarken kendini olusturur (canvas + varsayilan bar dahil).
///   Renk/boyut ayari icin sahneye bos bir objeye ekleyip Inspector'dan degistirebilirsin.
/// - Bir dusman hasar alinca (EnemyHitReceiver.AnyDamaged) bar havuzdan gelir, hasar almayinca kaybolur.
/// - Havuz (pool) kullanir, es zamanli bar sayisi sinirlidir.
/// </summary>
[DefaultExecutionOrder(1000)] // Kamera (TPSCamera.LateUpdate) hareket ettikten SONRA calissin, yoksa bar'lar titrer
public class EnemyHealthBarManager : MonoBehaviour
{
    public static EnemyHealthBarManager Instance { get; private set; }

    [Header("Kurulum (hepsi opsiyonel)")]
    [Tooltip("Kendi can bari prefab'in. Bos birakirsan kod ile varsayilan bar olusturulur.")]
    public EnemyHealthBar barPrefab;
    [Tooltip("Bos birakirsan Screen Space - Overlay canvas otomatik olusturulur. Kendi canvas'in da Overlay olmali.")]
    public Canvas canvas;
    [Tooltip("Bos = Camera.main")]
    public Camera cam;
    public int canvasSortingOrder = 5;

    [Header("Varsayilan Bar Gorunumu (prefab yoksa)")]
    public Vector2 barSize = new Vector2(120f, 12f);
    public float barPadding = 2f;
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);
    public Color healthColor = new Color(0.85f, 0.1f, 0.1f, 1f);
    public Color damageColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Header("Davranis")]
    [Tooltip("Hasar almayinca bar kac saniye sonra kaybolur")]
    public float hideDelay = 4f;
    [Tooltip("Dusman olunce bar kac saniye daha gorunur (son hasar dolsun)")]
    public float deathLingerTime = 1f;
    [Tooltip("Hasardan sonra sari bar'in beklemesi")]
    public float damageDelay = 0.45f;
    [Tooltip("Sari bar'in kirmiziya inis yumusakligi (kucuk = hizli)")]
    public float drainSmoothTime = 0.3f;
    public float fadeInSpeed = 10f;
    public float fadeOutSpeed = 3f;
    [Tooltip("Hasar aninda bar'in kisa sureli buyume miktari")]
    public float punchScale = 0.15f;
    public float punchSpeed = 6f;

    [Header("Gorunurluk")]
    public float maxVisibleDistance = 30f;
    [Tooltip("Bu mesafeden sonra bar solmaya baslar")]
    public float fadeStartDistance = 22f;
    public float screenEdgePadding = 50f;
    public bool scaleWithDistance = true;
    public float referenceDistance = 10f;
    public float minScale = 0.55f;
    public float maxScale = 1.15f;
    [Tooltip("Bina/duvar layer'lari. Doluysa duvar arkasindaki dusmanin bari gizlenir. Dusman ve Player layer'larini SECME.")]
    public LayerMask occlusionMask = 0;
    [Tooltip("Ayni anda en fazla kac bar. Dolunca en eski/sona yaklasan bar atilir.")]
    public int maxSimultaneousBars = 12;

    private readonly List<EnemyHealthBar> active = new List<EnemyHealthBar>();
    private readonly Dictionary<int, EnemyHealthBar> barsById = new Dictionary<int, EnemyHealthBar>();
    private readonly Stack<EnemyHealthBar> pool = new Stack<EnemyHealthBar>();

    private bool subscribed;

    // Sahnede manager yoksa oyun basinda otomatik olustur (sifir kurulum)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;

        GameObject go = new GameObject("EnemyHealthBarManager (Auto)");
        DontDestroyOnLoad(go);
        go.AddComponent<EnemyHealthBarManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        EnsureCanvas();
        if (barPrefab == null) barPrefab = BuildDefaultTemplate();
    }

    void OnEnable()
    {
        if (Instance != this || subscribed) return;
        EnemyHitReceiver.AnyDamaged += OnEnemyDamaged;
        subscribed = true;
    }

    void OnDisable()
    {
        if (!subscribed) return;
        EnemyHitReceiver.AnyDamaged -= OnEnemyDamaged;
        subscribed = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------
    //  PUBLIC API
    // ------------------------------------------------------------

    /// <summary>Bar'i zorla goster (ornegin dusman oyuncuyu fark edince). Hasar gelmesine gerek yok.</summary>
    public void ShowBar(EnemyHitReceiver enemy)
    {
        if (enemy == null || !enemy.showHealthBar || enemy.IsDead) return;
        CreateBarFor(enemy, enemy.HealthPercentage);
    }

    /// <summary>Bar'i yumusakca kaybet.</summary>
    public void HideBar(EnemyHitReceiver enemy)
    {
        if (enemy == null) return;

        EnemyHealthBar bar;
        if (barsById.TryGetValue(enemy.GetInstanceID(), out bar)) bar.RequestHide();
    }

    // ------------------------------------------------------------

    private void OnEnemyDamaged(EnemyHitReceiver enemy, float lost)
    {
        if (enemy == null || !enemy.showHealthBar) return;

        // Zaten bar varsa bir sey yapma: bar can degisimini kendi okuyup sari bar / sure / punch'i halleder
        if (barsById.ContainsKey(enemy.GetInstanceID())) return;

        // Ilk vurus: sari bar HASARDAN ONCEKI cani gostersin ki kaybedilen kisim gorunsun
        float startPercent = enemy.MaxHealth > 0f
            ? Mathf.Clamp01((enemy.CurrentHealth + lost) / enemy.MaxHealth)
            : 1f;

        CreateBarFor(enemy, startPercent);
    }

    private void CreateBarFor(EnemyHitReceiver enemy, float startPercent)
    {
        int id = enemy.GetInstanceID();
        if (barsById.ContainsKey(id)) return;

        if (active.Count >= maxSimultaneousBars) ReleaseOldest();

        EnemyHealthBar bar = pool.Count > 0 ? pool.Pop() : Instantiate(barPrefab, canvas.transform, false);
        bar.Setup(enemy, startPercent, hideDelay);

        active.Add(bar);
        barsById.Add(id, bar);
    }

    void LateUpdate()
    {
        if (active.Count == 0) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        float dt = Time.unscaledDeltaTime; // slow-motion'da UI animasyonlari normal aksin

        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (!active[i].Tick(dt, cam, this)) ReleaseAt(i);
        }
    }

    private void ReleaseAt(int index)
    {
        EnemyHealthBar bar = active[index];
        active.RemoveAt(index);
        barsById.Remove(bar.TargetId);

        bar.Release();
        pool.Push(bar);
    }

    private void ReleaseOldest()
    {
        if (active.Count == 0) return;

        int best = 0;
        float bestPriority = float.MaxValue;
        for (int i = 0; i < active.Count; i++)
        {
            float pr = active[i].Priority;
            if (pr < bestPriority)
            {
                bestPriority = pr;
                best = i;
            }
        }

        ReleaseAt(best);
    }

    // ------------------------------------------------------------
    //  OTOMATIK CANVAS + VARSAYILAN BAR
    // ------------------------------------------------------------
    private void EnsureCanvas()
    {
        if (canvas != null) return;

        GameObject cgo = new GameObject("EnemyHealthBarCanvas", typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);

        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        // GraphicRaycaster bilerek eklenmedi: bar'lar tiklama almaz, gereksiz maliyet yok
    }

    private EnemyHealthBar BuildDefaultTemplate()
    {
        GameObject root = new GameObject("HealthBarTemplate", typeof(RectTransform), typeof(CanvasGroup), typeof(EnemyHealthBar));
        root.transform.SetParent(transform, false);

        RectTransform rt = (RectTransform)root.transform;
        rt.sizeDelta = barSize;
        rt.pivot = new Vector2(0.5f, 0.5f);

        // Arka plan
        Image bg = CreateImage("Background", rt, backgroundColor);
        Stretch(bg.rectTransform, Vector2.zero, Vector2.zero);

        // Dolgu alani (kenar boslugu icin)
        RectTransform fillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(rt, false);
        Stretch(fillArea, new Vector2(barPadding, barPadding), new Vector2(-barPadding, -barPadding));

        // Sari (gecikmeli) once, kirmizi (anlik) ustte
        Image dmg = CreateImage("DamageFill", fillArea, damageColor);
        Stretch(dmg.rectTransform, Vector2.zero, Vector2.zero);

        Image main = CreateImage("MainFill", fillArea, healthColor);
        Stretch(main.rectTransform, Vector2.zero, Vector2.zero);

        EnemyHealthBar bar = root.GetComponent<EnemyHealthBar>();
        bar.mainFill = main;
        bar.damageFill = dmg;
        bar.canvasGroup = root.GetComponent<CanvasGroup>();

        root.SetActive(false); // sablon gorunmesin; klonlar Setup'ta acilir
        return bar;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}