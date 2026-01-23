using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class CompiledShowSerializer
{
    // Simple binary format (little-endian):
    // [magic 4] 'HBS2'
    // [version i32]
    // [seed u32]
    // [burstCount i32]
    // [particleCount i32]
    // [launchSpeed f32]
    // [fuseSeconds f32]
    // [gravityScale f32]
    // [windScale f32]
    // [dragScale f32]
    // bursts: time(f32), pos(xyz f32), eventFx(i32), particleCount(i32), particleStart(i32)
    // particles: pos(xyz f32), vel(xyz f32), life(f32), size(f32), color(rgba u8), delay(f32), profile(u16), seed(u32)

    const string MAGIC_V1 = "HBS1";
    const string MAGIC_V2 = "HBS2";

    public static byte[] Write(uint showSeed, BurstEvent[] bursts, ParticleInitV2[] particles, LaunchParams launchParams, int version = 2)
    {
        using var ms = new MemoryStream(1024);
        using var bw = new BinaryWriter(ms);

        bw.Write(Encoding.ASCII.GetBytes(MAGIC_V2));
        bw.Write(version);
        bw.Write(showSeed);
        bw.Write(bursts.Length);
        bw.Write(particles.Length);
        bw.Write(launchParams.launchSpeed);
        bw.Write(launchParams.fuseSeconds);
        bw.Write(launchParams.gravityScale);
        bw.Write(launchParams.windScale);
        bw.Write(launchParams.dragScale);

        for (int i = 0; i < bursts.Length; i++)
        {
            bw.Write(bursts[i].timeLocal);
            bw.Write(bursts[i].posLocal.x);
            bw.Write(bursts[i].posLocal.y);
            bw.Write(bursts[i].posLocal.z);
            bw.Write(bursts[i].eventFxId);
            bw.Write(bursts[i].particleCount);
            bw.Write(bursts[i].particleStartIndex);
        }

        for (int i = 0; i < particles.Length; i++)
        {
            var p = particles[i];
            bw.Write(p.pos0Local.x); bw.Write(p.pos0Local.y); bw.Write(p.pos0Local.z);
            bw.Write(p.vel0Local.x); bw.Write(p.vel0Local.y); bw.Write(p.vel0Local.z);
            bw.Write(p.life);
            bw.Write(p.size);
            bw.Write(p.color.r); bw.Write(p.color.g); bw.Write(p.color.b); bw.Write(p.color.a);
            bw.Write(p.spawnDelay);
            bw.Write(p.profileId);
            bw.Write(p.seed);
        }

        bw.Flush();
        return ms.ToArray();
    }

    public static bool TryRead(byte[] blob, out uint showSeed, out BurstEvent[] bursts, out ParticleInitV2[] particles, out LaunchParams launchParams, out int version)
    {
        showSeed = 0;
        bursts = Array.Empty<BurstEvent>();
        particles = Array.Empty<ParticleInitV2>();
        launchParams = new LaunchParams
        {
            launchSpeed = 0f,
            fuseSeconds = 0f,
            gravityScale = 1f,
            windScale = 1f,
            dragScale = 0f
        };
        version = 0;

        if (blob == null || blob.Length < 16) return false;

        try
        {
            using var ms = new MemoryStream(blob);
            using var br = new BinaryReader(ms);

            var magicBytes = br.ReadBytes(4);
            var magic = Encoding.ASCII.GetString(magicBytes);
            if (magic != MAGIC_V1 && magic != MAGIC_V2) return false;

            version = br.ReadInt32();
            showSeed = br.ReadUInt32();
            int burstCount = br.ReadInt32();
            int particleCount = br.ReadInt32();

            if (magic == MAGIC_V2)
            {
                launchParams.launchSpeed = br.ReadSingle();
                launchParams.fuseSeconds = br.ReadSingle();
                launchParams.gravityScale = br.ReadSingle();
                launchParams.windScale = br.ReadSingle();
                launchParams.dragScale = br.ReadSingle();
            }

            bursts = new BurstEvent[burstCount];
            for (int i = 0; i < burstCount; i++)
            {
                bursts[i].timeLocal = br.ReadSingle();
                float x = br.ReadSingle();
                float y = br.ReadSingle();
                float z = br.ReadSingle();
                bursts[i].posLocal = new Vector3(x, y, z);
                bursts[i].eventFxId = br.ReadInt32();
                bursts[i].particleCount = br.ReadInt32();
                bursts[i].particleStartIndex = br.ReadInt32();
            }

            particles = new ParticleInitV2[particleCount];
            for (int i = 0; i < particleCount; i++)
            {
                float px = br.ReadSingle(); float py = br.ReadSingle(); float pz = br.ReadSingle();
                float vx = br.ReadSingle(); float vy = br.ReadSingle(); float vz = br.ReadSingle();
                float life = br.ReadSingle();
                float size = br.ReadSingle();
                byte r = br.ReadByte(); byte g = br.ReadByte(); byte b = br.ReadByte(); byte a = br.ReadByte();
                float delay = br.ReadSingle();
                ushort profile = br.ReadUInt16();
                uint seed = br.ReadUInt32();

                particles[i] = new ParticleInitV2
                {
                    pos0Local = new Vector3(px, py, pz),
                    vel0Local = new Vector3(vx, vy, vz),
                    life = life,
                    size = size,
                    color = new Color32(r, g, b, a),
                    spawnDelay = delay,
                    profileId = profile,
                    seed = seed
                };
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
