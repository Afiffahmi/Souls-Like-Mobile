using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ElementalGems.Editor
{
    public static class GemCompactUIBuilder
    {
        static readonly Color Ink = new Color(.035f,.055f,.085f,.97f);
        static readonly Color Gold = new Color(.95f,.77f,.43f);
        public static string Upgrade()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Upgrade outside Play Mode.");
            var ui = Object.FindAnyObjectByType<GemSelectionUI>();
            if (ui == null) throw new System.InvalidOperationException("Existing gem UI is missing.");
            if (ui.compact && ui.slot != null) return "Compact slot already installed.";
            Undo.RecordObject(ui,"Compact gem UI");
            // Preserve the former layout for recovery, while removing it from the live HUD.
            foreach (Transform child in ui.transform) { Undo.RecordObject(child.gameObject,"Hide old gem panel"); child.gameObject.SetActive(false); }
            var root = Box("Gem Slot",ui.transform,new Vector2(1,1),new Vector2(-24,-98),new Vector2(292,64));
            root.pivot = new Vector2(1,1); root.anchoredPosition = new Vector2(-24,-98);
            Fill(root,Ink,false);
            var outline = root.gameObject.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor = new Color(.6f,.47f,.26f,.85f); outline.effectDistance = new Vector2(1,-1);
            var previous = Box("Previous Gem",root,new Vector2(0,.5f),new Vector2(23,0),new Vector2(42,60));
            ui.previousButton = Button(previous,Ink); Label(previous,"<",24,Gold);
            var center=Box("Equipped Gem",root,new Vector2(.5f,.5f),Vector2.zero,new Vector2(200,60));
            ui.openButton=Button(center,Ink);
            var icon=Box("Icon",center,new Vector2(0,.5f),new Vector2(30,0),new Vector2(46,46));
            ui.equippedIcon=Fill(icon,Color.white,false);ui.equippedIcon.preserveAspect=true;
            var caption=Box("Caption",center,new Vector2(0,.5f),new Vector2(117,14),new Vector2(114,18));
            Label(caption,"GEM",11,Gold,TextAlignmentOptions.MidlineLeft);
            var name=Box("Name",center,new Vector2(0,.5f),new Vector2(117,-9),new Vector2(114,27));
            ui.equippedLabel=Label(name,"Normal",19,Color.white,TextAlignmentOptions.MidlineLeft);
            var indicator=Box("Dropdown Indicator",center,new Vector2(1,.5f),new Vector2(-10,0),new Vector2(20,30));
            ui.dropdownIndicator=Label(indicator,"+",20,Gold);
            var next=Box("Next Gem",root,new Vector2(1,.5f),new Vector2(-23,0),new Vector2(42,60));
            ui.nextButton=Button(next,Ink);Label(next,">",24,Gold);
            var drop=Box("Gem Dropdown",root,new Vector2(1,0),new Vector2(0,-8),new Vector2(292,414));
            drop.pivot=new Vector2(1,1);drop.anchoredPosition=new Vector2(0,-8);Fill(drop,Ink,true);
            var border=drop.gameObject.AddComponent<UnityEngine.UI.Outline>();border.effectColor=new Color(.6f,.47f,.26f,.85f);border.effectDistance=new Vector2(1,-1);
            ui.cards=new GemCard[ui.manager.gems.Length];
            string[] roles={"No elemental effect","Burn / power","Slow / knockback","Root / poison / heal","Armor / stagger","Speed / stun","Mobility / combo","Void / life steal"};
            for(int i=0;i<ui.cards.Length;i++)
            {
                var gem=ui.manager.gems[i];
                var row=Box(gem.displayName,drop,new Vector2(.5f,1),new Vector2(0,-(8+24+i*50)),new Vector2(276,48));
                var button=Button(row,new Color(.075f,.105f,.16f));
                var rowIcon=Box("Icon",row,new Vector2(0,.5f),new Vector2(26,0),new Vector2(38,38));
                var image=Fill(rowIcon,Color.white,false);image.sprite=gem.icon;image.preserveAspect=true;
                var rowName=Box("Name",row,new Vector2(0,.5f),new Vector2(148,10),new Vector2(186,22));
                Label(rowName,gem.displayName,17,Color.white,TextAlignmentOptions.MidlineLeft);
                var role=Box("Effect",row,new Vector2(0,.5f),new Vector2(148,-11),new Vector2(186,18));
                Label(role,roles[(int)gem.element],12,new Color(.67f,.76f,.85f),TextAlignmentOptions.MidlineLeft);
                ui.cards[i]=new GemCard{gem=gem,button=button,background=row.GetComponent<UnityEngine.UI.Image>()};
            }
            ui.slot=root;ui.panel=drop.gameObject;ui.compact=true;
            ui.closeButton=null;ui.equipButton=null;ui.selectedIcon=null;ui.selectedName=null;ui.description=null;ui.matchup=null;ui.equipLabel=null;
            ui.equippedIcon.sprite=ui.manager.gems[0].icon;drop.gameObject.SetActive(false);
            EditorUtility.SetDirty(ui);
            PrefabUtility.SaveAsPrefabAsset(ui.gameObject,GemSystemSetup.Root+"/UI/Gem Selection UI.prefab");
            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);EditorSceneManager.SaveScene(ui.gameObject.scene);
            AssetDatabase.SaveAssets();return "Compact gem slot installed: previous/next arrows, instant-equip dropdown, no full-screen overlay.";
        }
        static RectTransform Box(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));Undo.RegisterCreatedObjectUndo(go,"Create compact gem slot");
            var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.sizeDelta=size;r.anchoredPosition=position;return r;
        }
        static UnityEngine.UI.Image Fill(RectTransform r,Color color,bool raycast)
        {var img=r.gameObject.AddComponent<UnityEngine.UI.Image>();img.color=color;img.raycastTarget=raycast;return img;}
        static UnityEngine.UI.Button Button(RectTransform r,Color color)
        {var image=Fill(r,color,true);var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;return b;}
        static TMP_Text Label(RectTransform parent,string value,float size,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.Center)
        {
            var t=new GameObject("Label",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();t.transform.SetParent(parent,false);
            t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;t.rectTransform.offsetMin=Vector2.zero;t.rectTransform.offsetMax=Vector2.zero;
            t.text=value;t.fontSize=size;t.color=color;t.raycastTarget=false;t.alignment=alignment;return t;
        }
    }
}
