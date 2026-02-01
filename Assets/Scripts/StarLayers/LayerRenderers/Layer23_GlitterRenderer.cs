using UnityEngine;

public sealed class Layer23_GlitterRenderer : StarLayerRendererBase
{
    public override string Id => "glitter";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float freq = Mathf.Max(2f, def.strobeFrequency);
        float phase = StarLayerRenderUtil.Hash01(seed);
        float cycle = age * freq + phase;
        float frac = cycle - Mathf.Floor(cycle);
        float spike = Mathf.Clamp01(1f - frac / 0.18f);
        int tick = Mathf.FloorToInt(age * freq);
        float sparkle = StarLayerRenderUtil.Hash01(seed ^ (uint)tick);
        return 0.1f + spike * (0.9f + 0.5f * sparkle);
    }

    public override Color ApplyColor(StarLayerDef def, Color c, float t, uint seed)
    {
        float r = StarLayerRenderUtil.Hash01(seed ^ 0x9e3779b9u);
        var warm = new Color(1f, 0.95f, 0.7f, c.a);
        return Color.Lerp(c, warm, 0.35f * r);
    }

    public override Vector3 ApplyOffsets(StarLayerDef def, Vector3 pos, Vector3 velocity, float age, uint seed)
    {
        pos = base.ApplyOffsets(def, pos, velocity, age, seed);
        float drift = Mathf.Clamp(def.defaultStartDelay, 0.1f, 0.6f);
        float phase = StarLayerRenderUtil.Hash01(seed) * Mathf.PI * 2f;
        Vector3 delayOffset = velocity * Mathf.Clamp(drift, 0.1f, 0.45f);
        pos -= delayOffset;
        pos += new Vector3(Mathf.Sin(age * 3f + phase), Mathf.Cos(age * 2.2f + phase), 0f) * 0.03f;
        pos -= Vector3.up * drift * 0.03f;
        return pos;
    }
}
