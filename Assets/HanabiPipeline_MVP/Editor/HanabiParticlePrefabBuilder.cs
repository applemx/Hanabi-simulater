using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class HanabiParticlePrefabBuilder
{
    const string FolderRoot = "Assets/Prefabs";
    const string ParticleFolder = "Assets/Prefabs/Particles";
    const string MaterialFolder = "Assets/Materials";
    const string TextureFolder = "Assets/Textures";
    const string MaterialPath = "Assets/Materials/HanabiParticle_Additive.mat";
    const string TexturePath = "Assets/Textures/HanabiSoftCircle.asset";
    const int MaxParticles = 30000;
    const int SoftTextureSize = 64;
    static readonly string[] StarKinds =
    {
        "Solid",
        "Tail",
        "ColorChange",
        "Comet",
        "Strobe",
        "Glitter",
        "Crackle",
        "Crossette"
    };

    static Material cachedMaterial;
    static Texture2D cachedTexture;

    [MenuItem("Hanabi/Create Star Particle Prefabs")]
    public static void CreateStarParticlePrefabs()
    {
        CreatePrefabs(placeInScene: false);
    }

    [MenuItem("Hanabi/Create Star Particle Prefabs And Place In Scene")]
    public static void CreateStarParticlePrefabsAndPlace()
    {
        CreatePrefabs(placeInScene: true);
    }

    [MenuItem("Hanabi/Assign Star Particle Systems In Scene")]
    public static void AssignStarParticleSystemsInScene()
    {
        var systems = FindSceneParticleSystems();
        AssignToPlaybackControllers(systems);
    }

    [MenuItem("Hanabi/Setup Particle Look (All)")]
    public static void SetupParticleLookAll()
    {
        EnsureFolders();
        var tex = GetOrCreateSoftTexture(forceRebuild: true);
        ApplyTextureSettings(tex);
        var mat = GetOrCreateParticleMaterial(forceShaderReset: true);
        ApplyMaterialSettings(mat);

        var systems = FindSceneParticleSystems();
        foreach (var ps in systems)
        {
            if (ps == null) continue;
            ApplyRendererSettings(ps);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Hanabi] Particle look settings applied.");
    }

    [MenuItem("Hanabi/Rebuild Soft Circle Texture")]
    public static void RebuildSoftCircleTextureMenu()
    {
        EnsureFolders();
        var tex = GetOrCreateSoftTexture(forceRebuild: true);
        ApplyTextureSettings(tex);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Hanabi] Soft circle texture rebuilt.");
    }

    static void CreatePrefabs(bool placeInScene)
    {
        EnsureFolders();

        float spacing = 1.5f;
        var placed = new ParticleSystem[StarKinds.Length];
        for (int i = 0; i < StarKinds.Length; i++)
        {
            string kind = StarKinds[i];
            var go = BuildParticleObject($"FireworkParticles_{kind}");
            string path = $"{ParticleFolder}/{go.name}.prefab";

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            if (placeInScene && prefab != null)
            {
                var existing = GameObject.Find(prefab.name);
                var inst = existing != null ? existing : (PrefabUtility.InstantiatePrefab(prefab) as GameObject);
                if (inst != null)
                {
                    if (existing == null)
                        inst.transform.position = new Vector3(i * spacing, 0f, 0f);
                    var ps = inst.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        ApplyRendererSettings(ps);
                        placed[i] = ps;
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Hanabi] Star particle prefabs created.");

        if (placeInScene)
            AssignToPlaybackControllers(placed);
    }

    static GameObject BuildParticleObject(string name)
    {
        var go = new GameObject(name);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = false;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.startLifetime = 999f;
        main.maxParticles = MaxParticles;

        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        ApplyRendererSettings(ps);

        return go;
    }

    static void ApplyRendererSettings(ParticleSystem ps)
    {
        if (ps == null) return;
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) return;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.mesh = null;
        renderer.enableGPUInstancing = true;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.sharedMaterial = GetOrCreateParticleMaterial();
        renderer.minParticleSize = 0.01f;
        renderer.maxParticleSize = 0.25f;
        ApplyDefaultVertexStreams(renderer);
    }

    static ParticleSystem[] FindSceneParticleSystems()
    {
        var result = new ParticleSystem[StarKinds.Length];
        for (int i = 0; i < StarKinds.Length; i++)
        {
            string name = $"FireworkParticles_{StarKinds[i]}";
            var go = GameObject.Find(name);
            if (go == null) continue;
            result[i] = go.GetComponent<ParticleSystem>();
        }
        return result;
    }

    static void AssignToPlaybackControllers(ParticleSystem[] systems)
    {
        HanabiPlaybackController[] controllers;
#if UNITY_2023_1_OR_NEWER
        controllers = Object.FindObjectsByType<HanabiPlaybackController>(FindObjectsSortMode.None);
#else
        controllers = Object.FindObjectsOfType<HanabiPlaybackController>();
#endif
        if (controllers == null || controllers.Length == 0)
        {
            Debug.LogWarning("[Hanabi] No HanabiPlaybackController found in scene.");
            return;
        }

        foreach (var controller in controllers)
        {
            if (controller == null) continue;
            var so = new SerializedObject(controller);

            var arr = so.FindProperty("starParticleSystems");
            if (arr != null)
            {
                arr.arraySize = StarKinds.Length;
                for (int i = 0; i < StarKinds.Length; i++)
                {
                    var sys = (systems != null && i < systems.Length) ? systems[i] : null;
                    if (sys != null) ApplyRendererSettings(sys);
                    var el = arr.GetArrayElementAtIndex(i);
                    el.objectReferenceValue = sys;
                }
            }

            var useMulti = so.FindProperty("useMultiParticleSystems");
            if (useMulti != null) useMulti.boolValue = true;

            var autoFind = so.FindProperty("autoFindStarRenderers");
            if (autoFind != null) autoFind.boolValue = false;

            var prefix = so.FindProperty("starRendererPrefix");
            if (prefix != null) prefix.stringValue = "FireworkParticles_";

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
        }

        Debug.Log("[Hanabi] Assigned star particle systems to HanabiPlaybackController.");
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(FolderRoot))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(ParticleFolder))
            AssetDatabase.CreateFolder(FolderRoot, "Particles");
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(TextureFolder))
            AssetDatabase.CreateFolder("Assets", "Textures");
    }

    static Shader FindParticleShader()
    {
        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader != null) return shader;
        shader = Shader.Find("Universal Render Pipeline/Particles/Additive");
        if (shader != null) return shader;
        shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null) return shader;
        return Shader.Find("Sprites/Default");
    }

    static Material GetOrCreateParticleMaterial(bool forceShaderReset = false)
    {
        if (cachedMaterial != null) return cachedMaterial;

        EnsureFolders();
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            var shader = FindParticleShader();
            if (shader == null)
                return null;
            mat = new Material(shader);
            ApplyColor(mat, new Color(1.6f, 1.6f, 1.6f, 1f));
            var tex = GetOrCreateSoftTexture();
            ApplyTexture(mat, tex);
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
        }
        else
        {
            if (forceShaderReset)
            {
                var shader = FindParticleShader();
                if (shader != null) mat.shader = shader;
            }
            var tex = GetOrCreateSoftTexture();
            ApplyTexture(mat, tex);
        }

        ApplyMaterialSettings(mat);
        cachedMaterial = mat;
        return mat;
    }

    static Texture2D GetOrCreateSoftTexture(bool forceRebuild = false)
    {
        if (cachedTexture != null) return cachedTexture;

        EnsureFolders();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (tex == null)
        {
            tex = BuildSoftCircleTexture();
            AssetDatabase.CreateAsset(tex, TexturePath);
            AssetDatabase.SaveAssets();
        }
        else if (forceRebuild)
        {
            RebuildSoftCircleTexture(tex);
        }

        cachedTexture = tex;
        return tex;
    }

    static void ApplyColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
        else if (mat.HasProperty("_TintColor"))
        {
            mat.SetColor("_TintColor", color);
        }
        else
        {
            mat.color = color;
        }
    }

    static void ApplyTexture(Material mat, Texture2D tex)
    {
        if (mat == null || tex == null) return;
        if (mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", tex);
        else if (mat.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", tex);
        else if (mat.HasProperty("_BaseColorMap"))
            mat.SetTexture("_BaseColorMap", tex);
    }

    static void ApplyTextureSettings(Texture2D tex)
    {
        if (tex == null) return;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.anisoLevel = 1;
        EditorUtility.SetDirty(tex);
    }

    static void ApplyMaterialSettings(Material mat)
    {
        if (mat == null) return;
        mat.enableInstancing = true;
        ApplyColor(mat, new Color(1.6f, 1.6f, 1.6f, 1f));
        // Force additive blending for glow-like look.
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
    }

    static Texture2D BuildSoftCircleTexture()
    {
        var tex = new Texture2D(SoftTextureSize, SoftTextureSize, TextureFormat.RGBA32, false, true);
        tex.name = "HanabiSoftCircle";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.anisoLevel = 1;
        RebuildSoftCircleTexture(tex);
        return tex;
    }

    static void RebuildSoftCircleTexture(Texture2D tex)
    {
        if (tex == null) return;
        float center = (SoftTextureSize - 1) * 0.5f;
        float inv = 1f / center;
        for (int y = 0; y < SoftTextureSize; y++)
        {
            for (int x = 0; x < SoftTextureSize; x++)
            {
                float dx = (x - center) * inv;
                float dy = (y - center) * inv;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = Mathf.SmoothStep(0f, 1f, a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        EditorUtility.SetDirty(tex);
    }

    static void ApplyDefaultVertexStreams(ParticleSystemRenderer renderer)
    {
        if (renderer == null) return;
        var streams = new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.AnimBlend,
            ParticleSystemVertexStream.Center,
            ParticleSystemVertexStream.SizeXY
        };
        renderer.SetActiveVertexStreams(streams);
        renderer.enableGPUInstancing = true;
    }
}
