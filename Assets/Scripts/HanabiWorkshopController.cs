using System.Collections.Generic;
using System.IO;
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
    [SerializeField] HanabiWorkshopCameraOrbit viewOrbit;
    [SerializeField] ParticleSystem starPreview;
    [SerializeField] ParticleSystem waruyakuPreview;

    [Header("Edit Mode")]
    [SerializeField] EditMode mode = EditMode.Star;
    [SerializeField] int symmetry = 8;

    [Header("Star Placement")]
    [SerializeField, Range(0.1f, 1.0f)] float starRadius = 0.85f;
    [SerializeField, Range(0f, 1.0f)] float starDepth = 0.0f;
    [SerializeField] byte starSize = 2;
    [SerializeField] int scatterPerStamp = 4;
    [SerializeField, Range(0.01f, 0.4f)] float brushRadius = 0.08f;
    [SerializeField] string starTag = "Solid";

    [Header("Waruyaku Paint")]
    [SerializeField, Range(0.1f, 1.0f)] float waruyakuRadius = 0.65f;
    [SerializeField, Range(0f, 1.0f)] float waruyakuDepth = 0.0f;
    [SerializeField] WaruyakuStrength waruyakuStrength = WaruyakuStrength.Medium;

    [Header("Preview")]
    [SerializeField] bool mirrorUpperPreview = true;
    [SerializeField] bool showStarsPreview = true;
    [SerializeField] bool showWaruyakuPreview = true;
    [SerializeField] bool showHemisphere = true;
    [SerializeField] bool colorByDepth = true;
    [SerializeField] bool useHitRadius = true;
    [SerializeField] bool snapToVoxels = true;
    [SerializeField] bool sliceMode = false;
    [SerializeField, Range(0f, 1f)] float sliceRadius = 0.85f;
    [SerializeField, Range(0.01f, 0.3f)] float sliceThickness = 0.06f;

    [Header("Volume Brush")]
    [SerializeField] bool volumeBrush = false;
    [SerializeField, Range(0.01f, 0.4f)] float volumeRadius = 0.12f;
    [SerializeField, Range(1, 64)] int volumeScatter = 12;

    [Header("Input")]
    [SerializeField] float paintSpacing = 0.03f;
    [SerializeField] KeyCode starModeKey = KeyCode.Alpha1;
    [SerializeField] KeyCode waruyakuModeKey = KeyCode.Alpha2;

    [Header("UI")]
    [SerializeField] float uiWidth = 320f;
    [SerializeField] float uiMaxHeight = 520f;
    [SerializeField] bool uiAutoHeight = true;

    readonly Stack<EditAction> undo = new Stack<EditAction>();
    readonly Stack<EditAction> redo = new Stack<EditAction>();

    bool painting;
    Vector3 lastPaintLocal;
    bool previewDirty;
    ParticleSystem.Particle[] previewBuffer;
    ParticleSystem.Particle[] waruyakuBuffer;
    Vector2 uiScroll;
    MeshRenderer hemisphereRenderer;

    void Awake()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewOrbit == null && viewCamera != null)
            viewOrbit = viewCamera.GetComponent<HanabiWorkshopCameraOrbit>();
        if (hemisphereCollider == null && hemisphereRoot != null)
            hemisphereCollider = hemisphereRoot.GetComponent<Collider>();
        if (hemisphereRoot == null && hemisphereCollider != null)
            hemisphereRoot = hemisphereCollider.transform;

        if (starPreview == null)
            starPreview = FindPreviewByName("StarPreview") ?? FindAnyObjectByType<ParticleSystem>();
        if (waruyakuPreview == null)
            waruyakuPreview = FindPreviewByName("WaruyakuPreview");
        if (hemisphereRenderer == null && hemisphereRoot != null)
            hemisphereRenderer = hemisphereRoot.GetComponent<MeshRenderer>();

        EnsurePreviewSetup();
        SyncTagFromDatabase();
        SyncFromBlueprint();
        MarkPreviewDirty();
    }

    public void SetReferences(FireworkBlueprint bp, HanabiDatabase db, Transform hemiRoot, Collider hemiCollider, Camera cam, ParticleSystem preview, ParticleSystem waruyakuPreviewSystem)
    {
        targetBlueprint = bp;
        database = db;
        hemisphereRoot = hemiRoot;
        hemisphereCollider = hemiCollider;
        viewCamera = cam;
        viewOrbit = cam != null ? cam.GetComponent<HanabiWorkshopCameraOrbit>() : null;
        starPreview = preview;
        waruyakuPreview = waruyakuPreviewSystem;
        EnsurePreviewSetup();
        SyncTagFromDatabase();
        SyncFromBlueprint();
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

        float baseRadius = useHitRadius ? hitLocal.magnitude : starRadius;
        int count = Mathf.Max(1, scatterPerStamp);

        if (volumeBrush)
        {
            int samples = Mathf.Max(1, volumeScatter);
            Vector3 center = dir * baseRadius;
            for (int i = 0; i < samples; i++)
            {
                if (!TrySampleVolumePoint(center, volumeRadius, out Vector3 pLocal))
                    continue;
                if (!TryLocalToDirRadius(pLocal, out Vector3 dVec, out float radius))
                    continue;
                if (snapToVoxels)
                    SnapToVoxelCenter(ref dVec, ref radius);
                AddStarWithSymmetry(dVec, radius, added);
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 d = RandomInsideCircle() * brushRadius;
                Vector3 dVec = (dir + t1 * d.x + t2 * d.y).normalized;
                float radius = baseRadius;
                if (starDepth > 0f)
                    radius += Random.Range(-starDepth * 0.5f, starDepth * 0.5f);
                radius = Mathf.Clamp01(radius);
                if (snapToVoxels)
                    SnapToVoxelCenter(ref dVec, ref radius);
                AddStarWithSymmetry(dVec, radius, added);
            }
        }

        if (added.Count > 0)
        {
            targetBlueprint.stars.AddRange(added);
            PushUndo(EditMode.Star, added);
            MarkDirty();
            MarkPreviewDirty();
        }
    }

    void AddStarWithSymmetry(Vector3 dir, float radius, List<StarPoint> added)
    {
        int n = Mathf.Max(1, symmetry);
        float step = 360f / n;
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Quaternion.AngleAxis(step * i, Vector3.up) * dir;
            added.Add(new StarPoint
            {
                dir = d,
                radius = Mathf.Clamp01(radius),
                tag = starTag,
                size = starSize
            });
        }
    }

    void AddWaruyakuStroke(Vector3 hitLocal)
    {
        if (targetBlueprint.waruyakuStrokes == null) targetBlueprint.waruyakuStrokes = new List<WaruyakuStroke>();

        float depth = Mathf.Clamp01(waruyakuDepth);
        float baseRadius = useHitRadius ? hitLocal.magnitude : waruyakuRadius;
        int depthLayers = depth <= 0.001f ? 1 : Mathf.Clamp(Mathf.RoundToInt(depth * 4f) + 1, 2, 6);
        var added = new List<WaruyakuStroke>(Mathf.Max(1, symmetry) * depthLayers);
        Vector3 dir = hitLocal.sqrMagnitude > 1e-6f ? hitLocal.normalized : Vector3.up;

        int n = Mathf.Max(1, symmetry);
        float step = 360f / n;
        byte strength = StrengthToByte(waruyakuStrength);
        for (int i = 0; i < n; i++)
        {
            Vector3 d = Quaternion.AngleAxis(step * i, Vector3.up) * dir;
            if (volumeBrush)
            {
                int samples = Mathf.Max(1, volumeScatter);
                Vector3 center = d * baseRadius;
                for (int s = 0; s < samples; s++)
                {
                    if (!TrySampleVolumePoint(center, volumeRadius, out Vector3 pLocal))
                        continue;
                    if (!TryLocalToDirRadius(pLocal, out Vector3 dVec, out float radius))
                        continue;
                    if (snapToVoxels)
                        SnapToVoxelCenter(ref dVec, ref radius);

                    added.Add(new WaruyakuStroke
                    {
                        dir = dVec,
                        radius = radius,
                        brushRadius = brushRadius,
                        strength = strength
                    });
                }
            }
            else
            {
                for (int layer = 0; layer < depthLayers; layer++)
                {
                    float radius = baseRadius;
                    if (depth > 0f)
                        radius += Random.Range(-depth * 0.5f, depth * 0.5f);
                    radius = Mathf.Clamp01(radius);
                    Vector3 dLayer = d;
                    if (snapToVoxels)
                        SnapToVoxelCenter(ref dLayer, ref radius);

                    added.Add(new WaruyakuStroke
                    {
                        dir = dLayer,
                        radius = radius,
                        brushRadius = brushRadius,
                        strength = strength
                    });
                }
            }
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
        UpdateHemisphereVisibility();
        UpdateStarPreview();
        UpdateWaruyakuPreview();
    }

    void UpdateStarPreview()
    {
        if (starPreview == null) return;
        if (!showStarsPreview)
        {
            starPreview.Clear();
            return;
        }
        if (targetBlueprint == null || targetBlueprint.stars == null || targetBlueprint.stars.Count == 0)
        {
            starPreview.Clear();
            return;
        }
        if (hemisphereRoot == null) return;
        if (previewBuffer == null || previewBuffer.Length == 0) return;

        int count = 0;
        int limit = previewBuffer.Length;
        for (int i = 0; i < targetBlueprint.stars.Count && count < limit; i++)
        {
            AddStarPreview(targetBlueprint.stars[i], ref count, limit);
            if (mirrorUpperPreview && count < limit)
            {
                var mirror = targetBlueprint.stars[i];
                mirror.dir = new Vector3(mirror.dir.x, -mirror.dir.y, mirror.dir.z);
                AddStarPreview(mirror, ref count, limit);
            }
        }

        if (count == 0)
        {
            starPreview.Clear();
            return;
        }

        starPreview.SetParticles(previewBuffer, count);
        starPreview.Play();
    }

    void UpdateWaruyakuPreview()
    {
        if (waruyakuPreview == null) return;
        if (!showWaruyakuPreview)
        {
            waruyakuPreview.Clear();
            return;
        }
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
                EmitWaruyakuStrokePreview(stroke, ref count);
                if (mirrorUpperPreview)
                {
                    var mirror = stroke;
                    mirror.dir = new Vector3(mirror.dir.x, -mirror.dir.y, mirror.dir.z);
                    EmitWaruyakuStrokePreview(mirror, ref count);
                }
            }
        }

        if (targetBlueprint.waruyaku != null && targetBlueprint.waruyaku.Count > 0 && count < waruyakuBuffer.Length)
        {
            for (int i = 0; i < targetBlueprint.waruyaku.Count && count < waruyakuBuffer.Length; i++)
            {
                var wk = targetBlueprint.waruyaku[i];
                if (wk.strength == 0) continue;
                if (sliceMode && Mathf.Abs(wk.center.magnitude - sliceRadius) > sliceThickness * 0.5f) continue;

                Vector3 pWorld = hemisphereRoot.TransformPoint(wk.center);
                float t = Mathf.InverseLerp(100f, 230f, wk.strength);
                Color c = Color.Lerp(new Color(0.2f, 0.7f, 0.9f, 0.5f), new Color(1f, 0.3f, 0.2f, 0.7f), t);

                waruyakuBuffer[count].position = pWorld;
                waruyakuBuffer[count].startLifetime = 999f;
                waruyakuBuffer[count].remainingLifetime = 999f;
                float voxelSize = GetVoxelSize();
                float scale = hemisphereRoot != null ? hemisphereRoot.lossyScale.x : 1f;
                float diameter = wk.radius * 2f * scale;
                waruyakuBuffer[count].startSize = Mathf.Clamp(diameter, voxelSize * 0.8f, voxelSize * 10f);
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

    void AddStarPreview(StarPoint sp, ref int count, int limit)
    {
        if (count >= limit) return;
        if (hemisphereRoot == null) return;
        if (sliceMode && Mathf.Abs(sp.radius - sliceRadius) > sliceThickness * 0.5f) return;

        Vector3 dir = sp.dir.sqrMagnitude > 1e-6f ? sp.dir.normalized : Vector3.up;
        Vector3 pLocal = dir * Mathf.Clamp01(sp.radius);
        Vector3 pWorld = hemisphereRoot.TransformPoint(pLocal);

        float voxelSize = GetVoxelSize();
        float scale = hemisphereRoot != null ? hemisphereRoot.lossyScale.x : 1f;
        float size = Mathf.Max(0.005f, sp.size * voxelSize * scale);
        float depthT = Mathf.Clamp01(sp.radius);
        Color baseColor = new Color(0.9f, 0.95f, 1f, 0.9f);
        if (colorByDepth)
            baseColor = Color.Lerp(new Color(1f, 0.7f, 0.4f, 0.9f), new Color(0.6f, 0.9f, 1f, 0.9f), depthT);

        previewBuffer[count].position = pWorld;
        previewBuffer[count].startLifetime = 999f;
        previewBuffer[count].remainingLifetime = 999f;
        previewBuffer[count].startSize = size;
        previewBuffer[count].startColor = baseColor;
        count++;
    }

    void EmitWaruyakuStrokePreview(WaruyakuStroke stroke, ref int count)
    {
        if (hemisphereRoot == null) return;
        if (count >= waruyakuBuffer.Length) return;
        if (sliceMode && Mathf.Abs(stroke.radius - sliceRadius) > sliceThickness * 0.5f) return;

        Vector3 dir = stroke.dir.sqrMagnitude > 1e-6f ? stroke.dir.normalized : Vector3.up;
        OrthonormalBasis(dir, out Vector3 t1, out Vector3 t2);
        Vector3 center = dir * Mathf.Clamp01(stroke.radius);

        int samples = Mathf.Clamp(Mathf.RoundToInt(stroke.brushRadius * 120f), 8, 64);
        float t = Mathf.InverseLerp(100f, 230f, stroke.strength);
        Color c = Color.Lerp(new Color(0.3f, 0.8f, 1f, 0.6f), new Color(1f, 0.4f, 0.2f, 0.8f), t);
        if (colorByDepth)
        {
            float depthT = Mathf.Clamp01(stroke.radius);
            c *= Color.Lerp(new Color(0.7f, 0.7f, 0.7f, 1f), new Color(1f, 1f, 1f, 1f), depthT);
        }
        float voxelSize = GetVoxelSize();
        float scale = hemisphereRoot != null ? hemisphereRoot.lossyScale.x : 1f;
        float size = Mathf.Lerp(voxelSize * 0.8f, voxelSize * 1.6f, t) * scale;

        for (int s = 0; s < samples && count < waruyakuBuffer.Length; s++)
        {
            Vector2 d = SampleDisk(s, samples, stroke.brushRadius);
            Vector3 pLocal = center + t1 * d.x + t2 * d.y;
            Vector3 pWorld = hemisphereRoot.TransformPoint(pLocal);

            waruyakuBuffer[count].position = pWorld;
            waruyakuBuffer[count].startLifetime = 999f;
            waruyakuBuffer[count].remainingLifetime = 999f;
            waruyakuBuffer[count].startSize = size;
            waruyakuBuffer[count].startColor = c;
            count++;
        }
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

    void SyncFromBlueprint()
    {
        if (targetBlueprint == null) return;
        mirrorUpperPreview = targetBlueprint.mirrorUpperHemisphere;
    }

    void UpdateHemisphereVisibility()
    {
        if (hemisphereRenderer == null && hemisphereRoot != null)
            hemisphereRenderer = hemisphereRoot.GetComponent<MeshRenderer>();
        if (hemisphereRenderer != null)
            hemisphereRenderer.enabled = showHemisphere;
    }

    static void OrthonormalBasis(Vector3 n, out Vector3 t1, out Vector3 t2)
    {
        Vector3 up = Mathf.Abs(n.y) < 0.99f ? Vector3.up : Vector3.right;
        t1 = Vector3.Normalize(Vector3.Cross(up, n));
        t2 = Vector3.Normalize(Vector3.Cross(n, t1));
    }

    bool TrySampleVolumePoint(Vector3 center, float radius, out Vector3 pLocal)
    {
        pLocal = center;
        float r = Mathf.Max(0.001f, radius);

        for (int i = 0; i < 8; i++)
        {
            Vector3 offset = RandomInsideSphere() * r;
            Vector3 p = center + offset;
            if (p.sqrMagnitude > 1f)
            {
                p = p.normalized * 0.999f;
            }
            if (p.y > 0f)
                p.y = -Mathf.Abs(p.y);
            pLocal = p;
            return true;
        }

        return false;
    }

    static bool TryLocalToDirRadius(Vector3 pLocal, out Vector3 dir, out float radius)
    {
        float mag = pLocal.magnitude;
        if (mag < 1e-6f)
        {
            dir = Vector3.up;
            radius = 0f;
            return false;
        }
        dir = pLocal / mag;
        radius = Mathf.Clamp01(mag);
        return true;
    }

    int GetShellRes()
    {
        if (targetBlueprint == null) return 64;
        switch (targetBlueprint.shellSize)
        {
            case ShellSize.Small: return 48;
            case ShellSize.Medium: return 64;
            case ShellSize.Large: return 80;
            default: return 64;
        }
    }

    float GetVoxelSize()
    {
        int res = GetShellRes();
        return 2f / res;
    }

    void SnapToVoxelCenter(ref Vector3 dir, ref float radius)
    {
        int res = GetShellRes();
        Vector3 pLocal = dir * Mathf.Clamp01(radius);

        int x = LocalToVoxelIndex(pLocal.x, res);
        int y = LocalToVoxelIndex(pLocal.y, res);
        int z = LocalToVoxelIndex(pLocal.z, res);

        Vector3 snapped = VoxelCenterToLocal(x, y, z, res);
        float mag = snapped.magnitude;
        if (mag > 1e-6f)
        {
            dir = snapped / mag;
            radius = Mathf.Clamp01(mag);
        }
    }

    static int LocalToVoxelIndex(float local, int res)
    {
        float f = (local * 0.5f) + 0.5f;
        return Mathf.Clamp(Mathf.RoundToInt(f * (res - 1)), 0, res - 1);
    }

    static Vector3 VoxelCenterToLocal(int x, int y, int z, int res)
    {
        float fx = ((x + 0.5f) / res) * 2f - 1f;
        float fy = ((y + 0.5f) / res) * 2f - 1f;
        float fz = ((z + 0.5f) / res) * 2f - 1f;
        return new Vector3(fx, fy, fz);
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

    static T FindAnyObjectByType<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>();
#else
        return Object.FindObjectOfType<T>();
#endif
    }

    static void EnsurePreviewMaterial(ParticleSystemRenderer renderer)
    {
        if (renderer == null) return;
        if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null &&
            renderer.sharedMaterial.shader.name != "Hidden/InternalErrorShader" &&
            !renderer.sharedMaterial.shader.name.Contains("Unlit"))
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

    static Vector3 RandomInsideSphere()
    {
        while (true)
        {
            Vector3 v = new Vector3(Random.value * 2f - 1f, Random.value * 2f - 1f, Random.value * 2f - 1f);
            if (v.sqrMagnitude <= 1f) return v;
        }
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
        if (Camera.current != null && viewCamera != null && Camera.current != viewCamera)
            return;

        bool prevChanged = GUI.changed;
        GUI.changed = false;

        float height = uiAutoHeight ? Mathf.Clamp(Screen.height - 20f, 200f, uiMaxHeight) : uiMaxHeight;
        GUILayout.BeginArea(new Rect(10, 10, uiWidth, height), GUI.skin.box);
        uiScroll = GUILayout.BeginScrollView(uiScroll, false, true);
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
        bool mirror = GUILayout.Toggle(mirrorUpperPreview, "Mirror Upper (Full Sphere)");
        if (mirror != mirrorUpperPreview)
        {
            mirrorUpperPreview = mirror;
            if (targetBlueprint != null)
            {
                targetBlueprint.mirrorUpperHemisphere = mirror;
                MarkDirty();
            }
            MarkPreviewDirty();
        }

        GUILayout.Space(4);
        GUILayout.Label("Preview");
        bool colorDepth = GUILayout.Toggle(colorByDepth, "Color By Depth");
        if (colorDepth != colorByDepth)
        {
            colorByDepth = colorDepth;
            MarkPreviewDirty();
        }
        bool volBrush = GUILayout.Toggle(volumeBrush, "Volume Brush");
        if (volBrush != volumeBrush)
        {
            volumeBrush = volBrush;
            MarkPreviewDirty();
        }
        if (volumeBrush)
        {
            GUILayout.Label($"Volume Radius: {volumeRadius:F2}");
            volumeRadius = GUILayout.HorizontalSlider(volumeRadius, 0.02f, 0.4f);
            GUILayout.Label($"Volume Scatter: {volumeScatter}");
            volumeScatter = Mathf.Clamp(EditorIntField(volumeScatter), 1, 64);
        }
        bool useHit = GUILayout.Toggle(useHitRadius, "Use Surface Depth");
        if (useHit != useHitRadius)
        {
            useHitRadius = useHit;
            MarkPreviewDirty();
        }
        bool snap = GUILayout.Toggle(snapToVoxels, "Snap To Voxels");
        if (snap != snapToVoxels)
        {
            snapToVoxels = snap;
            MarkPreviewDirty();
        }
        bool showStars = GUILayout.Toggle(showStarsPreview, "Show Stars");
        if (showStars != showStarsPreview)
        {
            showStarsPreview = showStars;
            MarkPreviewDirty();
        }
        bool showWaruyaku = GUILayout.Toggle(showWaruyakuPreview, "Show Waruyaku");
        if (showWaruyaku != showWaruyakuPreview)
        {
            showWaruyakuPreview = showWaruyaku;
            MarkPreviewDirty();
        }
        bool showHemi = GUILayout.Toggle(showHemisphere, "Show Hemisphere");
        if (showHemi != showHemisphere)
        {
            showHemisphere = showHemi;
            MarkPreviewDirty();
        }
        bool slice = GUILayout.Toggle(sliceMode, "Slice Mode");
        if (slice != sliceMode)
        {
            sliceMode = slice;
            MarkPreviewDirty();
        }
        if (sliceMode)
        {
            GUILayout.Label($"Slice Radius: {sliceRadius:F2}");
            sliceRadius = GUILayout.HorizontalSlider(sliceRadius, 0.0f, 1.0f);
            GUILayout.Label($"Slice Thickness: {sliceThickness:F2}");
            sliceThickness = GUILayout.HorizontalSlider(sliceThickness, 0.01f, 0.2f);
        }

        GUILayout.Space(4);
        GUILayout.Label("View");
        if (viewOrbit != null)
        {
            GUILayout.BeginHorizontal();
            bool wantTop = GUILayout.Toggle(viewOrbit.IsTopDown(), "Top Down", GUI.skin.button);
            bool wantOrbit = GUILayout.Toggle(!viewOrbit.IsTopDown(), "Orbit", GUI.skin.button);
            GUILayout.EndHorizontal();
            if (wantTop && !viewOrbit.IsTopDown())
                viewOrbit.SetTopDown(true);
            else if (wantOrbit && viewOrbit.IsTopDown())
                viewOrbit.SetTopDown(false);
        }
        else
        {
            GUILayout.Label("View: (no orbit camera)");
        }

        GUILayout.Space(4);
        GUILayout.Label($"Brush Radius: {brushRadius:F2}");
        brushRadius = GUILayout.HorizontalSlider(brushRadius, 0.02f, 0.25f);

        if (mode == EditMode.Star)
        {
            GUILayout.Label($"Star Radius: {starRadius:F2}");
            starRadius = GUILayout.HorizontalSlider(starRadius, 0.2f, 1.0f);
            GUILayout.Label($"Star Depth: {starDepth:F2}");
            starDepth = GUILayout.HorizontalSlider(starDepth, 0.0f, 0.5f);
            GUILayout.Label($"Star Size: {starSize}");
            starSize = (byte)Mathf.Clamp(EditorIntField(starSize), 1, 8);

            GUILayout.Label("Star Tag");
            starTag = GUILayout.TextField(starTag);
        }
        else if (mode == EditMode.Waruyaku)
        {
            GUILayout.Label($"Waruyaku Radius: {waruyakuRadius:F2}");
            waruyakuRadius = GUILayout.HorizontalSlider(waruyakuRadius, 0.2f, 1.0f);
            GUILayout.Label($"Waruyaku Depth: {waruyakuDepth:F2}");
            waruyakuDepth = GUILayout.HorizontalSlider(waruyakuDepth, 0.0f, 0.5f);
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
        if (targetBlueprint != null)
        {
            int starCount = targetBlueprint.stars != null ? targetBlueprint.stars.Count : 0;
            int waruStrokeCount = targetBlueprint.waruyakuStrokes != null ? targetBlueprint.waruyakuStrokes.Count : 0;
            int waruPrimCount = targetBlueprint.waruyaku != null ? targetBlueprint.waruyaku.Count : 0;
            GUILayout.Label($"Stats: stars={starCount} waruStrokes={waruStrokeCount} waruPrim={waruPrimCount}");
        }

        GUILayout.Space(6);
        GUILayout.Label("Tips:");
        GUILayout.Label("- LMB: paint/place");
        GUILayout.Label("- 1=Star  2=Waruyaku");
        GUILayout.Label("- Ctrl+Z / Ctrl+Y");
        GUILayout.Label("- Use Depth sliders for volume");
#if UNITY_EDITOR
        GUILayout.Space(6);
        GUILayout.Label("Actions:");
        if (GUILayout.Button("Compile Blueprint"))
            CompileTargetBlueprint(false);
        if (GUILayout.Button("Compile + Ping Asset"))
            CompileTargetBlueprint(true);
#endif
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        if (GUI.changed)
            MarkPreviewDirty();
        GUI.changed |= prevChanged;
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

#if UNITY_EDITOR
    void CompileTargetBlueprint(bool pingAsset)
    {
        if (targetBlueprint == null)
        {
            Debug.LogWarning("[Hanabi] Assign a FireworkBlueprint first.");
            return;
        }

        var db = GetOrCreateDatabase();

        string bpPath = AssetDatabase.GetAssetPath(targetBlueprint);
        string dir = Path.GetDirectoryName(bpPath);
        string csPath = Path.Combine(dir ?? "Assets", $"CS_{targetBlueprint.name}.asset").Replace("\\", "/");

        var cs = AssetDatabase.LoadAssetAtPath<CompiledShowAsset>(csPath);
        if (cs == null)
        {
            cs = ScriptableObject.CreateInstance<CompiledShowAsset>();
            cs.version = 2;
            AssetDatabase.CreateAsset(cs, csPath);
        }

        HanabiCompiler_MVP.Compile(targetBlueprint, db, out uint seed, out BurstEvent[] bursts, out ParticleInitV2[] inits, out LaunchParams launchParams);
        cs.blob = CompiledShowSerializer.Write(seed, bursts, inits, launchParams, version: 2);
        cs.version = 2;

        EditorUtility.SetDirty(cs);
        AssetDatabase.SaveAssets();

        if (pingAsset)
            EditorGUIUtility.PingObject(cs);

        Debug.Log($"[Hanabi] Compiled {targetBlueprint.name} -> {csPath}  bursts={bursts.Length} inits={inits.Length}");
    }

    static HanabiDatabase GetOrCreateDatabase()
    {
        const string defaultPath = "Assets/Data/HanabiDatabase_Default.asset";

        string[] guids = AssetDatabase.FindAssets("t:HanabiDatabase");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var db = AssetDatabase.LoadAssetAtPath<HanabiDatabase>(path);
            if (db != null)
            {
                db.EnsureDefaultsIfEmpty();
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssetIfDirty(db);
                return db;
            }
        }

        var existing = AssetDatabase.LoadAssetAtPath<HanabiDatabase>(defaultPath);
        if (existing != null)
        {
            existing.EnsureDefaultsIfEmpty();
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(defaultPath));
        var created = ScriptableObject.CreateInstance<HanabiDatabase>();
        created.EnsureDefaultsIfEmpty();
        AssetDatabase.CreateAsset(created, defaultPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[HanabiDB] Created default DB at {defaultPath}");
        return created;
    }
#endif

    struct EditAction
    {
        public EditMode mode;
        public StarPoint[] stars;
        public WaruyakuStroke[] strokes;
    }

    static Mesh cachedSphereMesh;
}
