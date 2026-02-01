using UnityEngine;

public sealed class Layer14_WillowKamuroTailRenderer : StarLayerRendererBase
{
    public override string Id => "willow_kamuro_tail";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float t = life > 0f ? Mathf.Clamp01(age / life) : 0f;
        return Mathf.Lerp(1.0f, 0.35f, t);
    }

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return StarLayerRenderUtil.Hash01(seed) > 0.2f;
    }

    public override Color ApplyTrailColor(StarLayerDef def, Color baseColor, float t)
    {
        return Color.Lerp(baseColor, new Color(0.3f, 0.1f, 0.05f, baseColor.a), t);
    }

    public override bool ShouldEmitTrailSample(StarLayerDef def, float t, uint seed, int sampleIndex)
    {
        return (sampleIndex % 2) != 0;
    }

    public override float GetTrailAlphaScale(StarLayerDef def, float t, uint seed, int sampleIndex)
    {
        return base.GetTrailAlphaScale(def, t, seed, sampleIndex) * 0.75f;
    }

    public override float GetTrailLengthScale(StarLayerDef def)
    {
        return 1.35f;
    }
}
