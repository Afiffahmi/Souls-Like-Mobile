using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using ElementalGems;
using Object=UnityEngine.Object;

namespace ElementalGems.Editor
{
public static class GemFireGroundFieldPolish
{
    const string Root="Assets/Data/ElementalGems/FantasyVFX";
    const string Prefab="Assets/Prefabs/ElementalGems/FantasyVFX/Fire Ground Field.prefab";
    static ParticleSystem.MinMaxCurve Range(float a,float b)=>new ParticleSystem.MinMaxCurve(a,b);
    // Three asymmetric tapered volumes, 260 triangles shared by every flame.
    static Mesh FlameMesh()
    {
        string path="Assets/Models/ElementalGems/FantasyVFX/Fire Volumetric Tongues.asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh{name="Fire volumetric tongues"};AssetDatabase.CreateAsset(mesh,path);}
        var vertices=new System.Collections.Generic.List<Vector3>();
        var uv=new System.Collections.Generic.List<Vector2>();
        var triangles=new System.Collections.Generic.List<int>();
        void Tongue(int sides,int rings,Vector3 offset,float width,float height,float phase)
        {
            int first=vertices.Count;
            for(int row=0;row<=rings;row++)
            {
                float h=(float)row/rings;
                float radius=width*Mathf.Pow(1-h,.78f)*(.82f+.18f*Mathf.Sin(h*6+phase));
                var bend=new Vector3(Mathf.Sin(h*5+phase)*h*h*.19f,h*height,Mathf.Sin(h*4+phase+2)*h*h*.16f);
                for(int s=0;s<=sides;s++)
                {
                    float a=(float)s/sides*Mathf.PI*2;
                    float lobe=1+.17f*Mathf.Sin(a*3+h*5+phase);
                    vertices.Add(offset+bend+new Vector3(Mathf.Cos(a)*radius*lobe,0,Mathf.Sin(a)*radius*lobe));
                    uv.Add(new Vector2((float)s/sides,h));
                    if(row<rings&&s<sides)
                    {
                        int v=first+row*(sides+1)+s;
                        triangles.Add(v);triangles.Add(v+sides+1);triangles.Add(v+1);
                        triangles.Add(v+1);triangles.Add(v+sides+1);triangles.Add(v+sides+2);
                    }
                }
            }
        }
        Tongue(10,7,Vector3.zero,.31f,1,0);
        Tongue(6,5,new Vector3(.2f,0,.12f),.19f,.64f,1.8f);
        Tongue(6,5,new Vector3(-.14f,0,-.19f),.18f,.76f,3.5f);
        mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var bounds=mesh.bounds;bounds.Expand(.6f);mesh.bounds=bounds;EditorUtility.SetDirty(mesh);return mesh;
    }
    static Gradient Fade(Color color,float opacity)
    {
        var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(color,0),new GradientColorKey(color,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(opacity,.12f),new GradientAlphaKey(opacity*.8f,.5f),new GradientAlphaKey(0,1)});return g;
    }
    static Material Material(string label,string shader,string texture=null)
    {
        string path="Assets/Materials/ElementalGems/FantasyVFX/"+label+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(mat,path);}else mat.shader=Shader.Find(shader);
        if(texture!=null)mat.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/ElementalGems/FantasyVFX/"+texture+".png"));
        return mat;
    }
    [MenuItem("Tools/Elemental Gems/Polish Fire Ground Field")]
    public static void ApplyMenu() => Apply();
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode before applying prefab edits.");
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var field=root.GetComponent<GemGroundField>();
            field.particleDensity=1.7f;field.particleBudgetPerLayer=100;
            field.synchronizeParticleFade=true;
            var so=new SerializedObject(field);so.FindProperty("preserveAuthoredParticleRatios").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
            string meshPath="Assets/Models/ElementalGems/FantasyVFX/Fire Fractured Footprint.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(mesh==null){mesh=new Mesh{name="Fire fractured footprint"};AssetDatabase.CreateAsset(mesh,meshPath);}
            const int count=72;var vertices=new Vector3[count+1];var uv=new Vector2[count+1];var tris=new int[count*3];uv[0]=Vector2.one*.5f;
            for(int j=0;j<count;j++)
            {
                float a=j*Mathf.PI*2/count;
                float r=.87f+.065f*Mathf.Sin(a*5+1.3f)+.04f*Mathf.Sin(a*9-.7f)+.02f*Mathf.Sin(a*17+2);
                vertices[j+1]=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);uv[j+1]=new Vector2(vertices[j+1].x,vertices[j+1].z)*.5f+Vector2.one*.5f;
                tris[j*3]=0;tris[j*3+1]=(j+1)%count+1;tris[j*3+2]=j+1;
            }
            mesh.Clear();mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=tris;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            field.surface.name="Fractured scorch and glowing fissures";
            field.surface.GetComponent<MeshFilter>().sharedMesh=mesh;
            var surface=field.surface.GetComponent<MeshRenderer>();
            var ground=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ElementalGems/FantasyVFX/Fire Ground Field.mat");ground.shader=Shader.Find("ElementalGems/Fire Field Scorch");ground.SetFloat("_Intensity",1.4f);ground.SetFloat("_Fade",1);EditorUtility.SetDirty(ground);surface.sharedMaterial=ground;
            var flameMesh=FlameMesh();
            var flame=Material("Fire Field Layered Flame","ElementalGems/Fire Field Flame");flame.SetFloat("_Glow",1.4f);flame.SetFloat("_Speed",1.6f);flame.SetFloat("_Core",0);EditorUtility.SetDirty(flame);
            var core=Material("Fire Field Volume Core","ElementalGems/Fire Field Flame");core.SetFloat("_Glow",1.55f);core.SetFloat("_Speed",1.9f);core.SetFloat("_Core",1);EditorUtility.SetDirty(core);
            var smoke=Material("Fire Field Soft Smoke","ElementalGems/Fire Field Smoke","Smoke");EditorUtility.SetDirty(smoke);
            foreach(var ps in field.particles)
            {
                bool isSmoke=ps.name.Contains("smoke"),isEmber=ps.name.Contains("embers"),isCore=ps.name.Contains("cores");
                ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.transform.localPosition=Vector3.up*(isSmoke?.9f:.055f);ps.transform.localRotation=Quaternion.identity;ps.transform.localScale=Vector3.one;
                var main=ps.main;main.loop=true;main.playOnAwake=false;main.startSpeed=0;main.startRotation3D=false;
                main.maxParticles=isSmoke?28:isEmber?36:isCore?32:72;
                main.startLifetime=isSmoke?Range(1.5f,1.95f):isEmber?Range(.6f,1.3f):isCore?Range(.5f,.85f):Range(.85f,1.35f);
                main.startSize3D=true;main.startSizeX=isSmoke?Range(2.1f,3.5f):isEmber?Range(.025f,.065f):isCore?Range(.65f,1.05f):Range(1.35f,2.15f);
                main.startSizeY=isSmoke?Range(1.8f,2.8f):isEmber?Range(.055f,.12f):isCore?Range(.6f,1.0f):Range(.6f,1.05f);main.startSizeZ=(isSmoke||isEmber)?new ParticleSystem.MinMaxCurve(1):main.startSizeX;
                main.startRotation=isSmoke?Range(-3.14f,3.14f):isEmber?Range(-.5f,.5f):Range(-.12f,.12f);
                if(!isSmoke&&!isEmber){main.startRotation3D=true;main.startRotationX=Range(-.06f,.06f);main.startRotationY=Range(-Mathf.PI,Mathf.PI);main.startRotationZ=Range(-.06f,.06f);}
                main.startColor=isSmoke?new ParticleSystem.MinMaxGradient(new Color(.19f,.16f,.13f,1),new Color(.38f,.33f,.28f,1)):isEmber?new ParticleSystem.MinMaxGradient(new Color(1,.28f,.025f),new Color(1,.73f,.15f)):new ParticleSystem.MinMaxGradient(new Color(.55f,.5f,1),Color.white);
                var emission=ps.emission;emission.rateOverTime=isSmoke?9:isEmber?13:isCore?19:32;
                emission.SetBursts(new ParticleSystem.Burst[0]);
                var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.MeshRenderer;shape.meshRenderer=surface;shape.meshShapeType=ParticleSystemMeshShapeType.Triangle;shape.useMeshColors=false;shape.normalOffset=0;shape.rotation=Vector3.zero;shape.position=Vector3.zero;shape.scale=Vector3.one;shape.randomDirectionAmount=0;
                var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;
                velocity.x=Range(isSmoke?.06f:-.09f,isSmoke?.23f:.09f);velocity.y=isSmoke?Range(.8f,1.25f):isEmber?Range(.8f,1.9f):Range(.06f,.18f);velocity.z=Range(-.08f,.12f);
                var noise=ps.noise;noise.enabled=true;noise.separateAxes=true;noise.strengthX=isSmoke?.14f:isEmber?.15f:.12f;noise.strengthY=isSmoke?.08f:.045f;noise.strengthZ=isSmoke?.12f:.1f;noise.frequency=isSmoke?.5f:1.1f;noise.scrollSpeed=.55f;noise.octaveCount=1;noise.quality=ParticleSystemNoiseQuality.Low;
                var color=ps.colorOverLifetime;color.enabled=true;color.color=Fade(Color.white,isSmoke?.68f:isCore?.78f:1);
                var size=ps.sizeOverLifetime;size.enabled=true;size.separateAxes=false;
                size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,isSmoke?.55f:.55f),new Keyframe(.18f,1),new Keyframe(.65f,isSmoke?1.25f:.85f),new Keyframe(1,isSmoke?1.5f:.28f)));
                var rotation=ps.rotationOverLifetime;rotation.enabled=isSmoke;rotation.z=Range(-.17f,.17f);
                var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=(isSmoke||isEmber)?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.Mesh;renderer.sortMode=ParticleSystemSortMode.Distance;renderer.maxParticleSize=1;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                if(isSmoke)renderer.sharedMaterial=smoke;
                else if(!isEmber){renderer.mesh=flameMesh;renderer.alignment=ParticleSystemRenderSpace.Local;renderer.pivot=Vector3.zero;renderer.sharedMaterial=isCore?core:flame;renderer.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV});}
            }
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();return "Polished the original four fire field layers, dedicated materials and fractured emitter mesh.";
    }
}
}
