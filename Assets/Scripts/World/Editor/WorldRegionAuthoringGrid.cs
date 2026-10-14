using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nusantara.World.Editor
{
    [InitializeOnLoad]
    public static class WorldRegionAuthoringGrid
    {
        const string Preference="Nusantara.ShowRegionCellGrid";
        public static bool Enabled
        {
            get=>SessionState.GetBool(Preference,true);
            set{SessionState.SetBool(Preference,value);SceneView.RepaintAll();}
        }
        static WorldRegionAuthoringGrid()=>SceneView.duringSceneGui+=Draw;
        static void Draw(SceneView view)
        {
            if(!Enabled||Event.current.type!=EventType.Repaint||EditorApplication.isPlayingOrWillChangePlaymode)return;
            var scene=SceneManager.GetActiveScene();if(scene.path!=WorldReferenceBuilder.ScenePath)return;
            var world=AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldFoundationSetup.DefinitionPath);
            var profile=AssetDatabase.LoadAssetAtPath<WorldGeographyProfile>(WorldReferenceBuilder.ProfilePath);
            if(world==null||profile==null||!world.regionalGroundPlanes)return;
            var oldColor=Handles.color;var oldDepth=Handles.zTest;
            var title=new GUIStyle(EditorStyles.boldLabel){fontSize=13};title.normal.textColor=Color.white;
            var cellLabel=new GUIStyle(EditorStyles.miniLabel);cellLabel.normal.textColor=new Color(1,1,1,.75f);
            try
            {
                Handles.zTest=UnityEngine.Rendering.CompareFunction.LessEqual;
                foreach(var ground in Object.FindObjectsByType<WorldRegionGround>())
                {
                    if(ground.gameObject.scene!=scene||SceneVisibilityManager.instance.IsHidden(ground.gameObject))continue;
                    var region=world.regions.FirstOrDefault(r=>r.id==ground.region);if(region==null)continue;
                    if(world.regionPaint!=null&&region.id<=world.MainlandRegionCount)continue;
                    var origin=world.ChunkOrigin(region.chunkOffset);float width=region.chunkGridSize.x*world.chunkSize,depth=region.chunkGridSize.y*world.chunkSize;
                    Vector3 Surface(float x,float z)
                    {
                        // Sample just inside this region at its outer border.
                        float sx=Mathf.Clamp(x,origin.x+.01f,origin.x+width-.01f),sz=Mathf.Clamp(z,origin.z+.01f,origin.z+depth-.01f);
                        return new Vector3(x,WorldTerrainSculptAssets.SampleGround(profile,sx,sz)+.15f,z);
                    }
                    void Line(bool alongZ,float fixedPosition,bool border)
                    {
                        float length=alongZ?depth:width;int steps=Mathf.Max(1,Mathf.CeilToInt(length/8));var points=new Vector3[steps+1];
                        for(int n=0;n<=steps;n++)points[n]=alongZ?Surface(fixedPosition,origin.z+length*n/steps):Surface(origin.x+length*n/steps,fixedPosition);
                        Handles.color=border?new Color(1,.85f,.40f,.95f):new Color(1,1,1,.45f);Handles.DrawAAPolyLine(border?3:1.5f,points);
                    }
                    for(int x=0;x<=region.chunkGridSize.x;x++)Line(true,origin.x+x*world.chunkSize,x==0||x==region.chunkGridSize.x);
                    for(int z=0;z<=region.chunkGridSize.y;z++)Line(false,origin.z+z*world.chunkSize,z==0||z==region.chunkGridSize.y);
                    Handles.Label(Surface(origin.x+width*.5f,origin.z+depth*.5f)+Vector3.up*2,
                        $"Region {region.id} · {region.boss}\n{region.chunkGridSize.x} × {region.chunkGridSize.y} cells · {width:0} m × {depth:0} m",title);
                    if(WorldSceneBookmarks.VisibleRegion==region.id)
                        for(int z=0;z<region.chunkGridSize.y;z++)for(int x=0;x<region.chunkGridSize.x;x++)
                            Handles.Label(Surface(origin.x+(x+.5f)*world.chunkSize,origin.z+(z+.5f)*world.chunkSize),$"{x+1}, {z+1}",cellLabel);
                }
            }
            finally{Handles.color=oldColor;Handles.zTest=oldDepth;}
        }
    }
}
