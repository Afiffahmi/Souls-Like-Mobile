using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalGems
{
    /// <summary>Animated elemental bonds, attached to the shield's actual blend shape vertices.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SkinnedMeshRenderer)), DefaultExecutionOrder(300)]
    public sealed class GemShieldEffects : MonoBehaviour
    {
        [Serializable] public struct Anchor
        {
            public Vector3 position;
            public Vector3[] blendDeltas;
        }
        public GemManager manager;
        public PlayerShieldAnimation deployment;
        public Material ribbonMaterial;
        public Material glowMaterial;
        public Material[] particleMaterials;
        public Anchor[] anchors = Array.Empty<Anchor>();
        public Vector3 core = new Vector3(0, .00010f, -.00525f);
        public float shieldHeight = .03f;
        [Range(.2f, 2f)] public float intensity = 1;
        [Tooltip("A quiet silver bond for the normal gem.")]
        public bool normalEnergy = true;
        private const int Segments = 25;
        private SkinnedMeshRenderer shield;
        private GameObject visualRoot;
        private LineRenderer[] ribbons, halos;
        private Vector3[][] points;
        private LineRenderer coreRing;
        private ParticleSystem motes;
        private ParticleSystem coreGlow;
        private readonly ParticleSystem.Particle[] coreParticle = new ParticleSystem.Particle[1];
        private ParticleSystemRenderer moteRenderer;
        private float[] weights;
        private float clock, emissionCarry;
        private ElementType element;
        private Color primary, highlight;
        private bool visible;
        public ElementType ActiveElement => element;
        public bool EffectsVisible => visible;

        private void Awake()
        {
            shield = GetComponent<SkinnedMeshRenderer>();
            if (deployment == null) deployment = GetComponent<PlayerShieldAnimation>();
            if (manager == null) manager = GetComponentInParent<GemManager>();
        }
        private void OnEnable()
        {
            if (manager != null) manager.GemChanged += Refresh;
            Refresh(manager != null ? manager.Equipped : null);
        }
        private void Refresh(GemDefinition gem)
        {
            element = gem != null ? gem.element : ElementType.Normal;
            Palette(element, out primary, out highlight);
            emissionCarry = 0;
            if (motes != null)
            {
                motes.Clear();
                SetParticleMaterial();
            }
        }
        public static void Palette(ElementType e, out Color color, out Color hot)
        {
            switch(e)
            {
                case ElementType.Lightning: color=new Color(.035f,.46f,1);hot=new Color(.62f,.96f,1);break;
                case ElementType.Fire: color=new Color(1,.12f,.008f);hot=new Color(1,.76f,.14f);break;
                case ElementType.Water: color=new Color(.015f,.35f,.85f);hot=new Color(.16f,.95f,1);break;
                case ElementType.Nature: color=new Color(.08f,.62f,.12f);hot=new Color(.66f,1,.25f);break;
                case ElementType.Earth: color=new Color(.6f,.24f,.035f);hot=new Color(1,.7f,.22f);break;
                case ElementType.Wind: color=new Color(.35f,.8f,.77f);hot=new Color(.82f,1,1);break;
                case ElementType.Darkness: color=new Color(.28f,.025f,.65f);hot=new Color(.85f,.28f,1);break;
                default: color=new Color(.24f,.38f,.5f);hot=new Color(.7f,.84f,.95f);break;
            }
        }
        private void Build()
        {
            if (visualRoot != null || ribbonMaterial == null || glowMaterial == null || anchors.Length == 0) return;
            shield = GetComponent<SkinnedMeshRenderer>();
            weights = new float[shield.sharedMesh.blendShapeCount];
            visualRoot = new GameObject("Elemental Shield Bonds");
            visualRoot.transform.SetParent(transform, false);visualRoot.layer=gameObject.layer;
            ribbons=new LineRenderer[anchors.Length];halos=new LineRenderer[anchors.Length];points=new Vector3[anchors.Length][];
            for(int i=0;i<anchors.Length;i++)
            {
                ribbons[i]=Line("Bond "+i,ribbonMaterial,Segments);
                halos[i]=Line("Bond glow "+i,ribbonMaterial,Segments);
                points[i]=new Vector3[Segments];
            }
            coreRing=Line("Moving core current",ribbonMaterial,65);
            var go=new GameObject("Elemental sparks and droplets");go.layer=gameObject.layer;go.transform.SetParent(visualRoot.transform,false);
            motes=go.AddComponent<ParticleSystem>();motes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=motes.main;main.playOnAwake=false;main.loop=true;main.maxParticles=120;
            main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            main.startSpeed=0;main.startLifetime=.5f;
            var emission=motes.emission;emission.enabled=false;var shape=motes.shape;shape.enabled=false;
            var size=motes.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.2f),new Keyframe(.18f,1),new Keyframe(1,0)));
            var col=motes.colorOverLifetime;col.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.1f),new GradientAlphaKey(.6f,.5f),new GradientAlphaKey(0,1)});col.color=gradient;
            moteRenderer=go.GetComponent<ParticleSystemRenderer>();moteRenderer.shadowCastingMode=ShadowCastingMode.Off;moteRenderer.receiveShadows=false;
            SetParticleMaterial();motes.Play();
            var coreGo=new GameObject("Pulsing gem halo");coreGo.layer=gameObject.layer;coreGo.transform.SetParent(visualRoot.transform,false);
            coreGlow=coreGo.AddComponent<ParticleSystem>();coreGlow.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var coreMain=coreGlow.main;coreMain.playOnAwake=false;coreMain.maxParticles=1;coreMain.simulationSpace=ParticleSystemSimulationSpace.Local;coreMain.scalingMode=ParticleSystemScalingMode.Hierarchy;
            var coreEmission=coreGlow.emission;coreEmission.enabled=false;var coreShape=coreGlow.shape;coreShape.enabled=false;
            coreGo.GetComponent<ParticleSystemRenderer>().sharedMaterial=glowMaterial;
        }
        private void SetParticleMaterial()
        {
            int index=(int)element;
            moteRenderer.sharedMaterial=particleMaterials!=null && index<particleMaterials.Length && particleMaterials[index]!=null?particleMaterials[index]:glowMaterial;
        }
        private LineRenderer Line(string label, Material material, int count)
        {
            var go=new GameObject(label);go.layer=gameObject.layer;go.transform.SetParent(visualRoot.transform,false);
            var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=material;
            line.positionCount=count;line.numCapVertices=2;line.numCornerVertices=2;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            line.widthCurve=new AnimationCurve(new Keyframe(0,.6f),new Keyframe(.15f,1),new Keyframe(.75f,.75f),new Keyframe(1,.3f));
            return line;
        }
        private void LateUpdate()
        {
            float amount=deployment!=null?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.65f,deployment.NormalizedTime)):1;
            Tick(Time.deltaTime,amount);
        }
        private void Tick(float dt,float amount)
        {
            Build();if(visualRoot==null)return;
            visible=amount>.001f && shield.enabled && (element!=ElementType.Normal || normalEnergy);
            if(visualRoot.activeSelf!=visible)
            {
                visualRoot.SetActive(visible);
                if(!visible){motes.Clear();emissionCarry=0;}
                else motes.Play();
            }
            if(!visible)return;
            clock+=Mathf.Max(0,dt);
            for(int j=0;j<weights.Length;j++)weights[j]=shield.GetBlendShapeWeight(j)*.01f;
            float worldHeight=transform.TransformVector(Vector3.forward*shieldHeight).magnitude;
            float energy=amount*intensity*(element==ElementType.Normal?.35f:1);
            bool lightning=element==ElementType.Lightning;
            float width=worldHeight*(lightning?.0065f:element==ElementType.Water?.022f:element==ElementType.Fire?.016f:.009f);
            for(int a=0;a<anchors.Length;a++)
            {
                var anchor=anchors[a];Vector3 end=anchor.position;
                for(int j=0;j<weights.Length && j<anchor.blendDeltas.Length;j++)end+=anchor.blendDeltas[j]*weights[j];
                Vector3 start=core;
                Vector3 direction=end-start;Vector3 side=Vector3.Cross(direction,Vector3.up).normalized;
                // Secondary bonds meet the primary trunk before forking toward another point on each plate.
                if(a%2==1)start=points[a-1][8];
                float phase=clock*(element==ElementType.Water?3:element==ElementType.Fire?7:2.5f)+a*1.73f;
                for(int i=0;i<Segments;i++)
                {
                    float u=i/(float)(Segments-1);float envelope=Mathf.Sin(u*Mathf.PI);
                    Vector3 p=Vector3.Lerp(start,end,u);
                    if(lightning)
                    {
                        float step=Mathf.Floor(clock*22);
                        p+=side*(Noise(a*173+i*31+step*79)*shieldHeight*.026f*envelope);
                        p.y+=Noise(a*29+i*71+step*13)*shieldHeight*.004f*envelope;
                    }
                    else if(element==ElementType.Fire)
                    {
                        p+=side*(Mathf.Sin(u*15-phase)*.019f+Mathf.Sin(u*29-phase*1.7f)*.008f)*shieldHeight*envelope;
                        p.z-=envelope*shieldHeight*.035f*(.6f+.4f*Mathf.Sin(phase+u*9));
                    }
                    else
                    {
                        float loops=element==ElementType.Wind?18:element==ElementType.Nature?11:8;
                        float amplitude=element==ElementType.Earth?.007f:.025f;
                        p+=side*Mathf.Sin(u*loops-phase)*shieldHeight*amplitude*envelope;
                        p.y+=Mathf.Cos(u*loops-phase)*shieldHeight*.008f*envelope;
                    }
                    points[a][i]=p;
                }
                ribbons[a].SetPositions(points[a]);halos[a].SetPositions(points[a]);
                float pulse=lightning?.65f+.35f*Mathf.Abs(Mathf.Sin(clock*39+a*4)):.85f+.15f*Mathf.Sin(phase);
                var c=highlight;c.a=energy*pulse;
                ribbons[a].startColor=c;ribbons[a].endColor=c;ribbons[a].widthMultiplier=width*(a%2==0?1:.62f);
                c=primary;c.a=energy*(lightning?.55f:.38f)*pulse;
                halos[a].startColor=c;halos[a].endColor=c;halos[a].widthMultiplier=width*(element==ElementType.Water?3:4.5f);
            }
            float radius=shieldHeight*(.048f+.002f*Mathf.Sin(clock*3));
            for(int i=0;i<65;i++)
            {
                float angle=i/64f*Mathf.PI*2;
                float ripple=1+(element==ElementType.Water?.12f:.05f)*Mathf.Sin(angle*5-clock*3);
                coreRing.SetPosition(i,core+new Vector3(Mathf.Cos(angle)*radius*ripple,-shieldHeight*.003f,Mathf.Sin(angle)*radius*ripple*1.2f));
            }
            var ringColor=primary;ringColor.a=.65f*energy;coreRing.startColor=ringColor;coreRing.endColor=ringColor;coreRing.widthMultiplier=worldHeight*.009f;
            var coreColor=primary;coreColor.a=.8f*energy;
            coreParticle[0].position=core+Vector3.up*shieldHeight*.005f;
            coreParticle[0].startColor=coreColor;coreParticle[0].startSize=shieldHeight*(.2f+.015f*Mathf.Sin(clock*4));
            coreParticle[0].startLifetime=100;coreParticle[0].remainingLifetime=100;coreGlow.SetParticles(coreParticle,1);
            emissionCarry+=Mathf.Min(dt,.05f)*(element==ElementType.Fire?100:65)*energy;
            while(emissionCarry>=1){emissionCarry--;EmitMote();}
        }
        private static float Noise(float seed)=>Mathf.Sin(seed*12.9898f+78.233f)*.65f+Mathf.Sin(seed*4.1414f)*.35f;
        private int particleSerial;
        private void EmitMote()
        {
            int serial=particleSerial++;int a=serial%points.Length;float u=(Noise(serial*17)+1)*.5f;
            int segment=Mathf.Clamp((int)(u*(Segments-1)),0,Segments-1);
            Vector3 position=points[a][segment];
            Vector3 radial=(position-core).normalized;
            Vector3 velocity=radial*shieldHeight*.07f;
            if(element==ElementType.Fire)velocity=new Vector3(Noise(serial)*.03f,-.005f,-.17f)*shieldHeight;
            if(element==ElementType.Water)velocity=(radial*.08f+new Vector3(.025f*Noise(serial),0,.05f))*shieldHeight;
            if(element==ElementType.Darkness)velocity=-radial*shieldHeight*.08f;
            float size=shieldHeight*(element==ElementType.Fire?.065f:element==ElementType.Water?.028f:.014f);
            var emit=new ParticleSystem.EmitParams{position=position,velocity=velocity,startColor=Color.Lerp(primary,highlight,(Noise(serial*3)+1)*.5f),startLifetime=element==ElementType.Lightning?.22f:.65f,startSize=size*(.65f+.35f*Mathf.Abs(Noise(serial*11))),rotation=Noise(serial*13)*180};
            motes.Emit(emit,1);
        }
        // Editor review uses a temporary clone; no preview overrides persist onto the player.
        public void Preview(ElementType type,float time,float delta=1f/30)
        {
            Build();element=type;Palette(type,out primary,out highlight);SetParticleMaterial();clock=time;
            Tick(delta,1);if(!Application.isPlaying)motes.Simulate(delta,false,false);
        }
        private void OnDisable()
        {
            if(manager!=null)manager.GemChanged-=Refresh;
            visible=false;
            if(visualRoot!=null){visualRoot.SetActive(false);if(Application.isPlaying)Destroy(visualRoot);else DestroyImmediate(visualRoot);}
            visualRoot=null;motes=null;emissionCarry=0;
        }
    }
}
