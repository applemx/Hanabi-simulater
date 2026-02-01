using UnityEngine;

public sealed class Layer09_DarkPrimeRenderer : StarLayerRendererBase
{
    public override string Id => "dark_prime";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float t = life > 0f ? Mathf.Clamp01(age / life) : 0f;
        return Mathf.Lerp(0.25f, 0.0f, Mathf.Pow(t, 0.5f));
    }

    public override Color ApplyColor(StarLayerDef def, Color c, float t, uint seed)
    {
        return Color.Lerp(c, Color.black, 0.85f);
    }

    public override float ApplySize(StarLayerDef def, float sizeScale, float t)
    {
        return sizeScale * 0.7f;
    }
}
