using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace ElementalGems.Editor
{
    public static class GemDarknessGroundFieldPolish
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
            m.Clear();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();var b=m.bounds;b.Expand(new Vector3(.12f,.65f,.12f));m.bounds=b;EditorUtility.SetDirty(m);return m;
        }
        static Mesh Surface()
        {
            const int sides=72,rings=16;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int j=0;j<=rings;j++)for(int k=0;k<=sides;k++)
            {
                float a=k*Mathf.PI*2/sides,r=(float)j/rings*(.94f+.031f*Mathf.Sin(a*5+2)+.02f*Mathf.Sin(a*13+.6f));var p=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);v.Add(p);uv.Add(new Vector2(p.x,p.z)*.5f+Vector2.one*.5f);
                if(j<rings&&k<sides){int b=j*(sides+1)+k;t.AddRange(new[]{b,b+1,b+sides+1,b+1,b+sides+2,b+sides+1});}
            }
            return SaveMesh("Darkness Shallow Vortex",v,uv,t);
        }
        static Mesh Wisp(int variant)
        {
            const int rings=11,sides=8;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int j=0;j<=rings;j++)for(int k=0;k<=sides;k++)
            {
                float h=(float)j/rings,a=k*Mathf.PI*2/sides;
                float radius=Mathf.Pow(1-h,.8f)*(.24f+.045f*Mathf.Sin(h*13+variant));
                float twist=h*2.8f+variant;
                Vector3 center=new Vector3(Mathf.Sin(twist)*h*h*.4f,h*.75f,Mathf.Cos(twist)*h*h*.4f);
                v.Add(center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius*.8f));uv.Add(new Vector2((float)k/sides,h));
                if(j<rings&&k<sides){int b=j*(sides+1)+k;t.AddRange(new[]{b,b+sides+1,b+1,b+1,b+sides+1,b+sides+2});}
            }
            return SaveMesh("Darkness Curling Shadow "+variant,v,uv,t);
        }
        [MenuItem("Tools/Elemental Gems/Polish Darkness Ground Field")]
        public static void ApplyMenu()=>Apply();
        public static string Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            string path="Assets/Prefabs/ElementalGems/FantasyVFX/Darkness Ground Field.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var field=root.GetComponent<GemGroundField>();field.preserveAuthoredParticleRatios=true;field.synchronizeParticleFade=true;field.particleDensity=.9f;field.particleBudgetPerLayer=48;
                var surface=field.surface.GetComponent<MeshRenderer>();field.surface.name="Frayed shadow vortex and dark core";field.surface.GetComponent<MeshFilter>().sharedMesh=Surface();
                var pool=Material("Darkness Ground Field","ElementalGems/Darkness Field Surface");surface.sharedMaterial=pool;EditorUtility.SetDirty(pool);
                var shadow=Material("Darkness Field Curled Wisps","ElementalGems/Darkness Field Wisps");EditorUtility.SetDirty(shadow);
                Material ParticleMat(string name,string texture)
                {
                    var m=Material(name,"ElementalGems/Fantasy Particles");m.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/ElementalGems/FantasyVFX/"+texture+".png"));m.SetFloat("_DstBlend",10);m.SetFloat("_Glow",1);m.SetColor("_Tint",Color.white);m.SetVector("_Flow",new Vector4(.7f,.025f,0,0));EditorUtility.SetDirty(m);return m;
                }
                var smoke=ParticleMat("Darkness Field Ground Smoke","Shadow");var ash=ParticleMat("Darkness Field Ash","Ember");
                var meshes=Enumerable.Range(0,3).Select(Wisp).ToArray();
                foreach(var ps in field.particles)
                {
                    bool wisp=ps.name.Contains("flames"),fog=ps.name.Contains("smoke");ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.transform.localPosition=Vector3.up*(fog?.09f:.025f);ps.transform.localRotation=Quaternion.identity;ps.transform.localScale=Vector3.one;
                    var main=ps.main;main.loop=true;main.playOnAwake=false;main.startSpeed=0;main.startLifetime=wisp?Range(.75f,1.25f):fog?Range(.8f,1.45f):Range(.6f,1.25f);main.maxParticles=wisp?16:fog?16:32;
                    main.startSize3D=wisp;if(wisp){main.startSizeX=Range(.65f,1.15f);main.startSizeY=Range(.32f,.65f);main.startSizeZ=main.startSizeX;}else main.startSize=fog?Range(.9f,1.7f):Range(.035f,.085f);
                    main.startRotation3D=wisp;if(wisp){main.startRotationX=0;main.startRotationY=Range(-Mathf.PI,Mathf.PI);main.startRotationZ=0;}else main.startRotation=Range(-Mathf.PI,Mathf.PI);
                    main.startColor=wisp?new ParticleSystem.MinMaxGradient(new Color(.6f,.5f,.7f),Color.white):fog?new ParticleSystem.MinMaxGradient(new Color(.012f,.01f,.02f,.4f),new Color(.04f,.035f,.05f,.55f)):new ParticleSystem.MinMaxGradient(new Color(.004f,.004f,.007f),new Color(.085f,.065f,.1f));main.gravityModifier=0;
                    var emission=ps.emission;emission.rateOverTime=wisp?13:fog?7:21;emission.SetBursts(new ParticleSystem.Burst[0]);
                    var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.MeshRenderer;shape.meshRenderer=surface;shape.meshShapeType=ParticleSystemMeshShapeType.Triangle;shape.rotation=Vector3.zero;shape.position=Vector3.zero;shape.scale=Vector3.one;shape.normalOffset=0;shape.useMeshColors=false;
                    var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;velocity.x=Range(-.09f,.09f);velocity.y=wisp?Range(0,0):fog?Range(.035f,.08f):Range(.18f,.45f);velocity.z=Range(-.09f,.09f);
                    var noise=ps.noise;noise.enabled=!wisp;noise.strength=.09f;noise.frequency=.75f;noise.scrollSpeed=.35f;noise.octaveCount=1;noise.quality=ParticleSystemNoiseQuality.Low;
                    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(.9f,.6f),new GradientAlphaKey(0,1)});var color=ps.colorOverLifetime;color.enabled=true;color.color=gradient;
                    var size=ps.sizeOverLifetime;size.enabled=true;size.separateAxes=false;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.25f),new Keyframe(.25f,1),new Keyframe(1,fog?1.3f:.3f)));
                    var rotation=ps.rotationOverLifetime;rotation.enabled=fog;rotation.separateAxes=false;rotation.z=Range(-.12f,.12f);
                    var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=wisp?ParticleSystemRenderMode.Mesh:ParticleSystemRenderMode.Billboard;renderer.sortMode=ParticleSystemSortMode.Distance;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.maxParticleSize=1;renderer.pivot=Vector3.zero;renderer.sharedMaterial=wisp?shadow:fog?smoke:ash;
                    if(wisp){renderer.SetMeshes(meshes);renderer.alignment=ParticleSystemRenderSpace.Local;renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV});}
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();return "Darkness field polished: low frayed vortex, dark central core, three 3D shadow-wisp variants, low smoke and ash; shared fade and 64-particle cap.";
        }
    }
}
