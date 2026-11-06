using UnityEngine;

// Bir vurusun tum bilgisi: kilic -> dusman arasinda tasinir
public struct HitInfo
{
    public float damage;
    public float poiseDamage;
    [Tooltip("Direnci 0 olan dusmanin metre cinsinden itilecegi mesafe")]
    public float knockbackDistance;
    public float hitStopTime;

    public Vector3 point;      // temas noktasi
    public Vector3 normal;     // yuzeyden kilica dogru (kivilcim yonu icin)
    public Vector3 direction;  // geri tepme yonu (yatay)

    public GameObject attacker;
}

// Hasar alabilen her sey (robot, orumcek, dinozor, kirilabilir obje...) bunu uygular
public interface IDamageable
{
    Animator HitAnimator { get; }
    bool TakeHit(HitInfo hit);
}