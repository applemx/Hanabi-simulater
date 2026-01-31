using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class StarLayerLibraryEditorUtil
{
    public const string DefaultLibraryPath = "Assets/Data/StarLayers/StarLayerLibrary_Default.asset";
    const string LayerDataFolder = "Assets/Data/StarLayers";
    const string LayerPrefabFolder = "Assets/Prefabs/StarLayers";

    public static StarLayerLibrary GetOrCreateDefault()
    {
        var lib = AssetDatabase.LoadAssetAtPath<StarLayerLibrary>(DefaultLibraryPath);
        if (lib == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DefaultLibraryPath));
            lib = ScriptableObject.CreateInstance<StarLayerLibrary>();
            AssetDatabase.CreateAsset(lib, DefaultLibraryPath);
            AssetDatabase.SaveAssets();
        }

        EnsureDefaultLayers(lib);
        EnsureDefaultPresets(lib);

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssetIfDirty(lib);
        AssetDatabase.SaveAssets();
        return lib;
    }

    static void EnsureDefaultLayers(StarLayerLibrary lib)
    {
        if (lib == null) return;
        if (lib.layers == null) lib.layers = new List<StarLayerDef>();
        for (int i = lib.layers.Count - 1; i >= 0; i--)
        {
            if (lib.layers[i] == null)
                lib.layers.RemoveAt(i);
        }

        Directory.CreateDirectory(LayerDataFolder);
        Directory.CreateDirectory(LayerPrefabFolder);

        var core = MakeLayer(lib, "SL_Core.asset", "core", "Core", StarLayerCategory.Core);
        ConfigureInvisible(core);

        var bodyCoat = MakeLayer(lib, "SL_BodyCoat.asset", "body_coat", "Body Coat", StarLayerCategory.Control);
        ConfigureInvisible(bodyCoat);
        bodyCoat.defaultSizeScale = 1.2f;

        var adhesion = MakeLayer(lib, "SL_AdhesionCoat.asset", "adhesion_coat", "Adhesion Coat", StarLayerCategory.Control);
        ConfigureInvisible(adhesion);
        adhesion.defaultDuration = 0.05f;

        var delay = MakeLayer(lib, "SL_Delay.asset", "delay", "Delay", StarLayerCategory.Control);
        ConfigureInvisible(delay);
        delay.defaultDuration = 0.2f;

        var barrier = MakeLayer(lib, "SL_Barrier.asset", "barrier", "Barrier / Blind", StarLayerCategory.Control);
        ConfigureInvisible(barrier);
        barrier.defaultDuration = 0.1f;

        var seal = MakeLayer(lib, "SL_Seal.asset", "seal_coat", "Seal Coat", StarLayerCategory.Control);
        ConfigureInvisible(seal);

        var prime = MakeLayer(lib, "SL_Prime.asset", "prime", "Prime Flash", StarLayerCategory.Prime);
        ConfigurePrime(prime, 0.08f, 80, 0.18f);
        prime.defaultIntensity = 1.6f;
        prime.defaultColor = new Color(1f, 0.96f, 0.85f);

        var starter = MakeLayer(lib, "SL_Starter.asset", "starter", "Starter", StarLayerCategory.Prime);
        ConfigurePrime(starter, 0.06f, 50, 0.13f);
        starter.defaultIntensity = 1.05f;
        starter.defaultColor = new Color(1f, 0.7f, 0.4f);
        starter.defaultStartDelay = 0.08f;

        var darkPrime = MakeLayer(lib, "SL_DarkPrime.asset", "dark_prime", "Dark Prime", StarLayerCategory.Control);
        ConfigureInvisible(darkPrime);
        darkPrime.defaultDuration = 0.12f;

        var steadyHigh = MakeLayer(lib, "SL_SteadyColorHigh.asset", "steady_color_high", "Steady Color (High)", StarLayerCategory.Color);
        ConfigureSteady(steadyHigh, 240, 8f, 1.8f, 0.07f, 1.05f, new Color(1f, 1f, 1f));

        var steadyLow = MakeLayer(lib, "SL_SteadyColorLow.asset", "steady_color_low", "Steady Color (Low)", StarLayerCategory.Color);
        ConfigureSteady(steadyLow, 200, 7f, 1.6f, 0.075f, 0.6f, new Color(0.9f, 0.85f, 0.75f));

        var illumination = MakeLayer(lib, "SL_Illumination.asset", "illumination", "Illumination / Flare", StarLayerCategory.Color);
        ConfigureSteady(illumination, 60, 3.0f, 6.5f, 0.12f, 1.6f, new Color(1f, 0.97f, 0.9f));
        illumination.gravityModifier = 0.25f;

        var chrysanthemum = MakeLayer(lib, "SL_ChrysanthemumTail.asset", "chrysanthemum_tail", "Chrysanthemum Tail", StarLayerCategory.Trail);
        ConfigureTail(chrysanthemum, 220, 7f, 3.0f, 0.055f, 0.8f, 1f, new Color(1f, 0.85f, 0.6f));

        var willow = MakeLayer(lib, "SL_WillowTail.asset", "willow_tail", "Willow / Kamuro Tail", StarLayerCategory.Trail);
        ConfigureTail(willow, 220, 4.2f, 5.8f, 0.06f, 1.7f, 1.0f, new Color(0.95f, 0.85f, 0.6f));
        willow.gravityModifier = 0.6f;

        var wabi = MakeLayer(lib, "SL_WabiTail.asset", "wabi_tail", "Wabi Tail", StarLayerCategory.Trail);
        ConfigureTail(wabi, 110, 5.0f, 1.8f, 0.055f, 0.5f, 0.5f, new Color(0.6f, 0.45f, 0.25f));
        wabi.enableNoise = true;
        wabi.noiseStrength = 0.05f;
        wabi.noiseFrequency = 1.5f;

        var tiger = MakeLayer(lib, "SL_TigerTail.asset", "tiger_tail", "Tiger Tail", StarLayerCategory.Trail);
        ConfigureTail(tiger, 120, 4.6f, 5.2f, 0.1f, 1.6f, 0.5f, new Color(0.5f, 0.22f, 0.12f));
        tiger.enableNoise = true;
        tiger.noiseStrength = 0.12f;
        tiger.noiseFrequency = 0.8f;

        var brocade = MakeLayer(lib, "SL_BrocadeTail.asset", "brocade_tail", "Brocade / Gold Tail", StarLayerCategory.Trail);
        ConfigureTail(brocade, 260, 6.5f, 3.6f, 0.075f, 1.1f, 1.2f, new Color(1f, 0.8f, 0.35f));

        var goldDust = MakeLayer(lib, "SL_GoldDust.asset", "gold_dust", "Gold Dust", StarLayerCategory.Trail);
        ConfigureTail(goldDust, 640, 4.0f, 0.2f, 0.025f, 0.15f, 1.0f, new Color(1f, 0.85f, 0.5f));

        var silverLine = MakeLayer(lib, "SL_SilverLine.asset", "silver_line", "Silver Line", StarLayerCategory.Trail);
        ConfigureTail(silverLine, 220, 9.5f, 1.6f, 0.03f, 0.25f, 1.05f, new Color(0.85f, 0.9f, 1f));

        var silverKamuro = MakeLayer(lib, "SL_SilverKamuro.asset", "silver_kamuro", "Silver Kamuro", StarLayerCategory.Trail);
        ConfigureTail(silverKamuro, 210, 3.8f, 5.2f, 0.05f, 1.5f, 1.0f, new Color(0.9f, 0.95f, 1f));
        silverKamuro.gravityModifier = 0.4f;

        var ironSparks = MakeLayer(lib, "SL_IronSparks.asset", "iron_sparks", "Iron Sparks", StarLayerCategory.Sparkle);
        ConfigureSpark(ironSparks, 260, 6f, 2.2f, 0.03f, 0.12f, new Color(1f, 0.55f, 0.2f));
        ironSparks.noiseFrequency = 1.6f;

        var granite = MakeLayer(lib, "SL_Granite.asset", "granite", "Granite / Spreader", StarLayerCategory.Sparkle);
        ConfigureSpark(granite, 240, 5.0f, 2.6f, 0.045f, 0.18f, new Color(0.6f, 0.9f, 0.8f));
        granite.noiseFrequency = 2.2f;

        var glitter = MakeLayer(lib, "SL_Glitter.asset", "glitter", "Glitter", StarLayerCategory.Sparkle);
        ConfigureStrobe(glitter, 160, 3.5f, 2.0f, 0.045f, 12f, 18, new Color(1f, 0.95f, 0.8f));
        glitter.defaultStartDelay = 0.3f;

        var tremolant = MakeLayer(lib, "SL_Tremolant.asset", "tremolant", "Tremolant", StarLayerCategory.Sparkle);
        ConfigureStrobe(tremolant, 140, 2.8f, 3.2f, 0.045f, 8f, 20, new Color(1f, 0.95f, 0.85f));
        tremolant.defaultStartDelay = 0.4f;

        var flitter = MakeLayer(lib, "SL_Flitter.asset", "flitter", "Flitter / Spangle", StarLayerCategory.Sparkle);
        ConfigureSteady(flitter, 180, 6f, 2.4f, 0.045f, 1.1f, new Color(1f, 0.95f, 0.9f));

        var firefly = MakeLayer(lib, "SL_FireflyOverlay.asset", "firefly_overlay", "Firefly Overlay", StarLayerCategory.Sparkle);
        ConfigureStrobe(firefly, 80, 3f, 2.2f, 0.03f, 14f, 10, new Color(1f, 0.9f, 0.7f));
        firefly.defaultStartDelay = 0.15f;

        var strobe = MakeLayer(lib, "SL_Strobe.asset", "strobe", "Strobe", StarLayerCategory.Sparkle);
        ConfigureStrobe(strobe, 100, 6f, 2.4f, 0.06f, 8f, 25, new Color(0.95f, 0.95f, 1f));

        var crackle = MakeLayer(lib, "SL_Crackle.asset", "crackle", "Crackle", StarLayerCategory.Sparkle);
        ConfigureSpark(crackle, 160, 4.5f, 1.6f, 0.045f, 0.1f, new Color(1f, 0.85f, 0.6f));

        var crossette = MakeLayer(lib, "SL_Crossette.asset", "crossette_split", "Crossette Split", StarLayerCategory.Motion);
        ConfigureSpark(crossette, 160, 6.5f, 2.4f, 0.05f, 0.06f, new Color(1f, 1f, 1f));

        var fallingLeaves = MakeLayer(lib, "SL_FallingLeaves.asset", "falling_leaves", "Falling Leaves", StarLayerCategory.Motion);
        ConfigureSpark(fallingLeaves, 120, 2.2f, 4.5f, 0.06f, 0.14f, new Color(0.9f, 0.8f, 0.6f));
        fallingLeaves.gravityModifier = 0.15f;
        fallingLeaves.enableNoise = true;
        fallingLeaves.noiseStrength = 0.12f;
        fallingLeaves.noiseFrequency = 1.2f;

        var afterglow = MakeLayer(lib, "SL_Afterglow.asset", "afterglow", "Afterglow", StarLayerCategory.Terminal);
        ConfigureSteady(afterglow, 120, 0.2f, 2.8f, 0.05f, 0.28f, new Color(0.7f, 0.85f, 1f));
        afterglow.defaultStartDelay = 1.2f;

        var terminalFlash = MakeLayer(lib, "SL_TerminalFlash.asset", "terminal_flash", "Terminal Flash", StarLayerCategory.Terminal);
        ConfigurePrime(terminalFlash, 0.1f, 10, 0.02f);
        terminalFlash.defaultStartDelay = 1.2f;
        terminalFlash.defaultIntensity = 0.8f;
        terminalFlash.defaultColor = Color.white;

        var terminalSlowFade = MakeLayer(lib, "SL_TerminalSlowFade.asset", "terminal_slow_fade", "Terminal Slow-Fade", StarLayerCategory.Terminal);
        ConfigureSteady(terminalSlowFade, 120, 1.2f, 3.8f, 0.06f, 0.5f, new Color(0.95f, 0.9f, 0.85f));

        for (int i = 0; i < lib.layers.Count; i++)
        {
            var def = lib.layers[i];
            if (def == null) continue;
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssetIfDirty(def);
        }
    }

    static void EnsureDefaultPresets(StarLayerLibrary lib)
    {
        if (lib == null) return;
        if (lib.presets == null) lib.presets = new List<StarStackPreset>();

        var core = lib.FindById("core");
        var prime = lib.FindById("prime");
        var darkPrime = lib.FindById("dark_prime");
        var steadyHigh = lib.FindById("steady_color_high");
        var steadyLow = lib.FindById("steady_color_low");
        var illumination = lib.FindById("illumination");
        var chrysanthemum = lib.FindById("chrysanthemum_tail");
        var willow = lib.FindById("willow_tail");
        var wabi = lib.FindById("wabi_tail");
        var tiger = lib.FindById("tiger_tail");
        var brocade = lib.FindById("brocade_tail");
        var goldDust = lib.FindById("gold_dust");
        var silverLine = lib.FindById("silver_line");
        var silverKamuro = lib.FindById("silver_kamuro");
        var ironSparks = lib.FindById("iron_sparks");
        var granite = lib.FindById("granite");
        var glitter = lib.FindById("glitter");
        var tremolant = lib.FindById("tremolant");
        var flitter = lib.FindById("flitter");
        var firefly = lib.FindById("firefly_overlay");
        var strobe = lib.FindById("strobe");
        var crackle = lib.FindById("crackle");
        var crossette = lib.FindById("crossette_split");
        var fallingLeaves = lib.FindById("falling_leaves");
        var afterglow = lib.FindById("afterglow");
        var terminalFlash = lib.FindById("terminal_flash");
        var terminalSlowFade = lib.FindById("terminal_slow_fade");

        EnsurePreset(lib, "peony", "01 尾なし単色 (Peony)", "色差分はパラメータ。", core, prime,
            steadyHigh, terminalSlowFade);

        EnsurePreset(lib, "peony_dim", "02 尾なし渋色", "渋い定常発光。", core, prime,
            steadyLow, terminalSlowFade);

        EnsurePreset(lib, "flare", "03 照明星 (Flare)", "高輝度・長持続。", core, prime,
            illumination, terminalSlowFade);

        EnsurePreset(lib, "color_change_2", "04 変色 (2段)", "暗つなぎで切替を強調。", core, prime,
            steadyHigh, darkPrime, steadyHigh, terminalFlash);

        EnsurePreset(lib, "color_change_3", "05 変色 (3段)", "暗つなぎを2回。", core, prime,
            steadyHigh, darkPrime, steadyHigh, darkPrime, steadyHigh);

        EnsurePreset(lib, "tail_tip", "06 引先○○ (尾+先端色)", "尾タイプ+先端色。", core, prime,
            chrysanthemum, darkPrime, steadyHigh, terminalFlash);

        EnsurePreset(lib, "afterglow", "07 Afterglow (残光)", "消えそうで消えない残光。", core, prime,
            afterglow, terminalSlowFade);

        EnsurePreset(lib, "chrysanthemum", "08 菊引き", "放射状の引き尾。", core, prime,
            chrysanthemum);

        EnsurePreset(lib, "willow", "09 冠菊/柳引き", "垂れる尾 + 点滅粒。", core, prime,
            willow, firefly);

        EnsurePreset(lib, "wabi", "10 和火尾", "渋い炭火尾。", core, prime,
            wabi);

        EnsurePreset(lib, "tiger", "11 虎の尾", "太尾・煙感。", core, prime,
            tiger);

        EnsurePreset(lib, "brocade", "12 錦/ブロケード", "豪華な金尾 + きらめき。", core, prime,
            brocade, glitter);

        EnsurePreset(lib, "gold_dust", "13 砂金", "微細な金尾。", core, prime,
            goldDust);

        EnsurePreset(lib, "silver_line", "14 銀引", "鋭い銀尾。", core, prime,
            silverLine);

        EnsurePreset(lib, "silver_kamuro", "15 銀冠", "銀の垂れ尾。", core, prime,
            silverKamuro);

        EnsurePreset(lib, "iron_sparks", "16 鉄火/鉄粉", "枝分かれ火花。", core, prime,
            ironSparks);

        EnsurePreset(lib, "granite", "17 グラナイト", "荒い火花 + 煙感。", core, prime,
            granite);

        EnsurePreset(lib, "glitter", "18 グリッター", "遅れて瞬く粒。", core, prime,
            glitter, tremolant);

        EnsurePreset(lib, "flitter", "19 フリッター/スパンコール", "鋭いきらめき。", core, prime,
            flitter);

        EnsurePreset(lib, "strobe", "20 ストロボ", "周期的な明滅。", core, prime,
            strobe);

        EnsurePreset(lib, "crackle", "21 クラックル", "パチパチ破裂。", core, prime,
            crackle);

        EnsurePreset(lib, "crossette", "22 クロセット", "途中で分裂。", core, prime,
            steadyHigh, crossette);

        EnsurePreset(lib, "falling_leaves", "23 落葉", "ふわっと漂って落ちる。", core, prime,
            fallingLeaves, steadyLow);
    }

    static StarLayerDef MakeLayer(StarLayerLibrary lib, string assetName, string id, string displayName, StarLayerCategory category)
    {
        var def = GetOrCreateLayerDef(Path.Combine(LayerDataFolder, assetName), id, displayName, category);
        if (!lib.layers.Contains(def))
            lib.layers.Add(def);
        return def;
    }

    static StarLayerDef GetOrCreateLayerDef(string assetPath, string id, string displayName, StarLayerCategory category)
    {
        assetPath = assetPath.Replace("\\", "/");
        var def = AssetDatabase.LoadAssetAtPath<StarLayerDef>(assetPath);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<StarLayerDef>();
            AssetDatabase.CreateAsset(def, assetPath);
        }

        def.id = id;
        def.displayName = displayName;
        def.category = category;

        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssetIfDirty(def);
        return def;
    }

    static void ConfigureInvisible(StarLayerDef def)
    {
        if (def == null) return;
        def.usesColor = false;
        def.defaultIntensity = 0f;
        def.defaultStartDelay = 0f;
        def.defaultDuration = 0.1f;
        def.applyTemplate = true;
        def.maxParticles = 1;
        def.burstCount = 0;
        def.startSpeed = 0f;
        def.startLifetime = 0.1f;
        def.startSize = 0.01f;
        def.shapeRadius = 0f;
        def.gravityModifier = 0f;
        def.enableTrails = false;
        def.enableNoise = false;
        def.enableStrobe = false;
    }

    static void ConfigurePrime(StarLayerDef def, float life, int burstCount, float size)
    {
        if (def == null) return;
        def.usesColor = true;
        def.defaultIntensity = 1.2f;
        def.defaultStartDelay = 0f;
        def.defaultDuration = life;
        def.defaultColor = Color.white;
        def.applyTemplate = true;
        def.maxParticles = Mathf.Max(200, burstCount * 8);
        def.burstCount = burstCount;
        def.startSpeed = 0f;
        def.startLifetime = life;
        def.startSize = size;
        def.shapeRadius = 0.02f;
        def.gravityModifier = 0f;
        def.enableTrails = false;
        def.enableNoise = false;
        def.enableStrobe = false;
    }

    static void ConfigureSteady(StarLayerDef def, int burstCount, float speed, float life, float size, float intensity, Color color)
    {
        if (def == null) return;
        def.usesColor = true;
        def.defaultIntensity = intensity;
        def.defaultStartDelay = 0f;
        def.defaultDuration = 0f;
        def.defaultColor = color;
        def.applyTemplate = true;
        def.maxParticles = Mathf.Max(800, burstCount * 6);
        def.burstCount = burstCount;
        def.startSpeed = speed;
        def.startLifetime = life;
        def.startSize = size;
        def.shapeRadius = 0.02f;
        def.gravityModifier = 0f;
        def.enableTrails = false;
        def.enableNoise = false;
        def.enableStrobe = false;
    }

    static void ConfigureTail(StarLayerDef def, int burstCount, float speed, float life, float size, float trailLife, float intensity, Color color)
    {
        if (def == null) return;
        def.usesColor = true;
        def.defaultIntensity = intensity;
        def.defaultStartDelay = 0f;
        def.defaultDuration = 0f;
        def.defaultColor = color;
        def.applyTemplate = true;
        def.maxParticles = Mathf.Max(1000, burstCount * 6);
        def.burstCount = burstCount;
        def.startSpeed = speed;
        def.startLifetime = life;
        def.startSize = size;
        def.shapeRadius = 0.02f;
        def.gravityModifier = 0f;
        def.enableTrails = true;
        def.trailsLifetime = trailLife;
        def.enableNoise = false;
        def.enableStrobe = false;
    }

    static void ConfigureSpark(StarLayerDef def, int burstCount, float speed, float life, float size, float noiseStrength, Color color)
    {
        if (def == null) return;
        def.usesColor = true;
        def.defaultIntensity = 1f;
        def.defaultStartDelay = 0f;
        def.defaultDuration = 0f;
        def.defaultColor = color;
        def.applyTemplate = true;
        def.maxParticles = Mathf.Max(800, burstCount * 6);
        def.burstCount = burstCount;
        def.startSpeed = speed;
        def.startLifetime = life;
        def.startSize = size;
        def.shapeRadius = 0.02f;
        def.gravityModifier = 0f;
        def.enableTrails = false;
        def.enableNoise = true;
        def.noiseStrength = noiseStrength;
        def.noiseFrequency = 0.5f;
        def.enableStrobe = false;
    }

    static void ConfigureStrobe(StarLayerDef def, int burstCount, float speed, float life, float size, float strobeFreq, int strobeCount, Color color)
    {
        if (def == null) return;
        def.usesColor = true;
        def.defaultIntensity = 1f;
        def.defaultStartDelay = 0f;
        def.defaultDuration = 0f;
        def.defaultColor = color;
        def.applyTemplate = true;
        def.maxParticles = Mathf.Max(800, burstCount * 6);
        def.burstCount = burstCount;
        def.startSpeed = speed;
        def.startLifetime = life;
        def.startSize = size;
        def.shapeRadius = 0.02f;
        def.gravityModifier = 0f;
        def.enableTrails = false;
        def.enableNoise = false;
        def.enableStrobe = true;
        def.strobeFrequency = strobeFreq;
        def.strobeBurstCount = strobeCount;
    }

    static void EnsurePreset(StarLayerLibrary lib, string id, string displayName, string memo, StarLayerDef core, StarLayerDef prime, params StarLayerDef[] layers)
    {
        if (lib == null) return;
        if (lib.presets == null) lib.presets = new List<StarStackPreset>();

        StarStackPreset preset = null;
        for (int i = 0; i < lib.presets.Count; i++)
        {
            if (lib.presets[i] != null && lib.presets[i].id == id)
            {
                preset = lib.presets[i];
                break;
            }
        }

        if (preset == null)
        {
            preset = new StarStackPreset();
            lib.presets.Add(preset);
        }

        preset.id = id;
        preset.displayName = displayName;
        preset.memo = memo;
        preset.core = core;
        preset.prime = prime;
        preset.layers = new List<StarLayerDef>();
        if (layers != null)
            preset.layers.AddRange(layers);
    }
}
