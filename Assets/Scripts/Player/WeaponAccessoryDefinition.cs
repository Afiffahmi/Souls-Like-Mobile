using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Weapons/Accessory")]
public sealed class WeaponAccessoryDefinition : ScriptableObject
{
    public PlayerCombatMode weapon = PlayerCombatMode.Sword;
    public string displayName;
    public Sprite icon;
    public WeaponAccessorySlot slot = WeaponAccessorySlot.Sheath;
    [TextArea] public string description;
    public WeaponStatModifier[] modifiers = { new WeaponStatModifier() };
}
