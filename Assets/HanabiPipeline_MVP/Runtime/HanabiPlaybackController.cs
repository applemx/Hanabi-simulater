using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class HanabiPlaybackController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] CompiledShowAsset compiledShow;
    [SerializeField] ParticleSystem particleSystemRenderer;
    [SerializeField] ParticleSystem[] starParticleSystems;
    [SerializeField] bool useMultiParticleSystems = true;
    [SerializeField] bool autoFindStarRenderers = true;
    [SerializeField] string starRendererPrefix = "FireworkParticles_";
    [SerializeField] Transform launchOrigin;           // set your launcher
    [SerializeField] float fallbackForwardDistance = 150f;
    [SerializeField] HanabiDatabase profileDatabase;   // optional: drive StarProfile visuals

    [Header("Playback")]
    [SerializeField] KeyCode launchKey = KeyCode.F;
    [SerializeField] float explodeHeight = 1300f;
    [SerializeField] bool loop = false;
    [SerializeField] bool playOnStart = false;

    [Header("Debug")]
    [SerializeField] bool spawnDebugBurstWhenNoCompiled = true;
    [SerializeField] int debugParticleCount = 2000;
    [SerializeField] float debugStartSize = 0.5f;

    [Header("Debug (Visibility / Diagnostics)")]
    [SerializeField] bool debugOverlay = true;
    [SerializeField] bool debugGizmos = true;
    [SerializeField] bool debugVerboseLogs = true;
    [Tooltip("Start playback close to the first burst so you can see something immediately.")]
    [SerializeField] bool debugSkipToFirstBurst = true;
    [Tooltip("If enabled, subtract the first burst time from all burst times. Useful if compiled times are huge.")]
    [SerializeField] bool debugRebaseFirstBurstToZero = false;
    [Tooltip("Multiply size of compiled particles (debug only).")]
    [SerializeField] float debugCompiledSizeMultiplier = 1.0f;
    [Tooltip("Clamp minimum particle size after multiplier (debug only).")]
    [SerializeField] float debugCompiledMinSize = 0.03f;
    [Tooltip("Log sim state every N seconds while playing (0 = off).")]
    [SerializeField] float debugPeriodicLogSeconds = 1.0f;

    [Header("Sim")]
    [SerializeField] Vector3 wind = new Vector3(0.2f, 0f, 0.05f);
    [SerializeField] float dragK = 0.06f;

    ParticleSystem.Particle[] psBuffer;
    ParticleSystem.Particle[][] starBuffers;
    int[] starCounts;
    int starKindCount;
    ParticleSim sim;

    uint seed;
    BurstEvent[] bursts;
    ParticleInitV2[] inits;
    LaunchParams launchParams;

    float t;
    int nextBurstIndex;
    bool playing;

    // Debug state
    float debugNextLogAt;
    int debugBurstsFired;

    // Launch state
    Vector3 shellPos;
    Vector3 shellVel;
    Vector3 burstOrigin;
    bool burstOriginReady;

    void Awake()
    {
        starKindCount = Enum.GetValues(typeof(StarKind)).Length;
        EnsureStarRenderers();
        SanitizeDebugSettings();

        var primary = GetPrimaryRenderer();
        if (primary == null)
        {
            Debug.LogError("[HanabiPlaybackController] ParticleSystem is not assigned.");
            enabled = false;
            return;
        }

        ConfigureStarRenderers();

        var main = primary.main;
        psBuffer = new ParticleSystem.Particle[Mathf.Max(256, main.maxParticles)];
        sim = new ParticleSim(psBuffer.Length);
        sim.SetProfileLookup(profileDatabase != null ? profileDatabase.starProfiles : null);

        ClearStarRenderers();

        LoadCompiled();

        if (playOnStart)
            StartShow();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        SanitizeDebugSettings();
    }
