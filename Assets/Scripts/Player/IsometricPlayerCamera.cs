using UnityEngine;
using Unity.Cinemachine;

/// <summary>Player-following overhead camera with a fixed angle, distance, and zoom.</summary>
[ExecuteAlways]
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera))]
public sealed class IsometricPlayerCamera : MonoBehaviour
{
    public Transform player;

    [Header("Fixed isometric view")]
    [Range(20f, 85f)] public float elevation = 55f;
    public float yaw = 45f;
    [Min(10f)] public float distance = 30f;
    [Min(2f)] public float orthographicSize = 8f;
    public Vector3 playerFocusOffset = new Vector3(0f, 0.25f, 0f);

    [Header("Player following")]
    [Min(0.1f)] public float followDamping = 8f;

    private CinemachineCamera cameraRig;
    private Vector3 focus;
    private bool initialized;

    private void OnEnable()
    {
        cameraRig = GetComponent<CinemachineCamera>();
        initialized = false;
        UpdateView(0f);
    }

    private void LateUpdate() => UpdateView(Application.isPlaying ? Time.deltaTime : 0f);

    private void UpdateView(float dt)
    {
        if (player == null || cameraRig == null) return;
        Vector3 desiredFocus = player.position + playerFocusOffset;
        bool snap = !initialized || !Application.isPlaying;
        focus = snap ? desiredFocus : Vector3.Lerp(focus, desiredFocus, 1f - Mathf.Exp(-followDamping * dt));

        // Lock-on never changes this camera's focus, angle, depth, or zoom.
        Quaternion rotation = Quaternion.Euler(elevation, yaw, 0f);
        cameraRig.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        cameraRig.Lens.OrthographicSize = orthographicSize;
        cameraRig.Lens.NearClipPlane = 0.1f;
        cameraRig.Lens.FarClipPlane = Mathf.Max(1000f, distance + 100f);
        transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        initialized = true;
    }
}
