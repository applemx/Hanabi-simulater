using System;
using UnityEngine;

public static class FireworkBaker
{
    public static PackedVolume Bake(FireworkBlueprint bp)
    {
        int res = bp.shellSize == ShellSize.Small ? 64 :
                  bp.shellSize == ShellSize.Medium ? 96 : 128;

        var pv = new PackedVolume(res);

        // 割薬（MVP）：とりあえず一律の密度
        for (int i = 0; i < pv.charge.Length; i++)
            pv.charge[i] = 80;

        // 星：リングを点群で生成→ボクセルに落とす
        var rng = new System.Random(bp.seed);
        for (int i = 0; i < bp.ring.count; i++)
        {
            float ang = (float)(rng.NextDouble() * Math.PI * 2.0);
            float r = bp.ring.radius + (float)(rng.NextDouble() * 2 - 1) * bp.ring.thickness;
            float z = (float)(rng.NextDouble() * 2 - 1) * bp.ring.thickness;

            Vector3 p = new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, z);
            if (p.magnitude > 1f) continue;

            int x = Mathf.Clamp((int)((p.x + 1f) * 0.5f * pv.res), 0, pv.res - 1);
            int y = Mathf.Clamp((int)((p.y + 1f) * 0.5f * pv.res), 0, pv.res - 1);
            int zI = Mathf.Clamp((int)((p.z + 1f) * 0.5f * pv.res), 0, pv.res - 1);

            int idx = pv.Index(x, y, zI);
            pv.starMask[idx] = 1;
            pv.starColor[idx] = 0; // palette[0]
        }

        // 和紙：discを「セル内wallId」に雑にベイク（最短で効果を見る用）
        foreach (var pr in bp.paper)
        {
            if (pr.shape != PaperShape.Disc) continue;

            Vector3 n = pr.normal.sqrMagnitude < 1e-6f ? Vector3.up : pr.normal.normalized;

            int radCells = Mathf.CeilToInt(pr.radius / pv.cellSize) + 1;
            Vector3 c = pr.center;

            int cx = Mathf.Clamp((int)((c.x + 1f) * 0.5f * pv.res), 0, pv.res - 1);
            int cy = Mathf.Clamp((int)((c.y + 1f) * 0.5f * pv.res), 0, pv.res - 1);
            int cz = Mathf.Clamp((int)((c.z + 1f) * 0.5f * pv.res), 0, pv.res - 1);

            for (int z = cz - radCells; z <= cz + radCells; z++)
                for (int y = cy - radCells; y <= cy + radCells; y++)
                    for (int x = cx - radCells; x <= cx + radCells; x++)
                    {
                        if (!pv.InBounds(x, y, z)) continue;

                        Vector3 p = pv.CellCenter(x, y, z);

                        float d = Vector3.Dot(p - pr.center, n);
                        if (Mathf.Abs(d) > pv.cellSize * 1.2f) continue;

                        Vector3 proj = p - n * d;
                        if ((proj - pr.center).sqrMagnitude > pr.radius * pr.radius) continue;

                        pv.paperCellWallId[pv.Index(x, y, z)] = pr.wallId;
                    }
        }

        return pv;
    }
}
