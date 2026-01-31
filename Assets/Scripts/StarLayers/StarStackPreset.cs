using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StarStackPreset
{
    public string id = "preset_id";
    public string displayName = "Preset";
    [TextArea(2, 3)]
    public string memo;
    public StarLayerDef core;
    public StarLayerDef prime;
    public List<StarLayerDef> layers = new List<StarLayerDef>();
}