using UnityEngine;

public sealed class Layer20_SilverKamuroRenderer : StarLayerRendererBase
{
    public override string Id => "silver_kamuro";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float t = life > 0f ? Mathf.Clamp01(age / life) : 0f;
        return Mathf.Lerp(1.0f, 0.35f, t);
    }

    public override Color ApplyColor(StarLayerDef def, Color c, float t, uint seed)
    {
        return Color.Lerp(c, new Color(0.9f, 0.95f, 1f, c.a), 0.6f);
    }

    public override float GetTrailLengthScale(StarLayerDef def)
    {
        return 1.25f;
    }
}
