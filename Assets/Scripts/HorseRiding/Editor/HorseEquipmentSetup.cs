using System;
using System.Linq;
using SoulsLike.Riding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HorseEquipmentSetup
{
    const string Folder="Assets/Data/Horse/Equipment";
    const string Art="Assets/Textures/Horse/Equipment";
    [MenuItem("Tools/Riding/Set Up Horse Equipment And Ground Contact")]
    public static string Setup()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode before horse equipment setup.");
        var rider=UnityEngine.Object.FindAnyObjectByType<MountedPlayerController>();
        var ui=UnityEngine.Object.FindAnyObjectByType<WeaponEquipmentUI>();
        var summoner=rider ? rider.GetComponent<HorseSummoner>() : null;
        if(!rider || !ui || !summoner)throw new InvalidOperationException("Existing riding player, summoner and equipment UI required.");
        EnsureFolder(Folder);EnsureFolder(Art);
        var saddleIcon=MakeIcon(false);var shoeIcon=MakeIcon(true);
        var items=new [] {
            Item("Trail Saddle",HorseEquipmentSlot.Saddle,saddleIcon,5,10,0,10,"A balanced trail saddle. Improves pace, dash damage and impact."),
            Item("Charger Saddle",HorseEquipmentSlot.Saddle,saddleIcon,-5,25,5,30,"A reinforced saddle for powerful charges. Trades a little speed for damage and knockback."),
            Item("Windrider Saddle",HorseEquipmentSlot.Saddle,saddleIcon,15,5,0,0,"A light saddle that raises movement and animation speed."),
            Item("Iron Horseshoes",HorseEquipmentSlot.Horseshoes,shoeIcon,5,0,0,25,"Heavy iron shoes give the special dash a stronger shove."),
            Item("Swift Horseshoes",HorseEquipmentSlot.Horseshoes,shoeIcon,25,0,0,0,"Lightweight shoes increase movement speed and the matching animation rate."),
            Item("War Horseshoes",HorseEquipmentSlot.Horseshoes,shoeIcon,10,15,0,15,"Reinforced shoes add speed, special damage and knockback.")
        };
        var equipment=rider.GetComponent<PlayerHorseEquipment>();if(!equipment)equipment=Undo.AddComponent<PlayerHorseEquipment>(rider.gameObject);
        equipment.catalog=items;EditorUtility.SetDirty(equipment);
        var bridge=ui.GetComponent<HorseEquipmentUI>();if(!bridge)bridge=Undo.AddComponent<HorseEquipmentUI>(ui.gameObject);
        bridge.ui=ui;bridge.equipment=equipment;bridge.summoner=summoner;ui.horseUI=bridge;
        for(int i=0;i<ui.tabs.Length;i++)Place((RectTransform)ui.tabs[i].transform,38+i*251,179,241,61);
        var tab=ui.safeContent.Find("04   HORSE");
        if(!tab)
        {
            var go=new GameObject("04   HORSE",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));
            go.transform.SetParent(ui.safeContent,false);tab=go.transform;
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.065f,.082f,.094f);image.raycastTarget=true;
            var b=go.GetComponent<UnityEngine.UI.Button>();b.targetGraphic=image;b.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            var line=go.AddComponent<UnityEngine.UI.Outline>();line.effectColor=new Color(.22f,.25f,.25f);line.effectDistance=new Vector2(1,-1);
            var label=new GameObject("Label",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();label.transform.SetParent(tab,false);
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
            label.text="04   HORSE";label.font=ui.tabs[0].GetComponentInChildren<TMP_Text>().font;label.fontSize=18;label.color=new Color(.92f,.91f,.86f);label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
        }
        Place((RectTransform)tab,38+3*251,179,241,61);bridge.horseTab=tab.GetComponent<UnityEngine.UI.Button>();
        foreach(var text in ui.panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if(text.text=="Build your weapon.")text.text="Build your loadout.";
            if(text.text=="Choose a weapon. Shape the way it fights.")text.text="Choose a weapon or horse. Equip it for your next battle.";
        }
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Horse/Horse.fbx");
        var preview=AssetPreview.GetAssetPreview(model);
        if(preview)bridge.horseArtwork=SaveSprite(Art+"/Horse.png",preview.EncodeToPNG());
        if(!bridge.horseArtwork)bridge.horseArtwork=saddleIcon;
        const string horsePath="Assets/Prefabs/Horse/RideableHorse.prefab";
        var root=PrefabUtility.LoadPrefabContents(horsePath);float prefabShift;
        try
        {
            var horse=root.GetComponent<HorseController>();horse.groundSnapDistance=.8f;horse.specialKnockback=5;
            prefabShift=AlignModel(horse);PrefabUtility.SaveAsPrefabAsset(root,horsePath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        var sceneHorse=summoner.sceneHorseTemplate;
        float sceneShift=AlignModel(sceneHorse);sceneHorse.equipment=equipment;
        EditorUtility.SetDirty(sceneHorse);PrefabUtility.RecordPrefabInstancePropertyModifications(sceneHorse);
        EditorUtility.SetDirty(bridge);EditorUtility.SetDirty(ui);
        // The UI retains its existing modal input/pause ownership and button listeners.
        ui.SelectWeapon(PlayerCombatMode.Sword);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);EditorSceneManager.SaveScene(ui.gameObject.scene);
        Selection.activeGameObject=ui.gameObject;
        return $"Horse tab and {items.Length} example items saved. Model alignment: prefab {prefabShift:F3}m, scene {sceneShift:F3}m. No gameplay tests.";
    }
    public static float AlignModel(HorseController horse)
    {
        var visual=horse.animationController.animator.transform;
        var renderer=visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="Skeletal_Horse");
        var baked=new Mesh();float bottom;
        try {renderer.BakeMesh(baked);bottom=baked.vertices.Min(v=>renderer.transform.TransformPoint(v).y);}
        finally{UnityEngine.Object.DestroyImmediate(baked);}
        var motor=horse.GetComponent<CharacterController>();
        float radius=motor.radius*Mathf.Max(Mathf.Abs(horse.transform.lossyScale.x),Mathf.Abs(horse.transform.lossyScale.z));
        float height=Mathf.Max(motor.height*Mathf.Abs(horse.transform.lossyScale.y),radius*2);
        float ground=horse.transform.TransformPoint(motor.center).y-height*.5f;
        float shift=ground-bottom;
        visual.position+=Vector3.up*shift;
        EditorUtility.SetDirty(visual);
        if(PrefabUtility.IsPartOfPrefabInstance(visual))PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        return shift;
    }
    static HorseEquipmentItem Item(string name,HorseEquipmentSlot slot,Sprite icon,float speed,float damage,int flat,float kb,string description)
    {
        string path=Folder+"/"+name+".asset";var item=AssetDatabase.LoadAssetAtPath<HorseEquipmentItem>(path);
        if(item)return item;
        item=ScriptableObject.CreateInstance<HorseEquipmentItem>();item.name=name;item.displayName=name;item.slot=slot;item.icon=icon;
        item.speedPercent=speed;item.specialDamagePercent=damage;item.specialFlatDamage=flat;item.specialKnockbackPercent=kb;item.description=description;
        AssetDatabase.CreateAsset(item,path);return item;
    }
    static Sprite MakeIcon(bool shoe)
    {
        string path=Art+(shoe?"/Horseshoes.png":"/Saddle.png");var existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(existing)return existing;
        var texture=new Texture2D(128,128,TextureFormat.RGBA32,false);
        for(int y=0;y<128;y++)for(int x=0;x<128;x++)
        {
            float dx=x-64,dy=y-62,r=Mathf.Sqrt(dx*dx+dy*dy);Color color=Color.clear;
            if(shoe)
            {
                if(r>29 && r<47 && !(dy>16 && Mathf.Abs(dx)<27))color=Color.Lerp(new Color(.37f,.49f,.54f),new Color(.83f,.89f,.86f),(y+128-x)/256f);
                foreach(var hole in new[]{new Vector2(29,61),new Vector2(35,40),new Vector2(52,27),new Vector2(99,61),new Vector2(93,40),new Vector2(76,27)})
                    if(Vector2.Distance(new Vector2(x,y),hole)<3 && color.a>0)color=new Color(.1f,.14f,.16f);
            }
            else
            {
                if(Mathf.Abs(dx)<43 && y>28 && y<66+9*Mathf.Cos(dx/43*Mathf.PI))color=new Color(.38f,.18f,.085f);
                if(Mathf.Abs(dx)<33 && y>50 && y<77+9*Mathf.Sin(dx/33*Mathf.PI*.5f))color=new Color(.68f,.39f,.16f);
                if((Mathf.Abs(dx+34)<5 && y>58 && y<91)||(Mathf.Abs(dx-34)<5 && y>65 && y<94))color=new Color(.87f,.65f,.32f);
                if(Mathf.Abs(dx)<5 && y>12 && y<52)color=new Color(.72f,.54f,.27f);
                if(Mathf.Abs(dx)<12 && y>12 && y<31 && (Mathf.Abs(dx)>8 || y<16 || y>27))color=new Color(.85f,.84f,.7f);
            }
            texture.SetPixel(x,y,color);
        }
        texture.Apply();var sprite=SaveSprite(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);return sprite;
    }
    static Sprite SaveSprite(string path,byte[] png)
    {
        System.IO.File.WriteAllBytes(path,png);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static void Place(RectTransform rect,float x,float y,float width,float height)
    {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
    static void EnsureFolder(string folder)
    {if(AssetDatabase.IsValidFolder(folder))return;int split=folder.LastIndexOf('/');EnsureFolder(folder.Substring(0,split));AssetDatabase.CreateFolder(folder.Substring(0,split),folder.Substring(split+1));}
}
