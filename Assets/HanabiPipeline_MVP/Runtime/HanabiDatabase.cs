using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Hanabi/Database", fileName = "HanabiDatabase_Default")]
public class HanabiDatabase : ScriptableObject
{
    [Header("Star Profiles (tag -> behavior/visual)")]
    public List<StarProfileDef> starProfiles = new List<StarProfileDef>();

    [Header("Palettes (tag -> colors)")]
    public List<PaletteDef> palettes = new List<PaletteDef>();

    [Header("Waruyaku (tag -> ignition scatter)")]
    public List<WaruyakuDef> waruyakuDefs = new List<WaruyakuDef>();

    [Header("Washi (tag -> delay/collimation)")]
    public List<WashiDef> washiDefs = new List<WashiDef>();

    [Header("Fuse (tag -> burn speed)")]
    public List<FuseDef> fuseDefs = new List<FuseDef>();

    [Header("Launch Profiles (tag -> launch params)")]
    public List<LaunchProfileDef> launchProfiles = new List<LaunchProfileDef>();

    Dictionary<string, int> _starTagToId;
    Dictionary<string, int> _paletteTagToId;
    Dictionary<string, int> _waruyakuTagToId;
    Dictionary<string, int> _washiTagToId;
    Dictionary<string, int> _fuseTagToId;
    Dictionary<string, int> _launchTagToId;

    public void BuildCaches()
    {
        _starTagToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < starProfiles.Count; i++)
        {
            var t = starProfiles[i]?.tag;
            if (string.IsNullOrWhiteSpace(t)) continue;
            if (!_starTagToId.ContainsKey(t)) _starTagToId.Add(t, i);
        }

        _paletteTagToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < palettes.Count; i++)
        {
            var t = palettes[i]?.tag;
            if (string.IsNullOrWhiteSpace(t)) continue;
            if (!_paletteTagToId.ContainsKey(t)) _paletteTagToId.Add(t, i);
        }

        _waruyakuTagToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < waruyakuDefs.Count; i++)
        {
            var t = waruyakuDefs[i]?.tag;
            if (string.IsNullOrWhiteSpace(t)) continue;
            if (!_waruyakuTagToId.ContainsKey(t)) _waruyakuTagToId.Add(t, i);
        }

        _washiTagToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < washiDefs.Count; i++)
        {
            var t = washiDefs[i]?.tag;
            if (string.IsNullOrWhiteSpace(t)) continue;
            if (!_washiTagToId.ContainsKey(t)) _washiTagToId.Add(t, i);
        }

        _fuseTagToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < fuseDefs.Count; i++)
        {
            var t = fuseDefs[i]?.tag;
            if (string.IsNullOrWhiteSpace(t)) continue;
            if (!_fuseTagToId.ContainsKey(t)) _fuseTagToId.Add(t, i);
        }

        _launchTagToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < launchProfiles.Count; i++)
        {
            var t = launchProfiles[i]?.tag;
            if (string.IsNullOrWhiteSpace(t)) continue;
            if (!_launchTagToId.ContainsKey(t)) _launchTagToId.Add(t, i);
        }
    }

    public bool TryGetStarId(string tag, out int id)
    {
        if (_starTagToId == null) BuildCaches();
        if (string.IsNullOrWhiteSpace(tag))
        {
            id = 0;
            return starProfiles.Count > 0;
        }
        return _starTagToId.TryGetValue(tag, out id);
    }

    public StarProfileDef GetStarById(int id)
    {
        if (id < 0 || id >= starProfiles.Count) return null;
        return starProfiles[id];
    }

    public bool TryGetPalette(string tag, out List<Color32> colors)
    {
        if (_paletteTagToId == null) BuildCaches();
        colors = null;

        if (string.IsNullOrWhiteSpace(tag))
        {
            if (palettes.Count == 0) return false;
            colors = palettes[0].colors;
            return colors != null && colors.Count > 0;
        }

        if (!_paletteTagToId.TryGetValue(tag, out int id)) return false;
        colors = palettes[id].colors;
        return colors != null && colors.Count > 0;
    }

    public bool TryGetWaruyakuId(string tag, out int id)
    {
        if (_waruyakuTagToId == null) BuildCaches();
        if (string.IsNullOrWhiteSpace(tag))
        {
            id = 0;
            return waruyakuDefs.Count > 0;
        }
        return _waruyakuTagToId.TryGetValue(tag, out id);
    }

    public WaruyakuDef GetWaruyakuById(int id)
    {
        if (id < 0 || id >= waruyakuDefs.Count) return null;
        return waruyakuDefs[id];
    }

    public bool TryGetWashiId(string tag, out int id)
    {
        if (_washiTagToId == null) BuildCaches();
        if (string.IsNullOrWhiteSpace(tag))
        {
            id = 0;
            return washiDefs.Count > 0;
        }
        return _washiTagToId.TryGetValue(tag, out id);
    }

    public WashiDef GetWashiById(int id)
    {
        if (id < 0 || id >= washiDefs.Count) return null;
        return washiDefs[id];
    }

    public bool TryGetFuseId(string tag, out int id)
    {
        if (_fuseTagToId == null) BuildCaches();
        if (string.IsNullOrWhiteSpace(tag))
        {
            id = 0;
            return fuseDefs.Count > 0;
        }
        return _fuseTagToId.TryGetValue(tag, out id);
    }

    public FuseDef GetFuseById(int id)
    {
        if (id < 0 || id >= fuseDefs.Count) return null;
        return fuseDefs[id];
    }

    public bool TryGetLaunchId(string tag, out int id)
    {
        if (_launchTagToId == null) BuildCaches();
        if (string.IsNullOrWhiteSpace(tag))
        {
            id = 0;
            return launchProfiles.Count > 0;
        }
        return _launchTagToId.TryGetValue(tag, out id);
    }

    public LaunchProfileDef GetLaunchById(int id)
    {
        if (id < 0 || id >= launchProfiles.Count) return null;
        return launchProfiles[id];
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureDefaultsIfEmpty();
        BuildCaches();
    }
