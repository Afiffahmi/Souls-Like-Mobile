public partial class PlayerStateManager
{
    private PlayerWeaponEquipment weaponEquipment;
    private WeaponAttackStats activeWeaponStats = WeaponAttackStats.Default;
    private WeaponAttackStats bowWeaponStats = WeaponAttackStats.Default;
    private WeaponAttackStats bowHeavyWeaponStats = WeaponAttackStats.Default;
    public WeaponAttackStats CurrentWeaponAttackStats => bowHeavyActive ? bowHeavyWeaponStats : bowAttackActive ? bowWeaponStats : activeWeaponStats;

    /// <summary>Capture this value when a future spell/skill starts; use Damage and Duration on its base values.</summary>
    public WeaponAttackStats CaptureWeaponStats(PlayerCombatMode weapon, CombatAttackInput input, int attackNumber = 1)
    {
        if (weaponEquipment == null) weaponEquipment = GetComponent<PlayerWeaponEquipment>();
        return weaponEquipment != null && weaponEquipment.isActiveAndEnabled
            ? weaponEquipment.Capture(weapon, input, attackNumber) : WeaponAttackStats.Default;
    }
}
