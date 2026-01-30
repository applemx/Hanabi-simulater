using System;
using UnityEngine;

public class StarPreviewController : MonoBehaviour
{
    public enum PreviewMode
    {
        StaticBurst,
        FallingBurst
    }

    public enum SpawnMode
    {
        Burst,
        Single
    }

    [Header("Refs")]
    [SerializeField] HanabiDatabase profileDatabase;
    [SerializeField] ParticleSystem[] starParticleSystems;
    [SerializeField] bool autoFindStarRenderers = true;
    [SerializeField] string starRendererPrefix = "FireworkParticles_";

    [Header("Preview Settings")]
    [SerializeField] PreviewMode mode = PreviewMode.StaticBurst;
    [SerializeField] SpawnMode spawnMode = SpawnMode.Burst;
    [SerializeField] int selectedKind = 0;
    [SerializeField] int particleCount = 600;
    [SerializeField] float speed = 40f;
    [SerializeField] float speedJitter = 8f;
    [SerializeField] float lifeOverride = 0f;
    [SerializeField] float sizeOverride = 0f;
    [SerializeField] float spawnRadius = 0f;
    [SerializeField] Vector3 wind = new Vector3(0.1f, 0f, 0.05f);
    [SerializeField] float dragK = 0.06f;
    [SerializeField] bool autoRespawn = false;
    [SerializeField] float autoRespawnInterval = 2.8f;
    [SerializeField] float singleYaw = 0f;
    [SerializeField] float singlePitch = 90f;
    float savedSpeed;
    float savedSpeedJitter;
    [SerializeField] float camDistance = 16f;
    [SerializeField] float camFov = 40f;
    int defaultParticleCount;
    float defaultSpeed;
    float defaultSpeedJitter;
    float defaultLifeOverride;
    float defaultSizeOverride;
    float defaultSpawnRadius;
    Vector3 defaultWind;
    float defaultDragK;
    bool defaultAutoRespawn;
    float defaultAutoRespawnInterval;
    float defaultCamDistance;
    float defaultCamFov;

    ParticleSystem.Particle[][] starBuffers;
    int[] starCounts;
    ParticleSim sim;
    int starKindCount;
    float respawnTimer;
    uint baseSeed = 12345;

    string[] kindNames;
    string lastDebugInfo = "";
    [SerializeField] Camera previewCamera;

    void Awake()
    {
        if (previewCamera == null)
            previewCamera = Camera.main ?? FindObjectOfType<Camera>();
        starKindCount = Enum.GetValues(typeof(StarKind)).Length;
        kindNames = Enum.GetNames(typeof(StarKind));
        EnsureStarRenderers();
        ConfigureStarRenderers();

        var maxParticles = 4096;
        for (int i = 0; i < starParticleSystems.Length; i++)
        {
            var ps = starParticleSystems[i];
            if (ps == null) continue;
            maxParticles = Mathf.Max(maxParticles, ps.main.maxParticles);
        }

        sim = new ParticleSim(maxParticles);
        if (profileDatabase != null)
            sim.SetProfileLookup(profileDatabase.starProfiles);

        CacheDefaults();

        starBuffers = new ParticleSystem.Particle[starKindCount][];
        starCounts = new int[starKindCount];
        for (int i = 0; i < starKindCount; i++)
        {
            var ps = GetStarRenderer(i);
            if (ps != null)
            {
                starBuffers[i] = new ParticleSystem.Particle[ps.main.maxParticles];
            }
        }
    }

    void Update()
    {
        if (autoRespawn)
        {
            respawnTimer += Time.deltaTime;
            if (respawnTimer >= autoRespawnInterval)
            {
                respawnTimer = 0f;
                Spawn();
            }
        }

        if (sim == null) return;

        Vector3 simWind = (mode == PreviewMode.StaticBurst) ? Vector3.zero : wind;
        float dt = Time.deltaTime;
        float gScale = (mode == PreviewMode.StaticBurst) ? 0f : 1f;
        sim.Step(dt, simWind, dragK, gScale);

        if (starBuffers == null || starCounts == null) return;

        sim.FillParticlesByKind(starBuffers, starCounts);
        for (int i = 0; i < starKindCount; i++)
        {
            var ps = GetStarRenderer(i);
            if (ps == null) continue;
            var buf = starBuffers[i];
            if (buf == null) continue;
            ps.SetParticles(buf, starCounts[i]);
        }

    }

