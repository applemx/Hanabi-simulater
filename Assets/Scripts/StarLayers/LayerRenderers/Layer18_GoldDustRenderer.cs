using UnityEngine;

public sealed class Layer18_GoldDustRenderer : StarLayerRendererBase
{
    public override string Id => "gold_dust";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float t = life > 0f ? Mathf.Clamp01(age / life) : 0f;
        return Mathf.Lerp(0.6f, 0.05f, Mathf.Clamp01(t * 2.5f));
    }

    public override float ApplySize(StarLayerDef def, float sizeScale, float t)
    {
        return sizeScale * 0.5f;
    }

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return StarLayerRenderUtil.Hash01(seed) > 0.15f;
    }
}
