using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Hanabi/Star Layers/Layer Library", fileName = "StarLayerLibrary_Default")]
public class StarLayerLibrary : ScriptableObject
{
    public List<StarLayerDef> layers = new List<StarLayerDef>();
    public List<StarStackPreset> presets = new List<StarStackPreset>();

    public int Count => layers == null ? 0 : layers.Count;

    public StarLayerDef Get(int index)
    {
        if (layers == null || index < 0 || index >= layers.Count)
            return null;
        return layers[index];
    }

    public StarLayerDef FindById(string id)
    {
        if (layers == null || string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < layers.Count; i++)
        {
            var def = layers[i];
            if (def != null && def.id == id) return def;
        }
        return null;
    }

    public StarStackPreset FindPresetById(string id)
    {
        if (presets == null || string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            if (preset != null && preset.id == id) return preset;
        }
        return null;
    }
}
