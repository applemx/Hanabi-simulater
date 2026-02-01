using UnityEngine;

public sealed class Layer08_StarterRenderer : StarLayerRendererBase
{
    public override string Id => "starter";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float t = life > 0f ? Mathf.Clamp01(age / life) : 0f;
        return Mathf.Lerp(1.4f, 0.0f, Mathf.Pow(t, 0.18f));
    }

    public override Color ApplyColor(StarLayerDef def, Color c, float t, uint seed)
    {
        return Color.Lerp(c, Color.white, 0.9f);
    }

    public override float ApplySize(StarLayerDef def, float sizeScale, float t)
    {
        return sizeScale * 0.5f;
    }
}
