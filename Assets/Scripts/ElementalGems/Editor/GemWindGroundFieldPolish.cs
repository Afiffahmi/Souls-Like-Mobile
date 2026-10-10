using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace ElementalGems.Editor
{
    public static class GemWindGroundFieldPolish
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
            m.Clear();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();var b=m.bounds;b.Expand(.18f);m.bounds=b;EditorUtility.SetDirty(m);return m;
        }
        static Mesh Surface()
        {
            var v=new List<Vector3>{Vector3.zero};var uv=new List<Vector2>{Vector2.one*.5f};var t=new List<int>();const int count=80;
            for(int j=0;j<count;j++){float a=j*Mathf.PI*2/count,r=.46f+.025f*Mathf.Sin(a*5+1)+.018f*Mathf.Sin(a*11+2);var p=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);v.Add(p);uv.Add(new Vector2(p.x,p.z)*.5f+Vector2.one*.5f);t.AddRange(new[]{0,(j+1)%count+1,j+1});}
            var detail=Enumerable.Repeat(Vector2.zero,v.Count).ToList();
            const int rings=22,sides=48;
            // Two separated shells give the wind depth from the isometric camera.
            for(int layer=0;layer<2;layer++)
            {
                int first=v.Count;
                for(int j=0;j<=rings;j++)for(int k=0;k<=sides;k++)
                {
                    float h=(float)j/rings,a=k*Mathf.PI*2/sides;
                    float radius=(.12f+.70f*Mathf.Pow(h,.85f))*(1+.065f*Mathf.Sin(a*3+h*12))*(layer==0?1:.63f);
                    float lean=h*h*.045f;
                    v.Add(new Vector3(Mathf.Cos(a)*radius+lean,h*5f,Mathf.Sin(a)*radius));uv.Add(new Vector2(2+layer*2+(float)k/sides,h));detail.Add(new Vector2(h,layer));
                    if(j<rings&&k<sides){int b=first+j*(sides+1)+k;t.AddRange(new[]{b,b+sides+1,b+1,b+1,b+sides+1,b+sides+2});}
                }
            }
            // Separate tapered helical strips; these rotate at different speeds in the shader.
            for(int strand=0;strand<6;strand++)
            {
                int first=v.Count;const int steps=96;
                for(int j=0;j<=steps;j++)for(int row=0;row<2;row++)
                {
                    float u=(float)j/steps,h=.04f+u*(.84f+(strand%3)*.025f);
                    float a=u*Mathf.PI*(3.2f+(strand%3)*.4f)+strand*2.39996f;
                    float radius=(.12f+.70f*Mathf.Pow(h,.85f))*(1.02f+(strand%3)*.045f);
                    float width=(strand%2==0?.48f:.20f)*Mathf.Sin(u*Mathf.PI);
                    v.Add(new Vector3(Mathf.Cos(a)*radius+h*h*.045f,h*5f+(row-.5f)*width,Mathf.Sin(a)*radius));
                    uv.Add(new Vector2(6+u,row));detail.Add(new Vector2(h,strand));
                    if(j<steps&&row==0){int b=first+j*2;t.AddRange(new[]{b,b+1,b+2,b+1,b+3,b+2});}
                }
            }
            var mesh=SaveMesh("Wind Compact Tornado and Footprint",v,uv,t);mesh.SetUVs(1,detail);
            var bounds=mesh.bounds;bounds.Expand(.35f);mesh.bounds=bounds;EditorUtility.SetDirty(mesh);return mesh;
        }
        static Mesh DustEmitter()
        {
            var v=new List<Vector3>{new Vector3(0,.05f,0)};var uv=new List<Vector2>{Vector2.zero};var t=new List<int>();
            for(int j=0;j<32;j++){float a=j*Mathf.PI/16;v.Add(new Vector3(Mathf.Cos(a)*.32f,.05f,Mathf.Sin(a)*.32f));uv.Add(Vector2.zero);t.AddRange(new[]{0,j+1,(j+1)%32+1});}
            return SaveMesh("Wind Low Dust Emission",v,uv,t);
        }
        static Mesh Debris()
        {
            return SaveMesh("Wind Tumbling Fragments",new List<Vector3>{new Vector3(-.5f,0,0),new Vector3(0,.18f,.23f),new Vector3(.6f,0,0),new Vector3(0,-.1f,-.22f)},new List<Vector2>{new Vector2(0,.5f),new Vector2(.5f,1),new Vector2(1,.5f),new Vector2(.5f,0)},new List<int>{0,1,2,0,2,3,0,3,1,1,3,2});
        }
        static Mesh Ribbon(int variant)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();const int segments=24;
            for(int k=0;k<=segments;k++)for(int row=0;row<2;row++)
            {
                float u=(float)k/segments,a=u*(2.8f+variant*.35f),r=.52f;
                v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(u*Mathf.PI)*.12f+(row-.5f)*.085f,Mathf.Sin(a)*r));uv.Add(new Vector2(u,row));
                if(k<segments&&row==0){int b=k*2;t.AddRange(new[]{b,b+1,b+2,b+1,b+3,b+2});}
            }
            return SaveMesh("Wind Sweeping Air Ribbon "+variant,v,uv,t);
        }
        [MenuItem("Tools/Elemental Gems/Polish Wind Ground Field")]
        public static void ApplyMenu()=>Apply();
        public static string Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            string path="Assets/Prefabs/ElementalGems/FantasyVFX/Wind Ground Field.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var field=root.GetComponent<GemGroundField>();field.preserveAuthoredParticleRatios=true;field.synchronizeParticleFade=true;field.particleDensity=.85f;field.particleBudgetPerLayer=48;
                var surface=field.surface.GetComponent<MeshRenderer>();field.surface.name="Turbulent tornado and faint local wind contact";field.surface.GetComponent<MeshFilter>().sharedMesh=Surface();
                var ground=Material("Wind Ground Field","ElementalGems/Wind Field Surface");surface.sharedMaterial=ground;EditorUtility.SetDirty(ground);
                var emitterTransform=field.surface.Find("Low dust emission source");
                if(emitterTransform==null){emitterTransform=new GameObject("Low dust emission source",typeof(MeshFilter),typeof(MeshRenderer)).transform;emitterTransform.SetParent(field.surface,false);}
                emitterTransform.GetComponent<MeshFilter>().sharedMesh=DustEmitter();var emitterRenderer=emitterTransform.GetComponent<MeshRenderer>();emitterRenderer.enabled=false;
                var air=Material("Wind Field Air Ribbons","ElementalGems/Wind Field Air");EditorUtility.SetDirty(air);
                Material ParticleMat(string name,string texture,Color tint)
                {
                    var m=Material(name,"ElementalGems/Fantasy Particles");m.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/ElementalGems/FantasyVFX/"+texture+".png"));m.SetFloat("_DstBlend",10);m.SetFloat("_Glow",1);m.SetColor("_Tint",tint);EditorUtility.SetDirty(m);return m;
                }
                var dust=ParticleMat("Wind Field Dust","Dust",new Color(.72f,.55f,.32f,.3f));var specks=ParticleMat("Wind Field Debris","Ember",new Color(.45f,.92f,.82f,.75f));specks.SetFloat("_Glow",1.35f);
                var fragments=Material("Wind Field Tumbling Fragments","ElementalGems/Fantasy Particles");fragments.SetTexture("_MainTex",Texture2D.whiteTexture);fragments.SetColor("_Tint",Color.white);fragments.SetFloat("_DstBlend",10);fragments.SetFloat("_Glow",1);EditorUtility.SetDirty(fragments);var fragmentMesh=Debris();
                var meshes=Enumerable.Range(0,3).Select(Ribbon).ToArray();var systems=field.particles.ToList();var source=systems.First(p=>p.name.Contains("streaks"));
                var dustLayer=systems.FirstOrDefault(p=>p.name=="Loose dust carried by wind");if(dustLayer==null){dustLayer=Object.Instantiate(source,root.transform,false);dustLayer.name="Loose dust carried by wind";systems.Add(dustLayer);}
                var debrisLayer=systems.FirstOrDefault(p=>p.name=="Tumbling leaves and grit");if(debrisLayer==null){debrisLayer=Object.Instantiate(source,root.transform,false);debrisLayer.name="Tumbling leaves and grit";systems.Add(debrisLayer);}
                foreach(var ps in systems)
                {
                    bool blade=ps.name.Contains("blades"),fog=ps==dustLayer,debris=ps==debrisLayer;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.transform.localPosition=Vector3.up*.05f;ps.transform.localRotation=Quaternion.identity;ps.transform.localScale=Vector3.one;
                    var main=ps.main;main.loop=true;main.playOnAwake=false;main.startSpeed=0;main.startLifetime=blade?Range(.45f,.8f):fog?Range(.75f,1.25f):debris?Range(.8f,1.3f):Range(.5f,.95f);main.maxParticles=blade?24:fog?16:debris?16:28;
                    main.startSize3D=false;main.startSize=blade?Range(1.2f,2.2f):fog?Range(.5f,1.05f):debris?Range(.08f,.18f):Range(.025f,.065f);
                    main.startRotation3D=blade;if(blade){main.startRotationX=0;main.startRotationY=Range(-Mathf.PI,Mathf.PI);main.startRotationZ=0;}else main.startRotation=Range(-Mathf.PI,Mathf.PI);
                    main.startColor=debris?new ParticleSystem.MinMaxGradient(new Color(.28f,.42f,.24f),new Color(.8f,.56f,.25f)):new ParticleSystem.MinMaxGradient(new Color(.56f,.88f,.78f,.65f),new Color(1,.94f,.73f,1));main.gravityModifier=0;
                    var emission=ps.emission;emission.rateOverTime=blade?23:fog?10:debris?12:24;emission.SetBursts(new ParticleSystem.Burst[0]);
                    var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.MeshRenderer;shape.meshRenderer=surface;shape.meshShapeType=ParticleSystemMeshShapeType.Triangle;shape.rotation=Vector3.zero;shape.position=Vector3.zero;shape.scale=Vector3.one;shape.normalOffset=0;shape.useMeshColors=false;
                    if(fog)shape.meshRenderer=emitterRenderer;
                    var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;velocity.x=Range(-.1f,.1f);velocity.y=blade?Range(.1f,.25f):fog?Range(.2f,.45f):debris?Range(.45f,.8f):Range(.3f,.65f);velocity.z=Range(-.1f,.1f);
                    velocity.orbitalX=Range(0,0);velocity.orbitalY=Range(3.1f,4.4f);velocity.orbitalZ=Range(0,0);
                    var noise=ps.noise;noise.enabled=!blade;noise.strength=.16f;noise.frequency=1.2f;noise.scrollSpeed=1.1f;noise.octaveCount=1;noise.quality=ParticleSystemNoiseQuality.Low;
                    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.7f,.65f),new GradientAlphaKey(0,1)});var color=ps.colorOverLifetime;color.enabled=true;color.color=gradient;
                    var size=ps.sizeOverLifetime;size.enabled=true;size.separateAxes=false;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.4f),new Keyframe(.3f,1),new Keyframe(1,fog?1.3f:.7f)));
                    var rotation=ps.rotationOverLifetime;rotation.enabled=blade;rotation.separateAxes=true;rotation.x=Range(0,0);rotation.y=Range(2.8f,4.2f);rotation.z=Range(0,0);
                    if(debris){rotation.enabled=true;rotation.x=Range(2,5);rotation.z=Range(-4,-2);}
                    var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=blade?ParticleSystemRenderMode.Mesh:ParticleSystemRenderMode.Billboard;renderer.sortMode=ParticleSystemSortMode.Distance;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.maxParticleSize=1;renderer.pivot=Vector3.zero;renderer.sharedMaterial=blade?air:fog?dust:specks;
                    if(blade){renderer.SetMeshes(meshes);renderer.alignment=ParticleSystemRenderSpace.Local;renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV});}
                    if(debris){renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=fragmentMesh;renderer.sharedMaterial=fragments;renderer.alignment=ParticleSystemRenderSpace.Local;}
                }
                field.particles=systems.ToArray();PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();return "Wind field polished: layered 5m storm, teal inner core, ivory outer gusts, six independent helical streamers, warm low dust, tumbling fragments and motes. Shared fade; 84-particle cap.";
        }
    }
}


