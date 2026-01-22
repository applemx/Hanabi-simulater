using System;
using UnityEngine;

/// <summary>
/// Lightweight voxel container used at compile-time.
/// Arrays are sized res^3 and indexed by x + res*(y + res*z).
/// </summary>
public class PackedVolume
{
    public int res;
    public float cellSize;

    /// <summary>0..255. 0 means empty / non-traversable.</summary>
    public byte[] charge;

    /// <summary>0/1.</summary>
    public byte[] starMask;

    /// <summary>Palette index (0..).</summary>
    public byte[] starColor;

    /// <summary>0 means no paper wall, otherwise wallId.</summary>
    public byte[] paperCellWallId;

    /// <summary>0..255 (paper strength for delay/collimation etc.).</summary>
    public byte[] paperStrength;

    public PackedVolume(int res)
    {
        this.res = res;
        cellSize = 2.0f / res;
        int n = res * res * res;
        charge = new byte[n];
        starMask = new byte[n];
        starColor = new byte[n];
        paperCellWallId = new byte[n];
        paperStrength = new byte[n];
    }

    public int Index(int x, int y, int z)
    {
        return x + res * (y + res * z);
    }

    public bool InBounds(int x, int y, int z)
    {
        return (uint)x < (uint)res && (uint)y < (uint)res && (uint)z < (uint)res;
    }

    public Vector3 CellCenter(int x, int y, int z)
    {
        float fx = -1f + (x + 0.5f) * cellSize;
        float fy = -1f + (y + 0.5f) * cellSize;
        float fz = -1f + (z + 0.5f) * cellSize;
        return new Vector3(fx, fy, fz);
    }

    public void XYZ(int idx, out int x, out int y, out int z)
    {
        int r2 = res * res;
        z = idx / r2;
        int rem = idx - z * r2;
        y = rem / res;
        x = rem - y * res;
    }

    public int Count => res * res * res;
}
