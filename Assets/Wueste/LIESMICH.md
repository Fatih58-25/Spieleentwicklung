# Wüste (Unity-Paket)

Dünenwüste, ca. 600 x 600 m begehbar, ringsum Sandberge und Sandsteinwände ohne Durchgang. Dazu eine ausgetrocknete Lehmpfanne mit Trockenrissen, sieben Sandstein-Tafelberge, Kies- und Geröllflächen, Felsbrocken, Totholz, Köcherbäume und trockene Sträucher. Keine Gebäude, kein Wasser.

Gebaut für Unity 6 mit URP.

## Einrichten

1. Den Ordner `Assets/Wueste` in dein Projekt kopieren (bei Git LFS: siehe unten).
2. Szene öffnen, in der die Wüste stehen soll.
3. Menü **Wueste → Alles auf einmal einrichten**. Das macht in einem Rutsch:
   - Materialien erzeugen und den Modellen zuweisen
   - Gelände, unsichtbare Begrenzung und alle Scans in die Szene stellen (mit Mesh-Collidern)
   - Himmel, Sonne und leichten Staubdunst einstellen
4. Den Spieler irgendwo im Gelände absetzen, zum Beispiel bei X 0, Z 0, und etwa 20 m über dem Boden starten lassen, weil das Gelände Dünen hat.

Die Einzelschritte gibt es auch als eigene Menüpunkte (`1 Materialien…`, `2 Szene aufbauen…`, `3 Himmel…`).

## Was drin ist

| Ordner | Inhalt |
|---|---|
| `Modelle` | `Wueste_Gelaende.fbx` (36 Kacheln à 150 m, damit Unity sie einzeln ausblenden kann), `Wueste_Grenze.fbx` (unsichtbare Wand), `Wueste_Scans.fbx` und `Wueste_Streuung.fbx` (Felsen, Totholz, Pflanzen und ihre Positionen) |
| `Shader` | `WuesteGelaende.shader`: mischt Sand, Fels, Lehm und Kies über eine Karte. Sand bekommt Windrippel, Fels wird von allen Seiten projiziert, damit steile Wände nicht gezogen aussehen |
| `Texturen` | Karten für Mischung (`splat.png`) und Grobfärbung (`makro.png`), Sand, Rippel, Fels, Lehm, Kies |
| `Himmel` | Himmelsbild (nur Himmel und Licht, die Sonne selbst ist herausgerechnet, sie kommt als Directional Light) |
| `Scans` | Texturen und Materialliste der Fotoscans |
| `Editor` | Menü `Wueste` |

## Wichtig zu wissen

- Das Gelände sitzt im Weltursprung. Wenn du das Objekt `Wueste` verschiebst oder drehst, stimmen die Karten für Mischung und Färbung nicht mehr mit dem Gelände überein. Lass es bei Position 0/0/0 und Drehung 0.
- Die Karten sind 4096 x 4096. Das macht im Speicher etwa 40 MB. Für schwächere Rechner in den Import-Einstellungen von `makro.png` und `splat.png` die Maximale Größe auf 2048 setzen.
- Der Himmel ist ein Panorama-Bild, keine echte Atmosphäre. Wolken oder Tageszeit ändern sich nicht.
- Sand, Wasser, Wind und Pflanzen bewegen sich nicht. Der Sand bekommt keine Fußspuren.
- Die Dünen sind Form und Textur, kein Sand, der sich verformt.

## Git LFS

Die Texturen und FBX-Dateien sind groß. In die `.gitattributes` deines Projekts:

```
Assets/Wueste/** filter=lfs diff=lfs merge=lfs -text
```

## Neu erzeugen

Alles stammt aus Python-Skripten (`wueste_welt.py`, `texturen_wueste.py`, `wueste.py`). Wenn du das Gelände ändern willst, ändere `wueste_welt.py`, erzeuge die Karten neu (`texturen_wueste.py 4096 karte`) und exportiere mit `wueste.py` in Blender.
