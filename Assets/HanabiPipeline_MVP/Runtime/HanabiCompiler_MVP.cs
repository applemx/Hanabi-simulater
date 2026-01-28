using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Compiler-side (editor time) entry point.
// Output is BurstEvent[] + ParticleInitV2[] which get serialized into CompiledShowAsset.blob.
public static class HanabiCompiler_MVP
{
    const bool LogWashiStats = true;
    const bool DumpCompileDebug = true;

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
        PackedVolume pv = FireworkBaker.Bake(bp, db);

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
            launchSpeed = (launchDef != null) ? Mathf.Max(1f, launchDef.launchSpeed) : 240f,
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
            var indices = new List<int>(capacity: 4096);
            for (int i = 0; i < pv.starMask.Length; i++)
                if (pv.starMask[i] != 0) indices.Add(i);

            var solvedAll = BurstSolver.SolveSubset(bp, pv, indices, paletteUsed, starProfile, washiDef, waruyakuDef, db);
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

            inits = new ParticleInitV2[solvedAll.Count];
            for (int i = 0; i < solvedAll.Count; i++)
            {
                int cell = (i < indices.Count) ? indices[i] : -1;
                ushort profileId = ResolveStarProfileIdForCell(db, bp, pv, cell);
                var s = solvedAll[i];
                inits[i] = new ParticleInitV2
                {
                    pos0Local = s.pos0,
                    vel0Local = s.vel0,
                    life = s.life,
                    size = ResolveStarSize(db, bp, seed, (uint)i, profileId),
                    color = s.color,
                    spawnDelay = s.delay,
                    profileId = profileId,
                    seed = seed ^ (uint)i * 2654435761u
                };
            }

            if (LogWashiStats)
            {
                AccumulateWashiStatsFromVolume(pv, washiDef, ref washiHitCount, ref totalParticleCount, ref washiDelaySum, ref washiDelayMax, ref washiStrengthSum);
                LogWashiSummary(bp, washiDef, washiHitCount, totalParticleCount, washiDelaySum, washiDelayMax, washiStrengthSum);
            }
            if (DumpCompileDebug)
                DumpCompileDebugFiles(bp, db, seed, bursts, inits, launchParams);
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
            var solved = BurstSolver.SolveSubset(bp, pv, starIdx, paletteUsed, starProfile, washiDef, waruyakuDef, db);

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

