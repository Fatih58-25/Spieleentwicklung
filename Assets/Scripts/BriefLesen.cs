using UnityEngine;

public class BriefLesen : InteraktionsZone
{
    [Header("Brief")]
    public GameObject objekt;
    public string titel = "Ein Brief von Opa";
    [TextArea(10, 30)]
    public string text =
        "Mein lieber Enkel,\n\n" +
        "wenn du diese Zeilen liest, bin ich weit weg und kann dir nicht mehr alles selbst erzählen.\n\n" +
        "Du stammst von einem auserwählten Stamm ab. Seit vielen Generationen wird sein Erbe weitergegeben, und du bist der Nächste, der es tragen darf. " +
        "Du bist würdig, diesen Ring zu tragen.\n\n" +
        "In diesem Paket liegen der Ring und der erste von drei alten Schlüsseln. Trage den Ring immer bei dir. Wer ihn nicht trägt, dem verschließen sich Dinge, die für dich bestimmt sind.\n\n" +
        "Der erste Schlüssel öffnet die Tore der Stadt. Die beiden anderen habe ich vor langer Zeit weit verstreut: einer liegt irgendwo in der Wüste, der andere im grünen Land. " +
        "Du musst sie finden, denn nur mit beiden kannst du die Maschinen abschalten.\n\n" +
        "Sei mutig und vertraue dem Weg.\n\n" +
        "Dein Opa";

    [Header("Texte")]
    public string hinweis = "[E] Brief nehmen";
    public string meldung = "Du nimmst den Brief an dich. Öffne mit I dein Inventar, um ihn zu lesen.";

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
        Inventar.Hinzufuegen("Brief", "Brief von Opa", "Ein Brief von deinem Opa. Lies ihn mit der linken Maustaste.", GegenstandArt.Brief, null, titel, text);
        if (objekt == null && transform.parent != null)
        {
            var geschwister = transform.parent.Find("Paket_Brief");
            if (geschwister != null) objekt = geschwister.gameObject;
        }
        if (objekt != null) objekt.SetActive(false);
        Beenden();
        Meldung(Inventar.MitHinweis(meldung), 4f);
    }
}
