using System;
using UnityEngine;

[Serializable]
public class StarLayerSlot
{
    public bool enabled = true;
    public StarLayerDef layer;
    public float startDelay = 0f;
    public float durationOverride = 0f;
    public bool overrideColor = false;
    public Color color = Color.white;
    public float intensityScale = 1f;
    public float sizeScale = 1f;
    public float lifetimeScale = 1f;
}