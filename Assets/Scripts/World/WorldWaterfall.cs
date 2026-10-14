using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nusantara.World
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Nusantara/Waterfall")]
    public sealed class WorldWaterfall : MonoBehaviour
    {
        public Vector3 landing = new Vector3(0, -12, 2);
        [Min(.25f)] public float width = 5;
        [Range(0, 8)] public float bow = 1;
        [Range(.1f, 5)] public float flowSpeed = 1.2f;
        [Range(.25f, 3)] public float splashSize = 1;
        [Range(0, 1)] public float foam = .75f;
        public Color waterColor = new Color(.04f, .69f, .78f, 1);
        public Material waterMaterial;
        public Material riverMaterial;
        public bool matchRiverColours = true;
        Mesh generated;
        MaterialPropertyBlock properties;
        bool pending;
        public Vector3 Top => transform.position;
        public Vector3 Bottom => transform.TransformPoint(landing);
        public Vector3 Center(float t)
        {
            float drop = t * t * (3 - 2 * t);
            return new Vector3(landing.x * t, landing.y * drop,
                landing.z * t + bow * Mathf.Sin(t * Mathf.PI));
        }
        public float WidthAt(float t) => width * (1 + .18f * t * t);
        void OnEnable() => Rebuild();
        void OnValidate() => pending = true;
        void Update() { if (pending) { pending = false; Rebuild(); } }
        void OnDisable()
        {
            if (generated == null) return;
            if (GetComponent<MeshFilter>().sharedMesh == generated) GetComponent<MeshFilter>().sharedMesh = null;
            if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated);
            generated = null;
        }
        public void Rebuild()
        {
            width = float.IsFinite(width) ? Mathf.Clamp(width, .25f, 100) : 5;
            bow = float.IsFinite(bow) ? Mathf.Clamp(bow, 0, 8) : 1;
            splashSize = float.IsFinite(splashSize) ? Mathf.Clamp(splashSize, .25f, 3) : 1;
            flowSpeed = float.IsFinite(flowSpeed) ? Mathf.Clamp(flowSpeed, .1f, 5) : 1.2f;
            foam = float.IsFinite(foam) ? Mathf.Clamp01(foam) : .75f;
            if (!float.IsFinite(landing.x) || !float.IsFinite(landing.y) || !float.IsFinite(landing.z)) landing = new Vector3(0,-12,2);
            landing.y = Mathf.Min(-.25f, landing.y);
            var vertices = new List<Vector3>(); var uv = new List<Vector2>();
            var modes = new List<Vector2>(); var triangles = new List<int>();
            const int columns = 20, rows = 56;
            for (int y = 0; y <= rows; y++)
            {
                float t = y / (float)rows; var center = Center(t);
                for (int x = 0; x <= columns; x++)
                {
                    float u = x / (float)columns;
                    var p = center + Vector3.right * ((u - .5f) * WidthAt(t));
                    p.z += Mathf.Sin(u * 39 + t * 2) * width * .012f * Mathf.Sin(t * Mathf.PI);
                    vertices.Add(p); uv.Add(new Vector2(u, t)); modes.Add(Vector2.zero);
                    if (y < rows && x < columns) Quad(triangles, y * (columns + 1) + x, columns + 1);
                }
            }
            float radius = width * .8f * splashSize;
            int start = vertices.Count;
            for (int z = 0; z < 2; z++) for (int x = 0; x < 2; x++)
            {
                vertices.Add(landing + new Vector3((x * 2 - 1) * radius, .07f, (z * 2 - 1) * radius * .7f));
                uv.Add(new Vector2(x, z)); modes.Add(Vector2.right);
            }
            Quad(triangles, start, 2);
            // Curved foam plumes break up the impact instead of a solid white wall.
            for (int plume = 0; plume < 13; plume++)
            {
                float angle = plume * 2.399963f;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var side = Vector3.Cross(Vector3.up, direction);
                float reach = radius * (.32f + .24f * Mathf.Abs(Mathf.Sin(plume * 7.1f)));
                start = vertices.Count;
                for (int row = 0; row <= 10; row++)
                {
                    float t = row / 10f;
                    var center = landing + direction * (width * .17f + reach * t);
                    center.y += .1f + Mathf.Sin(t * Mathf.PI * .78f) * width * .28f * splashSize;
                    for (int x = 0; x < 2; x++)
                    {
                        vertices.Add(center + side * ((x - .5f) * width * .27f * (1 - .8f * t)));
                        uv.Add(new Vector2(x, t)); modes.Add(new Vector2(2, plume * .17f));
                    }
                    if (row < 10) Quad(triangles, start + row * 2, 2);
                }
            }
            if (generated == null) generated = new Mesh { name = "Waterfall curtain and splash", hideFlags = HideFlags.DontSave };
            generated.Clear(); generated.SetVertices(vertices); generated.SetUVs(0, uv); generated.SetUVs(1, modes);
            generated.SetTriangles(triangles, 0); generated.RecalculateNormals(); generated.RecalculateBounds();
            var bounds = generated.bounds; bounds.Expand(2); generated.bounds = bounds;
            GetComponent<MeshFilter>().sharedMesh = generated;
            var renderer = GetComponent<MeshRenderer>();
            if (waterMaterial != null) renderer.sharedMaterial = waterMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (properties == null) properties = new MaterialPropertyBlock();
            bool match = matchRiverColours && riverMaterial != null && riverMaterial.HasProperty("_BaseColor");
            var baseColor = match ? riverMaterial.GetColor("_BaseColor") : waterColor;
            var deepColor = match && riverMaterial.HasProperty("_DeepColor") ? riverMaterial.GetColor("_DeepColor") : baseColor * .5f;
            var highlights = match && riverMaterial.HasProperty("_FoamColor") ? riverMaterial.GetColor("_FoamColor") : Color.Lerp(baseColor,Color.white,.45f);
            properties.SetColor("_WaterColor", baseColor);properties.SetColor("_DeepColor",deepColor);properties.SetColor("_HighlightColor",highlights);
            properties.SetFloat("_FlowSpeed", flowSpeed);
            properties.SetFloat("_Foam", foam); properties.SetFloat("_Width", width);
            properties.SetFloat("_FallLength", Mathf.Max(1, landing.magnitude));
            renderer.SetPropertyBlock(properties);
        }
        static void Quad(List<int> triangles, int a, int stride)
        {
            triangles.Add(a); triangles.Add(a + stride); triangles.Add(a + 1);
            triangles.Add(a + 1); triangles.Add(a + stride); triangles.Add(a + stride + 1);
        }
    }
}
