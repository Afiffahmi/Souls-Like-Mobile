using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WeaponEquipmentArtSetup
{
    const string Root = "Assets/Data/Combat/Weapons";
    public static string Run()
    {
        string atlasPath = "Assets/Textures/Combat/Weapons/UI/Art/EquipmentAtlas.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048; importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
        // Atlas boundaries follow the generated artwork; sprite rects are trimmed to alpha inside each cell.
        int[] x = {0,415,705,1022,1380,1600}; int[] y = {0,340,660,960};
        string[,] names = {
            {"Sword","Blade","Grip","Sheath","SwordCharm"},
            {"Bow","Limbs","String","Quiver","BowCharm"},
            {"Bracelet","Focus","Core","Conduit","MagicCharm"}
        };
        var sprites = new Sprite[3,5];
        var pixels = texture.GetPixels32();
        for(int row=0;row<3;row++) for(int col=0;col<5;col++)
        {
            string path="Assets/Textures/Combat/Weapons/UI/Art/"+names[row,col]+(row==1 && col==1?"-v2":"")+".asset";
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite==null)
            {
                int left=Mathf.RoundToInt(x[col]*texture.width/1600f), right=Mathf.RoundToInt(x[col+1]*texture.width/1600f);
                int bottom=texture.height-Mathf.RoundToInt(y[row+1]*texture.height/960f), top=texture.height-Mathf.RoundToInt(y[row]*texture.height/960f);
                if(row==1 && col==1) { left=Mathf.RoundToInt(420*texture.width/1600f);right=Mathf.RoundToInt(680*texture.width/1600f);bottom=texture.height-Mathf.RoundToInt(651*texture.height/960f); }
                int minX=right,minY=top,maxX=left,maxY=bottom;
                for(int yy=bottom;yy<top;yy++) for(int xx=left;xx<right;xx++) if(pixels[yy*texture.width+xx].a>30)
                {minX=Math.Min(minX,xx);minY=Math.Min(minY,yy);maxX=Math.Max(maxX,xx);maxY=Math.Max(maxY,yy);}
                var rect=new Rect(Math.Max(left,minX-2),Math.Max(bottom,minY-2),Math.Min(right,maxX+3)-Math.Max(left,minX-2),Math.Min(top,maxY+3)-Math.Max(bottom,minY-2));
                sprite=Sprite.Create(texture,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                sprite.name=names[row,col]; AssetDatabase.CreateAsset(sprite,path);
            }
            sprites[row,col]=sprite;
        }
        PlayerCombatMode[] modes={PlayerCombatMode.Sword,PlayerCombatMode.Bow,PlayerCombatMode.Magic};
        string[] weaponNames={"Vanguard Sword","Ranger Bow","Arcane Bracelet"};
        string[][] assets={
            new[]{"Sword_HeavyBlade","Sword_FinisherGrip","Sword_QuickdrawSheath","Sword_SpecialCharm"},
            new[]{"Bow_ReinforcedLimbs","Bow_FastString","Bow_HeavyQuiver","Bow_PrecisionCharm"},
            new[]{"Magic_QuickFocus","Magic_PowerCore","Magic_HeavyConduit","Magic_SpecialCharm"}
        };
        string[][] titles={
            new[]{"Heavy Blade","Finisher Grip","Quickdraw Sheath","Duelist Charm"},
            new[]{"Reinforced Limbs","Swift String","Heavy Quiver","Precision Charm"},
            new[]{"Quick Focus","Power Core","Heavy Conduit","Moon Charm"}
        };
        for(int row=0;row<3;row++)
        {
            var definition=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Combat/Weapons/"+modes[row]+".asset");
            definition.displayName=weaponNames[row];definition.artwork=sprites[row,0];EditorUtility.SetDirty(definition);
            for(int col=0;col<4;col++)
            {
                var item=AssetDatabase.LoadAssetAtPath<WeaponAccessoryDefinition>("Assets/Data/Combat/Weapons/Accessories/"+assets[row][col]+".asset");
                item.displayName=titles[row][col];item.icon=sprites[row,col+1];EditorUtility.SetDirty(item);
                string alternativePath="Assets/Data/Combat/Weapons/Accessories/"+modes[row]+"_Balanced"+item.slot+".asset";
                var alternative=AssetDatabase.LoadAssetAtPath<WeaponAccessoryDefinition>(alternativePath);
                if(alternative==null)
                {
                    alternative=ScriptableObject.CreateInstance<WeaponAccessoryDefinition>();
                    alternative.weapon=modes[row];alternative.slot=item.slot;
                    alternative.modifiers=new[]{new WeaponStatModifier{attacks=WeaponAttackMask.All,damagePercent=8,agilityPercent=8}};
                    alternative.description="A balanced "+item.slot.ToString().ToLowerInvariant()+". A small damage and speed bonus for every attack.";
                    AssetDatabase.CreateAsset(alternative,alternativePath);
                }
                alternative.displayName="Balanced "+item.slot;alternative.icon=sprites[row,col+1];EditorUtility.SetDirty(alternative);
            }
        }
        importer.isReadable=false;importer.SaveAndReimport();
        AssetDatabase.SaveAssets();return "15 sprite images assigned to 3 weapons and 24 sample accessories.";
    }
}
