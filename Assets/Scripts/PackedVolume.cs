using UnityEngine;

public class PackedVolume
{
    public readonly int res;
    public readonly float cellSize;

    public readonly byte[] charge;        // 0..255
    public readonly byte[] starMask;      // 0/1
    public readonly byte[] starColor;     // palette index
    public readonly byte[] paperCellWallId; // 0=‚È‚µ, >0=•Çidi’´MVPj

    public PackedVolume(int res)
    {
        this.res = res;
        cellSize = 2.0f / res;
        int n = res * res * res;

        charge = new byte[n];
        starMask = new byte[n];
        starColor = new byte[n];
        paperCellWallId = new byte[n];
    }

    public int Index(int x, int y, int z) => x + res * (y + res * z);

    public bool InBounds(int x, int y, int z)
        => (uint)x < (uint)res && (uint)y < (uint)res && (uint)z < (uint)res;

    public Vector3 CellCenter(int x, int y, int z)
    {
        float fx = -1f + (x + 0.5f) * cellSize;
        float fy = -1f + (y + 0.5f) * cellSize;
        float fz = -1f + (z + 0.5f) * cellSize;
        return new Vector3(fx, fy, fz);
    }
}
