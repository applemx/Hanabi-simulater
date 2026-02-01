using UnityEngine;

public sealed class Layer10_SteadyColorHighRenderer : StarLayerRendererBase
{
    public override string Id => "steady_color_high";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float t = life > 0f ? Mathf.Clamp01(age / life) : 0f;
        return Mathf.Lerp(1.1f, 0.25f, Mathf.Pow(t, 1.6f));
    }
}
