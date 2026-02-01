using UnityEngine;

public sealed class Layer15_WabiTailRenderer : StarLayerRendererBase
{
    public override string Id => "wabi_tail";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        int tick = Mathf.FloorToInt(age * 20f);
        float flicker = StarLayerRenderUtil.Hash01(seed ^ (uint)tick);
        return Mathf.Lerp(0.75f, 1f, flicker);
    }

    public override Color ApplyColor(StarLayerDef def, Color c, float t, uint seed)
    {
        return Color.Lerp(c, new Color(0.55f, 0.4f, 0.25f, c.a), 0.6f);
    }

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return StarLayerRenderUtil.Hash01(seed) > 0.35f;
    }

    public override float GetTrailLengthScale(StarLayerDef def)
    {
        return 1.1f;
    }
}
