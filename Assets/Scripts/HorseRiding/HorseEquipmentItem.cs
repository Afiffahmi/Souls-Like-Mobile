using UnityEngine;

namespace SoulsLike.Riding
{
    public enum HorseEquipmentSlot { Saddle, Horseshoes }
    [CreateAssetMenu(menuName="Riding/Horse Equipment Item")]
    public sealed class HorseEquipmentItem : ScriptableObject
    {
        public string displayName;
        [TextArea] public string description;
        public HorseEquipmentSlot slot;
        public Sprite icon;
        [Tooltip("Percent change to movement and mounted animation speed.")]
        public float speedPercent;
        public float specialDamagePercent;
        public int specialFlatDamage;
        [Tooltip("Percent change to special-attack knockback strength, before enemy resistance.")]
        public float specialKnockbackPercent;
    }
    public readonly struct HorseEquipmentStats
    {
        public readonly float speed,damage,knockback;
        public readonly int flatDamage;
        public HorseEquipmentStats(float speedPercent,float damagePercent,int flat,float knockbackPercent,
            PlayerCombatLevelSettings level=null)
        {
            var stats=new WeaponAttackStats(flat,
                1+WeaponStatModifier.Finite(damagePercent)/100,
                1+WeaponStatModifier.Finite(speedPercent)/100,
                1+WeaponStatModifier.Finite(knockbackPercent)/100);
            // Share the player's additive level growth and stat bounds.
            if(level!=null)stats=level.Apply(stats);
            speed=stats.AgilityMultiplier;
            damage=stats.DamageMultiplier;
            knockback=stats.KnockbackDurationMultiplier;
            flatDamage=flat;
        }
        public static HorseEquipmentStats Default=>new HorseEquipmentStats(0,0,0,0);
        public int Damage(int basis)=>Mathf.Max(0,Mathf.RoundToInt((basis+flatDamage)*damage));
    }
}
