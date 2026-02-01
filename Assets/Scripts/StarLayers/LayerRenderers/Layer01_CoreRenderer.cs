using UnityEngine;

public sealed class Layer01_CoreRenderer : StarLayerRendererBase
{
    public override string Id => "core";

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return false;
    }
}
