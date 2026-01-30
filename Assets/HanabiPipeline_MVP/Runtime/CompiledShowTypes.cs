using System;
using UnityEngine;

// Runtime-only types for playback.
// CompiledShow is stored in CompiledShowAsset as a binary blob.

[Serializable]
public struct CompiledShowHeader
{
    public int version;
    public uint showSeed;
    public int burstEventCount;
}

[Serializable]
public struct BurstEvent
{
    public float timeLocal;     // seconds since shell t0 (MVP uses 0)
    public Vector3 posLocal;    // shell-local
    public int eventFxId;       // optional, -1 = none
    public int particleCount;
    public int particleStartIndex; // index into ParticleInit array
}

[Serializable]
public struct ParticleInitV2
{
    public const byte FlagSmoke = 1;
    public Vector3 pos0Local;
    public Vector3 vel0Local;
    public float life;
    public float size;
    public Color32 color;
    public float spawnDelay;
    public ushort profileId; // StarProfile id (future-proof)
    public uint seed;
    public byte flags;
}

[Serializable]
public struct LaunchParams
{
    public float launchSpeed;
    public float fuseSeconds;
    public float gravityScale;
    public float windScale;
    public float dragScale;
}
