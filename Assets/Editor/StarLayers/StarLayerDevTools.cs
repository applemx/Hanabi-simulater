using UnityEditor;
using UnityEngine;

public static class StarLayerDevTools
{
    const string ChrysanthemumPrefabPath = "Assets/Prefabs/StarLayers/13_ChrysanthemumTail/PS_ChrysanthemumTail.prefab";
    const string ChrysanthemumMatPath = "Assets/Prefabs/StarLayers/13_ChrysanthemumTail/MAT_ChrysanthemumTail.mat";
    const string ChrysanthemumEmberMatPath = "Assets/Prefabs/StarLayers/13_ChrysanthemumTail/MAT_ChrysanthemumTail_Ember.mat";
    const string ChrysanthemumSparkMatPath = "Assets/Prefabs/StarLayers/13_ChrysanthemumTail/MAT_ChrysanthemumTail_Spark.mat";
    const string EmberChildName = "PS_ChrysanthemumTail_Ember";
    const string SparkChildName = "PS_ChrysanthemumTail_Spark";

    [MenuItem("Hanabi/Dev/StarLayers/Add Chrysanthemum Ember SubPS")]
    public static void AddChrysanthemumEmberSubPs()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChrysanthemumPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Prefab not found: {ChrysanthemumPrefabPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(ChrysanthemumPrefabPath);
        try
        {
            var existing = root.transform.Find(EmberChildName);
            if (existing != null)
            {
                Debug.LogWarning("Ember sub PS already exists. Skipping.");
                return;
            }

            var ember = new GameObject(EmberChildName);
            ember.transform.SetParent(root.transform, false);

            var ps = ember.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 1.6f;
            main.startSpeed = 0.6f;
            main.startSize = 0.015f;
            main.startColor = new Color(0.42f, 0.22f, 0.14f, 1f);

            var emission = ps.emission;
            emission.enabled = true;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 50)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;

            var trails = ps.trails;
            trails.enabled = false;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.06f;
            noise.frequency = 0.9f;

            var renderer = ember.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.minParticleSize = 0.01f;
            renderer.maxParticleSize = 0.05f;
            renderer.material = GetOrCreateEmberMaterial();

            PrefabUtility.SaveAsPrefabAsset(root, ChrysanthemumPrefabPath);
            Debug.Log("Added ember sub PS to Chrysanthemum tail prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Hanabi/Dev/StarLayers/Tune Chrysanthemum Ember Boost")]
    public static void TuneChrysanthemumEmberBoost()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChrysanthemumPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Prefab not found: {ChrysanthemumPrefabPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(ChrysanthemumPrefabPath);
        try
        {
            var ember = root.transform.Find(EmberChildName);
            if (ember == null)
            {
                Debug.LogError("Ember sub PS not found. Run 'Add Chrysanthemum Ember SubPS' first.");
                return;
            }

            var ps = ember.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                Debug.LogError("Ember sub PS missing ParticleSystem component.");
                return;
            }

            var main = ps.main;
            main.startLifetime = 2.6f;
            main.startSpeed = 0.7f;
            main.startSize = 0.012f;
            main.startColor = new Color(0.4f, 0.2f, 0.12f, 1f);

            var emission = ps.emission;
            emission.enabled = true;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 160)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.025f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 1.0f;

            var renderer = ember.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.minParticleSize = 0.008f;
            renderer.maxParticleSize = 0.04f;
            renderer.material = GetOrCreateEmberMaterial();

