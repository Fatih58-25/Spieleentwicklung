using UnityEngine;

[System.Serializable]
public class KartenRegion
{
    public string name;
    public Vector2 mitte;
    public Vector2 groesse;
    public Color farbe = Color.white;
}

public class WeltkartenDaten : ScriptableObject
{
    public Texture2D karte;
    public Vector2 ursprung;
    public float groesse;
    public KartenRegion[] regionen;
}
