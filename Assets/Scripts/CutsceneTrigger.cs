using UnityEngine;
using UnityEngine.Playables; // Wird für die Timeline (PlayableDirector) gebraucht

public class CutsceneTrigger : MonoBehaviour
{
    [Header("Cutscene")]
    public PlayableDirector cutscene; // Die Timeline-Cutscene (kommt später)

    [Header("Optional")]
    public MonoBehaviour spielerSteuerung; // Das Bewegungs-Script des Spielers
    public bool nurEinmalAbspielen = true;

    private bool schonGespielt = false;

    // Wird aufgerufen, wenn etwas den Trigger berührt
    private void OnTriggerEnter(Collider other)
    {
        // Nur reagieren, wenn es der Spieler ist
        if (!other.CompareTag("Player")) return;

        // Nicht nochmal abspielen, wenn es nur einmal laufen soll
        if (nurEinmalAbspielen && schonGespielt) return;
        schonGespielt = true;

        Debug.Log("Cutscene gestartet!");

        // Spielersteuerung ausschalten, solange die Cutscene läuft
        if (spielerSteuerung != null)
        {
            spielerSteuerung.enabled = false;
        }

        if (cutscene != null)
        {
            cutscene.stopped += CutsceneBeendet; // Bescheid sagen, wenn sie fertig ist
            cutscene.Play();
        }
        else
        {
            // Noch keine Cutscene zugewiesen, also gleich wieder freigeben
            CutsceneBeendet(null);
        }
    }

    // Wird aufgerufen, wenn die Cutscene zu Ende ist
    private void CutsceneBeendet(PlayableDirector director)
    {
        if (cutscene != null)
        {
            cutscene.stopped -= CutsceneBeendet;
        }

        if (spielerSteuerung != null)
        {
            spielerSteuerung.enabled = true;
        }

        Debug.Log("Cutscene beendet!");
    }
}