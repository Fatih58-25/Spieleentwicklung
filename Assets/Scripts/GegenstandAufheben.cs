using UnityEngine;

public class GegenstandAufheben : InteraktionsZone
{
    [Header("Gegenstand")]
    public GameObject objekt;
    public string gegenstandName = "Schluessel";
    public string anzeigeName;
    [TextArea(2, 4)]
    public string beschreibung;
    public string hinweis = "[E] Schlüssel nehmen";
    public string meldung = "Du nimmst den Schlüssel an dich.";
    public AudioClip aufnehmenSound;

    bool genommen;

    public bool Genommen
    {
        get { return genommen; }
    }

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
        string standardName;
        string standardBeschreibung;
        Inventar.StandardInfo(gegenstandName, out standardName, out standardBeschreibung);
        string n = string.IsNullOrEmpty(anzeigeName) ? standardName : anzeigeName;
        string b = string.IsNullOrEmpty(beschreibung) ? standardBeschreibung : beschreibung;
        Inventar.Hinzufuegen(gegenstandName, n, b, GegenstandArt.Schluessel, objekt);
        if (aufnehmenSound != null) AudioSource.PlayClipAtPoint(aufnehmenSound, transform.position);
        if (objekt != null) objekt.SetActive(false);
        Beenden();
        Meldung(Inventar.MitHinweis(meldung), 3.5f);
    }
}
