using UnityEngine;

public sealed class Layer33_TerminalSlowFadeRenderer : StarLayerRendererBase
{
    public override string Id => "terminal_slow_fade";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float t = life > 0f ? Mathf.Clamp01(age / life) : 0f;
        return Mathf.Pow(1f - t, 0.35f);
    }

    public override Color ApplyColor(StarLayerDef def, Color c, float t, uint seed)
    {
        var warm = new Color(c.r, c.g * 0.8f, c.b * 0.6f, c.a);
        return Color.Lerp(c, warm, t);
    }

    public override float ApplySize(StarLayerDef def, float sizeScale, float t)
    {
        return sizeScale * Mathf.Lerp(1f, 0.7f, t);
    }
}
