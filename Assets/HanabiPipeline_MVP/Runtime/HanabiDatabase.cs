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

    Dictionary<string, int> _starTagToId;
    Dictionary<string, int> _paletteTagToId;

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

        if (starProfiles.Count == 0)
        {
            // MVP initial 8
            starProfiles.Add(StarProfileDef.Make("Solid", StarKind.Solid, baseSize: 6.0f, sizeJitter: 1.5f, baseLife: 2.6f));
            starProfiles.Add(StarProfileDef.Make("Tail", StarKind.Tail, baseSize: 8.0f, sizeJitter: 2.0f, baseLife: 2.8f));
            starProfiles.Add(StarProfileDef.Make("ColorChange", StarKind.ColorChange, baseSize: 7.0f, sizeJitter: 1.8f, baseLife: 2.8f));
            starProfiles.Add(StarProfileDef.Make("Comet", StarKind.Comet, baseSize: 10.0f, sizeJitter: 2.5f, baseLife: 3.2f));
            starProfiles.Add(StarProfileDef.Make("Strobe", StarKind.Strobe, baseSize: 8.0f, sizeJitter: 2.0f, baseLife: 2.4f));
            starProfiles.Add(StarProfileDef.Make("Glitter", StarKind.Glitter, baseSize: 8.5f, sizeJitter: 2.2f, baseLife: 3.0f));
            starProfiles.Add(StarProfileDef.Make("Crackle", StarKind.Crackle, baseSize: 7.0f, sizeJitter: 1.8f, baseLife: 2.3f));
            starProfiles.Add(StarProfileDef.Make("Crossette", StarKind.Crossette, baseSize: 7.5f, sizeJitter: 1.9f, baseLife: 2.6f));
        }

        if (palettes.Count == 0)
        {
            palettes.Add(new PaletteDef
            {
                tag = "Palette_Default",
                colors = new List<Color32>
                {
                    new Color32(255, 210, 106, 255),
                    new Color32(255, 107, 107, 255),
                    new Color32(85, 193, 255, 255),
                    new Color32(182, 106, 255, 255),
                }
            });
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
    public float baseSize = 2.2f;
    public float sizeJitter = 0.4f;

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
