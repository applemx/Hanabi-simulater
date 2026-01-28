using UnityEngine;

public class FireworkController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] ParticleSystem particleSystemRenderer;
    [SerializeField] Transform launchOrigin;          // Launcherを刺す（推奨）
    [SerializeField] float fallbackForwardDistance = 150f;

    [Header("Test (Make it absolutely visible)")]
    [SerializeField] KeyCode launchKey = KeyCode.F;

    [Tooltip("打ち上げを省略して、即爆発する（見えるか確認用）")]
    [SerializeField] bool instantExplode = true;

    [Tooltip("爆発の高さ（instant時）")]
    [SerializeField] float testExplodeHeight = 120f;

    [Tooltip("爆発位置を launchOrigin から少し前に出す")]
    [SerializeField] float originForwardOffset = 0f;

    [Header("Particles (Giant)")]
    [SerializeField] int maxParticles = 60000;
    [SerializeField] int burstCount = 8000;
    [SerializeField] float burstSpeed = 220f;
    [SerializeField] float burstSpeedJitter = 80f;
    [SerializeField] float starLifetime = 4.0f;
    [SerializeField] float starSize = 2.0f;   // ← 馬鹿でかい
    [SerializeField] float starDrag = 0.02f;

    [Header("Flash Light (Giant)")]
    [SerializeField] bool enableFlash = true;
    [SerializeField] float flashIntensity = 80000f;
    [SerializeField] float flashRange = 300f;
    [SerializeField] float flashDuration = 0.15f;

    [Header("World")]
    [SerializeField] Vector3 wind = new Vector3(0.2f, 0f, 0.05f);
    [SerializeField] float gravity = -9.8f;

    struct Star
    {
        public Vector3 pos;
        public Vector3 vel;
        public float age;
        public float life;
        public Color32 color;
        public float size;
        public bool alive;
    }

    ParticleSystem.Particle[] psBuffer;
    Star[] stars;
    int starCount;

    Light flashLight;
    float flashT;

    ParticleSystem.MainModule main;

    void Awake()
    {
        if (particleSystemRenderer == null)
        {
            Debug.LogError("[FireworkController] ParticleSystem is not assigned.");
            enabled = false;
            return;
        }

        main = particleSystemRenderer.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(256, maxParticles);

        psBuffer = new ParticleSystem.Particle[main.maxParticles];
        stars = new Star[main.maxParticles];

        particleSystemRenderer.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleSystemRenderer.Clear(true);

        // フラッシュライトを自動生成（見えない問題の切り分け用）
        if (enableFlash)
        {
            var go = new GameObject("FireworkFlashLight");
            go.transform.SetParent(transform, false);
            flashLight = go.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.intensity = 0f;
            flashLight.range = flashRange;
            flashLight.enabled = true;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(launchKey))
        {
            Vector3 p = GetOriginPosition();
            if (instantExplode)
            {
                p.y = testExplodeHeight;
                Explode(p);
            }
            else
            {
                // （必要なら後で打ち上げモードも戻す）
                p.y = testExplodeHeight;
                Explode(p);
            }
        }

        Sim(Time.deltaTime);
        RenderParticles();

        UpdateFlash(Time.deltaTime);
    }

    Vector3 GetOriginPosition()
    {
        if (launchOrigin != null)
        {
            return launchOrigin.position + launchOrigin.forward * originForwardOffset;
        }
        return transform.position + transform.forward * fallbackForwardDistance;
    }

    void Explode(Vector3 center)
    {
        Debug.Log($"[FireworkController] Explode at {center}");

        // 既存クリア
        starCount = 0;
        particleSystemRenderer.Clear(true);

        // フラッシュ
        if (enableFlash && flashLight != null)
        {
            flashLight.transform.position = center;
            flashLight.intensity = flashIntensity;
            flashT = flashDuration;
        }

        int count = Mathf.Min(burstCount, stars.Length);
        starCount = count;

        // 眩しい白（見える確認用）
        Color32 col = new Color32(255, 255, 255, 255);

        var rng = new System.Random(Time.frameCount * 1337);

        for (int i = 0; i < count; i++)
        {
            Vector3 dir = RandomUnitVector(rng);

            float spd = burstSpeed + (float)(rng.NextDouble() * 2.0 - 1.0) * burstSpeedJitter;
            if (spd < 1f) spd = 1f;

            float life = starLifetime * (0.8f + 0.4f * (float)rng.NextDouble());

            stars[i] = new Star
            {
                pos = center,
                vel = dir * spd,
                age = 0f,
                life = life,
                color = col,
                size = starSize,
                alive = true
            };
        }
    }

    void Sim(float dt)
    {
        for (int i = 0; i < starCount; i++)
        {
            if (!stars[i].alive) continue;

            Star s = stars[i];
            s.age += dt;

            if (s.age >= s.life)
            {
                s.alive = false;
                stars[i] = s;
                continue;
            }

            s.vel += (new Vector3(wind.x, 0f, wind.z) + Vector3.up * gravity) * dt;
            s.vel *= Mathf.Clamp01(1f - starDrag * dt);
            s.pos += s.vel * dt;

            stars[i] = s;
        }
    }

    void RenderParticles()
    {
        int n = 0;

        for (int i = 0; i < starCount && n < psBuffer.Length; i++)
        {
            if (!stars[i].alive) continue;

            Star s = stars[i];

            float t = s.age / Mathf.Max(0.0001f, s.life);
            byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * (1f - t)), 0, 255);

            Color32 c = s.color;
            c.a = a;

            psBuffer[n].position = s.pos;
            psBuffer[n].startColor = c;
            psBuffer[n].startSize = s.size;  // 超巨大
            psBuffer[n].remainingLifetime = 999f;
            psBuffer[n].startLifetime = 999f;
            psBuffer[n].velocity = Vector3.zero;
            n++;
        }

        particleSystemRenderer.SetParticles(psBuffer, n);
    }

    void UpdateFlash(float dt)
    {
        if (!enableFlash || flashLight == null) return;

        if (flashT > 0f)
        {
            flashT -= dt;
            if (flashT <= 0f) flashLight.intensity = 0f;
        }
    }

    static Vector3 RandomUnitVector(System.Random rng)
    {
        // 均一球面
        float u = (float)rng.NextDouble();
        float v = (float)rng.NextDouble();
        float theta = 2f * Mathf.PI * u;
        float z = 2f * v - 1f;
        float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
        return new Vector3(r * Mathf.Cos(theta), r * Mathf.Sin(theta), z).normalized;
    }
}
