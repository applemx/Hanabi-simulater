using UnityEngine;

public sealed class Layer05_BarrierBlindRenderer : StarLayerRendererBase
{
    public override string Id => "barrier_blind";

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return false;
    }
}
