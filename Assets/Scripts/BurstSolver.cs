using System;
using System.Collections.Generic;
using UnityEngine;

public struct ParticleInit
{
    public Vector3 pos0;
    public Vector3 vel0;
    public float life;
    public float size;
    public Color32 color;
    public float delay;
}

public static class BurstSolver
{
    /// <summary>
    /// MVP: solve all star voxels (single-shot).
    /// </summary>
    public static List<ParticleInit> Solve(FireworkBlueprint bp, PackedVolume pv, StarProfileDef starProfile = null, WashiDef washiDef = null, WaruyakuDef waruyakuDef = null, HanabiDatabase db = null)
    {
        // Gather all star indices and forward to subset solver.
        var indices = new List<int>(capacity: 4096);
        for (int i = 0; i < pv.starMask.Length; i++)
            if (pv.starMask[i] != 0) indices.Add(i);

        return SolveSubset(bp, pv, indices, null, starProfile, washiDef, waruyakuDef, db);
    }

    /// <summary>
    /// Solve only the provided star cell indices, preserving order (one ParticleInit per index).
    /// This is used by the ignition-driven multi-burst compiler.
    /// </summary>
    public static List<ParticleInit> SolveSubset(FireworkBlueprint bp, PackedVolume pv, IReadOnlyList<int> starCellIndices, List<Color32> paletteOverride = null, StarProfileDef starProfile = null, WashiDef washiDef = null, WaruyakuDef waruyakuDef = null, HanabiDatabase db = null)
    {
        var outList = new List<ParticleInit>(capacity: Mathf.Max(64, starCellIndices.Count));
        var rng = new System.Random(bp.seed);

        float defaultBaseSpeed = bp.intent.baseSpeed;
        float defaultBaseLife = bp.intent.life;
        float defaultLifeJitter = 0f;
        float defaultSpeedJitter = 0f;
        StarKind defaultKind = StarKind.Solid;
        if (starProfile != null)
        {
            defaultBaseSpeed = starProfile.baseSpeed;
            defaultSpeedJitter = starProfile.speedJitter;
            defaultBaseLife = starProfile.baseLife;
            defaultLifeJitter = starProfile.lifeJitter;
            defaultKind = starProfile.kind;
        }

        float washiDelay = (washiDef != null) ? Mathf.Max(0f, washiDef.delaySeconds) : 0.05f;
        float washiCollimation = (washiDef != null) ? Mathf.Clamp01(washiDef.collimation) : 0.35f;

        float scatterStrength = 1f;
        float uniformity = Mathf.Clamp01(bp.intent.uniformity);
        if (waruyakuDef != null)
        {
            scatterStrength = Mathf.Clamp(waruyakuDef.scatterStrength, 0.5f, 2.0f);
            uniformity = Mathf.Clamp01(uniformity * waruyakuDef.uniformity);
        }

        int res = pv.res;
        int res2 = res * res;
        Vector3 center = Vector3.zero;

        for (int si = 0; si < starCellIndices.Count; si++)
        {
            int idx = starCellIndices[si];
            if (idx < 0 || idx >= pv.starMask.Length) continue;
            if (pv.starMask[idx] == 0) continue;

            int x = idx % res;
            int y = (idx / res) % res;
            int z = idx / res2;

            Vector3 p = pv.CellCenter(x, y, z);
            Vector3 posJitter = RandomInsideUnitSphere(rng) * (pv.cellSize * 0.45f);
            Vector3 pos = p + posJitter;
            Vector3 dir = (pos - center).normalized;

            float localAvg = SampleLocalChargeAvg(pv, x, y, z);
            float rayInt = SampleRayChargeIntegral(pv, pos, steps: 24);

            StarProfileDef profile = starProfile;
            if (db != null && pv.starProfileId != null)
            {
                int pid = pv.starProfileId[idx];
                var byId = db.GetStarById(pid);
                if (byId != null) profile = byId;
            }

            float baseSpeed = defaultBaseSpeed;
            float speedJitter = defaultSpeedJitter;
            float baseLife = defaultBaseLife;
            float lifeJitter = defaultLifeJitter;
            StarKind kind = defaultKind;
            if (profile != null)
            {
                baseSpeed = profile.baseSpeed;
                speedJitter = profile.speedJitter;
                baseLife = profile.baseLife;
                lifeJitter = profile.lifeJitter;
                kind = profile.kind;
            }

            float speed = baseSpeed * (1.0f + 0.6f * localAvg + 0.9f * rayInt);
            if (speedJitter > 0f)
            {
                float sj = ((float)rng.NextDouble() * 2f - 1f) * speedJitter;
                speed = Mathf.Max(0.01f, speed + sj);
            }
            speed = Mathf.Max(0.01f, speed * scatterStrength);

            float jitter = bp.intent.jitter;
            float jitterScale = scatterStrength * Mathf.Lerp(1.1f, 0.15f, uniformity);
            Vector3 j = RandomUnitVector(rng) * jitter * jitterScale;

            bool paperHit = (pv.paperCellWallId[idx] != 0);
            float paperStrength = 1f;
            if (paperHit && pv.paperStrength != null)
                paperStrength = Mathf.Clamp01(pv.paperStrength[idx] / 255f);

            Vector3 vDir = (dir + j).normalized;
            if (paperHit && washiCollimation > 0f && dir.sqrMagnitude > 1e-6f)
            {
                Vector3 vPar = Vector3.Dot(vDir, dir) * dir;
                Vector3 vPerp = vDir - vPar;
                float c = washiCollimation * paperStrength;
                vDir = (vPar + vPerp * (1f - c)).normalized;
            }
            float radialBias = Mathf.Clamp01(uniformity * 0.6f + (kind == StarKind.Tail ? 0.1f : 0f));
            if (radialBias > 0f && dir.sqrMagnitude > 1e-6f)
                vDir = Vector3.Slerp(vDir, dir, radialBias).normalized;

            float life = baseLife;
            if (lifeJitter > 0f)
            {
                float lj = ((float)rng.NextDouble() * 2f - 1f) * lifeJitter;
                life = Mathf.Max(0.05f, life + lj);
            }
            float size = 0.06f;
            if (profile != null)
            {
                size = profile.baseSize;
                if (profile.sizeJitter > 0f)
                {
                    float sj = ((float)rng.NextDouble() * 2f - 1f) * profile.sizeJitter;
                    size = Mathf.Max(0.01f, size + sj);
                }
            }
            float speedNorm = Mathf.InverseLerp(baseSpeed * 0.6f, baseSpeed * 1.6f, speed);
            float lifeScale = 1f;
            switch (kind)
            {
                case StarKind.Solid:
                    lifeScale = Mathf.Lerp(0.85f, 0.65f, speedNorm);
                    break;
                case StarKind.Tail:
                    lifeScale = Mathf.Lerp(1.25f, 1.05f, speedNorm);
                    break;
                case StarKind.Comet:
                    lifeScale = Mathf.Lerp(1.35f, 1.1f, speedNorm);
                    break;
                case StarKind.Glitter:
                    lifeScale = Mathf.Lerp(1.1f, 0.95f, speedNorm);
                    break;
                case StarKind.Crackle:
                    lifeScale = Mathf.Lerp(0.95f, 0.8f, speedNorm);
                    break;
                default:
                    lifeScale = Mathf.Lerp(1.0f, 0.85f, speedNorm);
                    break;
            }
            life = Mathf.Max(0.05f, life * lifeScale);
            float delay = paperHit ? (washiDelay * paperStrength) : 0f;

            int ci = pv.starColor[idx];
            var pal = (paletteOverride != null && paletteOverride.Count > 0) ? paletteOverride : bp.palette;
            Color32 color = (pal != null && pal.Count > 0)
                ? pal[Mathf.Clamp(ci, 0, pal.Count - 1)]
                : new Color32(255, 255, 255, 255);

            outList.Add(new ParticleInit
            {
                pos0 = pos,
                vel0 = vDir * speed,
                life = life,
                size = size,
                color = color,
                delay = delay
            });
        }

        return outList;
    }

