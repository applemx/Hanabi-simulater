using UnityEngine;

public sealed class Layer31_AfterglowRenderer : StarLayerRendererBase
{
    public override string Id => "afterglow";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float t = life > 0f ? Mathf.Clamp01(age / life) : 0f;
        float alpha = Mathf.Lerp(0.22f, 0.03f, Mathf.Pow(t, 1.2f));
        float variance = Mathf.Lerp(0.7f, 1.0f, StarLayerRenderUtil.Hash01(seed ^ 0x3f2a1bu));
        return alpha * variance;
    }

    public override Color ApplyColor(StarLayerDef def, Color c, float t, uint seed)
    {
        return Color.Lerp(c, new Color(0.5f, 0.75f, 1f, c.a), 0.9f);
    }

    public override float ApplySize(StarLayerDef def, float sizeScale, float t)
    {
        return sizeScale * Mathf.Lerp(1.2f, 1.9f, t);
    }

    public override Vector3 ApplyOffsets(StarLayerDef def, Vector3 pos, Vector3 velocity, float age, uint seed)
    {
        pos = base.ApplyOffsets(def, pos, velocity, age, seed);
        float drift = 0.04f;
        float phase = StarLayerRenderUtil.Hash01(seed) * Mathf.PI * 2f;
        pos += new Vector3(Mathf.Sin(age * 1.3f + phase), Mathf.Cos(age * 1.1f + phase), 0f) * drift;
        return pos;
    }
}
