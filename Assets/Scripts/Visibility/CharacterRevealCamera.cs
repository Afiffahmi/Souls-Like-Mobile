using UnityEngine;

/// <summary>Camera-scoped circular cutout for Nusantara reveal materials.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera))]
[AddComponentMenu("Nusantara/Visibility/Character Reveal Camera")]
public sealed class CharacterRevealCamera : MonoBehaviour
{
    public Transform target;
    [Tooltip("Radius in world units for the isometric camera.")]
    [Min(0.1f)] public float radius = 2.8f;
    [Min(0f)] public float feather = 0.25f;
    public Vector3 focusOffset = new Vector3(0f, 0.9f, 0f);
    [Tooltip("Keep surfaces at the player's feet visible.")]
    public float groundClearance = 0.12f;
    [Tooltip("Small depth margin at the far end of the reveal volume.")]
    [Min(0f)] public float depthBias = 0.15f;
    [Tooltip("Clear nearby back walls and inner leaf layers this far behind the player. Distant scenery stays visible.")]
    [Min(0f)] public float revealBehindPlayer = 12f;
    [Tooltip("Remove leaf-pattern shadows in the opening while retaining the character's own shadow.")]
    public bool clearFoliageShadows = true;
    public bool previewInEditMode = true;

    static readonly int Active = Shader.PropertyToID("_CharacterRevealActive");
    static readonly int Center = Shader.PropertyToID("_CharacterRevealCenter");
    static readonly int Right = Shader.PropertyToID("_CharacterRevealRight");
    static readonly int Up = Shader.PropertyToID("_CharacterRevealUp");
    static readonly int Forward = Shader.PropertyToID("_CharacterRevealForward");
    static readonly int Settings = Shader.PropertyToID("_CharacterRevealSettings");
    static readonly int CameraPosition = Shader.PropertyToID("_CharacterRevealCameraPosition");
    static readonly int RearDepth = Shader.PropertyToID("_CharacterRevealRearDepth");
    static readonly int ShadowDirection = Shader.PropertyToID("_CharacterRevealShadowDirection");
    static readonly int GroundHeight = Shader.PropertyToID("_CharacterRevealGroundHeight");
    Camera view;
    CharacterRevealOcclusion occlusion;
    CharacterController targetController;
    Transform cachedTarget;
    readonly RaycastHit[] groundHits = new RaycastHit[24];

    void OnEnable()
    {
        view = GetComponent<Camera>();
        occlusion = new CharacterRevealOcclusion();
        Camera.onPreCull += BeforeCamera;
        Camera.onPostRender += AfterCamera;
    }

    void OnDisable()
    {
        occlusion?.Dispose();
        occlusion = null;
        Camera.onPreCull -= BeforeCamera;
        Camera.onPostRender -= AfterCamera;
        Shader.SetGlobalFloat(Active, 0f);
    }

    void BeforeCamera(Camera camera)
    {
        // Scene view, previews, reflections and other cameras must keep solid props.
        if (camera != view) { Shader.SetGlobalFloat(Active, 0f); return; }
        if (!target)
        {
            var rig = FindFirstObjectByType<IsometricPlayerCamera>();
            if (rig) target = rig.player;
        }
        if (!target || (!Application.isPlaying && !previewInEditMode))
        {
            Shader.SetGlobalFloat(Active, 0f);
            return;
        }
        Vector3 center = target.position + focusOffset;
        if (cachedTarget != target)
        {
            occlusion?.Dispose();
            occlusion = new CharacterRevealOcclusion();
            cachedTarget = target;
            targetController = target.GetComponent<CharacterController>();
        }
        float feet = targetController && targetController.enabled
            ? targetController.bounds.min.y : target.position.y;
        float ground = feet;
        float nearest = float.PositiveInfinity;
        int hitCount = Physics.RaycastNonAlloc(target.position + Vector3.up * 0.1f,
            Vector3.down, groundHits, 50f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            var hit = groundHits[i];
            if (hit.transform.IsChildOf(target) || hit.normal.y < 0.65f || hit.distance >= nearest) continue;
            nearest = hit.distance;
            ground = hit.point.y;
        }
        float depth = Vector3.Dot(center - camera.transform.position, camera.transform.forward);
        Shader.SetGlobalVector(Center, center);
        Shader.SetGlobalVector(Right, camera.transform.right);
        Shader.SetGlobalVector(Up, camera.transform.up);
        Shader.SetGlobalVector(Forward, camera.transform.forward);
        Shader.SetGlobalVector(CameraPosition, new Vector4(camera.transform.position.x,
            camera.transform.position.y, camera.transform.position.z, camera.orthographic ? 0f : depth));
        Shader.SetGlobalVector(Settings, new Vector4(Mathf.Max(0.1f, radius),
            Mathf.Clamp(feather, 0f, radius), feet + groundClearance, depthBias));
        Shader.SetGlobalFloat(RearDepth, Mathf.Max(0f, revealBehindPlayer));
        var sun = RenderSettings.sun;
        Vector3 lightTravel = sun ? sun.transform.forward : Vector3.down;
        Shader.SetGlobalVector(ShadowDirection, new Vector4(lightTravel.x, lightTravel.y, lightTravel.z,
            clearFoliageShadows && sun && sun.enabled && sun.intensity > 0f ? 1f : 0f));
        Shader.SetGlobalFloat(GroundHeight, ground);
        Shader.SetGlobalFloat(Active, depth > camera.nearClipPlane &&
            occlusion.Evaluate(camera, target, center, targetController) ? 1f : 0f);
    }

    void AfterCamera(Camera camera) => Shader.SetGlobalFloat(Active, 0f);
}
