using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ElementalGems.Editor
{
    public static class GemCrescentBuilder
    {
        const string Root = "Assets/ElementalGems/FantasyVFX";
        [MenuItem("Tools/Elemental Gems/Build Broad Light Slashes")]
        public static void BuildMenu() => Build();
        public static string Build()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Build in Edit Mode.");
            var shader = Shader.Find("ElementalGems/Broad Crescent Slash");
            if (shader == null) throw new System.InvalidOperationException("Crescent shader has not imported.");
            var mesh = BuildMesh();
            var slashes = new List<GemCrescentSlash>();
            for (int e = 1; e <= 7; e++)
            {
                var element = (ElementType)e;
                var style = AssetDatabase.LoadAssetAtPath<GemVfxStyle>($"{Root}/Styles/{element}.asset");
                var root = new GameObject(element + " Broad Light Slash");
                try
                {
                    var fx = root.AddComponent<GemCrescentSlash>();
                    fx.element = element;
                    fx.color = style.trailColor;
                    if (element == ElementType.Nature) fx.color = new Color(.35f,1,.035f);
                    if (element == ElementType.Water) fx.color = new Color(.045f,.82f,1);
                    fx.intensity = element == ElementType.Darkness ? 1.65f : 1.45f;
                    fx.lifetime = element == ElementType.Lightning ? .34f : .46f;
                    fx.particlesPerLayer = element == ElementType.Wind ? 12 : 18;
                    var filter = root.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
                    var renderer = root.AddComponent<MeshRenderer>();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    string path = $"{Root}/Materials/{element} Broad Crescent.mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material,path); }
                    material.SetColor("_Tint",fx.color);material.SetFloat("_Intensity",fx.intensity);
                    material.SetFloat("_FlowSpeed",element==ElementType.Lightning?8:element==ElementType.Wind?6:3);
                    material.SetFloat("_Turbulence",element==ElementType.Fire?.9f:element==ElementType.Darkness?.8f:.4f);
                    EditorUtility.SetDirty(material); renderer.sharedMaterial=material;
                    string[] accents = element switch
                    {
                        ElementType.Fire => new[]{"Flame tongues","Rising embers","Charcoal smoke"},
                        ElementType.Water => new[]{"Bubble highlights","Liquid droplets"},
                        ElementType.Nature => new[]{"Living leaves","Golden pollen"},
                        ElementType.Earth => new[]{"Floating stone fragments","Ochre dust plumes","Mineral sparks"},
                        ElementType.Lightning => new[]{"Forked bolt flashes","Electric spark streaks"},
                        ElementType.Wind => new[]{"Curved air blades","Fast air streaks"},
                        _ => new[]{"Black shadow smoke","Violet shadow flames","Shadow fragments"}
                    };
                    var particles = new List<ParticleSystem>();
                    foreach(string label in accents)
                    {
                        var source = style.swordAura.GetComponentsInChildren<ParticleSystem>(true).First(p=>p.name==label);
                        var ps = Object.Instantiate(source,root.transform,false);ps.name=label+" across sweep";
                        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=1.2f;
                        main.startLifetime=new ParticleSystem.MinMaxCurve(.35f,.85f);main.maxParticles=24;
                        main.simulationSpace=ParticleSystemSimulationSpace.World;
                        if(label=="Bubble highlights"||label=="Living leaves")main.startSize=new ParticleSystem.MinMaxCurve(.12f,.24f);
                        var emission=ps.emission;emission.enabled=false;emission.SetBursts(new ParticleSystem.Burst[0]);
                        var shape=ps.shape;shape.enabled=false;
                        particles.Add(ps);
                    }
                    fx.accents=particles.ToArray();
                    var prefab=PrefabUtility.SaveAsPrefabAsset(root,$"{Root}/Prefabs/{element} Light Slash.prefab");
                    slashes.Add(prefab.GetComponent<GemCrescentSlash>());
                }
                finally { Object.DestroyImmediate(root); }
            }
            var player=Object.FindAnyObjectByType<PlayerStateManager>();
            var observer=player.GetComponent<GemLightSlashEffects>()??Undo.AddComponent<GemLightSlashEffects>(player.gameObject);
            Undo.RecordObject(observer,"Attach wide elemental light slashes");observer.elementalSlashes=slashes.ToArray();
            EditorUtility.SetDirty(observer);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);EditorSceneManager.SaveScene(player.gameObject.scene);
            return "Seven broad light-slash prefabs and materials, shared layered crescent mesh, and player visual observer saved.";
        }
        static Mesh BuildMesh()
        {
            var verts=new List<Vector3>();var uv=new List<Vector2>();var uv2=new List<Vector2>();
            var colors=new List<Color>();var triangles=new List<int>();
            void Band(float radius,float width,float arc,float offset,float layer,float opacity)
            {
                const int along=80,across=8;
                int start=verts.Count;
                for(int i=0;i<=along;i++)
                {
                    float u=(float)i/along;
                    float a=((u-.5f)*arc+offset)*Mathf.Deg2Rad;
                    float taper=Mathf.Pow(Mathf.Max(.001f,Mathf.Sin(u*Mathf.PI)),.7f)*(.7f+.6f*u);
                    for(int j=0;j<=across;j++)
                    {
                        float v=(float)j/across;
                        float r=radius+(v-.5f)*width*taper;
                        verts.Add(new Vector3(Mathf.Sin(a)*r,layer*.004f,Mathf.Cos(a)*r));
                        uv.Add(new Vector2(u,v));uv2.Add(new Vector2(layer,radius));colors.Add(new Color(1,1,1,opacity));
                        if(i==along||j==across)continue;
                        int k=start+i*(across+1)+j;
                        triangles.AddRange(new[]{k,k+across+1,k+1,k+1,k+across+1,k+across+2});
                    }
                }
            }
            // Broad energy body, separated inner/outer filaments, and a soft halo.
            Band(2.12f,1.52f,172,0,0,1);
            Band(2.65f,.19f,184,-4,1,.8f);
            Band(1.54f,.24f,158,7,1,.6f);
            Band(2.20f,2.0f,176,0,2,1);
            string path=Root+"/Meshes/Layered Crescent.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh{name="Layered 172 degree crescent"};AssetDatabase.CreateAsset(mesh,path);}
            mesh.Clear();mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetUVs(1,uv2);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.bounds=new Bounds(new Vector3(0,0,1),new Vector3(7,.2f,5));
            EditorUtility.SetDirty(mesh);return mesh;
        }
    }
}
