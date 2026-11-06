using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vurus hissi (hit stop) yoneticisi. Sahnede elle koymana gerek yok, ilk kullanimda kendini olusturur.
///
/// Freeze: sadece verilen Animator'lari saliselik dondurur (oyuncu + vurulan dusman).
///         Dunya akmaya devam eder, kamera/parcaciklar donmaz. Genelde en iyi his budur.
/// SlowMotion: tum oyunu kisa sureligine yavaslatir (sadece oldurucu vurus gibi ozel anlar icin).
/// </summary>
public class HitStopManager : MonoBehaviour
{
    private static HitStopManager instance;
    private static HitStopManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("HitStopManager");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<HitStopManager>();
            }
            return instance;
        }
    }

    private class FreezeEntry
    {
        public float endTime;
        public float originalSpeed;
    }

    private readonly Dictionary<Animator, FreezeEntry> frozen = new Dictionary<Animator, FreezeEntry>();
    private readonly List<Animator> finished = new List<Animator>();

    private float slowEndTime = -1f;
    private float baseFixedDelta;

    void Awake()
    {
        baseFixedDelta = Time.fixedDeltaTime;
    }

    // ------------------------------------------------------------
    //  PUBLIC API
    // ------------------------------------------------------------

    /// <summary>Animator'u 'duration' saniye (gercek zaman) dondurur. speed=0 tam durma, 0.05 gibi degerler "agir cekim".</summary>
    public static void Freeze(Animator animator, float duration, float speed = 0f)
    {
        if (animator == null || duration <= 0f) return;
        Instance.FreezeInternal(animator, duration, speed);
    }

    /// <summary>Tum oyunu yavaslatir. Pause menusu timeScale'i degistiriyorsa dikkat et.</summary>
    public static void SlowMotion(float scale, float duration)
    {
        if (duration <= 0f) return;
        HitStopManager m = Instance;
        Time.timeScale = Mathf.Clamp(scale, 0.01f, 1f);
        Time.fixedDeltaTime = m.baseFixedDelta * Time.timeScale;
        m.slowEndTime = Time.unscaledTime + duration;
    }

    // ------------------------------------------------------------

    private void FreezeInternal(Animator animator, float duration, float speed)
    {
        float end = Time.unscaledTime + duration;

        FreezeEntry entry;
        if (frozen.TryGetValue(animator, out entry))
        {
            // Zaten donukse sadece sureyi uzat (orijinal hizi ezme)
            entry.endTime = Mathf.Max(entry.endTime, end);
        }
        else
        {
            frozen[animator] = new FreezeEntry { endTime = end, originalSpeed = animator.speed };
        }

        animator.speed = speed;
    }

    void Update()
    {
        float now = Time.unscaledTime; // timeScale'den etkilenmesin

        if (frozen.Count > 0)
        {
            finished.Clear();
            foreach (KeyValuePair<Animator, FreezeEntry> kv in frozen)
            {
                if (kv.Key == null || now >= kv.Value.endTime) finished.Add(kv.Key);
            }

            for (int i = 0; i < finished.Count; i++)
            {
                Animator a = finished[i];
                if (a != null) a.speed = frozen[a].originalSpeed;
                frozen.Remove(a);
            }
        }

        if (slowEndTime > 0f && now >= slowEndTime)
        {
            RestoreTime();
        }
    }

    private void RestoreTime()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDelta;
        slowEndTime = -1f;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            foreach (KeyValuePair<Animator, FreezeEntry> kv in frozen)
            {
                if (kv.Key != null) kv.Key.speed = kv.Value.originalSpeed;
            }
            frozen.Clear();
            if (slowEndTime > 0f) RestoreTime();
            instance = null;
        }
    }
}