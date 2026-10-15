using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nusantara.World
{
    public enum WorldWaterKind { River, Sea }

    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Nusantara/Painted Water")]
    public sealed class WorldPaintedWater : MonoBehaviour
    {
        public const float CellSize = 1f;
        [HideInInspector] public Material waterMaterial;
        [HideInInspector] public Material shorelineMaterial;
        [SerializeField, HideInInspector] WorldWaterKind waterKind;
        [SerializeField, HideInInspector] bool waterKindAssigned;
        MaterialPropertyBlock kindProperties;
        public WorldWaterKind Kind => waterKindAssigned ? waterKind :
            waterMaterial && waterMaterial.HasProperty("_ShoreEnabled") && waterMaterial.GetFloat("_ShoreEnabled")>.5f ? WorldWaterKind.Sea : WorldWaterKind.River;
        public void SetKind(WorldWaterKind kind) { waterKind=kind; waterKindAssigned=true; }
        public void ApplyWaterKind()
        {
            var renderer=GetComponent<MeshRenderer>();
            if (!renderer) return;
            kindProperties ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(kindProperties);
            float shore=Kind==WorldWaterKind.Sea && waterMaterial && waterMaterial.HasProperty("_ShoreEnabled") ? waterMaterial.GetFloat("_ShoreEnabled") : 0;
            kindProperties.SetFloat("_ShoreEnabled",shore);
            renderer.SetPropertyBlock(kindProperties);
            bool wetEnabled=Kind==WorldWaterKind.Sea && cells.Count>0;
            var wet=transform.Find("Shoreline Wet Sand");
            if (wet && wet.gameObject.activeSelf!=wetEnabled) wet.gameObject.SetActive(wetEnabled);
        }
        [HideInInspector] public List<Vector2Int> cells = new List<Vector2Int>();
        Mesh generated;
        HashSet<Vector2Int> occupied;
        List<Vector2Int> cachedCells;
        int cachedCount;
        bool geometryNeedsRebuild=true;
        readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>();
        readonly List<int> triangles=new List<int>();
        readonly Dictionary<Vector2Int,int> indices=new Dictionary<Vector2Int,int>();
        public int CellCount=>cells.Count;
        void OnEnable()=>Rebuild();
        void OnValidate(){occupied=null;geometryNeedsRebuild=true;}
        void OnDisable()
        {
            if(generated!=null)
            {
                if(GetComponent<MeshFilter>().sharedMesh==generated)GetComponent<MeshFilter>().sharedMesh=null;
                if(Application.isPlaying)Destroy(generated);else DestroyImmediate(generated);
                generated=null;
            }
            occupied=null;vertices.Clear();normals.Clear();uv.Clear();triangles.Clear();indices.Clear();geometryNeedsRebuild=true;
        }
        void EnsureCells()
        {
            if(occupied!=null&&ReferenceEquals(cachedCells,cells)&&cachedCount==cells.Count)return;
            occupied=new HashSet<Vector2Int>(cells);cachedCells=cells;cachedCount=cells.Count;geometryNeedsRebuild=true;
        }
        public bool Paint(Vector3 worldPoint,float radius,bool erase,bool rebuild=true)
            =>PaintStroke(new[]{worldPoint},radius,erase,rebuild);
        public bool PaintStroke(IReadOnlyList<Vector3> worldPoints,float radius,bool erase,bool rebuild=true)
        {
            if(!float.IsFinite(radius)||radius<=0)return false;
            EnsureCells();bool changed=false;var removed=erase?new HashSet<Vector2Int>():null;
            foreach(var worldPoint in worldPoints)
            {
                if(!float.IsFinite(worldPoint.x)||!float.IsFinite(worldPoint.z))continue;
                var point=transform.InverseTransformPoint(worldPoint);
                int x0=Mathf.FloorToInt((point.x-radius)/CellSize),x1=Mathf.FloorToInt((point.x+radius)/CellSize);
                int z0=Mathf.FloorToInt((point.z-radius)/CellSize),z1=Mathf.FloorToInt((point.z+radius)/CellSize);
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                {
                    if(new Vector2((x+.5f)*CellSize-point.x,(z+.5f)*CellSize-point.z).sqrMagnitude>radius*radius)continue;
                    var key=new Vector2Int(x,z);
                    if(erase)
                    {
                        if(!occupied.Remove(key))continue;
                        removed.Add(key);geometryNeedsRebuild=true;changed=true;
                    }
                    else if(occupied.Add(key))
                    {
                        cells.Add(key);if(!geometryNeedsRebuild)AddQuad(key);changed=true;
                    }
                }
            }
            if(!changed)return false;
            if(removed!=null)cells.RemoveAll(c=>removed.Contains(c));
            cachedCount=cells.Count;
            if(rebuild)Rebuild(false);return true;
        }
        // Sort once after a complete stroke, instead of copying and sorting the entire area for every stamp.
        public void FinishEditing()=>cells.Sort((a,b)=>a.y==b.y?a.x.CompareTo(b.x):a.y.CompareTo(b.y));
        int Vertex(Vector2Int point)
        {
            if(indices.TryGetValue(point,out int index))return index;
            index=vertices.Count;indices.Add(point,index);vertices.Add(new Vector3(point.x*CellSize,0,point.y*CellSize));normals.Add(Vector3.up);uv.Add(new Vector2(point.x,point.y));return index;
        }
        void AddQuad(Vector2Int cell)
        {
            int a=Vertex(cell),b=Vertex(cell+Vector2Int.up),c=Vertex(cell+Vector2Int.one),d=Vertex(cell+Vector2Int.right);
            triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(a);triangles.Add(c);triangles.Add(d);
        }
        public void Rebuild(bool reloadCells=true)
        {
            if(reloadCells){occupied=null;geometryNeedsRebuild=true;}
            EnsureCells();
            if(generated==null)generated=new Mesh{name="Painted moving water",hideFlags=HideFlags.DontSave};
            if(geometryNeedsRebuild)
            {
                vertices.Clear();normals.Clear();uv.Clear();triangles.Clear();indices.Clear();
                foreach(var cell in cells)AddQuad(cell);geometryNeedsRebuild=false;
            }
            generated.Clear();generated.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            generated.SetVertices(vertices);generated.SetNormals(normals);generated.SetUVs(0,uv);generated.SetTriangles(triangles,0);
            generated.RecalculateBounds();var bounds=generated.bounds;bounds.Expand(new Vector3(0,4,0));generated.bounds=bounds;
            GetComponent<MeshFilter>().sharedMesh=generated;var renderer=GetComponent<MeshRenderer>();
            if(waterMaterial!=null)renderer.sharedMaterial=waterMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            ApplyWaterKind();
        }
    }
}
