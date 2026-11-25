using UnityEngine;

public static class StadtRaum
{
    public const float CX = 0f;
    public const float CY = 1000f;
    public const float RR0 = 225f;
    public const float DR = 110f;
    public const int NB = 6;
    public const float TH0 = 1.5f * Mathf.PI;

    public static Vector3 c = new Vector3(CX, 0f, CY);
    public static Vector3 ex = Vector3.right;
    public static Vector3 ey = Vector3.forward;
    public static Vector3 ez = Vector3.up;
    public static Quaternion modellKorrektur = Quaternion.identity;
    static bool gelesen;

    public static void Lesen(bool erzwingen = false)
    {
        if (gelesen && !erzwingen) return;
        var z = GameObject.Find("Marker_Zentrum");
        var x = GameObject.Find("Marker_X");
        var y = GameObject.Find("Marker_Y");
        var h = GameObject.Find("Marker_Z");
        if (z != null && x != null && y != null && h != null)
        {
            c = z.transform.position;
            ex = (x.transform.position - c) / 100f;
            ey = (y.transform.position - c) / 100f;
            ez = (h.transform.position - c) / 100f;
        }
        else
        {
            Debug.LogWarning("Marker nicht gefunden. Viertel_M2 muss in der Szene liegen, sonst wird die Standard-Ausrichtung benutzt.");
            c = new Vector3(CX, 0f, CY);
            ex = Vector3.right;
            ey = Vector3.forward;
            ez = Vector3.up;
        }
        modellKorrektur = Quaternion.Inverse(Quaternion.LookRotation(ey, ez));
        gelesen = true;
    }

    public static Vector3 Pos(float x, float y, float z)
    {
        return c + ex * (x - CX) + ey * (y - CY) + ez * z;
    }

    public static Vector3 Vektor(float x, float y, float z)
    {
        return ex * x + ey * y + ez * z;
    }

    public static Quaternion Drehung(Vector3 vor)
    {
        return Quaternion.LookRotation(vor, ez) * modellKorrektur;
    }

    public static Vector3 BetragVektor(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
