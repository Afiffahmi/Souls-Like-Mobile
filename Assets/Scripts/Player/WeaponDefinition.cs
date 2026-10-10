using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Weapons/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    public PlayerCombatMode weapon = PlayerCombatMode.Sword;
    public string displayName;
    public Sprite artwork;
    [Range(0, 100)] public int maxUpgradeLevel = 10;
    [Tooltip("Additive percentage per upgrade level, relative to original damage.")]
    public float damagePercentPerLevel = 10;
    public float agilityPercentPerLevel = 2;
    [Tooltip("Additive knockback-duration percentage per upgrade level.")]
    public float knockbackDurationPercentPerLevel = 5;
    public WeaponStatModifier[] modifiers = new WeaponStatModifier[0];
    [Tooltip("Three or four distinct slots. Sword: Blade/Grip/Sheath/Charm; Bow: Limbs/String/Quiver/Charm; Magic: Focus/Core/Conduit/Charm.")]
    public WeaponAccessorySlot[] slots = { WeaponAccessorySlot.Blade, WeaponAccessorySlot.Grip, WeaponAccessorySlot.Sheath, WeaponAccessorySlot.Charm };

    public bool HasSlot(WeaponAccessorySlot slot) => slots != null && System.Array.IndexOf(slots, slot) >= 0;
    public bool IsValid
    {
        get
        {
            if (weapon == PlayerCombatMode.Normal || !System.Enum.IsDefined(typeof(PlayerCombatMode), weapon) ||
                slots == null || slots.Length < 3 || slots.Length > 4) return false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!System.Enum.IsDefined(typeof(WeaponAccessorySlot), slots[i])) return false;
                for (int j = 0; j < i; j++) if (slots[i] == slots[j]) return false;
            }
            return true;
        }
    }
}
