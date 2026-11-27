#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PatrouilleEinrichten
{
    [MenuItem("Spiel/Maschinen-Patrouille auf Auswahl legen")]
    public static void AufAuswahl()
    {
        var auswahl = Selection.gameObjects;
        if (auswahl == null || auswahl.Length == 0)
        {
            Debug.LogWarning("Markiere zuerst eine oder mehrere Maschinen in der Hierarchie.");
            return;
        }
        int n = 0;
        var berichte = new List<string>();
        foreach (var go in auswahl)
        {
            if (go.scene.IsValid() == false) continue;
            var p = go.GetComponent<MaschinenPatrouille>();
            if (p == null) p = Undo.AddComponent<MaschinenPatrouille>(go);
            else Undo.RecordObject(p, "Patrouille einstellen");
            float hoehe = 2f;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                p.koerperRadius = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.45f, 0.4f, 12f);
                p.koerperHoehe = Mathf.Clamp(b.size.y, 0.8f, 30f);
                p.sichtweite = Mathf.Max(10f, p.koerperRadius * 6f);
                p.sicherheitsabstand = Mathf.Max(2.5f, p.koerperRadius * 1.6f);
                p.ankunftsAbstand = Mathf.Max(2f, p.koerperRadius);
                hoehe = p.koerperHoehe;
            }
            EditorUtility.SetDirty(p);
            berichte.Add(go.name + ": " + LaufanimationEinrichten(go, hoehe));
            n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Spiel: Patrouille auf " + n + " Objekt(e) gelegt. Körpergröße, Sichtweite und Sicherheitsabstand wurden aus dem Modell geschätzt.\n" + string.Join("\n", berichte));
    }

    [MenuItem("Spiel/Maschinen-Laufanimation auf Auswahl legen")]
    public static void NurAnimation()
    {
        var auswahl = Selection.gameObjects;
        if (auswahl == null || auswahl.Length == 0)
        {
            Debug.LogWarning("Markiere zuerst eine oder mehrere Maschinen in der Hierarchie.");
            return;
        }
        var berichte = new List<string>();
        foreach (var go in auswahl)
        {
            if (go.scene.IsValid() == false) continue;
            var p = go.GetComponent<MaschinenPatrouille>();
            if (p == null) p = Undo.AddComponent<MaschinenPatrouille>(go);
            berichte.Add(go.name + ": " + LaufanimationEinrichten(go, Mathf.Max(0.8f, p.koerperHoehe)));
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Spiel: Laufanimation eingerichtet.\n" + string.Join("\n", berichte));
    }

    public static string LaufanimationEinrichten(GameObject go, float hoehe)
    {
        var la = go.GetComponent<MaschinenLaufanimation>();
        if (la == null) la = Undo.AddComponent<MaschinenLaufanimation>(go);
        else Undo.RecordObject(la, "Laufanimation einstellen");
        la.modus = MaschinenLaufanimation.Modus.Automatisch;
        la.laufClip = null;
        la.stehClip = null;
        la.animationsWurzel = null;
        la.schrittLaenge = Mathf.Max(0.8f, hoehe * 1.2f);
        la.wippHoehe = Mathf.Clamp(hoehe * 0.025f, 0.02f, 0.4f);
        la.rotoren = null;
        string meldung;

        var animator = go.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            string text = ZustaendeUebernehmen(animator.runtimeAnimatorController, la);
            if (text != null)
            {
                EditorUtility.SetDirty(la);
                return text;
            }
        }

        string fbx = null;
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.sharedMesh == null) continue;
            string pf = AssetDatabase.GetAssetPath(r.sharedMesh);
            if (pf.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                fbx = pf;
                break;
            }
        }
        if (fbx != null)
        {
            AnimationClip lauf = null, steh = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
            {
                var c = o as AnimationClip;
                if (c == null || c.name.StartsWith("__preview") || c.name.EndsWith("_BAKED")) continue;
                string cn = c.name.ToLowerInvariant();
                if (cn.Contains("walk")) lauf = c;
                else if (cn.Contains("run") && (lauf == null || !lauf.name.ToLowerInvariant().Contains("walk")))
                {
                    if (lauf == null || c.isLooping) lauf = c;
                }
                if (cn.Contains("idle") || cn.Contains("stand") || cn.Contains("sentry")) steh = c;
            }
            if (lauf != null)
            {
                var wurzel = AnimationsWurzel(go, lauf);
                if (wurzel != null)
                {
                    la.laufClip = lauf;
                    la.stehClip = steh;
                    la.animationsWurzel = wurzel;
                    EditorUtility.SetDirty(la);
                    return "Lauf-Clip '" + lauf.name + "' aus dem Modell" + (steh != null ? ", Stand-Clip '" + steh.name + "'" : "") + " eingetragen. Rutschen die Füße, ändere 'Schritt Laenge' (aktuell " + la.schrittLaenge.ToString("F1") + ").";
                }
                meldung = "Clip '" + lauf.name + "' gefunden, passt aber nicht zur Hierarchie. ";
            }
            else meldung = "Im Modell steckt kein Lauf-Clip. ";
        }
        else meldung = "Kein Skelett-Modell erkannt. ";
        EditorUtility.SetDirty(la);
        return meldung + "Es werden Wippen, Wanken und Neigen sowie automatisch gefundene Rotoren animiert. Eigene Lauf-Clips kannst du bei 'Lauf Clip' einsetzen.";
    }

    static Transform AnimationsWurzel(GameObject go, AnimationClip clip)
    {
        string pfad = null;
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
        {
            if (!string.IsNullOrEmpty(b.path))
            {
                pfad = b.path;
                break;
            }
        }
        if (pfad == null) return go.transform;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t.Find(pfad) != null) return t;
        }
        return null;
    }

    static string ZustaendeUebernehmen(RuntimeAnimatorController rc, MaschinenLaufanimation la)
    {
        var ac = rc as AnimatorController;
        if (ac == null && rc is AnimatorOverrideController)
        {
            ac = ((AnimatorOverrideController)rc).runtimeAnimatorController as AnimatorController;
        }
        if (ac == null || ac.layers.Length == 0) return null;
        string idle = null, walk = null, run = null, erster = null;
        foreach (var s in ac.layers[0].stateMachine.states)
        {
            string n = s.state.name;
            string l = n.ToLowerInvariant();
            if (erster == null) erster = n;
            if (idle == null && (l.Contains("idle") || l.Contains("stand"))) idle = n;
            else if (walk == null && l.Contains("walk")) walk = n;
            else if (run == null && (l.Contains("run") || l.Contains("sprint"))) run = n;
        }
        if (walk == null && run == null) return null;
        la.modus = MaschinenLaufanimation.Modus.AnimatorZustaende;
        if (idle != null) la.stehZustand = idle;
        if (walk != null) la.gehZustand = walk;
        if (run != null) la.laufZustand = run;
        return "Animator-Zustände übernommen (Stand '" + (idle ?? "-") + "', Gehen '" + (walk ?? "-") + "', Rennen '" + (run ?? "-") + "'). Der Animator wird nach Tempo umgeschaltet.";
    }
}
#endif
