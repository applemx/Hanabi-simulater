using UnityEngine;

public sealed class Layer16_TigerTailRenderer : StarLayerRendererBase
{
    public override string Id => "tiger_tail";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float t = life > 0f ? Mathf.Clamp01(age / life) : 0f;
        return Mathf.Lerp(0.3f, 0.05f, Mathf.Pow(t, 1.4f));
    }

    public override Color ApplyColor(StarLayerDef def, Color c, float t, uint seed)
    {
        return Color.Lerp(c, new Color(0.3f, 0.14f, 0.07f, c.a), 0.7f);
    }

    public override float ApplySize(StarLayerDef def, float sizeScale, float t)
    {
        return sizeScale * 0.9f;
    }

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return StarLayerRenderUtil.Hash01(seed) > 0.35f;
    }

    public override Color ApplyTrailColor(StarLayerDef def, Color baseColor, float t)
    {
        return Color.Lerp(baseColor, new Color(0.15f, 0.06f, 0.03f, baseColor.a), t * 0.9f);
    }

    public override float GetTrailLengthScale(StarLayerDef def)
    {
        return 2.6f;
    }

    public override bool ShouldEmitTrailSample(StarLayerDef def, float t, uint seed, int sampleIndex)
    {
        float r = StarLayerRenderUtil.Hash01(seed ^ (uint)sampleIndex);
        return r >= 0.35f;
    }

    public override float GetTrailAlphaScale(StarLayerDef def, float t, uint seed, int sampleIndex)
    {
        return base.GetTrailAlphaScale(def, t, seed, sampleIndex) * 0.45f;
    }
}
