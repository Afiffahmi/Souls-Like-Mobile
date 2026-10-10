using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace ElementalGems.Editor
{
    public static class GemNatureGroundFieldPolish
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
            m.Clear();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();var b=m.bounds;b.Expand(.08f);m.bounds=b;EditorUtility.SetDirty(m);return m;
        }
        static void Leaf(List<Vector3> v,List<Vector2> uv,List<int> t,Vector3 center,Vector3 direction,float size)
        {
            int k=v.Count;var side=Vector3.Cross(direction.normalized,Vector3.up).normalized*size*.38f;
            v.AddRange(new[]{center,center+direction.normalized*size*.48f-side,center+direction.normalized*size+Vector3.up*size*.1f,center+direction.normalized*size*.48f+side,center+direction.normalized*size*.48f+Vector3.up*size*.15f});
            uv.AddRange(new[]{new Vector2(2.5f,0),new Vector2(2,.5f),new Vector2(2.5f,1),new Vector2(3,.5f),new Vector2(2.5f,.5f)});
            t.AddRange(new[]{k,k+4,k+1,k+1,k+4,k+2,k+2,k+4,k+3,k+3,k+4,k});
        }
        static Mesh RootMesh(int variant,bool creeping)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();float phase=variant*1.7f;
            Vector3 Path(float u)=>creeping?new Vector3(Mathf.Sin(u*4+phase)*u*.5f,.025f+Mathf.Sin(u*Mathf.PI)*(.16f+variant*.025f),u*1.65f):new Vector3(Mathf.Sin(u*5+phase)*u*.47f,u*(1.05f+variant*.09f)-Mathf.Pow(Mathf.Max(0,u-.55f),2)*1.65f,Mathf.Cos(u*5+phase)*u*.38f);
            void Tube(Func<float,Vector3> path,int segments,int sides,float width)
            {
                int first=v.Count;
                for(int j=0;j<=segments;j++)
                {
                    float u=(float)j/segments;Vector3 p=path(u),tangent=(path(Mathf.Min(1,u+.01f))-path(Mathf.Max(0,u-.01f))).normalized;
                    Vector3 x=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.right:Vector3.up).normalized,z=Vector3.Cross(tangent,x);
                    float radius=width*Mathf.Pow(1-u,.7f)+.006f;
                    for(int k=0;k<=sides;k++)
                    {
                        float a=(float)k/sides*Mathf.PI*2;float ridge=1+.055f*Mathf.Sin(a*3+u*15+phase);
                        v.Add(p+(x*Mathf.Cos(a)+z*Mathf.Sin(a))*radius*ridge);uv.Add(new Vector2((float)k/sides,u));
                        if(j<segments&&k<sides){int a0=first+j*(sides+1)+k,a1=a0+1,b0=a0+sides+1,b1=b0+1;t.AddRange(new[]{a0,a1,b0,a1,b1,b0});}
                    }
                }
            }
            Tube(Path,14,8,creeping?.095f:.19f);
            for(int branch=0;branch<2;branch++)
            {
                float along=.23f+branch*.28f;Vector3 origin=Path(along);float angle=phase+branch*2.8f;
                var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Vector3 Branch(float u)=>origin+direction*u*(creeping?.75f:.55f)+new Vector3(Mathf.Sin(u*6)*.07f,Mathf.Sin(u*Mathf.PI)*.18f-u*origin.y*.65f,Mathf.Cos(u*5)*u*.08f);
                Tube(Branch,8,6,creeping?.044f:.075f);
                Leaf(v,uv,t,Branch(.6f),direction+Vector3.up*.15f,creeping?.2f:.27f);
            }
            Leaf(v,uv,t,Path(.72f),new Vector3(Mathf.Cos(phase),.2f,Mathf.Sin(phase)),creeping?.18f:.24f);
            return SaveMesh("Nature "+(creeping?"Creeping Tendril ":"Twisted Root ")+variant,v,uv,t);
        }
        [MenuItem("Tools/Elemental Gems/Polish Nature Ground Field")]
        public static void ApplyMenu()=>Apply();
        public static string Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            string path="Assets/Prefabs/ElementalGems/FantasyVFX/Nature Ground Field.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var field=root.GetComponent<GemGroundField>();field.preserveAuthoredParticleRatios=true;field.synchronizeParticleFade=true;field.particleDensity=1.15f;field.particleBudgetPerLayer=48;
                var v=new List<Vector3>{Vector3.zero};var uv=new List<Vector2>{Vector2.one*.5f};var t=new List<int>();const int count=80;
                for(int j=0;j<count;j++){float a=j*Mathf.PI*2/count;float r=.91f+.05f*Mathf.Sin(a*5+2)+.032f*Mathf.Sin(a*9-.8f)+.019f*Mathf.Sin(a*17+1);var p=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);v.Add(p);uv.Add(new Vector2(p.x,p.z)*.5f+Vector2.one*.5f);t.AddRange(new[]{0,(j+1)%count+1,j+1});}
                field.surface.GetComponent<MeshFilter>().sharedMesh=SaveMesh("Nature Overgrown Footprint",v,uv,t);field.surface.name="Irregular moss and living root bed";var surface=field.surface.GetComponent<MeshRenderer>();
                var ground=Material("Nature Ground Field","ElementalGems/Nature Field Surface");EditorUtility.SetDirty(ground);surface.sharedMaterial=ground;
                var bark=Material("Nature Field Living Bark","ElementalGems/Nature Field Roots");EditorUtility.SetDirty(bark);
                Material ParticleMat(string name,string texture,float glow,float blend)
                {
                    var m=Material(name,"ElementalGems/Fantasy Particles");m.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/ElementalGems/FantasyVFX/"+texture+".png"));m.SetFloat("_DstBlend",blend);m.SetFloat("_Glow",glow);m.SetColor("_Tint",Color.white);EditorUtility.SetDirty(m);return m;
                }
                var pollen=ParticleMat("Nature Field Luminous Spores","Ember",1.5f,1);var wisp=ParticleMat("Nature Field Ground Mist","Smoke",1,10);
                var standing=Enumerable.Range(0,4).Select(j=>RootMesh(j,false)).ToArray();var creeping=Enumerable.Range(0,4).Select(j=>RootMesh(j,true)).ToArray();
                v=new List<Vector3>();uv=new List<Vector2>();t=new List<int>();Leaf(v,uv,t,Vector3.zero,Vector3.forward,1);var leafMesh=SaveMesh("Nature Field Curved Leaf",v,uv,t);
                var systems=field.particles.ToList();var source=systems.First(p=>p.name.Contains("leaves"));
                ParticleSystem Layer(string name){var p=systems.FirstOrDefault(s=>s.name==name);if(p==null){p=Object.Instantiate(source,root.transform,false);p.name=name;systems.Add(p);}return p;}
                var roots=Layer("Twisting roots emerging from ground");var tendrils=Layer("Creeping vines emerging from ground");
                foreach(var ps in systems)
                {
                    bool thick=ps==roots,thin=ps==tendrils,plant=thick||thin,leaf=ps==source,mist=ps.name.Contains("wisps"),spore=!plant&&!leaf&&!mist;
                    ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.transform.localPosition=Vector3.up*(plant?.008f:mist?.12f:.04f);ps.transform.localRotation=Quaternion.identity;ps.transform.localScale=Vector3.one;
                    var main=ps.main;main.loop=true;main.playOnAwake=false;main.startSpeed=0;main.startLifetime=plant?Range(1.8f,2.5f):mist?Range(.8f,1.5f):Range(.55f,1.2f);main.maxParticles=thick?14:thin?20:mist?12:24;
                    main.startSize3D=plant;if(plant){main.startSizeX=thick?Range(.65f,1.3f):Range(.65f,1.35f);main.startSizeY=thick?Range(.55f,1.15f):Range(.65f,1.3f);main.startSizeZ=main.startSizeX;}else main.startSize=leaf?Range(.12f,.25f):mist?Range(.9f,1.6f):Range(.025f,.06f);
                    main.startRotation3D=plant||leaf;if(plant){main.startRotationX=0;main.startRotationY=Range(-Mathf.PI,Mathf.PI);main.startRotationZ=0;}else if(leaf){main.startRotationX=Range(-1.2f,1.2f);main.startRotationY=Range(-Mathf.PI,Mathf.PI);main.startRotationZ=Range(-.5f,.5f);}else main.startRotation=Range(-Mathf.PI,Mathf.PI);
                    main.startColor=plant||leaf?new ParticleSystem.MinMaxGradient(new Color(.73f,.83f,.67f),Color.white):mist?new ParticleSystem.MinMaxGradient(new Color(.12f,.38f,.25f,.13f),new Color(.28f,.57f,.39f,.18f)):new ParticleSystem.MinMaxGradient(new Color(.42f,1,.65f),new Color(.88f,1,.57f));
                    var emission=ps.emission;emission.rateOverTime=thick?4:thin?5:mist?4:leaf?10:14;emission.SetBursts(plant?new[]{new ParticleSystem.Burst(.02f,(short)(thick?8:12))}:new ParticleSystem.Burst[0]);
                    var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.MeshRenderer;shape.meshRenderer=surface;shape.meshShapeType=ParticleSystemMeshShapeType.Triangle;shape.rotation=Vector3.zero;shape.position=Vector3.zero;shape.scale=Vector3.one;shape.normalOffset=0;shape.useMeshColors=false;
                    var velocity=ps.velocityOverLifetime;velocity.enabled=!plant;velocity.space=ParticleSystemSimulationSpace.Local;velocity.x=Range(-.12f,.16f);velocity.y=mist?Range(.04f,.12f):Range(.15f,.45f);velocity.z=Range(-.12f,.16f);
                    var noise=ps.noise;noise.enabled=!plant;noise.strength=mist?.08f:.15f;noise.frequency=.7f;noise.scrollSpeed=.3f;noise.octaveCount=1;noise.quality=ParticleSystemNoiseQuality.Low;
                    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,plant?.055f:.15f),new GradientAlphaKey(1,.77f),new GradientAlphaKey(0,1)});
                    var color=ps.colorOverLifetime;color.enabled=true;color.color=gradient;
                    var size=ps.sizeOverLifetime;size.enabled=true;size.separateAxes=plant;
                    if(plant){size.x=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.25f),new Keyframe(.22f,1),new Keyframe(1,1)));size.z=size.x;size.y=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.015f),new Keyframe(.32f,1),new Keyframe(1,1)));}
                    else size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.4f),new Keyframe(.2f,1),new Keyframe(1,mist?1.4f:.4f)));
                    var rotation=ps.rotationOverLifetime;rotation.enabled=leaf;rotation.separateAxes=true;rotation.x=Range(-.45f,.45f);rotation.y=Range(-.3f,.3f);rotation.z=Range(-.4f,.4f);
                    var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=plant||leaf?ParticleSystemRenderMode.Mesh:ParticleSystemRenderMode.Billboard;renderer.sortMode=ParticleSystemSortMode.Distance;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.maxParticleSize=1;renderer.pivot=Vector3.zero;renderer.sharedMaterial=plant||leaf?bark:mist?wisp:pollen;
                    if(plant||leaf){renderer.SetMeshes(thick?standing:thin?creeping:new[]{leafMesh});renderer.alignment=ParticleSystemRenderSpace.Local;renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV});}
                }
                field.particles=systems.ToArray();PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();return "Nature field polished: eight 3D root/vine mesh variants, anchored growth, curved leaves, spores, mist and irregular moss bed. All five layers share the ground fade; cap 94; at most 34 root/vine clusters.";
        }
    }
}
