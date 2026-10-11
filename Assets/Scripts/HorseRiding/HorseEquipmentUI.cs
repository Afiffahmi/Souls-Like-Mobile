using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace SoulsLike.Riding
{
    public sealed class HorseEquipmentUI : MonoBehaviour
    {
        public WeaponEquipmentUI ui;
        public PlayerHorseEquipment equipment;
        public HorseSummoner summoner;
        public UnityEngine.UI.Button horseTab;
        public Sprite horseArtwork;
        public const string Help="Upgrade your horse independently. Speed affects movement, animations and mount/dismount time. Special stats apply to the next dash.";
        private int displayedLevel=-1;
        private HorseEquipmentSlot slot;
        private void Update()
        {
            if(ui && ui.HorseSelected && equipment && displayedLevel!=equipment.Level)Refresh();
        }
        private HorseEquipmentItem selected;
        private HorseEquipmentItem[] visible=Array.Empty<HorseEquipmentItem>();
        static readonly Color Gold=new Color(.86f,.70f,.43f),Dark=new Color(.065f,.082f,.094f),Selected=new Color(.16f,.19f,.19f);
        private void Awake(){if(horseTab)horseTab.onClick.AddListener(OpenTab);}
        private void OnEnable(){if(equipment)equipment.EquipmentChanged+=OnChanged;}
        private void OnDisable(){if(equipment)equipment.EquipmentChanged-=OnChanged;}
        private void OnDestroy(){if(horseTab)horseTab.onClick.RemoveListener(OpenTab);}
        private void OpenTab(){if(ui)ui.SelectHorse();}
        private void OnChanged(){if(ui && ui.HorseSelected)Refresh();}
        public void Leave()
        {
            if(horseTab){horseTab.GetComponent<UnityEngine.UI.Image>().color=Dark;horseTab.GetComponentInChildren<TMP_Text>().color=Color.white;}
        }
        public void SelectSlot(int index)
        {
            if(index<0 || index>1 || !equipment)return;
            slot=(HorseEquipmentSlot)index;
            visible=(equipment.catalog??Array.Empty<HorseEquipmentItem>()).Where(i=>i && i.slot==slot).Distinct().ToArray();
            selected=equipment.Equipped(slot);
            if(!selected || !visible.Contains(selected))selected=visible.FirstOrDefault();
            if(ui.accessoryScroll)ui.accessoryScroll.verticalNormalizedPosition=1;
            Refresh();ui.feedback.text=Help;
        }
        public void SelectItem(int index){if(index>=0 && index<visible.Length){selected=visible[index];Refresh();}}
        public void Equip()
        {
            if(selected && equipment.TryEquip(slot,selected)){Refresh();ui.feedback.text=Name(selected)+" equipped. Movement updates now; special stats update on the next dash.";}
        }
        public void Remove(){if(equipment.TryEquip(slot,null)){Refresh();ui.feedback.text=SlotName(slot)+" removed. Item stays in the collection.";}}
        public void Upgrade()
        {
            if(!equipment)return;
            bool upgraded=equipment.TryUpgrade();
            Refresh();
            ui.feedback.text=upgraded ? $"Horse upgraded to level {equipment.Level}. Speed also reduces mount and dismount time." : "Horse is at maximum level.";
        }
        private HorseController Basis=>summoner ? (summoner.ActiveHorse ? summoner.ActiveHorse : summoner.sceneHorseTemplate ? summoner.sceneHorseTemplate : summoner.horsePrefab) : null;
        public void Refresh()
        {
            if(!ui || !equipment)return;
            ui.weaponName.text="YOUR HORSE";
            ui.weaponCaption.text="SADDLE  /  HORSESHOES";
            ui.weaponImage.sprite=horseArtwork;ui.weaponImage.enabled=horseArtwork;
            displayedLevel=equipment.Level;
            ui.levelLabel.text=$"LEVEL {displayedLevel:00} / {PlayerHorseEquipment.MaxLevel}";
            ui.upgradeButton.gameObject.SetActive(true);
            ui.upgradeButton.interactable=displayedLevel<PlayerHorseEquipment.MaxLevel;
            ui.upgradeLabel.text=ui.upgradeButton.interactable ? "UPGRADE  +1" : "MAX LEVEL";
            for(int i=0;i<ui.tabs.Length;i++)
            {ui.tabBackgrounds[i].color=Dark;ui.tabs[i].GetComponentInChildren<TMP_Text>().color=Color.white;}
            horseTab.GetComponent<UnityEngine.UI.Image>().color=Selected;horseTab.GetComponentInChildren<TMP_Text>().color=Gold;
            for(int i=0;i<ui.slots.Length;i++)
            {
                var s=ui.slots[i];s.button.gameObject.SetActive(i<2);if(i>=2)continue;
                var kind=(HorseEquipmentSlot)i;var item=equipment.Equipped(kind);
                var example=item ? item : equipment.catalog.FirstOrDefault(e=>e && e.slot==kind);
                s.title.text=SlotName(kind).ToUpperInvariant();s.item.text=item ? "EQUIPPED" : "EMPTY +";
                s.item.color=item ? new Color(.42f,.86f,.77f) : Gold;
                s.icon.sprite=example ? example.icon : null;s.icon.color=item ? Color.white : new Color(1,1,1,.4f);
                s.background.color=slot==kind ? Selected : Dark;
            }
            ui.slotTitle.text=SlotName(slot).ToUpperInvariant()+" COLLECTION";
            var equipped=equipment.Equipped(slot);
            if(ui.cardContent)ui.cardContent.sizeDelta=new Vector2(ui.cardContent.sizeDelta.x,visible.Length*106);
            for(int i=0;i<ui.cards.Length;i++)
            {
                var card=ui.cards[i];card.button.gameObject.SetActive(i<visible.Length);if(i>=visible.Length)continue;
                var item=visible[i];card.icon.sprite=item.icon;card.title.text=Name(item);
                card.bonus.text=$"{item.speedPercent:+0.#;-0.#;0}% SPEED  {item.specialDamagePercent:+0.#;-0.#;0}% DAMAGE\n{item.specialKnockbackPercent:+0.#;-0.#;0}% KNOCKBACK"+(item.specialFlatDamage!=0?$"  {item.specialFlatDamage:+0;-0;0} DMG":"");
                card.status.text=item==equipped?"EQUIPPED":item==selected?"SELECTED":"AVAILABLE";
                card.background.color=item==selected?Selected:Dark;
            }
            ui.selectedImage.sprite=selected?selected.icon:null;ui.selectedImage.enabled=selected;
            ui.selectionTitle.text=selected?Name(selected):"Choose an item";
            ui.selectionDescription.text=selected?selected.description:"No items available for this slot.";
            var current=equipment.Capture();var preview=selected?equipment.Preview(slot,selected):current;
            var basis=Basis;float walk=basis?basis.walkSpeed:3.5f,run=basis?basis.runSpeed:8f,kb=basis?basis.specialKnockback:5f;int damage=basis?basis.specialDamage:35;
            ui.liveStats[0].text=$"MOVEMENT\nWALK {walk*current.speed:0.##} m/s\nRUN {run*current.speed:0.##} m/s";
            var animations=basis ? basis.Animations : null;
            float mountSeconds=(animations ? animations.Duration(HorseMotion.Mount) : 1f)/current.speed;
            float dismountSeconds=(animations ? animations.Duration(HorseMotion.Dismount) : 1f)/current.speed;
            ui.liveStats[1].text=$"ANIMATION {current.speed:0.##}x\nMOUNT {mountSeconds:0.##} s\nDISMOUNT {dismountSeconds:0.##} s";
            ui.liveStats[2].text=$"SPECIAL DASH\nDMG {current.Damage(damage)}\nKNOCKBACK {kb*current.knockback:0.##}";
            ui.comparisonStats[0].text=$"WALK {walk*current.speed:0.##} > {walk*preview.speed:0.##}   RUN {run*current.speed:0.##} > {run*preview.speed:0.##} m/s";
            ui.comparisonStats[1].text=$"ANIMATION {current.speed:0.##}x > {preview.speed:0.##}x\nSPECIAL DAMAGE {current.Damage(damage)} > {preview.Damage(damage)}";
            ui.comparisonStats[2].text=$"SPECIAL KNOCKBACK {kb*current.knockback:0.##} > {kb*preview.knockback:0.##}\nBefore enemy resistance";
            ui.attachButton.interactable=selected && selected!=equipped;ui.attachLabel.text=selected==equipped && selected?"EQUIPPED":"EQUIP ITEM";
            ui.removeButton.interactable=equipped;
        }
        static string Name(HorseEquipmentItem item)=>string.IsNullOrWhiteSpace(item.displayName)?item.name:item.displayName;
        static string SlotName(HorseEquipmentSlot slot)=>slot==HorseEquipmentSlot.Saddle?"Saddle":"Horseshoes";
    }
}
