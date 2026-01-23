using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class HanabiWorkshopController : MonoBehaviour
{
    public enum EditMode
    {
        Star = 0,
        Waruyaku = 1,
        Washi = 2,
        Fuse = 3
    }

    public enum WaruyakuStrength
    {
        Low,
        Medium,
        High
    }

    [Header("Refs")]
    [SerializeField] FireworkBlueprint targetBlueprint;
    [SerializeField] HanabiDatabase database;
    [SerializeField] Transform hemisphereRoot;
    [SerializeField] Collider hemisphereCollider;
    [SerializeField] Camera viewCamera;
    [SerializeField] ParticleSystem starPreview;
    [SerializeField] ParticleSystem waruyakuPreview;

    [Header("Edit Mode")]
    [SerializeField] EditMode mode = EditMode.Star;
    [SerializeField] int symmetry = 8;

    [Header("Star Placement")]
    [SerializeField, Range(0.1f, 1.0f)] float starRadius = 0.85f;
    [SerializeField] byte starSize = 2;
    [SerializeField] int scatterPerStamp = 4;
    [SerializeField, Range(0.01f, 0.4f)] float brushRadius = 0.08f;
    [SerializeField] string starTag = "Solid";

    [Header("Waruyaku Paint")]
    [SerializeField, Range(0.1f, 1.0f)] float waruyakuRadius = 0.65f;
    [SerializeField] WaruyakuStrength waruyakuStrength = WaruyakuStrength.Medium;

    [Header("Input")]
    [SerializeField] float paintSpacing = 0.03f;
    [SerializeField] KeyCode starModeKey = KeyCode.Alpha1;
    [SerializeField] KeyCode waruyakuModeKey = KeyCode.Alpha2;

    readonly Stack<EditAction> undo = new Stack<EditAction>();
    readonly Stack<EditAction> redo = new Stack<EditAction>();

    bool painting;
    Vector3 lastPaintLocal;
    bool previewDirty;
    ParticleSystem.Particle[] previewBuffer;
    ParticleSystem.Particle[] waruyakuBuffer;

    void Awake()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (hemisphereCollider == null && hemisphereRoot != null)
            hemisphereCollider = hemisphereRoot.GetComponent<Collider>();
        if (hemisphereRoot == null && hemisphereCollider != null)
            hemisphereRoot = hemisphereCollider.transform;

        if (starPreview == null)
            starPreview = FindPreviewByName("StarPreview") ?? FindObjectOfType<ParticleSystem>();
        if (waruyakuPreview == null)
            waruyakuPreview = FindPreviewByName("WaruyakuPreview");

        EnsurePreviewSetup();
        SyncTagFromDatabase();
        MarkPreviewDirty();
    }

    public void SetReferences(FireworkBlueprint bp, HanabiDatabase db, Transform hemiRoot, Collider hemiCollider, Camera cam, ParticleSystem preview, ParticleSystem waruyakuPreviewSystem)
    {
        targetBlueprint = bp;
        database = db;
        hemisphereRoot = hemiRoot;
        hemisphereCollider = hemiCollider;
        viewCamera = cam;
        starPreview = preview;
        waruyakuPreview = waruyakuPreviewSystem;
        EnsurePreviewSetup();
        SyncTagFromDatabase();
        MarkPreviewDirty();
    }

    void Update()
    {
        if (targetBlueprint == null || viewCamera == null || hemisphereCollider == null) return;

        HandleShortcuts();

        if (Input.GetMouseButtonDown(0))
        {
            painting = true;
            TryPaintAtMouse(true);
        }
        else if (Input.GetMouseButton(0) && painting)
        {
            TryPaintAtMouse(false);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            painting = false;
        }

        if (previewDirty)
            UpdatePreviews();
    }

    void HandleShortcuts()
    {
        if (Input.GetKeyDown(starModeKey)) mode = EditMode.Star;
        if (Input.GetKeyDown(waruyakuModeKey)) mode = EditMode.Waruyaku;

        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Z))
            Undo();
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Y))
            Redo();
    }

    void TryPaintAtMouse(bool first)
    {
        if (!TryGetHitLocal(out Vector3 hitLocal)) return;
        if (!first && Vector3.Distance(hitLocal, lastPaintLocal) < paintSpacing) return;
        lastPaintLocal = hitLocal;

        switch (mode)
        {
            case EditMode.Star:
                AddStarsFromBrush(hitLocal);
                break;
            case EditMode.Waruyaku:
                AddWaruyakuStroke(hitLocal);
                break;
        }
    }

    bool TryGetHitLocal(out Vector3 hitLocal)
    {
        hitLocal = Vector3.zero;
        Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);
        if (hemisphereCollider.Raycast(ray, out RaycastHit hit, 100f))
        {
            hitLocal = hemisphereRoot.InverseTransformPoint(hit.point);
            return hitLocal.y <= 0f;
        }
        return false;
    }

    void AddStarsFromBrush(Vector3 hitLocal)
    {
        if (targetBlueprint.stars == null) targetBlueprint.stars = new List<StarPoint>();

        var added = new List<StarPoint>(scatterPerStamp * Mathf.Max(1, symmetry));
        Vector3 dir = hitLocal.sqrMagnitude > 1e-6f ? hitLocal.normalized : Vector3.up;
        OrthonormalBasis(dir, out Vector3 t1, out Vector3 t2);

        int count = Mathf.Max(1, scatterPerStamp);
        for (int i = 0; i < count; i++)
        {
            Vector2 d = RandomInsideCircle() * brushRadius;
            Vector3 dVec = (dir + t1 * d.x + t2 * d.y).normalized;
            AddStarWithSymmetry(dVec, added);
        }

        if (added.Count > 0)
        {
            targetBlueprint.stars.AddRange(added);
            PushUndo(EditMode.Star, added);
            MarkDirty();
            MarkPreviewDirty();
        }
    }

    void AddStarWithSymmetry(Vector3 dir, List<StarPoint> added)
    {
        int n = Mathf.Max(1, symmetry);
        float step = 360f / n;
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Quaternion.AngleAxis(step * i, Vector3.up) * dir;
            added.Add(new StarPoint
            {
                dir = d,
                radius = Mathf.Clamp01(starRadius),
                tag = starTag,
                size = starSize
            });
        }
    }

    void AddWaruyakuStroke(Vector3 hitLocal)
    {
        if (targetBlueprint.waruyakuStrokes == null) targetBlueprint.waruyakuStrokes = new List<WaruyakuStroke>();

        var added = new List<WaruyakuStroke>(Mathf.Max(1, symmetry));
        Vector3 dir = hitLocal.sqrMagnitude > 1e-6f ? hitLocal.normalized : Vector3.up;

        int n = Mathf.Max(1, symmetry);
        float step = 360f / n;
        byte strength = StrengthToByte(waruyakuStrength);
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Quaternion.AngleAxis(step * i, Vector3.up) * dir;
            added.Add(new WaruyakuStroke
            {
                dir = d,
                radius = Mathf.Clamp01(waruyakuRadius),
                brushRadius = brushRadius,
                strength = strength
            });
        }

        targetBlueprint.waruyakuStrokes.AddRange(added);
        PushUndo(EditMode.Waruyaku, added);
        MarkDirty();
        MarkPreviewDirty();
    }

    void PushUndo(EditMode editMode, List<StarPoint> added)
    {
        undo.Push(new EditAction { mode = editMode, stars = added.ToArray() });
        redo.Clear();
    }

    void PushUndo(EditMode editMode, List<WaruyakuStroke> added)
    {
        undo.Push(new EditAction { mode = editMode, strokes = added.ToArray() });
        redo.Clear();
    }

    void Undo()
    {
        if (undo.Count == 0 || targetBlueprint == null) return;
        var action = undo.Pop();

        switch (action.mode)
        {
            case EditMode.Star:
                RemoveTail(targetBlueprint.stars, action.stars.Length);
                break;
            case EditMode.Waruyaku:
                RemoveTail(targetBlueprint.waruyakuStrokes, action.strokes.Length);
                break;
        }

        redo.Push(action);
        MarkDirty();
        MarkPreviewDirty();
    }

    void Redo()
    {
        if (redo.Count == 0 || targetBlueprint == null) return;
        var action = redo.Pop();

        switch (action.mode)
        {
            case EditMode.Star:
                if (targetBlueprint.stars == null) targetBlueprint.stars = new List<StarPoint>();
                targetBlueprint.stars.AddRange(action.stars);
                break;
            case EditMode.Waruyaku:
                if (targetBlueprint.waruyakuStrokes == null) targetBlueprint.waruyakuStrokes = new List<WaruyakuStroke>();
                targetBlueprint.waruyakuStrokes.AddRange(action.strokes);
                break;
        }

        undo.Push(action);
        MarkDirty();
        MarkPreviewDirty();
    }

    void RemoveTail<T>(List<T> list, int count)
    {
        if (list == null || count <= 0) return;
        int start = Mathf.Max(0, list.Count - count);
        list.RemoveRange(start, list.Count - start);
    }

    void EnsurePreviewSetup()
    {
        SetupPreview(starPreview, 0.02f, 10000, ref previewBuffer);
        SetupPreview(waruyakuPreview, 0.03f, 20000, ref waruyakuBuffer);
    }

    void UpdatePreviews()
    {
        previewDirty = false;
        UpdateStarPreview();
        UpdateWaruyakuPreview();
    }

    void UpdateStarPreview()
    {
        if (starPreview == null) return;
        if (targetBlueprint == null || targetBlueprint.stars == null || targetBlueprint.stars.Count == 0)
        {
            starPreview.Clear();
            return;
        }
        if (hemisphereRoot == null) return;
        if (previewBuffer == null || previewBuffer.Length == 0) return;

        int count = Mathf.Min(targetBlueprint.stars.Count, previewBuffer.Length);
        for (int i = 0; i < count; i++)
        {
            var sp = targetBlueprint.stars[i];
            Vector3 dir = sp.dir.sqrMagnitude > 1e-6f ? sp.dir.normalized : Vector3.up;
            Vector3 pLocal = dir * Mathf.Clamp01(sp.radius);
            Vector3 pWorld = hemisphereRoot.TransformPoint(pLocal);

            previewBuffer[i].position = pWorld;
            previewBuffer[i].startLifetime = 999f;
            previewBuffer[i].remainingLifetime = 999f;
            previewBuffer[i].startSize = Mathf.Max(0.01f, sp.size * 0.02f);
            previewBuffer[i].startColor = new Color(0.9f, 0.95f, 1f, 0.9f);
        }

        starPreview.SetParticles(previewBuffer, count);
        starPreview.Play();
    }

    void UpdateWaruyakuPreview()
    {
        if (waruyakuPreview == null) return;
        if (targetBlueprint == null)
        {
            waruyakuPreview.Clear();
            return;
        }
        if (hemisphereRoot == null) return;
        if (waruyakuBuffer == null || waruyakuBuffer.Length == 0) return;

        int count = 0;

        if (targetBlueprint.waruyakuStrokes != null && targetBlueprint.waruyakuStrokes.Count > 0)
        {
            for (int i = 0; i < targetBlueprint.waruyakuStrokes.Count; i++)
            {
                var stroke = targetBlueprint.waruyakuStrokes[i];
                if (stroke.strength == 0) continue;

                Vector3 dir = stroke.dir.sqrMagnitude > 1e-6f ? stroke.dir.normalized : Vector3.up;
                OrthonormalBasis(dir, out Vector3 t1, out Vector3 t2);
                Vector3 center = dir * Mathf.Clamp01(stroke.radius);

                int samples = Mathf.Clamp(Mathf.RoundToInt(stroke.brushRadius * 120f), 8, 64);
                for (int s = 0; s < samples && count < waruyakuBuffer.Length; s++)
                {
                    Vector2 d = SampleDisk(s, samples, stroke.brushRadius);
                    Vector3 pLocal = center + t1 * d.x + t2 * d.y;
                    Vector3 pWorld = hemisphereRoot.TransformPoint(pLocal);

                    float t = Mathf.InverseLerp(100f, 230f, stroke.strength);
                    Color c = Color.Lerp(new Color(0.3f, 0.8f, 1f, 0.6f), new Color(1f, 0.4f, 0.2f, 0.8f), t);

                    waruyakuBuffer[count].position = pWorld;
                    waruyakuBuffer[count].startLifetime = 999f;
                    waruyakuBuffer[count].remainingLifetime = 999f;
                    waruyakuBuffer[count].startSize = Mathf.Lerp(0.015f, 0.03f, t);
                    waruyakuBuffer[count].startColor = c;
                    count++;
                }
            }
        }

        if (targetBlueprint.waruyaku != null && targetBlueprint.waruyaku.Count > 0 && count < waruyakuBuffer.Length)
        {
            for (int i = 0; i < targetBlueprint.waruyaku.Count && count < waruyakuBuffer.Length; i++)
            {
                var wk = targetBlueprint.waruyaku[i];
                if (wk.strength == 0) continue;

                Vector3 pWorld = hemisphereRoot.TransformPoint(wk.center);
                float t = Mathf.InverseLerp(100f, 230f, wk.strength);
                Color c = Color.Lerp(new Color(0.2f, 0.7f, 0.9f, 0.5f), new Color(1f, 0.3f, 0.2f, 0.7f), t);

                waruyakuBuffer[count].position = pWorld;
                waruyakuBuffer[count].startLifetime = 999f;
                waruyakuBuffer[count].remainingLifetime = 999f;
                waruyakuBuffer[count].startSize = Mathf.Clamp(wk.radius * 0.08f, 0.02f, 0.08f);
                waruyakuBuffer[count].startColor = c;
                count++;
            }
        }

        if (count == 0)
        {
            waruyakuPreview.Clear();
            return;
        }

        waruyakuPreview.SetParticles(waruyakuBuffer, count);
        waruyakuPreview.Play();
    }

    void MarkDirty()
    {
#if UNITY_EDITOR
        if (targetBlueprint != null)
            EditorUtility.SetDirty(targetBlueprint);
#endif
    }

    void MarkPreviewDirty()
    {
        previewDirty = true;
    }

    void SyncTagFromDatabase()
    {
        if (database == null || database.starProfiles == null || database.starProfiles.Count == 0) return;
        if (string.IsNullOrWhiteSpace(starTag))
            starTag = database.starProfiles[0].tag;
    }

    static void OrthonormalBasis(Vector3 n, out Vector3 t1, out Vector3 t2)
    {
        Vector3 up = Mathf.Abs(n.y) < 0.99f ? Vector3.up : Vector3.right;
        t1 = Vector3.Normalize(Vector3.Cross(up, n));
        t2 = Vector3.Normalize(Vector3.Cross(n, t1));
    }

    static void SetupPreview(ParticleSystem ps, float size, int minMaxParticles, ref ParticleSystem.Particle[] buffer)
    {
        if (ps == null) return;
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 999f;
        main.startSpeed = 0f;
        main.startSize = size;
        main.maxParticles = Mathf.Max(main.maxParticles, minMaxParticles);

        var emission = ps.emission;
        emission.enabled = false;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = GetSphereMesh();
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.enableGPUInstancing = true;
            EnsurePreviewMaterial(renderer);
        }

        buffer = new ParticleSystem.Particle[main.maxParticles];
    }

    static Vector2 SampleDisk(int index, int total, float radius)
    {
        float golden = 2.39996323f;
        float r = radius * Mathf.Sqrt((index + 0.5f) / total);
        float theta = index * golden;
        return new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * r;
    }

    static ParticleSystem FindPreviewByName(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) return null;
        return go.GetComponent<ParticleSystem>();
    }

    static void EnsurePreviewMaterial(ParticleSystemRenderer renderer)
    {
        if (renderer == null) return;
        if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null &&
            renderer.sharedMaterial.shader.name != "Hidden/InternalErrorShader")
            return;

        Shader shader = FindPreviewShader();
        if (shader == null) return;

        var mat = new Material(shader);
        ApplyColor(mat, Color.white);
        renderer.sharedMaterial = mat;
    }

    static Shader FindPreviewShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader != null) return shader;
        shader = Shader.Find("Particles/Standard Surface");
        if (shader != null) return shader;
        shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null) return shader;
        shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null) return shader;
        return Shader.Find("Sprites/Default");
    }

    static Mesh GetSphereMesh()
    {
        if (cachedSphereMesh != null) return cachedSphereMesh;
        var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var mf = temp.GetComponent<MeshFilter>();
        cachedSphereMesh = mf != null ? mf.sharedMesh : null;
        if (Application.isPlaying)
            Destroy(temp);
        else
            DestroyImmediate(temp);
        return cachedSphereMesh;
    }

    static void ApplyColor(Material mat, Color color)
    {
        if (mat == null) return;
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

    static Vector2 RandomInsideCircle()
    {
        float a = Random.value * Mathf.PI * 2f;
        float r = Mathf.Sqrt(Random.value);
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
    }

    static byte StrengthToByte(WaruyakuStrength s)
    {
        switch (s)
        {
            case WaruyakuStrength.Low: return 120;
            case WaruyakuStrength.High: return 230;
            default: return 180;
        }
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 320, 340), GUI.skin.box);
        GUILayout.Label("Hanabi Workshop (MVP)");
        GUILayout.Label(targetBlueprint != null ? $"Blueprint: {targetBlueprint.name}" : "Blueprint: (none)");

        GUILayout.Space(4);
        GUILayout.Label("Mode");
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(mode == EditMode.Star, "Star", GUI.skin.button)) mode = EditMode.Star;
        if (GUILayout.Toggle(mode == EditMode.Waruyaku, "Waruyaku", GUI.skin.button)) mode = EditMode.Waruyaku;
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("Symmetry");
        symmetry = Mathf.Clamp(EditorIntField(symmetry), 1, 16);

        GUILayout.Space(4);
        GUILayout.Label($"Brush Radius: {brushRadius:F2}");
        brushRadius = GUILayout.HorizontalSlider(brushRadius, 0.02f, 0.25f);

        if (mode == EditMode.Star)
        {
            GUILayout.Label($"Star Radius: {starRadius:F2}");
            starRadius = GUILayout.HorizontalSlider(starRadius, 0.2f, 1.0f);
            GUILayout.Label($"Star Size: {starSize}");
            starSize = (byte)Mathf.Clamp(EditorIntField(starSize), 1, 8);

            GUILayout.Label("Star Tag");
            starTag = GUILayout.TextField(starTag);
        }
        else if (mode == EditMode.Waruyaku)
        {
            GUILayout.Label($"Waruyaku Radius: {waruyakuRadius:F2}");
            waruyakuRadius = GUILayout.HorizontalSlider(waruyakuRadius, 0.2f, 1.0f);
            GUILayout.Label("Strength");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(waruyakuStrength == WaruyakuStrength.Low, "L", GUI.skin.button)) waruyakuStrength = WaruyakuStrength.Low;
            if (GUILayout.Toggle(waruyakuStrength == WaruyakuStrength.Medium, "M", GUI.skin.button)) waruyakuStrength = WaruyakuStrength.Medium;
            if (GUILayout.Toggle(waruyakuStrength == WaruyakuStrength.High, "H", GUI.skin.button)) waruyakuStrength = WaruyakuStrength.High;
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Undo")) Undo();
        if (GUILayout.Button("Redo")) Redo();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Stars") && targetBlueprint != null)
        {
            if (targetBlueprint.stars != null)
                targetBlueprint.stars.Clear();
            MarkDirty();
            MarkPreviewDirty();
        }
        if (GUILayout.Button("Clear Waruyaku") && targetBlueprint != null)
        {
            if (targetBlueprint.waruyakuStrokes != null)
                targetBlueprint.waruyakuStrokes.Clear();
            MarkDirty();
            MarkPreviewDirty();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        if (GUILayout.Button("Reset All"))
        {
            ResetAllEdits();
        }

        GUILayout.Space(6);
        GUILayout.Label("Tips:");
        GUILayout.Label("- LMB: paint/place");
        GUILayout.Label("- 1=Star  2=Waruyaku");
        GUILayout.Label("- Ctrl+Z / Ctrl+Y");
        GUILayout.EndArea();
    }

    static int EditorIntField(int value)
    {
        string s = GUILayout.TextField(value.ToString(), GUILayout.Width(60));
        if (int.TryParse(s, out int v)) return v;
        return value;
    }

    void ResetAllEdits()
    {
        if (targetBlueprint == null) return;
        if (targetBlueprint.stars != null)
            targetBlueprint.stars.Clear();
        if (targetBlueprint.waruyakuStrokes != null)
            targetBlueprint.waruyakuStrokes.Clear();
        undo.Clear();
        redo.Clear();
        MarkDirty();
        MarkPreviewDirty();
    }

    struct EditAction
    {
        public EditMode mode;
        public StarPoint[] stars;
        public WaruyakuStroke[] strokes;
    }

    static Mesh cachedSphereMesh;
}