#endif

    void SanitizeDebugSettings()
    {
        if (debugCompiledSizeMultiplier > 5f || debugCompiledMinSize > 0.1f)
        {
            debugCompiledSizeMultiplier = 1.0f;
            debugCompiledMinSize = 0.03f;
            debugStartSize = Mathf.Min(debugStartSize, 0.2f);
            if (debugVerboseLogs)
                Debug.LogWarning("[HanabiPlayback] Debug size settings were too large; clamped to safe defaults.");
        }
    }

    void LoadCompiled()
    {
        if (compiledShow == null || compiledShow.blob == null || compiledShow.blob.Length == 0)
        {
            seed = 0;
            bursts = Array.Empty<BurstEvent>();
            inits = Array.Empty<ParticleInitV2>();
            launchParams = default;
            return;
        }

        if (!CompiledShowSerializer.TryRead(compiledShow.blob, out seed, out bursts, out inits, out launchParams, out _))
        {
            Debug.LogError("[HanabiPlaybackController] Failed to read compiled blob. (wrong version?)");
            bursts = Array.Empty<BurstEvent>();
            inits = Array.Empty<ParticleInitV2>();
            launchParams = default;
        }

        // Debug summary / fixups
        if (debugRebaseFirstBurstToZero && bursts != null && bursts.Length > 0)
        {
            float t0 = bursts[0].timeLocal;
            if (t0 > 0.001f)
            {
                for (int i = 0; i < bursts.Length; i++)
                    bursts[i].timeLocal -= t0;
            }
        }

        if (debugVerboseLogs)
        {
            float firstT = (bursts != null && bursts.Length > 0) ? bursts[0].timeLocal : 0f;
            float lastT = (bursts != null && bursts.Length > 0) ? bursts[bursts.Length - 1].timeLocal : 0f;
            Debug.Log($"[HanabiPlayback] Loaded compiled: bursts={bursts?.Length ?? 0} inits={inits?.Length ?? 0} seed={seed} firstT={firstT:F3} lastT={lastT:F3}");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(launchKey))
        {
            Debug.Log("[Hanabi] Launch pressed");
            Debug.Log($"[Hanabi] refs: ps={(GetPrimaryRenderer() != null)} origin={(launchOrigin != null)} compiled={(compiledShow != null)} blob={(compiledShow != null && compiledShow.blob != null ? compiledShow.blob.Length : -1)}");

            // Re-load on press so you can tweak & re-save compiled assets without restarting play mode.
            LoadCompiled();

            if (bursts.Length > 0 && inits.Length > 0)
            {
                StartShow();
            }
            else if (spawnDebugBurstWhenNoCompiled)
            {
                DebugSpawn();
            }
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log("[Hanabi] StartShow pressed");
            StartShow();
        }

        if (!playing) return;

        float dt = Time.deltaTime;
        float prevT = t;
        t += dt;

        if (UseLaunchParams() && !burstOriginReady)
        {
            float fuseT = launchParams.fuseSeconds;
            if (prevT < fuseT)
            {
                float step = Mathf.Min(t, fuseT) - prevT;
                if (step > 0f)
                    StepShell(step);

                if (t >= fuseT)
                {
                    burstOrigin = shellPos;
                    burstOriginReady = true;
                }
            }
        }

        // Fire bursts
        float burstOffset = UseLaunchParams() ? launchParams.fuseSeconds : 0f;
        while (nextBurstIndex < bursts.Length && bursts[nextBurstIndex].timeLocal + burstOffset <= t)
        {
            SpawnBurst(bursts[nextBurstIndex]);
            nextBurstIndex++;
        }

        // Sim
        sim.Step(dt, wind, dragK);
        if (UseMultiRenderers())
        {
            EnsureStarBuffers();
            sim.FillParticlesByKind(starBuffers, starCounts);
        }
        else
        {
            sim.FillParticles(psBuffer);
        }

        // DEBUG: make compiled particles more visible (optional)
        if (debugCompiledSizeMultiplier != 1f || debugCompiledMinSize > 0f)
        {
            float mul = debugCompiledSizeMultiplier;
            float minS = debugCompiledMinSize;
            if (UseMultiRenderers())
            {
                for (int sIdx = 0; sIdx < starBuffers.Length; sIdx++)
                {
                    var buf = starBuffers[sIdx];
                    int count = (starCounts != null && sIdx < starCounts.Length) ? starCounts[sIdx] : 0;
                    if (buf == null) continue;
                    for (int i = 0; i < count; i++)
                    {
                        float s = buf[i].startSize * mul;
                        if (s < minS) s = minS;
                        buf[i].startSize = s;
                    }
                }
            }
            else
            {
                int n = sim.AliveCount;
                for (int i = 0; i < n; i++)
                {
                    float s = psBuffer[i].startSize * mul;
                    if (s < minS) s = minS;
                    psBuffer[i].startSize = s;
                }
            }
        }

        if (debugPeriodicLogSeconds > 0f && Time.time >= debugNextLogAt)
        {
            debugNextLogAt = Time.time + debugPeriodicLogSeconds;
            float nextT = (bursts != null && nextBurstIndex < bursts.Length) ? bursts[nextBurstIndex].timeLocal : -1f;
            Debug.Log($"[HanabiPlayback] t={t:F2} alive={sim.AliveCount} nextIdx={nextBurstIndex}/{bursts?.Length ?? 0} nextT={nextT:F2}");
        }

        if (UseMultiRenderers())
        {
            for (int i = 0; i < starParticleSystems.Length; i++)
            {
                var ps = starParticleSystems[i];
                if (ps == null) continue;
                var buf = starBuffers[i];
                int count = (starCounts != null && i < starCounts.Length) ? starCounts[i] : 0;
                if (buf == null) continue;
                ps.SetParticles(buf, count);
            }
        }
        else
        {
            particleSystemRenderer.SetParticles(psBuffer, sim.AliveCount);
        }

        if (nextBurstIndex >= bursts.Length && sim.AliveCount == 0)
        {
            if (loop) StartShow();
            else playing = false;
        }
    }

    void DebugSpawn()
    {
        var ps = GetPrimaryRenderer();
        if (ps == null)
        {
            Debug.LogError("[Hanabi] ParticleSystem is NULL. Assign FireworkParticles.");
            return;
        }
        if (launchOrigin == null)
        {
            Debug.LogError("[Hanabi] LaunchOrigin is NULL. Assign Launcher.");
            return;
        }

        ps.Clear(true);
        var p = new ParticleSystem.Particle[Mathf.Max(1, debugParticleCount)];
        for (int i = 0; i < p.Length; i++)
        {
            p[i].position = launchOrigin.position + launchOrigin.forward * 20f + Vector3.up * 5f;
            p[i].velocity = Random.onUnitSphere * 2f + Vector3.up * 2f;
            p[i].startLifetime = 5f;
            p[i].remainingLifetime = 5f;
            p[i].startSize = debugStartSize;
            p[i].startColor = Color.white;
        }
        ps.SetParticles(p, p.Length);
        ps.Play(true);
        Debug.Log("[Hanabi] Debug particles spawned: " + p.Length);
    }

    void StartShow()
    {
        LoadCompiled();
        if (bursts.Length == 0 || inits.Length == 0)
        {
            Debug.LogWarning("[HanabiPlaybackController] No compiled data. Did you save the blueprint?");
            return;
        }

        // Ensure PS + buffers are large enough for the compiled show
        int need = Mathf.Max(256, inits.Length);
        if (UseMultiRenderers())
        {
            EnsureStarBuffers(need);
        }
        else
        {
            var main = particleSystemRenderer.main;
            if (main.maxParticles < need) main.maxParticles = need;
            int cap = main.maxParticles;
            if (psBuffer == null || psBuffer.Length != cap)
                psBuffer = new ParticleSystem.Particle[cap];
        }

        // Recreate sim every show start (cheap at MVP scale and avoids capacity bookkeeping)
        sim = new ParticleSim(need);
        sim.SetProfileLookup(profileDatabase != null ? profileDatabase.starProfiles : null);

        t = 0f;
        nextBurstIndex = 0;
        playing = true;
        debugBurstsFired = 0;
        debugNextLogAt = Time.time + Mathf.Max(0.01f, debugPeriodicLogSeconds);

        InitLaunchState();

        if (debugSkipToFirstBurst && bursts != null && bursts.Length > 0)
        {
            // Set t so the first burst triggers immediately on this frame.
            if (UseLaunchParams())
            {
                SimulateShellToFuse();
                burstOrigin = shellPos;
                burstOriginReady = true;
                t = Mathf.Max(0f, launchParams.fuseSeconds + bursts[0].timeLocal - 0.01f);
            }
            else
            {
                t = Mathf.Max(0f, bursts[0].timeLocal - 0.01f);
            }
        }

        if (debugVerboseLogs)
        {
            float nextT = (bursts != null && bursts.Length > 0) ? bursts[0].timeLocal : -1f;
            Debug.Log($"[HanabiPlayback] StartShow: origin={GetOriginPosition()} explodeY={explodeHeight} nextBurstT={nextT:F3} skipToFirst={debugSkipToFirstBurst}");
        }

        ClearStarRenderers();

        sim.Reset();
        var primary = GetPrimaryRenderer();
        if (primary != null) primary.Play(true);
    }

    void SpawnBurst(BurstEvent be)
    {
        // Convert shell-local to world
        Vector3 origin = GetBurstOrigin();

        // Append compiled particles into the live sim.
        // Final world pos = origin + be.posLocal + p.pos0Local
        debugBurstsFired++;
        if (debugVerboseLogs)
        {
            Vector3 centerW = origin + be.posLocal;
            Debug.Log($"[HanabiPlayback] Burst fired #{debugBurstsFired} t={be.timeLocal:F3} count={be.particleCount} start={be.particleStartIndex} centerW={centerW}");
        }
        sim.AppendSpawn(inits, be.particleStartIndex, be.particleCount, origin, be.posLocal);
    }

    Vector3 GetOriginPosition()
    {
        if (launchOrigin != null)
            return launchOrigin.position;
        return transform.position + transform.forward * fallbackForwardDistance;
    }

    bool UseLaunchParams()
    {
        return launchParams.launchSpeed > 0f && launchParams.fuseSeconds > 0f;
    }

    void InitLaunchState()
    {
        burstOriginReady = false;
        burstOrigin = Vector3.zero;

        Vector3 origin = GetOriginPosition();
        if (UseLaunchParams())
        {
            shellPos = origin;
            Vector3 launchDir = (launchOrigin != null) ? launchOrigin.up : Vector3.up;
            shellVel = launchDir.normalized * launchParams.launchSpeed;
            if (launchParams.fuseSeconds <= 0f)
            {
                burstOriginReady = true;
                burstOrigin = shellPos;
            }
        }
        else
        {
            burstOriginReady = true;
            burstOrigin = origin;
            burstOrigin.y = explodeHeight;
        }
    }

    void StepShell(float dt)
    {
        Vector3 accel = new Vector3(0f, -9.81f * launchParams.gravityScale, 0f);
        accel += wind * launchParams.windScale;

        shellVel += accel * dt;
        if (launchParams.dragScale > 0f)
        {
            float speed = shellVel.magnitude;
            shellVel += (-dragK * launchParams.dragScale * speed) * shellVel * dt;
        }
        shellPos += shellVel * dt;
    }

    void SimulateShellToFuse()
    {
        if (!UseLaunchParams()) return;
        float fuseT = launchParams.fuseSeconds;
        if (fuseT <= 0f) return;

        float step = 1f / 60f;
        float time = 0f;
        while (time < fuseT)
        {
            float dt = Mathf.Min(step, fuseT - time);
            StepShell(dt);
            time += dt;
        }
    }

    Vector3 GetBurstOrigin()
    {
        if (!UseLaunchParams()) return burstOrigin;
        return burstOriginReady ? burstOrigin : shellPos;
    }

    void OnGUI()
    {
        if (!debugOverlay) return;

        int blob = (compiledShow != null && compiledShow.blob != null) ? compiledShow.blob.Length : 0;
        int bCount = bursts != null ? bursts.Length : 0;
        float nextT = (bursts != null && nextBurstIndex < bursts.Length) ? bursts[nextBurstIndex].timeLocal : -1f;

        GUILayout.BeginArea(new Rect(10, 10, 460, 170), GUI.skin.box);
        GUILayout.Label("Hanabi Playback (debug)");
        GUILayout.Label($"compiledBlob={blob} bytes  bursts={bCount}  inits={(inits != null ? inits.Length : 0)}");
        GUILayout.Label($"playing={playing}  t={t:F2}  alive={(sim != null ? sim.AliveCount : 0)}  nextIdx={nextBurstIndex}/{bCount}  nextT={nextT:F2}");
        if (UseLaunchParams())
            GUILayout.Label($"launchSpeed={launchParams.launchSpeed:F1} fuse={launchParams.fuseSeconds:F2} g={launchParams.gravityScale:F2} windScale={launchParams.windScale:F2}");
        else
            GUILayout.Label($"origin={(launchOrigin != null ? launchOrigin.position.ToString() : ("fallback:" + fallbackForwardDistance))}  explodeHeight={explodeHeight}");
        GUILayout.Label($"sizeMul={debugCompiledSizeMultiplier}  minSize={debugCompiledMinSize}  skipFirst={debugSkipToFirstBurst}  rebaseFirst={debugRebaseFirstBurstToZero}");
        GUILayout.EndArea();
    }

    void OnDrawGizmosSelected()
    {
        if (!debugGizmos) return;

        Vector3 origin = GetOriginPosition();
        Vector3 burstOrigin = UseLaunchParams() ? GetBurstOrigin() : new Vector3(origin.x, explodeHeight, origin.z);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, burstOrigin);
        Gizmos.DrawWireSphere(burstOrigin, 2f);

        if (bursts != null && nextBurstIndex < bursts.Length)
        {
            Gizmos.color = Color.cyan;
            Vector3 nextCenter = burstOrigin + bursts[nextBurstIndex].posLocal;
            Gizmos.DrawWireSphere(nextCenter, 3f);
        }
    }

    void EnsureStarRenderers()
    {
        if (starParticleSystems == null || starParticleSystems.Length != starKindCount)
            starParticleSystems = new ParticleSystem[starKindCount];

        bool any = HasAnyStarRenderer();
        if (!any && autoFindStarRenderers)
        {
            for (int i = 0; i < starKindCount; i++)
            {
                string name = $"{starRendererPrefix}{(StarKind)i}";
                var go = GameObject.Find(name);
                if (go != null) starParticleSystems[i] = go.GetComponent<ParticleSystem>();
            }
        }

        if (!HasAnyStarRenderer() && particleSystemRenderer != null)
            starParticleSystems[0] = particleSystemRenderer;
    }

    bool HasAnyStarRenderer()
    {
        if (starParticleSystems == null) return false;
        for (int i = 0; i < starParticleSystems.Length; i++)
            if (starParticleSystems[i] != null) return true;
        return false;
    }

    bool UseMultiRenderers()
    {
        return useMultiParticleSystems && HasAnyStarRenderer();
    }

    ParticleSystem GetPrimaryRenderer()
    {
        if (UseMultiRenderers())
        {
            for (int i = 0; i < starParticleSystems.Length; i++)
                if (starParticleSystems[i] != null) return starParticleSystems[i];
        }
        return particleSystemRenderer;
    }

    void ConfigureStarRenderers()
    {
        if (UseMultiRenderers())
        {
            for (int i = 0; i < starParticleSystems.Length; i++)
                ConfigureRenderer(starParticleSystems[i]);
        }
        else
        {
            ConfigureRenderer(particleSystemRenderer);
        }
    }

    static void ConfigureRenderer(ParticleSystem ps)
    {
        if (ps == null) return;
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(256, main.maxParticles);
    }

    void EnsureStarBuffers(int required = 0)
    {
        if (!UseMultiRenderers()) return;
        if (starBuffers == null || starBuffers.Length != starKindCount)
            starBuffers = new ParticleSystem.Particle[starKindCount][];
        if (starCounts == null || starCounts.Length != starKindCount)
            starCounts = new int[starKindCount];

        int need = Mathf.Max(256, required);
        for (int i = 0; i < starKindCount; i++)
        {
            var ps = starParticleSystems[i];
            if (ps == null) continue;
            int multiplier = (i == (int)StarKind.Tail || i == (int)StarKind.Comet) ? (1 + ParticleSim.TrailSamples) : 1;
            var main = ps.main;
            int target = need * multiplier;
            if (main.maxParticles < target) main.maxParticles = target;
            int cap = main.maxParticles;
            if (starBuffers[i] == null || starBuffers[i].Length != cap)
                starBuffers[i] = new ParticleSystem.Particle[cap];
        }
    }

    void ClearStarRenderers()
    {
        if (UseMultiRenderers())
        {
            for (int i = 0; i < starParticleSystems.Length; i++)
            {
                var ps = starParticleSystems[i];
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
            }
        }
        else if (particleSystemRenderer != null)
        {
            particleSystemRenderer.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystemRenderer.Clear(true);
        }
    }
}
