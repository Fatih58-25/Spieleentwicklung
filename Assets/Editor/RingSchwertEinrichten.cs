#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class RingSchwertEinrichten
{
    const string ModellRing = "Assets/ring_unity.fbx";
    const string MatOrdner = "Assets/Spiel/Materialien";

    static GameObject ZoneErzeugen(GameObject ziel, string name, float radius)
    {
        var zone = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(zone, name);
        zone.transform.SetParent(ziel.transform.parent, false);
        zone.transform.position = ziel.transform.position;
        var col = zone.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = radius;
        return zone;
    }

    static GameObject FesteZone(string name, Transform eltern, Vector3 lokal, float radius)
    {
        var zone = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(zone, name);
        zone.transform.SetParent(eltern, false);
        zone.transform.localPosition = lokal;
        var col = zone.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = radius;
        return zone;
    }

    static void OrdnerSicherstellen(string pfad)
    {
        if (AssetDatabase.IsValidFolder(pfad)) return;
        int i = pfad.LastIndexOf('/');
        OrdnerSicherstellen(pfad.Substring(0, i));
        AssetDatabase.CreateFolder(pfad.Substring(0, i), pfad.Substring(i + 1));
    }

    static Material MatErzeugen(string name, Color farbe)
    {
        OrdnerSicherstellen(MatOrdner);
        string pfad = MatOrdner + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(pfad);
        if (mat != null) return mat;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        mat = new Material(shader);
        mat.color = farbe;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", farbe);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(mat, pfad);
        return mat;
    }

    static GameObject Quader(string name, Transform eltern, Vector3 lokal, Vector3 groesse, Material mat, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.name = name;
        go.transform.SetParent(eltern, false);
        go.transform.localPosition = lokal;
        go.transform.localScale = groesse;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void AufGroesse(GameObject go, float zielMeter)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        float max = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (max <= 0.00001f) return;
        go.transform.localScale = go.transform.localScale * (zielMeter / max);
    }

    static GameObject Modell(string pfad, string name, Transform eltern, Vector3 lokal, float groesse, PrimitiveType ersatz)
    {
        var vorlage = AssetDatabase.LoadAssetAtPath<GameObject>(pfad);
        GameObject go;
        if (vorlage != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(vorlage);
        }
        else
        {
            go = GameObject.CreatePrimitive(ersatz);
            Debug.LogWarning("Spiel: " + pfad + " nicht gefunden, es wird ein Platzhalter benutzt.");
        }
        Undo.RegisterCreatedObjectUndo(go, name);
        go.name = name;
        go.transform.SetParent(eltern, false);
        go.transform.localPosition = lokal;
        go.transform.localRotation = Quaternion.identity;
        foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        AufGroesse(go, groesse);
        return go;
    }

    static Transform FingerSuchen(Transform wurzel)
    {
        Transform ring = null;
        Transform hand = null;
        foreach (var t in wurzel.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            bool rechts = n.EndsWith("_r") || n.EndsWith(".r") || n.Contains("right") || n.Contains("rechts");
            if (!rechts) continue;
            if (n.Contains("ring") && !n.Contains("spring") && !n.Contains("string"))
            {
                if (ring == null || n.Contains("01")) ring = t;
            }
            if (n.Contains("hand") && hand == null) hand = t;
        }
        return ring != null ? ring : hand;
    }

    static Vector3 Platzierung(string alterName, out Quaternion rot, out bool ersetzt)
    {
        rot = Quaternion.identity;
        ersetzt = false;
        var alt = alterName != null ? GameObject.Find(alterName) : null;
        var auswahl = Selection.activeGameObject;
        Vector3 pos;
        if (alt != null)
        {
            pos = alt.transform.position;
            rot = alt.transform.rotation;
            Undo.DestroyObjectImmediate(alt);
            ersetzt = true;
        }
        else if (auswahl != null && !auswahl.CompareTag("Player") && auswahl.scene.IsValid())
        {
            pos = auswahl.transform.position;
        }
        else
        {
            var sv = SceneView.lastActiveSceneView;
            pos = sv != null ? sv.pivot : Vector3.zero;
            RaycastHit treffer;
            if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out treffer, 200f)) pos = treffer.point;
        }
        return pos;
    }

    [MenuItem("Spiel/1 Ring-Traeger zum Spieler hinzufuegen")]
    public static void TraegerHinzufuegen()
    {
        var spieler = GameObject.FindGameObjectWithTag("Player");
        if (spieler == null)
        {
            Debug.LogWarning("Kein Objekt mit dem Tag Player in der Szene gefunden.");
            return;
        }
        if (spieler.GetComponent<RingTraeger>() == null) Undo.AddComponent<RingTraeger>(spieler);
        EditorUtility.SetDirty(spieler);
        Debug.Log("Spiel: RingTraeger auf " + spieler.name + " vorhanden.");
    }

    [MenuItem("Spiel/2 Schwert im Stein einrichten (Schwert markieren)")]
    public static void SchwertEinrichten()
    {
        var ziel = Selection.activeGameObject;
        if (ziel == null)
        {
            Debug.LogWarning("Markiere zuerst das Schwert in der Hierarchie.");
            return;
        }
        var zone = ZoneErzeugen(ziel, "SchwertImStein_Zone", 2.5f);
        var s = Undo.AddComponent<SchwertImStein>(zone);
        s.schwertObjekt = ziel;
        var kiste = Object.FindFirstObjectByType<ChestOpen>();
        if (kiste != null && kiste.playerHand != null) s.spielerHand = kiste.playerHand;
        Selection.activeGameObject = zone;
        EditorUtility.SetDirty(zone);
        Debug.Log("Spiel: Zone fuer " + ziel.name + " erstellt.");
    }

    [MenuItem("Spiel/3 Ring-Aufheben-Zone fuer markierten Ring einrichten")]
    public static void RingEinrichten()
    {
        var ziel = Selection.activeGameObject;
        if (ziel == null)
        {
            Debug.LogWarning("Markiere zuerst den Ring in der Hierarchie.");
            return;
        }
        var zone = ZoneErzeugen(ziel, "Ring_Zone", 1.8f);
        var r = Undo.AddComponent<RingAufheben>(zone);
        r.ringObjekt = ziel;
        Selection.activeGameObject = zone;
        EditorUtility.SetDirty(zone);
    }

    [MenuItem("Spiel/4 Ring an der Hand des Spielers anbringen (unsichtbar)")]
    public static void RingAnHand()
    {
        var spieler = GameObject.FindGameObjectWithTag("Player");
        if (spieler == null)
        {
            Debug.LogWarning("Kein Objekt mit dem Tag Player in der Szene gefunden.");
            return;
        }
        var traeger = spieler.GetComponent<RingTraeger>();
        if (traeger == null) traeger = Undo.AddComponent<RingTraeger>(spieler);
        if (traeger.ringAnzeige != null)
        {
            Debug.Log("Spiel: Der Spieler hat schon einen Ring an der Hand (" + traeger.ringAnzeige.name + ").");
            return;
        }
        foreach (var t in spieler.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.ToLowerInvariant().StartsWith("ring_unity"))
            {
                traeger.ringAnzeige = t.gameObject;
                traeger.traegtRing = false;
                EditorUtility.SetDirty(traeger);
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                Debug.Log("Spiel: Dein vorhandener Ring " + t.name + " wird benutzt. Er wird beim Spielstart ausgeblendet und erscheint, sobald der Spieler den Ring aufnimmt.");
                return;
            }
        }
        var finger = FingerSuchen(spieler.transform);
        if (finger == null) finger = spieler.transform;
        var ring = Modell(ModellRing, "RingAnHand", finger, Vector3.zero, 0.024f, PrimitiveType.Cylinder);
        traeger.ringAnzeige = ring;
        traeger.traegtRing = false;
        ring.SetActive(false);
        EditorUtility.SetDirty(traeger);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Selection.activeGameObject = ring;
        Debug.Log("Spiel: Ring an " + finger.name + " angebracht. Er ist unsichtbar, bis der Spieler ihn aufnimmt.");
    }

    [MenuItem("Spiel/5 Paket aufbauen (am markierten Objekt oder in der Szenenansicht)")]
    public static void PaketBauen()
    {
        var spieler = GameObject.FindGameObjectWithTag("Player");
        Quaternion rot;
        bool ersetzt;
        Vector3 pos = Platzierung("Paket", out rot, out ersetzt);
        var root = new GameObject("Paket");
        Undo.RegisterCreatedObjectUndo(root, "Paket");
        root.transform.position = pos;
        root.transform.rotation = rot;

        var karton = MatErzeugen("Paket_Karton", new Color(0.55f, 0.4f, 0.25f));
        var dunkel = MatErzeugen("Paket_Deckel", new Color(0.42f, 0.3f, 0.18f));
        var papier = MatErzeugen("Paket_Papier", new Color(0.92f, 0.86f, 0.7f));

        Quader("Paket_Kasten", root.transform, new Vector3(0f, 0.1f, 0f), new Vector3(0.5f, 0.2f, 0.34f), karton, true);
        var deckel = Quader("Paket_Deckel", root.transform, new Vector3(0f, 0.215f, 0f), new Vector3(0.52f, 0.03f, 0.36f), dunkel, false);

        var brief = Quader("Paket_Brief", root.transform, new Vector3(-0.1f, 0.203f, 0.02f), new Vector3(0.14f, 0.004f, 0.2f), papier, false);
        var schluessel = Modell("Assets/schluessel_a.fbx", "Paket_Schluessel_A", root.transform, new Vector3(0.04f, 0.215f, -0.02f), 0.1f, PrimitiveType.Capsule);
        var ring = Modell(ModellRing, "Paket_Ring", root.transform, new Vector3(0.16f, 0.22f, 0.07f), 0.03f, PrimitiveType.Cylinder);

        var zonePaket = FesteZone("Paket_Zone", root.transform, new Vector3(0f, 0.2f, 0f), 2.2f);
        var zoneBrief = FesteZone("Brief_Zone", root.transform, brief.transform.localPosition, 1.4f);
        var zoneSchluessel = FesteZone("Schluessel_A_Zone", root.transform, schluessel.transform.localPosition, 1.4f);
        var zoneRing = FesteZone("Ring_Zone", root.transform, ring.transform.localPosition, 1.4f);

        var inhalt = new List<GameObject> { brief, schluessel, ring, zoneBrief, zoneSchluessel, zoneRing };
        var paket = Undo.AddComponent<PaketOeffnen>(zonePaket);
        paket.deckel = deckel;
        paket.inhalt = inhalt.ToArray();
        Undo.AddComponent<BriefLesen>(zoneBrief).objekt = brief;
        var ra = Undo.AddComponent<RingAufheben>(zoneRing);
        ra.ringObjekt = ring;
        var gs = Undo.AddComponent<GegenstandAufheben>(zoneSchluessel);
        gs.objekt = schluessel;
        gs.gegenstandName = "Schluessel_A";
        gs.hinweis = "[E] Schlüssel nehmen";
        gs.meldung = "Du nimmst den Schlüssel an dich. Er soll die Tore der Stadt öffnen.";

        foreach (var o in inhalt) o.SetActive(false);

        if (spieler != null && spieler.GetComponent<RingTraeger>() == null) Undo.AddComponent<RingTraeger>(spieler);
        Selection.activeGameObject = root;
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Spiel: Paket aufgebaut bei " + pos + ". Verschiebe das Objekt Paket in der Hierarchie dorthin, wo es liegen soll.");
    }

    static void SchluesselPlatzieren(string buchstabe, string modellPfad, string meldung)
    {
        string name = "Schluessel_" + buchstabe + "_Welt";
        Quaternion rot;
        bool ersetzt;
        Vector3 pos = Platzierung(name, out rot, out ersetzt);
        var root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(root, name);
        root.transform.position = ersetzt ? pos : pos + Vector3.up * 0.3f;
        var modell = Modell(modellPfad, "Schluessel_" + buchstabe, root.transform, Vector3.zero, 0.2f, PrimitiveType.Capsule);
        modell.AddComponent<SchluesselSchweben>();
        var licht = new GameObject("Schein");
        licht.transform.SetParent(modell.transform, false);
        licht.transform.localPosition = Vector3.zero;
        var l = licht.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.85f, 0.5f);
        l.intensity = 1.5f;
        l.range = 3f;
        l.shadows = LightShadows.None;
        var zone = FesteZone("Schluessel_" + buchstabe + "_Zone", root.transform, Vector3.zero, 1.5f);
        var gs = Undo.AddComponent<GegenstandAufheben>(zone);
        gs.objekt = modell;
        gs.gegenstandName = "Schluessel_" + buchstabe;
        gs.hinweis = "[E] Schlüssel nehmen";
        gs.meldung = meldung;
        Selection.activeGameObject = root;
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Spiel: Schlüssel " + buchstabe + " liegt bei " + root.transform.position + ". Verschiebe das Objekt " + name + " an sein Versteck.");
    }

    [MenuItem("Spiel/6 Schluessel B platzieren (am markierten Objekt oder in der Szenenansicht)")]
    public static void SchluesselBPlatzieren()
    {
        SchluesselPlatzieren("B", "Assets/schluessel_b.fbx", "Du hast einen zweiten Schlüssel gefunden. Er hilft, die Maschinen abzuschalten.");
    }

    [MenuItem("Spiel/7 Schluessel C platzieren (am markierten Objekt oder in der Szenenansicht)")]
    public static void SchluesselCPlatzieren()
    {
        SchluesselPlatzieren("C", "Assets/schluessel_c.fbx", "Du hast den dritten Schlüssel gefunden. Nun fehlt nur noch, die Maschinen abzuschalten.");
    }

    [MenuItem("Spiel/5b Vorhandene Truhe als Paket einrichten (Truhe markieren)")]
    public static void TruheAlsPaket()
    {
        var truhe = Selection.activeGameObject;
        if (truhe == null)
        {
            Debug.LogWarning("Markiere zuerst die Truhe in der Hierarchie.");
            return;
        }
        Bounds b;
        if (!GrenzenBerechnen(new[] { truhe }, out b))
        {
            Debug.LogWarning("An der markierten Truhe wurde kein sichtbares Modell gefunden.");
            return;
        }
        var spieler = GameObject.FindGameObjectWithTag("Player");
        var vorhanden = truhe.transform.Find("Truhe_Inhalt");
        if (vorhanden != null) Undo.DestroyObjectImmediate(vorhanden.gameObject);
        var alt = truhe.GetComponent<ChestOpen>();
        if (alt != null)
        {
            Undo.RecordObject(alt, "ChestOpen ausschalten");
            alt.enabled = false;
        }

        var root = new GameObject("Truhe_Inhalt");
        Undo.RegisterCreatedObjectUndo(root, "Truhe_Inhalt");
        root.transform.SetParent(truhe.transform, false);
        Vector3 ls = truhe.transform.lossyScale;
        root.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, ls.x), 1f / Mathf.Max(0.0001f, ls.y), 1f / Mathf.Max(0.0001f, ls.z));

        var papier = MatErzeugen("Paket_Papier", new Color(0.92f, 0.86f, 0.7f));
        float hoehe = b.min.y + b.size.y * 0.45f;
        float abstand = Mathf.Min(0.22f, Mathf.Min(b.size.x, b.size.z) * 0.28f);
        Vector3 mitte = new Vector3(b.center.x, hoehe, b.center.z);
        Vector3 rechts = truhe.transform.right;
        Vector3 lokalBrief = root.transform.InverseTransformPoint(mitte - rechts * abstand);
        Vector3 lokalSchluessel = root.transform.InverseTransformPoint(mitte);
        Vector3 lokalRing = root.transform.InverseTransformPoint(mitte + rechts * abstand);

        var brief = Quader("Paket_Brief", root.transform, lokalBrief, new Vector3(0.12f, 0.004f, 0.17f), papier, false);
        var schluessel = Modell("Assets/schluessel_a.fbx", "Paket_Schluessel_A", root.transform, lokalSchluessel, 0.1f, PrimitiveType.Capsule);
        var ring = Modell(ModellRing, "Paket_Ring", root.transform, lokalRing, 0.03f, PrimitiveType.Cylinder);

        float zonenRadius = Mathf.Max(1.8f, Mathf.Max(b.size.x, b.size.z) * 1.2f);
        var zonePaket = FesteZone("Truhe_Zone", root.transform, root.transform.InverseTransformPoint(b.center), zonenRadius);
        var zoneBrief = FesteZone("Brief_Zone", root.transform, lokalBrief, 1.4f);
        var zoneSchluessel = FesteZone("Schluessel_A_Zone", root.transform, lokalSchluessel, 1.4f);
        var zoneRing = FesteZone("Ring_Zone", root.transform, lokalRing, 1.4f);

        var inhalt = new List<GameObject> { brief, schluessel, ring, zoneBrief, zoneSchluessel, zoneRing };
        var paket = Undo.AddComponent<PaketOeffnen>(zonePaket);
        paket.deckel = null;
        paket.inhalt = inhalt.ToArray();
        paket.animator = truhe.GetComponentInChildren<Animator>();
        paket.animatorParameter = "IsOpen";
        paket.inhaltVerzoegerung = 1.5f;
        paket.hinweis = "[E] Truhe öffnen";
        paket.meldung = "In der Truhe liegt ein Paket von deinem Opa: ein Brief, ein Schlüssel und ein Ring.";
        Undo.AddComponent<BriefLesen>(zoneBrief).objekt = brief;
        var ra = Undo.AddComponent<RingAufheben>(zoneRing);
        ra.ringObjekt = ring;
        var gs = Undo.AddComponent<GegenstandAufheben>(zoneSchluessel);
        gs.objekt = schluessel;
        gs.gegenstandName = "Schluessel_A";
        gs.hinweis = "[E] Schlüssel nehmen";
        gs.meldung = "Du nimmst den Schlüssel an dich. Er soll die Tore der Stadt öffnen.";

        foreach (var o in inhalt) o.SetActive(false);

        if (spieler != null && spieler.GetComponent<RingTraeger>() == null) Undo.AddComponent<RingTraeger>(spieler);
        Selection.activeGameObject = zonePaket;
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        if (paket.animator == null) Debug.LogWarning("Spiel: An der Truhe wurde kein Animator gefunden. Die Truhe öffnet sich nicht sichtbar, der Inhalt erscheint trotzdem.");
        Debug.Log("Spiel: Truhe eingerichtet. Das alte Skript ChestOpen an dieser Truhe wurde ausgeschaltet. Brief, Schlüssel und Ring erscheinen nach dem Öffnen in der Truhe.");
    }

    static Transform FindeObjekt(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == name) return t;
        }
        return null;
    }

    [MenuItem("Spiel/10 Stadttore einrichten (alle vier Tore, Schluessel A)")]
    public static void StadtToreEinrichten()
    {
        string[] namen = { "Nord", "Ost", "Sued", "West" };
        var paare = new List<Transform[]>();
        var benutzt = new List<string>();
        var mitten = new List<Bounds>();
        foreach (var n in namen)
        {
            var links = FindeObjekt("Tor_" + n + "_Fluegel_Links");
            var rechts = FindeObjekt("Tor_" + n + "_Fluegel_Rechts");
            if (links == null || rechts == null)
            {
                Debug.LogWarning("Tor " + n + ": Die Flügel wurden nicht gefunden. Ist die Szene Stadt geöffnet?");
                continue;
            }
            Bounds b;
            if (!GrenzenBerechnen(new[] { links.gameObject, rechts.gameObject }, out b)) continue;
            paare.Add(new[] { links, rechts });
            benutzt.Add(n);
            mitten.Add(b);
        }
        if (paare.Count == 0) return;
        Vector3 stadtMitte = Vector3.zero;
        foreach (var b in mitten) stadtMitte += b.center;
        stadtMitte /= mitten.Count;
        for (int i = 0; i < paare.Count; i++)
        {
            string zoneName = "Tor_" + benutzt[i] + "_Zone";
            var alt = FindeObjekt(zoneName);
            if (alt != null) Undo.DestroyObjectImmediate(alt.gameObject);
            var zone = new GameObject(zoneName);
            Undo.RegisterCreatedObjectUndo(zone, zoneName);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(zone, paare[i][0].gameObject.scene);
            zone.transform.position = new Vector3(mitten[i].center.x, mitten[i].min.y + 1f, mitten[i].center.z);
            var col = zone.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 25f;
            var tor = Undo.AddComponent<StadtTor>(zone);
            tor.fluegel = paare[i];
            tor.benoetigterSchluessel = "Schluessel_A";
            tor.schieben = false;
            tor.winkel = 100f;
            tor.dauer = 7f;
            tor.NachAussenOeffnen(new Vector3(stadtMitte.x, 0f, stadtMitte.z));
            foreach (var f in paare[i])
            {
                foreach (var t in f.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            }
            EditorUtility.SetDirty(zone);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Spiel: " + paare.Count + " Stadttor(e) eingerichtet. Mit Schlüssel A öffnen sie sich per E. Öffnet ein Tor in die falsche Richtung, hake bei der Zone Umkehren an.");
    }

    static bool GrenzenBerechnen(IEnumerable<GameObject> objekte, out Bounds b)
    {
        b = new Bounds();
        bool erste = true;
        foreach (var go in objekte)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (erste)
                {
                    b = r.bounds;
                    erste = false;
                }
                else b.Encapsulate(r.bounds);
            }
        }
        return !erste;
    }

    [MenuItem("Spiel/8 Tor einrichten (Torflügel markieren, 1 oder 2)")]
    public static void TorEinrichten()
    {
        var auswahl = Selection.gameObjects;
        if (auswahl == null || auswahl.Length == 0)
        {
            Debug.LogWarning("Markiere zuerst die Torflügel in der Hierarchie (ein Flügel oder beide mit gedrückter Strg-Taste).");
            return;
        }
        Bounds b;
        if (!GrenzenBerechnen(auswahl, out b))
        {
            Debug.LogWarning("An den markierten Objekten wurde kein sichtbares Modell gefunden.");
            return;
        }
        var zone = new GameObject("Tor_Zone");
        Undo.RegisterCreatedObjectUndo(zone, "Tor_Zone");
        zone.transform.SetParent(auswahl[0].transform.parent, false);
        zone.transform.position = new Vector3(b.center.x, b.min.y + 1f, b.center.z);
        var col = zone.AddComponent<SphereCollider>();
        col.isTrigger = true;
        float breite = Mathf.Max(b.size.x, b.size.z);
        col.radius = Mathf.Max(4f, breite * 0.8f);
        var tor = Undo.AddComponent<StadtTor>(zone);
        var fluegel = new List<Transform>();
        foreach (var go in auswahl)
        {
            fluegel.Add(go.transform);
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }
        tor.fluegel = fluegel.ToArray();
        tor.benoetigterSchluessel = "Schluessel_A";
        Selection.activeGameObject = zone;
        EditorUtility.SetDirty(zone);
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Spiel: Tor eingerichtet mit " + fluegel.Count + " Flügel(n). Öffnet es in die falsche Richtung, hake bei Tor_Zone 'Umkehren' an. Statt Schwingen kannst du bei 'Schieben' ein Schiebetor wählen.");
    }
}
#endif
