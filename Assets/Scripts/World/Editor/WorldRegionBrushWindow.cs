using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace Nusantara.World.Editor
{
    public sealed class WorldRegionBrushWindow : EditorWindow
    {
        [SerializeField] int region=1;
        [SerializeField] float radius=64;
        bool painting,stroke,segment;
        int undoGroup,hotControl;
        int observedRevision;
        Vector2 last;
        Toggle paintToggle;
        Slider radiusSlider;
        HelpBox status;
        Label areas;
        WorldRegionPaintData data;
        WorldDefinition world;
        readonly Vector3[] labelPoints=new Vector3[7];
        readonly bool[] labelVisible=new bool[7];

        [MenuItem("Tools/Nusantara/Region Brush (1-7)")]
        public static void Open()
        {
            var window=GetWindow<WorldRegionBrushWindow>("Region Brush");
            window.minSize=new Vector2(380,480);window.Show();
        }
        void OnEnable()
        {
            SceneView.duringSceneGui+=SceneGUI;
            Undo.undoRedoPerformed+=OnUndo;
            AssemblyReloadEvents.beforeAssemblyReload+=DisablePainting;
            EditorApplication.playModeStateChanged+=OnPlay;
        }
        void OnDisable()
        {
            if(data!=null)WorldRegionPaintAssets.Save(data,false);
            DisablePainting();SceneView.duringSceneGui-=SceneGUI;
            Undo.undoRedoPerformed-=OnUndo;
            AssemblyReloadEvents.beforeAssemblyReload-=DisablePainting;
            EditorApplication.playModeStateChanged-=OnPlay;
        }
        void OnPlay(PlayModeStateChange state)=>DisablePainting();
        public void DisablePainting(){EndStroke();painting=false;paintToggle?.SetValueWithoutNotify(false);SceneView.RepaintAll();}
        bool Ready()=>!EditorApplication.isPlayingOrWillChangePlaymode&&SceneManager.GetActiveScene().path==WorldReferenceBuilder.ScenePath&&world!=null&&data!=null&&world.regionPaint==data;
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var sheet=AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/World/Editor/WorldRoadBrushWindow.uss");
            if(sheet!=null)rootVisualElement.styleSheets.Add(sheet);
            var root=new ScrollView();root.AddToClassList("road-panel");rootVisualElement.Add(root);
            var title=new Label("PAINT YOUR REGIONS");title.AddToClassList("road-title");root.Add(title);
            var note=new Label("Choose a region and paint its shape across the mainland. Paint over another region to make yours bigger.");note.AddToClassList("road-note");root.Add(note);
            world=WorldRegionPaintAssets.World;data=world!=null?world.regionPaint:null;
            var names=Enumerable.Range(1,world?.MainlandRegionCount??7).Select(id=>$"Region {id} · {world?.regions.FirstOrDefault(r=>r.id==id)?.name}").ToList();
            var picker=new DropdownField("Paint region",names,region-1);
            picker.RegisterValueChangedCallback(e=>{EndStroke();region=picker.index+1;});root.Add(picker);
            radiusSlider=new Slider("Brush radius (m)",4,384){value=radius,showInputField=true};
            radiusSlider.RegisterValueChangedCallback(e=>{if(!float.IsFinite(e.newValue))return;EndStroke();radius=Mathf.Clamp(e.newValue,4,384);});root.Add(radiusSlider);
            paintToggle=new Toggle("Enable region brush"){value=painting};
            paintToggle.RegisterValueChangedCallback(e=>
            {
                EndStroke();
                if(e.newValue)
                {
                    if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().path!=WorldReferenceBuilder.ScenePath)
                    {paintToggle.SetValueWithoutNotify(false);status.text="Open NusantaraQuest_World outside Play mode.";return;}
                    data=WorldRegionPaintAssets.Ensure(world);
                    foreach(var tool in Resources.FindObjectsOfTypeAll<WorldTerrainBrushWindow>())tool.DisablePainting();
                    foreach(var tool in Resources.FindObjectsOfTypeAll<WorldWaterBrushWindow>())tool.DisablePainting();
                    foreach(var tool in Resources.FindObjectsOfTypeAll<WorldAINoWalkBrushWindow>())tool.DisablePainting();
                    foreach(var tool in Resources.FindObjectsOfTypeAll<WorldRoadBrushWindow>())tool.DisablePainting();
                    foreach(var tool in Resources.FindObjectsOfTypeAll<VegetationPainterWindow>())tool.DisablePainting();
                    WorldSceneBookmarks.ShowWholeWorld();
                }
                painting=e.newValue;RefreshAreas();SceneView.RepaintAll();
            });root.Add(paintToggle);
            root.Add(new Button(()=>{EndStroke();if(Ready())WorldRegionPaintAssets.FrameMainland();}){text="Top view of mainland"});
            root.Add(new Button(()=>{EndStroke();WorldRegionPaintAssets.Save(data);status.text="Region shapes saved.";}){text="Save region shapes"});
            root.Add(new Button(()=>
            {
                if(!Ready())return;EndStroke();
                data.CapturePixels();Undo.RegisterCompleteObjectUndo(data,"Fill mainland with region "+region);
                var pixels=data.map.GetPixels32();var color=data.Pixel(region);
                for(int n=0;n<pixels.Length;n++)if(pixels[n].a>=1&&pixels[n].a<=world.MainlandRegionCount)pixels[n]=color;
                data.map.SetPixels32(pixels);data.map.Apply(false);WorldRegionPaintAssets.Save(data);RefreshAreas();
                status.text=$"Mainland filled with Region {region}. Choose another region and paint its area. Ctrl+Z restores the previous layout.";
            }){text="Fill mainland with selected region"});
            root.Add(new HelpBox("Drag: paint • Ctrl+Z: undo • Shift: restore original region\n[ / ]: brush size • Esc: stop • Alt + mouse: orbit\nRegions 8 and 9 are locked. Roads, heights, trees and props stay in place.",HelpBoxMessageType.Info));
            areas=new Label();areas.AddToClassList("road-note");root.Add(areas);
            status=new HelpBox("Enable the brush, then drag on the ground in Scene view. Every completed stroke saves automatically.",HelpBoxMessageType.Info);root.Add(status);
            RefreshAreas();
        }
        void RefreshAreas()
        {
            if(data?.map==null||areas==null)return;
            observedRevision=data.revision;
            var counts=new int[8];foreach(var pixel in data.map.GetPixels32())if(pixel.a>=1&&pixel.a<=world.MainlandRegionCount)counts[pixel.a]++;
            float total=counts.Sum();
            var profile=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            for(int id=1;id<=world.MainlandRegionCount;id++)
            {
                labelVisible[id-1]=WorldRegionPaintAssets.RegionBounds(data,id,out _,out var point);
                if(labelVisible[id-1])labelPoints[id-1]=new Vector3(point.x,WorldTerrainSculptAssets.SampleGround(profile,point.x,point.y)+2,point.y);
            }
            areas.text="PAINTED AREA\n"+string.Join("\n",Enumerable.Range(1,world?.MainlandRegionCount??7).Select(id=>$"Region {id}: {counts[id]*data.cellSize*data.cellSize/1000000:0.000} km²  ({(total>0?counts[id]*100/total:0):0.0}%)"))+ $"\nBorder detail: {data.cellSize:0.#} m";
        }
        bool Ground(Ray ray,out RaycastHit hit)
        {
            hit=default;float nearest=float.MaxValue;
            // Select the nearest ground first; a locked island must never paint mainland below it.
            foreach(var candidate in Physics.RaycastAll(ray,30000,~0,QueryTriggerInteraction.Ignore))
            {
                if(candidate.distance>=nearest||!(candidate.collider is MeshCollider collider)||SceneVisibilityManager.instance.IsHidden(collider.gameObject))continue;
                var marker=collider.GetComponentInParent<WorldRegionGround>();
                if(marker==null||marker.gameObject.scene!=SceneManager.GetActiveScene()||collider.sharedMesh!=marker.ground)continue;
                hit=candidate;nearest=candidate.distance;
            }
            return nearest<float.MaxValue&&hit.collider.GetComponentInParent<WorldRegionGround>().region<=world.MainlandRegionCount;
        }
        void SceneGUI(SceneView view)
        {
            var e=Event.current;
            if(Ready()&&e.type==EventType.Repaint)
            {
                for(int id=1;id<=world.MainlandRegionCount;id++)if(labelVisible[id-1])Handles.Label(labelPoints[id-1],$"Region {id}",EditorStyles.whiteBoldLabel);
            }
            if(stroke&&(e.rawType==EventType.MouseUp||e.type==EventType.Ignore||e.type==EventType.MouseLeaveWindow))EndStroke();
            if(!painting)return;
            if(!Ready()){DisablePainting();return;}
            if(e.type==EventType.KeyDown)
            {
                if(e.keyCode==KeyCode.Escape){DisablePainting();e.Use();return;}
                if(e.keyCode==KeyCode.LeftBracket||e.keyCode==KeyCode.RightBracket)
                {radius=Mathf.Clamp(radius*(e.keyCode==KeyCode.LeftBracket?.8f:1.25f),4,384);radiusSlider?.SetValueWithoutNotify(radius);e.Use();view.Repaint();return;}
            }
            if(e.alt||Tools.viewToolActive){EndStroke();return;}
            int control=GUIUtility.GetControlID("NusantaraRegionBrush".GetHashCode(),FocusType.Passive);
            if(e.type==EventType.Layout)HandleUtility.AddDefaultControl(control);
            if(!Ground(HandleUtility.GUIPointToWorldRay(e.mousePosition),out var hit)){segment=false;return;}
            Handles.color=e.shift?Color.white:data.colors[region-1];
            Handles.DrawWireDisc(hit.point+Vector3.up*.15f,Vector3.up,radius);
            Handles.Label(hit.point+Vector3.up*2,$"{(e.shift?"Restore original":"Paint Region "+region)} · {radius*2:0} m wide");
            if(e.type==EventType.MouseMove)view.Repaint();
            if(e.button!=0||(e.type!=EventType.MouseDown&&e.type!=EventType.MouseDrag)||e.control||e.command)return;
            if(e.type==EventType.MouseDown&&HandleUtility.nearestControl!=control)return;
            if(!stroke&&e.type!=EventType.MouseDown)return;
            if(!stroke)
            {
                Undo.IncrementCurrentGroup();undoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Paint mainland region");
                data.CapturePixels();Undo.RegisterCompleteObjectUndo(data,"Paint mainland region");stroke=true;segment=false;hotControl=control;GUIUtility.hotControl=control;
            }
            var point=new Vector2(hit.point.x,hit.point.z);
            WorldRegionPaintAssets.Stamp(world,data,segment?last:point,point,radius,region,e.shift);
            last=point;segment=true;e.Use();view.Repaint();
        }
        void EndStroke()
        {
            if(!stroke)return;stroke=false;segment=false;
            if(GUIUtility.hotControl==hotControl)GUIUtility.hotControl=0;hotControl=0;
            Undo.CollapseUndoOperations(undoGroup);WorldRegionPaintAssets.Save(data);RefreshAreas();
            if(status!=null)status.text="Region shape saved. Keep painting or press Ctrl+Z to undo.";
        }
        void OnUndo()
        {
            if(data?.map==null||data.revision==observedRevision)return;
            data.RestorePixels();EditorUtility.SetDirty(data.map);EditorUtility.SetDirty(data);RefreshAreas();SceneView.RepaintAll();
        }
    }
}
