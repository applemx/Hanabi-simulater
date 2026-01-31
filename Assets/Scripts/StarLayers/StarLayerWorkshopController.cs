using System;
using System.Collections.Generic;
using UnityEngine;

public class StarLayerWorkshopController : MonoBehaviour
{
    enum UiTab
    {
        StarBuilder,
        LayerLab
    }

    enum LabSimMode
    {
        Static,
        Active
    }

    enum SelectionTarget
    {
        Core,
        Prime,
        Slot1,
        Slot2,
        Slot3,
        Slot4,
        Slot5,
        Slot6
    }

    [Header("Refs")]
    [SerializeField] StarLayerLibrary library;
    [SerializeField] Transform layerRoot;
    [SerializeField] Camera previewCamera;

    [Header("Core / Prime")]
    [SerializeField] StarLayerDef coreLayer;
    [SerializeField] StarLayerSlot coreOverrides = new StarLayerSlot();
    [SerializeField] StarLayerDef primeLayer;
    [SerializeField] StarLayerSlot primeOverrides = new StarLayerSlot();

    [Header("Layer Slots")]
    [SerializeField] List<StarLayerSlot> slots = new List<StarLayerSlot>();
    [SerializeField] int maxLayers = 6;

    [Header("Stack Timing")]
    [SerializeField] bool useStackSpacing = true;
    [SerializeField] float stackSpacing = 0.12f;

