using System;
using UnityEngine;

namespace SoulsLike.Riding
{
    [DisallowMultipleComponent]
    public sealed class PlayerHorseEquipment : MonoBehaviour
    {
        public HorseEquipmentItem[] catalog=Array.Empty<HorseEquipmentItem>();
        public HorseEquipmentItem saddle, horseshoes;
        public event Action EquipmentChanged;
        [Header("Horse level (independent of player level)")]
        [Tooltip("Horse level and growth per level. Speed also shortens mount/dismount time.")]
        public PlayerCombatLevelSettings horseLevel = new PlayerCombatLevelSettings();
        public const int MaxLevel = 100;
        public int Level => Mathf.Clamp(horseLevel?.level ?? 1,1,MaxLevel);
        public bool TryUpgrade()
        {
            if(Level>=MaxLevel)return false;
            if(horseLevel==null)horseLevel=new PlayerCombatLevelSettings();
            horseLevel.level=Level+1;
            EquipmentChanged?.Invoke();
            return true;
        }
        public HorseEquipmentItem Equipped(HorseEquipmentSlot slot)=>slot==HorseEquipmentSlot.Saddle?saddle:horseshoes;
        public bool TryEquip(HorseEquipmentSlot slot,HorseEquipmentItem item)
        {
            if(!Enum.IsDefined(typeof(HorseEquipmentSlot),slot) || (item && (item.slot!=slot || Array.IndexOf(catalog,item)<0)))return false;
            if(slot==HorseEquipmentSlot.Saddle)saddle=item;else horseshoes=item;
            EquipmentChanged?.Invoke();return true;
        }
        public HorseEquipmentStats Capture()=>Evaluate(saddle,horseshoes);
        public HorseEquipmentStats Preview(HorseEquipmentSlot slot,HorseEquipmentItem item)=>
            Evaluate(slot==HorseEquipmentSlot.Saddle?item:saddle,slot==HorseEquipmentSlot.Horseshoes?item:horseshoes);
        private HorseEquipmentStats Evaluate(HorseEquipmentItem saddle,HorseEquipmentItem shoes)
        {
            float speed=0,damage=0,knockback=0;int flat=0;
            void Add(HorseEquipmentItem item,HorseEquipmentSlot slot)
            {
                if(!item || item.slot!=slot)return;
                speed+=WeaponStatModifier.Finite(item.speedPercent);damage+=WeaponStatModifier.Finite(item.specialDamagePercent);
                knockback+=WeaponStatModifier.Finite(item.specialKnockbackPercent);flat+=item.specialFlatDamage;
            }
            Add(saddle,HorseEquipmentSlot.Saddle);Add(shoes,HorseEquipmentSlot.Horseshoes);
            return new HorseEquipmentStats(speed,damage,flat,knockback,horseLevel);
        }
    }
}
