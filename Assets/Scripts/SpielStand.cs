using System.Collections.Generic;
using UnityEngine;

public static class SpielStand
{
    static readonly HashSet<string> besitz = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Zuruecksetzen()
    {
        besitz.Clear();
    }

    public static void Setze(string name)
    {
        besitz.Add(name);
    }

    public static bool Hat(string name)
    {
        return besitz.Contains(name);
    }
}
