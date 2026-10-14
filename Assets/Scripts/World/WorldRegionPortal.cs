using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using TMPro;
using SoulsLike.Riding;
namespace Nusantara.World
{
    [DisallowMultipleComponent]
    public sealed class WorldRegionPortal : MonoBehaviour
    {
        public WorldDefinition world;
        [Range(1,9)] public int destinationRegion;
        public Vector3 destination;
        public string label="Travel";
        [Tooltip("Progression can enable this when the Region 1 access is unlocked.")]
        public bool accessUnlocked=true;
        [Min(1)] public float interactionDistance=6;
        public Canvas promptCanvas;
        public UnityEngine.UI.Button promptButton;
        public TMP_Text promptText;
        WorldChunkStreamer streamer;
        float cooldown;
        bool loading;
        static float nextTravelTime;
        static int lastTravelFrame=-1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession(){active.Clear();nextTravelTime=0;lastTravelFrame=-1;}
        public static bool OwnsInteractionKeyFor(Transform player)=>lastTravelFrame==Time.frameCount||HasNearbyAccess(player);
        static readonly HashSet<WorldRegionPortal> active=new HashSet<WorldRegionPortal>();
        void OnEnable()=>active.Add(this);
        public static bool HasNearbyAccess(Transform player)
        {foreach(var portal in active)if(portal!=null&&portal.streamer!=null&&portal.streamer.player==player&&portal.Available)return true;return false;}
        public bool Available=>isActiveAndEnabled&&accessUnlocked&&Time.timeScale>0&&Time.unscaledTime>=cooldown&&Time.unscaledTime>=nextTravelTime&&streamer!=null&&streamer.Running&&streamer.player!=null
            &&Vector3.Distance(streamer.player.position,transform.position)<=interactionDistance;
        void Start()
        {
            foreach(var candidate in FindObjectsByType<WorldChunkStreamer>(FindObjectsSortMode.None))if(candidate.definition==world&&candidate.gameObject.scene==gameObject.scene){streamer=candidate;break;}
            if(promptButton!=null)promptButton.onClick.AddListener(Activate);
        }
        void Update()
        {
            bool available=Available;
            if(promptCanvas!=null)promptCanvas.gameObject.SetActive(available);
            if(promptText!=null)promptText.text=label;
            if(available&&Keyboard.current!=null&&Keyboard.current.eKey.wasPressedThisFrame)Interact();
        }
        public void Activate()=>Interact();
        public bool Interact()
        {
            if(!Available)return false;
            if(world==null||!world.Contains(world.WorldToChunk(destination)))return false;
            var player=streamer.player;
            var riding=player.GetComponent<MountedPlayerController>();
            if(riding!=null&&riding.IsBusy&&!riding.IsRiding)return false;
            if(!streamer.PreloadCollision(destination))return false;
            nextTravelTime=float.PositiveInfinity;lastTravelFrame=Time.frameCount;loading=true;
            if(streamer.IsCollisionReady(destination))FinishTravel();else StartCoroutine(WaitForCollision());
            return true;
        }
        IEnumerator WaitForCollision()
        {
            float timeout=Time.unscaledTime+20;
            while(streamer!=null&&streamer.Running&&!streamer.IsCollisionReady(destination)&&Time.unscaledTime<timeout)yield return null;
            if(streamer!=null&&streamer.Running&&streamer.IsCollisionReady(destination))FinishTravel();else CancelTravel();
        }
        void FinishTravel()
        {
            var player=streamer.player;var riding=player.GetComponent<MountedPlayerController>();
            foreach(var safety in FindObjectsByType<WorldStreamingPlayerGate>(FindObjectsSortMode.None))if(safety.streamer==streamer)safety.PrepareForTeleport();
            if(riding!=null&&riding.IsRiding&&riding.Horse!=null)Move(riding.Horse.transform,destination);
            else
            {Move(player,destination);var motor=player.GetComponent<PlayerStateManager>();if(motor!=null)motor.playerVelocity=Vector3.zero;}
            Physics.SyncTransforms();streamer.RequestReplan();streamer.Tick();streamer.FinishCollisionPreload(destination);
            foreach(var nav in FindObjectsByType<WorldAINavigation>(FindObjectsSortMode.None))if(nav.world==world)nav.RequestRebuild();
            foreach(var camera in FindObjectsByType<IsometricPlayerCamera>(FindObjectsSortMode.None))if(camera.player==player)camera.SnapToPlayer();
            loading=false;nextTravelTime=Time.unscaledTime+.75f;cooldown=Time.unscaledTime+1;lastTravelFrame=Time.frameCount;
        }
        void CancelTravel()
        {if(streamer!=null)streamer.FinishCollisionPreload(destination);loading=false;nextTravelTime=Time.unscaledTime+.1f;}
        static void Move(Transform actor,Vector3 position)
        {
            var controller=actor.GetComponent<CharacterController>();bool enabled=controller!=null&&controller.enabled;if(enabled)controller.enabled=false;
            var body=actor.GetComponent<Rigidbody>();if(body!=null){body.position=position;if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}}
            actor.position=position;if(enabled)controller.enabled=true;
            var agent=actor.GetComponent<NavMeshAgent>();if(agent!=null&&agent.enabled&&agent.isOnNavMesh)agent.Warp(position);
        }
        void OnDisable(){active.Remove(this);if(loading){StopAllCoroutines();CancelTravel();}if(promptCanvas!=null)promptCanvas.gameObject.SetActive(false);}
        void OnDestroy(){if(promptButton!=null)promptButton.onClick.RemoveListener(Activate);}
    }
}
