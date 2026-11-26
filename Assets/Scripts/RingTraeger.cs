using UnityEngine;

public class RingTraeger : MonoBehaviour
{
    public static RingTraeger Instanz { get; private set; }

    public bool traegtRing;
    public GameObject ringAnzeige;

    public static bool HatRing
    {
        get { return Instanz != null && Instanz.traegtRing; }
    }

    void Awake()
    {
        Instanz = this;
        Aktualisieren();
    }

    void OnDestroy()
    {
        if (Instanz == this) Instanz = null;
    }

    public void RingAnlegen()
    {
        traegtRing = true;
        Aktualisieren();
    }

    public void RingAblegen()
    {
        traegtRing = false;
        Aktualisieren();
    }

    void Aktualisieren()
    {
        if (ringAnzeige != null) ringAnzeige.SetActive(traegtRing);
    }
}
