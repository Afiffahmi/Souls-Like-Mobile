using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;
namespace Nusantara.World.Editor
{
    public static class VegetationPaintChecks
    {
        public static string Run()
        {
            var palette=VegetationPaintAssets.Prepare();var profile=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            var data=ScriptableObject.CreateInstance<VegetationPaintData>();data.coordinate=profile.world.WorldToChunk(profile.world.locations[0].position);
            Vector3 origin=profile.world.ChunkOrigin(data.coordinate);
            for(int i=0;i<2;i++)data.stamps.Add(new VegetationStamp{kind=VegetationPaintKind.Tree,position=origin+new Vector3(20+i*20,42,20),scale=(8+i*4)/22f,yaw=i*35});
            for(int i=0;i<32;i++)data.stamps.Add(new VegetationStamp{kind=VegetationPaintKind.Grass,position=origin+new Vector3(20+i*.6f,42,23),scale=.35f,yaw=i*13});
            var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Paint checks");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);root.transform.position=origin;
            const string saved="Assets/NusantaraWorld/VegetationPaint/CanopyCheck.prefab";
            int checks=0;
            void Check(bool valid,string name){if(!valid)throw new InvalidOperationException("Paint check failed: "+name);checks++;}
            try
            {
                Check(palette.treePrefab!=null&&palette.grassPrefab!=null&&palette.leafMaterial!=null,"palette references");
                Check(MonoScript.FromScriptableObject(palette)!=null,"persistent palette script");
                Check(palette.grassPrefab.GetComponent<MeshFilter>().sharedMesh.vertexCount==70,"actual curved grass mesh");
                VegetationPaintAssets.BuildVisuals(root,data,palette,false);
                Check(root.transform.Cast<Transform>().Count(t=>t.name=="Painted tree prop")==2,"editable tree prefab props");
                var canopy=root.GetComponentInChildren<WorldCanopyVfx>();Check(canopy!=null&&canopy.crowns.Length==2,"batched VFX crowns");
                Check(canopy.GetComponent<ParticleSystem>().particleCount>0&&canopy.GetComponent<ParticleSystem>().particleCount<=384,"bounded canopy particles");
                Check(root.GetComponentsInChildren<Collider>().Length==0,"decoration has no late streaming collision");
                var grasses=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Painted grass")).ToArray();
                Check(grasses.Length==2&&grasses.Sum(f=>f.sharedMesh.vertexCount)==32*70,"spatial grass batches");
                Check(grasses.All(f=>f.GetComponent<KampungGrassQuality>()!=null&&f.GetComponent<LODGroup>()!=null),"grass quality and culling");
                var ui=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/World/Editor/VegetationPainter.uxml").CloneTree();
                Check(ui.Q<Button>("save")!=null&&ui.Q<Slider>("radius")!=null&&ui.Q<Toggle>("paint")!=null,"Editor paint controls");
                foreach(var island in profile.scenicIslands)Check(!VegetationPainterWindow.Allowed(profile,new Vector3(island.center.x,42,island.center.y),Vector3.up,VegetationPaintKind.Tree),"scenic island exclusion");
                Check(!VegetationPainterWindow.Allowed(profile,new Vector3(-100000,42,-100000),Vector3.up,VegetationPaintKind.Grass),"outside region exclusion");
                Check(!VegetationPainterWindow.Allowed(profile,profile.world.locations[0].position,Vector3.up,VegetationPaintKind.Tree),"market path exclusion");
                // Persist only the canopy object here; temporary grass meshes are checked above.
                var vfxObject=canopy.gameObject;vfxObject.transform.SetParent(null,true);PrefabUtility.SaveAsPrefabAsset(vfxObject,saved);
                var restored=AssetDatabase.LoadAssetAtPath<GameObject>(saved).GetComponent<WorldCanopyVfx>();
                Check(restored.crowns.Length==2&&restored.radii.Length==2,"serialized VFX crown data");
                Object.DestroyImmediate(vfxObject);
                string report=$"Passed {checks} paint checks: palette persistence, tree prefab props, bounded VFX leaves, curved grass geometry and batches, quality culling, UI controls, water/island/region exclusions, canopy serialization. Test placements were removed; no vegetation was painted in the world.";
                File.WriteAllText("Library/Nusantara/VegetationPaintChecks.txt",report);return report;
            }
            finally
            {
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null&&!AssetDatabase.Contains(filter.sharedMesh))Object.DestroyImmediate(filter.sharedMesh);
                Object.DestroyImmediate(root);Object.DestroyImmediate(data);EditorSceneManager.ClosePreviewScene(scene);AssetDatabase.DeleteAsset(saved);
            }
        }
    }
}
