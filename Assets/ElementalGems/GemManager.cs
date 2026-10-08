using System;
using UnityEngine;

namespace ElementalGems
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class GemManager : MonoBehaviour
    {
        public GemDefinition[] gems = Array.Empty<GemDefinition>();
        [SerializeField] private ElementType equippedElement = ElementType.Normal;
        [SerializeField] private string saveKey = "ElementalGems.Equipped.v1";
        public bool persistSelection = true;
        public event Action<GemDefinition> GemChanged;
        public GemDefinition Equipped { get; private set; }
        public ElementType EquippedElement => Equipped != null ? Equipped.element : ElementType.Normal;
        public float MovementScale => EquippedElement == ElementType.Normal ? 1 : Mathf.Max(0.1f, Equipped.movementScale);
        private int combo;
        private double lastHit = double.NegativeInfinity;
        private void Awake()
        {
            var wanted = persistSelection ? (ElementType)PlayerPrefs.GetInt(saveKey, (int)equippedElement) : equippedElement;
            if (!Equip(wanted, false)) Equip(ElementType.Normal, false);
        }
        public bool Equip(ElementType type, bool save = true)
        {
            var next = Array.Find(gems, g => g != null && g.element == type);
            if (next == null) return false;
            Equipped = next;
            equippedElement = type;
            combo = 0;
            lastHit = double.NegativeInfinity;
            if (save && persistSelection) { PlayerPrefs.SetInt(saveKey, (int)type); PlayerPrefs.Save(); }
            GemChanged?.Invoke(next);
            return true;
        }
        public GemAttack Capture()
        {
            if (Equipped == null) return new GemAttack(null);
            if (Time.timeAsDouble - lastHit > Equipped.comboTimeout) combo = 0;
            return new GemAttack(Equipped, 1 + combo * Equipped.comboDamagePerHit);
        }
        public void RegisterHit(GemAttack attack, int actualDamage)
        {
            if (actualDamage <= 0) return;
            if (attack.element == EquippedElement && Equipped != null)
            {
                if (Time.timeAsDouble - lastHit > Equipped.comboTimeout) combo = 0;
                combo = Mathf.Min(combo + 1, Equipped.maxComboStacks);
                lastHit = Time.timeAsDouble;
            }
            var health = GetComponent<PlayerOverall>();
            if (health != null) health.Heal(attack.healing + Mathf.RoundToInt(actualDamage * attack.lifeSteal));
        }
        public int ReduceIncomingDamage(int damage) => Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, damage) *
            (1 - (EquippedElement == ElementType.Normal ? 0 : Mathf.Clamp(Equipped.armorReduction, 0, 0.9f)))));
    }
}
