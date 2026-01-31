using UnityEngine;

[CreateAssetMenu(menuName = "Hanabi/Star Layers/Layer", fileName = "SL_")]
public class StarLayerDef : ScriptableObject
{
    public string id = "layer_id";
    public string displayName = "Layer";
    public StarLayerCategory category = StarLayerCategory.Other;
    public GameObject prefab;

    [Header("Defaults")]
    public float defaultStartDelay = 0f;
    public float defaultDuration = 0f;
    public Color defaultColor = Color.white;
    public float defaultSizeScale = 1f;
    public float defaultLifetimeScale = 1f;
    public float defaultIntensity = 1f;
    public bool usesColor = true;

    [Header("Particle Template")]
    public bool applyTemplate = true;
    public int maxParticles = 2000;
    public int burstCount = 200;
    public float burstTime = 0f;
    public float startSpeed = 8f;
    public float startLifetime = 2.5f;
    public float startSize = 0.08f;
    public float shapeRadius = 0.02f;
    public float gravityModifier = 0f;
    public bool enableTrails = false;
    public float trailsLifetime = 0.6f;
    public bool enableNoise = false;
    public float noiseStrength = 0.3f;
    public float noiseFrequency = 0.5f;
    public bool enableStrobe = false;
    public float strobeFrequency = 10f;
    public int strobeBurstCount = 40;
}
