using UnityEngine;

public sealed class Layer13_ChrysanthemumTailRenderer : StarLayerRendererBase
{
    public override string Id => "chrysanthemum_tail";

    public override void ConfigureParticleSystem(ParticleSystem ps, StarLayerDef def, StarLayerSlot slot, bool useGlobalColor, Color globalColor, float globalIntensity, float globalSizeScale, float globalLifetimeScale)
    {
        base.ConfigureParticleSystem(ps, def, slot, useGlobalColor, globalColor, globalIntensity, globalSizeScale, globalLifetimeScale);
        if (ps == null) return;
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) return;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.4f;
        renderer.velocityScale = 0.6f;
        renderer.cameraVelocityScale = 0f;
        renderer.minParticleSize = 0.002f;
        renderer.maxParticleSize = 0.06f;
    }

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float t = life > 0f ? Mathf.Clamp01(age / life) : 0f;
        return Mathf.Lerp(0.55f, 0.08f, Mathf.Pow(t, 1.4f));
    }

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return StarLayerRenderUtil.Hash01(seed) > 0.05f;
    }

    public override Color ApplyColor(StarLayerDef def, Color c, float t, uint seed)
    {
        return Color.Lerp(c, new Color(1f, 0.6f, 0.2f, c.a), 0.6f);
    }

    public override float ApplySize(StarLayerDef def, float sizeScale, float t)
    {
        return sizeScale * 0.6f;
    }

    public override Color ApplyTrailColor(StarLayerDef def, Color baseColor, float t)
    {
        var bright = new Color(1f, 0.65f, 0.22f, baseColor.a);
        var mid = new Color(0.9f, 0.35f, 0.12f, baseColor.a);
        var dark = new Color(0.35f, 0.08f, 0.04f, baseColor.a);
        if (t < 0.4f)
            return Color.Lerp(bright, mid, t / 0.4f);
        if (t < 0.85f)
            return Color.Lerp(mid, dark, (t - 0.4f) / 0.45f);
        return Color.Lerp(dark, Color.black, (t - 0.85f) / 0.15f);
    }

    public override float GetTrailLengthScale(StarLayerDef def)
    {
        return 2.4f;
    }

    public override float GetTrailAlphaScale(StarLayerDef def, float t, uint seed, int sampleIndex)
    {
        return base.GetTrailAlphaScale(def, t, seed, sampleIndex) * 0.6f;
    }

    public override float GetTrailSizeScale(StarLayerDef def, float t, uint seed, int sampleIndex)
    {
        return base.GetTrailSizeScale(def, t, seed, sampleIndex) * 0.8f;
    }
}
