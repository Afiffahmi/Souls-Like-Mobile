using System.Collections.Generic;
using UnityEngine;

namespace ElementalGems
{
    [DisallowMultipleComponent, RequireComponent(typeof(GemWeaponEffects)), DefaultExecutionOrder(210)]
    public sealed class GemLightSlashEffects : MonoBehaviour
    {
        [Header("Light-attack crescent placement (world metres)")]
        [Min(0)] public float height = 1.0f;
        public float forwardOffset = .05f;
        [Range(-30, 30)] public float swingTilt = 9;
        [Tooltip("One prefab per element. Normal deliberately has no crescent.")]
        public GemCrescentSlash[] elementalSlashes;
        GemManager manager;
        GemSwordCombat melee;
        PlayerStateManager player;
        PlayerSwordVisuals sword;
        bool previousWindow;
        int previousStep;
        readonly List<GemCrescentSlash> live = new List<GemCrescentSlash>(4);

        void Awake()
        {
            manager = GetComponent<GemManager>();
            melee = GetComponent<GemSwordCombat>();
            player = GetComponent<PlayerStateManager>();
            sword = GetComponent<PlayerSwordVisuals>();
        }
        void OnEnable() { manager.GemChanged += GemChanged; }
        void OnDisable() { manager.GemChanged -= GemChanged; Clear(); }
        void GemChanged(GemDefinition _) { Clear(); }
        void Clear()
        {
            foreach (var fx in live) if (fx != null) { fx.gameObject.SetActive(false); Destroy(fx.gameObject); }
            live.Clear(); previousWindow = false; previousStep = 0;
        }
        void LateUpdate()
        {
            bool active = player != null && player.CombatMode == PlayerCombatMode.Sword &&
                sword != null && sword.IsSwordDrawn && manager.EquippedElement != ElementType.Normal;
            if (!active) { if (live.Count > 0 || previousWindow) Clear(); return; }
            live.RemoveAll(fx => fx == null);
            bool window = melee != null && melee.TrailActive && player.IsAttacking &&
                player.CurrentAttackInput == CombatAttackInput.LightAttack;
            int step = player.CurrentAttackNumber;
            if (window && (!previousWindow || step != previousStep))
            {
                var prefab = elementalSlashes == null ? null : System.Array.Find(elementalSlashes,
                    s => s != null && s.element == manager.EquippedElement);
                if (prefab != null)
                {
                    int direction = step % 2 == 0 ? -1 : 1;
                    var position = transform.position + Vector3.up * height + transform.forward * forwardOffset;
                    var orientation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up), Vector3.up) * Quaternion.Euler(0, 0, swingTilt * direction);
                    var fx = Instantiate(prefab, position, orientation);
                    fx.name = manager.EquippedElement + " Light Attack Crescent";
                    fx.Initialize(direction);
                    live.Add(fx);
                }
            }
            previousWindow = window;
            previousStep = step;
        }
    }
}
