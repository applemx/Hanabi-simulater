using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HanabiStarPreviewSceneBuilder
{
    const string ScenePath = "Assets/Scenes/StarPreview.unity";
    const string DatabasePath = "Assets/Data/HanabiDatabase_Default.asset";
    const string ParticlePrefabFolder = "Assets/Prefabs/Particles";

    [MenuItem("Hanabi/Create Star Preview Scene")]
    public static void CreateStarPreviewScene()
    {
        EnsureFolders();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var root = new GameObject("StarPreview");
        var controller = root.AddComponent<StarPreviewController>();
        controller.gameObject.transform.position = Vector3.zero;

        // Camera
        var camGo = new GameObject("StarPreviewCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.05f, 0.08f, 1f);
        cam.transform.position = new Vector3(0f, 0f, -12f);
        cam.transform.rotation = Quaternion.identity;
        cam.fieldOfView = 45f;
        cam.tag = "MainCamera";

        // Light (optional for UI readability)
        var lightGo = new GameObject("StarPreviewLight");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 0.6f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Database
        var db = AssetDatabase.LoadAssetAtPath<HanabiDatabase>(DatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<HanabiDatabase>();
            db.EnsureDefaultsIfEmpty();
            AssetDatabase.CreateAsset(db, DatabasePath);
            AssetDatabase.SaveAssets();
        }
        controller.GetType().GetField("profileDatabase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(controller, db);

        // Star particle systems
        var particlesRoot = new GameObject("StarParticles");
        particlesRoot.transform.SetParent(root.transform);

        var kinds = System.Enum.GetNames(typeof(StarKind));
        var systems = new ParticleSystem[kinds.Length];
        for (int i = 0; i < kinds.Length; i++)
        {
            string prefabPath = $"{ParticlePrefabFolder}/FireworkParticles_{kinds[i]}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Hanabi] Missing prefab: {prefabPath}");
                continue;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, particlesRoot.transform);
            inst.name = prefab.name;
            inst.transform.localPosition = Vector3.zero;
            var ps = inst.GetComponent<ParticleSystem>();
            systems[i] = ps;
        }

        controller.GetType().GetField("starParticleSystems", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(controller, systems);

        // Save scene
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log($"[Hanabi] Star Preview scene created at {ScenePath}");
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
    }
}
