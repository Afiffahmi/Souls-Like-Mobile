using UnityEngine;

/// <summary>Swaps the hand sword and back sheath during the existing equipment animations.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStateManager))]
public sealed class PlayerSwordVisuals : MonoBehaviour
{
    [SerializeField] private PlayerStateManager player;
    [Header("Models")]
    [SerializeField] private GameObject swordWithoutSheath;
    [SerializeField] private GameObject swordWithSheath;
    [SerializeField] private GameObject sheath;
    [Header("Animation swap timing")]
    [Tooltip("Normalized time in Sword_Equip when the sword appears in the hand.")]
    [Range(0f, 1f)] [SerializeField] private float drawSwapTime = 0.45f;
    [Tooltip("Normalized time in Sword_Unequip when the sword returns to the back.")]
    [Range(0f, 1f)] [SerializeField] private float sheathSwapTime = 0.55f;

    private static readonly int EquipState = Animator.StringToHash("Base Layer.Attack.Sword_Equip");
    private static readonly int UnequipState = Animator.StringToHash("Base Layer.Attack.Sword_Unequip");

    public bool IsSwordDrawn { get; private set; }

    private void Awake()
    {
        if (player == null) player = GetComponent<PlayerStateManager>();
        if (swordWithoutSheath == null || swordWithSheath == null || sheath == null)
            Debug.LogWarning("[Sword] Assign the hand sword, sheathed sword, and empty sheath.", this);
    }

    private void OnEnable() => RefreshVisuals();
    private void LateUpdate() => RefreshVisuals();

    private void RefreshVisuals()
    {
        if (player == null) return;
        var animator = player.anim;
        if (animator != null && animator.isInitialized)
        {
            // Inspect the incoming state first so crossfades follow the new action.
            if (animator.IsInTransition(0) && ApplyEquipmentPose(animator.GetNextAnimatorStateInfo(0)))
                return;
            if (ApplyEquipmentPose(animator.GetCurrentAnimatorStateInfo(0))) return;
        }
        // CombatMode changes as soon as a request is made. Keep the previous
        // models until the draw/sheath animation reaches its swap point.
        if (!player.IsChangingEquipment)
            SetDrawn(player.CombatMode == PlayerCombatMode.Sword);
    }

    private bool ApplyEquipmentPose(AnimatorStateInfo state)
    {
        if (state.fullPathHash == EquipState)
        {
            SetDrawn(state.normalizedTime >= drawSwapTime);
            return true;
        }
        if (state.fullPathHash == UnequipState)
        {
            SetDrawn(state.normalizedTime < sheathSwapTime);
            return true;
        }
        return false;
    }

    private void SetDrawn(bool drawn)
    {
        IsSwordDrawn = drawn;
        SetVisible(swordWithoutSheath, drawn);
        SetVisible(sheath, drawn);
        SetVisible(swordWithSheath, !drawn);
    }

    private static void SetVisible(GameObject model, bool visible)
    {
        if (model != null && model.activeSelf != visible) model.SetActive(visible);
    }
}
