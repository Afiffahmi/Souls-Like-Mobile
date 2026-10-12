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
        const string PreviewName="World preview (Editor only)";
        [MenuItem("Tools/Nusantara/Preview/Region 1 - Player start")]
        public static void RegionOne() { var w=AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);LoadPreview(w.playerStart,true); }
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
            if(index==4)
            {
                var profile=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
                var p=profile.ReferencePoint(1268,310);
                LoadPreview(new Vector3(p.x,profile.BaseHeight(p)+.2f,p.y),true);return;
            }
            LoadPreview(w.locations[index].position+Vector3.up*.2f,true);
        }
        public static void LoadPreview(Vector3 position,bool movePlayer)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) {Debug.LogWarning("Use these authoring bookmarks outside Play mode.");return;}
            var scene=SceneManager.GetActiveScene();if(scene.path!=WorldReferenceBuilder.ScenePath) {Debug.LogWarning("Open NusantaraQuest_World first.");return;}
            var w=AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
            if(!w.layoutApproved)return;
            var existing=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==PreviewName);
            if(existing!=null)
            {
                foreach(Transform chunk in existing.transform)foreach(Transform content in chunk)
                {
                    if(PrefabUtility.IsPartOfPrefabInstance(content.gameObject)&&PrefabUtility.HasPrefabInstanceAnyOverrides(content.gameObject,false))
                    {
                        EditorUtility.DisplayDialog("Keep your preview edits","Apply or revert the modified chunk prefab overrides before changing bookmarks. Your current preview has been kept.","Keep preview");return;
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
                if(Mathf.Abs(record.coordinate.x-center.x)>1||Mathf.Abs(record.coordinate.y-center.y)>1)continue;
                var chunk=new GameObject($"Region {record.region} - Chunk {record.coordinate.x:D2}_{record.coordinate.y:D2}");chunk.transform.SetParent(preview.transform);chunk.transform.position=w.ChunkOrigin(record.coordinate);
                foreach(string address in new[]{record.essentialAddress}.Concat(record.decorationAddresses).Concat(record.gameplayAddresses))
                {
                    var entry=settings.groups.Where(g=>g!=null).SelectMany(g=>g.entries).FirstOrDefault(e=>e.address==address);if(entry==null)continue;
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);if(prefab!=null)PrefabUtility.InstantiatePrefab(prefab,chunk.transform);
                }
            }
            Selection.activeGameObject=preview;
            SceneView.lastActiveSceneView?.LookAt(position,Quaternion.Euler(55,0,0),180,false,true);
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
