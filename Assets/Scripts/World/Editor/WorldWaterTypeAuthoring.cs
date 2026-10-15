using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nusantara.World.Editor
{
    public static class WorldWaterTypeAuthoring
    {
        public static Material DefaultMaterial(WorldWaterKind kind)
        {
            var river=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Nusantara_River.mat");
            if (!river) throw new System.InvalidOperationException("The river water material is missing.");
            if (kind==WorldWaterKind.River) return river;
            const string path="Assets/Materials/Nusantara_Sea.mat";
            var sea=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!sea)
            {
                sea=new Material(river){name="Nusantara_Sea"};
                sea.SetFloat("_ShoreEnabled",0);
                sea.SetFloat("_WaveScale",.085f); sea.SetFloat("_WaveHeight",.12f);
                sea.SetFloat("_CrestStrength",.18f); sea.SetFloat("_Gloss",.4f);
                sea.SetColor("_BaseColor",new Color(.18f,.68f,.73f));
                sea.SetColor("_DeepColor",new Color(.075f,.42f,.52f));
                AssetDatabase.CreateAsset(sea,path);
            }
            return sea;
        }

        public static string SetType(WorldPaintedWater water,WorldWaterKind kind,bool refresh=true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Exit Play mode to change water type.";
            var material=DefaultMaterial(kind);
            Undo.RecordObject(water,"Change water type");
            if (water.Kind==WorldWaterKind.Sea && water.waterMaterial && water.waterMaterial.HasProperty("_ShoreEnabled") && water.waterMaterial.GetFloat("_ShoreEnabled")>.5f)
                water.shorelineMaterial=water.waterMaterial;
            water.SetKind(kind);
            water.waterMaterial=kind==WorldWaterKind.Sea && water.shorelineMaterial ? water.shorelineMaterial : material;
            water.Rebuild(); EditorUtility.SetDirty(water); EditorSceneManager.MarkSceneDirty(water.gameObject.scene);
            return refresh ? RefreshShoreline(water) : Description(kind);
        }

        public static string Description(WorldWaterKind kind)=>kind==WorldWaterKind.Sea
            ? "Sea: waves wash onto the shore with white foam and wet sand."
            : "River: flowing water with no white shoreline foam or sea wash.";

        public static string RefreshShoreline(WorldPaintedWater water)
        {
            if (water.Kind!=WorldWaterKind.Sea) { water.ApplyWaterKind(); return Description(WorldWaterKind.River); }
            water.ApplyWaterKind();
            if (water.CellCount==0) return "Sea selected. Paint water near the beach to create shoreline foam.";
            try { WorldShorelineBaker.Bake(water); return "Sea shoreline updated. White foam and wet sand follow the beach."; }
            catch (System.InvalidOperationException e) { return "Sea shoreline needs an update: "+e.Message; }
        }
    }
}
