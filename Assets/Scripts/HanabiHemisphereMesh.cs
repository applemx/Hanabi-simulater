using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[ExecuteAlways]
public class HanabiHemisphereMesh : MonoBehaviour
{
    [SerializeField, Range(6, 64)] int segments = 24;
    [SerializeField, Range(0.2f, 4f)] float radius = 1f;
    [SerializeField] bool addCollider = true;
    [SerializeField] bool openUp = true;

    Mesh mesh;

    void Awake()
    {
        Build();
    }

    void OnValidate()
    {
        Build();
    }

    void Build()
    {
        if (segments < 6) segments = 6;
        if (radius <= 0f) radius = 1f;

        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "HanabiHemisphereMesh";
        }
        else
        {
            mesh.Clear();
        }

        int latSegments = segments / 2;
        int lonSegments = segments;

        int vertCount = (latSegments + 1) * (lonSegments + 1);
        var verts = new Vector3[vertCount];
        var normals = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];

        int v = 0;
        for (int lat = 0; lat <= latSegments; lat++)
        {
            float phi = Mathf.PI * 0.5f * (lat / (float)latSegments);
            float y = Mathf.Cos(phi);
            float r = Mathf.Sin(phi);

            for (int lon = 0; lon <= lonSegments; lon++)
            {
                float theta = 2f * Mathf.PI * (lon / (float)lonSegments);
                float x = r * Mathf.Cos(theta);
                float z = r * Mathf.Sin(theta);

                Vector3 p = new Vector3(x, openUp ? -y : y, z) * radius;
                verts[v] = p;
                Vector3 n = p.sqrMagnitude > 1e-8f ? p.normalized : Vector3.up;
                normals[v] = openUp ? -n : n;
                uvs[v] = new Vector2(lon / (float)lonSegments, lat / (float)latSegments);
                v++;
            }
        }

        int triCount = latSegments * lonSegments * 6;
        var tris = new int[triCount];
        int t = 0;
        for (int lat = 0; lat < latSegments; lat++)
        {
            for (int lon = 0; lon < lonSegments; lon++)
            {
                int i0 = lat * (lonSegments + 1) + lon;
                int i1 = i0 + 1;
                int i2 = i0 + lonSegments + 1;
                int i3 = i2 + 1;

                if (openUp)
                {
                    tris[t++] = i0;
                    tris[t++] = i1;
                    tris[t++] = i2;

                    tris[t++] = i1;
                    tris[t++] = i3;
                    tris[t++] = i2;
                }
                else
                {
                    tris[t++] = i0;
                    tris[t++] = i2;
                    tris[t++] = i1;

                    tris[t++] = i1;
                    tris[t++] = i2;
                    tris[t++] = i3;
                }
            }
        }

        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        var mf = GetComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = GetComponent<MeshRenderer>();
        if (mr.sharedMaterial == null || mr.sharedMaterial.shader == null ||
            mr.sharedMaterial.shader.name == "Hidden/InternalErrorShader" ||
            mr.sharedMaterial.shader.name.Contains("Unlit"))
        {
            var shader = FindHemisphereShader();
            if (shader != null)
            {
                var mat = new Material(shader);
                ApplyColor(mat, new Color(0.2f, 0.6f, 1f, 0.18f));
                ConfigureHemisphereMaterial(mat);
                mr.sharedMaterial = mat;
            }
        }

        if (addCollider)
        {
            var col = GetComponent<MeshCollider>();
            if (col == null) col = gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
        }
    }

    static Shader FindHemisphereShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader != null) return shader;
        shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null) return shader;
        shader = Shader.Find("Particles/Standard Surface");
        if (shader != null) return shader;
        shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
        if (shader != null) return shader;
        shader = Shader.Find("Standard");
        if (shader != null) return shader;
        return Shader.Find("Sprites/Default");
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

    static void ConfigureHemisphereMaterial(Material mat)
    {
        if (mat == null) return;

        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        else if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.2f);
        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.2f);
    }
}