    [Header("Global Overrides")]
    [SerializeField] bool useGlobalColor = true;
    [SerializeField] Color globalColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] float globalIntensity = 1f;
    [SerializeField] float globalSizeScale = 1f;
    [SerializeField] float globalLifetimeScale = 1f;

    [Header("Playback")]
    [SerializeField] bool autoRespawn = false;
    [SerializeField] float autoRespawnInterval = 2.5f;

    [Header("Camera")]
    [SerializeField] float camDistance = 10f;
    [SerializeField] float camFov = 45f;

    [Header("Layer Lab")]
    [SerializeField] StarLayerDef labLayer;
    [SerializeField] StarLayerSlot labOverrides = new StarLayerSlot();
    [SerializeField] bool labFilterAll = true;
    [SerializeField] StarLayerCategory labCategoryFilter = StarLayerCategory.Color;
    [SerializeField] bool labAutoSpawn = true;
    [SerializeField] int labSpawnCount = 1;
    [SerializeField] LabSimMode labSimMode = LabSimMode.Active;

    [Header("Star Simulation")]
    [SerializeField] int maxSimParticles = 4096;
    [SerializeField] float baseSpeedJitter = 1.6f;
    [SerializeField] float baseLifeJitter = 0.25f;
    [SerializeField] float baseSizeJitter = 0.02f;
    [SerializeField] float spawnRadius = 0.02f;
    [SerializeField] Vector3 wind = Vector3.zero;
    [SerializeField] float dragK = 0.06f;
    [SerializeField] float gravityScale = 1f;

    [Header("UI")]
    [SerializeField] UiTab uiTab = UiTab.StarBuilder;
    [SerializeField] bool autoSpawnPreset = true;
    string lastPresetName = "";
    string lastPresetMemo = "";

    readonly List<LayerInstance> activeLayers = new List<LayerInstance>();
    SelectionTarget selectionTarget = SelectionTarget.Slot1;
    Vector2 scroll;
    float respawnTimer;
    float simElapsed;

    ParticleSim sim;
    ParticleSystem.Particle[] baseParticles;
    int simCapacity;
    bool simActive;
    uint baseSeed = 12345;

    class LayerInstance
    {
        public StarLayerDef def;
        public StarLayerSlot slot;
        public ParticleSystem system;
        public ParticleSystem.Particle[] buffer;
        public int stackIndex;
        public bool labMode;
    }

    void Awake()
    {
        EnsureSlots();
        EnsureLayerRoot();
        EnsureCamera();
        ApplyCameraSettings();
        InitSimulation();
    }

    void OnValidate()
    {
        EnsureSlots();
        if (maxSimParticles < 256) maxSimParticles = 256;
    }

    void Update()
    {
        if (autoRespawn)
        {
            respawnTimer += Time.deltaTime;
            if (respawnTimer >= autoRespawnInterval)
            {
                respawnTimer = 0f;
                if (uiTab == UiTab.LayerLab)
                    SpawnLab();
                else
                    Spawn();
            }
        }

        if (simActive && sim != null)
        {
            simElapsed += Time.deltaTime;
            Vector3 simWind = wind;
            float simDrag = dragK;
            float simGravity = gravityScale;
            if (uiTab == UiTab.LayerLab && labSimMode == LabSimMode.Static)
            {
                simWind = Vector3.zero;
                simDrag = 0f;
                simGravity = 0f;
            }
            sim.Step(Time.deltaTime, simWind, simDrag, simGravity);
            UpdateLayerRenderers();
        }
    }

    void InitSimulation()
    {
        simCapacity = Mathf.Max(256, maxSimParticles);
        sim = new ParticleSim(simCapacity);
        baseParticles = new ParticleSystem.Particle[simCapacity];
    }

    void EnsureSlots()
    {
        if (maxLayers < 1) maxLayers = 1;
        if (slots == null)
            slots = new List<StarLayerSlot>();

        while (slots.Count < maxLayers)
            slots.Add(new StarLayerSlot());
        if (slots.Count > maxLayers)
            slots.RemoveRange(maxLayers, slots.Count - maxLayers);
    }

    void EnsureLayerRoot()
    {
        if (layerRoot != null) return;
        var root = new GameObject("StarLayerRoot");
        root.transform.SetParent(transform);
        root.transform.localPosition = Vector3.zero;
        layerRoot = root.transform;
    }

    void EnsureCamera()
    {
        if (previewCamera != null) return;
        previewCamera = Camera.main ?? FindObjectOfType<Camera>();
    }

    void ApplyCameraSettings()
    {
        if (previewCamera == null) return;
        var t = previewCamera.transform;
        t.position = new Vector3(0f, 0f, -camDistance);
        t.rotation = Quaternion.identity;
        previewCamera.fieldOfView = camFov;
    }

    public void Rebuild()
    {
        EnsureLayerRoot();
        ClearInstances(true);

        if (coreLayer != null)
            AddInstance(coreLayer, coreOverrides, "Core", 0, false);

        int stackIndex = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || !slot.enabled || slot.layer == null) continue;
            stackIndex++;
            AddInstance(slot.layer, slot, $"L{i + 1}", stackIndex, false);
        }

        if (primeLayer != null)
            AddInstance(primeLayer, primeOverrides, "Prime", 0, false);
    }

    public void RebuildLab()
    {
        EnsureLayerRoot();
        ClearInstances(true);

        if (labLayer != null)
            AddInstance(labLayer, labOverrides, "Lab", 0, true);
    }

    public void Spawn()
    {
        StartSimulation(false);
    }

    public void SpawnLab()
    {
        StartSimulation(true);
    }

    void StartSimulation(bool lab)
    {
        if (activeLayers.Count == 0)
        {
            if (lab) RebuildLab();
            else Rebuild();
        }

        if (activeLayers.Count == 0)
            return;

        if (sim == null || simCapacity != Mathf.Max(256, maxSimParticles))
            InitSimulation();

        sim.Reset();
        simActive = true;
        respawnTimer = 0f;
        simElapsed = 0f;

        var baseDef = ResolveBaseLayer(lab);
        ResolveBaseSpawn(baseDef, out int count, out float speed, out float life, out float size);
        int spawnCount = Mathf.Clamp(count, 1, simCapacity);
        if (lab)
            spawnCount = Mathf.Clamp(labSpawnCount, 1, simCapacity);
        if (lab && labSimMode == LabSimMode.Static)
            speed = 0f;

        var init = new ParticleInitV2[spawnCount];
        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 dir = UnityEngine.Random.onUnitSphere;
            float speedJitter = lab ? 0f : baseSpeedJitter;
            float lifeJitter = lab ? 0f : baseLifeJitter;
            float sizeJitter = lab ? 0f : baseSizeJitter;
            float s = speed + UnityEngine.Random.Range(-speedJitter, speedJitter);
            float l = Mathf.Max(0.05f, life + UnityEngine.Random.Range(-lifeJitter, lifeJitter));
            float sz = Mathf.Max(0.001f, size + UnityEngine.Random.Range(-sizeJitter, sizeJitter));
            float radius = lab ? 0f : spawnRadius;
            Vector3 pos = radius > 0f ? UnityEngine.Random.insideUnitSphere * radius : Vector3.zero;

            init[i] = new ParticleInitV2
            {
                pos0Local = pos,
                vel0Local = dir * Mathf.Max(0f, s),
                life = l,
                size = sz,
                color = new Color32(255, 255, 255, 255),
                spawnDelay = 0f,
                profileId = 0,
                seed = baseSeed + (uint)i * 2654435761u,
                flags = 0
            };
        }

        sim.AppendSpawn(init, 0, init.Length, Vector3.zero, Vector3.zero);
        UpdateLayerRenderers();
    }

    public void Clear()
    {
        simActive = false;
        simElapsed = 0f;
        if (sim != null)
            sim.Reset();
        ClearRenderers();
    }

    void ClearRenderers()
    {
        foreach (var inst in activeLayers)
        {
            if (inst == null || inst.system == null) continue;
            inst.system.SetParticles(Array.Empty<ParticleSystem.Particle>(), 0);
        }
    }

    void AddInstance(StarLayerDef def, StarLayerSlot slot, string label, int stackIndex, bool labMode)
    {
        if (def == null) return;
        GameObject go = null;
        if (def.prefab != null)
            go = Instantiate(def.prefab, layerRoot);
        else
            go = new GameObject($"{label}_{def.name}");

        go.name = $"{label}_{def.name}";
        go.transform.localPosition = Vector3.zero;
        var ps = go.GetComponent<ParticleSystem>();
        if (ps == null)
            ps = go.AddComponent<ParticleSystem>();

        int trailSamples = GetTrailSampleCount(def);
        int maxParticles = simCapacity * (1 + trailSamples);
        ConfigureRendererSystem(ps, maxParticles);

        var instance = new LayerInstance
        {
            def = def,
            slot = slot,
            system = ps,
            buffer = new ParticleSystem.Particle[maxParticles],
            stackIndex = stackIndex,
            labMode = labMode
        };
        activeLayers.Add(instance);
    }

    void ConfigureRendererSystem(ParticleSystem ps, int maxParticles)
    {
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpeed = 0f;
        main.maxParticles = Mathf.Max(1, maxParticles);

        var emission = ps.emission;
        emission.enabled = false;

        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = false;
        var limitVelocityOverLifetime = ps.limitVelocityOverLifetime;
        limitVelocityOverLifetime.enabled = false;
        var inheritVelocity = ps.inheritVelocity;
        inheritVelocity.enabled = false;
        var forceOverLifetime = ps.forceOverLifetime;
        forceOverLifetime.enabled = false;
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = false;
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = false;
        var sizeBySpeed = ps.sizeBySpeed;
        sizeBySpeed.enabled = false;
        var rotationOverLifetime = ps.rotationOverLifetime;
        rotationOverLifetime.enabled = false;
        var rotationBySpeed = ps.rotationBySpeed;
        rotationBySpeed.enabled = false;
        var noise = ps.noise;
        noise.enabled = false;
        var trails = ps.trails;
        trails.enabled = false;
        var subEmitters = ps.subEmitters;
        subEmitters.enabled = false;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void UpdateLayerRenderers()
    {
        if (sim == null || baseParticles == null) return;
        int baseCount = sim.FillParticlesRaw(baseParticles);
        if (baseCount <= 0)
        {
            simActive = false;
            ClearRenderers();
            return;
        }

        for (int i = 0; i < activeLayers.Count; i++)
        {
            var inst = activeLayers[i];
            if (inst == null || inst.system == null || inst.def == null || inst.buffer == null) continue;
            int count = BuildLayerParticles(inst, baseParticles, baseCount);
            inst.system.SetParticles(inst.buffer, count);
        }
    }

    int BuildLayerParticles(LayerInstance inst, ParticleSystem.Particle[] baseBuf, int baseCount)
    {
        var def = inst.def;
        var slot = inst.slot;
        var buffer = inst.buffer;
        int write = 0;

        float layerStart = def.defaultStartDelay;
        if (useStackSpacing && inst.stackIndex > 0)
            layerStart += stackSpacing * inst.stackIndex;
        if (slot != null)
            layerStart += slot.startDelay;

        float layerDuration = 0f;
        if (slot != null && slot.durationOverride > 0f)
            layerDuration = slot.durationOverride;
        else if (def.defaultDuration > 0f)
            layerDuration = def.defaultDuration;

        Color baseColor = def.defaultColor;
        if (slot != null && slot.overrideColor)
            baseColor = slot.color;
        if (useGlobalColor)
            baseColor = globalColor;

        float intensity = def.defaultIntensity * globalIntensity * (slot != null ? slot.intensityScale : 1f);
        float sizeScale = def.defaultSizeScale * globalSizeScale * (slot != null ? slot.sizeScale : 1f);
        float lifetimeScale = def.defaultLifetimeScale * globalLifetimeScale * (slot != null ? slot.lifetimeScale : 1f);

        bool trailLayer = IsTrailLayer(def);
        int trailSamples = trailLayer ? GetTrailSampleCount(def) : 0;

        for (int i = 0; i < baseCount; i++)
        {
            var p = baseBuf[i];
            if (!IsFinite(p.position) || !IsFinite(p.velocity) || !IsFinite(p.startSize) ||
                !IsFinite(p.startLifetime) || !IsFinite(p.remainingLifetime))
                continue;
            float baseLife = p.startLifetime;
            float age = Mathf.Max(0f, baseLife - p.remainingLifetime);

            if (age < layerStart)
                continue;
            if (layerDuration > 0f && age > layerStart + layerDuration)
                continue;

            float renderLife = Mathf.Max(0.05f, baseLife * lifetimeScale);
            float layerT = renderLife > 0f ? Mathf.Clamp01(age / renderLife) : 0f;

            float alphaMul = ComputeLayerAlpha(def, age, renderLife, p.randomSeed);
            if (alphaMul <= 0.001f)
                continue;

            float remaining = Mathf.Max(0.01f, renderLife - age);

            Color c = MultiplyColor(baseColor, intensity);
            c = ApplyLayerColor(def, c, layerT, p.randomSeed);
            c.a = Mathf.Clamp01(c.a * alphaMul);

            sizeScale = ApplyLayerSize(def, sizeScale, layerT);

            p.position = ApplyLayerOffsets(def, p.position, age, p.randomSeed);
            p.startColor = c;
            p.startSize = Mathf.Clamp(p.startSize * sizeScale, 0.0005f, 5f);
            p.startLifetime = Mathf.Clamp(renderLife, 0.02f, 60f);
            p.remainingLifetime = Mathf.Clamp(remaining, 0.01f, p.startLifetime);

            if (!IsFinite(p.position) || !IsFinite(p.startSize) || !IsFinite(p.startLifetime) ||
                !IsFinite(p.remainingLifetime))
                continue;
            if (Mathf.Abs(p.position.x) > 10000f || Mathf.Abs(p.position.y) > 10000f || Mathf.Abs(p.position.z) > 10000f)
                continue;

            if (write < buffer.Length)
                buffer[write++] = p;

            if (trailLayer && trailSamples > 0)
                EmitVelocityStreak(buffer, ref write, p, c, trailSamples, def.trailsLifetime);

            if (IsCrossetteLayer(def))
                EmitCrossette(buffer, ref write, p, c, renderLife, age);

            if (IsCrackleLayer(def))
                EmitCrackle(buffer, ref write, p, c, renderLife, age);
        }

        return write;
    }

    float ComputeLayerAlpha(StarLayerDef def, float age, float life, uint seed)
    {
        float alphaMul = 1f;
        float t = life > 0f ? Mathf.Clamp01(age / life) : 0f;

        if (def.enableStrobe && def.strobeFrequency > 0f && !IsGlitterLayer(def))
        {
            float freq = Mathf.Max(0.1f, def.strobeFrequency);
            float phase = Hash01(seed);
            float frac = Mathf.Repeat(age * freq + phase, 1f);
            float duty = Mathf.Clamp(def.strobeBurstCount / 100f, 0.05f, 0.6f);
            alphaMul *= frac < duty ? 1f : 0f;
        }

        if (IsGlitterLayer(def))
        {
            float freq = Mathf.Max(2f, def.strobeFrequency);
            float phase = Hash01(seed);
            float cycle = age * freq + phase;
            float frac = cycle - Mathf.Floor(cycle);
            float spike = Mathf.Clamp01(1f - frac / 0.18f);
            int tick = Mathf.FloorToInt(age * freq);
            float sparkle = Hash01(seed ^ (uint)tick);
            alphaMul *= 0.1f + spike * (0.9f + 0.5f * sparkle);
        }

        if (IsFlitterLayer(def))
        {
            float freq = Mathf.Max(12f, def.strobeFrequency);
            int tick = Mathf.FloorToInt(age * freq);
            float flicker = Hash01(seed ^ (uint)tick);
            alphaMul *= Mathf.Lerp(0.4f, 1f, flicker);
        }

        if (IsLowSteadyLayer(def) || IsWabiLayer(def))
        {
            int tick = Mathf.FloorToInt(age * 20f);
            float flicker = Hash01(seed ^ (uint)tick);
            alphaMul *= Mathf.Lerp(0.75f, 1f, flicker);
        }

        if (IsCrackleLayer(def))
        {
            if (life > 0f && age > life * 0.65f)
            {
                int tick = Mathf.FloorToInt(age * 40f);
                float s = Hash01(seed ^ (uint)tick);
                alphaMul *= (s > 0.5f) ? 1.3f : 0.25f;
            }
            else
            {
                alphaMul *= 0.1f;
            }
        }

        if (IsAfterglowLayer(def))
            alphaMul *= Mathf.Lerp(0.6f, 0.1f, t);

        if (IsTerminalSlowFadeLayer(def))
            alphaMul *= Mathf.Pow(1f - t, 0.35f);

        return alphaMul;
    }

    static void EmitVelocityStreak(ParticleSystem.Particle[] buffer, ref int write, ParticleSystem.Particle p, Color c, int samples, float lengthScale)
    {
        if (!IsFinite(p.position) || !IsFinite(p.velocity))
            return;
        Vector3 v = p.velocity;
        float speed = v.magnitude;
        if (speed < 0.01f) return;

        Vector3 dir = v / speed;
        float len = Mathf.Clamp(speed * 0.03f * Mathf.Max(0.2f, lengthScale), 0.03f, 1.2f);
        int count = Mathf.Clamp(samples, 1, 12);
        for (int k = 1; k <= count; k++)
        {
            if (write >= buffer.Length) break;
            float t = k / (float)count;
            var tp = p;
            tp.position = p.position - dir * (len * t);
            float alphaScale = Mathf.Lerp(0.8f, 0.1f, t);
            float sizeScale = Mathf.Lerp(0.9f, 0.35f, t);
            var tc = c;
            tc.a = Mathf.Clamp01(c.a * alphaScale);
            tp.startColor = tc;
            tp.startSize *= sizeScale;
            buffer[write++] = tp;
        }
    }

    StarLayerDef ResolveBaseLayer(bool lab)
    {
        if (lab)
            return labLayer;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || !slot.enabled || slot.layer == null) continue;
            return slot.layer;
        }
        return coreLayer != null ? coreLayer : primeLayer;
    }

    void ResolveBaseSpawn(StarLayerDef def, out int count, out float speed, out float life, out float size)
    {
        count = 220;
        speed = 8f;
        life = 3f;
        size = 0.08f;

        if (def == null) return;
        if (def.burstCount > 0) count = def.burstCount;
        if (def.startSpeed > 0f) speed = def.startSpeed;
        if (def.startLifetime > 0f) life = def.startLifetime;
        if (def.startSize > 0f) size = def.startSize;
    }

    static Color MultiplyColor(Color color, float intensity)
    {
        return new Color(color.r * intensity, color.g * intensity, color.b * intensity, color.a);
    }

    static int GetTrailSampleCount(StarLayerDef def)
    {
        if (def == null) return 0;
        if (def.category != StarLayerCategory.Trail) return 0;
        if (def.trailsLifetime <= 0f) return 6;
        return Mathf.Clamp(Mathf.RoundToInt(def.trailsLifetime * 10f), 3, 12);
    }

    static bool IsTrailLayer(StarLayerDef def)
    {
        return def != null && def.category == StarLayerCategory.Trail;
    }

    static bool IsGlitterLayer(StarLayerDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return false;
        var id = def.id.ToLowerInvariant();
        return id.Contains("glitter") || id.Contains("tremolant") || id.Contains("firefly");
    }

    static bool IsCrackleLayer(StarLayerDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return false;
        return def.id.ToLowerInvariant().Contains("crackle");
    }

    static bool IsCrossetteLayer(StarLayerDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return false;
        return def.id.ToLowerInvariant().Contains("crossette");
    }

    static bool IsFlitterLayer(StarLayerDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return false;
        var id = def.id.ToLowerInvariant();
        return id.Contains("flitter") || id.Contains("spangle");
    }

    static bool IsLowSteadyLayer(StarLayerDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return false;
        return def.id.ToLowerInvariant().Contains("steady_color_low");
    }

    static bool IsWabiLayer(StarLayerDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return false;
        return def.id.ToLowerInvariant().Contains("wabi");
    }

    static bool IsAfterglowLayer(StarLayerDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return false;
        return def.id.ToLowerInvariant().Contains("afterglow");
    }

    static bool IsTerminalFlashLayer(StarLayerDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return false;
        return def.id.ToLowerInvariant().Contains("terminal_flash");
    }

    static bool IsTerminalSlowFadeLayer(StarLayerDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return false;
        return def.id.ToLowerInvariant().Contains("terminal_slow_fade");
    }

    static bool IsFallingLeavesLayer(StarLayerDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return false;
        return def.id.ToLowerInvariant().Contains("falling_leaves");
    }

    static Color ApplyLayerColor(StarLayerDef def, Color c, float t, uint seed)
    {
        if (IsGlitterLayer(def))
        {
            float r = Hash01(seed ^ 0x9e3779b9u);
            var warm = new Color(1f, 0.95f, 0.7f, c.a);
            c = Color.Lerp(c, warm, 0.35f * r);
        }

        if (IsTerminalFlashLayer(def))
            c = Color.Lerp(c, Color.white, 0.9f);

        if (IsTerminalSlowFadeLayer(def))
        {
            var warm = new Color(c.r, c.g * 0.8f, c.b * 0.6f, c.a);
            c = Color.Lerp(c, warm, t);
        }

        return c;
    }

    static float ApplyLayerSize(StarLayerDef def, float sizeScale, float t)
    {
        if (IsTerminalFlashLayer(def))
            sizeScale *= 0.45f;

        if (IsAfterglowLayer(def))
            sizeScale *= Mathf.Lerp(1f, 2.4f, t);

        return sizeScale;
    }

    static Vector3 ApplyLayerOffsets(StarLayerDef def, Vector3 pos, float age, uint seed)
    {
        if (def == null) return pos;

        if (def.enableNoise && def.noiseStrength > 0f)
        {
            float f = Mathf.Max(0.01f, def.noiseFrequency);
            float t = age * f;
            float sx = Hash01(seed) * 10f;
            float sy = Hash01(seed ^ 0x68bc21u) * 10f;
            float sz = Hash01(seed ^ 0x02e5be93u) * 10f;
            float nx = Mathf.PerlinNoise(sx, t) - 0.5f;
            float ny = Mathf.PerlinNoise(sy, t + 3.3f) - 0.5f;
            float nz = Mathf.PerlinNoise(sz, t + 7.7f) - 0.5f;
            pos += new Vector3(nx, ny, nz) * def.noiseStrength;
        }

        if (IsFallingLeavesLayer(def))
        {
            float sway = Mathf.Sin(age * 2.1f + Hash01(seed) * Mathf.PI * 2f);
            pos.x += sway * 0.25f;
        }

        if (Mathf.Abs(def.gravityModifier) > 0.001f)
            pos += Vector3.down * (0.5f * def.gravityModifier * age * age);

        return pos;
    }

    static void EmitCrossette(ParticleSystem.Particle[] buffer, ref int write, ParticleSystem.Particle p, Color c, float life, float age)
    {
        if (life <= 0f) return;
        float t = age / life;
        if (t < 0.55f) return;

        float splitAge = Mathf.Max(0f, age - life * 0.55f);
        float offset = Mathf.Clamp(splitAge * 1.6f, 0.05f, 1.2f);
        Vector3 dir = p.velocity.sqrMagnitude > 0.0001f ? p.velocity.normalized : Vector3.right;
        Vector3 right = Vector3.Cross(dir, Vector3.up);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.Cross(dir, Vector3.forward);
        right.Normalize();
        Vector3 up = Vector3.Cross(right, dir).normalized;

        AddDerivedParticle(buffer, ref write, p, c, p.position + right * offset, 0.75f);
        AddDerivedParticle(buffer, ref write, p, c, p.position - right * offset, 0.75f);
        AddDerivedParticle(buffer, ref write, p, c, p.position + up * offset, 0.75f);
        AddDerivedParticle(buffer, ref write, p, c, p.position - up * offset, 0.75f);
    }

    static void EmitCrackle(ParticleSystem.Particle[] buffer, ref int write, ParticleSystem.Particle p, Color c, float life, float age)
    {
        if (life <= 0f) return;
        float t = age / life;
        if (t < 0.7f) return;

        float burst = Mathf.Clamp01((t - 0.7f) / 0.3f);
        for (int i = 0; i < 4; i++)
        {
            uint s = p.randomSeed ^ (uint)(i * 2654435761u);
            Vector3 dir = HashUnitVector(s);
            float radius = 0.15f + 0.35f * burst;
            Vector3 pos = p.position + dir * radius;
            AddDerivedParticle(buffer, ref write, p, c, pos, 0.6f);
        }
    }

    static void AddDerivedParticle(ParticleSystem.Particle[] buffer, ref int write, ParticleSystem.Particle baseP, Color c, Vector3 pos, float sizeScale)
    {
        if (write >= buffer.Length) return;
        if (!IsFinite(pos))
            return;
        var p = baseP;
        p.position = pos;
        p.startColor = c;
        p.startSize *= sizeScale;
        buffer[write++] = p;
    }

    static Vector3 HashUnitVector(uint seed)
    {
        float x = Hash01(seed) * 2f - 1f;
        float y = Hash01(seed ^ 0x68bc21u) * 2f - 1f;
        float z = Hash01(seed ^ 0x02e5be93u) * 2f - 1f;
        var v = new Vector3(x, y, z);
        if (v.sqrMagnitude < 0.0001f)
            return Vector3.up;
        return v.normalized;
    }

    static bool IsFinite(float v)
    {
        return !float.IsNaN(v) && !float.IsInfinity(v);
    }

    static bool IsFinite(Vector3 v)
    {
        return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
    }

    static float Hash01(uint x)
    {
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        return (x & 0x00FFFFFF) / 16777215.0f;
    }

    void ClearInstances(bool destroy)
    {
        if (destroy)
        {
            foreach (var inst in activeLayers)
            {
                if (inst == null || inst.system == null) continue;
                DestroyGameObject(inst.system.gameObject);
            }
        }
        activeLayers.Clear();
    }

    void DestroyGameObject(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    void OnGUI()
    {
        const float w = 360f;
        var areaRect = new Rect(16f, 16f, w, Screen.height - 32f);
        GUILayout.BeginArea(areaRect, "Star Layer Workshop", GUI.skin.window);

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Width(w - 6f), GUILayout.Height(areaRect.height - 28f));

        GUILayout.Label($"Library: {(library != null ? library.name : "None")}");

        GUILayout.Space(6);
        uiTab = (UiTab)GUILayout.Toolbar((int)uiTab, new[] { "Star Builder", "Layer Lab" });

        GUILayout.Space(6);
        bool showDebug = uiTab == UiTab.StarBuilder;
        if (showDebug)
            DrawStarBuilderUI();
        else
            DrawLayerLabUI();

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        if (showDebug)
            DrawStarBuilderDebugPanel();
    }

    void DrawStarBuilderUI()
    {
        DrawPresetList();

        GUILayout.Space(6);
        GUILayout.Label("Select Target");
        selectionTarget = (SelectionTarget)GUILayout.SelectionGrid((int)selectionTarget, new[]
        {
            "Core","Prime","L1","L2","L3","L4","L5","L6"
        }, 4);

        GUILayout.Space(6);
        DrawTargetInfo();

        GUILayout.Space(8);
        DrawStackTiming();

        GUILayout.Space(8);
        DrawGlobalOverrides();

        GUILayout.Space(8);
        DrawPlaybackControls();

        GUILayout.Space(8);
        DrawCameraControls();

        GUILayout.Space(8);
        DrawLibraryList();

        GUILayout.Space(8);
        if (GUILayout.Button("Rebuild")) Rebuild();
        if (GUILayout.Button("Spawn")) Spawn();
        if (GUILayout.Button("Rebuild & Spawn"))
        {
            Rebuild();
            Spawn();
        }
        if (GUILayout.Button("Clear")) Clear();
    }

    void DrawLayerLabUI()
    {
        DrawLabLibraryList();

        GUILayout.Space(6);
        GUILayout.Label($"Layer: {(labLayer != null ? GetDisplayName(labLayer) : "None")}");
        DrawSlotOverrides(labOverrides);
        if (GUILayout.Button("Clear Layer")) labLayer = null;

        GUILayout.Space(6);
        GUILayout.Label("Simulation Mode");
        labSimMode = (LabSimMode)GUILayout.Toolbar((int)labSimMode, new[] { "Static", "Active" });
        labAutoSpawn = GUILayout.Toggle(labAutoSpawn, "Auto Spawn On Select");
        GUILayout.Label($"Lab Spawn Count: {Mathf.Max(1, labSpawnCount)} (Single Star)");

        GUILayout.Space(8);
        DrawGlobalOverrides();

        GUILayout.Space(8);
        DrawPlaybackControls();

        GUILayout.Space(8);
        DrawCameraControls();

        GUILayout.Space(8);
        if (GUILayout.Button("Rebuild Lab")) RebuildLab();
        if (GUILayout.Button("Spawn Lab")) SpawnLab();
        if (GUILayout.Button("Rebuild & Spawn"))
        {
            RebuildLab();
            SpawnLab();
        }
        if (GUILayout.Button("Clear")) Clear();
    }

    void DrawPresetList()
    {
        GUILayout.Label("Star Presets");
        if (library == null || library.presets == null || library.presets.Count == 0)
        {
            GUILayout.Label("(No presets in library)");
            return;
        }

        autoSpawnPreset = GUILayout.Toggle(autoSpawnPreset, "Auto Spawn On Apply");

        for (int i = 0; i < library.presets.Count; i++)
        {
            var preset = library.presets[i];
            if (preset == null) continue;
            if (GUILayout.Button(preset.displayName))
            {
                ApplyPreset(preset);
                if (autoSpawnPreset)
                {
                    Rebuild();
                    Spawn();
                }
            }
        }

        if (!string.IsNullOrEmpty(lastPresetName))
            GUILayout.Label($"Last Preset: {lastPresetName}");
        if (!string.IsNullOrEmpty(lastPresetMemo))
            GUILayout.Label(lastPresetMemo);
    }

    void DrawStackTiming()
    {
        GUILayout.Label("Stack Timing");
        useStackSpacing = GUILayout.Toggle(useStackSpacing, "Use Stack Spacing");
        if (useStackSpacing)
        {
            GUILayout.Label($"Spacing: {stackSpacing:F2}s");
            stackSpacing = GUILayout.HorizontalSlider(stackSpacing, 0f, 0.5f);
        }
    }

    void DrawLabLibraryList()
    {
        GUILayout.Label("Layer Library");
        if (library == null || library.layers == null || library.layers.Count == 0)
        {
            GUILayout.Label("(No layers in library)");
            return;
        }

        labFilterAll = GUILayout.Toggle(labFilterAll, "All Categories");
        if (!labFilterAll)
        {
            var names = Enum.GetNames(typeof(StarLayerCategory));
            labCategoryFilter = (StarLayerCategory)GUILayout.SelectionGrid((int)labCategoryFilter, names, 3);
        }

        var list = GetFilteredLibraryForLab();
        if (list.Count == 0)
        {
            GUILayout.Label("(No matching layers)");
            return;
        }

        foreach (var def in list)
        {
            if (def == null) continue;
            string label = $"{GetDisplayName(def)} [{def.category}]";
            if (GUILayout.Button(label))
            {
                labLayer = def;
                if (labAutoSpawn)
                {
                    RebuildLab();
                    SpawnLab();
                }
            }
        }
    }

    void ApplyPreset(StarStackPreset preset)
    {
        if (preset == null) return;
        EnsureSlots();

        coreLayer = preset.core;
        primeLayer = preset.prime;

        ResetSlotOverrides(coreOverrides);
        ResetSlotOverrides(primeOverrides);

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            ResetSlotOverrides(slot);
            if (i < preset.layers.Count)
                slot.layer = preset.layers[i];
            else
                slot.layer = null;
        }

        lastPresetName = preset.displayName;
        lastPresetMemo = preset.memo;
        selectionTarget = SelectionTarget.Slot1;
    }

    void ResetSlotOverrides(StarLayerSlot slot)
    {
        if (slot == null) return;
        slot.enabled = true;
        slot.startDelay = 0f;
        slot.durationOverride = 0f;
        slot.overrideColor = false;
        slot.color = Color.white;
        slot.intensityScale = 1f;
        slot.sizeScale = 1f;
        slot.lifetimeScale = 1f;
    }

    void DrawTargetInfo()
    {
        switch (selectionTarget)
        {
            case SelectionTarget.Core:
                GUILayout.Label($"Core: {(coreLayer != null ? GetDisplayName(coreLayer) : "None")}");
                DrawSlotOverrides(coreOverrides);
                if (GUILayout.Button("Clear Core")) coreLayer = null;
                break;
            case SelectionTarget.Prime:
                GUILayout.Label($"Prime: {(primeLayer != null ? GetDisplayName(primeLayer) : "None")}");
                DrawSlotOverrides(primeOverrides);
                if (GUILayout.Button("Clear Prime")) primeLayer = null;
                break;
            default:
                int slotIndex = (int)selectionTarget - (int)SelectionTarget.Slot1;
                if (slotIndex >= 0 && slotIndex < slots.Count)
                {
                    var slot = slots[slotIndex];
                    GUILayout.Label($"Slot {slotIndex + 1}: {(slot.layer != null ? GetDisplayName(slot.layer) : "None")}");
                    slot.enabled = GUILayout.Toggle(slot.enabled, "Enabled");
                    DrawSlotOverrides(slot);
                    if (GUILayout.Button("Clear Slot")) slot.layer = null;
                }
                break;
        }
    }

    void DrawSlotOverrides(StarLayerSlot slot)
    {
        if (slot == null) return;

        GUILayout.Label($"Start Delay: {slot.startDelay:F2}s");
        slot.startDelay = GUILayout.HorizontalSlider(slot.startDelay, 0f, 2f);

        GUILayout.Label($"Duration Override: {(slot.durationOverride > 0f ? slot.durationOverride.ToString("F2") : "Default")}");
        slot.durationOverride = GUILayout.HorizontalSlider(slot.durationOverride, 0f, 8f);

        slot.overrideColor = GUILayout.Toggle(slot.overrideColor, "Override Color");
        if (slot.overrideColor)
        {
            DrawColorSliders(ref slot.color);
        }

        GUILayout.Label($"Intensity: {slot.intensityScale:F2}");
        slot.intensityScale = GUILayout.HorizontalSlider(slot.intensityScale, 0.1f, 3f);

        GUILayout.Label($"Size Scale: {slot.sizeScale:F2}");
        slot.sizeScale = GUILayout.HorizontalSlider(slot.sizeScale, 0.2f, 3f);

        GUILayout.Label($"Lifetime Scale: {slot.lifetimeScale:F2}");
        slot.lifetimeScale = GUILayout.HorizontalSlider(slot.lifetimeScale, 0.2f, 3f);
    }

    void DrawGlobalOverrides()
    {
        GUILayout.Label("Global Overrides");
        useGlobalColor = GUILayout.Toggle(useGlobalColor, "Use Global Color");
        if (useGlobalColor)
            DrawColorSliders(ref globalColor);

        GUILayout.Label($"Global Intensity: {globalIntensity:F2}");
        globalIntensity = GUILayout.HorizontalSlider(globalIntensity, 0.1f, 3f);

        GUILayout.Label($"Global Size Scale: {globalSizeScale:F2}");
        globalSizeScale = GUILayout.HorizontalSlider(globalSizeScale, 0.2f, 3f);

        GUILayout.Label($"Global Lifetime Scale: {globalLifetimeScale:F2}");
        globalLifetimeScale = GUILayout.HorizontalSlider(globalLifetimeScale, 0.2f, 3f);
    }

    void DrawPlaybackControls()
    {
        autoRespawn = GUILayout.Toggle(autoRespawn, "Auto Respawn");
        GUILayout.Label($"Interval: {autoRespawnInterval:F1}s");
        autoRespawnInterval = GUILayout.HorizontalSlider(autoRespawnInterval, 0.5f, 6f);
    }

    void DrawCameraControls()
    {
        GUILayout.Label("Camera");
        camDistance = GUILayout.HorizontalSlider(camDistance, 0.5f, 150f);
        GUILayout.Label($"Distance: {camDistance:F1}");
        camFov = GUILayout.HorizontalSlider(camFov, 25f, 60f);
        GUILayout.Label($"FOV: {camFov:F0}");
        ApplyCameraSettings();
    }

    void DrawLibraryList()
    {
        GUILayout.Label("Layer Library");
        if (library == null || library.layers == null || library.layers.Count == 0)
        {
            GUILayout.Label("(No layers in library)");
            return;
        }

        var filtered = GetFilteredLibrary(selectionTarget);
        if (filtered.Count == 0)
        {
            GUILayout.Label("(No matching layers)");
            return;
        }

        foreach (var def in filtered)
        {
            if (def == null) continue;
            string label = $"{GetDisplayName(def)} [{def.category}]";
            if (GUILayout.Button(label))
                AssignToTarget(def);
        }
    }

    void DrawStarBuilderDebugPanel()
    {
        const float w = 320f;
        var area = new Rect(Screen.width - w - 16f, 16f, w, Screen.height - 32f);
        GUILayout.BeginArea(area, "Star Debug", GUI.skin.window);

        GUILayout.Label("Playback");
        GUILayout.Label($"Sim Active: {simActive}");
        GUILayout.Label($"Alive: {(sim != null ? sim.AliveCount : 0)}");
        GUILayout.Label($"Auto Respawn: {autoRespawn}");

        GUILayout.Space(6);
        GUILayout.Label("Preset");
        GUILayout.Label(string.IsNullOrEmpty(lastPresetName) ? "(None)" : lastPresetName);
        if (!string.IsNullOrEmpty(lastPresetMemo))
            GUILayout.Label(lastPresetMemo);

        GUILayout.Space(6);
        GUILayout.Label("Layers");
        GUILayout.Label($"Core: {(coreLayer != null ? GetDisplayName(coreLayer) : "None")}");
        GUILayout.Label($"Prime: {(primeLayer != null ? GetDisplayName(primeLayer) : "None")}");
        float baseLife = GetBaseLifeForDebug();
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            string name = (slot != null && slot.layer != null) ? GetDisplayName(slot.layer) : "None";
            string state = (slot != null && slot.enabled) ? "On" : "Off";
            string active = IsLayerActiveForDebug(i, slot, baseLife) ? ">>" : "  ";
            GUILayout.Label($"{active} L{i + 1}: {name} [{state}]");
        }

        GUILayout.Space(6);
        GUILayout.Label("Active Renderers");
        for (int i = 0; i < activeLayers.Count; i++)
        {
            var inst = activeLayers[i];
            if (inst == null || inst.def == null) continue;
            string label = inst.stackIndex > 0 ? $"L{inst.stackIndex}" : "Core/Prime";
            GUILayout.Label($"{label}: {GetDisplayName(inst.def)}");
        }

        GUILayout.EndArea();
    }

    float GetBaseLifeForDebug()
    {
        var baseDef = ResolveBaseLayer(false);
        ResolveBaseSpawn(baseDef, out _, out _, out float life, out _);
        return Mathf.Max(0.01f, life);
    }

    bool IsLayerActiveForDebug(int slotIndex, StarLayerSlot slot, float baseLife)
    {
        if (!simActive || slot == null || !slot.enabled || slot.layer == null)
            return false;

        var def = slot.layer;
        float start = def.defaultStartDelay + slot.startDelay;
        int stackIndex = slotIndex + 1;
        if (useStackSpacing)
            start += stackSpacing * stackIndex;

        float duration = 0f;
        if (slot.durationOverride > 0f)
            duration = slot.durationOverride;
        else if (def.defaultDuration > 0f)
            duration = def.defaultDuration;
        else
            duration = baseLife * def.defaultLifetimeScale * globalLifetimeScale * slot.lifetimeScale;

        float end = start + Mathf.Max(0.02f, duration);
        return simElapsed >= start && simElapsed <= end;
    }

    List<StarLayerDef> GetFilteredLibrary(SelectionTarget target)
    {
        var result = new List<StarLayerDef>();
        if (library == null || library.layers == null) return result;

        for (int i = 0; i < library.layers.Count; i++)
        {
            var def = library.layers[i];
            if (def == null) continue;

            if (target == SelectionTarget.Core)
            {
                if (def.category != StarLayerCategory.Core) continue;
            }
            else if (target == SelectionTarget.Prime)
            {
                if (def.category != StarLayerCategory.Prime) continue;
            }
            else
            {
                if (def.category == StarLayerCategory.Core || def.category == StarLayerCategory.Prime)
                    continue;
            }

            result.Add(def);
        }
        return result;
    }

    List<StarLayerDef> GetFilteredLibraryForLab()
    {
        var result = new List<StarLayerDef>();
        if (library == null || library.layers == null) return result;

        for (int i = 0; i < library.layers.Count; i++)
        {
            var def = library.layers[i];
            if (def == null) continue;
            if (!labFilterAll && def.category != labCategoryFilter) continue;
            result.Add(def);
        }
        return result;
    }

    void AssignToTarget(StarLayerDef def)
    {
        if (def == null) return;
        switch (selectionTarget)
        {
            case SelectionTarget.Core:
                coreLayer = def;
                break;
            case SelectionTarget.Prime:
                primeLayer = def;
                break;
            default:
                int slotIndex = (int)selectionTarget - (int)SelectionTarget.Slot1;
                if (slotIndex >= 0 && slotIndex < slots.Count)
                {
                    slots[slotIndex].layer = def;
                }
                break;
        }
    }

    static string GetDisplayName(StarLayerDef def)
    {
        if (def == null) return "None";
        if (!string.IsNullOrEmpty(def.displayName)) return def.displayName;
        return def.name;
    }

    static void DrawColorSliders(ref Color color)
    {
        GUILayout.Label($"R: {color.r:F2}");
        color.r = GUILayout.HorizontalSlider(color.r, 0f, 1f);
        GUILayout.Label($"G: {color.g:F2}");
        color.g = GUILayout.HorizontalSlider(color.g, 0f, 1f);
        GUILayout.Label($"B: {color.b:F2}");
        color.b = GUILayout.HorizontalSlider(color.b, 0f, 1f);
        GUILayout.Label($"A: {color.a:F2}");
        color.a = GUILayout.HorizontalSlider(color.a, 0f, 1f);
    }
}
