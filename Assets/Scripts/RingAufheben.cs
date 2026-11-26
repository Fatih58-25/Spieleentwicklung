using UnityEngine;

public class RingAufheben : InteraktionsZone
{
    [Header("Ring")]
    public GameObject ringObjekt;
    public string hinweis = "[E] Ring nehmen";
    public string erfolgsMeldung = "Du nimmst den Ring an dich. Öffne mit I dein Inventar, um ihn anzuziehen.";
    public AudioClip aufnehmenSound;

    bool genommen;

    protected override float MindestReichweite
    {
        get { return 1.4f; }
    }

    protected override string HinweisText()
    {
        return hinweis;
    }

    protected override bool KannInteragieren()
    {
        return !genommen;
    }

    protected override void Interagieren()
    {
        genommen = true;
        Inventar.Hinzufuegen("Ring", "Besonderer Ring", "Der Ring deines Stammes. Wer ihn trägt, ist würdig.", GegenstandArt.Ring, null);
        if (aufnehmenSound != null) AudioSource.PlayClipAtPoint(aufnehmenSound, transform.position);
        if (ringObjekt != null) ringObjekt.SetActive(false);
        Beenden();
        Meldung(erfolgsMeldung, 4f);
    }
}
