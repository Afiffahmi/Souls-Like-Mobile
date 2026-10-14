using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nusantara.World.Editor
{
    public static class WorldRoadCurve
    {
        public static List<Vector3> Sample(IReadOnlyList<Vector3> points, float spacing, bool smooth)
        {
            var result=new List<Vector3>();
            if(points.Count==0)return result;
            result.Add(points[0]);spacing=Mathf.Max(.1f,spacing);
            for(int i=0;i<points.Count-1;i++)
            {
                var a=points[i];var b=points[i+1];
                var previous=points[Mathf.Max(0,i-1)];var next=points[Mathf.Min(points.Count-1,i+2)];
                // Clamp tangent lengths so a short bend beside a long segment cannot loop back.
                float length=Vector3.Distance(a,b);
                var ta=Vector3.ClampMagnitude((b-previous)*.5f,length);
                var tb=Vector3.ClampMagnitude((next-a)*.5f,length);
                int steps=Mathf.Clamp(Mathf.CeilToInt((length+ta.magnitude+tb.magnitude)/spacing),1,4096);
                for(int j=1;j<=steps;j++)
                {
                    float t=j/(float)steps,t2=t*t,t3=t2*t;
                    result.Add(smooth?(2*t3-3*t2+1)*a+(t3-2*t2+t)*ta+(-2*t3+3*t2)*b+(t3-t2)*tb:Vector3.Lerp(a,b,t));
                }
            }
            return result;
        }

        public static float[] Coverage(int width,int height,Rect bounds,IReadOnlyList<Vector3> path,float radius,float hardness)
        {
            var mask=new float[width*height];
            if(path.Count<2||radius<=0||bounds.width<=0||bounds.height<=0)return mask;
            for(int i=1;i<path.Count;i++)
            {
                var a=new Vector2(path[i-1].x,path[i-1].z);var b=new Vector2(path[i].x,path[i].z);
                int x0=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,b.x)-radius-bounds.xMin)/bounds.width*width),0,width);
                int x1=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x,b.x)+radius-bounds.xMin)/bounds.width*width),0,width);
                int y0=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y,b.y)-radius-bounds.yMin)/bounds.height*height),0,height);
                int y1=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.y,b.y)+radius-bounds.yMin)/bounds.height*height),0,height);
                var delta=b-a;float sq=delta.sqrMagnitude;
                for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
                {
                    var p=new Vector2(bounds.xMin+(x+.5f)*bounds.width/width,bounds.yMin+(y+.5f)*bounds.height/height);
                    var nearest=a+delta*(sq>0?Mathf.Clamp01(Vector2.Dot(p-a,delta)/sq):0);
                    int n=y*width+x;
                    mask[n]=Mathf.Max(mask[n],WorldTerrainBrushMath.Falloff(Vector2.Distance(p,nearest),radius,hardness));
                }
            }
            return mask;
        }

        public static int Apply(MeshCollider primary,IReadOnlyList<Vector3> path,float radius,float strength,float hardness,
            WorldSurfaceStyle style,int slot,bool erase,ISet<Texture2D> recorded,ISet<Material> materials)
        {
            var owner=primary.GetComponentInParent<WorldRegionGround>();
            if(owner==null)throw new InvalidOperationException("Place the road on regional ground.");
            int changed=0;
            foreach(var root in primary.gameObject.scene.GetRootGameObjects())
            foreach(var ground in root.GetComponentsInChildren<WorldRegionGround>())
            {
                if((owner.region>=8||ground.region>=8)&&owner!=ground)continue;
                var surface=ground.transform.Find("Terrain");var collider=surface!=null?surface.GetComponent<MeshCollider>():null;
                if(collider==null||!collider.enabled||SceneVisibilityManager.instance.IsHidden(collider.gameObject))continue;
                var material=surface.GetComponent<MeshRenderer>()?.sharedMaterial;if(material==null)continue;
                var bounds=WorldSurfacePaint.Bounds(collider);
                var existing=material.HasProperty("_SurfacePaint")?material.GetTexture("_SurfacePaint") as Texture2D:null;
                int width=existing!=null?existing.width:1024,height=existing!=null?existing.height:1024;
                var mask=Coverage(width,height,bounds,path,radius,hardness);
                if(!Array.Exists(mask,v=>v>0))continue;
                if(erase&&existing==null)continue;
                var legacy=WorldSurfacePaint.Prepare(material,bounds,ground.region);
                var detail=WorldSurfaceDetails.Prepare(material,legacy);
                if(detail.width!=width||detail.height!=height)throw new InvalidOperationException("Ground coverage map dimensions do not match.");
                if(recorded.Add(detail))Undo.RegisterCompleteObjectUndo(detail,"Apply curved road");
                if(erase&&recorded.Add(legacy))Undo.RegisterCompleteObjectUndo(legacy,"Erase curved road");
                materials.Add(material);var pixels=detail.GetPixels();var old=erase?legacy.GetPixels():null;
                for(int i=0;i<mask.Length;i++)if(mask[i]>0)
                {
                    float amount=mask[i]*Mathf.Clamp01(strength);
                    pixels[i]=WorldSurfaceDetails.Blend(pixels[i],slot,amount,erase);
                    if(erase)old[i]*=1-amount;
                }
                detail.SetPixels(pixels);detail.Apply(false);EditorUtility.SetDirty(detail);
                if(erase){legacy.SetPixels(old);legacy.Apply();EditorUtility.SetDirty(legacy);}
                changed++;
            }
            return changed;
        }
    }
}
