using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class WeaponEquipmentUIBuilder
{
    public const string Root = "Assets/Combat/Weapons/UI";
    static readonly Color Ink = new Color(.027f,.040f,.049f);
    static readonly Color Panel = new Color(.042f,.058f,.067f);
    static readonly Color Card = new Color(.065f,.082f,.094f);
    static readonly Color Gold = new Color(.86f,.70f,.43f);
    static readonly Color Muted = new Color(.51f,.59f,.61f);
    static readonly Color White = new Color(.92f,.91f,.86f);
    static readonly Color Teal = new Color(.35f,.75f,.67f);
    [MenuItem("Tools/Combat/Build Weapon Equipment UI")]
    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build in Edit Mode.");
        var equipment = Object.FindFirstObjectByType<PlayerWeaponEquipment>();
        if (equipment == null) throw new InvalidOperationException("Set up player weapon equipment first.");
        var existing = Object.FindFirstObjectByType<WeaponEquipmentUI>();
        if (existing != null) throw new InvalidOperationException("Weapon Equipment UI already exists; edit it directly or delete it before rebuilding.");
        var root = new GameObject("Weapon Equipment UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root,"Create weapon equipment UI");
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600,900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var ui = root.AddComponent<WeaponEquipmentUI>(); ui.equipment = equipment;
        ui.catalog = AssetDatabase.FindAssets("t:WeaponAccessoryDefinition",new[]{"Assets/Combat/Weapons/Accessories"})
            .Select(g=>AssetDatabase.LoadAssetAtPath<WeaponAccessoryDefinition>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(a=>a.name).ToArray();
        var launcher = Rect("Open Equipment",root.transform,0,0,264,58);
        launcher.anchorMin = launcher.anchorMax = launcher.pivot = Vector2.one; launcher.anchoredPosition = new Vector2(-24,-180);
        ui.openButton = Button(launcher,Card); Outline(launcher,Gold);
        Text(launcher,"EQUIPMENT    [ I ]",20,Gold);

        var overlay = Rect("Equipment Overlay",root.transform,0,0,1600,900);
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one; overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        Fill(overlay,Ink,true); ui.panel = overlay.gameObject;
        var content = Rect("Safe Area Content",overlay,0,0,1600,900);
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f,.5f); content.anchoredPosition = Vector2.zero; ui.safeContent = content;
        Fill(Rect("Header Accent",content,38,28,48,3),Gold);
        Label(content,"ARMORY / EQUIPMENT",38,43,500,24,14,Gold);
        Label(content,"Build your weapon.",36,69,800,56,38,White);
        Label(content,"Choose a weapon. Shape the way it fights.",38,128,740,28,17,Muted);
        ui.closeButton = Button(Rect("Close Equipment",content,1478,45,82,64),Card);
        Text(ui.closeButton.transform,"CLOSE",14,White);
        ui.tabs = new Button[3]; ui.tabBackgrounds = new Image[3];
        string[] names = {"01   SWORD", "02   BOW", "03   MAGIC BRACELET"};
        for (int i=0;i<3;i++)
        {
            var tab=Rect(names[i],content,38+i*322,179,306,61);
            ui.tabs[i]=Button(tab,Card); ui.tabBackgrounds[i]=tab.GetComponent<Image>();
            Text(tab,names[i],18,i==0?Gold:White); Outline(tab,new Color(.22f,.25f,.25f));
        }
        Label(content,"ACCESSORY COLLECTION",1075,182,478,25,15,Gold);
        Label(content,"Example items ready to attach",1075,215,478,24,16,Muted);

        var left = Rect("Weapon Workbench",content,38,262,994,516); Fill(left,Panel); Outline(left,new Color(.18f,.22f,.23f));
        ui.weaponName = Label(left,"Vanguard Sword",215,26,565,46,32,White,TextAlignmentOptions.Center);
        ui.weaponCaption = Label(left,"MODULAR SWORD / 4 ATTACHMENT SLOTS",190,77,615,24,13,Muted,TextAlignmentOptions.Center);
        // Fine bracket lines visually connect slots to the weapon illustration.
        for(int i=0;i<2;i++)
        {
            int y=180+i*188;
            Fill(Rect("Slot Connection Left",left,187,y,118,1),new Color(.29f,.28f,.22f));
            Fill(Rect("Slot Connection Right",left,689,y,118,1),new Color(.29f,.28f,.22f));
        }
        ui.weaponImage=Fill(Rect("Weapon Illustration",left,281,119,432,320),Color.white);ui.weaponImage.preserveAspect=true;
        Label(left,"ATTACHMENTS",354,457,286,18,12,Muted,TextAlignmentOptions.Center);
        ui.slots = new WeaponUISlot[4];
        for(int i=0;i<4;i++)
        {
            int x=i%2==0?28:807, y=i<2?111:299;
            var box=Rect("Accessory Slot "+(i+1),left,x,y,159,165);
            var button=Button(box,Card); Outline(box,new Color(.31f,.29f,.22f));
            ui.slots[i]=new WeaponUISlot {button=button,background=box.GetComponent<Image>(),
                title=Label(box,"SLOT",8,8,143,25,14,Gold,TextAlignmentOptions.Center),
                icon=Fill(Rect("Accessory Image",box,31,37,97,87),Color.white),
                item=Label(box,"EMPTY +",6,134,147,20,12,Muted,TextAlignmentOptions.Center)};
            ui.slots[i].icon.preserveAspect=true;
        }

        var stats=Rect("Live Weapon Stats",content,38,795,994,73); Fill(stats,Panel);
        ui.liveStats=new TMP_Text[3];
        for(int i=0;i<3;i++)
        {
            ui.liveStats[i]=Label(stats,"",20+i*226,5,216,64,14,White);
            if(i>0) Fill(Rect("Stat Divider",stats,i*226+4,14,1,44),new Color(.18f,.22f,.23f));
        }
        ui.levelLabel=Label(stats,"LEVEL 00 / 10",722,5,252,18,13,Gold,TextAlignmentOptions.Center);
        ui.upgradeButton=Button(Rect("Upgrade Weapon",stats,722,29,252,35),new Color(.14f,.22f,.21f));
        ui.upgradeLabel=Text(ui.upgradeButton.transform,"UPGRADE +1",14,Teal);

        var right=Rect("Accessory Details",content,1056,262,506,516); Fill(right,Panel); Outline(right,new Color(.18f,.22f,.23f));
        ui.slotTitle=Label(right,"SHEATH ACCESSORIES",20,17,462,26,18,White);
        var viewport=Rect("Collection Viewport",right,20,55,466,202); Fill(viewport,Color.clear,true); viewport.gameObject.AddComponent<RectMask2D>();
        var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped;
        var cardContent=Rect("Accessory Cards",viewport,0,0,466,ui.catalog.Length*106); scroll.content=cardContent; scroll.viewport=viewport;
        ui.cardContent=cardContent;ui.accessoryScroll=scroll;
        ui.cards=new WeaponUICard[ui.catalog.Length];
        for(int i=0;i<ui.cards.Length;i++)
        {
            var card=Rect("Accessory Card "+(i+1),cardContent,0,i*106,466,96); var b=Button(card,Card);
            ui.cards[i]=new WeaponUICard{button=b,background=card.GetComponent<Image>(),icon=Fill(Rect("Icon",card,10,9,74,76),Color.white),
                title=Label(card,"Accessory",96,9,358,27,19,White),
                bonus=Label(card,"",96,41,358,24,13,Muted),status=Label(card,"AVAILABLE",96,71,358,17,11,Gold)};
            ui.cards[i].icon.preserveAspect=true;
        }
        Fill(Rect("Detail Divider",right,20,270,466,1),new Color(.18f,.22f,.23f));
        ui.selectedImage=Fill(Rect("Selected Accessory",right,20,284,60,64),Color.white);ui.selectedImage.preserveAspect=true;
        ui.selectionTitle=Label(right,"Select an accessory",94,286,390,29,21,White);
        ui.selectionDescription=Label(right,"",94,321,390,53,14,Muted,TextAlignmentOptions.TopLeft);
        Label(right,"ON ATTACH  /  CURRENT > PREVIEW",20,384,466,20,11,Gold);
        ui.comparisonStats=new TMP_Text[3];
        for(int i=0;i<3;i++) ui.comparisonStats[i]=Label(right,"",20,413+i*27,466,25,13,White);

        ui.attachButton=Button(Rect("Attach Accessory",content,1056,795,328,44),Gold);
        ui.attachLabel=Text(ui.attachButton.transform,"ATTACH ACCESSORY",16,Ink);
        ui.removeButton=Button(Rect("Remove Accessory",content,1398,795,164,44),Card);Text(ui.removeButton.transform,"REMOVE",15,White);
        Label(content,"Demo collection / upgrades are free",1056,847,506,22,12,Muted,TextAlignmentOptions.Center);
        ui.feedback=Label(content,"Select a slot, choose an accessory, then attach it.",1075,115,487,50,14,Muted,TextAlignmentOptions.TopLeft);
        WeaponKnockbackUILayout.Apply(ui);
        ui.SelectWeapon(PlayerCombatMode.Sword);
        overlay.gameObject.SetActive(false);
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        return "Created image-based weapon equipment UI with " + ui.catalog.Length + " demo accessories.";
    }
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform));var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
    static Image Fill(RectTransform rect,Color color,bool raycast=false)
    {var img=rect.gameObject.AddComponent<Image>();img.color=color;img.raycastTarget=raycast;return img;}
    static Button Button(RectTransform rect,Color color)
    {
        var img=Fill(rect,color,true);var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=img;
        var colors=button.colors;colors.highlightedColor=new Color(1.18f,1.18f,1.18f);colors.pressedColor=new Color(.8f,.8f,.8f);colors.disabledColor=new Color(.55f,.55f,.55f,.7f);button.colors=colors;
        button.navigation=new Navigation { mode=Navigation.Mode.None };return button;
    }
    static void Outline(RectTransform rect,Color color)
    {var o=rect.gameObject.AddComponent<Outline>();o.effectColor=color;o.effectDistance=new Vector2(1,-1);}
    static TMP_Text Label(Transform parent,string text,float x,float y,float w,float h,float size,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft)
    {return Text(Rect(text.Length<40?text:"Text",parent,x,y,w,h),text,size,color,alignment);}
    static TMP_Text Text(Transform parent,string value,float size,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.Center)
    {
        var go=new GameObject("Label",typeof(RectTransform));go.transform.SetParent(parent,false);var text=go.AddComponent<TextMeshProUGUI>();
        text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;
        text.font=TMP_Settings.defaultFontAsset;text.text=value;text.fontSize=size;text.color=color;text.alignment=alignment;text.raycastTarget=false;
        text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Ellipsis;return text;
    }
}
