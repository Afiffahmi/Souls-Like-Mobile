using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Routes touch/mouse press and release directly to the shared light-attack API.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.UI.Button))]
public sealed class PlayerCanvasAttackButton : MonoBehaviour, IPointerDownHandler,
    IPointerUpHandler, IPointerExitHandler, ICancelHandler
{
    [SerializeField] private PlayerStateManager player;
    private UnityEngine.UI.Button button;
    private bool held;
    private int pointerId;

    private void Awake() => button = GetComponent<UnityEngine.UI.Button>();

    public void OnPointerDown(PointerEventData data)
    {
        if (held || data.button != PointerEventData.InputButton.Left || player == null ||
            !player.isActiveAndEnabled || !isActiveAndEnabled || !button.IsInteractable()) return;
        held = true;
        pointerId = data.pointerId;
        player.BeginLightAttackHold();
    }

    public void OnPointerUp(PointerEventData data)
    {
        if (held && data.pointerId == pointerId) Release();
    }

    public void OnPointerExit(PointerEventData data)
    {
        if (held && data.pointerId == pointerId) Release();
    }

    public void OnCancel(BaseEventData data) => Release();
    private void OnDisable() => Release();
    private void OnApplicationFocus(bool focused) { if (!focused) Release(); }
    private void OnApplicationPause(bool paused) { if (paused) Release(); }

    private void Release()
    {
        if (!held) return;
        held = false;
        if (player != null) player.EndLightAttackHold();
    }
}
