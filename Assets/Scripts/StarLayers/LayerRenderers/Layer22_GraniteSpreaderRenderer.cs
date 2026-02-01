using UnityEngine;

public sealed class Layer22_GraniteSpreaderRenderer : StarLayerRendererBase
{
    public override string Id => "granite_spreader";

    public override Vector3 ApplyOffsets(StarLayerDef def, Vector3 pos, Vector3 velocity, float age, uint seed)
    {
        pos = base.ApplyOffsets(def, pos, velocity, age, seed);
        float jit = Mathf.Sin(age * 25f + StarLayerRenderUtil.Hash01(seed) * 7f);
        return pos + new Vector3(jit, -jit, 0f) * 0.02f;
    }
}
