using UnityEngine;

public interface IStarLayerRenderer
{
    string Id { get; }
    void ConfigureParticleSystem(ParticleSystem ps, StarLayerDef def, StarLayerSlot slot, bool useGlobalColor, Color globalColor, float globalIntensity, float globalSizeScale, float globalLifetimeScale);
    float ComputeAlpha(StarLayerDef def, float age, float life, uint seed);
    Color ApplyColor(StarLayerDef def, Color c, float t, uint seed);
    float ApplySize(StarLayerDef def, float sizeScale, float t);
    Vector3 ApplyOffsets(StarLayerDef def, Vector3 pos, Vector3 velocity, float age, uint seed);
    bool ShouldEmitParticle(StarLayerDef def, uint seed, float age);
    Color ApplyTrailColor(StarLayerDef def, Color baseColor, float t);
    float GetTrailLengthScale(StarLayerDef def);
    float GetTrailAlphaScale(StarLayerDef def, float t, uint seed, int sampleIndex);
    float GetTrailSizeScale(StarLayerDef def, float t, uint seed, int sampleIndex);
    bool ShouldEmitTrailSample(StarLayerDef def, float t, uint seed, int sampleIndex);
    void EmitExtras(ParticleSystem.Particle[] buffer, ref int write, StarLayerDef def, ParticleSystem.Particle p, Color c, float life, float age);
}

public abstract class StarLayerRendererBase : IStarLayerRenderer
{
    public virtual string Id => "";

    public virtual void ConfigureParticleSystem(ParticleSystem ps, StarLayerDef def, StarLayerSlot slot, bool useGlobalColor, Color globalColor, float globalIntensity, float globalSizeScale, float globalLifetimeScale)
    {
        if (ps == null || def == null) return;

        Color baseColor = def.defaultColor;
        if (slot != null && slot.overrideColor)
            baseColor = slot.color;
        if (useGlobalColor)
            baseColor = globalColor;

        float intensity = def.defaultIntensity * globalIntensity * (slot != null ? slot.intensityScale : 1f);
        float sizeScale = def.defaultSizeScale * globalSizeScale * (slot != null ? slot.sizeScale : 1f);
        float lifetimeScale = def.defaultLifetimeScale * globalLifetimeScale * (slot != null ? slot.lifetimeScale : 1f);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(baseColor.r * intensity, baseColor.g * intensity, baseColor.b * intensity, baseColor.a));
        if (def.startLifetime > 0f)
            main.startLifetime = def.startLifetime * lifetimeScale;
        if (def.startSpeed > 0f)
            main.startSpeed = def.startSpeed;
        if (def.startSize > 0f)
            main.startSize = def.startSize * sizeScale;
        if (Mathf.Abs(def.gravityModifier) > 0.001f)
            main.gravityModifier = def.gravityModifier;
        else
            main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = false;

        var trails = ps.trails;
        trails.enabled = def.enableTrails;
        if (def.enableTrails)
        {
            trails.lifetime = def.trailsLifetime > 0f ? def.trailsLifetime : 0.6f;
            trails.ribbonCount = 1;
        }

        var noise = ps.noise;
        noise.enabled = def.enableNoise;
        if (def.enableNoise)
        {
            noise.strength = def.noiseStrength;
            noise.frequency = def.noiseFrequency;
        }
    }

    public virtual float ComputeAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        return 1f;
    }

    public virtual Color ApplyColor(StarLayerDef def, Color c, float t, uint seed)
    {
        return c;
    }

    public virtual float ApplySize(StarLayerDef def, float sizeScale, float t)
    {
        return sizeScale;
    }

    public virtual Vector3 ApplyOffsets(StarLayerDef def, Vector3 pos, Vector3 velocity, float age, uint seed)
    {
        if (def == null) return pos;

        if (def.enableNoise && def.noiseStrength > 0f)
        {
            float f = Mathf.Max(0.01f, def.noiseFrequency);
            float t = age * f;
            float sx = StarLayerRenderUtil.Hash01(seed) * 10f;
            float sy = StarLayerRenderUtil.Hash01(seed ^ 0x68bc21u) * 10f;
            float sz = StarLayerRenderUtil.Hash01(seed ^ 0x02e5be93u) * 10f;
            float nx = Mathf.PerlinNoise(sx, t) - 0.5f;
            float ny = Mathf.PerlinNoise(sy, t + 3.3f) - 0.5f;
            float nz = Mathf.PerlinNoise(sz, t + 7.7f) - 0.5f;
            pos += new Vector3(nx, ny, nz) * def.noiseStrength;
        }

        if (Mathf.Abs(def.gravityModifier) > 0.001f)
            pos += Vector3.down * (0.5f * def.gravityModifier * age * age);

        return pos;
    }

    public virtual bool ShouldEmitParticle(StarLayerDef def, uint seed, float age)
    {
        return true;
    }

    public virtual Color ApplyTrailColor(StarLayerDef def, Color baseColor, float t)
    {
        return baseColor;
    }

    public virtual float GetTrailLengthScale(StarLayerDef def)
    {
        return 1f;
    }

    public virtual float GetTrailAlphaScale(StarLayerDef def, float t, uint seed, int sampleIndex)
    {
        return Mathf.Lerp(0.8f, 0.1f, t);
    }

    public virtual float GetTrailSizeScale(StarLayerDef def, float t, uint seed, int sampleIndex)
    {
        return Mathf.Lerp(0.9f, 0.35f, t);
    }

    public virtual bool ShouldEmitTrailSample(StarLayerDef def, float t, uint seed, int sampleIndex)
    {
        return true;
    }

    public virtual void EmitExtras(ParticleSystem.Particle[] buffer, ref int write, StarLayerDef def, ParticleSystem.Particle p, Color c, float life, float age)
    {
    }

    protected void AddDerivedParticle(ParticleSystem.Particle[] buffer, ref int write, ParticleSystem.Particle baseP, Color c, Vector3 pos, float sizeScale)
    {
        if (write >= buffer.Length) return;
        if (!StarLayerRenderUtil.IsFinite(pos))
            return;
        var p = baseP;
        p.position = pos;
        p.startColor = c;
        p.startSize *= sizeScale;
        buffer[write++] = p;
    }
}
