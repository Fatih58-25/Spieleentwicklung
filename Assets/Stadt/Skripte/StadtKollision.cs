using UnityEngine;

public static class StadtKollision
{
    public static bool BoxHinzufuegen(GameObject go)
    {
        if (go == null) return false;
        if (go.GetComponent<Collider>() != null) return false;
        var renderer = go.GetComponentsInChildren<Renderer>(true);
        if (renderer.Length == 0) return false;
        Matrix4x4 zuLokal = go.transform.worldToLocalMatrix;
        bool erste = true;
        Bounds b = new Bounds();
        foreach (var r in renderer)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            Bounds lb = r.localBounds;
            Matrix4x4 m = zuLokal * r.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 ecke = lb.center + new Vector3(((i & 1) == 0 ? -1f : 1f) * lb.extents.x, ((i & 2) == 0 ? -1f : 1f) * lb.extents.y, ((i & 4) == 0 ? -1f : 1f) * lb.extents.z);
                Vector3 p = m.MultiplyPoint3x4(ecke);
                if (erste)
                {
                    b = new Bounds(p, Vector3.zero);
                    erste = false;
                }
                else b.Encapsulate(p);
            }
        }
        if (erste) return false;
        var bc = go.AddComponent<BoxCollider>();
        bc.center = b.center;
        bc.size = b.size;
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        return true;
    }
}
