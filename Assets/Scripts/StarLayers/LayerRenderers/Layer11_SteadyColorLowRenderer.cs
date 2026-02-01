using UnityEngine;

public sealed class Layer11_SteadyColorLowRenderer : StarLayerRendererBase
{
    public override string Id => "steady_color_low";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float t = life > 0f ? Mathf.Clamp01(age / life) : 0f;
        float alpha = Mathf.Lerp(0.7f, 0.15f, Mathf.Pow(t, 1.2f));
        int tick = Mathf.FloorToInt(age * 20f);
        float flicker = StarLayerRenderUtil.Hash01(seed ^ (uint)tick);
        alpha *= Mathf.Lerp(0.75f, 1f, flicker);
        return alpha;
    }
}
