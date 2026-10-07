using UnityEngine;

public enum PlayerActionState { Normal, Attack }
public enum PlayerAttackSubstate { None, Sword, Magic, Bow, Parry }

public partial class PlayerStateManager
{
    [Header("State Logging")]
    [Tooltip("Log when the Animator enters Normal or an Attack substate. Does not log every frame.")]
    public bool logStateChanges = true;

    // These describe the actual entered Animator state. CombatMode remains the
    // existing selection API; Sword/Magic/Bow are children of Attack, not peers of Normal.
    public PlayerActionState ActionState { get; private set; } = PlayerActionState.Normal;
    public PlayerAttackSubstate AttackSubstate { get; private set; } = PlayerAttackSubstate.None;
    public string ActiveStatePath { get; private set; }

    public void NotifyAnimatorStateEntered(PlayerCombatMode mode, bool parry)
    {
        EquipmentPhase = PlayerEquipmentPhase.None;
        if (!parry) NotifyEquipmentSettled(mode);
        ActionState = mode == PlayerCombatMode.Normal && !parry
            ? PlayerActionState.Normal : PlayerActionState.Attack;
        AttackSubstate = parry ? PlayerAttackSubstate.Parry : mode switch
        {
            PlayerCombatMode.Sword => PlayerAttackSubstate.Sword,
            PlayerCombatMode.Magic => PlayerAttackSubstate.Magic,
            PlayerCombatMode.Bow => PlayerAttackSubstate.Bow,
            _ => PlayerAttackSubstate.None
        };
        string path = ActionState == PlayerActionState.Normal ? "Normal"
            : parry ? $"Attack > Parry (return: {mode})" : $"Attack > {AttackSubstate}";
        SetActiveStatePath(path);
    }

    private void SetActiveStatePath(string path)
    {
        if (path == ActiveStatePath) return;
        ActiveStatePath = path;
        if (logStateChanges) Debug.Log($"[Player State] {name}: {path}", this);
    }
}
