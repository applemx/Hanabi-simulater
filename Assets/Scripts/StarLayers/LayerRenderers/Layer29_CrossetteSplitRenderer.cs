using UnityEngine;

public sealed class Layer29_CrossetteSplitRenderer : StarLayerRendererBase
{
    public override string Id => "crossette_split";

    public override void EmitExtras(ParticleSystem.Particle[] buffer, ref int write, StarLayerDef def, ParticleSystem.Particle p, Color c, float life, float age)
    {
        EmitCrossette(buffer, ref write, p, c, life, age);
    }

    void EmitCrossette(ParticleSystem.Particle[] buffer, ref int write, ParticleSystem.Particle p, Color c, float life, float age)
    {
        if (life <= 0f) return;
        float t = age / life;
        if (t < 0.55f) return;

        float splitAge = Mathf.Max(0f, age - life * 0.55f);
        float offset = Mathf.Clamp(splitAge * 1.6f, 0.05f, 1.2f);
        Vector3 dir = p.velocity.sqrMagnitude > 0.0001f ? p.velocity.normalized : Vector3.right;
        Vector3 right = Vector3.Cross(dir, Vector3.up);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.Cross(dir, Vector3.forward);
        right.Normalize();
        Vector3 up = Vector3.Cross(right, dir).normalized;

        AddDerivedParticle(buffer, ref write, p, c, p.position + right * offset, 0.75f);
        AddDerivedParticle(buffer, ref write, p, c, p.position - right * offset, 0.75f);
        AddDerivedParticle(buffer, ref write, p, c, p.position + up * offset, 0.75f);
        AddDerivedParticle(buffer, ref write, p, c, p.position - up * offset, 0.75f);
    }
}
