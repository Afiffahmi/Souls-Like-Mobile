using UnityEngine;
using Unity.Cinemachine;

/// <summary>Frames both actors with a dedicated Cinemachine view; the normal camera resumes on release.</summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerLockOn))]
public sealed class PlayerLockOnCamera : MonoBehaviour
{
    [Tooltip("The existing exploration camera. Its targets and settings are preserved.")]
    public CinemachineVirtualCameraBase virtualCamera;
    [Range(25f, 80f)] public float fieldOfView = 50f;
    [Range(5f, 50f)] public float elevation = 18f;
    [Range(-45f, 45f)] public float shoulderAngle = 12f;
    [Min(2f)] public float minimumDistance = 6f;
    [Min(0.1f)] public float subjectRadius = 1.3f;
    [Range(0.5f, 0.95f)] public float screenFill = 0.75f;
    [Min(0.1f)] public float damping = 8f;
    public Vector3 playerFocusOffset = new Vector3(0f, 1f, 0f);

    private PlayerLockOn lockOn;
    private CinemachineCamera lockCamera;
    private CameraLook lookInput;
    private bool restoreLookInput;
    private bool cameraOwned;
    private Vector3 focus;
    private Quaternion viewRotation;
    private float distance;

    private void Awake()
    {
        lockOn = GetComponent<PlayerLockOn>();
        var cameraObject = new GameObject(name + " LockOn Camera");
        cameraObject.SetActive(false);
        lockCamera = cameraObject.AddComponent<CinemachineCamera>();
        lockCamera.Lens = LensSettings.Default;
        lockCamera.Lens.FieldOfView = fieldOfView;
        lockCamera.Lens.NearClipPlane = 0.1f;
    }

    private void OnEnable()
    {
        lockOn.TargetChanged += OnTargetChanged;
        OnTargetChanged(lockOn.CurrentTarget);
    }

    private void OnTargetChanged(LockOnTarget target)
    {
        if (target == null) { RestoreCamera(); return; }
        if (virtualCamera == null || lockOn.viewCamera == null) return;
        if (!cameraOwned)
        {
            lookInput = virtualCamera.GetComponent<CameraLook>();
            restoreLookInput = lookInput != null && lookInput.enabled;
            if (restoreLookInput) lookInput.enabled = false;
        }
        cameraOwned = true;
        lockCamera.OutputChannel = virtualCamera.OutputChannel;
        lockCamera.Priority = virtualCamera.Priority.Value + 10;
        UpdateFraming(0f, true);
        lockCamera.gameObject.SetActive(true);
    }

    private void LateUpdate()
    {
        if (!lockOn.IsLockedOn || virtualCamera == null || lockOn.viewCamera == null)
        { RestoreCamera(); return; }
        if (!cameraOwned) OnTargetChanged(lockOn.CurrentTarget);
        UpdateFraming(Time.deltaTime, false);
    }

    private void UpdateFraming(float dt, bool snap)
    {
        Vector3 playerPoint = transform.position + playerFocusOffset;
        Vector3 targetPoint = lockOn.CurrentTarget.AimPosition;
        Vector3 direction = Vector3.ProjectOnPlane(targetPoint - playerPoint, Vector3.up);
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
        Quaternion desiredRotation = Quaternion.LookRotation(direction, Vector3.up)
            * Quaternion.Euler(elevation, shoulderAngle, 0f);
        float blend = snap ? 1f : 1f - Mathf.Exp(-damping * dt);
        focus = Vector3.Lerp(focus, (playerPoint + targetPoint) * 0.5f, blend);
        viewRotation = Quaternion.Slerp(viewRotation, desiredRotation, blend);

        // Solve the distance using both subjects in camera space, including a
        // radius for their bodies. This also works in portrait aspect ratios.
        float tanY = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * screenFill;
        float tanX = tanY * Mathf.Max(0.1f, lockOn.viewCamera.aspect);
        Quaternion inverse = Quaternion.Inverse(viewRotation);
        float required = Mathf.Max(minimumDistance,
            FitDistance(inverse * (playerPoint - focus), tanX, tanY),
            FitDistance(inverse * (targetPoint - focus), tanX, tanY));
        // Expand immediately to avoid clipping an actor; ease back in.
        distance = snap ? required : Mathf.Max(required, Mathf.Lerp(distance, required, blend));
        lockCamera.Lens.FieldOfView = fieldOfView;
        lockCamera.transform.SetPositionAndRotation(focus - viewRotation * Vector3.forward * distance, viewRotation);
    }

    private float FitDistance(Vector3 point, float tanX, float tanY) => Mathf.Max(
        (Mathf.Abs(point.x) + subjectRadius) / tanX - point.z + subjectRadius,
        (Mathf.Abs(point.y) + subjectRadius) / tanY - point.z + subjectRadius);

    private void RestoreCamera()
    {
        if (lockCamera != null) lockCamera.gameObject.SetActive(false);
        if (cameraOwned && restoreLookInput && lookInput != null) lookInput.enabled = true;
        cameraOwned = false;
        restoreLookInput = false;
    }

    private void OnDisable()
    {
        if (lockOn != null) lockOn.TargetChanged -= OnTargetChanged;
        RestoreCamera();
    }

    private void OnDestroy()
    {
        RestoreCamera();
        if (lockCamera != null) Destroy(lockCamera.gameObject);
    }
}
