using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StarLayerWorkshopSceneBuilder
{
    const string ScenePath = "Assets/Scenes/StarLayerWorkshop.unity";

    [MenuItem("Hanabi/Scenes/Create Star Layer Workshop Scene")]
    public static void CreateStarLayerWorkshopScene()
    {
        EnsureFolders();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var root = new GameObject("StarLayerWorkshop");
        var controller = root.AddComponent<StarLayerWorkshopController>();

        var layerRoot = new GameObject("StarLayerRoot");
        layerRoot.transform.SetParent(root.transform);
        layerRoot.transform.localPosition = Vector3.zero;

        // Camera
        var camGo = new GameObject("StarLayerCamera");
        camGo.transform.SetParent(root.transform);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.05f, 0.08f, 1f);
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.transform.rotation = Quaternion.identity;
        cam.fieldOfView = 45f;
        cam.tag = "MainCamera";
        camGo.AddComponent<AudioListener>();

        // Light
        var lightGo = new GameObject("StarLayerLight");
        lightGo.transform.SetParent(root.transform);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 0.6f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Library
        var lib = StarLayerLibraryEditorUtil.GetOrCreateDefault();

        SetPrivateField(controller, "library", lib);
        SetPrivateField(controller, "layerRoot", layerRoot.transform);
        SetPrivateField(controller, "previewCamera", cam);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(ScenePath);
        Debug.Log($"[StarLayers] Workshop scene created at {ScenePath}");
    }

    static void EnsureFolders()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        Directory.CreateDirectory("Assets/Prefabs/StarLayers");
        Directory.CreateDirectory("Assets/Data/StarLayers");
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null) return;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var field = target.GetType().GetField(fieldName, flags);
        field?.SetValue(target, value);
    }
}
