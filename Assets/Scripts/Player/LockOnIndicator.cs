using UnityEngine;

/// <summary>Projects the target into a replaceable, non-interactive Canvas visual.</summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerLockOn))]
public sealed class LockOnIndicator : MonoBehaviour
{
    public RectTransform visual;
    public Vector3 worldOffset = new Vector3(0f, 1f, 0f);
    private PlayerLockOn lockOn;
    private Canvas canvas;

    private void Awake() => lockOn = GetComponent<PlayerLockOn>();

    private void LateUpdate()
    {
        if (visual == null) return;
        Camera view = lockOn.viewCamera;
        bool visible = lockOn.IsLockedOn && view != null;
        Vector3 screen = visible ? view.WorldToScreenPoint(lockOn.CurrentTarget.AimPosition + worldOffset) : Vector3.zero;
        visible &= screen.z > 0f && view.pixelRect.Contains(new Vector2(screen.x, screen.y));
        visual.gameObject.SetActive(visible);
        if (!visible) return;
        if (canvas == null) canvas = visual.GetComponentInParent<Canvas>();
        var parent = visual.parent as RectTransform;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out Vector2 local))
            visual.localPosition = new Vector3(local.x, local.y, 0f);
    }

    private void OnDisable()
    {
        if (visual != null) visual.gameObject.SetActive(false);
    }
}
