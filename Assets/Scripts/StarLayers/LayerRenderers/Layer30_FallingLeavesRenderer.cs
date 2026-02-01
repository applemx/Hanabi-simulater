using UnityEngine;

public sealed class Layer30_FallingLeavesRenderer : StarLayerRendererBase
{
    public override string Id => "falling_leaves";

    public override Vector3 ApplyOffsets(StarLayerDef def, Vector3 pos, Vector3 velocity, float age, uint seed)
    {
        pos = base.ApplyOffsets(def, pos, velocity, age, seed);
        float sway = Mathf.Sin(age * 2.1f + StarLayerRenderUtil.Hash01(seed) * Mathf.PI * 2f);
        pos.x += sway * 0.25f;
        return pos;
    }
}
