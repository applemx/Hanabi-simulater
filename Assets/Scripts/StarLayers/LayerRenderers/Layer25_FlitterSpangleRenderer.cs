using UnityEngine;

public sealed class Layer25_FlitterSpangleRenderer : StarLayerRendererBase
{
    public override string Id => "flitter_spangle";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float freq = Mathf.Max(12f, def.strobeFrequency);
        int tick = Mathf.FloorToInt(age * freq);
        float flicker = StarLayerRenderUtil.Hash01(seed ^ (uint)tick);
        return Mathf.Lerp(0.4f, 1f, flicker);
    }

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return StarLayerRenderUtil.Hash01(seed) > 0.4f;
    }
}
