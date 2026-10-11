using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using SoulsLike.Riding;
using TMPro;

public static class HorseRidingSetup
{
    public const string Folder = "Assets/Animation/Horse";
    public const string PlayerFolder = "Assets/Animation/Player/HorseRiding";
    [MenuItem("Tools/Riding/Set Up Horse In Active Scene")]
    public static void Setup()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before setup.");
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerStateManager>();
        if (!player || !player.anim) throw new InvalidOperationException("Active scene requires the existing player and Animator.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Horse/Horse.fbx");
        if (!source) throw new InvalidOperationException("Horse.fbx missing.");
        System.IO.Directory.CreateDirectory(Folder);
        System.IO.Directory.CreateDirectory(PlayerFolder);
        System.IO.Directory.CreateDirectory("Assets/Prefabs/Horse");
        AssetDatabase.Refresh();
        var set = AssetDatabase.LoadAssetAtPath<HorseAnimationSet>(Folder+"/HorseAnimations.asset");
        if (!set)
        {
            set = ScriptableObject.CreateInstance<HorseAnimationSet>();
            AssetDatabase.CreateAsset(set,Folder+"/HorseAnimations.asset");
            set.animations = Enum.GetValues(typeof(HorseMotion)).Cast<HorseMotion>().Select(m=>new MountedAnimation {
                motion=m, horseState=HasHorseAnimation(m) ? "Base Layer."+m : "", playerState=HasPlayerAnimation(m) ? "Base Layer.PlayerHorse"+m : "",
                duration=m==HorseMotion.Run ? .65f : m==HorseMotion.Death1 ? 1.6f : m==HorseMotion.Ejected ? 1.2f : 1f
            }).ToArray();
            foreach(var a in set.animations)
            {
                if (HasHorseAnimation(a.motion)) a.horseClip = MakeHorseClip(a.motion,a.Seconds,source.transform);
                if (HasPlayerAnimation(a.motion)) a.playerClip = MakePlayerClip(a.motion,a.Seconds);
                a.horseControllerClip = a.horseClip;
                a.playerControllerClip = a.playerClip;
            }
            set.horseController = MakeController(set,false);
            set.playerController = MakeController(set,true);
            EditorUtility.SetDirty(set);
        }
        HorseWalkingLayerSetup.Configure(set, source.transform);
        // Existing equipment/visual scripts continue reading these parameters while riding.
        var mountedGraph = set.playerController as AnimatorController;
        if (mountedGraph)
        {
            foreach (var parameter in player.anim.parameters)
                if (!mountedGraph.parameters.Any(p => p.name == parameter.name))
                    mountedGraph.AddParameter(parameter);
            EditorUtility.SetDirty(mountedGraph);
        }
        var rider=player.GetComponent<MountedPlayerController>();
        if(!rider) rider=Undo.AddComponent<MountedPlayerController>(player.gameObject);
        rider.player=player; rider.playerAnimator=player.anim; rider.input=player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        EditorUtility.SetDirty(rider);
        var horse=UnityEngine.Object.FindAnyObjectByType<HorseController>();
        if(!horse)
        {
            var root=new GameObject("Rideable Horse"); Undo.RegisterCreatedObjectUndo(root,"Create riding horse");
            root.transform.rotation=Quaternion.Euler(0,45,0);
            Vector3 near=player.transform.position+new Vector3(2.4f,0,0);
            var hits=Physics.RaycastAll(near+Vector3.up*5,Vector3.down,100,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance);
            var ground=hits.FirstOrDefault(h=>!h.transform.IsChildOf(player.transform));
            root.transform.position=ground.collider ? ground.point+Vector3.up*.04f : near;
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source,root.transform);
            model.name="HorseVisual"; model.transform.localScale=Vector3.one*2.5f;
            var anim=model.GetComponent<Animator>(); if(!anim) anim=model.AddComponent<Animator>();
            anim.runtimeAnimatorController=set.horseController; anim.applyRootMotion=false;
            var motor=root.AddComponent<CharacterController>(); motor.height=1.65f; motor.radius=.58f; motor.center=new Vector3(0,.88f,0); motor.stepOffset=.35f; motor.skinWidth=.04f;
            var driver=root.AddComponent<HorseAnimationController>(); driver.animator=anim; driver.animationSet=set;
            horse=root.AddComponent<HorseController>(); horse.animationController=driver;
            float hipOffset = player.anim.isHuman ? player.anim.GetBoneTransform(HumanBodyBones.Hips).position.y - player.transform.position.y : .38f;
            horse.saddlePoint=Child(root.transform,"SaddlePoint",new Vector3(0,1.35f-hipOffset,-.05f));
            var saddleBone = model.GetComponentsInChildren<Transform>(true).Single(t => t.name == "spine_3");
            horse.saddlePoint.SetParent(saddleBone, true); // Preserve the rider offset while following animated torso motion.
            horse.dismountPoint=Child(root.transform,"DismountPoint",new Vector3(-1.7f,0,0));
            var interaction=root.AddComponent<HorseInteractionSystem>(); interaction.horse=horse;
            BuildPrompt(interaction);
            // Save a reusable horse without a scene-only player reference.
            PrefabUtility.SaveAsPrefabAssetAndConnect(root,"Assets/Prefabs/Horse/RideableHorse.prefab",InteractionMode.AutomatedAction);
        }
        var prompt=horse.GetComponent<HorseInteractionSystem>(); prompt.player=rider;
        EditorUtility.SetDirty(prompt); PrefabUtility.RecordPrefabInstancePropertyModifications(prompt);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        Selection.activeGameObject=horse.saddlePoint.gameObject;
        Debug.Log("Horse riding configured: SaddlePoint, DismountPoint, existing Special Attack input, paired placeholder Animators.");
    }
    static Transform Child(Transform root,string name,Vector3 position)
    {
        var t=new GameObject(name).transform; t.SetParent(root,false); t.localPosition=position; return t;
    }
    static void BuildPrompt(HorseInteractionSystem interaction)
    {
        var go=new GameObject("Horse Interaction Prompt",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));
        go.transform.SetParent(interaction.transform,false); go.transform.localPosition=interaction.promptOffset; go.transform.localScale=Vector3.one*.012f;
        var canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.sortingOrder=30;
        var rect=(RectTransform)go.transform; rect.sizeDelta=new Vector2(180,56);
        var bg=new GameObject("Interact",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));
        bg.transform.SetParent(go.transform,false); var br=(RectTransform)bg.transform; br.anchorMin=Vector2.zero;br.anchorMax=Vector2.one;br.offsetMin=br.offsetMax=Vector2.zero;
        var image=bg.GetComponent<UnityEngine.UI.Image>(); image.color=new Color(.05f,.09f,.12f,.95f);
        var button=bg.GetComponent<UnityEngine.UI.Button>(); button.targetGraphic=image;
        button.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
        var label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI)); label.transform.SetParent(bg.transform,false);
        var lr=(RectTransform)label.transform;lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
        var text=label.GetComponent<TextMeshProUGUI>();text.text="Mount";text.fontSize=28;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
        interaction.promptCanvas=canvas; interaction.promptButton=button;interaction.promptText=text;
        go.SetActive(false);
    }
    static bool HasHorseAnimation(HorseMotion motion) => motion <= HorseMotion.Death2;
    static bool HasPlayerAnimation(HorseMotion motion) => motion != HorseMotion.Death1 && motion != HorseMotion.Death2;

    static AnimatorController MakeController(HorseAnimationSet set,bool player)
    {
        var controller=AnimatorController.CreateAnimatorControllerAtPath((player ? PlayerFolder : Folder)+(player?"/MountedPlayer.controller":"/Horse.controller"));
        controller.AddParameter(set.timeParameter,AnimatorControllerParameterType.Float);
        var machine=controller.layers[0].stateMachine;
        foreach(var a in set.animations)
        {
            if (player ? !HasPlayerAnimation(a.motion) : !HasHorseAnimation(a.motion)) continue;
            string stateName=player?"PlayerHorse"+a.motion:a.motion.ToString();
            var state=machine.AddState(stateName,new Vector3(((int)a.motion%4)*240,((int)a.motion/4)*80));
            state.motion=player?a.playerClip:a.horseClip;state.timeParameter=set.timeParameter;state.timeParameterActive=true;
            state.writeDefaultValues=true;
            if(a.motion==HorseMotion.Idle) machine.defaultState=state;
        }
        // Runtime uses one shared clock and CrossFadeInFixedTime. Death1 has no outgoing
        // graph interruption; HorseController switches only after the full configured duration.
        EditorUtility.SetDirty(controller); return controller;
    }
    static AnimationClip MakeHorseClip(HorseMotion motion,float duration,Transform model)
    {
        var clip=new AnimationClip{name="Horse_"+motion+"_Placeholder",frameRate=30};
        bool gait=motion>=HorseMotion.Walk && motion<=HorseMotion.Run;
        foreach(var bone in model.GetComponentsInChildren<Transform>())
        {
            bool leg=bone.name.StartsWith("back_leg_upper")||bone.name.StartsWith("front_humerus");
            bool head=bone.name=="head";
            if(!leg&&!head)continue;
            string path=AnimationUtility.CalculateTransformPath(bone,model);
            Vector3 basis=bone.localEulerAngles;
            float amplitude=leg?(gait?(motion==HorseMotion.Run?25:12):0):2;
            if(motion==HorseMotion.SpecialAttack)amplitude=leg?35:15;
            float sign=bone.name.EndsWith("_l")?1:-1;
            var curve=new AnimationCurve();
            for(int i=0;i<=8;i++)curve.AddKey(duration*i/8,basis.z+Mathf.Sin(i*Mathf.PI/4)*amplitude*sign);
            clip.SetCurve(path,typeof(Transform),"localEulerAnglesRaw.z",curve);
        }
        // Root visual tilt is confined to the model. Motor and saddle anchors stay stable.
        if(motion==HorseMotion.Death1||motion==HorseMotion.Death2)
            clip.SetCurve("",typeof(Transform),"localEulerAnglesRaw.z",AnimationCurve.EaseInOut(0,motion==HorseMotion.Death1?0:80,duration,80));
        SaveClip(clip,motion);return clip;
    }
    static AnimationClip MakePlayerClip(HorseMotion motion,float duration)
    {
        var clip=new AnimationClip{name="Player_"+motion+"_Placeholder",frameRate=30};
        foreach(string muscle in HumanTrait.MuscleName)
        {
            float value=0;
            if(muscle.Contains("Upper Leg Front-Back"))value=.65f;
            if(muscle.Contains("Upper Leg In-Out"))value=.3f;
            if(muscle.Contains("Lower Leg Stretch"))value=-.5f;
            if(muscle.Contains("Arm Down-Up"))value=-.65f;
            if(muscle.Contains("Forearm Stretch"))value=-.4f;
            if(muscle.Contains("Spine Front-Back"))value=motion==HorseMotion.Run?.15f:0;
            if(motion==HorseMotion.Ejected && muscle.Contains("Arm Down-Up"))value=.3f;
            if(motion==HorseMotion.SpecialAttack && muscle=="Right Arm Down-Up")value=.7f;
            float start=value,end=value;
            if(motion==HorseMotion.Mount)start=0;
            if(motion==HorseMotion.Dismount||motion==HorseMotion.Ejected)end=0;
            clip.SetCurve("",typeof(Animator),muscle,AnimationCurve.EaseInOut(0,start,duration,end));
        }
        SaveClip(clip,motion);return clip;
    }
    static void SaveClip(AnimationClip clip,HorseMotion motion)
    {
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=HorseAnimationSet.Loops(motion);
        AnimationUtility.SetAnimationClipSettings(clip,settings);
        AssetDatabase.CreateAsset(clip,(clip.name.StartsWith("Player_") ? PlayerFolder : Folder)+"/"+clip.name+".anim");
    }
}
