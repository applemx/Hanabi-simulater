using System;
using UnityEngine;

/// <summary>
/// Computes igniteTime[] for each voxel cell by running Dijkstra once at compile-time.
///
/// - Multiple igniters supported (bp.igniters)
/// - Traversal is blocked for empty cells (charge==0)
/// - Paper adds extra delay (bp.ignition.paperExtraDelayPerCell * paperStrength)
///
/// NOTE: this is compile-time only; never call at runtime every launch.
/// </summary>
public static class IgnitionSolver
{
    struct HeapNode
    {
        public float d;
        public int idx;

        public HeapNode(float d, int idx)
        {
            this.d = d;
            this.idx = idx;
        }
    }

    sealed class MinHeap
    {
        HeapNode[] a;
        int n;

        public MinHeap(int capacity)
        {
            a = new HeapNode[Mathf.Max(64, capacity)];
            n = 0;
        }

        public int Count => n;

        public void Clear() => n = 0;

        public void Push(float d, int idx)
        {
            if (n == a.Length)
            {
                Array.Resize(ref a, a.Length * 2);
            }

            int i = n++;
            a[i] = new HeapNode(d, idx);

            // sift up
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (a[p].d <= a[i].d) break;
                (a[p], a[i]) = (a[i], a[p]);
                i = p;
            }
        }

        public HeapNode Pop()
        {
            var root = a[0];
            a[0] = a[--n];

            // sift down
            int i = 0;
            while (true)
            {
                int l = (i << 1) + 1;
                if (l >= n) break;
                int r = l + 1;
                int m = (r < n && a[r].d < a[l].d) ? r : l;
                if (a[i].d <= a[m].d) break;
                (a[i], a[m]) = (a[m], a[i]);
                i = m;
            }

            return root;
        }
    }

    public static float[] Solve(FireworkBlueprint bp, PackedVolume pv, FuseDef fuseDef = null, WaruyakuDef waruyakuDef = null)
    {
        int res = pv.res;
        int n = res * res * res;

        float[] dist = new float[n];
        for (int i = 0; i < n; i++) dist[i] = float.PositiveInfinity;

        var heap = new MinHeap(Mathf.Min(n, 4096));

        // Seed igniters
        int seeded = 0;
        if (bp.igniters != null)
        {
            for (int i = 0; i < bp.igniters.Count; i++)
            {
                int sIdx = V3ToIndex(bp.igniters[i].posLocal, res);
                if (pv.charge[sIdx] == 0) continue; // outside shell -> ignore
                if (dist[sIdx] > 0f)
                {
                    dist[sIdx] = 0f;
                    heap.Push(0f, sIdx);
                    seeded++;
                }
            }
        }

        if (seeded == 0)
        {
            // Fallback: center
            int c = V3ToIndex(Vector3.zero, res);
            if (pv.charge[c] == 0)
            {
                // pick any non-empty voxel as last resort
                for (int i = 0; i < n; i++)
                {
                    if (pv.charge[i] > 0)
                    {
                        c = i;
                        break;
                    }
                }
            }
            dist[c] = 0f;
            heap.Push(0f, c);
        }

        float baseStep = Mathf.Max(1e-5f, bp.ignition.secondsPerVoxel);
        float paperDelayPerCell = Mathf.Max(0f, bp.ignition.paperExtraDelayPerCell);
        float fuseCost = (fuseDef != null) ? Mathf.Max(1e-5f, fuseDef.igniteCost) : 0f;
        float fuseSpeed = (fuseDef != null) ? Mathf.Max(0.01f, fuseDef.burnSpeed) : 1.0f;
        float waruyakuMul = (waruyakuDef != null) ? Mathf.Max(0.1f, waruyakuDef.igniteCostMultiplier) : 1.0f;

        while (heap.Count > 0)
        {
            var node = heap.Pop();
            float d = node.d;
            int idx = node.idx;

            if (d != dist[idx]) continue; // stale

            int x, y, z;
            pv.XYZ(idx, out x, out y, out z);

            // 6-neighborhood
            Relax(idx, x - 1, y, z);
            Relax(idx, x + 1, y, z);
            Relax(idx, x, y - 1, z);
            Relax(idx, x, y + 1, z);
            Relax(idx, x, y, z - 1);
            Relax(idx, x, y, z + 1);
        }

        return dist;

        void Relax(int fromIdx, int nx, int ny, int nz)
        {
            if ((uint)nx >= (uint)res || (uint)ny >= (uint)res || (uint)nz >= (uint)res) return;
            int toIdx = pv.Index(nx, ny, nz);

            // blocked
            if (pv.charge[toIdx] == 0) return;

            float step = baseStep;

            // Fuel strength affects burn speed (weaker charge burns slower)
            float strength = pv.charge[toIdx] / 255f; // 0..1
            float fuelMult = 1f / Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(strength)); // 1..4
            step *= fuelMult;

            // Fuse cells propagate faster with their own cost curve.
            if (pv.fuseMask != null && pv.fuseMask[toIdx] != 0)
            {
                step = fuseCost / fuseSpeed;
            }
            else
            {
                step *= waruyakuMul;
            }

            // Paper adds extra delay (scaled by strength)
            if (paperDelayPerCell > 0f && pv.paperStrength != null)
            {
                byte ps = pv.paperStrength[toIdx];
                if (ps > 0)
                {
                    step += paperDelayPerCell * (ps / 255f);
                }
                else if (pv.paperCellWallId != null && pv.paperCellWallId[toIdx] != 0)
                {
                    // legacy: wall id without strength
                    step += paperDelayPerCell;
                }
            }
            else if (paperDelayPerCell > 0f && pv.paperCellWallId != null && pv.paperCellWallId[toIdx] != 0)
            {
                step += paperDelayPerCell;
            }

            float nd = dist[fromIdx] + step;
            if (nd < dist[toIdx])
            {
                dist[toIdx] = nd;
                heap.Push(nd, toIdx);
            }
        }
    }

    static int V3ToIndex(Vector3 local, int res)
    {
        float fx = (local.x * 0.5f) + 0.5f;
        float fy = (local.y * 0.5f) + 0.5f;
        float fz = (local.z * 0.5f) + 0.5f;

        int x = Mathf.Clamp((int)(fx * res), 0, res - 1);
        int y = Mathf.Clamp((int)(fy * res), 0, res - 1);
        int z = Mathf.Clamp((int)(fz * res), 0, res - 1);

        return x + res * (y + res * z);
    }
}
