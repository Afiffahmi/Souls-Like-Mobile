using System;
using System.Linq;
using ElementalGems;
using SoulsLike.Riding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HorseSummonSetup
{
    [MenuItem("Tools/Riding/Set Up Elemental Horse Summoning")]
    public static void Setup()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode before horse setup.");
        var rider=UnityEngine.Object.FindAnyObjectByType<MountedPlayerController>();
        var horse=UnityEngine.Object.FindObjectsByType<HorseController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
        if(!rider || !rider.GetComponent<GemManager>())throw new InvalidOperationException("Player and GemManager required.");
        var group=GameObject.Find("Canvas/Action Buttons");
        if(!group || !group.GetComponent<UnityEngine.UI.GridLayoutGroup>())throw new InvalidOperationException("Existing action button grid required.");
        var canvas=group.GetComponentInParent<Canvas>();
        if(!canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>())throw new InvalidOperationException("HUD raycaster missing.");
        if(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length!=1)throw new InvalidOperationException("Expected one existing EventSystem.");
        const string materialPath="Assets/Materials/Horse/Horse Tail Energy.mat";
        if(!AssetDatabase.IsValidFolder("Assets/Materials/Horse"))AssetDatabase.CreateFolder("Assets/Materials","Horse");
        var shader=Shader.Find("Horse/Layered Tail Energy");
        if(!shader)throw new InvalidOperationException("Horse tail shader not imported yet.");
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!material){material=new Material(shader){name="Horse Tail Energy"};AssetDatabase.CreateAsset(material,materialPath);}
        // Add the visual component to the reusable prefab through the prefab API.
        const string prefabPath="Assets/Prefabs/Horse/RideableHorse.prefab";
        var contents=PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ConfigureAura(contents.GetComponent<HorseController>(),material);
            PrefabUtility.SaveAsPrefabAsset(contents,prefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(contents);}
        var sceneAura=ConfigureAura(horse,material);
        PrefabUtility.RecordPrefabInstancePropertyModifications(sceneAura);
        var summoner=rider.GetComponent<HorseSummoner>();
        if(!summoner)summoner=Undo.AddComponent<HorseSummoner>(rider.gameObject);
        summoner.rider=rider;summoner.gems=rider.GetComponent<GemManager>();
        summoner.horsePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<HorseController>();
        summoner.sceneHorseTemplate=horse;summoner.summonMaterial=material;
        var buttonTransform=group.transform.Find("Summon Horse");
        if(!buttonTransform)
        {
            var go=new GameObject("Summon Horse",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));
            Undo.RegisterCreatedObjectUndo(go,"Create horse summon button");
            go.transform.SetParent(group.transform,false);buttonTransform=go.transform;
            var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(170,76);
            var image=go.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.06f,.11f,.16f,.95f);image.raycastTarget=true;
            var button=go.GetComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
            button.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            var outline=go.AddComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color(.75f,.61f,.35f,.9f);outline.effectDistance=new Vector2(1,-1);
            var label=new GameObject("Label",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();label.transform.SetParent(go.transform,false);
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
            label.rectTransform.offsetMin=new Vector2(8,6);label.rectTransform.offsetMax=new Vector2(-8,-6);
            var existing=group.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t!=label);
            if(existing)label.font=existing.font;
            label.text="Summon Horse";label.fontSize=23;label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=23;
            label.alignment=TextAlignmentOptions.Center;label.color=new Color(1,.91f,.7f);label.raycastTarget=false;
        }
        summoner.summonButton=buttonTransform.GetComponent<UnityEngine.UI.Button>();summoner.buttonLabel=buttonTransform.GetComponentInChildren<TMP_Text>();
        EditorUtility.SetDirty(summoner);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(horse.gameObject.scene);EditorSceneManager.SaveScene(horse.gameObject.scene);
        Selection.activeGameObject=rider.gameObject;
    }
    static HorseTailAura ConfigureAura(HorseController horse,Material material)
    {
        var aura=horse.GetComponent<HorseTailAura>();if(!aura)aura=horse.gameObject.AddComponent<HorseTailAura>();
        aura.horse=horse;aura.strandMaterial=material;
        string[] names={"tail_1","tail_2","tail_3","tail_4","tail_leaf","tail_leaf_end"};
        var bones=horse.GetComponentsInChildren<Transform>(true);
        aura.tailBones=names.Select(name=>bones.FirstOrDefault(t=>t.name==name)).Where(t=>t).ToArray();
        if(aura.tailBones.Length<2)throw new InvalidOperationException("Horse tail bones missing.");
        EditorUtility.SetDirty(aura);return aura;
    }
}
