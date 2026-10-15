using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Nusantara.World.Editor
{
    public static class WorldShorelineBaker
    {
        [MenuItem("Tools/Nusantara/Water/Rebake Selected Shoreline")]
        public static void RebuildSelected()
        {
            var water = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<WorldPaintedWater>() : null;
            if (!water) throw new InvalidOperationException("Select the Painted Water object to bake its shoreline.");
            Bake(water);
        }

        public static string Bake(WorldPaintedWater water, bool extendWashZone=false)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Bake in Edit mode.");
            if (water.Kind != WorldWaterKind.Sea) throw new InvalidOperationException("Choose Sea in the Water Brush before creating shoreline foam.");
            if (water.CellCount == 0) throw new InvalidOperationException("The water area is empty.");
            Physics.SyncTransforms();
            var bounds = water.GetComponent<Renderer>().bounds;
            // Only actual ground colliders contribute: piers, houses, rocks and characters do not become beaches.
            var grounds = Object.FindObjectsByType<MeshCollider>().Where(c => c.name == "Terrain" && c.sharedMesh &&
                c.bounds.max.x >= bounds.min.x && c.bounds.min.x <= bounds.max.x &&
                c.bounds.max.z >= bounds.min.z && c.bounds.min.z <= bounds.max.z).ToArray();
            if (grounds.Length == 0) throw new InvalidOperationException("No loaded Terrain mesh colliders overlap this water.");
            // Extend only the narrow wash zone into missing painted cells. This hides brush-shaped
            // notches at high water without growing the distant ocean boundary or filling deep gaps.
            if (extendWashZone)
            {
            Undo.RecordObject(water,"Extend painted shoreline wash zone");
            var expanded = new HashSet<Vector2Int>(water.cells);
            var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            for (int pass=0; pass<4; pass++)
            {
                var candidates = new HashSet<Vector2Int>();
                foreach (var cell in expanded) foreach (var dir in directions)
                    if (!expanded.Contains(cell+dir)) candidates.Add(cell+dir);
                foreach (var candidate in candidates)
                {
                    var point = water.transform.TransformPoint(new Vector3((candidate.x+.5f)*WorldPaintedWater.CellSize,0,(candidate.y+.5f)*WorldPaintedWater.CellSize));
                    bool washZone=false;
                    foreach (var collider in grounds)
                    {
                        var b=collider.bounds;
                        if (point.x<b.min.x || point.x>b.max.x || point.z<b.min.z || point.z>b.max.z) continue;
                        if (collider.Raycast(new Ray(new Vector3(point.x,b.max.y+10,point.z),Vector3.down),out var hit,b.size.y+20)
                            && Mathf.Abs(hit.point.y-water.transform.position.y)<1.4f) { washZone=true; break; }
                    }
                    if (washZone) { expanded.Add(candidate); water.cells.Add(candidate); }
                }
            }
            water.FinishEditing();
            }
            water.Rebuild();
            bounds=water.GetComponent<Renderer>().bounds;
            float step = Mathf.Max(1f, Mathf.Max(bounds.size.x, bounds.size.z) / 1534f);
            int width = Mathf.CeilToInt(bounds.size.x / step) + 2;
            int height = Mathf.CeilToInt(bounds.size.z / step) + 2;
            float minX = bounds.min.x - step, minZ = bounds.min.z - step;
            var rect = new Vector4(minX, minZ, width * step, height * step);
            float level = water.transform.position.y;
            var cells = new HashSet<Vector2Int>(water.cells);
            var pixels = new Color[width * height];
            int valid = 0;
            for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
            {
                float wx = minX + (x + .5f) * step, wz = minZ + (z + .5f) * step;
                float ground = float.NegativeInfinity;
                foreach (var collider in grounds)
                {
                    var b = collider.bounds;
                    if (wx < b.min.x || wx > b.max.x || wz < b.min.z || wz > b.max.z) continue;
                    if (collider.Raycast(new Ray(new Vector3(wx, b.max.y + 10, wz), Vector3.down), out var hit, b.size.y + 20))
                        ground = Mathf.Max(ground, hit.point.y);
                }
                var local = water.transform.InverseTransformPoint(new Vector3(wx, level, wz));
                bool painted = cells.Contains(new Vector2Int(Mathf.FloorToInt(local.x / WorldPaintedWater.CellSize), Mathf.FloorToInt(local.z / WorldPaintedWater.CellSize)));
                bool found = !float.IsNegativeInfinity(ground);
                if (found) valid++;
                pixels[z * width + x] = new Color(found ? level - ground : 100, found ? 1 : 0, .1f, painted ? 1 : 0);
            }
            for (int z = 1; z < height-1; z++) for (int x = 1; x < width-1; x++)
            {
                int i = z * width + x;
                if (pixels[i].g < .5f) continue;
                float dx = pixels[i-1].g * pixels[i+1].g > .5f ? (pixels[i+1].r-pixels[i-1].r)/(2*step) : 0;
                float dz = pixels[i-width].g * pixels[i+width].g > .5f ? (pixels[i+width].r-pixels[i-width].r)/(2*step) : 0;
                pixels[i].b = Mathf.Max(.09f, Mathf.Sqrt(dx*dx+dz*dz));
            }
            string root = "Assets/NusantaraWorld/Water";
            if (!AssetDatabase.IsValidFolder(root)) AssetDatabase.CreateFolder("Assets/NusantaraWorld", "Water");
            // Stable per-area path; rebaking preserves GUIDs and material references.
            var previousSea=water.shorelineMaterial;
            if (!previousSea && water.waterMaterial && water.waterMaterial.GetFloat("_ShoreEnabled")>.5f) previousSea=water.waterMaterial;
            string previousPath=previousSea ? AssetDatabase.GetAssetPath(previousSea) : "";
            string folder=previousPath.StartsWith(root+"/") ? System.IO.Path.GetDirectoryName(previousPath).Replace('\\','/') : "";
            if (string.IsNullOrEmpty(folder))
            {
                string key="Shore_"+Guid.NewGuid().ToString("N");
                AssetDatabase.CreateFolder(root,key); folder=root+"/"+key;
            }
            string mapPath = folder + "/BeachDepth.asset";
            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(mapPath);
            if (!map) { map = new Texture2D(width,height,TextureFormat.RGBAHalf,false,true); AssetDatabase.CreateAsset(map,mapPath); }
            else map.Reinitialize(width,height,TextureFormat.RGBAHalf,false);
            map.name = "Beach depth, valid ground, slope, painted coverage";
            map.wrapMode = TextureWrapMode.Clamp; map.filterMode = FilterMode.Bilinear;
            map.SetPixels(pixels); map.Apply(false,false); EditorUtility.SetDirty(map);

            string materialPath = folder + "/Sea Shoreline.mat";
            var sea = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!sea)
            {
                sea = new Material(water.waterMaterial) { name = "Sea Shoreline" };
                sea.SetFloat("_WaveScale",.085f); sea.SetFloat("_WaveHeight",.12f);
                sea.SetFloat("_CrestStrength",.18f); sea.SetFloat("_Gloss",.4f);
                sea.SetColor("_BaseColor", new Color(.18f,.68f,.73f));
                sea.SetColor("_DeepColor", new Color(.075f,.42f,.52f));
                sea.SetFloat("_ShoreAmplitude",.45f); sea.SetFloat("_ShorePeriod",4.2f);
                sea.SetFloat("_ShoreFoamWidth",.85f);
                AssetDatabase.CreateAsset(sea,materialPath);
            }
            sea.SetFloat("_ShoreEnabled",1); sea.SetFloat("_ShoreLevel",level);
            sea.SetTexture("_ShoreMap",map); sea.SetVector("_ShoreRect",rect); EditorUtility.SetDirty(sea);
            water.shorelineMaterial=sea; water.waterMaterial=sea; water.Rebuild(); EditorUtility.SetDirty(water);

            // Reuse the exact ground triangles so the wet sand sits flush, including sculpted slopes.
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            foreach (var collider in grounds)
            {
                var mesh = collider.sharedMesh; var source = mesh.vertices; var indices = mesh.triangles;
                var lookup = new Dictionary<int,int>();
                for (int i=0;i<indices.Length;i+=3)
                {
                    Vector3 a=collider.transform.TransformPoint(source[indices[i]]), b=collider.transform.TransformPoint(source[indices[i+1]]), c=collider.transform.TransformPoint(source[indices[i+2]]);
                    if (Mathf.Min(a.y,b.y,c.y)>level+2 || Mathf.Max(a.y,b.y,c.y)<level-2) continue;
                    if (Mathf.Min(a.x,b.x,c.x)>bounds.max.x || Mathf.Max(a.x,b.x,c.x)<bounds.min.x || Mathf.Min(a.z,b.z,c.z)>bounds.max.z || Mathf.Max(a.z,b.z,c.z)<bounds.min.z) continue;
                    for (int k=0;k<3;k++)
                    {
                        int index=indices[i+k];
                        if (!lookup.TryGetValue(index,out int target))
                        {
                            target=vertices.Count; lookup.Add(index,target);
                            vertices.Add(water.transform.InverseTransformPoint(collider.transform.TransformPoint(source[index])));
                        }
                        triangles.Add(target);
                    }
                }
            }
            string meshPath=folder+"/WetSandMesh.asset";
            var wetMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (!wetMesh) { wetMesh=new Mesh {name="Coastal wet sand"}; AssetDatabase.CreateAsset(wetMesh,meshPath); }
            wetMesh.Clear(); wetMesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            wetMesh.SetVertices(vertices); wetMesh.SetTriangles(triangles,0); wetMesh.RecalculateBounds(); EditorUtility.SetDirty(wetMesh);
            string wetPath=folder+"/Wet Sand.mat";
            var wet=AssetDatabase.LoadAssetAtPath<Material>(wetPath);
            if (!wet) { wet=new Material(Shader.Find("Nusantara/Shoreline Wet Sand")); AssetDatabase.CreateAsset(wet,wetPath); }
            var child=water.transform.Find("Shoreline Wet Sand");
            if (!child)
            {
                var go=new GameObject("Shoreline Wet Sand"); Undo.RegisterCreatedObjectUndo(go,"Add wet sand");
                go.transform.SetParent(water.transform,false); child=go.transform;
            }
            var filter=child.GetComponent<MeshFilter>(); if (!filter) filter=Undo.AddComponent<MeshFilter>(child.gameObject); filter.sharedMesh=wetMesh;
            var renderer=child.GetComponent<MeshRenderer>(); if (!renderer) renderer=Undo.AddComponent<MeshRenderer>(child.gameObject); renderer.sharedMaterial=wet;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            var sync=child.GetComponent<WorldShorelineWetSand>(); if (!sync) sync=Undo.AddComponent<WorldShorelineWetSand>(child.gameObject); sync.seaMaterial=sea;
            water.ApplyWaterKind();
            EditorUtility.SetDirty(sync); EditorUtility.SetDirty(filter); EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssetIfDirty(map); AssetDatabase.SaveAssetIfDirty(sea); AssetDatabase.SaveAssetIfDirty(wetMesh); AssetDatabase.SaveAssetIfDirty(wet);
            EditorSceneManager.MarkSceneDirty(water.gameObject.scene); SceneView.RepaintAll();
            return $"Baked {width}x{height} shoreline map ({valid} ground samples), {triangles.Count/3} wet-sand triangles. {folder}";
        }
    }
}

