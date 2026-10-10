using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ElementalGems.Editor
{
    public static class GemUIBuilder
    {
        private static readonly Color Ink = new Color(.035f,.055f,.085f,.98f);
        private static readonly Color Muted = new Color(.67f,.76f,.85f);
        private static readonly Color Gold = new Color(.95f,.77f,.43f);
        public static GemSelectionUI Create(GemManager manager)
        {
            var root = new GameObject("Gem Selection UI",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(root,"Create gem selection UI");
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay; root.GetComponent<Canvas>().sortingOrder=100;
            var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900); scaler.matchWidthOrHeight=.5f;
            var ui=root.AddComponent<GemSelectionUI>(); ui.manager=manager;
            var launcher=Rect("Open Gems",root.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-320,-82),new Vector2(-32,-30));
            ui.openButton=Button(launcher, new Color(.075f,.105f,.16f));
            ui.equippedLabel=Text("Equipped Gem",launcher,"GEMS  /  NORMAL",19,Gold); ui.equippedLabel.alignment=TextAlignmentOptions.Center;
            var overlay=Rect("Selection Overlay",root.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            Image(overlay,new Color(0,0,0,.68f),true); ui.panel=overlay.gameObject;
            var panel=Rect("Gem Panel",overlay,new Vector2(.08f,.08f),new Vector2(.92f,.92f),Vector2.zero,Vector2.zero); Image(panel,Ink,true);
            var accent=Rect("Accent",panel,new Vector2(0,1),Vector2.one,new Vector2(0,-3),Vector2.zero); Image(accent,Gold,false);
            var title=Rect("Title",panel,new Vector2(.035f,.84f),new Vector2(.8f,.96f),Vector2.zero,Vector2.zero);
            Text("Heading",title,"ELEMENTAL GEMS",34,Color.white);
            var sub=Rect("Subtitle",panel,new Vector2(.035f,.79f),new Vector2(.92f,.85f),Vector2.zero,Vector2.zero);
            Text("Subtitle",sub,"ONE GEM. BOTH WEAPONS.   /   Select an element, then equip it.",17,Muted);
            var close=Rect("Close",panel,new Vector2(.91f,.89f),new Vector2(.97f,.97f),Vector2.zero,Vector2.zero);
            ui.closeButton=Button(close,new Color(.12f,.16f,.22f)); Text("Label",close,"X",22,Color.white).alignment=TextAlignmentOptions.Center;
            var grid=Rect("Gem Grid",panel,new Vector2(.035f,.08f),new Vector2(.57f,.75f),Vector2.zero,Vector2.zero);
            var layout=grid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>(); layout.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount=2;
            layout.spacing=new Vector2(12,12); layout.cellSize=new Vector2(345,112);
            var fitter=grid.gameObject.AddComponent<GemGridLayout>(); fitter.rows=4; fitter.columns=2;
            ui.cards=new GemCard[manager.gems.Length];
            for(int i=0;i<manager.gems.Length;i++)
            {
                var gem=manager.gems[i];
                var card=Rect(gem.displayName,grid,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(345,112));
                var button=Button(card,new Color(.075f,.105f,.16f));
                var icon=Rect("Icon",card,new Vector2(.02f,.16f),new Vector2(.28f,.84f),Vector2.zero,Vector2.zero);
                var img=Image(icon,Color.white,false); img.sprite=gem.icon; img.preserveAspect=true;
                var name=Rect("Name",card,new Vector2(.31f,.47f),new Vector2(.98f,.86f),Vector2.zero,Vector2.zero); Text("Label",name,gem.displayName,23,Color.white);
                var role=Rect("Role",card,new Vector2(.31f,.15f),new Vector2(.98f,.47f),Vector2.zero,Vector2.zero);
                string[] roles={"Unmodified attacks","Burn / Power","Slow / Knockback","Root / Poison / Heal","Armor / Stagger","Speed / Stun","Mobility / Combo","Void / Life steal"};
                Text("Label",role,roles[i],15,Muted);
                ui.cards[i]=new GemCard{gem=gem,button=button,background=card.GetComponent<UnityEngine.UI.Image>()};
            }
            var detail=Rect("Gem Details",panel,new Vector2(.6f,.08f),new Vector2(.965f,.75f),Vector2.zero,Vector2.zero); Image(detail,new Color(.06f,.085f,.13f),false);
            var hero=Rect("Selected Icon",detail,new Vector2(.06f,.71f),new Vector2(.32f,.96f),Vector2.zero,Vector2.zero); ui.selectedIcon=Image(hero,Color.white,false); ui.selectedIcon.preserveAspect=true;
            var nameBox=Rect("Selected Name",detail,new Vector2(.35f,.72f),new Vector2(.95f,.94f),Vector2.zero,Vector2.zero); ui.selectedName=Text("Name",nameBox,"Normal",32,Color.white);
            var descBox=Rect("Description",detail,new Vector2(.07f,.43f),new Vector2(.93f,.7f),Vector2.zero,Vector2.zero); ui.description=Text("Text",descBox,"",20,Muted); ui.description.alignment=TextAlignmentOptions.TopLeft;
            var matchBox=Rect("Matchups",detail,new Vector2(.07f,.25f),new Vector2(.93f,.42f),Vector2.zero,Vector2.zero); ui.matchup=Text("Text",matchBox,"Neutral matchups  1x",18,Gold);
            var equip=Rect("Equip",detail,new Vector2(.07f,.07f),new Vector2(.93f,.21f),Vector2.zero,Vector2.zero);
            ui.equipButton=Button(equip,Gold); ui.equipLabel=Text("Label",equip,"EQUIPPED",22,Ink); ui.equipLabel.alignment=TextAlignmentOptions.Center;
            var footer=Rect("Footer",panel,new Vector2(.035f,.015f),new Vector2(.97f,.065f),Vector2.zero,Vector2.zero);
            Text("Hint",footer,"Sword + bow share this gem   /   Flying arrows keep their element   /   G to open - Esc to close",15,Muted);
            var events=UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
            if(events.Length==0) new GameObject("EventSystem",typeof(UnityEngine.EventSystems.EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            overlay.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/ElementalGems/UI/Gem Selection UI.prefab");
            GemCompactUIBuilder.Upgrade();
            return ui;
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
            r.anchorMin=min;r.anchorMax=max;r.offsetMin=low;r.offsetMax=high; return r;
        }
        private static UnityEngine.UI.Image Image(RectTransform r,Color color,bool raycast)
        {var i=r.gameObject.AddComponent<UnityEngine.UI.Image>();i.color=color;i.raycastTarget=raycast;return i;}
        private static UnityEngine.UI.Button Button(RectTransform r,Color color)
        {
            var image=Image(r,color,true);var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;
            var c=b.colors;c.highlightedColor=new Color(1.15f,1.15f,1.15f);c.pressedColor=new Color(.8f,.8f,.8f);c.selectedColor=Color.white;c.disabledColor=new Color(.55f,.55f,.55f,.8f);b.colors=c;return b;
        }
        private static TMP_Text Text(string name,RectTransform parent,string value,float size,Color color)
        {
            var r=Rect(name,parent,Vector2.zero,Vector2.one,new Vector2(4,2),new Vector2(-4,-2));
            var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.text=value;text.fontSize=size;text.color=color;text.raycastTarget=false;
            text.alignment=TextAlignmentOptions.MidlineLeft;text.enableAutoSizing=true;text.fontSizeMin=size*.8f;text.fontSizeMax=size;
            return text;
        }
    }
}
