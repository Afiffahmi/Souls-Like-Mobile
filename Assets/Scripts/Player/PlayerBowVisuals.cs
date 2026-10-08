using UnityEngine;

/// <summary>Animated bow with live placement offsets relative to the active socket.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStateManager))]
public sealed class PlayerBowVisuals : MonoBehaviour
{
    public GameObject bowPrefab;
    public AnimationClip drawAnimation;
    public AnimationClip holdAnimation;
    public AnimationClip releaseAnimation;
    [Header("Optional sockets (created on the humanoid rig when empty)")]
    public Transform handSocket;
    public Transform backSocket;
    [Header("Live bow offsets (relative to each socket)")]
    [Tooltip("Adjust these fields during Play Mode. Use Keep Bow Adjustments After Play Mode in this component menu to retain them.")]
    public Vector3 handPosition = new Vector3(0f, 0.08f, 0f);
    public Vector3 handRotation = new Vector3(0f, 0f, 90f);
    public Vector3 backPosition = new Vector3(0f, 0.15f, -0.2f);
    public Vector3 backRotation = new Vector3(0f, 0f, 35f);
    public Vector3 modelScale = Vector3.one;
    [Header("Existing equipment animation swap points")]
    [Range(0f, 1f)] public float equipSwapTime = 0.45f;
    [Range(0f, 1f)] public float unequipSwapTime = 0.55f;

    private static readonly int EquipState = Animator.StringToHash("Base Layer.Attack.Bow_Equip");
    private static readonly int UnequipState = Animator.StringToHash("Base Layer.Attack.Bow_Unequip");
    private PlayerStateManager player;
    private GameObject model;
    public Transform BowModel => model != null ? model.transform : null;

    private bool drawn;
    private Vector3 prefabScale;
    private Transform generatedHandSocket;
    private Transform generatedBackSocket;
    private AnimationClip sampledClip;
    private float sampledTime = -1f;

    private void Start()
    {
        player = GetComponent<PlayerStateManager>();
        if (bowPrefab == null || player.anim == null || !player.anim.isHuman)
        {
            Debug.LogWarning("[Bow] Assign a bow prefab and a humanoid player Animator.", this);
            enabled = false;
            return;
        }
        if (handSocket == null)
            handSocket = generatedHandSocket = CreateSocket("Bow Hand Socket", HumanBodyBones.LeftHand);
        if (backSocket == null)
            backSocket = generatedBackSocket = CreateSocket("Bow Back Socket", HumanBodyBones.Chest);
        model = Instantiate(bowPrefab, backSocket, false);
        model.name = "Bow Visual (Runtime)";
        prefabScale = model.transform.localScale;
        // Only the character timeline drives this model, not a second animation clock.
        foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var animation in model.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
        Refresh();
    }

    private Transform CreateSocket(string socketName, HumanBodyBones bone)
    {
        var socket = new GameObject(socketName).transform;
        socket.SetParent(player.anim.GetBoneTransform(bone) ?? player.anim.transform, false);
        return socket;
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (model == null || player == null || player.anim == null) return;
        var animator = player.anim;
        bool equipmentPose = animator.isInitialized &&
            ((animator.IsInTransition(0) && ApplyEquipmentPose(animator.GetNextAnimatorStateInfo(0))) ||
             ApplyEquipmentPose(animator.GetCurrentAnimatorStateInfo(0)));
        if (!equipmentPose && !player.IsChangingEquipment) SetDrawn(player.CombatMode == PlayerCombatMode.Bow);
        AnimationClip clip = drawAnimation;
        float progress = 0f;
        switch (player.BowPhase)
        {
            case BowAttackPhase.Draw:
                progress = Mathf.InverseLerp(BowAttackTimeline.DrawFrame, BowAttackTimeline.HoldFrame, player.BowAnimationFrame);
                break;
            case BowAttackPhase.Hold:
                clip = holdAnimation;
                progress = player.BowAnimationFrame - BowAttackTimeline.HoldFrame;
                break;
            case BowAttackPhase.Release:
            case BowAttackPhase.Finished:
                clip = releaseAnimation;
                progress = Mathf.InverseLerp(BowAttackTimeline.ReleaseFrame, player.BowLastFrame, player.BowAnimationFrame);
                break;
        }
        Sample(clip, progress);
        RefreshPlacement();
    }

    private bool ApplyEquipmentPose(AnimatorStateInfo state)
    {
        if (state.fullPathHash == EquipState) SetDrawn(state.normalizedTime >= equipSwapTime);
        else if (state.fullPathHash == UnequipState) SetDrawn(state.normalizedTime < unequipSwapTime);
        else return false;
        return true;
    }

    private void SetDrawn(bool value)
    {
        Transform socket = value ? handSocket : backSocket;
        if (drawn == value && model.transform.parent == socket) return;
        drawn = value;
        model.transform.SetParent(socket, false);
        RefreshPlacement();
    }

    /// <summary>Apply the currently selected socket's offsets without restarting or re-equipping.</summary>
    public void RefreshPlacement()
    {
        if (model == null) return;
        model.transform.SetLocalPositionAndRotation(
            drawn ? handPosition : backPosition,
            Quaternion.Euler(drawn ? handRotation : backRotation));
        // Start from the prefab scale each time so live edits never compound.
        model.transform.localScale = Vector3.Scale(prefabScale, modelScale);
    }

    private void Sample(AnimationClip clip, float progress)
    {
        if (clip == null) return;
        float time = Mathf.Clamp01(progress) * clip.length;
        if (sampledClip == clip && Mathf.Approximately(time, sampledTime)) return;
        Vector3 position = model.transform.localPosition;
        Quaternion rotation = model.transform.localRotation;
        Vector3 scale = model.transform.localScale;
        clip.SampleAnimation(model, time);
        // Imported root curves must not move the model away from its socket.
        model.transform.SetLocalPositionAndRotation(position, rotation);
        model.transform.localScale = scale;
        sampledClip = clip;
        sampledTime = time;
    }

    private void OnDisable()
    {
        if (model != null) model.SetActive(false);
    }

    private void OnEnable()
    {
        if (model != null) model.SetActive(true);
    }

    private void OnDestroy()
    {
        if (model != null) Destroy(model);
        if (generatedHandSocket != null) Destroy(generatedHandSocket.gameObject);
        if (generatedBackSocket != null) Destroy(generatedBackSocket.gameObject);
    }
}


