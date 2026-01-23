using System;
using System.Collections.Generic;
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

        // Resolve waruyaku tag -> base charge (MVP mapping)
        byte waruyakuCharge = 200;
        if (!string.IsNullOrWhiteSpace(bp.waruyakuTag))
        {
            if (bp.waruyakuTag.IndexOf("L", StringComparison.OrdinalIgnoreCase) >= 0) waruyakuCharge = 220;
            else if (bp.waruyakuTag.IndexOf("H", StringComparison.OrdinalIgnoreCase) >= 0) waruyakuCharge = 160;
        }

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

                    pv.charge[idx] = waruyakuCharge;
                }
            }
        }

        // 1.5) Waruyaku overrides (MVP: spheres)
        if (bp.waruyaku != null && bp.waruyaku.Count > 0)
        {
            for (int w = 0; w < bp.waruyaku.Count; w++)
            {
                var wk = bp.waruyaku[w];
                if (wk.shape != WaruyakuShape.Sphere) continue;
                if (wk.strength == 0) continue;

                float radius = Mathf.Max(0.001f, wk.radius);
                float radius2 = radius * radius;

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

                            Vector3 v = new Vector3(fx, fy, fz) - wk.center;
                            if (v.sqrMagnitude > radius2) continue;

                            pv.charge[idx] = wk.strength;
                        }
                    }
                }
            }
        }

        // 1.6) Waruyaku paint strokes (optional)
        if (bp.waruyakuStrokes != null && bp.waruyakuStrokes.Count > 0)
        {
            ApplyWaruyakuStrokes(pv, bp.waruyakuStrokes, res);
        }

        // 2) Stars (placed points or ring skeleton)
        int paletteCount = Mathf.Max(1, bp.palette != null ? bp.palette.Count : 1);
        if (bp.stars != null && bp.stars.Count > 0)
        {
            ApplyStarPoints(pv, bp.stars, paletteCount, rng);
        }
        else
        {
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
        }

        // Resolve washi tag -> wall id/strength (simple mapping for MVP)
        byte washiWallId = 1;
        byte washiStrength = 180;
        if (!string.IsNullOrWhiteSpace(bp.washiTag))
        {
            if (bp.washiTag.IndexOf("Strong", StringComparison.OrdinalIgnoreCase) >= 0) washiStrength = 230;
            else if (bp.washiTag.IndexOf("Light", StringComparison.OrdinalIgnoreCase) >= 0) washiStrength = 100;
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
                            byte s = (paper.strength > 0) ? paper.strength : washiStrength;
                            if (s >= pv.paperStrength[idx])
                            {
                                pv.paperCellWallId[idx] = (paper.wallId != 0) ? paper.wallId : washiWallId;
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
                Vector3 pLocal = bp.igniters[i].posLocal;
                int idx = V3ToIndex(pLocal, res);
                pv.charge[idx] = 255;

                if (pv.fuseMask != null)
                {
                    RasterizeLine(Vector3.zero, pLocal, res, (x, y, z) =>
                    {
                        int fIdx = pv.Index(x, y, z);
                        pv.fuseMask[fIdx] = 1;
                    });
                }
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

    static void ApplyStarPoints(PackedVolume pv, IList<StarPoint> stars, int paletteCount, Random rng)
    {
        int res = pv.res;
        for (int i = 0; i < stars.Count; i++)
        {
            var sp = stars[i];
            Vector3 dir = sp.dir;
            if (dir.sqrMagnitude < 1e-8f) dir = Vector3.up;
            dir.Normalize();

            float radius = Mathf.Clamp01(sp.radius);
            Vector3 p = dir * radius;

            LocalToVoxel(p, res, out int cx, out int cy, out int cz);
            byte color = (byte)rng.Next(paletteCount);
            int size = Mathf.Max(1, sp.size);
            StampStarCube(pv, cx, cy, cz, size, color);
        }
    }

    static void StampStarCube(PackedVolume pv, int cx, int cy, int cz, int size, byte color)
    {
        int res = pv.res;
        int half = size / 2;
        int startX = cx - half;
        int startY = cy - half;
        int startZ = cz - half;

        for (int z = startZ; z < startZ + size; z++)
        {
            if ((uint)z >= (uint)res) continue;
            for (int y = startY; y < startY + size; y++)
            {
                if ((uint)y >= (uint)res) continue;
                for (int x = startX; x < startX + size; x++)
                {
                    if ((uint)x >= (uint)res) continue;
                    int idx = pv.Index(x, y, z);
                    if (pv.charge[idx] == 0) continue; // outside shell
                    pv.starMask[idx] = 1;
                    pv.starColor[idx] = color;
                }
            }
        }
    }

    static void ApplyWaruyakuStrokes(PackedVolume pv, IList<WaruyakuStroke> strokes, int res)
    {
        for (int i = 0; i < strokes.Count; i++)
        {
            var stroke = strokes[i];
            if (stroke.strength == 0) continue;

            Vector3 dir = stroke.dir;
            if (dir.sqrMagnitude < 1e-8f) dir = Vector3.up;
            dir.Normalize();

            float radius = Mathf.Clamp01(stroke.radius);
            float brush = Mathf.Max(0.001f, stroke.brushRadius);
            float brush2 = brush * brush;

            Vector3 center = dir * radius;

            int minX = LocalToVoxelIndex(center.x - brush, res) - 1;
            int maxX = LocalToVoxelIndex(center.x + brush, res) + 1;
            int minY = LocalToVoxelIndex(center.y - brush, res) - 1;
            int maxY = LocalToVoxelIndex(center.y + brush, res) + 1;
            int minZ = LocalToVoxelIndex(center.z - brush, res) - 1;
            int maxZ = LocalToVoxelIndex(center.z + brush, res) + 1;

            minX = Mathf.Clamp(minX, 0, res - 1);
            maxX = Mathf.Clamp(maxX, 0, res - 1);
            minY = Mathf.Clamp(minY, 0, res - 1);
            maxY = Mathf.Clamp(maxY, 0, res - 1);
            minZ = Mathf.Clamp(minZ, 0, res - 1);
            maxZ = Mathf.Clamp(maxZ, 0, res - 1);

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        int idx = pv.Index(x, y, z);
                        if (pv.charge[idx] == 0) continue; // outside shell

                        Vector3 p = pv.CellCenter(x, y, z);
                        Vector3 v = p - center;
                        if (v.sqrMagnitude > brush2) continue;

                        if (stroke.strength > pv.charge[idx])
                            pv.charge[idx] = stroke.strength;
                    }
                }
            }
        }
    }

    static int LocalToVoxelIndex(float local, int res)
    {
        float f = (local * 0.5f) + 0.5f;
        return Mathf.Clamp((int)(f * res), 0, res - 1);
    }

    static void RasterizeLine(Vector3 aLocal, Vector3 bLocal, int res, Action<int, int, int> visit)
    {
        LocalToVoxel(aLocal, res, out int x0, out int y0, out int z0);
        LocalToVoxel(bLocal, res, out int x1, out int y1, out int z1);

        int dx = x1 - x0;
        int dy = y1 - y0;
        int dz = z1 - z0;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy), Mathf.Abs(dz));
        if (steps <= 0)
        {
            visit(x0, y0, z0);
            return;
        }

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int x = Mathf.Clamp(Mathf.RoundToInt(x0 + dx * t), 0, res - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(y0 + dy * t), 0, res - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(z0 + dz * t), 0, res - 1);
            visit(x, y, z);
        }
    }

    static void LocalToVoxel(Vector3 local, int res, out int x, out int y, out int z)
    {
        float fx = (local.x * 0.5f) + 0.5f;
        float fy = (local.y * 0.5f) + 0.5f;
        float fz = (local.z * 0.5f) + 0.5f;

        x = Mathf.Clamp((int)(fx * res), 0, res - 1);
        y = Mathf.Clamp((int)(fy * res), 0, res - 1);
        z = Mathf.Clamp((int)(fz * res), 0, res - 1);
    }
}
