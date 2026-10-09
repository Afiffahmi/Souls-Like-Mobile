using System.Collections.Generic;
using ElementalGems;
using UnityEngine;

namespace SoulsLike.Enemies
{
    /// <summary>Reuses the player's crescent renderer; EnemyBrain remains the damage/parry owner.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyBrain), typeof(EnemyAnimationDriver))]
    public sealed class EnemySlashVisual : MonoBehaviour
    {
        readonly Dictionary<GemCrescentSlash, GemCrescentSlash> cached = new Dictionary<GemCrescentSlash, GemCrescentSlash>();
        EnemyBrain brain;
        EnemyAnimationDriver driver;
        EnemyAttackDefinition attack;
        GemSlashSettings settings;
        GemCrescentSlash current;
        Vector3 origin;
        Quaternion facing;
        bool consumed;
        public GemCrescentSlash ActiveSlash => current;
        void Awake() { brain=GetComponent<EnemyBrain>(); driver=GetComponent<EnemyAnimationDriver>(); }
        void OnEnable() { brain.AttackStarted+=Begin; brain.StateChanged+=StateChanged; }
        void OnDisable()
        {
            brain.AttackStarted-=Begin; brain.StateChanged-=StateChanged;
            Clear(); attack=null;
        }
        void OnDestroy()
        {
            foreach(var slash in cached.Values) if(slash!=null) Destroy(slash.gameObject);
            cached.Clear();
        }
        void StateChanged(EnemyState state)
        {
            if(state==EnemyState.Attacking) return;
            Clear(); attack=null;
        }
        void Begin(EnemyAttackDefinition definition)
        {
            Clear(); attack=definition; consumed=false;
            if(attack.slashPrefab==null || attack.slash==null || !attack.slash.enabled) return;
            float frames=attack.animation.length*attack.animation.frameRate;
            // Runtime copy keeps shared attack assets immutable and timing authoritative.
            var source=attack.slash;
            settings=new GemSlashSettings {
                enabled=true,
                startFrame=attack.useFrameWindow?attack.hitStartFrame:attack.hitStart*frames,
                endFrame=attack.useFrameWindow?attack.hitEndFrame:attack.hitEnd*frames,
                localOffset=source.localOffset, localEulerAngles=source.localEulerAngles,
                reverseSweep=source.reverseSweep, startSweepAngle=source.startSweepAngle,
                endSweepAngle=source.endSweepAngle, progression=source.progression,
                size=source.size, forwardDrift=source.forwardDrift, followPlayer=source.followPlayer
            };
        }
        void LateUpdate()
        {
            if(!brain.enabled || brain.State!=EnemyState.Attacking || brain.Health.IsDead ||
                attack==null || attack!=brain.CurrentAttack || attack.slashPrefab==null ||
                attack.slash==null || !attack.slash.enabled || settings==null || !settings.IsValid(attack.animation) ||
                !driver.TryGetTime(attack.stateName,out float time)) { Clear(); return; }
            float frame=settings.FrameAt(attack.animation,time);
            // An entirely skipped window never spawns a late visual or damage.
            if(frame>=settings.endFrame) { consumed=true; Clear(); return; }
            if(!settings.ContainsFrame(frame)) { Clear(); return; }
            if(current==null && !consumed)
            {
                consumed=true;
                if(!cached.TryGetValue(attack.slashPrefab,out current) || current==null)
                {
                    current=Instantiate(attack.slashPrefab);
                    current.name=name+" Sword Slash";
                    cached[attack.slashPrefab]=current;
                }
                CapturePose(); current.gameObject.SetActive(true); current.InitializeDriven(settings);
            }
            if(current==null) return;
            if(settings.followPlayer) CapturePose();
            current.SetAnimationFrame(settings.ProgressAt(frame),origin,facing);
        }
        void CapturePose()
        {
            Vector3 forward=Vector3.ProjectOnPlane(transform.forward,Vector3.up);
            facing=Quaternion.LookRotation(forward.sqrMagnitude>.0001f?forward:Vector3.forward,Vector3.up);
            origin=transform.position+facing*settings.localOffset;
        }
        void Clear()
        {
            if(current!=null) current.gameObject.SetActive(false);
            current=null;
        }
    }
}
