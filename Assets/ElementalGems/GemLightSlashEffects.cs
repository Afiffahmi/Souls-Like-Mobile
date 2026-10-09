using UnityEngine;

namespace ElementalGems
{
    [DisallowMultipleComponent, RequireComponent(typeof(GemWeaponEffects)), DefaultExecutionOrder(190)]
    public sealed class GemLightSlashEffects : MonoBehaviour
    {
        // Retain old serialized placement fields for existing scenes/build tools.
        [HideInInspector] public float height = 1;
        [HideInInspector] public float forwardOffset = .05f;
        [HideInInspector] public float swingTilt = 9;
        [Tooltip("One prefab per element. A neutral copy is used for a plain sword. Timing and direction come from Combat Attack Configuration > Slash.")]
        public GemCrescentSlash[] elementalSlashes;
        GemManager manager;
        PlayerStateManager player;
        PlayerSwordVisuals sword;
        GemSwordCombat melee;
        GemSlashSettings settings;
        AnimationClip clip;
        GemCrescentSlash current;
        int stateHash;
        float frame;
        bool tracking, consumed;
        Vector3 origin;
        Quaternion facing;

        public bool HasConfiguredSlash => tracking && settings != null && settings.enabled;
        public bool TrailActive => HasConfiguredSlash && settings.IsValid(clip) && settings.ContainsFrame(frame) && CanShow;
        public GemCrescentSlash ActiveSlash => current;
        bool CanShow => manager != null && player != null && player.IsAttacking &&
            player.CombatMode == PlayerCombatMode.Sword && sword != null && sword.IsSwordDrawn;

        void Awake()
        {
            manager = GetComponent<GemManager>();
            player = GetComponent<PlayerStateManager>();
            sword = GetComponent<PlayerSwordVisuals>();
            melee = GetComponent<GemSwordCombat>();
        }
        void OnEnable() { if (manager != null) manager.GemChanged += GemChanged; }
        void OnDisable() { if (manager != null) manager.GemChanged -= GemChanged; tracking = false; Clear(); }
        void GemChanged(GemDefinition _) { Clear(); consumed = true; }
        void Clear()
        {
            if (current == null) return;
            current.gameObject.SetActive(false);
            Destroy(current.gameObject);
            current = null;
        }
        public void Begin(int hash, AnimationClip animation, GemSlashSettings slash)
        {
            Clear(); stateHash = hash; clip = animation; settings = slash;
            // Previously unconfigured heavy/special attacks use their existing damage window,
            // but now show the same size crescent and derive contacts from it as well.
            if (clip != null && (settings == null || !settings.enabled))
            {
                float frames = clip.length * clip.frameRate;
                settings = new GemSlashSettings {
                    enabled = true,
                    startFrame = frames * (melee != null ? melee.hitStart : .2f),
                    endFrame = frames * (melee != null ? melee.hitEnd : .72f)
                };
            }
            frame = 0; consumed = false; tracking = true;
        }
        public void Tick(int hash, float normalizedTime)
        {
            if (tracking && hash == stateHash && clip != null)
                frame = normalizedTime * clip.length * clip.frameRate;
        }
        public void End(int hash)
        {
            if (hash != stateHash) return;
            tracking = false; Clear();
        }
        public void FinishWindow(int hash, float normalizedTime)
        {
            if (!tracking || hash != stateHash || current == null || !CanShow || !settings.IsValid(clip)) return;
            // Called before the Animator releases the attack lock. Interrupted swings don't finish.
            if (settings.FrameAt(clip, normalizedTime) >= settings.endFrame) UpdateSlash(1);
        }
        void LateUpdate()
        {
            if (!HasConfiguredSlash || !settings.IsValid(clip)) { Clear(); return; }
            if (!CanShow) { Clear(); return; }
            // No wall-clock timer: pauses, speed changes and low FPS use the current animation frame.
            // A completely skipped window is expired, never replayed late.
            if (frame >= settings.endFrame)
            {
                // Complete a slash that was actually shown, including the last low-FPS slice.
                // A window skipped entirely still cannot create an invisible hit.
                if (current != null) UpdateSlash(1);
                consumed = true; Clear(); return;
            }
            if (!settings.ContainsFrame(frame)) { Clear(); return; }
            if (current == null && !consumed)
            {
                consumed = true;
                var prefab = elementalSlashes == null ? null : System.Array.Find(elementalSlashes,
                    s => s != null && s.element == manager.EquippedElement);
                if (prefab == null && manager.EquippedElement == ElementType.Normal && elementalSlashes != null)
                    prefab = System.Array.Find(elementalSlashes, s => s != null);
                if (prefab == null) return;
                CapturePose();
                current = Instantiate(prefab, origin, facing * Quaternion.Euler(settings.localEulerAngles));
                current.name = manager.EquippedElement + " Configured Attack Slash";
                if (manager.EquippedElement == ElementType.Normal) current.MakeNeutral();
                current.InitializeDriven(settings);
            }
            if (current == null) return;
            UpdateSlash(settings.ProgressAt(frame));
        }
        void UpdateSlash(float progress)
        {
            if (settings.followPlayer) CapturePose();
            current.SetAnimationFrame(progress, origin, facing);
            // Sample immediately after positioning the VFX, never from the sword transform.
            if (melee != null) melee.SampleSlash(current);
        }
        void CapturePose()
        {
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            facing = Quaternion.LookRotation(forward.sqrMagnitude > .0001f ? forward : Vector3.forward, Vector3.up);
            origin = transform.position + facing * settings.localOffset;
        }
    }
}
