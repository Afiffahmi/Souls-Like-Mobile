using System.Collections;
using System.Linq;
using ElementalGems;
using TMPro;
using UnityEngine;

namespace SoulsLike.Riding
{
    [DisallowMultipleComponent, RequireComponent(typeof(MountedPlayerController))]
    public sealed class HorseSummoner : MonoBehaviour
    {
        public MountedPlayerController rider;
        public GemManager gems;
        public HorseController horsePrefab;
        [Tooltip("Optional scene horse used as an inactive spawn template at runtime, preserving its saddle and Inspector adjustments.")]
        public HorseController sceneHorseTemplate;
        public UnityEngine.UI.Button summonButton;
        public TMP_Text buttonLabel;
        [Header("Summoning")]
        [Min(1)] public float spawnDistance=3.5f;
        [Min(.1f)] public float transitionSeconds=1.65f;
        public LayerMask groundAndObstacles=~0;
        public Material summonMaterial;
        public HorseController ActiveHorse {get;private set;}
        public bool IsTransitioning {get;private set;}
        private GameObject staging;
        private string feedback;
        private float feedbackUntil;
        private Coroutine transition;
        private ShieldDustTransfer dustTransfer;

        private void Awake()
        {
            if(!rider)rider=GetComponent<MountedPlayerController>();
            if(!gems)gems=GetComponent<GemManager>();
            if(summonButton)summonButton.onClick.AddListener(ToggleHorse);
        }
        private void Start()
        {
            // Only the runtime instance is hidden. The authored scene horse remains
            // available in Edit Mode, and every summon copies its latest settings.
            if(sceneHorseTemplate)sceneHorseTemplate.gameObject.SetActive(false);
            RefreshButton();
        }
        private void Update()=>RefreshButton();
        private void RefreshButton()
        {
            if(summonButton)summonButton.interactable=!IsTransitioning && Time.timeScale>0 && rider &&
                (ActiveHorse ? !ActiveHorse.Rider && !rider.IsBusy : rider.CanMount);
            if(!buttonLabel)return;
            string label=Time.unscaledTime<feedbackUntil ? feedback : IsTransitioning ? (ActiveHorse ? "Summoning..." : "Dismissing...") :
                ActiveHorse ? (ActiveHorse.Rider ? "Dismount First" : "Dismiss Horse") : "Summon Horse";
            if(buttonLabel.text!=label)buttonLabel.text=label;
        }
        public void ToggleHorse()
        {
            if(IsTransitioning || Time.timeScale<=0 || !rider)return;
            if(ActiveHorse)
            {
                if(ActiveHorse.Rider || rider.IsBusy)return;
                transition=StartCoroutine(Dismiss());
            }
            else
            {
                if(!rider.CanMount)return;
                var source=sceneHorseTemplate ? sceneHorseTemplate : horsePrefab;
                if(!source)return;
                if(!TrySpawnPoint(source,out var position,out var rotation))
                {feedback="No Clear Ground";feedbackUntil=Time.unscaledTime+2;return;}
                // Copy the equipped identity at the button press, before the reveal.
                var gem=gems ? gems.Equipped : null;
                transition=StartCoroutine(Summon(source,position,rotation,gem));
            }
        }
        private IEnumerator Summon(HorseController source,Vector3 position,Quaternion rotation,GemDefinition gem)
        {
            IsTransitioning=true;
            if(!staging)
            {
                staging=new GameObject("Horse Summon Staging"); staging.SetActive(false);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(staging,gameObject.scene);
            }
            var horse=Instantiate(source,position,rotation,staging.transform);
            horse.gameObject.SetActive(false); horse.name="Summoned Horse";
            horse.equipment=GetComponent<PlayerHorseEquipment>();
            var aura=horse.GetComponent<HorseTailAura>();
            if(aura){aura.CaptureGem(gem);aura.Visibility=0;}
            var interaction=horse.GetComponent<HorseInteractionSystem>();
            if(interaction){interaction.player=rider;interaction.enabled=false;if(interaction.promptCanvas)interaction.promptCanvas.gameObject.SetActive(false);}
            var motor=horse.GetComponent<CharacterController>();
            horse.enabled=false; if(motor)motor.enabled=false;
            horse.transform.SetParent(null,true);
            horse.gameObject.SetActive(true); ActiveHorse=horse;
            var shield=ShieldDustTransfer.FindShield(transform,position);
            dustTransfer=ShieldDustTransfer.Begin(horse.transform,shield,gem ? gem.color : new Color(.7f,.78f,.88f),true,transitionSeconds);
            if(!dustTransfer)
            {
                Destroy(horse.gameObject);ActiveHorse=null;feedback="Shield Path Blocked";feedbackUntil=Time.unscaledTime+2;
            }
            else
            {
                while(dustTransfer && !dustTransfer.Finished && horse)yield return null;
                dustTransfer=null;
                if(horse)
                {
                    if(aura)aura.Visibility=1;
                    if(motor)motor.enabled=true;horse.enabled=true;if(interaction)interaction.enabled=true;
                }
            }
            IsTransitioning=false;transition=null;RefreshButton();
        }
        private IEnumerator Dismiss()
        {
            IsTransitioning=true;
            var horse=ActiveHorse; dismissingHorse=horse; ActiveHorse=null;
            var aura=horse.GetComponent<HorseTailAura>();
            var interaction=horse.GetComponent<HorseInteractionSystem>(); if(interaction)interaction.enabled=false;
            horse.enabled=false;
            var colliders=horse.GetComponentsInChildren<Collider>();
            var colliderStates=colliders.Select(c=>c.enabled).ToArray();
            foreach(var collider in colliders)collider.enabled=false;
            if(aura)aura.Visibility=0;
            var shield=ShieldDustTransfer.FindShield(transform,horse.transform.position);
            dustTransfer=ShieldDustTransfer.Begin(horse.transform,shield,gems && gems.Equipped ? gems.Equipped.color : aura ? aura.ElementColor : Color.white,false,transitionSeconds);
            if(!dustTransfer)
            {
                ActiveHorse=horse;horse.enabled=true;if(aura)aura.Visibility=1;
                for(int i=0;i<colliders.Length;i++)if(colliders[i])colliders[i].enabled=colliderStates[i];
                if(interaction)interaction.enabled=true;
                feedback="Shield Path Blocked";feedbackUntil=Time.unscaledTime+2;
            }
            else
            {
                while(dustTransfer && !dustTransfer.Finished && horse)yield return null;
                dustTransfer=null;
                if(horse)Destroy(horse.gameObject);
            }
            dismissingHorse=null;
            IsTransitioning=false;transition=null;RefreshButton();
        }
        private bool TrySpawnPoint(HorseController source,out Vector3 position,out Quaternion rotation)
        {
            position=default;
            Vector3 forward=Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
            rotation=Quaternion.LookRotation(forward,Vector3.up);
            var motor=source.GetComponent<CharacterController>();
            var scale=source.transform.localScale;
            float radius=motor ? motor.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z)) : .8f;
            float height=motor ? Mathf.Max(radius*2,motor.height*Mathf.Abs(scale.y)) : 2f;
            Vector3 center=motor ? rotation*Vector3.Scale(motor.center,scale) : Vector3.up;
            float bottom=center.y-height*.5f;
            for(int ring=0;ring<2;ring++)
                for(int i=0;i<8;i++)
                {
                    Vector3 direction=Quaternion.AngleAxis(45+i*45,Vector3.up)*forward;
                    Vector3 candidate=transform.position+direction*(Mathf.Max(1,spawnDistance)+ring*1.25f);
                    var hits=Physics.RaycastAll(candidate+Vector3.up*5,Vector3.down,12,groundAndObstacles,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance);
                    foreach(var hit in hits)
                    {
                        if(hit.transform.IsChildOf(transform) || Vector3.Angle(hit.normal,Vector3.up)>(motor ? motor.slopeLimit : 45))continue;
                        Vector3 root=hit.point+Vector3.up*(.08f-bottom);
                        Vector3 mid=root+center;
                        float half=Mathf.Max(0,height*.5f-radius);
                        if(Physics.CheckCapsule(mid+Vector3.up*half,mid-Vector3.up*half,radius+.03f,groundAndObstacles,QueryTriggerInteraction.Ignore))continue;
                        // Keep summons on the player's side of a wall.
                        if(Physics.Linecast(transform.position+Vector3.up,root+Vector3.up,groundAndObstacles,QueryTriggerInteraction.Ignore))continue;
                        position=root;return true;
                    }
                }
            return false;
        }
        private void OnDisable()
        {
            if(transition!=null)StopCoroutine(transition);
            transition=null;IsTransitioning=false;
            if(ActiveHorse)
            {
                if(ActiveHorse.Rider)ActiveHorse.Rider.AbortRide();
                Destroy(ActiveHorse.gameObject);ActiveHorse=null;
            }
            // A dismissed horse can still be in its transition coroutine.
            if(dismissingHorse)Destroy(dismissingHorse.gameObject);
            dismissingHorse=null;
            if(dustTransfer)dustTransfer.Cancel();dustTransfer=null;
        }
        private HorseController dismissingHorse;
        private void OnDestroy()
        {
            if(summonButton)summonButton.onClick.RemoveListener(ToggleHorse);
            if(staging)Destroy(staging);
        }
    }
}


