using UnityEngine;

public sealed class Layer06_SealCoatRenderer : StarLayerRendererBase
{
    public override string Id => "seal_coat";

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return false;
    }
}
