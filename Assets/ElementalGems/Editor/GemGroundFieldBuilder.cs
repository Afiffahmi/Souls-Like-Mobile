using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace ElementalGems.Editor
{
    public static class GemGroundFieldBuilder
    {
        const string Root="Assets/ElementalGems/FantasyVFX";
        [MenuItem("Tools/Elemental Gems/Build Bow Ground Fields")]
        public static void BuildMenu()=>Build();
        public static string Build()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Exit Play Mode first.");
            var shader=Shader.Find("ElementalGems/Ground Field");
            if(shader==null)throw new System.InvalidOperationException("Ground field shader missing.");
            string settingsPath=Root+"/Styles/Bow Ground Field Settings.asset";
            var settings=AssetDatabase.LoadAssetAtPath<GemGroundFieldSettings>(settingsPath);
            if(settings==null){settings=ScriptableObject.CreateInstance<GemGroundFieldSettings>();AssetDatabase.CreateAsset(settings,settingsPath);}
            settings.elementPrefabs=new GemGroundField[8];
            string meshPath=Root+"/Meshes/Ground Field Surface.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(mesh==null)
            {
                mesh=new Mesh{name="Ground field surface"};mesh.vertices=new[]{new Vector3(-1,0,-1),new Vector3(-1,0,1),new Vector3(1,0,1),new Vector3(1,0,-1)};
                mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,meshPath);
            }
            for(int i=0;i<8;i++)
            {
                var element=(ElementType)i;
                var style=i==0?null:AssetDatabase.LoadAssetAtPath<GemVfxStyle>($"{Root}/Styles/{element}.asset");
                var root=new GameObject(element+" Bow Ground Field");
                try
                {
                    var field=root.AddComponent<GemGroundField>();field.particleDensity=element==ElementType.Fire?1.7f:.7f;
                    var surface=new GameObject("Elemental ground surface and radius boundary");surface.transform.SetParent(root.transform,false);field.surface=surface.transform;
                    surface.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=surface.AddComponent<MeshRenderer>();renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                    string matPath=$"{Root}/Materials/{element} Ground Field.mat";
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,matPath);}
                    mat.SetColor("_Tint",i==0?new Color(.55f,.45f,.3f,.45f):style.trailColor);mat.SetFloat("_Style",i);mat.SetFloat("_Intensity",i==0?.6f:1.35f);
                    mat.SetFloat("_Speed",element==ElementType.Lightning?3:1.2f);EditorUtility.SetDirty(mat);renderer.sharedMaterial=mat;
                    string[] layers=element switch
                    {
                        ElementType.Fire=>new[]{"Flame tongues","Yellow flame cores","Rising embers","Charcoal smoke"},
                        ElementType.Water=>new[]{"Liquid droplets","Bubble highlights","Splash crowns"},
                        ElementType.Nature=>new[]{"Living leaves","Golden pollen","Emerald wisps"},
                        ElementType.Earth=>new[]{"Floating stone fragments","Ochre dust plumes","Molten golden fissures"},
                        ElementType.Lightning=>new[]{"Forked bolt flashes","Electric spark streaks"},
                        ElementType.Wind=>new[]{"Curved air blades","Fast air streaks"},
                        ElementType.Darkness=>new[]{"Black shadow smoke","Violet shadow flames","Void motes"},
                        _=>new[]{"Ochre dust plumes"}
                    };
                    var source=style!=null?style.swordAura:AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Earth Sword.prefab");
                    var systems=new List<ParticleSystem>();
                    foreach(string label in layers)
                    {
                        var ps=Object.Instantiate(source.GetComponentsInChildren<ParticleSystem>(true).First(p=>p.name==label),root.transform,false);ps.name=label+" over ground";
                        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.transform.localPosition=Vector3.up*.04f;
                        var main=ps.main;main.loop=true;main.playOnAwake=false;main.duration=4;main.maxParticles=120;main.startSpeed=0;
                        main.startLifetime=new ParticleSystem.MinMaxCurve(.5f,1.1f);main.scalingMode=ParticleSystemScalingMode.Shape;
                        if(label.Contains("Flame")||label.Contains("flame")||label.Contains("smoke"))main.startSize=new ParticleSystem.MinMaxCurve(.45f,.9f);
                        if(label=="Forked bolt flashes")main.startSize=new ParticleSystem.MinMaxCurve(.35f,.9f);
                        var emission=ps.emission;emission.enabled=true;emission.rateOverTime=45;emission.SetBursts(new ParticleSystem.Burst[0]);
                        var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=1;shape.radiusThickness=1;shape.rotation=new Vector3(90,0,0);shape.scale=Vector3.one;shape.position=Vector3.zero;shape.arc=360;
                        systems.Add(ps);
                    }
                    field.particles=systems.ToArray();
                    settings.elementPrefabs[i]=PrefabUtility.SaveAsPrefabAsset(root,$"{Root}/Prefabs/{element} Ground Field.prefab").GetComponent<GemGroundField>();
                }
                finally{Object.DestroyImmediate(root);}
            }
            GemFireGroundFieldPolish.Apply();
            GemLightningGroundFieldPolish.Apply();
            GemNatureGroundFieldPolish.Apply();
            GemWaterGroundFieldPolish.Apply();
            GemDarknessGroundFieldPolish.Apply();
            GemWindGroundFieldPolish.Apply();
            var shooter=Object.FindAnyObjectByType<PlayerBowShooter>();Undo.RecordObject(shooter,"Configure heavy bow damage fields");shooter.heavyImpactRadius=4;shooter.heavyGroundField=settings;
            EditorUtility.SetDirty(shooter);EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(shooter.gameObject.scene);EditorSceneManager.SaveScene(shooter.gameObject.scene);
            return "Eight ground field prefabs saved; 4m radius, 4s duration, 12 base damage/second, 0.5s ticks. Heavy bow release wired.";
        }
    }
}
