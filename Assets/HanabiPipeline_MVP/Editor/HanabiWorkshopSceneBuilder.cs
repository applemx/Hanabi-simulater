using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HanabiWorkshopSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Workshop.unity";

    [MenuItem("Hanabi/Scenes/Create Workshop Scene")]
    public static void CreateWorkshopScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Root
        var root = new GameObject("Workshop");

        // Hemisphere
        var hemi = new GameObject("Hemisphere");
        hemi.transform.SetParent(root.transform);
        hemi.transform.position = Vector3.zero;
        hemi.AddComponent<HanabiHemisphereMesh>();

        // Camera
        var camObj = new GameObject("WorkshopCamera");
        camObj.transform.SetParent(root.transform);
        var cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
        var orbit = camObj.AddComponent<HanabiWorkshopCameraOrbit>();
        orbit.SetTarget(hemi.transform);
        camObj.AddComponent<AudioListener>();

        // Preview camera (oblique)
        var previewCamObj = new GameObject("WorkshopPreviewCamera");
        previewCamObj.transform.SetParent(root.transform);
        var previewCam = previewCamObj.AddComponent<Camera>();
        previewCam.clearFlags = CameraClearFlags.Depth;
        previewCam.depth = cam.depth + 1f;
        previewCam.rect = new Rect(0.70f, 0.04f, 0.28f, 0.28f);
        var previewOrbit = previewCamObj.AddComponent<HanabiWorkshopCameraOrbit>();
        previewOrbit.SetTarget(hemi.transform);
        previewOrbit.Configure(inputEnabled: false, topDownEnabled: false, useFixedAngles: true, fixedYaw: -35f, fixedPitch: 35f, distanceValue: 2.6f, applyViewport: true, viewport: previewCam.rect);

        // Light
        var lightObj = new GameObject("WorkshopLight");
        lightObj.transform.SetParent(root.transform);
        var light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.8f;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var fillObj = new GameObject("WorkshopFillLight");
        fillObj.transform.SetParent(root.transform);
        var fill = fillObj.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.intensity = 1.1f;
        fill.range = 6f;
        fillObj.transform.position = new Vector3(0f, 1.6f, 1.2f);

        // Star preview particle system
        var previewObj = new GameObject("StarPreview");
        previewObj.transform.SetParent(root.transform);
        var ps = previewObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 999f;
        main.startSpeed = 0f;
        main.startSize = 0.02f;
        main.maxParticles = 10000;
        var emission = ps.emission;
        emission.enabled = false;
        var psRenderer = previewObj.GetComponent<ParticleSystemRenderer>();
        var sphereMesh = GetSphereMesh();
        if (sphereMesh != null)
        {
            psRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            psRenderer.mesh = sphereMesh;
            psRenderer.alignment = ParticleSystemRenderSpace.World;
            psRenderer.enableGPUInstancing = true;
        }
        var psShader = FindParticleShader();
        if (psShader != null)
        {
            var mat = new Material(psShader);
            ApplyColor(mat, new Color(0.85f, 0.9f, 1f, 0.7f));
            psRenderer.sharedMaterial = mat;
        }

        // Waruyaku preview particle system
        var waruObj = new GameObject("WaruyakuPreview");
        waruObj.transform.SetParent(root.transform);
        var waruPs = waruObj.AddComponent<ParticleSystem>();
        var waruMain = waruPs.main;
        waruMain.loop = false;
        waruMain.playOnAwake = false;
        waruMain.startLifetime = 999f;
        waruMain.startSpeed = 0f;
        waruMain.startSize = 0.03f;
        waruMain.maxParticles = 20000;
        var waruEmission = waruPs.emission;
        waruEmission.enabled = false;
        var waruRenderer = waruObj.GetComponent<ParticleSystemRenderer>();
        if (sphereMesh != null)
        {
            waruRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            waruRenderer.mesh = sphereMesh;
            waruRenderer.alignment = ParticleSystemRenderSpace.World;
            waruRenderer.enableGPUInstancing = true;
        }
        if (psShader != null)
        {
            var waruMat = new Material(psShader);
            ApplyColor(waruMat, new Color(1f, 1f, 1f, 0.8f));
            waruRenderer.sharedMaterial = waruMat;
        }

        // Controller
        var controllerObj = new GameObject("WorkshopController");
        controllerObj.transform.SetParent(root.transform);
        var controller = controllerObj.AddComponent<HanabiWorkshopController>();

        // Assign references
        // Auto-assign a blueprint if one is selected
        var bp = Selection.activeObject as FireworkBlueprint;
        if (bp == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:FireworkBlueprint");
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                bp = AssetDatabase.LoadAssetAtPath<FireworkBlueprint>(path);
            }
        }
        var db = HanabiDatabaseEditorUtil.GetOrCreateDefault();
        controller.SetReferences(bp, db, hemi.transform, hemi.GetComponent<Collider>(), cam, ps, waruPs);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(ScenePath);
        Debug.Log($"[Hanabi] Workshop scene created at {ScenePath}");
    }

    static Shader FindParticleShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader != null) return shader;
        shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null) return shader;
        shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null) return shader;
        shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader != null) return shader;
        return Shader.Find("Sprites/Default");
    }

    static Mesh GetSphereMesh()
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var mf = temp.GetComponent<MeshFilter>();
        var mesh = mf != null ? mf.sharedMesh : null;
        Object.DestroyImmediate(temp);
        return mesh;
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
}