    void Spawn()
    {
        if (sim == null) return;

        sim.Reset();

        if (profileDatabase == null || profileDatabase.starProfiles == null || profileDatabase.starProfiles.Count == 0)
        {
            Debug.LogWarning("[StarPreview] No profile database assigned.");
            return;
        }

        int profileIndex = ResolveProfileIndex(selectedKind, profileDatabase);
        var def = profileDatabase.starProfiles[Mathf.Clamp(profileIndex, 0, profileDatabase.starProfiles.Count - 1)];

        float life = (lifeOverride > 0f) ? lifeOverride : def.baseLife;
        float size = (sizeOverride > 0f) ? sizeOverride : def.baseSize;

        int spawnCount = (spawnMode == SpawnMode.Single) ? 1 : particleCount;
        Color32 color = ResolveColor(profileDatabase);
        var init = new ParticleInitV2[spawnCount];
        Vector3 singleDir = DirectionFromYawPitch(singleYaw, singlePitch);

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 dir = (spawnMode == SpawnMode.Single) ? singleDir : UnityEngine.Random.onUnitSphere;
            float jitter = (spawnMode == SpawnMode.Single) ? 0f : speedJitter;
            float s = speed + UnityEngine.Random.Range(-jitter, jitter);
            Vector3 pos = (spawnRadius > 0f) ? UnityEngine.Random.insideUnitSphere * spawnRadius : Vector3.zero;

            init[i] = new ParticleInitV2
            {
                pos0Local = pos,
                vel0Local = dir * Mathf.Max(0f, s),
                life = Mathf.Max(0.05f, life + UnityEngine.Random.Range(-def.lifeJitter, def.lifeJitter)),
                size = Mathf.Max(0.001f, size + UnityEngine.Random.Range(-def.sizeJitter, def.sizeJitter)),
                color = color,
                spawnDelay = 0f,
                profileId = (ushort)profileIndex,
                seed = baseSeed + (uint)i * 2654435761u,
                flags = 0
            };
        }

