using System.Collections;
using UnityEngine;

public class PaketOeffnen : InteraktionsZone
{
    [Header("Paket")]
    public GameObject deckel;
    public GameObject[] inhalt;
    public Animator animator;
    public string animatorParameter = "IsOpen";
    public float inhaltVerzoegerung;

    [Header("Texte")]
    public string hinweis = "[E] Paket öffnen";
    public string meldung = "Ein Paket von deinem Opa. Darin liegen ein Brief, ein Schlüssel und ein Ring.";

    bool offen;

    protected override string HinweisText()
    {
        return hinweis;
    }

    protected override bool KannInteragieren()
    {
        return !offen;
    }

    protected override void Interagieren()
    {
        offen = true;
        if (deckel != null) deckel.SetActive(false);
        if (animator != null && !string.IsNullOrEmpty(animatorParameter)) animator.SetBool(animatorParameter, true);
        Beenden();
        Meldung(meldung, 5f);
        StartCoroutine(InhaltZeigen());
    }

    IEnumerator InhaltZeigen()
    {
        if (inhaltVerzoegerung > 0f) yield return new WaitForSeconds(inhaltVerzoegerung);
        foreach (var o in inhalt)
        {
            if (o != null) o.SetActive(true);
        }
    }
}
