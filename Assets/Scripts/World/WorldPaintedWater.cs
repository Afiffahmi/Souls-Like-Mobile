using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nusantara.World
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Nusantara/Painted Water")]
    public sealed class WorldPaintedWater : MonoBehaviour
    {
        public const float CellSize = 1f;
        public const int CellLimit = 100000;
        [HideInInspector] public Material waterMaterial;
        [HideInInspector] public List<Vector2Int> cells = new List<Vector2Int>();
        Mesh generated;
        void OnEnable() => Rebuild();
        void OnDisable()
        {
            if (generated == null) return;
            if (GetComponent<MeshFilter>().sharedMesh == generated) GetComponent<MeshFilter>().sharedMesh = null;
            if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated);
            generated = null;
        }
        public bool Paint(Vector3 worldPoint, float radius, bool erase, bool rebuild = true)
        {
            var point = transform.InverseTransformPoint(worldPoint);
            var set = new HashSet<Vector2Int>(cells); bool changed = false;
            int x0 = Mathf.FloorToInt((point.x - radius) / CellSize), x1 = Mathf.FloorToInt((point.x + radius) / CellSize);
            int z0 = Mathf.FloorToInt((point.z - radius) / CellSize), z1 = Mathf.FloorToInt((point.z + radius) / CellSize);
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
            {
                if (new Vector2((x + .5f) * CellSize - point.x, (z + .5f) * CellSize - point.z).sqrMagnitude > radius * radius) continue;
                var key = new Vector2Int(x,z);
                if (erase) changed |= set.Remove(key);
                else if (set.Count < CellLimit) changed |= set.Add(key);
            }
            if (!changed) return false;
            cells = new List<Vector2Int>(set);
            cells.Sort((a,b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            if (rebuild) Rebuild(); return true;
        }
        public void Rebuild()
        {
            if (generated == null) generated = new Mesh { name = "Painted moving water", hideFlags = HideFlags.DontSave };
            generated.Clear();
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            var normals = new List<Vector3>(); var uv = new List<Vector2>();
            var indices = new Dictionary<Vector2Int, int>();
            int Vertex(Vector2Int p)
            {
                if (indices.TryGetValue(p, out int index)) return index;
                index = vertices.Count; indices.Add(p,index);
                vertices.Add(new Vector3(p.x * CellSize,0,p.y * CellSize)); normals.Add(Vector3.up); uv.Add(new Vector2(p.x,p.y));
                return index;
            }
            foreach (var cell in cells)
            {
                int a=Vertex(cell), b=Vertex(cell+Vector2Int.up), c=Vertex(cell+Vector2Int.one), d=Vertex(cell+Vector2Int.right);
                triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(a);triangles.Add(c);triangles.Add(d);
            }
            generated.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            generated.SetVertices(vertices); generated.SetNormals(normals); generated.SetUVs(0,uv); generated.SetTriangles(triangles,0);
            generated.RecalculateBounds(); var bounds = generated.bounds; bounds.Expand(new Vector3(0,4,0)); generated.bounds=bounds;
            GetComponent<MeshFilter>().sharedMesh = generated;
            if (waterMaterial != null) GetComponent<MeshRenderer>().sharedMaterial = waterMaterial;
            var renderer = GetComponent<MeshRenderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
    }
}
