using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nusantara.World
{
    // Keep the authored mesh/collider intact; derive smaller render-only tiles.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(WorldRegionGround))]
    public sealed class WorldGroundRenderChunks : MonoBehaviour
    {
        [Min(4)] public int intervalsPerTile = 16;
        [NonSerialized] GameObject generated;
        readonly List<Mesh> meshes = new List<Mesh>();
        Renderer nearRenderer, farRenderer;

        bool nearOff, farOff;
        public int TileCount => generated != null ? generated.transform.childCount : 0;

        void OnEnable() => Rebuild();
        void OnDisable() => Clear();
        void OnDestroy() => Clear();

        public void Rebuild()
        {
            Clear();
            var marker = GetComponent<WorldRegionGround>();
            var source = marker.ground;
            var near = transform.Find("Terrain");
            var far = transform.Find("Distant Ground");
            if (source == null || near == null || !source.isReadable) return;
            int nx = marker.cells.x * marker.nearIntervals, nz = marker.cells.y * marker.nearIntervals;
            if (source.vertexCount != (nx + 1) * (nz + 1))
            { Debug.LogError("Ground chunking requires the authored vertex grid.", this); return; }
            var template = near.GetComponent<MeshRenderer>();
            if (template == null) return;
            var vertices = source.vertices;
            var normals = source.normals;
            var uv = source.uv;
            var colors = source.colors;
            var tangents = source.tangents;
            var uv2 = source.uv2;
            int step = Mathf.Max(4, intervalsPerTile);
            generated = new GameObject("Ground render chunks") { hideFlags = HideFlags.DontSave };
            generated.transform.SetParent(near, false);
            try
            {
                for (int z = 0; z < nz; z += step)
                    for (int x = 0; x < nx; x += step)
                    {
                        int w = Mathf.Min(step, nx - x), d = Mathf.Min(step, nz - z), count = (w + 1) * (d + 1);
                        var v = new Vector3[count];
                        var n = normals.Length == vertices.Length ? new Vector3[count] : null;
                        var u = uv.Length == vertices.Length ? new Vector2[count] : null;
                        var c = colors.Length == vertices.Length ? new Color[count] : null;
                        var t = tangents.Length == vertices.Length ? new Vector4[count] : null;
                        var origin = vertices[z * (nx + 1) + x]; origin.y = 0;
                        for (int iz = 0; iz <= d; iz++) for (int ix = 0; ix <= w; ix++)
                        {
                            int a = (z + iz) * (nx + 1) + x + ix, b = iz * (w + 1) + ix;
                            v[b] = vertices[a] - origin;
                            if (n != null) n[b] = normals[a];
                            if (u != null) u[b] = uv[a];
                            if (c != null) c[b] = colors[a];
                            if (t != null) t[b] = tangents[a];
                        }
                        var indices = new int[w * d * 6];
                        for (int iz = 0; iz < d; iz++) for (int ix = 0; ix < w; ix++)
                        {
                            int a = iz * (w + 1) + ix, b = (iz * w + ix) * 6;
                            indices[b] = a; indices[b+1] = a+w+1; indices[b+2] = a+1;
                            indices[b+3] = a+1; indices[b+4] = a+w+1; indices[b+5] = a+w+2;
                        }
                        var mesh = new Mesh { name = $"Ground tile {x/step:00}_{z/step:00}", hideFlags = HideFlags.DontSave,
                            indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                        meshes.Add(mesh);
                        mesh.vertices = v; mesh.triangles = indices;
                        if (n != null) mesh.normals = n; else mesh.RecalculateNormals();
                        if (u != null) mesh.uv = u;
                        if (c != null) mesh.colors = c;
                        if (t != null) mesh.tangents = t;
                        mesh.RecalculateBounds();
                        var tile = new GameObject(mesh.name) { hideFlags = HideFlags.DontSave, layer = near.gameObject.layer };
                        tile.transform.SetParent(generated.transform, false); tile.transform.localPosition = origin;
                        tile.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var r = tile.AddComponent<MeshRenderer>();
                        r.sharedMaterials = template.sharedMaterials;
                        r.shadowCastingMode = template.shadowCastingMode; r.receiveShadows = template.receiveShadows;
                        r.lightProbeUsage = template.lightProbeUsage; r.reflectionProbeUsage = template.reflectionProbeUsage;
                        r.renderingLayerMask = template.renderingLayerMask;
                        r.lightmapIndex = template.lightmapIndex; r.lightmapScaleOffset = template.lightmapScaleOffset;

                        if(uv2.Length == vertices.Length) {
                            var tileUV2 = new Vector2[count];
                            for(int iz=0;iz<=d;iz++) for(int ix=0;ix<=w;ix++) tileUV2[iz*(w+1)+ix]=uv2[(z+iz)*(nx+1)+x+ix];
                            mesh.uv2=tileUV2;
                        }
                    }
                nearRenderer = template; nearOff = template.forceRenderingOff; template.forceRenderingOff = true;
                farRenderer = far != null ? far.GetComponent<Renderer>() : null;
                if (farRenderer != null) { farOff = farRenderer.forceRenderingOff; farRenderer.forceRenderingOff = true; }


            }
            catch { Clear(); throw; }
        }

        void Clear()
        {
            if (nearRenderer != null) nearRenderer.forceRenderingOff = nearOff;
            if (farRenderer != null) farRenderer.forceRenderingOff = farOff;

            nearRenderer = null; farRenderer = null;
            if (generated != null) { generated.SetActive(false); Dispose(generated); generated = null; }
            foreach (var mesh in meshes) if (mesh != null) Dispose(mesh);
            meshes.Clear();
        }
        static void Dispose(UnityEngine.Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}

