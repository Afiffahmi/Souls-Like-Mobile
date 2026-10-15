using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Nusantara.World.Editor
{
    public static class WorldRegionPaintAssets
    {
        public const string Root = "Assets/NusantaraWorld/RegionPaint";
        public static WorldDefinition World => AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);

        public static int OriginalRegion(WorldDefinition world, Vector2 p)
        {
            var c = world.WorldToChunk(new Vector3(p.x, 0, p.y));
            foreach (var r in world.regions)
                if (!r.paintOnly && !r.floatingIsland && c.x >= r.chunkOffset.x && c.y >= r.chunkOffset.y && c.x < r.chunkOffset.x + r.chunkGridSize.x && c.y < r.chunkOffset.y + r.chunkGridSize.y) return r.id;
            return 0;
        }

        public static WorldRegionPaintData Ensure(WorldDefinition world)
        {
            if (world == null || !world.regionalGroundPlanes) throw new InvalidOperationException("Open the regional ground world first.");
            if (world.regionPaint != null) return world.regionPaint;
            Region5EnvironmentAssets.EnsureFolder(Root);
            var data = AssetDatabase.LoadAssetAtPath<WorldRegionPaintData>(Root + "/MainlandRegions.asset");
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WorldRegionPaintData>();
                var mainland = world.regions.Where(r => !r.paintOnly && !r.floatingIsland).ToArray();
                float minX = mainland.Min(r => world.ChunkOrigin(r.chunkOffset).x), minZ = mainland.Min(r => world.ChunkOrigin(r.chunkOffset).z);
                float maxX = mainland.Max(r => world.ChunkOrigin(r.chunkOffset + r.chunkGridSize).x), maxZ = mainland.Max(r => world.ChunkOrigin(r.chunkOffset + r.chunkGridSize).z);
                data.bounds = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
                data.cellSize = world.chunkSize / 32;
                int width = Mathf.RoundToInt(data.bounds.width / data.cellSize), height = Mathf.RoundToInt(data.bounds.height / data.cellSize);
                data.map = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
                { name = "Mainland region ownership", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                foreach (var r in mainland) data.colors[r.id - 1] = r.groundPrefab.GetComponentInChildren<MeshRenderer>().sharedMaterial.GetColor("_Color");
                var pixels = new Color32[width * height];
                for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
                    pixels[z * width + x] = data.Pixel(OriginalRegion(world, new Vector2(minX + (x + .5f) * data.cellSize, minZ + (z + .5f) * data.cellSize)));
                data.map.SetPixels32(pixels); data.map.Apply(false);
                AssetDatabase.CreateAsset(data, Root + "/MainlandRegions.asset");
                AssetDatabase.AddObjectToAsset(data.map, data);
            }
            Undo.RecordObject(world, "Enable mainland region painting");
            world.regionPaint = data; EditorUtility.SetDirty(world);
            foreach (var r in world.regions.Where(r => !r.paintOnly && !r.floatingIsland))
            {
                var o = world.ChunkOrigin(r.chunkOffset);
                foreach (var material in r.groundPrefab.GetComponentsInChildren<MeshRenderer>(true).SelectMany(rend => rend.sharedMaterials).Distinct())
                {
                    WorldSurfacePaint.Prepare(material, new Rect(o.x, o.z, r.chunkGridSize.x * world.chunkSize, r.chunkGridSize.y * world.chunkSize), r.id);
                    material.SetTexture("_RegionMap", data.map);
                    material.SetVector("_RegionRect", new Vector4(data.bounds.x, data.bounds.y, data.bounds.width, data.bounds.height));
                    material.SetFloat("_RegionEnabled", 1); EditorUtility.SetDirty(material);
                }
            }
            AssetDatabase.SaveAssets(); return data;
        }

        public static bool Stamp(WorldDefinition world, WorldRegionPaintData data, Vector2 from, Vector2 to, float radius, int id, bool restore = false)
        {
            if (id < 1 || id > world.MainlandRegionCount || radius <= 0 || !float.IsFinite(radius)) return false;
            var map = data.map; var b = data.bounds; float s = data.cellSize;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(from.x,to.x)-radius-b.x)/s),0,map.width);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(from.y,to.y)-radius-b.y)/s),0,map.height);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(from.x,to.x)+radius-b.x)/s),0,map.width);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(from.y,to.y)+radius-b.y)/s),0,map.height);
            int width=x1-x0, height=z1-z0; if(width<=0||height<=0)return false;
            var pixels=map.GetPixels32(); bool changed=false;
            for(int z=z0;z<z1;z++)for(int x=x0;x<x1;x++)
            {
                int index=z*map.width+x;
                if(pixels[index].a<1||pixels[index].a>world.MainlandRegionCount)continue;
                var p=new Vector2(b.x+(x+.5f)*s,b.y+(z+.5f)*s);
                if(WorldGeographyProfile.SegmentDistance(p,from,to)>radius)continue;
                var target=data.Pixel(restore?OriginalRegion(world,p):id);
                if(pixels[index].Equals(target))continue;
                pixels[index]=target;changed=true;
            }
            if(changed){map.SetPixels32(pixels);map.Apply(false);data.CapturePixels();EditorUtility.SetDirty(data);EditorUtility.SetDirty(map);}
            return changed;
        }

        public static void Save(WorldRegionPaintData data, bool capturePixels = true)
        {
            if(data==null)return;
            if(capturePixels)data.CapturePixels();
            EditorUtility.SetDirty(data);EditorUtility.SetDirty(data.map);
            AssetDatabase.SaveAssetIfDirty(data);SceneView.RepaintAll();
        }

        public static bool RegionBounds(WorldRegionPaintData data,int id,out Rect bounds,out Vector2 point)
        {
            bounds=default;point=default;bool found=false;var pixels=data.map.GetPixels32();float s=data.cellSize;
            for(int z=0;z<data.map.height;z++)for(int x=0;x<data.map.width;x++)if(pixels[z*data.map.width+x].a==id)
            {
                var p=new Vector2(data.bounds.x+x*s,data.bounds.y+z*s);
                if(!found){bounds=new Rect(p,Vector2.one*s);point=p+Vector2.one*s*.5f;found=true;}
                else bounds=Rect.MinMaxRect(Mathf.Min(bounds.xMin,p.x),Mathf.Min(bounds.yMin,p.y),Mathf.Max(bounds.xMax,p.x+s),Mathf.Max(bounds.yMax,p.y+s));
            }
            if(found)
            {
                float nearest=float.MaxValue;
                for(int z=0;z<data.map.height;z++)for(int x=0;x<data.map.width;x++)if(pixels[z*data.map.width+x].a==id)
                {
                    var p=new Vector2(data.bounds.x+(x+.5f)*s,data.bounds.y+(z+.5f)*s);
                    float distance=(p-bounds.center).sqrMagnitude;
                    if(distance<nearest){nearest=distance;point=p;}
                }
            }
            return found;
        }

        public static void FrameMainland()
        {
            var world=World;if(world?.regionPaint==null)return;
            WorldSceneBookmarks.ShowWholeWorld();
            var b=world.regionPaint.bounds;
            SceneView.lastActiveSceneView?.LookAt(new Vector3(b.center.x,42,b.center.y),Quaternion.Euler(90,0,0),Mathf.Max(b.width,b.height)*.57f,true,true);
        }
    }
}