                ushort profileId = ResolveStarProfileIdForCell(db, bp, pv, cell);
                initList.Add(new ParticleInitV2
                {
                    pos0Local = s.pos0,
                    vel0Local = s.vel0,
                    life = s.life,
                    size = ResolveStarSize(db, bp, seed, (uint)(start + k), profileId),
                    color = s.color,
                    spawnDelay = s.delay + dtWithin,
                    profileId = profileId,
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
        if (DumpCompileDebug)
            DumpCompileDebugFiles(bp, db, seed, bursts, inits, launchParams);
    }

    static int ResolveStarProfileId(HanabiDatabase db, FireworkBlueprint bp)
    {
        if (db != null && db.TryGetStarId(bp.starProfileTag, out int id)) return id;
        if (db == null || db.starProfiles == null || db.starProfiles.Count == 0) return 0;
        return 0;
    }

    static float ResolveStarSize(HanabiDatabase db, FireworkBlueprint bp, uint seed, uint index, ushort profileId)
    {
        // If DB exists, use baseSize +/- jitter. Otherwise use old shell defaults.
        if (db != null)
        {
            var def = db.GetStarById(profileId);
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

    static ushort ResolveStarProfileIdForCell(HanabiDatabase db, FireworkBlueprint bp, PackedVolume pv, int cell)
    {
        if (pv != null && pv.starProfileId != null && cell >= 0 && cell < pv.starProfileId.Length)
            return pv.starProfileId[cell];
        return (ushort)ResolveStarProfileId(db, bp);
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

#if UNITY_EDITOR
    [Serializable]
    class CompileDump
    {
        public string blueprintName;
        public int blueprintVersion;
        public int seed;
        public string starTag;
        public string waruyakuTag;
        public string washiTag;
        public string launchTag;
        public int starCount;
        public int burstCount;
        public float launchSpeed;
        public float fuseSeconds;
        public float gravityScale;
        public float windScale;
        public float dragScale;
        public KindStat[] kindStats;
        public BurstStat[] bursts;
    }

    [Serializable]
    class KindStat
    {
        public string kind;
        public int count;
        public float avgSpeed;
        public float maxSpeed;
        public float minSpeed;
        public float avgLife;
        public float avgSize;
        public float avgDelay;
    }

    [Serializable]
    class BurstStat
    {
        public int index;
        public float timeLocal;
        public int particleCount;
        public float avgSpeed;
        public float maxSpeed;
        public float minSpeed;
        public float avgLife;
        public float avgSize;
        public float avgDelay;
        public float dirBias;
        public Vector3 meanDir;
    }
#endif

    static void DumpCompileDebugFiles(FireworkBlueprint bp, HanabiDatabase db, uint seed, BurstEvent[] bursts, ParticleInitV2[] inits, LaunchParams launchParams)
    {
#if UNITY_EDITOR
        try
        {
            string root = Path.Combine(Application.dataPath, "Debug", "CompiledDumps");
            string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
            string folder = Path.Combine(root, dateFolder);
            Directory.CreateDirectory(folder);

            string name = string.IsNullOrWhiteSpace(bp.displayName) ? bp.name : bp.displayName;
            if (string.IsNullOrWhiteSpace(name)) name = "Blueprint";
            string stamp = DateTime.Now.ToString("HHmmss");
            string baseName = $"{SanitizeFileName(name)}_seed{seed}_{stamp}";

            var dump = BuildCompileDump(bp, db, bursts, inits, launchParams);
            string json = JsonUtility.ToJson(dump, true);
            File.WriteAllText(Path.Combine(folder, baseName + ".json"), json);

            string csv = BuildBurstCsv(dump);
            File.WriteAllText(Path.Combine(folder, baseName + ".csv"), csv);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Hanabi] Compile debug dump failed: {ex.Message}");
        }
#endif
    }

#if UNITY_EDITOR
    static CompileDump BuildCompileDump(FireworkBlueprint bp, HanabiDatabase db, BurstEvent[] bursts, ParticleInitV2[] inits, LaunchParams launchParams)
    {
        var dump = new CompileDump
        {
            blueprintName = string.IsNullOrWhiteSpace(bp.displayName) ? bp.name : bp.displayName,
            blueprintVersion = bp.version,
            seed = bp.seed,
            starTag = bp.starProfileTag,
            waruyakuTag = bp.waruyakuTag,
            washiTag = bp.washiTag,
            launchTag = bp.launchTag,
            starCount = inits != null ? inits.Length : 0,
            burstCount = bursts != null ? bursts.Length : 0,
            launchSpeed = launchParams.launchSpeed,
            fuseSeconds = launchParams.fuseSeconds,
            gravityScale = launchParams.gravityScale,
            windScale = launchParams.windScale,
            dragScale = launchParams.dragScale,
        };

        var kindStats = new Dictionary<StarKind, KindAccumulator>();
        if (inits != null)
        {
            for (int i = 0; i < inits.Length; i++)
            {
                var p = inits[i];
                float speed = p.vel0Local.magnitude;
                var kind = ResolveKind(db, p.profileId);
                if (!kindStats.TryGetValue(kind, out var acc))
                    acc = new KindAccumulator();
                acc.Add(speed, p.life, p.size, p.spawnDelay);
                kindStats[kind] = acc;
            }
        }

        var kindList = new List<KindStat>(kindStats.Count);
        foreach (var kv in kindStats)
        {
            var acc = kv.Value;
            kindList.Add(new KindStat
            {
                kind = kv.Key.ToString(),
                count = acc.count,
                avgSpeed = acc.AvgSpeed,
                maxSpeed = acc.maxSpeed,
                minSpeed = acc.minSpeed,
                avgLife = acc.AvgLife,
                avgSize = acc.AvgSize,
                avgDelay = acc.AvgDelay
            });
        }
        dump.kindStats = kindList.ToArray();

        var burstList = new List<BurstStat>();
        if (bursts != null && inits != null)
        {
            for (int b = 0; b < bursts.Length; b++)
            {
                var be = bursts[b];
                int start = Mathf.Clamp(be.particleStartIndex, 0, inits.Length);
                int count = Mathf.Clamp(be.particleCount, 0, inits.Length - start);
                if (count <= 0) continue;

                float sumSpeed = 0f, maxSpeed = 0f, minSpeed = float.MaxValue;
                float sumLife = 0f, sumSize = 0f, sumDelay = 0f;
                Vector3 meanDir = Vector3.zero;
                for (int i = 0; i < count; i++)
                {
                    var p = inits[start + i];
                    float speed = p.vel0Local.magnitude;
                    sumSpeed += speed;
                    maxSpeed = Mathf.Max(maxSpeed, speed);
                    minSpeed = Mathf.Min(minSpeed, speed);
                    sumLife += p.life;
                    sumSize += p.size;
                    sumDelay += p.spawnDelay;
                    if (speed > 1e-6f)
                        meanDir += p.vel0Local / speed;
                }

                meanDir /= count;
                float dirBias = Mathf.Clamp01(meanDir.magnitude);

                burstList.Add(new BurstStat
                {
                    index = b,
                    timeLocal = be.timeLocal,
                    particleCount = count,
                    avgSpeed = sumSpeed / count,
                    maxSpeed = maxSpeed,
                    minSpeed = minSpeed == float.MaxValue ? 0f : minSpeed,
                    avgLife = sumLife / count,
                    avgSize = sumSize / count,
                    avgDelay = sumDelay / count,
                    dirBias = dirBias,
                    meanDir = meanDir
                });
            }
        }
        dump.bursts = burstList.ToArray();

        return dump;
    }

    struct KindAccumulator
    {
        public int count;
        public float sumSpeed;
        public float sumLife;
        public float sumSize;
        public float sumDelay;
        public float maxSpeed;
        public float minSpeed;

        public void Add(float speed, float life, float size, float delay)
        {
            if (count == 0)
            {
                maxSpeed = speed;
                minSpeed = speed;
            }
            else
            {
                maxSpeed = Mathf.Max(maxSpeed, speed);
                minSpeed = Mathf.Min(minSpeed, speed);
            }
            count++;
            sumSpeed += speed;
            sumLife += life;
            sumSize += size;
            sumDelay += delay;
        }

        public float AvgSpeed => count > 0 ? sumSpeed / count : 0f;
        public float AvgLife => count > 0 ? sumLife / count : 0f;
        public float AvgSize => count > 0 ? sumSize / count : 0f;
        public float AvgDelay => count > 0 ? sumDelay / count : 0f;
    }

    static StarKind ResolveKind(HanabiDatabase db, ushort profileId)
    {
        if (db != null)
        {
            var def = db.GetStarById(profileId);
            if (def != null) return def.kind;
        }
        if (profileId < Enum.GetValues(typeof(StarKind)).Length)
            return (StarKind)profileId;
        return StarKind.Solid;
    }

    static string BuildBurstCsv(CompileDump dump)
    {
        var sb = new StringBuilder(2048);
        sb.AppendLine("index,timeLocal,particleCount,avgSpeed,maxSpeed,minSpeed,avgLife,avgSize,avgDelay,dirBias,meanDirX,meanDirY,meanDirZ");
        if (dump.bursts == null) return sb.ToString();
        foreach (var b in dump.bursts)
        {
            sb.Append(b.index).Append(',')
              .Append(b.timeLocal.ToString("0.###")).Append(',')
              .Append(b.particleCount).Append(',')
              .Append(b.avgSpeed.ToString("0.###")).Append(',')
              .Append(b.maxSpeed.ToString("0.###")).Append(',')
              .Append(b.minSpeed.ToString("0.###")).Append(',')
              .Append(b.avgLife.ToString("0.###")).Append(',')
              .Append(b.avgSize.ToString("0.###")).Append(',')
              .Append(b.avgDelay.ToString("0.###")).Append(',')
              .Append(b.dirBias.ToString("0.###")).Append(',')
              .Append(b.meanDir.x.ToString("0.###")).Append(',')
              .Append(b.meanDir.y.ToString("0.###")).Append(',')
              .Append(b.meanDir.z.ToString("0.###"))
              .AppendLine();
        }
        return sb.ToString();
    }

    static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
#endif
}
