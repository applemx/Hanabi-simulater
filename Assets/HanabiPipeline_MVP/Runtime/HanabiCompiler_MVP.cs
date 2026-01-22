using System;
using System.Collections.Generic;
using UnityEngine;

// Compiler-side (editor time) entry point.
// Output is BurstEvent[] + ParticleInitV2[] which get serialized into CompiledShowAsset.blob.
public static class HanabiCompiler_MVP
{
    public static void Compile(FireworkBlueprint bp, HanabiDatabase db, out uint seed, out BurstEvent[] bursts, out ParticleInitV2[] inits)
    {
        CompileIgnitionMultiBurst(bp, db, out seed, out bursts, out inits);
    }

    /// <summary>
    /// Default compile path: bake -> ignition solve -> burst binning -> particle init.
    /// </summary>
    public static void Compile(FireworkBlueprint bp, out uint seed, out BurstEvent[] bursts, out ParticleInitV2[] inits)
    {
        CompileIgnitionMultiBurst(bp, null, out seed, out bursts, out inits);
    }

    public static void CompileIgnitionMultiBurst(FireworkBlueprint bp, HanabiDatabase db, out uint seed, out BurstEvent[] bursts, out ParticleInitV2[] inits)
    {
        seed = (uint)bp.seed;
        if (db != null) db.BuildCaches();

        // 1) Bake blueprint -> voxel volume
        PackedVolume pv = FireworkBaker.Bake(bp);

        // 2) Ignition solve (time per voxel)
        float[] ignite = IgnitionSolver.Solve(bp, pv);

        float binSize = Mathf.Max(0.01f, bp.ignition.burstBinSize);
        float maxIgnition = Mathf.Max(0.01f, bp.ignition.maxIgnitionTime);

        // Resolve palette (DB tag wins; fallback to blueprint.palette)
        List<Color32> paletteUsed = null;
        if (db != null && db.TryGetPalette(bp.paletteTag, out var palFromDb))
            paletteUsed = palFromDb;
        else
            paletteUsed = bp.palette;
// 3) Bin star voxels by ignition time
        var bins = new Dictionary<int, List<int>>(256);

        for (int i = 0; i < pv.starMask.Length; i++)
        {
            if (pv.starMask[i] == 0) continue;

            float t = ignite[i];
            if (float.IsNaN(t) || float.IsInfinity(t)) continue;
            if (t < 0f) t = 0f;
            if (t > maxIgnition) continue;

            int bin = Mathf.FloorToInt(t / binSize);
            if (!bins.TryGetValue(bin, out var list))
            {
                list = new List<int>(256);
                bins.Add(bin, list);
            }
            list.Add(i);
        }

        if (bins.Count == 0)
        {
            // Fallback: single-shot all stars
            var solvedAll = BurstSolver.Solve(bp, pv);
            bursts = new[]
            {
                new BurstEvent
                {
                    timeLocal = 0f,
                    posLocal = Vector3.zero,
                    eventFxId = -1,
                    particleCount = solvedAll.Count,
                    particleStartIndex = 0
                }
            };
            inits = ConvertInits(bp, db, solvedAll, seed, extraSpawnDelay: null);
            return;
        }

        // 4) Generate BurstEvent list in time order
        var keys = new List<int>(bins.Keys);
        keys.Sort();

        var burstList = new List<BurstEvent>(keys.Count);
        var initList = new List<ParticleInitV2>(capacity: 4096);

        foreach (int bin in keys)
        {
            List<int> starIdx = bins[bin];
            if (starIdx == null || starIdx.Count == 0) continue;

            float t0 = bin * binSize;

            // Solve only these star cells
            var solved = BurstSolver.SolveSubset(bp, pv, starIdx, paletteUsed);

            int start = initList.Count;

            // Per-particle: add ignition offset inside the bin as spawnDelay.
            for (int k = 0; k < solved.Count && k < starIdx.Count; k++)
            {
                int cell = starIdx[k];
                float dtWithin = Mathf.Max(0f, ignite[cell] - t0);

                var s = solved[k];
                initList.Add(new ParticleInitV2
                {
                    pos0Local = s.pos0,
                    vel0Local = s.vel0,
                    life = s.life,
                    size = ResolveStarSize(db, bp, seed, (uint)(start + k)),
                    color = s.color,
                    spawnDelay = s.delay + dtWithin,
                    profileId = (ushort)ResolveStarProfileId(db, bp),
                    seed = seed ^ (uint)(start + k) * 2654435761u
                });
            }

            int count = initList.Count - start;
            if (count <= 0) continue;

            burstList.Add(new BurstEvent
            {
                timeLocal = t0,
                posLocal = Vector3.zero,
                eventFxId = -1,
                particleCount = count,
                particleStartIndex = start
            });
        }

        bursts = burstList.ToArray();
        inits = initList.ToArray();
    }

    static int ResolveStarProfileId(HanabiDatabase db, FireworkBlueprint bp)
    {
        if (db != null && db.TryGetStarId(bp.starProfileTag, out int id)) return id;
                // NOTE: particleTypeId was a legacy field. We now use tag-based StarProfile selection.
                // If the tag is missing / not found, fall back to the first StarProfile (index 0).
                if (db.starProfiles == null || db.starProfiles.Count == 0) return 0;
                Debug.LogWarning($"[HanabiCompiler] StarProfileTag '{bp.starProfileTag}' not found in DB. Fallback to starProfiles[0].");
                return 0;
    }

    static float ResolveStarSize(HanabiDatabase db, FireworkBlueprint bp, uint seed, uint index)
    {
        // If DB exists, use baseSize +/- jitter. Otherwise use old shell defaults.
        if (db != null)
        {
            int id = ResolveStarProfileId(db, bp);
            var def = db.GetStarById(id);
            if (def != null)
            {
                float j = Hash01(seed ^ (index * 2654435761u));
                float signed = (j * 2f - 1f);
                float sz = def.baseSize + signed * def.sizeJitter;
                return Mathf.Max(0.01f, sz);
            }
        }
        return DefaultStarSize(bp.shellSize);
    }

    static float Hash01(uint x)
    {
        // xorshift -> 0..1
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        return (x & 0x00FFFFFF) / 16777215.0f;
    }

    static float DefaultStarSize(ShellSize size)
    {
        // Visual-only default. Real StarProfile will control this later.
        if (size == ShellSize.Large) return 0.75f;
        if (size == ShellSize.Medium) return 0.5f;
        return 0.35f;
    }

    static ParticleInitV2[] ConvertInits(FireworkBlueprint bp, HanabiDatabase db, List<ParticleInit> solved, uint seed, float[] extraSpawnDelay)
    {
        var arr = new ParticleInitV2[solved.Count];
        for (int i = 0; i < solved.Count; i++)
        {
            float extra = (extraSpawnDelay != null && i < extraSpawnDelay.Length) ? extraSpawnDelay[i] : 0f;
            var s = solved[i];
            arr[i] = new ParticleInitV2
            {
                pos0Local = s.pos0,
                vel0Local = s.vel0,
                life = s.life,
                size = ResolveStarSize(db, bp, seed, (uint)i),
                color = s.color,
                spawnDelay = s.delay + extra,
                profileId = (ushort)ResolveStarProfileId(db, bp),
                seed = seed ^ (uint)i * 2654435761u
            };
        }
        return arr;
    }
}