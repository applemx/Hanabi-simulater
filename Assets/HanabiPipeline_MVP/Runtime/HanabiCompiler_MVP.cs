using System;
using System.Collections.Generic;
using UnityEngine;

// Compiler-side (editor time) entry point.
// Output is BurstEvent[] + ParticleInitV2[] which get serialized into CompiledShowAsset.blob.
public static class HanabiCompiler_MVP
{
    const bool LogWashiStats = true;

    public static void Compile(FireworkBlueprint bp, HanabiDatabase db, out uint seed, out BurstEvent[] bursts, out ParticleInitV2[] inits, out LaunchParams launchParams)
    {
        CompileIgnitionMultiBurst(bp, db, out seed, out bursts, out inits, out launchParams);
    }

    /// <summary>
    /// Default compile path: bake -> ignition solve -> ignition-region clustering -> particle init.
    /// </summary>
    public static void Compile(FireworkBlueprint bp, out uint seed, out BurstEvent[] bursts, out ParticleInitV2[] inits, out LaunchParams launchParams)
    {
        CompileIgnitionMultiBurst(bp, null, out seed, out bursts, out inits, out launchParams);
    }

    public static void CompileIgnitionMultiBurst(FireworkBlueprint bp, HanabiDatabase db, out uint seed, out BurstEvent[] bursts, out ParticleInitV2[] inits, out LaunchParams launchParams)
    {
        seed = (uint)bp.seed;
        if (db != null) db.BuildCaches();

        StarProfileDef starProfile = null;
        WashiDef washiDef = null;
        if (db != null)
        {
            int starId = ResolveStarProfileId(db, bp);
            starProfile = db.GetStarById(starId);
            if (db.TryGetWashiId(bp.washiTag, out int waId))
                washiDef = db.GetWashiById(waId);
        }
        if (LogWashiStats && washiDef == null && !string.IsNullOrWhiteSpace(bp.washiTag))
            Debug.LogWarning($"[Hanabi] Washi tag '{bp.washiTag}' not found in DB. Washi stats skipped.");

        // 1) Bake blueprint -> voxel volume
        PackedVolume pv = FireworkBaker.Bake(bp);

        FuseDef fuseDef = null;
        WaruyakuDef waruyakuDef = null;
        LaunchProfileDef launchDef = null;
        if (db != null)
        {
            if (db.TryGetFuseId(bp.igniters != null && bp.igniters.Count > 0 ? bp.igniters[0].fuseTag : null, out int fId))
                fuseDef = db.GetFuseById(fId);
            if (db.TryGetWaruyakuId(bp.waruyakuTag, out int wId))
                waruyakuDef = db.GetWaruyakuById(wId);
            if (db.TryGetLaunchId(bp.launchTag, out int lId))
                launchDef = db.GetLaunchById(lId);
        }

        float fuseSeconds = (fuseDef != null && fuseDef.burnSeconds > 0f) ? fuseDef.burnSeconds : 3.5f;
        float startDelay = 0f;
        if (bp.igniters != null && bp.igniters.Count > 0)
        {
            startDelay = bp.igniters[0].startDelay;
            for (int i = 1; i < bp.igniters.Count; i++)
                startDelay = Mathf.Min(startDelay, bp.igniters[i].startDelay);
            startDelay = Mathf.Max(0f, startDelay);
        }

        launchParams = new LaunchParams
        {
            launchSpeed = (launchDef != null) ? Mathf.Max(1f, launchDef.launchSpeed) : 70f,
            fuseSeconds = fuseSeconds + startDelay,
            gravityScale = (launchDef != null) ? Mathf.Max(0.01f, launchDef.gravityScale) : 1f,
            windScale = (launchDef != null) ? Mathf.Max(0f, launchDef.windScale) : 0.2f,
            dragScale = (launchDef != null) ? Mathf.Max(0f, launchDef.dragScale) : 0.2f
        };

        // 2) Ignition solve (time per voxel)
        float[] ignite = IgnitionSolver.Solve(bp, pv, fuseDef, waruyakuDef);

        float clusterWindow = Mathf.Max(0.01f, bp.ignition.burstBinSize);
        float maxIgnition = Mathf.Max(0.01f, bp.ignition.maxIgnitionTime);

        int washiHitCount = 0;
        int totalParticleCount = 0;
        float washiDelaySum = 0f;
        float washiDelayMax = 0f;
        float washiStrengthSum = 0f;

        // Resolve palette (DB tag wins; fallback to blueprint.palette)
        List<Color32> paletteUsed = null;
        if (db != null && db.TryGetPalette(bp.paletteTag, out var palFromDb))
            paletteUsed = palFromDb;
        else
            paletteUsed = bp.palette;
        // 3) Cluster star voxels into ignition regions
        var regions = BuildIgnitionRegions(pv, ignite, maxIgnition, clusterWindow);
        if (regions.Count == 0)
        {
            // Fallback: single-shot all stars
            var solvedAll = BurstSolver.Solve(bp, pv, starProfile, washiDef, waruyakuDef);
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

            if (LogWashiStats)
            {
                AccumulateWashiStatsFromVolume(pv, washiDef, ref washiHitCount, ref totalParticleCount, ref washiDelaySum, ref washiDelayMax, ref washiStrengthSum);
                LogWashiSummary(bp, washiDef, washiHitCount, totalParticleCount, washiDelaySum, washiDelayMax, washiStrengthSum);
            }
            return;
        }

        // 4) Generate BurstEvent list in time order
        regions.Sort((a, b) => a.minTime.CompareTo(b.minTime));

        var burstList = new List<BurstEvent>(regions.Count);
        var initList = new List<ParticleInitV2>(capacity: 4096);

        foreach (var region in regions)
        {
            List<int> starIdx = region.starIndices;
            if (starIdx == null || starIdx.Count == 0) continue;

            float t0 = region.minTime;

            // Solve only these star cells
            var solved = BurstSolver.SolveSubset(bp, pv, starIdx, paletteUsed, starProfile, washiDef, waruyakuDef);

            int start = initList.Count;

            // Per-particle: add ignition offset inside the bin as spawnDelay.
            for (int k = 0; k < solved.Count && k < starIdx.Count; k++)
            {
                int cell = starIdx[k];
                float dtWithin = Mathf.Max(0f, ignite[cell] - t0);

                var s = solved[k];
                if (LogWashiStats)
                {
                    totalParticleCount++;
                    if (pv.paperCellWallId != null && pv.paperCellWallId[cell] != 0)
                    {
                        washiHitCount++;
                        washiDelaySum += s.delay;
                        washiDelayMax = Mathf.Max(washiDelayMax, s.delay);
                        float strength = 1f;
                        if (pv.paperStrength != null)
                            strength = Mathf.Clamp01(pv.paperStrength[cell] / 255f);
                        washiStrengthSum += strength;
                    }
                }

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
                posLocal = region.posLocal,
                eventFxId = -1,
                particleCount = count,
                particleStartIndex = start
            });
        }