    static float SampleLocalChargeAvg(PackedVolume pv, int cx, int cy, int cz)
    {
        int sum = 0, cnt = 0;
        for (int z = cz - 1; z <= cz + 1; z++)
            for (int y = cy - 1; y <= cy + 1; y++)
                for (int x = cx - 1; x <= cx + 1; x++)
                {
                    if (!pv.InBounds(x, y, z)) continue;
                    sum += pv.charge[pv.Index(x, y, z)];
                    cnt++;
                }
        return (cnt > 0) ? (sum / (255f * cnt)) : 0f;
    }

    static float SampleRayChargeIntegral(PackedVolume pv, Vector3 starPos, int steps)
    {
        Vector3 dir = starPos;
        float len = dir.magnitude;
        if (len < 1e-5f) return 0f;

        dir /= len;
        float sum = 0f;

        for (int i = 0; i < steps; i++)
        {
            float t = (i + 0.5f) / steps * len;
            Vector3 p = dir * t;

            int x = Mathf.Clamp((int)((p.x + 1f) * 0.5f * pv.res), 0, pv.res - 1);
            int y = Mathf.Clamp((int)((p.y + 1f) * 0.5f * pv.res), 0, pv.res - 1);
            int z = Mathf.Clamp((int)((p.z + 1f) * 0.5f * pv.res), 0, pv.res - 1);

            sum += pv.charge[pv.Index(x, y, z)] / 255f;
        }
        return sum / steps;
    }

    static Vector3 RandomUnitVector(System.Random rng)
    {
        float u = (float)rng.NextDouble();
        float v = (float)rng.NextDouble();
        float theta = 2f * Mathf.PI * u;
        float z = 2f * v - 1f;
        float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
        return new Vector3(r * Mathf.Cos(theta), r * Mathf.Sin(theta), z);
    }

    static Vector3 RandomInsideUnitSphere(System.Random rng)
    {
        while (true)
        {
            float x = (float)(rng.NextDouble() * 2.0 - 1.0);
            float y = (float)(rng.NextDouble() * 2.0 - 1.0);
            float z = (float)(rng.NextDouble() * 2.0 - 1.0);
            Vector3 v = new Vector3(x, y, z);
            if (v.sqrMagnitude <= 1.0f) return v;
        }
    }
}
