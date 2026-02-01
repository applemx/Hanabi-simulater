using UnityEngine;

public sealed class Layer28_CrackleRenderer : StarLayerRendererBase
{
    public override string Id => "crackle";

    public override float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        if (life > 0f && age > life * 0.65f)
        {
            int tick = Mathf.FloorToInt(age * 40f);
            float s = StarLayerRenderUtil.Hash01(seed ^ (uint)tick);
            return (s > 0.5f) ? 1.3f : 0.25f;
        }
        return 0.1f;
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

    public override void EmitExtras(ParticleSystem.Particle[] buffer, ref int write, StarLayerDef def, ParticleSystem.Particle p, Color c, float life, float age)
    {
        EmitCrackle(buffer, ref write, p, c, life, age);
    }

    void EmitCrackle(ParticleSystem.Particle[] buffer, ref int write, ParticleSystem.Particle p, Color c, float life, float age)
    {
        if (life <= 0f) return;
        float t = age / life;
        if (t < 0.7f) return;

        float burst = Mathf.Clamp01((t - 0.7f) / 0.3f);
        for (int i = 0; i < 4; i++)
        {
            uint s = p.randomSeed ^ (uint)(i * 2654435761u);
            Vector3 dir = StarLayerRenderUtil.HashUnitVector(s);
            float radius = 0.15f + 0.35f * burst;
            Vector3 pos = p.position + dir * radius;
            AddDerivedParticle(buffer, ref write, p, c, pos, 0.6f);
        }
    }
}
