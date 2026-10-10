using UnityEngine;

namespace ElementalGems
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerStateManager), typeof(GemManager)), DefaultExecutionOrder(250)]
    public sealed class GemChargeEffects : MonoBehaviour
    {
        public GemChargeAura auraPrefab;
        public Vector3 localCenter = new Vector3(0, 1.1f, 0);
        PlayerStateManager player;
        GemManager gems;
        GemChargeAura aura;
        public GemChargeAura ActiveAura => aura;
        Color GemColor => gems != null && gems.Equipped != null ? gems.Equipped.color : new Color(.8f,.9f,1);

        void Awake() { player = GetComponent<PlayerStateManager>(); gems = GetComponent<GemManager>(); }
        void OnEnable()
        {
            if (player == null) Awake();
            player.ParrySucceeded += OnParrySucceeded;
        }
        bool EnsureAura()
        {
            if (aura == null && auraPrefab != null)
            {
                aura = Instantiate(auraPrefab, transform, false);
                aura.name = "Gem Charge Aura (Runtime)";
                // Ignore model/import scale: one unit of aura is one world metre.
                var scale = transform.lossyScale;
                aura.transform.localScale = new Vector3(1 / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                    1 / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1 / Mathf.Max(.001f, Mathf.Abs(scale.z)));
            }
            if (aura == null) return false;
            aura.transform.position = transform.position + transform.rotation * localCenter;
            bool unscaled = player.anim != null && player.anim.updateMode == AnimatorUpdateMode.UnscaledTime;
            SetClock(aura.inward, unscaled); SetClock(aura.outward, unscaled);
            SetClock(aura.contractingRings, unscaled); SetClock(aura.releaseRings, unscaled);
            SetClock(aura.milestoneRings, unscaled); SetClock(aura.stageMarkers, unscaled);
            return true;
        }
        static void SetClock(ParticleSystem particles, bool unscaled)
        {
            if (particles == null) return;
            var main = particles.main; main.useUnscaledTime = unscaled;
        }
        void LateUpdate()
        {
            if (!player.isActiveAndEnabled) { if (aura != null) aura.Cancel(); return; }
            if (player.TryGetChargeVisual(out int stage, out float progress))
            {
                if (!EnsureAura()) return;
                if (player.IsBowLightChargingVisual) aura.SetSingleCharge(GemColor, player.BowLightChargeProgress);
                else if (player.IsChargingBowHeavyAttack) aura.SetSingleCharge(GemColor, player.BowHeavyChargeProgress);
                else if (player.TryGetChargeVisualSeconds(out float seconds)) aura.SetTimedCharge(GemColor, seconds);
                else aura.SetCharge(GemColor, stage, progress);
                aura.Tick(player.anim != null && player.anim.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
            }
            else if (aura != null) aura.StopCharge();
        }
        void OnParrySucceeded()
        {
            if (EnsureAura()) aura.Release(GemColor, 2);
        }
        void OnDisable()
        {
            if (player != null) player.ParrySucceeded -= OnParrySucceeded;
            if (aura != null) aura.Cancel();
        }
        void OnDestroy() { if (aura != null) Destroy(aura.gameObject); }
    }
}
