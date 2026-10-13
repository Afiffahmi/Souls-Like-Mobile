using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace Nusantara.World
{
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class WorldGrassInteraction : MonoBehaviour
    {
        public const int MaxInfluences=24;
        [Header("Grass reacts to all moving bodies")]
        public bool automaticallyFindActors=true;
        public Camera interactionCamera;
        [Min(5)] public float visibleDistance=80;
        [Range(1,12)] public int maximumActors=12;
        [Min(0)] public float footprintPadding=.45f;
        [Range(0,1)] public float bendStrength=.5f;
        [Range(.1f,3)] public float recoverySeconds=.8f;
        [Min(.1f)] public float verticalReach=.85f;
        [Range(.1f,2)] public float discoverySeconds=.5f;
        sealed class Body
        {
            public Transform root;public CharacterController controller;public NavMeshAgent agent;public Rigidbody physics;
            public Collider collider;public WorldGrassInteractor explicitBody;
            public Vector3 previous;public float distance,lastTrail;
        }
        struct Wake {public Vector3 feet;public Vector2 direction;public float radius,strength,age;}
        readonly List<Body> bodies=new List<Body>();
        readonly List<Body> nearby=new List<Body>();
        readonly List<Wake> wake=new List<Wake>();
        readonly HashSet<Transform> known=new HashSet<Transform>();
        readonly Vector4[] positions=new Vector4[MaxInfluences],motion=new Vector4[MaxInfluences];
        float discovery;
        static readonly int CountId=Shader.PropertyToID("_GrassInteractorCount"),PositionsId=Shader.PropertyToID("_GrassInteractorPositions"),MotionId=Shader.PropertyToID("_GrassInteractorMotion"),StrengthId=Shader.PropertyToID("_GrassInteractionStrength"),HeightId=Shader.PropertyToID("_GrassInteractionHeight");
        public int ActiveInfluenceCount {get;private set;}
        public int TrackedActorCount => bodies.Count;
        public void RegisterActor(Transform actor){if(actor!=null)Add(actor,actor.GetComponent<WorldGrassInteractor>());}
        void OnEnable(){DiscoverActors();}
        void LateUpdate(){if(Application.isPlaying)UpdateInfluences(Time.deltaTime);}
        public void DiscoverActors()
        {
            foreach(var custom in FindObjectsByType<WorldGrassInteractor>(FindObjectsSortMode.None))Add(custom.transform,custom);
            if(automaticallyFindActors)
            {
                foreach(var controller in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))Add(controller.transform,null);
                foreach(var agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))Add(agent.transform,null);
                foreach(var physics in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                {
                    if(physics.GetComponent<CharacterController>()!=null || physics.GetComponent<NavMeshAgent>()!=null)continue;
                    Add(physics.transform,null);
                }
                foreach(var animator in FindObjectsByType<Animator>(FindObjectsSortMode.None))
                    if(animator.GetComponentInParent<CharacterController>()==null && animator.GetComponentInParent<NavMeshAgent>()==null && animator.GetComponentInParent<Rigidbody>()==null && animator.GetComponentInParent<WorldGrassInteractor>()==null)Add(animator.transform,null);
            }
        }
        void Add(Transform root,WorldGrassInteractor custom)
        {
            if(root.gameObject.scene!=gameObject.scene)return;
            if(!known.Add(root))
            {
                if(custom!=null)foreach(var body in bodies)if(body.root==root){body.explicitBody=custom;break;}
                return;
            }
            bodies.Add(new Body{root=root,controller=root.GetComponent<CharacterController>(),agent=root.GetComponent<NavMeshAgent>(),physics=root.GetComponent<Rigidbody>(),collider=root.GetComponent<Collider>(),explicitBody=custom,previous=root.position});
        }
        public void UpdateInfluences(float deltaTime)
        {
            float dt=Mathf.Max(0,deltaTime);discovery-=dt;
            if(discovery<=0){DiscoverActors();discovery=Mathf.Max(.1f,discoverySeconds);}
            for(int i=wake.Count-1;i>=0;i--){var w=wake[i];w.age+=dt;if(w.age>=recoverySeconds)wake.RemoveAt(i);else wake[i]=w;}
            if(interactionCamera==null)interactionCamera=Camera.main;
            var focus=interactionCamera!=null?interactionCamera.transform.position:transform.position;
            nearby.Clear();
            for(int i=bodies.Count-1;i>=0;i--)
            {
                var b=bodies[i];if(b.root==null){known.Remove(b.root);bodies.RemoveAt(i);continue;}
                b.distance=(b.root.position-focus).sqrMagnitude;
                bool active=b.root.gameObject.activeInHierarchy && (b.controller!=null?b.controller.enabled:b.agent!=null?b.agent.enabled:true);
                if(active && b.distance<visibleDistance*visibleDistance)nearby.Add(b);else b.previous=b.root.position;
            }
            nearby.Sort((a,b)=>a.distance.CompareTo(b.distance));int count=0;
            for(int i=0;i<nearby.Count;i++)
            {
                var b=nearby[i];Vector3 step=b.root.position-b.previous;b.previous=b.root.position;
                if(i>=Mathf.Clamp(maximumActors,1,12))continue;
                Vector3 feet=b.explicitBody!=null?b.explicitBody.Feet:b.controller!=null?new Vector3(b.controller.bounds.center.x,b.controller.bounds.min.y,b.controller.bounds.center.z):b.collider!=null?new Vector3(b.collider.bounds.center.x,b.collider.bounds.min.y,b.collider.bounds.center.z):b.root.position;
                float scale=Mathf.Max(Mathf.Abs(b.root.lossyScale.x),Mathf.Abs(b.root.lossyScale.z));
                float radius=b.explicitBody!=null?b.explicitBody.radius*scale:b.controller!=null?b.controller.radius*scale+footprintPadding:b.agent!=null?b.agent.radius*scale+footprintPadding:b.collider!=null?Mathf.Min(2.5f,Mathf.Max(b.collider.bounds.extents.x,b.collider.bounds.extents.z))+footprintPadding:.8f;
                if(b.root.GetComponent<SoulsLike.Riding.HorseController>()!=null)radius=Mathf.Max(radius,1.35f*scale);
                Vector2 planar=new Vector2(step.x,step.z);float speed=dt>.0001f?planar.magnitude/dt:0;
                // Teleports must not flatten a long strip between unrelated locations.
                if(step.magnitude>8){speed=0;planar=Vector2.zero;}
                Vector2 direction=planar.sqrMagnitude>.00001f?planar.normalized:Vector2.zero;
                float strength=(b.explicitBody!=null?b.explicitBody.strength:1)*Mathf.Lerp(.28f,1,Mathf.Clamp01(speed/6));
                Write(count++,feet,radius,direction,strength,0);
                b.lastTrail+=dt;
                if(speed>.2f && b.lastTrail>=.06f)
                {
                    if(wake.Count>=64)wake.RemoveAt(0);
                    wake.Add(new Wake{feet=feet,radius=radius,direction=direction,strength=strength,age=0});b.lastTrail=0;
                }
            }
            for(int i=wake.Count-1;i>=0 && count<MaxInfluences;i--)
            {
                var w=wake[i];if((w.feet-focus).sqrMagnitude>visibleDistance*visibleDistance)continue;
                float recovery=1-Mathf.Clamp01(w.age/Mathf.Max(.1f,recoverySeconds));recovery=recovery*recovery*(3-2*recovery);
                Write(count++,w.feet,w.radius,w.direction,w.strength*recovery,1);
            }
            ActiveInfluenceCount=count;Shader.SetGlobalVectorArray(PositionsId,positions);Shader.SetGlobalVectorArray(MotionId,motion);
            Shader.SetGlobalFloat(StrengthId,bendStrength);Shader.SetGlobalFloat(HeightId,verticalReach);Shader.SetGlobalInt(CountId,count);
        }
        void Write(int index,Vector3 feet,float radius,Vector2 direction,float strength,float trail)
        {positions[index]=new Vector4(feet.x,feet.y,feet.z,Mathf.Max(.1f,radius));motion[index]=new Vector4(direction.x,direction.y,strength,trail);}
        public void ClearInfluences(){wake.Clear();ActiveInfluenceCount=0;Shader.SetGlobalInt(CountId,0);}
        void OnDisable(){ClearInfluences();bodies.Clear();known.Clear();}
    }
}
