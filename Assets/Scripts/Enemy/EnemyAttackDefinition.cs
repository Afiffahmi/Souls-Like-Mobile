using UnityEngine;

namespace SoulsLike.Enemies
{
    public enum EnemyAttackType { Melee, Ranged, Magic, Special }
    public enum EnemyAttackTrigger { Normal, HealthLossSpecial }
    public enum EnemyAttackMotion { AnimationOnly, Dash, Jump }
    public enum EnemyAttackDirection { Forward, Backward, TowardTarget, AwayFromTarget }
    [CreateAssetMenu(menuName = "Enemies/Attack Definition")]
    public sealed class EnemyAttackDefinition : ScriptableObject
    {
        public bool available = true;
        public EnemyAttackType attackType;
        [Tooltip("Health Loss Special is chosen only for a queued health threshold or the player's enemy debug controls.")]
        public EnemyAttackTrigger trigger;
        public string stateName = "Attack1";
        public AnimationClip animation;
        [Min(.05f)] public float playbackSpeed = 1;
        [Tooltip("Extra animation speed during Movement Start Frame through Movement End Frame for Jump attacks. Travel and impact remain synchronized to the clip. 1 = normal, 1.5 = faster, 0.5 = slower.")]
        [Min(.05f)] public float jumpSpeedMultiplier = 1f;
        [Tooltip("Animation speed multiplier before Hit Start Frame (or normalized Hit Start). 1 disables slowdown; 0.5 is half speed. Attack and recovery use the normal Playback Speed.")]
        [Range(.05f, 1f)] public float windupSpeedMultiplier = 1f;
        [Min(0)] public float minimumRange;
        [Min(.1f)] public float range = 2.3f;
        [Range(1, 360)] public float arc = 100;
        [Min(0)] public int damage = 15;
        [Min(0)] public float cooldown = 2;
        [Header("Step behavior")]
        [Tooltip("Disable for a reposition/prepare animation that should not deal damage.")]
        public bool dealsDamage = true;
        [Header("Attack warning and protection")]
        public bool showAttackCue;
        [Tooltip("Yellow warning permits a timed parry. Red warning cannot be parried.")]
        public bool canBeParried = true;
        [Tooltip("Zero-based animation frame that shows the warning and starts attack protection. Only damage-dealing attacks use this cue.")]
        [Min(0)] public float attackCueFrame = 3;
        [Tooltip("Ignore damage, hit reactions, knockback and elemental changes from Cue Frame until this attack animation ends. A successful permitted parry still interrupts.")]
        public bool invulnerableAfterCue;
        [Min(.1f)] public float cueDuration = .65f;
        [Min(.1f)] public float cueRadius = .75f;
        public Material cueMaterial;
        [Tooltip("Only enable for attacks that should apply an element, trigger link-up reactions and change the player's equipped gem. Normal attacks use plain health damage.")]
        public bool applyElementToPlayer;
        public bool HasDamagePayload => dealsDamage || (animationAction != null && animationAction.DealsDamage);
        public EnemyAttackMotion movement;
        public EnemyAttackDirection movementDirection = EnemyAttackDirection.TowardTarget;
        [Min(0)] public float movementDistance = 3f;
        [Min(0)] public float movementStartFrame;
        [Min(1)] public float movementEndFrame = 14;
        [Tooltip("Stop this far from the target when moving toward it. Direction is captured at step start. Jump uses the clip's own airborne pose; movement stays on the NavMesh.")]
        [Min(0)] public float targetStoppingDistance = 1f;
        [Header("Animation action (optional)")]
        [Tooltip("An action such as Summon Army, independent of movement and melee damage. Works on opening attacks and follow-ups.")]
        public EnemyAnimationAction animationAction;
        [Tooltip("Zero-based clip frame that executes the action once. Interrupted casts cannot execute afterward.")]
        [Min(0)] public float actionFrame = 10f;
        [Header("Follow-up sequence")]
        [Tooltip("Optional ordered steps after this animation finishes. No four-step limit. Each step is an Attack Definition with its own clip, damage and movement. Follow-ups cannot contain nested follow-ups.")]
        public EnemyAttackDefinition[] followUpSteps = new EnemyAttackDefinition[0];
        [Header("Damage timing")]
        [Tooltip("Use exact clip frames instead of normalized Hit Start/End. A completely skipped window cannot hit.")]
        public bool useFrameWindow;
        [Tooltip("Allow parries during the active window, then resolve damage once at its end. Requires contact during the window and range/line of sight at resolution.")]
        public bool damageAtWindowEnd;
        [Tooltip("Zero-based frame shown on Unity's clip timeline, using this clip's frame rate.")]
        [Min(0)] public int hitStartFrame;
        [Min(0)] public int hitEndFrame;
        [Range(0, 1)] public float hitStart = .4f;
        [Range(0, 1)] public float hitEnd = .55f;
        [Range(0, 1)] public float turnUntil = .25f;
        [Min(0)] public float weight = 1;
        public int priority;
        [Range(0, 1)] public float minimumHealthRatio;
        [Range(0, 1)] public float maximumHealthRatio = 1;
        [Tooltip("Stateless reusable effect; runtime state belongs to the enemy/projectile.")]
        public EnemyAttackEffect effect;
        [Header("Reusable slash visual")]
        [Tooltip("Optional shared sword slash prefab. Timing always uses this attack's damage window.")]
        public ElementalGems.GemCrescentSlash slashPrefab;
        [Tooltip("Placement, direction and size; start/end frames are read from Damage Timing above.")]
        public ElementalGems.GemSlashSettings slash = new ElementalGems.GemSlashSettings();
        private bool StepConfigured => available && animation != null && !animation.isLooping &&
            (!dealsDamage || effect != null) && !string.IsNullOrWhiteSpace(stateName) && playbackSpeed > 0 &&
            (animationAction == null || (animationAction.IsConfigured && actionFrame >= 0 &&
                actionFrame <= animation.length * animation.frameRate + .01f)) &&
            (movement == EnemyAttackMotion.AnimationOnly || (movementStartFrame >= 0 && movementEndFrame > movementStartFrame &&
                movementEndFrame <= animation.length * animation.frameRate + .01f));
        public bool IsUsable
        {
            get
            {
                if (!StepConfigured || weight <= 0) return false;
                if (followUpSteps != null) foreach (var step in followUpSteps)
                    if (step == null || step == this || (step.available && (!step.StepConfigured || step.weight <= 0 ||
                        (step.followUpSteps != null && step.followUpSteps.Length > 0)))) return false;
                return true;
            }
        }
        public float WindupMultiplierAt(float normalizedTime)
        {
            float start = useFrameWindow && animation != null && animation.length > 0 && animation.frameRate > 0
                ? hitStartFrame / (animation.length * animation.frameRate) : hitStart;
            if (!dealsDamage && animationAction != null && animation != null && animation.length * animation.frameRate > 0)
                start = actionFrame / (animation.length * animation.frameRate);
            return normalizedTime < start ? Mathf.Clamp(windupSpeedMultiplier, .05f, 1f) : 1f;
        }
        public float PlaybackMultiplierAt(float normalizedTime)
        {
            float frame = animation != null ? normalizedTime * animation.length * animation.frameRate : 0;
            float jump = movement == EnemyAttackMotion.Jump && frame >= movementStartFrame && frame < movementEndFrame
                ? Mathf.Max(.05f, jumpSpeedMultiplier) : 1f;
            return Mathf.Max(.05f, playbackSpeed) * WindupMultiplierAt(normalizedTime) * jump;
        }
        public bool IsInsideFrameWindow(float normalizedTime)
        {
            if (animation == null || animation.length <= 0 || animation.frameRate <= 0) return false;
            float frame = normalizedTime * animation.length * animation.frameRate;
            return frame >= hitStartFrame && frame < hitEndFrame;
        }
        private void OnValidate()
        {
            range = Mathf.Max(.1f, range); minimumRange = Mathf.Clamp(minimumRange, 0, range);
            hitEnd = Mathf.Max(hitStart, hitEnd); maximumHealthRatio = Mathf.Max(minimumHealthRatio, maximumHealthRatio);
            playbackSpeed = Mathf.Max(.05f, playbackSpeed);
            windupSpeedMultiplier = Mathf.Clamp(windupSpeedMultiplier, .05f, 1f);
            movementDistance = Mathf.Max(0, movementDistance);
            movementEndFrame = Mathf.Max(movementStartFrame + 1, movementEndFrame);
            hitStartFrame = Mathf.Max(0, hitStartFrame); hitEndFrame = Mathf.Max(hitStartFrame, hitEndFrame);
        }
    }
}