#endif

    public void EnsureDefaultsIfEmpty()
    {
        if (starProfiles == null) starProfiles = new List<StarProfileDef>();
        if (palettes == null) palettes = new List<PaletteDef>();
        if (waruyakuDefs == null) waruyakuDefs = new List<WaruyakuDef>();
        if (washiDefs == null) washiDefs = new List<WashiDef>();
        if (fuseDefs == null) fuseDefs = new List<FuseDef>();
        if (launchProfiles == null) launchProfiles = new List<LaunchProfileDef>();

        if (starProfiles.Count == 0)
        {
            // MVP initial 8 (smaller sizes to avoid heavy overdraw)
            var solid = StarProfileDef.Make("Solid", StarKind.Solid, baseSize: 0.06f, sizeJitter: 0.02f, baseLife: 2.6f);
            solid.baseSpeed = 40f;
            starProfiles.Add(solid);
            starProfiles.Add(StarProfileDef.Make("Tail", StarKind.Tail, baseSize: 0.08f, sizeJitter: 0.03f, baseLife: 2.8f));
            starProfiles.Add(StarProfileDef.Make("ColorChange", StarKind.ColorChange, baseSize: 0.07f, sizeJitter: 0.025f, baseLife: 2.8f));
            starProfiles.Add(StarProfileDef.Make("Comet", StarKind.Comet, baseSize: 0.10f, sizeJitter: 0.035f, baseLife: 3.2f));
            starProfiles.Add(StarProfileDef.Make("Strobe", StarKind.Strobe, baseSize: 0.08f, sizeJitter: 0.03f, baseLife: 2.4f));
            starProfiles.Add(StarProfileDef.Make("Glitter", StarKind.Glitter, baseSize: 0.09f, sizeJitter: 0.03f, baseLife: 3.0f));
            starProfiles.Add(StarProfileDef.Make("Crackle", StarKind.Crackle, baseSize: 0.07f, sizeJitter: 0.025f, baseLife: 2.3f));
            starProfiles.Add(StarProfileDef.Make("Crossette", StarKind.Crossette, baseSize: 0.075f, sizeJitter: 0.03f, baseLife: 2.6f));
        }

        if (palettes.Count == 0)
        {
            palettes.Add(new PaletteDef
            {
                tag = "Palette_Default",
                colors = new List<Color32>
                {
                    new Color32(200, 230, 255, 255),
                    new Color32(110, 180, 255, 255),
                    new Color32(255, 255, 255, 255),
                }
            });
        }

        if (waruyakuDefs.Count == 0)
        {
            waruyakuDefs.Add(new WaruyakuDef { tag = "Waruyaku_L", igniteCostMultiplier = 0.9f, scatterStrength = 1.15f, uniformity = 0.9f });
            waruyakuDefs.Add(new WaruyakuDef { tag = "Waruyaku_M", igniteCostMultiplier = 1.0f, scatterStrength = 1.0f, uniformity = 0.85f });
            waruyakuDefs.Add(new WaruyakuDef { tag = "Waruyaku_H", igniteCostMultiplier = 1.15f, scatterStrength = 0.85f, uniformity = 0.8f });
        }

        if (washiDefs.Count == 0)
        {
            washiDefs.Add(new WashiDef { tag = "Washi_Default", delaySeconds = 0.05f, collimation = 0.35f });
        }

        if (fuseDefs.Count == 0)
        {
            fuseDefs.Add(new FuseDef { tag = "Fuse_Default", burnSeconds = 3.5f, burnSpeed = 1.0f, igniteCost = 0.02f, jitter = 0.0f });
            fuseDefs.Add(new FuseDef { tag = "Fuse_Fast", burnSeconds = 2.8f, burnSpeed = 2.0f, igniteCost = 0.01f, jitter = 0.0f });
            fuseDefs.Add(new FuseDef { tag = "Fuse_Slow", burnSeconds = 4.2f, burnSpeed = 0.6f, igniteCost = 0.04f, jitter = 0.0f });
        }

        if (launchProfiles.Count == 0)
        {
            launchProfiles.Add(new LaunchProfileDef { tag = "Launch_Default", launchSpeed = 70f, gravityScale = 1.0f, windScale = 0.2f, dragScale = 0.2f });
            launchProfiles.Add(new LaunchProfileDef { tag = "Launch_Low", launchSpeed = 60f, gravityScale = 1.0f, windScale = 0.2f, dragScale = 0.25f });
            launchProfiles.Add(new LaunchProfileDef { tag = "Launch_High", launchSpeed = 78f, gravityScale = 1.0f, windScale = 0.2f, dragScale = 0.18f });
        }
    }
}

