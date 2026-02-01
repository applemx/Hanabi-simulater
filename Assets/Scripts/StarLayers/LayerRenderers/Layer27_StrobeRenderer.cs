using UnityEngine;

public sealed class Layer27_StrobeRenderer : StarLayerRendererBase
{
    public override string Id => "strobe";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float freq = Mathf.Max(0.1f, def.strobeFrequency);
        float phase = StarLayerRenderUtil.Hash01(seed);
        float frac = Mathf.Repeat(age * freq + phase, 1f);
        float duty = Mathf.Clamp(def.strobeBurstCount / 100f, 0.05f, 0.6f);
        return frac < duty ? 1f : 0f;
    }
}