            PrefabUtility.SaveAsPrefabAsset(root, ChrysanthemumPrefabPath);
            Debug.Log("Tuned chrysanthemum ember sub PS (boost).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Material GetOrCreateEmberMaterial()
    {
        var emberMat = AssetDatabase.LoadAssetAtPath<Material>(ChrysanthemumEmberMatPath);
        if (emberMat != null)
            return emberMat;

        var baseMat = AssetDatabase.LoadAssetAtPath<Material>(ChrysanthemumMatPath);
        if (baseMat == null)
        {
            Debug.LogError($"Base material not found: {ChrysanthemumMatPath}");
            return null;
        }

        emberMat = Object.Instantiate(baseMat);
        emberMat.name = "MAT_ChrysanthemumTail_Ember";
        emberMat.SetColor("_Color", new Color(0.6f, 0.3f, 0.18f, 1f));
        emberMat.SetColor("_TintColor", new Color(0.6f, 0.3f, 0.18f, 1f));
        AssetDatabase.CreateAsset(emberMat, ChrysanthemumEmberMatPath);
        AssetDatabase.SaveAssets();
        return emberMat;
    }

    [MenuItem("Hanabi/Dev/StarLayers/Add Chrysanthemum Spark SubPS")]
    public static void AddChrysanthemumSparkSubPs()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChrysanthemumPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Prefab not found: {ChrysanthemumPrefabPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(ChrysanthemumPrefabPath);
        try
        {
            var existing = root.transform.Find(SparkChildName);
            if (existing != null)
            {
                Debug.LogWarning("Spark sub PS already exists. Skipping.");
                return;
            }

            var spark = new GameObject(SparkChildName);
            spark.transform.SetParent(root.transform, false);

            var ps = spark.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = 1.2f;
            main.startSpeed = 1.2f;
            main.startSize = 0.008f;
            main.startColor = new Color(0.75f, 0.35f, 0.12f, 1f);

            var emission = ps.emission;
            emission.enabled = true;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 180)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 1.1f;

            var renderer = spark.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.minParticleSize = 0.004f;
            renderer.maxParticleSize = 0.03f;
            renderer.material = GetOrCreateSparkMaterial();

            PrefabUtility.SaveAsPrefabAsset(root, ChrysanthemumPrefabPath);
            Debug.Log("Added spark sub PS to Chrysanthemum tail prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Hanabi/Dev/StarLayers/Tune Chrysanthemum Spark Boost")]
    public static void TuneChrysanthemumSparkBoost()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChrysanthemumPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Prefab not found: {ChrysanthemumPrefabPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(ChrysanthemumPrefabPath);
        try
        {
            var spark = root.transform.Find(SparkChildName);
            if (spark == null)
            {
                Debug.LogError("Spark sub PS not found. Run 'Add Chrysanthemum Spark SubPS' first.");
                return;
            }

            var ps = spark.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                Debug.LogError("Spark sub PS missing ParticleSystem component.");
                return;
            }

            var main = ps.main;
            main.startLifetime = 1.5f;
            main.startSpeed = 1.4f;
            main.startSize = 0.007f;
            main.startColor = new Color(0.75f, 0.35f, 0.12f, 1f);

            var emission = ps.emission;
            emission.enabled = true;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 220)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.025f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.06f;
            noise.frequency = 1.2f;

            var renderer = spark.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.minParticleSize = 0.003f;
            renderer.maxParticleSize = 0.028f;
            renderer.material = GetOrCreateSparkMaterial();

            PrefabUtility.SaveAsPrefabAsset(root, ChrysanthemumPrefabPath);
            Debug.Log("Tuned chrysanthemum spark sub PS (boost).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Material GetOrCreateSparkMaterial()
    {
        var sparkMat = AssetDatabase.LoadAssetAtPath<Material>(ChrysanthemumSparkMatPath);
        if (sparkMat != null)
            return sparkMat;

        var baseMat = AssetDatabase.LoadAssetAtPath<Material>(ChrysanthemumMatPath);
        if (baseMat == null)
        {
            Debug.LogError($"Base material not found: {ChrysanthemumMatPath}");
            return null;
        }

        sparkMat = Object.Instantiate(baseMat);
        sparkMat.name = "MAT_ChrysanthemumTail_Spark";
        sparkMat.SetColor("_Color", new Color(0.75f, 0.35f, 0.12f, 1f));
        sparkMat.SetColor("_TintColor", new Color(0.75f, 0.35f, 0.12f, 1f));
        AssetDatabase.CreateAsset(sparkMat, ChrysanthemumSparkMatPath);
        AssetDatabase.SaveAssets();
        return sparkMat;
    }
}
