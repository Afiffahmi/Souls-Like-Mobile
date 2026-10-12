using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Nusantara.World.Editor
{
    public sealed class VegetationPainterWindow : EditorWindow
    {
        readonly Dictionary<Vector2Int,VegetationPaintData> patches=new Dictionary<Vector2Int,VegetationPaintData>();
        readonly Dictionary<Vector2Int,GameObject> previews=new Dictionary<Vector2Int,GameObject>();
        readonly HashSet<VegetationPaintData> pending=new HashSet<VegetationPaintData>();
        readonly List<GameObject> hidden=new List<GameObject>();
        WorldGeographyProfile geography;
        VegetationPaintPalette palette;
        VegetationPaintKind kind;
        float radius=8,density=.6f;
        bool painting,erasing,stroke;
        Vector3 lastStroke;
        int undoGroup;
        Label status;
        readonly System.Random random=new System.Random();

        [MenuItem("Tools/Nusantara/Vegetation/Tree and Grass Painter")]
        public static void Open()=>GetWindow<VegetationPainterWindow>("Tree & Grass Paint");
        void OnEnable()
        {
            geography=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            palette=AssetDatabase.LoadAssetAtPath<VegetationPaintPalette>(VegetationPaintAssets.PalettePath);
            SceneView.duringSceneGui+=SceneGUI;Undo.undoRedoPerformed+=OnUndo;
        }
        void OnDisable()
        {
            EndStroke();SceneView.duringSceneGui-=SceneGUI;Undo.undoRedoPerformed-=OnUndo;
            foreach(var preview in previews.Values)if(preview!=null){DestroyTemporaryMeshes(preview);Object.DestroyImmediate(preview);}
            foreach(var go in hidden)if(go!=null)SceneVisibilityManager.instance.Show(go,true);
            previews.Clear();hidden.Clear();
        }
        public void CreateGUI()
        {
            var tree=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/World/Editor/VegetationPainter.uxml");
            tree.CloneTree(rootVisualElement);
            var paletteField=rootVisualElement.Q<ObjectField>("palette");paletteField.objectType=typeof(VegetationPaintPalette);paletteField.value=palette;
            paletteField.RegisterValueChangedCallback(e=>{palette=e.newValue as VegetationPaintPalette;RefreshAll();});
            var mode=rootVisualElement.Q<EnumField>("mode");mode.Init(kind);mode.RegisterValueChangedCallback(e=>{kind=(VegetationPaintKind)e.newValue;SceneView.RepaintAll();});
            var brush=rootVisualElement.Q<Slider>("radius");brush.value=radius;brush.RegisterValueChangedCallback(e=>radius=e.newValue);
            var amount=rootVisualElement.Q<Slider>("density");amount.value=density;amount.RegisterValueChangedCallback(e=>density=e.newValue);
            rootVisualElement.Q<Toggle>("paint").RegisterValueChangedCallback(e=>{painting=e.newValue;SceneView.RepaintAll();});
            rootVisualElement.Q<Toggle>("erase").RegisterValueChangedCallback(e=>erasing=e.newValue);
            rootVisualElement.Q<Button>("save").clicked+=Save;
            rootVisualElement.Q<Button>("frame").clicked+=()=>
            {
                if(geography!=null&&geography.world.locations.Length>0)SceneView.lastActiveSceneView?.LookAt(geography.world.locations[0].position,Quaternion.Euler(55,0,0),120,false,true);
            };
            status=rootVisualElement.Q<Label>("status");SetStatus("Ready. Enable painting, then drag on dry ground in Scene view.");
            if(palette!=null)LoadExisting();
        }
        void SetStatus(string message){if(status!=null)status.text=message;}
        void LoadExisting()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:VegetationPaintData",new[]{VegetationPaintAssets.Root+"/Patches"}))
            {
                var data=AssetDatabase.LoadAssetAtPath<VegetationPaintData>(AssetDatabase.GUIDToAssetPath(guid));patches[data.coordinate]=data;
                if(data.stamps.Count>0)Refresh(data);
            }
        }
        VegetationPaintData Patch(Vector2Int coordinate)
        {
            if(patches.TryGetValue(coordinate,out var data))return data;
            string path=$"{VegetationPaintAssets.Root}/Patches/{coordinate.x:D2}_{coordinate.y:D2}.asset";
            data=AssetDatabase.LoadAssetAtPath<VegetationPaintData>(path);
            if(data==null){data=CreateInstance<VegetationPaintData>();data.coordinate=coordinate;AssetDatabase.CreateAsset(data,path);}
            patches[coordinate]=data;return data;
        }
        void HideCommitted(Vector2Int coordinate)
        {
            string chunkName=$"Region 5 - Chunk {coordinate.x:D2}_{coordinate.y:D2}";
            foreach(var go in Object.FindObjectsByType<Transform>().Where(t=>t.name=="Hand painted vegetation"&&t.parent!=null&&t.parent.name==chunkName).Select(t=>t.gameObject))
                if(!hidden.Contains(go)){SceneVisibilityManager.instance.Hide(go,true);hidden.Add(go);}
        }
        void Refresh(VegetationPaintData data)
        {
            if(palette==null||geography==null)return;
            if(!previews.TryGetValue(data.coordinate,out var root)||root==null)
            {
                root=new GameObject($"Vegetation brush preview {data.coordinate}"){hideFlags=HideFlags.HideAndDontSave};root.transform.position=geography.world.ChunkOrigin(data.coordinate);previews[data.coordinate]=root;
            }
            HideCommitted(data.coordinate);VegetationPaintAssets.BuildVisuals(root,data,palette,false);
            foreach(Transform child in root.GetComponentsInChildren<Transform>(true))child.gameObject.hideFlags=HideFlags.HideAndDontSave;
        }
        void RefreshAll(){foreach(var patch in patches.Values)Refresh(patch);SceneView.RepaintAll();}
        void OnUndo()
        {foreach(var patch in patches.Values){EditorUtility.SetDirty(patch);pending.Add(patch);}RefreshAll();SetStatus("Undo / Redo applied. Save painted chunks to update streaming and collision.");}
        static void DestroyTemporaryMeshes(GameObject root)
        {foreach(var f in root.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh!=null&&!AssetDatabase.Contains(f.sharedMesh))Object.DestroyImmediate(f.sharedMesh);}
        void EndStroke()
        {
            if(!stroke)return;stroke=false;Undo.CollapseUndoOperations(undoGroup);
            foreach(var patch in pending)AssetDatabase.SaveAssetIfDirty(patch);
        }
        bool Ground(Ray ray,out RaycastHit hit)
        {
            hit=default;
            foreach(var candidate in Physics.RaycastAll(ray,10000,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                if(candidate.collider.name!="Terrain"||candidate.collider is not MeshCollider)continue;
                hit=candidate;return true;
            }
            return false;
        }
        public static bool Allowed(WorldGeographyProfile profile,Vector3 p,Vector3 normal,VegetationPaintKind mode)
        {
            var xz=new Vector2(p.x,p.z);
            if(!profile.InsideRegion(xz,5,mode==VegetationPaintKind.Tree?5:1)||profile.IsForbiddenWater(xz))return false;
            if(profile.WaterDistance(xz,out float surface)<20||p.y<surface+.5f||profile.RouteDistance(xz)<(mode==VegetationPaintKind.Tree?4:1.5f))return false;
            if(profile.walkingPlatforms.Any(platform=>WorldGeographyProfile.Inside(xz,platform.boundary)))return false;
            return Vector3.Angle(normal,Vector3.up)<(mode==VegetationPaintKind.Tree?25:40);
        }
        bool Clear(Vector3 position,float clearance)
        {
            foreach(var collider in Physics.OverlapCapsule(position+Vector3.up*.55f,position+Vector3.up*2.5f,clearance,~0,QueryTriggerInteraction.Ignore))
                if(!VegetationPaintAssets.IsFloor(collider))return false;
            return true;
        }
        void SceneGUI(SceneView view)
        {
            if(Event.current.type==EventType.MouseUp&&Event.current.button==0)EndStroke();
            if(!painting||palette==null||geography==null||EditorApplication.isPlayingOrWillChangePlaymode)return;
            var e=Event.current;if(e.alt)return;
            int control=GUIUtility.GetControlID(FocusType.Passive);
            if(e.type==EventType.Layout)HandleUtility.AddDefaultControl(control);
            if(!Ground(HandleUtility.GUIPointToWorldRay(e.mousePosition),out var hit))return;
            bool erase=erasing||e.shift;bool allowed=erase||Allowed(geography,hit.point,hit.normal,kind);
            Handles.color=!allowed?new Color(1,.3f,.15f):erase?new Color(1,.65f,.2f):new Color(.4f,1,.65f);
            Handles.DrawWireDisc(hit.point+hit.normal*.04f,hit.normal,radius);Handles.DrawWireDisc(hit.point+hit.normal*.04f,hit.normal,Mathf.Min(radius,kind==VegetationPaintKind.Tree?palette.treeSpacing:palette.grassSpacing));
            if(e.type==EventType.MouseMove)view.Repaint();
            if(e.type==EventType.MouseUp&&e.button==0){EndStroke();e.Use();return;}
            if((e.type!=EventType.MouseDown&&e.type!=EventType.MouseDrag)||e.button!=0||!allowed)return;
            if(!stroke){Undo.IncrementCurrentGroup();undoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(erase?"Erase vegetation":"Paint "+kind);stroke=true;lastStroke=hit.point+Vector3.one*1000;}
            float spacing=kind==VegetationPaintKind.Tree?palette.treeSpacing:palette.grassSpacing;
            if(e.type==EventType.MouseDrag&&Vector3.Distance(lastStroke,hit.point)<Mathf.Max(.35f,radius*.28f)){e.Use();return;}
            lastStroke=hit.point;
            if(erase)Erase(hit.point);else Paint(hit.point,spacing);
            e.Use();view.Repaint();
        }
        void Erase(Vector3 center)
        {
            int removed=0;
            foreach(var data in patches.Values)
            {
                bool InBrush(VegetationStamp s)=>s.kind==kind&&new Vector2(s.position.x-center.x,s.position.z-center.z).sqrMagnitude<radius*radius;
                if(!data.stamps.Any(InBrush))continue;Undo.RecordObject(data,"Erase vegetation");removed+=data.stamps.RemoveAll(s=>InBrush(s));EditorUtility.SetDirty(data);pending.Add(data);Refresh(data);
            }
            SetStatus($"Erased {removed} {kind.ToString().ToLower()} placements. Save painted chunks when finished.");
        }
        void Paint(Vector3 center,float spacing)
        {
            int count=Mathf.Clamp(Mathf.CeilToInt(Mathf.PI*radius*radius/(spacing*spacing)*density),1,kind==VegetationPaintKind.Tree?24:256),added=0;
            var changed=new HashSet<VegetationPaintData>();
            float sourceHeight=kind==VegetationPaintKind.Tree?KampungAssets.Bounds(palette.treePrefab).size.y:palette.grassPrefab.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.y;
            Vector2 height=kind==VegetationPaintKind.Tree?palette.treeHeight:palette.grassHeight;
            for(int attempt=0;attempt<count*3&&added<count;attempt++)
            {
                float angle=(float)random.NextDouble()*Mathf.PI*2,distance=Mathf.Sqrt((float)random.NextDouble())*radius;
                Vector3 p=center+new Vector3(Mathf.Cos(angle)*distance,100,Mathf.Sin(angle)*distance);
                if(!Ground(new Ray(p,Vector3.down),out var ground)||!Allowed(geography,ground.point,ground.normal,kind)||!Clear(ground.point,kind==VegetationPaintKind.Tree?1.5f:.25f))continue;
                var coordinate=geography.world.WorldToChunk(ground.point);
                if(!geography.world.chunks.Any(c=>c.coordinate==coordinate&&c.region==5))continue;
                if(patches.Values.Any(data=>data.stamps.Any(s=>s.kind==kind&&new Vector2(s.position.x-ground.point.x,s.position.z-ground.point.z).sqrMagnitude<spacing*spacing)))continue;
                var data=Patch(coordinate);if(data.stamps.Count>=20000){SetStatus("Chunk paint budget reached. Erase or reduce density.");continue;}
                if(changed.Add(data))Undo.RecordObject(data,"Paint vegetation");
                data.stamps.Add(new VegetationStamp{kind=kind,position=ground.point,yaw=(float)random.NextDouble()*360,scale=Mathf.Lerp(height.x,height.y,(float)random.NextDouble())/Mathf.Max(.01f,sourceHeight),seed=random.Next()});added++;
            }
            foreach(var data in changed){EditorUtility.SetDirty(data);pending.Add(data);Refresh(data);}
            SetStatus($"Painted {added} {kind.ToString().ToLower()} placements. Save painted chunks when finished.");
        }
        void Save()
        {
            EndStroke();if(palette==null)return;
            try
            {
                // Include loaded patches so re-opening the tool can commit previously staged strokes.
                foreach(var data in patches.Values)VegetationPaintAssets.Save(data,palette);
                pending.Clear();SetStatus($"Saved {patches.Count} chunks, with VFX canopy, grass batches, trunk collision and navigation. Close the tool to show committed props.");
                var player=GameObject.Find("Player");if(player!=null)WorldSceneBookmarks.LoadPreview(player.transform.position,false);
                foreach(var data in patches.Values)HideCommitted(data.coordinate);
            }
            catch(Exception exception){SetStatus("Save failed: "+exception.Message);Debug.LogException(exception);}
        }
    }
}
