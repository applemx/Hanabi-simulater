using System;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSim
{
    public int AliveCount => aliveCount;
    public const int TrailSamples = 24;

    readonly Vector3[] pos;
    readonly Vector3[] vel;
    readonly float[] age;
    readonly float[] life;
    readonly Color32[] col;
    readonly float[] size;
    readonly ushort[] profileId;
    readonly uint[] seed;
    readonly Vector3[] trailHistory;
    readonly byte[] trailCursor;

    StarKind[] kindLookup;
    float[] brightnessLookup;

    int aliveCount;
    static readonly int StarKindCount = Enum.GetValues(typeof(StarKind)).Length;

    public ParticleSim(int max)
    {
        pos = new Vector3[max];
        vel = new Vector3[max];
        age = new float[max];
        life = new float[max];
        col = new Color32[max];
        size = new float[max];
        profileId = new ushort[max];
        seed = new uint[max];
        trailHistory = new Vector3[max * TrailSamples];
        trailCursor = new byte[max];
        aliveCount = 0;
    }

    public void Reset()
    {
        aliveCount = 0;
    }

    public void SetProfileLookup(IList<StarProfileDef> defs)
    {
        if (defs == null || defs.Count == 0)
        {
            kindLookup = null;
            brightnessLookup = null;
            return;
        }

        kindLookup = new StarKind[defs.Count];
        brightnessLookup = new float[defs.Count];
        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            kindLookup[i] = def != null ? def.kind : StarKind.Solid;
            brightnessLookup[i] = def != null ? Mathf.Clamp(def.brightness, 0f, 2f) : 1f;
        }
    }

    /// <summary>
    /// Append a segment of compiled particles into the live simulation.
    /// Supports per-particle delay via age = -delay.
    /// If capacity is exceeded, extra particles are dropped.
    /// </summary>
    public void AppendSpawn(ParticleInitV2[] init, int start, int count, Vector3 worldOrigin, Vector3 burstLocalOffset)
    {
        if (init == null || count <= 0) return;

        int room = pos.Length - aliveCount;
        if (room <= 0) return;

        int n = Mathf.Min(count, room);
        int write = aliveCount;

        for (int i = 0; i < n; i++)
        {
            var p = init[start + i];

            pos[write] = worldOrigin + burstLocalOffset + p.pos0Local;
            vel[write] = p.vel0Local;
            age[write] = -p.spawnDelay;
            life[write] = p.life;
            col[write] = p.color;
            size[write] = Mathf.Max(0.001f, p.size);
            profileId[write] = p.profileId;
            seed[write] = p.seed;
            InitTrail(write, pos[write]);
            write++;
        }

        aliveCount = write;
    }

    public void Step(float dt, Vector3 wind, float dragK)
    {
        Step(dt, wind, dragK, 1f);
    }

    public void Step(float dt, Vector3 wind, float dragK, float gravityScale)
    {
        Vector3 g = new Vector3(0f, -9.81f * gravityScale, 0f);

        int write = 0;
        for (int i = 0; i < aliveCount; i++)
        {
            float a = age[i] + dt;
            age[i] = a;

            if (a < 0f)
            {
                if (write != i) Copy(i, write);
                write++;
                continue;
            }

            float L = life[i];
            if (a >= L) continue;

            Vector3 v = vel[i];
            v += g * dt;
            v += wind * dt;

            float speed = v.magnitude;
            v += (-dragK * speed) * v * dt;

            Vector3 oldPos = pos[i];
            Vector3 p = oldPos + v * dt;

            vel[i] = v;
            PushTrail(i, oldPos);
            pos[i] = p;

            if (write != i) Copy(i, write);
            write++;
        }
        aliveCount = write;
    }

    void Copy(int src, int dst)
    {
        pos[dst] = pos[src];
        vel[dst] = vel[src];
        age[dst] = age[src];
        life[dst] = life[src];
        col[dst] = col[src];
        size[dst] = size[src];
        profileId[dst] = profileId[src];
        seed[dst] = seed[src];
        trailCursor[dst] = trailCursor[src];
        int srcBase = src * TrailSamples;
        int dstBase = dst * TrailSamples;
        for (int k = 0; k < TrailSamples; k++)
            trailHistory[dstBase + k] = trailHistory[srcBase + k];
    }

    public void FillParticles(ParticleSystem.Particle[] outParticles)
    {
        for (int i = 0; i < aliveCount; i++)
        {
            float a = Mathf.Max(0f, age[i]);
            float L = Mathf.Max(0.001f, life[i]);
            float t = a / L;

            float alphaMul = 1f;
            float sizeMul = 1f;
            StarKind kind = ResolveKind(profileId[i]);
            float brightness = ResolveBrightness(profileId[i]);
            float phase = Hash01(seed[i]);

            switch (kind)
            {
                case StarKind.Tail:
                    // Chrysanthemum: longer-lasting, visible trailing sparks.
                    alphaMul *= Mathf.Lerp(2.2f, 1.0f, t);
                    sizeMul *= Mathf.Lerp(1.0f, 0.55f, t);
                    break;
                case StarKind.Comet:
                    alphaMul *= Mathf.Lerp(1.2f, 0.6f, t);
                    sizeMul *= 1.35f;
                    break;
                case StarKind.Solid:
                    // Peony: clean bloom, no tail, quicker fade.
                    alphaMul *= Mathf.Lerp(1.0f, 0.12f, t);
                    break;
                case StarKind.Strobe:
                {
                    float freq = 10f;
                    float st = 0.5f + 0.5f * Mathf.Sin((t * freq + phase) * Mathf.PI * 2f);
                    alphaMul *= Mathf.Lerp(0.2f, 1.0f, st);
                    break;
                }
                case StarKind.Glitter:
                {
                    int tick = (int)(a * 30f);
                    float sparkle = Hash01(seed[i] ^ (uint)tick);
                    alphaMul *= 0.6f + 0.6f * sparkle;
                    break;
                }
                case StarKind.Crackle:
                {
                    if (t > 0.65f)
                    {
                        int tick = (int)(a * 40f);
                        float s = Hash01(seed[i] ^ (uint)tick);
                        alphaMul *= (s > 0.5f) ? 1.3f : 0.4f;
                    }
                    break;
                }
                case StarKind.Crossette:
                {
                    if (t > 0.6f)
                    {
                        float pulse = 0.6f + 0.4f * Mathf.Sin((t * 6f + phase) * Mathf.PI * 2f);
                        alphaMul *= pulse;
                    }
                    break;
                }
                case StarKind.ColorChange:
                    break;
                default:
                    break;
            }

            float alpha01 = Mathf.Clamp01((1f - t) * alphaMul * brightness);
            byte alpha = (byte)Mathf.Clamp(alpha01 * 255f, 0f, 255f);

            byte r = col[i].r;
            byte g = col[i].g;
            byte b = col[i].b;
            if (kind == StarKind.ColorChange)
            {
                float u = Mathf.Clamp01((t - 0.4f) / 0.6f);
                r = (byte)Mathf.Clamp(Mathf.Lerp(r, 255f, u), 0f, 255f);
                g = (byte)Mathf.Clamp(Mathf.Lerp(g, 255f, u), 0f, 255f);
                b = (byte)Mathf.Clamp(Mathf.Lerp(b, 255f, u), 0f, 255f);
            }

            outParticles[i].position = pos[i];
            outParticles[i].startColor = new Color32(r, g, b, alpha);

            outParticles[i].startSize = size[i] * sizeMul;
            outParticles[i].startLifetime = L;
            outParticles[i].remainingLifetime = Mathf.Max(0.01f, L - a);
            outParticles[i].velocity = vel[i];
            outParticles[i].randomSeed = seed[i];
        }
    }

    public void FillParticlesByKind(ParticleSystem.Particle[][] outBuffers, int[] counts)
    {
        if (outBuffers == null || counts == null) return;
        Array.Clear(counts, 0, counts.Length);
        if (outBuffers.Length == 0) return;
        int fallback = FindFirstBuffer(outBuffers);
        if (fallback < 0) return;

        for (int i = 0; i < aliveCount; i++)
        {
            float a = Mathf.Max(0f, age[i]);
            float L = Mathf.Max(0.001f, life[i]);
            float t = a / L;

            float alphaMul = 1f;
            float sizeMul = 1f;
            StarKind kind = ResolveKind(profileId[i]);
            float brightness = ResolveBrightness(profileId[i]);
            float phase = Hash01(seed[i]);

            switch (kind)
            {
                case StarKind.Tail:
                    // Chrysanthemum: longer-lasting, visible trailing sparks.
                    alphaMul *= Mathf.Lerp(2.2f, 1.0f, t);
                    sizeMul *= Mathf.Lerp(1.0f, 0.55f, t);
                    break;
                case StarKind.Comet:
                    alphaMul *= Mathf.Lerp(1.2f, 0.6f, t);
                    sizeMul *= 1.35f;
                    break;
                case StarKind.Solid:
                    // Peony: clean bloom, no trail, quicker fade to avoid streak feel.
                    alphaMul *= Mathf.Lerp(1.0f, 0.12f, t);
                    break;
                case StarKind.Strobe:
                {
                    float freq = 10f;
                    float st = 0.5f + 0.5f * Mathf.Sin((t * freq + phase) * Mathf.PI * 2f);
                    alphaMul *= Mathf.Lerp(0.2f, 1.0f, st);
                    break;
                }
                case StarKind.Glitter:
                {
                    int tick = (int)(a * 30f);
                    float sparkle = Hash01(seed[i] ^ (uint)tick);
                    alphaMul *= 0.6f + 0.6f * sparkle;
                    break;
                }
                case StarKind.Crackle:
                {
                    if (t > 0.65f)
                    {
                        int tick = (int)(a * 40f);
                        float s = Hash01(seed[i] ^ (uint)tick);
                        alphaMul *= (s > 0.5f) ? 1.3f : 0.4f;
                    }
                    break;
                }
                case StarKind.Crossette:
                {
                    if (t > 0.6f)
                    {
                        float pulse = 0.6f + 0.4f * Mathf.Sin((t * 6f + phase) * Mathf.PI * 2f);
                        alphaMul *= pulse;
                    }
                    break;
                }
                case StarKind.ColorChange:
                    break;
                default:
                    break;
            }

            float alpha01 = Mathf.Clamp01((1f - t) * alphaMul * brightness);
            byte alpha = (byte)Mathf.Clamp(alpha01 * 255f, 0f, 255f);

            byte r = col[i].r;
            byte g = col[i].g;
            byte b = col[i].b;
            if (kind == StarKind.ColorChange)
            {
                float u = Mathf.Clamp01((t - 0.4f) / 0.6f);
                r = (byte)Mathf.Clamp(Mathf.Lerp(r, 255f, u), 0f, 255f);
                g = (byte)Mathf.Clamp(Mathf.Lerp(g, 255f, u), 0f, 255f);
                b = (byte)Mathf.Clamp(Mathf.Lerp(b, 255f, u), 0f, 255f);
            }

            int slot = (int)kind;
            if (slot < 0 || slot >= outBuffers.Length || outBuffers[slot] == null)
                slot = fallback;
            var target = outBuffers[slot];
            if (target == null) continue;

            int writeIndex = counts[slot];
            if (writeIndex < target.Length)
            {
                target[writeIndex].position = pos[i];
                target[writeIndex].startColor = new Color32(r, g, b, alpha);
                target[writeIndex].startSize = size[i] * sizeMul;
                target[writeIndex].startLifetime = L;
                target[writeIndex].remainingLifetime = Mathf.Max(0.01f, L - a);
                target[writeIndex].velocity = vel[i];
                target[writeIndex].randomSeed = seed[i];
                counts[slot]++;
            }

            if (kind == StarKind.Tail)
            {
                EmitTrailSamples(i, target, counts, slot, r, g, b, alpha, size[i] * sizeMul, L, a, TrailSamples);
                EmitVelocityStreak(target, counts, slot, pos[i], vel[i], new Color32(r, g, b, alpha), size[i] * sizeMul, L, a);
            }
            else if (kind == StarKind.Comet)
                EmitTrailSamples(i, target, counts, slot, r, g, b, alpha, size[i] * sizeMul, L, a, 10);
        }
    }

    StarKind ResolveKind(ushort id)
    {
        if (kindLookup != null && id < kindLookup.Length) return kindLookup[id];
        if (id < StarKindCount) return (StarKind)id;
        return StarKind.Solid;
    }

    float ResolveBrightness(ushort id)
    {
        if (brightnessLookup != null && id < brightnessLookup.Length) return brightnessLookup[id];
        return 1f;
    }

    static int FindFirstBuffer(ParticleSystem.Particle[][] buffers)
    {
        for (int i = 0; i < buffers.Length; i++)
        {
            if (buffers[i] != null) return i;
        }
        return -1;
    }

    void InitTrail(int index, Vector3 pos)
    {
        int baseIdx = index * TrailSamples;
        for (int i = 0; i < TrailSamples; i++)
            trailHistory[baseIdx + i] = pos;
        trailCursor[index] = 0;
    }

    void PushTrail(int index, Vector3 pos)
    {
        int baseIdx = index * TrailSamples;
        int cursor = trailCursor[index];
        trailHistory[baseIdx + cursor] = pos;
        trailCursor[index] = (byte)((cursor + 1) % TrailSamples);
    }

    void EmitTrailSamples(int index, ParticleSystem.Particle[] target, int[] counts, int slot, byte r, byte g, byte b, byte alpha, float size, float life, float age, int sampleCount)
    {
        if (target == null || counts == null) return;
        int baseIdx = index * TrailSamples;
        int cursor = trailCursor[index];

        int n = Mathf.Clamp(sampleCount, 0, TrailSamples);
        for (int k = 0; k < n; k++)
        {
            int sampleIdx = cursor - 1 - k;
            if (sampleIdx < 0) sampleIdx += TrailSamples;
            Vector3 p = trailHistory[baseIdx + sampleIdx];

            float t = (k + 1f) / (n + 1f);
            float alphaScale = Mathf.Lerp(0.95f, 0.08f, t);
            float sizeScale = Mathf.Lerp(0.85f, 0.3f, t);
            byte a = (byte)Mathf.Clamp(alpha * alphaScale, 0f, 255f);

            AddTrailPoint(target, counts, slot, p, new Color32(r, g, b, a), size * sizeScale, life, age);
        }
    }

    static void AddTrailPoint(ParticleSystem.Particle[] target, int[] counts, int slot, Vector3 pos, Color32 color, float size, float life, float age)
    {
        if (target == null || counts == null) return;
        int writeIndex = counts[slot];
        if (writeIndex >= target.Length) return;

        target[writeIndex].position = pos;
        target[writeIndex].startColor = color;
        target[writeIndex].startSize = size;
        target[writeIndex].startLifetime = life;
        target[writeIndex].remainingLifetime = Mathf.Max(0.01f, life - age);
        target[writeIndex].velocity = Vector3.zero;
        target[writeIndex].randomSeed = (uint)(writeIndex * 2654435761u);
        counts[slot]++;
    }

    static void EmitVelocityStreak(ParticleSystem.Particle[] target, int[] counts, int slot, Vector3 pos, Vector3 vel, Color32 color, float size, float life, float age)
    {
        if (target == null || counts == null) return;
        float speed = vel.magnitude;
        if (speed < 0.01f) return;

        Vector3 dir = vel / speed;
        float len = Mathf.Clamp(speed * 0.03f, 0.03f, 0.6f);
        for (int k = 1; k <= 3; k++)
        {
            float t = k / 3f;
            Vector3 p = pos - dir * (len * t);
            float alphaScale = Mathf.Lerp(0.7f, 0.1f, t);
            float sizeScale = Mathf.Lerp(0.8f, 0.35f, t);
            byte a = (byte)Mathf.Clamp(color.a * alphaScale, 0f, 255f);
            AddTrailPoint(target, counts, slot, p, new Color32(color.r, color.g, color.b, a), size * sizeScale, life, age);
        }
    }

    static float Hash01(uint x)
    {
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        return (x & 0x00FFFFFF) / 16777215.0f;
    }
}
