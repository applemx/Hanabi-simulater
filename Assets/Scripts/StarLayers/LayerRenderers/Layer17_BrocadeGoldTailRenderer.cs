using UnityEngine;

public sealed class Layer17_BrocadeGoldTailRenderer : StarLayerRendererBase
{
    public override string Id => "brocade_gold_tail";

    public override Color ApplyColor(StarLayerDef def, Color c, float t, uint seed)
    {
        float r = StarLayerRenderUtil.Hash01(seed ^ 0x9e3779b9u);
        if (r > 0.7f)
            return Color.Lerp(c, new Color(1f, 0.85f, 0.4f, c.a), 0.8f);
        return Color.Lerp(c, new Color(0.8f, 0.3f, 0.15f, c.a), 0.4f);
    }

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return StarLayerRenderUtil.Hash01(seed) > 0.15f;
    }

    public override Color ApplyTrailColor(StarLayerDef def, Color baseColor, float t)
    {
        var dark = new Color(0.7f, 0.2f, 0.1f, baseColor.a);
        return Color.Lerp(baseColor, dark, t * 0.7f);
    }

    public override float GetTrailLengthScale(StarLayerDef def)
    {
        return 1.3f;
    }

    public override void EmitExtras(ParticleSystem.Particle[] buffer, ref int write, StarLayerDef def, ParticleSystem.Particle p, Color c, float life, float age)
    {
        EmitBrocadeHighlights(buffer, ref write, p, c);
    }

    void EmitBrocadeHighlights(ParticleSystem.Particle[] buffer, ref int write, ParticleSystem.Particle p, Color c)
    {
        uint s = p.randomSeed;
        float r = StarLayerRenderUtil.Hash01(s);
        if (r < 0.65f) return;

        Color gold = new Color(1f, 0.85f, 0.4f, c.a);
        Vector3 offset = StarLayerRenderUtil.HashUnitVector(s ^ 0x7f4a7c15u) * 0.03f;
        AddDerivedParticle(buffer, ref write, p, gold, p.position + offset, 0.45f);
    }
}
