using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace ElementalGems.Editor
{
    public static class GemLightningGroundFieldPolish
    {
        const string Root="Assets/Data/ElementalGems/FantasyVFX";
        static ParticleSystem.MinMaxCurve Range(float min,float max)=>new ParticleSystem.MinMaxCurve(min,max);
        static Material Material(string name,string shader)
        {
            string path="Assets/Materials/ElementalGems/FantasyVFX/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}else m.shader=Shader.Find(shader);
            m.SetFloat("_Fade",1);return m;
        }
        static Mesh SaveMesh(string name,List<Vector3> v,List<Vector2> uv,List<int> t)
        {
            string path="Assets/Models/ElementalGems/FantasyVFX/"+name+".asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(m==null){m=new Mesh{name=name};AssetDatabase.CreateAsset(m,path);}
            m.Clear();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();var b=m.bounds;b.Expand(.12f);m.bounds=b;EditorUtility.SetDirty(m);return m;
        }
        static Mesh BoltMesh(int variant,bool crawl)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();var random=new System.Random(7919+variant*37+(crawl?113:0));
            float Rand(float min,float max)=>min+(float)random.NextDouble()*(max-min);
            void Tube(Vector3[] points,float width)
            {
                int start=v.Count;const int sides=5;
                for(int j=0;j<points.Length;j++)
                {
                    float u=(float)j/(points.Length-1);Vector3 tangent=(points[Mathf.Min(j+1,points.Length-1)]-points[Mathf.Max(j-1,0)]).normalized;
                    Vector3 x=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.right:Vector3.up).normalized,z=Vector3.Cross(tangent,x).normalized;
                    float radius=width*Mathf.Lerp(1,.25f,u);
                    for(int k=0;k<sides;k++)
                    {
                        float a=k*Mathf.PI*2/sides;v.Add(points[j]+(x*Mathf.Cos(a)+z*Mathf.Sin(a))*radius);uv.Add(new Vector2((float)k/sides,u));
                        if(j<points.Length-1){int a0=start+j*sides+k,a1=start+j*sides+(k+1)%sides,b0=a0+sides,b1=a1+sides;t.AddRange(new[]{a0,a1,b0,a1,b1,b0});}
                    }
                }
            }
            const int segments=9;var trunk=new Vector3[segments];
            for(int j=0;j<segments;j++)
            {
                float u=(float)j/(segments-1);
                trunk[j]=crawl?new Vector3((u-.5f)*1.65f,.06f+Mathf.Sin(u*Mathf.PI)*.38f+Rand(-.055f,.055f),Rand(-.15f,.15f)):new Vector3(Rand(-.18f,.18f)+u*.2f,u*1.5f,Rand(-.14f,.14f));
            }
            Tube(trunk,.027f);
            for(int branch=0;branch<3;branch++)
            {
                int index=2+branch*2;Vector3 origin=trunk[index];var points=new Vector3[4];points[0]=origin;
                float side=branch%2==0?1:-1;
                for(int j=1;j<4;j++)points[j]=origin+new Vector3(side*j*.14f+(j%2)*.09f,(crawl?-.055f:.13f)*j,Rand(-.11f,.11f)+j*.08f);
                Tube(points,.015f);
            }
            return SaveMesh("Lightning "+(crawl?"Crawling Arc ":"Forked Strike ")+variant,v,uv,t);
        }
        [MenuItem("Tools/Elemental Gems/Polish Lightning Ground Field")]
        public static void ApplyMenu()=>Apply();
        public static string Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            string path="Assets/Prefabs/ElementalGems/FantasyVFX/Lightning Ground Field.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var field=root.GetComponent<GemGroundField>();field.preserveAuthoredParticleRatios=true;field.synchronizeParticleFade=true;field.particleDensity=1.1f;field.particleBudgetPerLayer=48;
                var v=new List<Vector3>{Vector3.zero};var uv=new List<Vector2>{Vector2.one*.5f};var t=new List<int>();const int count=80;
                for(int j=0;j<count;j++){float a=j*Mathf.PI*2/count;float r=.91f+.05f*Mathf.Sin(a*5+.8f)+.03f*Mathf.Sin(a*11+2)+.018f*Mathf.Sin(a*19);var p=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);v.Add(p);uv.Add(new Vector2(p.x,p.z)*.5f+Vector2.one*.5f);t.AddRange(new[]{0,(j+1)%count+1,j+1});}
                field.surface.GetComponent<MeshFilter>().sharedMesh=SaveMesh("Lightning Fractured Footprint",v,uv,t);field.surface.name="Fractured electrical vortex";var surface=field.surface.GetComponent<MeshRenderer>();
                var ground=Material("Lightning Ground Field","ElementalGems/Lightning Field Surface");ground.SetFloat("_Intensity",1.2f);EditorUtility.SetDirty(ground);surface.sharedMaterial=ground;
                var bolts=Material("Lightning Field Volume Bolts","ElementalGems/Lightning Field Bolt");bolts.SetFloat("_Glow",1.7f);EditorUtility.SetDirty(bolts);
                var sparks=Material("Lightning Field Cyan Sparks","ElementalGems/Fantasy Particles");sparks.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/ElementalGems/FantasyVFX/Streak.png"));sparks.SetFloat("_DstBlend",1);sparks.SetFloat("_Glow",1.8f);sparks.SetColor("_Tint",Color.white);EditorUtility.SetDirty(sparks);
                var standing=Enumerable.Range(0,3).Select(j=>BoltMesh(j,false)).ToArray();var crawling=Enumerable.Range(0,3).Select(j=>BoltMesh(j,true)).ToArray();
                var systems=field.particles.ToList();var source=systems.First(p=>p.name.Contains("Forked bolt"));
                var arcs=systems.FirstOrDefault(p=>p.name=="Crawling electrical arcs over ground");
                if(arcs==null){arcs=Object.Instantiate(source,root.transform,false);arcs.name="Crawling electrical arcs over ground";systems.Add(arcs);}
                foreach(var ps in systems)
                {
                    bool spark=ps.name.Contains("spark"),crawl=ps==arcs;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.transform.localPosition=Vector3.up*.045f;ps.transform.localRotation=Quaternion.identity;
                    var main=ps.main;main.loop=true;main.playOnAwake=false;main.startSpeed=0;main.startLifetime=spark?Range(.25f,.65f):crawl?Range(.18f,.35f):Range(.16f,.32f);main.maxParticles=spark?40:crawl?32:24;
                    main.startSize3D=!spark;if(spark)main.startSize=Range(.06f,.16f);else {main.startSizeX=Range(.8f,1.3f);main.startSizeY=crawl?Range(.65f,1):Range(.45f,.85f);main.startSizeZ=Range(.8f,1.3f);}
                    main.startRotation3D=!spark;if(spark)main.startRotation=Range(-Mathf.PI,Mathf.PI);else {main.startRotationX=0;main.startRotationY=Range(-Mathf.PI,Mathf.PI);main.startRotationZ=0;}
                    main.startColor=spark?new ParticleSystem.MinMaxGradient(new Color(.18f,.65f,1),new Color(.6f,.88f,1)):new ParticleSystem.MinMaxGradient(new Color(.4f,.7f,1),Color.white);
                    var emission=ps.emission;emission.rateOverTime=spark?18:crawl?35:28;emission.SetBursts(new ParticleSystem.Burst[0]);
                    var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.MeshRenderer;shape.meshRenderer=surface;shape.meshShapeType=ParticleSystemMeshShapeType.Triangle;shape.rotation=Vector3.zero;shape.position=Vector3.zero;shape.scale=Vector3.one;shape.normalOffset=0;shape.useMeshColors=false;
                    var velocity=ps.velocityOverLifetime;velocity.enabled=spark;velocity.space=ParticleSystemSimulationSpace.Local;velocity.x=Range(-.18f,.18f);velocity.y=Range(.25f,.65f);velocity.z=Range(-.18f,.18f);
                    var noise=ps.noise;noise.enabled=spark;noise.strength=.13f;noise.frequency=1.8f;noise.scrollSpeed=.7f;noise.octaveCount=1;noise.quality=ParticleSystemNoiseQuality.Low;
                    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.07f),new GradientAlphaKey(.8f,.45f),new GradientAlphaKey(0,1)});
                    var color=ps.colorOverLifetime;color.enabled=true;color.color=gradient;
                    var size=ps.sizeOverLifetime;size.enabled=spark;size.separateAxes=false;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.1f));
                    var rotation=ps.rotationOverLifetime;rotation.enabled=false;
                    var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=spark?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.Mesh;renderer.sortMode=ParticleSystemSortMode.Distance;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.maxParticleSize=1;renderer.pivot=Vector3.zero;
                    renderer.sharedMaterial=spark?sparks:bolts;
                    if(!spark){renderer.SetMeshes(crawl?crawling:standing);renderer.alignment=ParticleSystemRenderSpace.Local;renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV});}
                }
                field.particles=systems.ToArray();PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();return "Lightning ground field polished: irregular cyan-violet vortex, 3D forked strikes, crawling arcs and sparks. Shared lifetime fade; 96-particle cap.";
        }
    }
}
