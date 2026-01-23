using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Hanabi/FireworkBlueprint", fileName = "BP_Firework")]
public class FireworkBlueprint : ScriptableObject
{
    [Header("Meta")]
    public int version = 2;
    public string displayName = "sample";
    public int seed = 12345;

    [Header("Shell")]
    public ShellSize shellSize = ShellSize.Small;

    // Temporary numeric id. Later this should become a tag that resolves to StarProfile in DB.

    [Header("Tags (preferred)")]
    public string starProfileTag = "Solid";
    public string paletteTag = "Palette_Default";
    public string waruyakuTag = "Waruyaku_M";
    public string washiTag = "Washi_Default";
[Header("Palette")]
    public List<Color32> palette = new List<Color32>
    {
        new Color32(255, 210, 106, 255),
        new Color32(255, 107, 107, 255),
        new Color32(85, 193, 255, 255)
    };

    [Header("Intent (temporary knobs)")]
    public IntentParams intent = new IntentParams
    {
        baseSpeed = 18f,
        uniformity = 0.85f,
        jitter = 0.12f,
        life = 2.4f,
        drag = 0.35f,
        wind = 0.15f
    };

    [Header("Star skeleton (MVP: ring)")]
    public StarSkeletonRing ring = new StarSkeletonRing
    {
        count = 1500,
        radius = 0.85f,
        thickness = 0.10f
    };

    [Header("Paper (MVP: disc walls)")]
    public List<PaperPrimitive> paper = new List<PaperPrimitive>();

    [Header("Ignition (compile-time only)")]
    public IgnitionParams ignition = new IgnitionParams
    {
        secondsPerVoxel = 0.02f,
        burstBinSize = 0.05f,
        maxIgnitionTime = 6.0f,
        paperExtraDelayPerCell = 0.01f
    };

    // MUST: multiple ignition points (shell-local coordinates).
    // The compiler will ensure at least one entry exists.
    public List<IgniterSpec> igniters = new List<IgniterSpec> { IgniterSpec.Default };

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (igniters == null) igniters = new List<IgniterSpec>();
        if (igniters.Count == 0) igniters.Add(IgniterSpec.Default);
        if (ignition.burstBinSize <= 0f) ignition.burstBinSize = 0.05f;
        if (ignition.secondsPerVoxel <= 0f) ignition.secondsPerVoxel = 0.02f;
        if (ignition.maxIgnitionTime <= 0f) ignition.maxIgnitionTime = 6.0f;
    }
#endif
}

public enum ShellSize { Small, Medium, Large }

[Serializable]
public struct IntentParams
{
    public float baseSpeed;
    public float uniformity;
    public float jitter;
    public float life;
    public float drag;
    public float wind;
}

[Serializable]
public struct StarSkeletonRing
{
    public int count;
    public float radius;
    public float thickness;
}

public enum PaperShape { Disc }

[Serializable]
public struct PaperPrimitive
{
    public PaperShape shape;
    public Vector3 center;
    public Vector3 normal;
    public float radius;
    public byte wallId;
    public byte strength; // 0..255
}

[Serializable]
public struct IgnitionParams
{
    [Tooltip("Base time (seconds) to propagate across one voxel at charge=255.")]
    public float secondsPerVoxel;

    [Tooltip("Burst bin size (seconds). Cells ignited within the same bin become one BurstEvent.")]
    public float burstBinSize;

    [Tooltip("Clamp ignition solving to this time (seconds). Cells beyond are ignored for burst generation.")]
    public float maxIgnitionTime;

    [Tooltip("Extra delay added per step when entering a voxel marked as paper wall.")]
    public float paperExtraDelayPerCell;
}

[Serializable]
public struct IgniterSpec
{
    [Tooltip("Shell-local [-1..1] coordinates. (0,0,0)=center.")]
    public Vector3 posLocal;

    [Tooltip("Fuse tag (resolves in DB later). For now this is metadata only.")]
    public string fuseTag;

    [Tooltip("Start delay (seconds) added to this igniter's ignition source time.")]
    public float startDelay;

    public static IgniterSpec Default => new IgniterSpec
    {
        posLocal = Vector3.zero,
        fuseTag = "Fuse_Default",
        startDelay = 0f
    };
}