public enum StarKind
{
    Solid = 0,
    Tail = 1,
    ColorChange = 2,
    Comet = 3,
    Strobe = 4,
    Glitter = 5,
    Crackle = 6,
    Crossette = 7,
}

[Serializable]
public class StarProfileDef
{
    public string tag = "Solid";
    public StarKind kind = StarKind.Solid;

    [Header("Physics-ish")]
    public float baseSpeed = 18f;
    public float speedJitter = 2.5f;
    public float baseLife = 2.6f;
    public float lifeJitter = 0.3f;
    public float drag = 0.35f;

    [Header("Visual")]
    public float baseSize = 0.06f;
    public float sizeJitter = 0.02f;

    /// <summary>0..2 (MVP: used to scale alpha only; true HDR comes later via material/Bloom).</summary>
    [Range(0f, 2f)]
    public float brightness = 1.0f;

    public static StarProfileDef Make(string tag, StarKind kind, float baseSize, float sizeJitter, float baseLife)
    {
        return new StarProfileDef
        {
            tag = tag,
            kind = kind,
            baseSize = baseSize,
            sizeJitter = sizeJitter,
            baseLife = baseLife,
        };
    }
}

[Serializable]
public class PaletteDef
{
    public string tag = "Palette_Default";
    public List<Color32> colors = new List<Color32>();
}

[Serializable]
public class WaruyakuDef
{
    public string tag = "Waruyaku_M";
    public float scatterStrength = 1.0f;
    public float uniformity = 0.85f;
    public float igniteCostMultiplier = 1.0f;
}

[Serializable]
public class WashiDef
{
    public string tag = "Washi_Default";
    public float delaySeconds = 0.05f;
    [Range(0f, 1f)]
    public float collimation = 0.35f;
}

[Serializable]
public class FuseDef
{
    public string tag = "Fuse_Default";
    public float burnSeconds = 3.5f;
    public float burnSpeed = 1.0f;
    public float igniteCost = 0.02f;
    public float jitter = 0.0f;
}

[Serializable]
public class LaunchProfileDef
{
    public string tag = "Launch_Default";
    public float launchSpeed = 70f;
    public float gravityScale = 1.0f;
    public float windScale = 0.2f;
    public float dragScale = 0.2f;
}
