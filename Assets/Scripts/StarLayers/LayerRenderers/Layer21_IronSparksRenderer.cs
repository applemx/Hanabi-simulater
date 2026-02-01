using UnityEngine;

public sealed class Layer21_IronSparksRenderer : StarLayerRendererBase
{
    public override string Id => "iron_sparks";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        int tick = Mathf.FloorToInt(age * 20f);
        float flicker = StarLayerRenderUtil.Hash01(seed ^ (uint)tick);
        return Mathf.Lerp(0.75f, 1f, flicker);
    }

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return StarLayerRenderUtil.Hash01(seed) > 0.2f;
    }

    public override Vector3 ApplyOffsets(StarLayerDef def, Vector3 pos, Vector3 velocity, float age, uint seed)
    {
        pos = base.ApplyOffsets(def, pos, velocity, age, seed);
        float zig = Mathf.Sin(age * 18f + StarLayerRenderUtil.Hash01(seed) * Mathf.PI * 2f);
        return pos + new Vector3(zig, -zig * 0.4f, 0f) * 0.03f;
    }
}
