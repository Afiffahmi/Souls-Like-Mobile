using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nusantara.World.Editor
{
    public static class WorldSceneBookmarks
    {
        public static int VisibleRegion=>SessionState.GetInt("Nusantara.VisibleEditorRegion",-1);
        const string PreviewName="World preview (Editor only)";
        [MenuItem("Tools/Nusantara/Preview/Region 1 - Player start")]
        public static void RegionOne() { var w=AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);LoadPreview(w.playerStart,true); }
        [MenuItem("Tools/Nusantara/Preview/Brush Region 1")]
        public static void BrushRegionOne() => RegionCenter(1);
        [MenuItem("Tools/Nusantara/Preview/Brush Region 2")]
        public static void BrushRegionTwo() => RegionCenter(2);
        [MenuItem("Tools/Nusantara/Preview/Brush Region 3")]
        public static void BrushRegionThree() => RegionCenter(3);
        [MenuItem("Tools/Nusantara/Preview/Brush Region 4")]
        public static void BrushRegionFour() => RegionCenter(4);
        [MenuItem("Tools/Nusantara/Preview/Brush Region 5")]
        public static void BrushRegionFive() => RegionCenter(5);
        [MenuItem("Tools/Nusantara/Preview/Brush Region 6")]
        public static void BrushRegionSix() => RegionCenter(6);
        [MenuItem("Tools/Nusantara/Preview/Brush Region 7 - Raja Bersiong")]
        public static void BrushRegionSeven() => RegionCenter(7);
        [MenuItem("Tools/Nusantara/Preview/Brush Region 8 - Ulek Mayang Island")]
        public static void BrushRegionEight() => RegionCenter(8);
        public static string SaveLoadedBrushChunks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Exit Play mode before saving chunks.";
            var scene = SceneManager.GetActiveScene();
            var preview = scene.GetRootGameObjects().FirstOrDefault(g => g.name == PreviewName);
            if (preview == null) return "Load a Brush Region preview first.";
            int count = 0;
            foreach (Transform chunk in preview.transform) foreach (Transform part in chunk)
                if (PrefabUtility.IsAnyPrefabInstanceRoot(part.gameObject) && PrefabUtility.HasPrefabInstanceAnyOverrides(part.gameObject, false))
                { PrefabUtility.ApplyPrefabInstance(part.gameObject, InteractionMode.UserAction); count++; }
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            return $"Saved {count} modified loaded chunk prefabs and the scene. Save sculpted terrain through Terrain Brush.";
        }
        [MenuItem("Tools/Nusantara/Preview/Brush Region 9 - Darkness Island")]
        public static void BrushRegionNine() => RegionCenter(9);
        [MenuItem("Tools/Nusantara/Edit Region 9")]
        public static void EditRegionNineMenu()=>ShowRegionForEditing(9);
        static void RegionCenter(int id) => ShowRegionForEditing(id);
        [MenuItem("Tools/Nusantara/Preview/Show Whole World")]
        public static void WholeWorldMenu() => Debug.Log(ShowWholeWorld());
        [MenuItem("Tools/Nusantara/Edit Region 1")]
        public static void EditRegionOneMenu()=>ShowRegionForEditing(1);
        [MenuItem("Tools/Nusantara/Edit Region 2")]
        public static void EditRegionTwoMenu()=>ShowRegionForEditing(2);
        [MenuItem("Tools/Nusantara/Edit Region 3")]
        public static void EditRegionThreeMenu()=>ShowRegionForEditing(3);
        [MenuItem("Tools/Nusantara/Edit Region 4")]
        public static void EditRegionFourMenu()=>ShowRegionForEditing(4);
        [MenuItem("Tools/Nusantara/Edit Region 6")]
        public static void EditRegionSixMenu()=>ShowRegionForEditing(6);
        [MenuItem("Tools/Nusantara/Edit Region 7")]
        public static void EditRegionSevenMenu()=>ShowRegionForEditing(7);
        [MenuItem("Tools/Nusantara/Edit Region 8")]
        public static void EditRegionEightMenu()=>ShowRegionForEditing(8);
        [MenuItem("Tools/Nusantara/Show All Regions")]
        public static void ShowAllRegionsMenu()=>ShowWholeWorld();
        [MenuItem("Tools/Nusantara/Edit Region 5")]
        public static void EditRegionFiveMenu() => Debug.Log(ShowRegionForEditing(5));
        public static string ShowRegionForEditing(int id)
        {
            var world=AuthoringWorld();var region=world.regions.First(r=>r.id==id);
            if(world.regionPaint!=null&&id<=world.MainlandRegionCount)
            {
                if(!WorldRegionPaintAssets.RegionBounds(world.regionPaint,id,out var paintedBounds,out var paintedPoint))
                    return $"Region {id} has no painted area. Open Region Brush to paint it.";
                ShowWholeWorld();
                SceneView.lastActiveSceneView?.LookAt(new Vector3(paintedBounds.center.x,42,paintedBounds.center.y),Quaternion.Euler(65,0,0),Mathf.Max(paintedBounds.width,paintedBounds.height)*.6f,false,true);
                return $"Framed the painted area of Region {id}. Mainland stays visible for painting across borders.";
            }
            var p=world.ChunkOrigin(region.chunkOffset)+new Vector3(region.chunkGridSize.x*world.chunkSize/2,0,region.chunkGridSize.y*world.chunkSize/2);
            var profile=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            p.y=WorldTerrainSculptAssets.SampleGround(profile,p.x,p.z);
            bool loaded=LoadPreview(p,false,id);
            SetRegionVisibility(world,id);
            var ground=Object.FindObjectsByType<WorldRegionGround>().FirstOrDefault(g=>g.region==id&&g.gameObject.scene==SceneManager.GetActiveScene());
            if(ground!=null){Selection.activeGameObject=ground.gameObject;FrameGrounds(new[]{ground});EditorGUIUtility.PingObject(ground.gameObject);}
            return loaded?$"Editing Region {id} · {region.boss}. Only this region is visible, with all its saved props loaded. Paint in the Scene view; save through each brush tool.":$"Framed Region {id}. Save Region Chunk Changes to preserve your modified props before switching the preview.";
        }
        static void SetRegionVisibility(WorldDefinition world,int id)
        {
            SessionState.SetInt("Nusantara.VisibleEditorRegion",id);
            var scene=SceneManager.GetActiveScene();var visibility=SceneVisibilityManager.instance;
            void Visible(GameObject go,bool show){if(show)visibility.Show(go,true);else visibility.Hide(go,true);}
            var grounds=Object.FindObjectsByType<WorldRegionGround>().Where(g=>g.gameObject.scene==scene).ToArray();
            foreach(var ground in grounds)Visible(ground.gameObject,id==0||ground.region==id);
            var region=world.regions.FirstOrDefault(r=>r.id==id);
            Bounds regionBounds=default;
            if(region!=null)
            {
                var o=world.ChunkOrigin(region.chunkOffset);float width=region.chunkGridSize.x*world.chunkSize,depth=region.chunkGridSize.y*world.chunkSize;
                regionBounds=new Bounds(o+new Vector3(width/2,region.groundElevation,depth/2),new Vector3(width,1000,depth));
            }
            foreach(var root in scene.GetRootGameObjects())
            {
                if(root.name==WorldRegionalGroundAssets.SceneRoot||root.name==PreviewName||root.name=="Region 1 Island Access")continue;
                var renderers=root.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)continue;
                Visible(root,id==0||renderers.Any(r=>r.bounds.Intersects(regionBounds)));
            }
            foreach(var portal in Object.FindObjectsByType<WorldRegionPortal>())
            {
                if(portal.gameObject.scene!=scene)continue;
                var owner=world.chunks.FirstOrDefault(c=>c.coordinate==world.WorldToChunk(portal.transform.position));
                Visible(portal.gameObject,id==0||owner?.region==id);
            }
            var preview=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==PreviewName);
            if(preview!=null)foreach(Transform cell in preview.transform)
            {
                var owner=world.chunks.FirstOrDefault(c=>c.coordinate==world.WorldToChunk(cell.position));
                Visible(cell.gameObject,id==0||owner?.region==id);
            }
            foreach(var painter in Resources.FindObjectsOfTypeAll<VegetationPainterWindow>())painter.RefreshEditorVisibility();
            SceneView.RepaintAll();
        }
        public static bool IsVisibleArea(WorldDefinition world,Vector3 position)
        {return VisibleRegion<=0||world.chunks.Any(c=>c.coordinate==world.WorldToChunk(position)&&c.region==VisibleRegion);}
        public static void ApplyCurrentRegionVisibility()
        {
            if(VisibleRegion<0||EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().path!=WorldReferenceBuilder.ScenePath)return;
            var world=AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);if(world!=null)SetRegionVisibility(world,VisibleRegion);
        }
        public static void RefreshEditorPreview(Vector3 fallback)
        {
            var world=AuthoringWorld();int region=VisibleRegion;var position=fallback;
            if(region>0)
            {var r=world.regions.First(r=>r.id==region);position=world.ChunkOrigin(r.chunkOffset)+new Vector3(r.chunkGridSize.x*world.chunkSize/2,0,r.chunkGridSize.y*world.chunkSize/2);}
            var view=SceneView.lastActiveSceneView;var pivot=view!=null?view.pivot:Vector3.zero;var rotation=view!=null?view.rotation:Quaternion.identity;float size=view!=null?view.size:0;bool orthographic=view!=null&&view.orthographic;
            LoadPreview(position,false,Mathf.Max(0,region),region==0);if(region>=0)SetRegionVisibility(world,region);
            if(view!=null)view.LookAt(pivot,rotation,size,orthographic,true);
        }
        static WorldDefinition AuthoringWorld()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit Play mode to edit the world.");
            if(SceneManager.GetActiveScene().path!=WorldReferenceBuilder.ScenePath)throw new System.InvalidOperationException("Open NusantaraQuest_World first.");
            var world=AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
            if(world==null)throw new System.InvalidOperationException("The world definition is missing.");return world;
        }
        public static string ShowWholeWorld()
        {
            var world=AuthoringWorld();bool loaded=LoadPreview(world.playerStart,false,0,true);
            SetRegionVisibility(world,0);
            var grounds=Object.FindObjectsByType<WorldRegionGround>().Where(g=>g.gameObject.scene==SceneManager.GetActiveScene()).ToArray();
            if(grounds.Length>0){FrameGrounds(grounds);Selection.activeGameObject=grounds[0].transform.parent.gameObject;}
            return loaded?$"Showing all {world.regions.Length} regions and their saved props. Regions 1–7 share the mainland; 8–9 are separate floating islands.":"Showing all ground planes. Save Region Chunk Changes before switching the modified prop preview.";
        }
        static void FrameGrounds(WorldRegionGround[] grounds)
        {
            var renderers=grounds.SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).ToArray();if(renderers.Length==0)return;
            var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            var view=SceneView.lastActiveSceneView;if(view==null)view=EditorWindow.GetWindow<SceneView>();
            view.rotation=Quaternion.Euler(55,0,0);view.Frame(bounds,false);view.Repaint();
        }
        [MenuItem("Tools/Nusantara/Preview/Region 5 - Kampung Sungai")]
        public static void Village() => Landmark(0);
        [MenuItem("Tools/Nusantara/Preview/Region 5 - Hutan Rimba")]
        public static void Forest() => Landmark(1);
        [MenuItem("Tools/Nusantara/Preview/Region 5 - Delta Sungai")]
        public static void Delta() => Landmark(2);
        [MenuItem("Tools/Nusantara/Preview/Region 5 - Rawa Berkabus")]
        public static void Swamp() => Landmark(3);
        [MenuItem("Tools/Nusantara/Preview/Region 5 - Air Terjun Suci")]
        public static void Waterfall() => Landmark(4);
        [MenuItem("Tools/Nusantara/Preview/Region 5 - Runtuhan Lama")]
        public static void Ruins() => Landmark(5);
        [MenuItem("Tools/Nusantara/Preview/Region 5 - Gua Sunyi")]
        public static void Cave() => Landmark(6);
        [MenuItem("Tools/Nusantara/Preview/Region 5 - Pulau Buaya")]
        public static void Elite() => Landmark(7);
        [MenuItem("Tools/Nusantara/Preview/Region 5 - Sarang Bujang Senang")]
        public static void Boss() => Landmark(8);
        static void Landmark(int index)
        {
            var w=AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
            if (w.brushAuthoredTerrain) { RegionCenter(w.WetlandRegionId); return; }
            if(index==4)
            {
                var profile=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
                var p=profile.ReferencePoint(1268,310);
                LoadPreview(new Vector3(p.x,profile.BaseHeight(p)+.2f,p.y),true);return;
            }
            LoadPreview(w.locations[index].position+Vector3.up*.2f,true);
        }
        static bool HasPreviewEdits(GameObject instance)
        {
            if(!PrefabUtility.IsPartOfPrefabInstance(instance))return false;
            if(PrefabUtility.HasPrefabInstanceAnyOverrides(instance,false))return true;
            var source=PrefabUtility.GetCorrespondingObjectFromSource(instance.transform);
            // Unity excludes prefab-root transform overrides from its ordinary override check.
            return source!=null&&((instance.transform.localPosition-source.localPosition).sqrMagnitude>.00000001f
                ||Quaternion.Angle(instance.transform.localRotation,source.localRotation)>.0001f
                ||(instance.transform.localScale-source.localScale).sqrMagnitude>.00000001f);
        }
        public static bool LoadPreview(Vector3 position,bool movePlayer,int region=0,bool wholeWorld=false)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) {Debug.LogWarning("Use these authoring bookmarks outside Play mode.");return false;}
            var scene=SceneManager.GetActiveScene();if(scene.path!=WorldReferenceBuilder.ScenePath) {Debug.LogWarning("Open NusantaraQuest_World first.");return false;}
            var w=AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
            if(!w.layoutApproved)return false;
            var existing=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==PreviewName);
            if(existing!=null)
            {
                foreach(Transform chunk in existing.transform)foreach(Transform content in chunk)
                {
                    if(HasPreviewEdits(content.gameObject))
                    {
                        Debug.LogWarning("Apply or save modified prop content, or restore moved prefab roots before changing previews. Your current preview has been kept.");return false;
                    }
                }
                Object.DestroyImmediate(existing);
            }
            if(movePlayer)
            {
                var player=scene.GetRootGameObjects().First(g=>g.name=="Player");var controller=player.GetComponent<CharacterController>();
                var destination=position-Vector3.up*(controller.center.y-controller.height*.5f);
                Vector3 delta=destination-player.transform.position;player.transform.position=destination;
                foreach(string name in new[]{"Camera","Isometric Camera","Rideable Horse"}) {var go=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==name);if(go!=null)go.transform.position+=delta;}
            }
            var preview=new GameObject(PreviewName);preview.tag="EditorOnly";preview.AddComponent<WorldEditorPreview>();
            var center=w.WorldToChunk(position);var settings=AddressableAssetSettingsDefaultObject.Settings;
            foreach(var record in w.chunks)
            {
                if(!wholeWorld&&(region!=0?record.region!=region:Mathf.Abs(record.coordinate.x-center.x)>1||Mathf.Abs(record.coordinate.y-center.y)>1))continue;
                var addresses=new[]{record.essentialAddress}.Concat(record.decorationAddresses).Concat(record.gameplayAddresses).Where(a=>!string.IsNullOrEmpty(a)).ToArray();
                if(addresses.Length==0)continue;
                var chunk=new GameObject($"Region {record.region} - Chunk {record.coordinate.x:D2}_{record.coordinate.y:D2}");chunk.transform.SetParent(preview.transform);chunk.transform.position=w.ChunkOrigin(record.coordinate);
                foreach(string address in addresses)
                {
                    var entry=settings.groups.Where(g=>g!=null).SelectMany(g=>g.entries).FirstOrDefault(e=>e.address==address);if(entry==null)continue;
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);if(prefab!=null)PrefabUtility.InstantiatePrefab(prefab,chunk.transform);
                }
            }
            Selection.activeGameObject=preview;
            SceneView.lastActiveSceneView?.LookAt(position,Quaternion.Euler(55,0,0),180,false,true);
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }
    }
}
