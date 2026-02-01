using UnityEngine;

public sealed class Layer02_BodyCoatRenderer : StarLayerRendererBase
{
    public override string Id => "body_coat";

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return false;
    }
}
