using UnityEngine;

public sealed class Layer12_IlluminationFlareRenderer : StarLayerRendererBase
{
    public override string Id => "illumination";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float t = life > 0f ? Mathf.Clamp01(age / life) : 0f;
        float alpha = Mathf.Lerp(0.45f, 0.15f, Mathf.Pow(t, 0.8f));
        float variance = Mathf.Lerp(0.3f, 0.7f, StarLayerRenderUtil.Hash01(seed ^ 0x91c2b3u));
        return alpha * variance;
    }

    public override Color ApplyColor(StarLayerDef def, Color c, float t, uint seed)
    {
        return Color.Lerp(c, new Color(1f, 0.98f, 0.92f, c.a), 0.9f);
    }

    public override float ApplySize(StarLayerDef def, float sizeScale, float t)
    {
        return sizeScale * Mathf.Lerp(0.9f, 0.7f, t);
    }

    public override Vector3 ApplyOffsets(StarLayerDef def, Vector3 pos, Vector3 velocity, float age, uint seed)
    {
        pos = base.ApplyOffsets(def, pos, velocity, age, seed);
        return pos * 0.85f;
    }

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return StarLayerRenderUtil.Hash01(seed) > 0.55f;
    }

    public override void EmitExtras(ParticleSystem.Particle[] buffer, ref int write, StarLayerDef def, ParticleSystem.Particle p, Color c, float life, float age)
    {
        EmitFlareHalo(buffer, ref write, p, c);
    }

    void EmitFlareHalo(ParticleSystem.Particle[] buffer, ref int write, ParticleSystem.Particle p, Color c)
    {
        uint s = p.randomSeed;
        if (StarLayerRenderUtil.Hash01(s) < 0.2f) return;

        Color halo = new Color(1f, 0.98f, 0.94f, Mathf.Clamp01(c.a * 0.12f));
        float radius = 0.4f + 0.35f * StarLayerRenderUtil.Hash01(s ^ 0x51c7e9u);
        Vector3 offset = StarLayerRenderUtil.HashUnitVector(s ^ 0x4b1d3a19u) * radius;
        AddDerivedParticle(buffer, ref write, p, halo, p.position + offset, 7.5f);
    }
}
