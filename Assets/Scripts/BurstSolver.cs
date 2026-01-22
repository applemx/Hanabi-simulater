using System;
using System.Collections.Generic;
using UnityEngine;

public struct ParticleInit
{
    public Vector3 pos0;
    public Vector3 vel0;
    public float life;
    public Color32 color;
    public float delay;
}

public static class BurstSolver
{
    /// <summary>
    /// MVP: solve all star voxels (single-shot).
    /// </summary>
    public static List<ParticleInit> Solve(FireworkBlueprint bp, PackedVolume pv)
    {
        // Gather all star indices and forward to subset solver.
        var indices = new List<int>(capacity: 4096);
        for (int i = 0; i < pv.starMask.Length; i++)
            if (pv.starMask[i] != 0) indices.Add(i);

        return SolveSubset(bp, pv, indices);
    }

    /// <summary>
    /// Solve only the provided star cell indices, preserving order (one ParticleInit per index).
    /// This is used by the ignition-driven multi-burst compiler.
    /// </summary>
    public static List<ParticleInit> SolveSubset(FireworkBlueprint bp, PackedVolume pv, IReadOnlyList<int> starCellIndices, List<Color32> paletteOverride = null)
    {
        var outList = new List<ParticleInit>(capacity: Mathf.Max(64, starCellIndices.Count));
        var rng = new System.Random(bp.seed);

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
            Vector3 dir = (p - center).normalized;

            float localAvg = SampleLocalChargeAvg(pv, x, y, z);
            float rayInt = SampleRayChargeIntegral(pv, p, steps: 24);

            float speed = bp.intent.baseSpeed * (1.0f + 0.6f * localAvg + 0.9f * rayInt);

            float jitter = bp.intent.jitter;
            Vector3 j = RandomUnitVector(rng) * jitter;

            float paperOcclusion = (pv.paperCellWallId[idx] != 0) ? 1f : 0f;
            float collimation = Mathf.Lerp(1f, 0.6f, paperOcclusion);

            Vector3 vDir = (dir + j * collimation).normalized;

            float life = bp.intent.life;
            float delay = paperOcclusion * 0.05f;

            int ci = pv.starColor[idx];
            var pal = (paletteOverride != null && paletteOverride.Count > 0) ? paletteOverride : bp.palette;
            Color32 color = (pal != null && pal.Count > 0)
                ? pal[Mathf.Clamp(ci, 0, pal.Count - 1)]
                : new Color32(255, 255, 255, 255);

            outList.Add(new ParticleInit
            {
                pos0 = p,
                vel0 = vDir * speed,
                life = life,
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
}
