using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nusantara.World.Editor
{
    // The brush is in world space, so every adjoining ground touched by a dab receives it.
    public static class WorldSurfaceBrush
    {
        public static bool StampAcrossGrounds(MeshCollider primary, Vector3 center, float radius,
            float strength, float hardness, WorldSurfaceStyle style, int slot, bool erase,
            ISet<Texture2D> recorded, ISet<Material> materials)
        {
            var owner=primary.GetComponentInParent<WorldRegionGround>();
            if(owner==null)return false;
            bool changed=false;
            foreach(var ground in Object.FindObjectsByType<WorldRegionGround>(FindObjectsSortMode.None))
            {
                if(ground.gameObject.scene!=primary.gameObject.scene)continue;
                // Floating islands have independent surfaces, even where their X/Z footprints overlap.
                if((owner.region>=8||ground.region>=8)&&owner!=ground)continue;
                var surface=ground.transform.Find("Terrain");
                var collider=surface!=null?surface.GetComponent<MeshCollider>():null;
                if(collider==null||!collider.enabled||SceneVisibilityManager.instance.IsHidden(collider.gameObject))continue;
                var bounds=WorldSurfacePaint.Bounds(collider);
                var point=new Vector2(center.x,center.z);
                var nearest=new Vector2(Mathf.Clamp(point.x,bounds.xMin,bounds.xMax),Mathf.Clamp(point.y,bounds.yMin,bounds.yMax));
                if((nearest-point).sqrMagnitude>radius*radius)continue;
                var renderer=surface.GetComponent<MeshRenderer>();var material=renderer!=null?renderer.sharedMaterial:null;
                if(material==null)continue;
                if(erase&&(material.shader.name!=WorldSurfacePaint.ShaderName||material.GetTexture("_SurfacePaint")==null))continue;
                var map=WorldSurfacePaint.Prepare(material,bounds,ground.region);
                var detail=WorldSurfaceDetails.Prepare(material,map);
                materials.Add(material);
                if(recorded.Add(map))Undo.RegisterCompleteObjectUndo(map,"Paint ground surface");
                if(recorded.Add(detail))Undo.RegisterCompleteObjectUndo(detail,"Paint fine ground surface");
                bool modified=WorldSurfaceDetails.Stamp(detail,bounds,point,radius,strength,hardness,slot,erase);
                if(erase)modified|=WorldSurfacePaint.Stamp(map,bounds,point,radius,strength,hardness,style,true);
                if(modified){detail.Apply(false);EditorUtility.SetDirty(detail);if(erase){map.Apply();EditorUtility.SetDirty(map);}changed=true;}
            }
            return changed;
        }
    }
}
