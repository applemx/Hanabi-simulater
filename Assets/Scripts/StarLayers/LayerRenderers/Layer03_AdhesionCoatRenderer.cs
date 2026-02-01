using UnityEngine;

public sealed class Layer03_AdhesionCoatRenderer : StarLayerRendererBase
{
    public override string Id => "adhesion_coat";

    public override bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return false;
    }
}
