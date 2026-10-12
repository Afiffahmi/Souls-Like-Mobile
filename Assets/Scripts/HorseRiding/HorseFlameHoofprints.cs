using UnityEngine;
using UnityEngine.Rendering;

namespace SoulsLike.Riding
{
    /// <summary>Short-lived, world-space fire marks triggered by animated hoof contact.</summary>
    [DefaultExecutionOrder(140), DisallowMultipleComponent, RequireComponent(typeof(HorseMotionAura))]
    public sealed class HorseFlameHoofprints : MonoBehaviour
    {
        public HorseController horse;
        public HorseMotionAura aura;
        public Transform[] hooves;
        public Material printMaterial, wispMaterial;
        [Min(.1f)] public float lifetime = 1f;
        [Min(.02f)] public float printSize = .3f;
        [Min(.02f)] public float contactHeight = .16f;
        [Min(.01f)] public float liftHysteresis = .06f;
        [Min(.05f)] public float minimumStepInterval = .16f;
        public int EmittedSteps { get; private set; }
        ParticleSystem prints, wisps;
        GameObject effects;
        Mesh quad;
        bool[] grounded;
        float[] lastStep;
        readonly RaycastHit[] hits = new RaycastHit[24];
        int groundMask;

        void Awake()
        {
            if (!horse) horse = GetComponent<HorseController>();
            if (!aura) aura = GetComponent<HorseMotionAura>();
            ResetContacts();
        }
        void ResetContacts()
        {
            int count = hooves == null ? 0 : hooves.Length;
            grounded = new bool[count]; lastStep = new float[count];
            for (int i = 0; i < count; i++) lastStep[i] = -10;
        }
        void LateUpdate()
        {
            if (!horse || !aura || hooves == null) return;
            if (grounded == null || grounded.Length != hooves.Length) ResetContacts();
            bool moving = horse.isActiveAndEnabled && !horse.IsDead && horse.CurrentSpeed > .1f && aura.AuraAmount > .025f;
            if (!moving) { for (int i = 0; i < grounded.Length; i++) grounded[i] = false; return; }
            groundMask = horse.groundLayers;
            for (int i = 0; i < hooves.Length; i++)
            {
                if (!hooves[i]) continue;
                Vector3 hoof = hooves[i].position;
                int count = Physics.RaycastNonAlloc(hoof + Vector3.up * .35f, Vector3.down, hits, .95f, groundMask, QueryTriggerInteraction.Ignore);
                float distance = float.PositiveInfinity; RaycastHit ground = default;
                for (int j = 0; j < count; j++)
                {
                    var hit = hits[j];
                    if (hit.transform.IsChildOf(transform) || (horse.Rider && hit.transform.IsChildOf(horse.Rider.transform)) || hit.normal.y < .45f) continue;
                    if (hit.distance < distance) { distance = hit.distance; ground = hit; }
                }
                float gap = distance - .35f;
                if (gap > contactHeight + liftHysteresis) grounded[i] = false;
                if (gap <= contactHeight && gap >= -.15f && !grounded[i])
                {
                    grounded[i] = true;
                    if (Time.time - lastStep[i] >= minimumStepInterval)
                    {
                        lastStep[i] = Time.time;
                        EmitStep(ground.point, ground.normal, aura.CurrentColor, horse.Motion == HorseMotion.Run);
                    }
                }
            }
        }
        void EnsureParticles()
        {
            if (effects || !printMaterial || !wispMaterial) return;
            effects = new GameObject("Temporary Horse Flame Hoofprints") { hideFlags = HideFlags.DontSave };
            effects.transform.SetParent(transform, false);
            quad = new Mesh { name = "Hoofprint quad", hideFlags = HideFlags.DontSave };
            quad.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0) };
            quad.uv = new[] { Vector2.zero,Vector2.right,Vector2.up,Vector2.one };
            quad.triangles = new[] {0,1,2,2,1,3};quad.RecalculateBounds();
            prints = Create("Fading Hoof Marks",printMaterial,32,lifetime);
            var pr=prints.GetComponent<ParticleSystemRenderer>();pr.renderMode=ParticleSystemRenderMode.Mesh;pr.mesh=quad;
            var main=prints.main;main.startRotation3D=true;
            wisps = Create("Hoof Flame Wisps",wispMaterial,96,.45f);
            var velocity=wisps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            velocity.y=new ParticleSystem.MinMaxCurve(.25f,.55f);
            velocity.x=new ParticleSystem.MinMaxCurve(-.07f,.07f);velocity.z=new ParticleSystem.MinMaxCurve(-.07f,.07f);
            var noise=wisps.noise;noise.enabled=true;noise.strength=.06f;noise.frequency=2;noise.quality=ParticleSystemNoiseQuality.Low;
            var size=wisps.sizeOverLifetime;size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.25f),new Keyframe(.2f,1),new Keyframe(1,0)));
            var block=new MaterialPropertyBlock();block.SetColor("_Tint",Color.white);block.SetFloat("_Intensity",1.6f);
            wisps.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(block);
        }
        ParticleSystem Create(string name,Material material,int capacity,float seconds)
        {
            var go=new GameObject(name);go.layer=gameObject.layer;go.transform.SetParent(effects.transform,false);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.scalingMode=ParticleSystemScalingMode.Shape;main.maxParticles=capacity;main.startLifetime=seconds;main.startSpeed=0;
            var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
            var color=ps.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.9f,.08f),new GradientAlphaKey(.5f,.5f),new GradientAlphaKey(0,1)});
            color.color=gradient;
            var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            ps.Play();return ps;
        }
        void EmitStep(Vector3 point,Vector3 normal,Color color,bool running)
        {
            EnsureParticles();if(!prints || !wisps)return;
            float size=printSize*(running?1.1f:1);
            Vector3 forward=Vector3.ProjectOnPlane(transform.forward,normal).normalized;
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(Vector3.forward,normal).normalized;
            Quaternion rotation=Quaternion.LookRotation(normal,forward);
            color.a=1;
            prints.Emit(new ParticleSystem.EmitParams {position=point+normal*.018f,rotation3D=rotation.eulerAngles,startColor=color,
                startSize=size,startLifetime=Mathf.Clamp(lifetime,.1f,2f)},1);
            for(int i=0;i<5;i++)
            {
                float angle=i*6.283185f/5;
                Vector3 offset=(forward*Mathf.Sin(angle)+Vector3.Cross(normal,forward)*Mathf.Cos(angle))*size*.3f;
                wisps.Emit(new ParticleSystem.EmitParams{position=point+normal*.025f+offset,startColor=color,
                    startSize=size*.9f,startLifetime=.3f+i*.035f},1);
            }
            EmittedSteps++;
        }
        void OnDisable()
        {
            if(prints)prints.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(wisps)wisps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void OnEnable()
        {
            ResetContacts();
            if(prints)prints.Play();if(wisps)wisps.Play();
        }
        void OnDestroy()
        {
            if(Application.isPlaying){if(effects)Destroy(effects);if(quad)Destroy(quad);}
            else {if(effects)DestroyImmediate(effects);if(quad)DestroyImmediate(quad);}
        }
    }
}
