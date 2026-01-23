using UnityEngine;

public class HanabiPlaybackController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] CompiledShowAsset compiledShow;
    [SerializeField] ParticleSystem particleSystemRenderer;
    [SerializeField] Transform launchOrigin;           // set your launcher
    [SerializeField] float fallbackForwardDistance = 150f;

    [Header("Playback")]
    [SerializeField] KeyCode launchKey = KeyCode.F;
    [SerializeField] float explodeHeight = 180f;
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
    ParticleSim sim;

    uint seed;
    BurstEvent[] bursts;
    ParticleInitV2[] inits;

    float t;
    int nextBurstIndex;
    bool playing;

    // Debug state
    float debugNextLogAt;
    int debugBurstsFired;


    void Awake()
    {
        if (particleSystemRenderer == null)
        {
            Debug.LogError("[HanabiPlaybackController] ParticleSystem is not assigned.");
            enabled = false;
            return;
        }

        // Configure PS
        var main = particleSystemRenderer.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(256, main.maxParticles);

        psBuffer = new ParticleSystem.Particle[main.maxParticles];
        sim = new ParticleSim(main.maxParticles);

        particleSystemRenderer.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleSystemRenderer.Clear(true);

        LoadCompiled();

        if (playOnStart)
            StartShow();
    }

    void LoadCompiled()
    {
        if (compiledShow == null || compiledShow.blob == null || compiledShow.blob.Length == 0)
        {
            seed = 0;
            bursts = System.Array.Empty<BurstEvent>();
            inits = System.Array.Empty<ParticleInitV2>();
            return;
        }

        if (!CompiledShowSerializer.TryRead(compiledShow.blob, out seed, out bursts, out inits, out _))
        {
            Debug.LogError("[HanabiPlaybackController] Failed to read compiled blob. (wrong version?)");
            bursts = System.Array.Empty<BurstEvent>();
            inits = System.Array.Empty<ParticleInitV2>();
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
            Debug.Log($"[Hanabi] refs: ps={(particleSystemRenderer != null)} origin={(launchOrigin != null)} compiled={(compiledShow != null)} blob={(compiledShow != null && compiledShow.blob != null ? compiledShow.blob.Length : -1)}");


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
        t += dt;

        // Fire bursts
        while (nextBurstIndex < bursts.Length && bursts[nextBurstIndex].timeLocal <= t)
        {
            SpawnBurst(bursts[nextBurstIndex]);
            nextBurstIndex++;
        }

        // Sim
        sim.Step(dt, wind, dragK);
        sim.FillParticles(psBuffer);
        // DEBUG: make compiled particles more visible (optional)
        if (debugCompiledSizeMultiplier != 1f || debugCompiledMinSize > 0f)
        {
            int n = sim.AliveCount;
            float mul = debugCompiledSizeMultiplier;
            float minS = debugCompiledMinSize;
            for (int i = 0; i < n; i++)
            {
                float s = psBuffer[i].startSize * mul;
                if (s < minS) s = minS;
                psBuffer[i].startSize = s;
            }
        }

        if (debugPeriodicLogSeconds > 0f && Time.time >= debugNextLogAt)
        {
            debugNextLogAt = Time.time + debugPeriodicLogSeconds;
            float nextT = (bursts != null && nextBurstIndex < bursts.Length) ? bursts[nextBurstIndex].timeLocal : -1f;
            Debug.Log($"[HanabiPlayback] t={t:F2} alive={sim.AliveCount} nextIdx={nextBurstIndex}/{bursts?.Length ?? 0} nextT={nextT:F2}");
        }
        particleSystemRenderer.SetParticles(psBuffer, sim.AliveCount);

        if (nextBurstIndex >= bursts.Length && sim.AliveCount == 0)
        {
            if (loop) StartShow();
            else playing = false;
        }
    }

    void DebugSpawn()
    {
        var ps = particleSystemRenderer;
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
        var main = particleSystemRenderer.main;
        int need = Mathf.Max(256, inits.Length);
        if (main.maxParticles < need) main.maxParticles = need;

        int cap = main.maxParticles;
        if (psBuffer == null || psBuffer.Length != cap)
            psBuffer = new ParticleSystem.Particle[cap];

        // Recreate sim every show start (cheap at MVP scale and avoids capacity bookkeeping)
        sim = new ParticleSim(cap);

        t = 0f;
        nextBurstIndex = 0;
        playing = true;
        debugBurstsFired = 0;
        debugNextLogAt = Time.time + Mathf.Max(0.01f, debugPeriodicLogSeconds);

        if (debugSkipToFirstBurst && bursts != null && bursts.Length > 0)
        {
            // Set t so the first burst triggers immediately on this frame.
            t = Mathf.Max(0f, bursts[0].timeLocal - 0.01f);
        }

        if (debugVerboseLogs)
        {
            float nextT = (bursts != null && bursts.Length > 0) ? bursts[0].timeLocal : -1f;
            Debug.Log($"[HanabiPlayback] StartShow: origin={GetOriginPosition()} explodeY={explodeHeight} nextBurstT={nextT:F3} skipToFirst={debugSkipToFirstBurst}");
        }

        particleSystemRenderer.Clear(true);

        sim.Reset();
        particleSystemRenderer.Play(true);
    }

    void SpawnBurst(BurstEvent be)
    {
        // Convert shell-local to world
        Vector3 origin = GetOriginPosition();
        origin.y = explodeHeight;

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
        GUILayout.Label($"origin={(launchOrigin != null ? launchOrigin.position.ToString() : ("fallback:" + fallbackForwardDistance))}  explodeHeight={explodeHeight}");
        GUILayout.Label($"sizeMul={debugCompiledSizeMultiplier}  minSize={debugCompiledMinSize}  skipFirst={debugSkipToFirstBurst}  rebaseFirst={debugRebaseFirstBurstToZero}");
        GUILayout.EndArea();
    }

    void OnDrawGizmosSelected()
    {
        if (!debugGizmos) return;

        Vector3 origin = GetOriginPosition();
        Vector3 burstOrigin = origin;
        burstOrigin.y = explodeHeight;

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

}
