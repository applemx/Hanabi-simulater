using UnityEngine;

public static class StarLayerRenderUtil
{
    public static bool IdContains(StarLayerDef def, string token)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return false;
        return def.id.ToLowerInvariant().Contains(token);
    }

    public static float Hash01(uint x)
    {
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        return (x & 0x00FFFFFF) / 16777215.0f;
    }

    public static Vector3 HashUnitVector(uint seed)
    {
        float x = Hash01(seed) * 2f - 1f;
        float y = Hash01(seed ^ 0x68bc21u) * 2f - 1f;
        float z = Hash01(seed ^ 0x02e5be93u) * 2f - 1f;
        var v = new Vector3(x, y, z);
        if (v.sqrMagnitude < 0.0001f)
            return Vector3.up;
        return v.normalized;
    }

    public static bool IsFinite(float v)
    {
        return !float.IsNaN(v) && !float.IsInfinity(v);
    }

    public static bool IsFinite(Vector3 v)
    {
        return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
    }
}
