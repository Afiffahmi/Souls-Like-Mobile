using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nusantara.World.Editor
{
    public sealed class WorldGenerationGeometry
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<int> triangles = new List<int>();
        readonly List<Vector2> uv = new List<Vector2>();
        public bool Empty => vertices.Count == 0;
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int n = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uv.Add(new Vector2(a.x, a.z + a.y) / 4); uv.Add(new Vector2(b.x, b.z + b.y) / 4);
            uv.Add(new Vector2(c.x, c.z + c.y) / 4); uv.Add(new Vector2(d.x, d.z + d.y) / 4);
            triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
            triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
        }
        public void Box(Vector3 center, Vector3 size, Quaternion rotation)
        {
            Vector3 V(float x, float y, float z) => center + rotation * Vector3.Scale(new Vector3(x,y,z), size * .5f);
            var a=V(-1,-1,-1); var b=V(-1,-1,1); var c=V(1,-1,1); var d=V(1,-1,-1);
            var e=V(-1,1,-1); var f=V(-1,1,1); var g=V(1,1,1); var h=V(1,1,-1);
            Quad(e,f,g,h); Quad(d,c,b,a); Quad(a,b,f,e); Quad(b,c,g,f); Quad(c,d,h,g); Quad(d,a,e,h);
        }
        public Mesh Mesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        public static Mesh Simplify(Mesh original, int divisions = 14)
        {
            // Vertex clustering is used only for the distant LOD; the source model stays intact.
            Vector3[] source = original.vertices; var originalUV = original.uv;
            float cell = Mathf.Max(original.bounds.size.x, original.bounds.size.y, original.bounds.size.z) / divisions;
            if (cell <= .00001f) return Object.Instantiate(original);
            var map = new Dictionary<Vector3Int,int>(); var counts = new List<int>();
            var positions = new List<Vector3>(); var coords = new List<Vector2>(); var remap = new int[source.Length];
            for (int i=0;i<source.Length;i++)
            {
                var p=source[i]/cell; var key=new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));
                if(!map.TryGetValue(key,out int index)) { index=positions.Count; map.Add(key,index); positions.Add(Vector3.zero); coords.Add(Vector2.zero); counts.Add(0); }
                remap[i]=index; positions[index]+=source[i]; coords[index]+=originalUV.Length==source.Length?originalUV[i]:Vector2.zero; counts[index]++;
            }
            for(int i=0;i<positions.Count;i++) { positions[i]/=counts[i]; coords[i]/=counts[i]; }
            var result=new Mesh { name=original.name+"_DistanceLOD", indexFormat=positions.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16 };
            result.SetVertices(positions); result.SetUVs(0,coords); result.subMeshCount=original.subMeshCount;
            for(int s=0;s<original.subMeshCount;s++)
            {
                var input=original.GetTriangles(s); var output=new List<int>();
                for(int i=0;i<input.Length;i+=3) { int a=remap[input[i]],b=remap[input[i+1]],c=remap[input[i+2]]; if(a==b||a==c||b==c)continue; output.Add(a);output.Add(b);output.Add(c); }
                result.SetTriangles(output,s);
            }
            result.RecalculateNormals(); result.RecalculateBounds(); return result;
        }
        public static void AddSkirt(Mesh mesh, int intervals, float depth)
        {
            var v = new List<Vector3>(mesh.vertices); var n = new List<Vector3>(mesh.normals); var u = new List<Vector2>(mesh.uv); var t = new List<int>(mesh.triangles);
            int side=intervals+1;
            void Edge(int a,int b) { int c=v.Count; v.Add(v[a]-Vector3.up*depth);v.Add(v[b]-Vector3.up*depth); n.Add(n[a]);n.Add(n[b]);u.Add(u[a]);u.Add(u[b]); t.Add(a);t.Add(c);t.Add(b);t.Add(b);t.Add(c);t.Add(c+1); }
            for(int i=0;i<intervals;i++) { Edge(i,i+1);Edge(intervals*side+i+1,intervals*side+i);Edge((i+1)*side,i*side);Edge(i*side+intervals,(i+1)*side+intervals); }
            mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,u);mesh.SetTriangles(t,0);mesh.RecalculateBounds();
        }
    }
}
