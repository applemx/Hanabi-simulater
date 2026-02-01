using UnityEngine;

public sealed class Layer32_TerminalFlashRenderer : StarLayerRendererBase
{
    public override string Id => "terminal_flash";

    public override Color ApplyColor(StarLayerDef def, Color c, float t, uint seed)
    {
        return Color.Lerp(c, Color.white, 0.9f);
    }

    public override float ApplySize(StarLayerDef def, float sizeScale, float t)
    {
        return sizeScale * 0.45f;
    }
}
