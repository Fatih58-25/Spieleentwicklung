using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class BiomGras : MonoBehaviour
{
    [System.Serializable]
    public class Typ
    {
        public string name;
        public Mesh mesh;
        public Material[] materialien;
        public float anteil = 0.33f;
    }

    public Typ[] typen;
    public float reichweite = 38f;
    public float zelle = 12f;
    public float dichte = 3.0f;
    public int zellenProBild = 3;
    public int rayHoehe = 120;
    public string bodenName = "Boden";
    public float minAbstandStrasse = 0f;
    public bool schattenEmpfangen = true;

    class ZellenDaten
    {
        public Matrix4x4[][] pro;
    }

    readonly Dictionary<Vector2Int, ZellenDaten> zellen = new Dictionary<Vector2Int, ZellenDaten>();
    readonly Matrix4x4[] puffer = new Matrix4x4[1000];
    readonly List<Vector2Int> entfernen = new List<Vector2Int>();

    void Update()
    {
        var cam = Camera.main;
        if (cam == null || typen == null || typen.Length == 0) return;
        Vector3 cp = cam.transform.position;
        int r = Mathf.CeilToInt(reichweite / zelle);
        var mitte = new Vector2Int(Mathf.FloorToInt(cp.x / zelle), Mathf.FloorToInt(cp.z / zelle));
        float grenze2 = (reichweite + zelle) * (reichweite + zelle);
        int neu = 0;
        for (int dx = -r; dx <= r; dx++)
        {
            for (int dz = -r; dz <= r; dz++)
            {
                var k = new Vector2Int(mitte.x + dx, mitte.y + dz);
                var zm = new Vector3((k.x + 0.5f) * zelle, cp.y, (k.y + 0.5f) * zelle);
                if ((zm - cp).sqrMagnitude > grenze2) continue;
                ZellenDaten d;
                if (!zellen.TryGetValue(k, out d))
                {
                    if (neu >= zellenProBild) continue;
                    d = Erzeugen(k, Vector3.Distance(zm, cp));
                    zellen[k] = d;
                    neu++;
                }
                Zeichnen(d);
            }
        }
        entfernen.Clear();
        float weg = (reichweite + zelle * 3f) * (reichweite + zelle * 3f);
        foreach (var kv in zellen)
        {
            var zm = new Vector3((kv.Key.x + 0.5f) * zelle, cp.y, (kv.Key.y + 0.5f) * zelle);
            if ((zm - cp).sqrMagnitude > weg) entfernen.Add(kv.Key);
        }
        foreach (var k in entfernen) zellen.Remove(k);
    }

    ZellenDaten Erzeugen(Vector2Int k, float abstand)
    {
        var rnd = new System.Random(k.x * 73856093 ^ k.y * 19349663);
        var listen = new List<Matrix4x4>[typen.Length];
        for (int i = 0; i < listen.Length; i++) listen[i] = new List<Matrix4x4>();
        float faktor = abstand < 18f ? 1f : 0.45f;
        int n = Mathf.RoundToInt(dichte * faktor * zelle * zelle);
        float summe = 0f;
        foreach (var t in typen) summe += t.anteil;
        for (int i = 0; i < n; i++)
        {
            float x = (k.x + (float)rnd.NextDouble()) * zelle;
            float z = (k.y + (float)rnd.NextDouble()) * zelle;
            RaycastHit hit;
            if (!Physics.Raycast(new Vector3(x, rayHoehe, z), Vector3.down, out hit, rayHoehe * 2f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!string.IsNullOrEmpty(bodenName) && !hit.collider.name.Contains(bodenName)) continue;
            float w = (float)rnd.NextDouble() * summe;
            int ti = 0;
            float acc = 0f;
            for (int j = 0; j < typen.Length; j++)
            {
                acc += typen[j].anteil;
                if (w <= acc)
                {
                    ti = j;
                    break;
                }
            }
            float sk = 0.8f + (float)rnd.NextDouble() * 0.5f;
            var rot = Quaternion.Euler((float)(rnd.NextDouble() - 0.5) * 9f, (float)rnd.NextDouble() * 360f, (float)(rnd.NextDouble() - 0.5) * 9f);
            listen[ti].Add(Matrix4x4.TRS(hit.point + Vector3.down * 0.02f, rot, new Vector3(sk, sk, sk)));
        }
        var d = new ZellenDaten { pro = new Matrix4x4[typen.Length][] };
        for (int i = 0; i < listen.Length; i++) d.pro[i] = listen[i].ToArray();
        return d;
    }

    void Zeichnen(ZellenDaten d)
    {
        for (int ti = 0; ti < typen.Length; ti++)
        {
            var t = typen[ti];
            var arr = d.pro[ti];
            if (t.mesh == null || arr.Length == 0) continue;
            int sub = Mathf.Min(t.mesh.subMeshCount, t.materialien.Length);
            for (int s = 0; s < sub; s++)
            {
                if (t.materialien[s] == null) continue;
                var rp = new RenderParams(t.materialien[s])
                {
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = schattenEmpfangen,
                    layer = gameObject.layer
                };
                for (int i = 0; i < arr.Length; i += 1000)
                {
                    int n = Mathf.Min(1000, arr.Length - i);
                    System.Array.Copy(arr, i, puffer, 0, n);
                    Graphics.RenderMeshInstanced(rp, t.mesh, s, puffer, n);
                }
            }
        }
    }
}