        bursts = burstList.ToArray();
        inits = initList.ToArray();

        if (LogWashiStats)
            LogWashiSummary(bp, washiDef, washiHitCount, totalParticleCount, washiDelaySum, washiDelayMax, washiStrengthSum);
    }

    static int ResolveStarProfileId(HanabiDatabase db, FireworkBlueprint bp)
    {
        if (db != null && db.TryGetStarId(bp.starProfileTag, out int id)) return id;
        if (db == null || db.starProfiles == null || db.starProfiles.Count == 0) return 0;
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

    struct IgnitionRegion
    {
        public float minTime;
        public Vector3 posLocal;
        public List<int> starIndices;
    }

    static List<IgnitionRegion> BuildIgnitionRegions(PackedVolume pv, float[] ignite, float maxIgnition, float clusterWindow)
    {
        var regions = new List<IgnitionRegion>(64);
        if (pv == null || ignite == null || pv.starMask == null) return regions;

        int n = pv.starMask.Length;
        var visited = new bool[n];

        int[] dx = { 1, -1, 0, 0, 0, 0 };
        int[] dy = { 0, 0, 1, -1, 0, 0 };
        int[] dz = { 0, 0, 0, 0, 1, -1 };

        for (int i = 0; i < n; i++)
        {
            if (visited[i] || pv.starMask[i] == 0) continue;

            float t0 = ignite[i];
            if (!IsIgnitionValid(t0, maxIgnition)) continue;

            float minT = t0;
            float maxT = t0;

            var indices = new List<int>(128);
            var queue = new Queue<int>();
            queue.Enqueue(i);
            visited[i] = true;

            Vector3 sumPos = Vector3.zero;
            int count = 0;

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                indices.Add(idx);

                pv.XYZ(idx, out int x, out int y, out int z);
                sumPos += pv.CellCenter(x, y, z);
                count++;

                for (int k = 0; k < 6; k++)
                {
                    int nx = x + dx[k];
                    int ny = y + dy[k];
                    int nz = z + dz[k];
                    if (!pv.InBounds(nx, ny, nz)) continue;

                    int nIdx = pv.Index(nx, ny, nz);
                    if (visited[nIdx] || pv.starMask[nIdx] == 0) continue;

                    float tn = ignite[nIdx];
                    if (!IsIgnitionValid(tn, maxIgnition)) continue;

                    float newMin = Mathf.Min(minT, tn);
                    float newMax = Mathf.Max(maxT, tn);
                    if (newMax - newMin > clusterWindow) continue;

                    minT = newMin;
                    maxT = newMax;
                    visited[nIdx] = true;
                    queue.Enqueue(nIdx);
                }
            }

            if (indices.Count == 0) continue;

            regions.Add(new IgnitionRegion
            {
                minTime = minT,
                posLocal = sumPos / Mathf.Max(1, count),
                starIndices = indices
            });
        }

        return regions;
    }

    static bool IsIgnitionValid(float t, float maxIgnition)
    {
        return !(float.IsNaN(t) || float.IsInfinity(t)) && t >= 0f && t <= maxIgnition;
    }

    static void AccumulateWashiStatsFromVolume(PackedVolume pv, WashiDef washiDef, ref int hitCount, ref int totalCount, ref float delaySum, ref float delayMax, ref float strengthSum)
    {
        if (pv == null || pv.starMask == null) return;
        if (washiDef == null) return;

        float baseDelay = Mathf.Max(0f, washiDef.delaySeconds);
        for (int i = 0; i < pv.starMask.Length; i++)
        {
            if (pv.starMask[i] == 0) continue;
            totalCount++;
            if (pv.paperCellWallId != null && pv.paperCellWallId[i] != 0)
            {
                float strength = 1f;
                if (pv.paperStrength != null)
                    strength = Mathf.Clamp01(pv.paperStrength[i] / 255f);
                float d = baseDelay * strength;
                hitCount++;
                strengthSum += strength;
                delaySum += d;
                delayMax = Mathf.Max(delayMax, d);
            }
        }
    }

    static void LogWashiSummary(FireworkBlueprint bp, WashiDef washiDef, int hitCount, int totalCount, float delaySum, float delayMax, float strengthSum)
    {
        if (!LogWashiStats || washiDef == null) return;
        float avgDelay = hitCount > 0 ? delaySum / hitCount : 0f;
        float avgStrength = hitCount > 0 ? strengthSum / hitCount : 0f;
        float avgCollimation = avgStrength * Mathf.Clamp01(washiDef.collimation);
        float ratio = totalCount > 0 ? (float)hitCount / totalCount : 0f;

        Debug.Log($"[Hanabi] Washi stats tag={washiDef.tag} hits={hitCount}/{totalCount} ({ratio:P0}) avgDelay={avgDelay:F3}s maxDelay={delayMax:F3}s avgStrength={avgStrength:F2} avgCollim={avgCollimation:F2}");
    }
}
