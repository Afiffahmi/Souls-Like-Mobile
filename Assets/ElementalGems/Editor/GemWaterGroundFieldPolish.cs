using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace ElementalGems.Editor
{
    public static class GemWaterGroundFieldPolish
    {
        const string Root="Assets/ElementalGems/FantasyVFX";
        static ParticleSystem.MinMaxCurve Range(float min,float max)=>new ParticleSystem.MinMaxCurve(min,max);
        static Material Material(string name,string shader)
        {
            string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}else m.shader=Shader.Find(shader);
            m.SetFloat("_Fade",1);return m;
        }
        static Mesh SaveMesh(string name,List<Vector3> v,List<Vector2> uv,List<int> t)
        {
            string path=Root+"/Meshes/"+name+".asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(m==null){m=new Mesh{name=name};AssetDatabase.CreateAsset(m,path);}
            m.Clear();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();var b=m.bounds;b.Expand(new Vector3(.1f,1.8f,.1f));m.bounds=b;EditorUtility.SetDirty(m);return m;
        }
        static Mesh Surface()
        {
            const int sides=80,rings=18;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int j=0;j<=rings;j++)for(int k=0;k<=sides;k++)
            {
                float a=k*Mathf.PI*2/sides;float radius=(float)j/rings*(.93f+.035f*Mathf.Sin(a*5+.7f)+.022f*Mathf.Sin(a*11+1.7f));var p=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);v.Add(p);uv.Add(new Vector2(p.x,p.z)*.5f+Vector2.one*.5f);
                if(j<rings&&k<sides){int b=j*(sides+1)+k;t.AddRange(new[]{b,b+1,b+sides+1,b+1,b+sides+2,b+sides+1});}
            }
            return SaveMesh("Water Sculpted Whirlpool",v,uv,t);
        }
        static Mesh Crest(int variant)
        {
            const int segments=18,rows=4;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int j=0;j<=rows;j++)for(int k=0;k<=segments;k++)
            {
                float u=(float)k/segments,h=(float)j/rows,a=(u-.5f)*(2.3f+variant*.3f);
                float radius=.58f-h*.19f;float height=Mathf.Sin(u*Mathf.PI)*(.34f+.08f*Mathf.Sin(u*19+variant)+.045f*Mathf.Sin(u*37));
                v.Add(new Vector3(Mathf.Sin(a)*radius,h*height,Mathf.Cos(a)*radius-.35f+h*h*.14f));uv.Add(new Vector2(u,h));
                if(j<rows&&k<segments){int b=j*(segments+1)+k;t.AddRange(new[]{b,b+segments+1,b+1,b+1,b+segments+1,b+segments+2});}
            }
            return SaveMesh("Water Curling Crest "+variant,v,uv,t);
        }
        static Mesh Drop()
        {
            const int sides=8,rings=6;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int j=0;j<=rings;j++)for(int k=0;k<=sides;k++)
            {
                float u=(float)j/rings,a=k*Mathf.PI*2/sides,r=Mathf.Sin(u*Mathf.PI)*.4f*(1-u*.3f);
                v.Add(new Vector3(Mathf.Cos(a)*r,(u-.5f)*1.2f,Mathf.Sin(a)*r));uv.Add(new Vector2((float)k/sides,u));
                if(j<rings&&k<sides){int b=j*(sides+1)+k;t.AddRange(new[]{b,b+sides+1,b+1,b+1,b+sides+1,b+sides+2});}
            }
            return SaveMesh("Water Rounded Droplet",v,uv,t);
        }
        [MenuItem("Tools/Elemental Gems/Polish Water Ground Field")]
        public static void ApplyMenu()=>Apply();
        public static string Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            string path=Root+"/Prefabs/Water Ground Field.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var field=root.GetComponent<GemGroundField>();field.preserveAuthoredParticleRatios=true;field.synchronizeParticleFade=true;field.particleDensity=.95f;field.particleBudgetPerLayer=48;
                var surface=field.surface.GetComponent<MeshRenderer>();field.surface.name="Uneven whirlpool with rolling wave crests";field.surface.GetComponent<MeshFilter>().sharedMesh=Surface();
                var pool=Material("Water Ground Field","ElementalGems/Water Field Surface");surface.sharedMaterial=pool;EditorUtility.SetDirty(pool);
                var crest=Material("Water Field Foaming Crests","ElementalGems/Water Field Volume");crest.SetFloat("_Droplet",0);EditorUtility.SetDirty(crest);
                var drop=Material("Water Field Rounded Droplets","ElementalGems/Water Field Volume");drop.SetFloat("_Droplet",1);EditorUtility.SetDirty(drop);
                var foam=Material("Water Field Foam Flecks","ElementalGems/Fantasy Particles");foam.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Bubble.png"));foam.SetFloat("_DstBlend",10);foam.SetFloat("_Glow",1);foam.SetColor("_Tint",new Color(.65f,.91f,.96f));EditorUtility.SetDirty(foam);
                var meshes=Enumerable.Range(0,3).Select(Crest).ToArray();var droplet=Drop();
                foreach(var ps in field.particles)
                {
                    bool splash=ps.name.Contains("Splash"),drops=ps.name.Contains("droplets");ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.transform.localPosition=Vector3.up*(splash?.02f:drops?.045f:.025f);ps.transform.localRotation=Quaternion.identity;ps.transform.localScale=Vector3.one;
                    var main=ps.main;main.loop=true;main.playOnAwake=false;main.startSpeed=0;main.startLifetime=splash?Range(.55f,.95f):drops?Range(.4f,.75f):Range(.55f,1.1f);main.maxParticles=splash?20:drops?44:32;
                    main.startSize3D=splash;if(splash){main.startSizeX=Range(.7f,1.35f);main.startSizeY=Range(.12f,.22f);main.startSizeZ=main.startSizeX;}else main.startSize=drops?Range(.05f,.12f):Range(.07f,.19f);
                    main.startRotation3D=splash||drops;if(splash){main.startRotationX=0;main.startRotationY=Range(-Mathf.PI,Mathf.PI);main.startRotationZ=0;}else if(drops){main.startRotationX=Range(-.2f,.2f);main.startRotationY=Range(-Mathf.PI,Mathf.PI);main.startRotationZ=Range(-.2f,.2f);}else main.startRotation=Range(-Mathf.PI,Mathf.PI);
                    main.startColor=new ParticleSystem.MinMaxGradient(new Color(.75f,.9f,1,.8f),Color.white);main.gravityModifier=drops?.06f:0;
                    var emission=ps.emission;emission.rateOverTime=splash?13:drops?28:20;emission.SetBursts(new ParticleSystem.Burst[0]);
                    var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.MeshRenderer;shape.meshRenderer=surface;shape.meshShapeType=ParticleSystemMeshShapeType.Triangle;shape.rotation=Vector3.zero;shape.position=Vector3.zero;shape.scale=Vector3.one;shape.normalOffset=0;shape.useMeshColors=false;
                    var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;velocity.x=Range(-.14f,.14f);velocity.y=drops?Range(.18f,.38f):Range(0,0);velocity.z=Range(-.14f,.14f);
                    var noise=ps.noise;noise.enabled=false;
                    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.85f,.65f),new GradientAlphaKey(0,1)});var color=ps.colorOverLifetime;color.enabled=true;color.color=gradient;
                    var size=ps.sizeOverLifetime;size.enabled=true;size.separateAxes=splash;
                    var grow=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.25f),new Keyframe(.3f,1),new Keyframe(1,.25f)));
                    if(splash){size.x=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.7f,1,1.4f));size.z=size.x;size.y=grow;}else size.size=grow;
                    var rotation=ps.rotationOverLifetime;rotation.enabled=false;
                    var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=splash||drops?ParticleSystemRenderMode.Mesh:ParticleSystemRenderMode.HorizontalBillboard;renderer.sortMode=ParticleSystemSortMode.Distance;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.maxParticleSize=1;renderer.pivot=Vector3.zero;renderer.sharedMaterial=splash?crest:drops?drop:foam;
                    if(splash||drops){renderer.SetMeshes(splash?meshes:new[]{droplet});renderer.alignment=ParticleSystemRenderSpace.Local;renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV});}
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();return "Water field polished: animated sculpted whirlpool, irregular shoreline, 3D curling crests, rounded droplets and foam. Shared fade; 96 particle cap.";
        }
    }
}
