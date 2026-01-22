using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Compute ignition time per voxel (PackedVolume cell).
/// MVP: Dijkstra over 6-neighborhood with cost based on charge and paper wall.
/// Output is shell-local compile-time data. Runtime never runs this.
/// </summary>
public static class IgnitionSolver
{
    public static float[] Solve(FireworkBlueprint bp, PackedVolume pv)
    {
        int n = pv.res * pv.res * pv.res;
        var dist = new float[n];
        for (int i = 0; i < n; i++) dist[i] = float.PositiveInfinity;

        // Multi-source: igniters
        var heap = new MinHeap(n);

        EnsureIgniters(bp);

        foreach (var ig in bp.igniters)
        {
            int idx = LocalToIndexClamped(pv, ig.posLocal);
            float t0 = Mathf.Max(0f, ig.startDelay);
            if (t0 < dist[idx])
            {
                dist[idx] = t0;
                heap.Push(idx, t0);
            }
        }

        int res = pv.res;
        int res2 = res * res;

        // Neighbor deltas (6-neighborhood)
        int[] dx = { 1, -1, 0, 0, 0, 0 };
        int[] dy = { 0, 0, 1, -1, 0, 0 };
        int[] dz = { 0, 0, 0, 0, 1, -1 };

        float secondsPerVoxel = Mathf.Max(1e-4f, bp.ignition.secondsPerVoxel);
        float maxT = Mathf.Max(0.01f, bp.ignition.maxIgnitionTime);

        while (heap.Count > 0)
        {
            heap.Pop(out int u, out float du);
            if (du != dist[u]) continue; // stale
            if (du > maxT) continue;

            int ux = u % res;
            int uy = (u / res) % res;
            int uz = u / res2;

            float uCharge = pv.charge[u] / 255f;
            float uSpeedFactor = 0.2f + 0.8f * Mathf.Clamp01(uCharge); // 0.2..1.0
            float baseStep = secondsPerVoxel / uSpeedFactor;

            for (int k = 0; k < 6; k++)
            {
                int vx = ux + dx[k];
                int vy = uy + dy[k];
                int vz = uz + dz[k];
                if (vx < 0 || vx >= res || vy < 0 || vy >= res || vz < 0 || vz >= res) continue;

                int v = vx + vy * res + vz * res2;

                float extra = 0f;
                if (pv.paperCellWallId != null && pv.paperCellWallId[v] != 0)
                    extra += Mathf.Max(0f, bp.ignition.paperExtraDelayPerCell);

                float nd = du + baseStep + extra;
                if (nd < dist[v])
                {
                    dist[v] = nd;
                    heap.Push(v, nd);
                }
            }
        }

        return dist;
    }

    static void EnsureIgniters(FireworkBlueprint bp)
    {
        if (bp.igniters == null) bp.igniters = new List<IgniterSpec>();
        if (bp.igniters.Count == 0) bp.igniters.Add(IgniterSpec.Default);
    }

    static int LocalToIndexClamped(PackedVolume pv, Vector3 pLocal)
    {
        int x = Mathf.Clamp((int)((pLocal.x + 1f) * 0.5f * pv.res), 0, pv.res - 1);
        int y = Mathf.Clamp((int)((pLocal.y + 1f) * 0.5f * pv.res), 0, pv.res - 1);
        int z = Mathf.Clamp((int)((pLocal.z + 1f) * 0.5f * pv.res), 0, pv.res - 1);
        return pv.Index(x, y, z);
    }

    // Tiny binary heap (min by key)
    sealed class MinHeap
    {
        int[] nodes;
        float[] keys;
        int count;

        public int Count => count;

        public MinHeap(int capacity)
        {
            nodes = new int[Mathf.Max(16, capacity)];
            keys = new float[nodes.Length];
            count = 0;
        }

        public void Push(int node, float key)
        {
            if (count >= nodes.Length)
            {
                int newLen = nodes.Length * 2;
                Array.Resize(ref nodes, newLen);
                Array.Resize(ref keys, newLen);
            }

            int i = count++;
            nodes[i] = node;
            keys[i] = key;
            SiftUp(i);
        }

        public void Pop(out int node, out float key)
        {
            node = nodes[0];
            key = keys[0];

            int last = --count;
            if (last <= 0)
            {
                count = 0;
                return;
            }

            nodes[0] = nodes[last];
            keys[0] = keys[last];
            SiftDown(0);
        }

        void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (keys[p] <= keys[i]) break;
                Swap(i, p);
                i = p;
            }
        }

        void SiftDown(int i)
        {
            while (true)
            {
                int l = (i << 1) + 1;
                if (l >= count) break;
                int r = l + 1;
                int m = (r < count && keys[r] < keys[l]) ? r : l;
                if (keys[i] <= keys[m]) break;
                Swap(i, m);
                i = m;
            }
        }

        void Swap(int a, int b)
        {
            (nodes[a], nodes[b]) = (nodes[b], nodes[a]);
            (keys[a], keys[b]) = (keys[b], keys[a]);
        }
    }
}