        sim.AppendSpawn(init, 0, init.Length, Vector3.zero, Vector3.zero);

    }

    void LogDebugInfo()
    {
        int assignedSystems = 0;
        for (int i = 0; i < starKindCount; i++)
        {
            if (GetStarRenderer(i) != null) assignedSystems++;
        }

        int profileIndex = (profileDatabase != null) ? ResolveProfileIndex(selectedKind, profileDatabase) : -1;
        string profileTag = (profileDatabase != null && profileIndex >= 0 && profileIndex < profileDatabase.starProfiles.Count)
            ? profileDatabase.starProfiles[profileIndex]?.tag
            : "None";

        string modeName = mode.ToString();
        string spawnName = spawnMode.ToString();
        int alive = (sim != null) ? sim.AliveCount : 0;
        string lifeLabel = (lifeOverride > 0f) ? lifeOverride.ToString("F2") : "DB";
        string sizeLabel = (sizeOverride > 0f) ? sizeOverride.ToString("F3") : "DB";

        lastDebugInfo =
            $"[StarPreview] mode={modeName} spawn={spawnName} kind={((StarKind)selectedKind)} profile={profileTag} " +
            $"particles={particleCount} speed={speed:F1} jitter={speedJitter:F1} " +
            $"life={lifeLabel} size={sizeLabel} " +
            $"spawnRadius={spawnRadius:F2} wind=({wind.x:F2},{wind.y:F2},{wind.z:F2}) drag={dragK:F2} " +
            $"systems={assignedSystems}/{starKindCount} alive={alive}";

        Debug.Log(lastDebugInfo);
    }

    int ResolveProfileIndex(int kindIndex, HanabiDatabase db)
    {
        if (db == null || db.starProfiles == null) return 0;
        var kind = (StarKind)Mathf.Clamp(kindIndex, 0, starKindCount - 1);
        for (int i = 0; i < db.starProfiles.Count; i++)
        {
            if (db.starProfiles[i] != null && db.starProfiles[i].kind == kind)
                return i;
        }
        return 0;
    }

    Color32 ResolveColor(HanabiDatabase db)
    {
        if (db == null || db.palettes == null || db.palettes.Count == 0)
            return new Color32(255, 255, 255, 255);

        var pal = db.palettes[0];
        if (pal == null || pal.colors == null || pal.colors.Count == 0)
            return new Color32(255, 255, 255, 255);

        var c = pal.colors[UnityEngine.Random.Range(0, pal.colors.Count)];
        return c;
    }

    void EnsureStarRenderers()
    {
        if (!autoFindStarRenderers) return;
        if (starParticleSystems == null || starParticleSystems.Length != starKindCount)
            starParticleSystems = new ParticleSystem[starKindCount];

        for (int i = 0; i < starKindCount; i++)
        {
            string name = $"{starRendererPrefix}{(StarKind)i}";
            var go = GameObject.Find(name);
            if (go == null) continue;
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null) starParticleSystems[i] = ps;
        }
    }


    ParticleSystem GetStarRenderer(int index)
    {
        if (starParticleSystems == null || index < 0 || index >= starParticleSystems.Length)
            return null;
        return starParticleSystems[index];
    }

    void ConfigureStarRenderers()
    {
        if (starParticleSystems == null) return;
        foreach (var ps in starParticleSystems)
        {
            if (ps == null) continue;
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
        }
    }


    Vector2 scroll;

    void OnGUI()
    {
        const float w = 320f;
        var areaRect = new Rect(16f, 16f, w, Screen.height - 32f);
        GUILayout.BeginArea(areaRect, "Star Preview", GUI.skin.window);

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Width(w - 6f), GUILayout.Height(areaRect.height - 28f));

        GUILayout.Space(8);
        GUILayout.Label("Spawn Mode");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Burst"))
        {
            spawnMode = SpawnMode.Burst;
            RestoreBurstParams();
        }
        if (GUILayout.Button("Single Star"))
        {
            spawnMode = SpawnMode.Single;
        }
        GUILayout.EndHorizontal();
        if (spawnMode == SpawnMode.Burst)
        {
            GUILayout.Label($"Burst Mode: {mode}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Static Burst"))
            {
                mode = PreviewMode.StaticBurst;
                ResetToDefaults();
            }
            if (GUILayout.Button("Falling Burst"))
            {
                mode = PreviewMode.FallingBurst;
                ResetToDefaults();
            }
            GUILayout.EndHorizontal();
        }
        if (spawnMode == SpawnMode.Single)
        {
            GUILayout.Label("Single Star Gravity");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Static (No Gravity)"))
            {
                mode = PreviewMode.StaticBurst;
                ResetToDefaults();
                if (speed > 0f || speedJitter > 0f)
                {
                    savedSpeed = speed;
                    savedSpeedJitter = speedJitter;
                }
                speed = 0f;
                speedJitter = 0f;
            }
            if (GUILayout.Button("Falling (Gravity)"))
            {
                mode = PreviewMode.FallingBurst;
                ResetToDefaults();
                if (savedSpeed > 0f || savedSpeedJitter > 0f)
                {
                    speed = savedSpeed;
                    speedJitter = savedSpeedJitter;
                }
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8);
        GUILayout.Label("Camera Distance");
        camDistance = GUILayout.HorizontalSlider(camDistance, 0.5f, 200f);
        GUILayout.Label($"Distance: {camDistance:F1}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("40")) camDistance = 40f;
        if (GUILayout.Button("80")) camDistance = 80f;
        if (GUILayout.Button("120")) camDistance = 120f;
        if (GUILayout.Button("200")) camDistance = 200f;
        GUILayout.EndHorizontal();
        camFov = GUILayout.HorizontalSlider(camFov, 25f, 60f);
        GUILayout.Label($"FOV: {camFov:F0}");
        SetCameraDistance(camDistance, camFov);
        if (previewCamera != null)
            GUILayout.Label($"Cam Z: {previewCamera.transform.position.z:F1}  FOV: {previewCamera.fieldOfView:F0}");

        GUILayout.Space(8);
        GUILayout.Label("Star Kind");
        selectedKind = GUILayout.SelectionGrid(selectedKind, kindNames, 2);

        GUILayout.Space(8);
        if (spawnMode == SpawnMode.Single)
        {
            GUILayout.Label("Particles: 1 (Single)");
        }
        else
        {
            GUILayout.Label($"Particles: {particleCount}");
            particleCount = (int)GUILayout.HorizontalSlider(particleCount, 50, 2000);
        }
        GUILayout.Label($"Speed: {speed:F1}");
        speed = GUILayout.HorizontalSlider(speed, 5f, 120f);
        if (spawnMode == SpawnMode.Single)
        {
            GUILayout.Label("Speed Jitter: 0 (Single)");
        }
        else
        {
            GUILayout.Label($"Speed Jitter: {speedJitter:F1}");
            speedJitter = GUILayout.HorizontalSlider(speedJitter, 0f, 30f);
        }
        GUILayout.Label($"Life Override: {(lifeOverride <= 0f ? "DB" : lifeOverride.ToString("F2"))}");
        lifeOverride = GUILayout.HorizontalSlider(lifeOverride, 0f, 8f);
        GUILayout.Label($"Size Override: {(sizeOverride <= 0f ? "DB" : sizeOverride.ToString("F3"))}");
        sizeOverride = GUILayout.HorizontalSlider(sizeOverride, 0f, 0.3f);
        GUILayout.Label($"Spawn Radius: {spawnRadius:F2}");
        spawnRadius = GUILayout.HorizontalSlider(spawnRadius, 0f, 1.5f);

        GUILayout.Space(8);
        GUILayout.Label($"Wind: {wind}");
        wind.x = GUILayout.HorizontalSlider(wind.x, -1f, 1f);
        wind.z = GUILayout.HorizontalSlider(wind.z, -1f, 1f);
        GUILayout.Label($"Drag: {dragK:F2}");
        dragK = GUILayout.HorizontalSlider(dragK, 0.0f, 0.2f);

        if (spawnMode == SpawnMode.Single)
        {
            GUILayout.Space(8);
            GUILayout.Label($"Direction Yaw: {singleYaw:F0}");
            singleYaw = GUILayout.HorizontalSlider(singleYaw, -180f, 180f);
            GUILayout.Label($"Direction Pitch: {singlePitch:F0}");
            singlePitch = GUILayout.HorizontalSlider(singlePitch, -90f, 90f);
        }

        GUILayout.Space(8);
        autoRespawn = GUILayout.Toggle(autoRespawn, "Auto Respawn");
        GUILayout.Label($"Auto Interval: {autoRespawnInterval:F1}s");
        autoRespawnInterval = GUILayout.HorizontalSlider(autoRespawnInterval, 0.5f, 6f);

        GUILayout.Space(12);
        if (GUILayout.Button("Spawn")) Spawn();
        if (GUILayout.Button("Clear")) sim?.Reset();
        if (GUILayout.Button("Debug Info")) LogDebugInfo();
        if (!string.IsNullOrEmpty(lastDebugInfo))
            GUILayout.Label(lastDebugInfo, GUI.skin.label);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void SetCameraDistance(float distance, float fov)
    {
        if (previewCamera == null)
            previewCamera = Camera.main ?? FindObjectOfType<Camera>();
        if (previewCamera == null) return;
        var t = previewCamera.transform;
        t.position = new Vector3(0f, 0f, -distance);
        t.rotation = Quaternion.identity;
        previewCamera.fieldOfView = fov;
    }

    void RestoreBurstParams()
    {
        if (savedSpeed > 0f || savedSpeedJitter > 0f)
        {
            speed = savedSpeed;
            speedJitter = savedSpeedJitter;
        }
    }

    void CacheDefaults()
    {
        defaultParticleCount = particleCount;
        defaultSpeed = speed;
        defaultSpeedJitter = speedJitter;
        defaultLifeOverride = lifeOverride;
        defaultSizeOverride = sizeOverride;
        defaultSpawnRadius = spawnRadius;
        defaultWind = wind;
        defaultDragK = dragK;
        defaultAutoRespawn = autoRespawn;
        defaultAutoRespawnInterval = autoRespawnInterval;
        defaultCamDistance = camDistance;
        defaultCamFov = camFov;
    }

    void ResetToDefaults()
    {
        particleCount = defaultParticleCount;
        speed = defaultSpeed;
        speedJitter = defaultSpeedJitter;
        lifeOverride = defaultLifeOverride;
        sizeOverride = defaultSizeOverride;
        spawnRadius = defaultSpawnRadius;
        wind = defaultWind;
        dragK = defaultDragK;
        autoRespawn = defaultAutoRespawn;
        autoRespawnInterval = defaultAutoRespawnInterval;
        camDistance = defaultCamDistance;
        camFov = defaultCamFov;
        SetCameraDistance(camDistance, camFov);
    }

    static Vector3 DirectionFromYawPitch(float yawDeg, float pitchDeg)
    {
        float yaw = yawDeg * Mathf.Deg2Rad;
        float pitch = pitchDeg * Mathf.Deg2Rad;
        float cosPitch = Mathf.Cos(pitch);
        return new Vector3(
            Mathf.Sin(yaw) * cosPitch,
            Mathf.Sin(pitch),
            Mathf.Cos(yaw) * cosPitch
        ).normalized;
    }
}
