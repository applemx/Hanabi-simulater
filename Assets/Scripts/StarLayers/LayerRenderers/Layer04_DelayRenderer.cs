using UnityEngine;

public sealed class Layer04_DelayRenderer : StarLayerRendererBase
{
    public override string Id => "delay";

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return false;
    }
}
