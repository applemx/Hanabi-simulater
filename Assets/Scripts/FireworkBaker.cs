using Random = System.Random;
using UnityEngine;

/// <summary>
/// Blueprint -> PackedVolume (voxel) (compile-time)
///
/// This bake is intentionally simple but "production shaped":
/// - Outside the shell sphere: empty (charge=0)
/// - Inside the sphere: fuel (charge=255)
/// - Stars: voxelized to starMask/starColor
/// - Paper walls: voxelized to paperCellWallId + paperStrength
/// </summary>
public static class FireworkBaker
{
    public static PackedVolume Bake(FireworkBlueprint bp)
    {
        var rng = new Random(bp.seed);
        return Bake(bp, rng);
    }

    public static PackedVolume Bake(FireworkBlueprint bp, Random rng)
    {
        int res = ShellRes(bp.shellSize);
        var pv = new PackedVolume(res);

        float invRes = 1f / res;

        // 1) Fill: sphere interior as fuel; outside empty
        for (int z = 0; z < res; z++)
        {
            float fz = ((z + 0.5f) * invRes) * 2f - 1f;
            for (int y = 0; y < res; y++)
            {
                float fy = ((y + 0.5f) * invRes) * 2f - 1f;
                for (int x = 0; x < res; x++)
                {
                    float fx = ((x + 0.5f) * invRes) * 2f - 1f;
                    int idx = pv.Index(x, y, z);

                    float r2 = fx * fx + fy * fy + fz * fz;
                    if (r2 > 1f)
                    {
                        pv.charge[idx] = 0;
                        continue;
                    }

                    pv.charge[idx] = 255;
                }
            }
        }

        // 2) Stars (MVP: ring skeleton)
        int paletteCount = Mathf.Max(1, bp.palette != null ? bp.palette.Count : 1);
        var ring = bp.ring;
        for (int i = 0; i < ring.count; i++)
        {
            Vector3 dir = RandomOnUnitSphere(rng);
            Vector3 center = dir * ring.radius;
            Vector3 jitter = RandomInsideUnitSphere(rng) * ring.thickness;
            Vector3 p = center + jitter;
            if (p.sqrMagnitude > 1f) p = p.normalized; // keep inside

            int idx = V3ToIndex(p, res);
            if (pv.charge[idx] == 0) continue; // should not happen, but just in case

            pv.starMask[idx] = 1;
            pv.starColor[idx] = (byte)rng.Next(paletteCount);
        }

        // 3) Paper walls (disc on plane)
        if (bp.paper != null && bp.paper.Count > 0)
        {
            // thickness in local space: about ~2 voxels (MVP)
            float halfThickness = Mathf.Max(invRes * 2.0f, 0.01f);

            for (int pIdx = 0; pIdx < bp.paper.Count; pIdx++)
            {
                var paper = bp.paper[pIdx];
                if (paper.shape != PaperShape.Disc) continue;

                Vector3 n = paper.normal;
                if (n.sqrMagnitude < 1e-8f) n = Vector3.up;
                n.Normalize();

                float radius = Mathf.Max(0.001f, paper.radius);
                float radius2 = radius * radius;

                // brute-force voxelization (OK for MVP)
                for (int z = 0; z < res; z++)
                {
                    float fz = ((z + 0.5f) * invRes) * 2f - 1f;
                    for (int y = 0; y < res; y++)
                    {
                        float fy = ((y + 0.5f) * invRes) * 2f - 1f;
                        for (int x = 0; x < res; x++)
                        {
                            float fx = ((x + 0.5f) * invRes) * 2f - 1f;
                            int idx = pv.Index(x, y, z);

                            if (pv.charge[idx] == 0) continue; // outside shell

                            Vector3 v = new Vector3(fx, fy, fz) - paper.center;

                            float d = Vector3.Dot(v, n);
                            if (Mathf.Abs(d) > halfThickness) continue;

                            Vector3 inPlane = v - n * d;
                            if (inPlane.sqrMagnitude > radius2) continue;

                            // Keep strongest wall if overlaps
                            byte s = paper.strength;
                            if (s >= pv.paperStrength[idx])
                            {
                                pv.paperCellWallId[idx] = paper.wallId;
                                pv.paperStrength[idx] = s;
                            }
                        }
                    }
                }
            }
        }

        // 4) Ensure igniters are traversable
        if (bp.igniters != null)
        {
            for (int i = 0; i < bp.igniters.Count; i++)
            {
                int idx = V3ToIndex(bp.igniters[i].posLocal, res);
                pv.charge[idx] = 255;
            }
        }

        return pv;
    }

    static int ShellRes(ShellSize size)
    {
        switch (size)
        {
            case ShellSize.Small: return 48;
            case ShellSize.Medium: return 64;
            case ShellSize.Large: return 80;
            default: return 64;
        }
    }

    static int V3ToIndex(Vector3 local, int res)
    {
        // local in [-1..1]
        float fx = (local.x * 0.5f) + 0.5f;
        float fy = (local.y * 0.5f) + 0.5f;
        float fz = (local.z * 0.5f) + 0.5f;

        int x = Mathf.Clamp((int)(fx * res), 0, res - 1);
        int y = Mathf.Clamp((int)(fy * res), 0, res - 1);
        int z = Mathf.Clamp((int)(fz * res), 0, res - 1);

        return x + res * (y + res * z);
    }

    static Vector3 RandomOnUnitSphere(Random rng)
    {
        // Marsaglia (fast)
        float x1, x2, s;
        do
        {
            x1 = (float)(rng.NextDouble() * 2.0 - 1.0);
            x2 = (float)(rng.NextDouble() * 2.0 - 1.0);
            s = x1 * x1 + x2 * x2;
        } while (s >= 1.0f || s < 1e-12f);

        float z = 1.0f - 2.0f * s;
        float t = 2.0f * Mathf.Sqrt(1.0f - s);
        float x = x1 * t;
        float y = x2 * t;
        return new Vector3(x, y, z);
    }

    static Vector3 RandomInsideUnitSphere(Random rng)
    {
        // Rejection sampling
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
