using System.Collections.Generic;
using UnityEngine;

public class ParticleSim
{
    public int AliveCount => aliveCount;

    readonly Vector3[] pos;
    readonly Vector3[] vel;
    readonly float[] age;
    readonly float[] life;
    readonly Color32[] col;
    readonly float[] size;
    readonly ushort[] profileId;
    readonly uint[] seed;

    int aliveCount;

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
        aliveCount = 0;
    }

    public void Reset()
    {
        aliveCount = 0;
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
            write++;
        }

        aliveCount = write;
    }

    public void Step(float dt, Vector3 wind, float dragK)
    {
        Vector3 g = new Vector3(0f, -9.81f, 0f);

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

            Vector3 p = pos[i] + v * dt;

            vel[i] = v;
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
    }

    public void FillParticles(ParticleSystem.Particle[] outParticles)
    {
        for (int i = 0; i < aliveCount; i++)
        {
            float a = Mathf.Max(0f, age[i]);
            float L = Mathf.Max(0.001f, life[i]);
            float t = a / L;

            byte alpha = (byte)Mathf.Clamp((1f - t) * 255f, 0f, 255f);

            outParticles[i].position = pos[i];
            outParticles[i].startColor = new Color32(col[i].r, col[i].g, col[i].b, alpha);

            outParticles[i].startSize = size[i];
            outParticles[i].startLifetime = L;
            outParticles[i].remainingLifetime = Mathf.Max(0.01f, L - a);
            outParticles[i].velocity = Vector3.zero;
            outParticles[i].randomSeed = seed[i];
        }
    }
}
