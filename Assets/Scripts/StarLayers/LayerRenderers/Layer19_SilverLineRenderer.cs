using UnityEngine;

public sealed class Layer19_SilverLineRenderer : StarLayerRendererBase
{
    public override string Id => "silver_line";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float t = life > 0f ? Mathf.Clamp01(age / life) : 0f;
        return Mathf.Lerp(1.2f, 0.08f, Mathf.Pow(t, 2.0f));
    }

    public override Color ApplyColor(StarLayerDef def, Color c, float t, uint seed)
    {
        return Color.Lerp(c, new Color(0.85f, 0.9f, 1f, c.a), 0.7f);
    }

    public override float ApplySize(StarLayerDef def, float sizeScale, float t)
    {
        return sizeScale * 0.45f;
    }

    public override float GetTrailLengthScale(StarLayerDef def)
    {
        return 1.1f;
    }
}
