using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class ShaderHandler : MonoBehaviour
{
    [Tooltip("Pixel size, colour grading, shading bands and sunlight controls are on this material. Overlay UI stays crisp.")]
    public Material effectMaterial;

    private Light styledSun;
    private LightShadows originalShadows;
    private float originalStrength;
    private Texture originalCookie;
    private float originalCookieSize;
    private RenderTexture canopyCookie;
    private float nextCookieUpdate;
    private Camera effectCamera;

    private static readonly int StylizedSun = Shader.PropertyToID("_StylizedSun");
    private static readonly int ShadowStrength = Shader.PropertyToID("_ShadowStrength");
    private static readonly int CanopyStrength = Shader.PropertyToID("_CanopyStrength");
    private static readonly int CanopySize = Shader.PropertyToID("_CanopySize");
    private static readonly int NeonTexture = Shader.PropertyToID("_NeonTex");
    private static readonly int NeonStrength = Shader.PropertyToID("_NeonStrength");
    private static readonly int GlowDirection = Shader.PropertyToID("_GlowDirection");

    private void LateUpdate()
    {
        // Camera-local colour grading is safe in edit mode. Shared sunlight is only
        // temporarily styled during gameplay; never save runtime lighting into a scene.
        if (!Application.isPlaying || effectMaterial == null ||
            !effectMaterial.HasProperty(StylizedSun) || effectMaterial.GetFloat(StylizedSun) < 0.5f)
        {
            RestoreSun();
            return;
        }

        // Only the gameplay camera owns the shared light, not minimap/preview cameras.
        if (effectCamera == null) effectCamera = GetComponent<Camera>();
        if (Camera.main != effectCamera)
        {
            RestoreSun();
            return;
        }

        var sun = RenderSettings.sun;
        if (sun == null || !sun.isActiveAndEnabled || sun.type != LightType.Directional)
        {
            RestoreSun();
            return;
        }

        if (styledSun != sun)
        {
            RestoreSun();
            styledSun = sun;
            originalShadows = sun.shadows;
            originalStrength = sun.shadowStrength;
            originalCookie = sun.cookie;
            originalCookieSize = sun.cookieSize;
        }

        // Real shadow maps still follow characters, terrain and the sun direction.
        sun.shadows = LightShadows.Hard;
        sun.shadowStrength = Mathf.Clamp01(effectMaterial.GetFloat(ShadowStrength));

        // Respect authored light cookies. The optional canopy modulates direct light,
        // so it crosses objects and terrain together and never darkens overlay text.
        // The world atmosphere owns travelling clouds. Keep camera grading and
        // crisp shadows, without installing a second repeating cloud cookie.
        var worldClouds = sun.GetComponent<Nusantara.World.WorldCloudShadows>();
        bool useCanopy = (worldClouds == null || !worldClouds.isActiveAndEnabled) &&
            originalCookie == null && effectMaterial.passCount > 1 &&
            effectMaterial.GetFloat(CanopyStrength) > 0.001f;
        if (useCanopy)
        {
            if (canopyCookie == null)
            {
                canopyCookie = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear)
                {
                    name = "Pixel Art Canopy (Runtime)",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat,
                    hideFlags = HideFlags.HideAndDontSave
                };
                canopyCookie.Create();
                nextCookieUpdate = 0f;
            }
            // The slow canopy needs only twelve tiny updates per second on mobile.
            if (Time.time >= nextCookieUpdate)
            {
                Graphics.Blit(null, canopyCookie, effectMaterial, 1);
                nextCookieUpdate = Time.time + 1f / 12f;
            }
            sun.cookieSize = Mathf.Max(1f, effectMaterial.GetFloat(CanopySize));
            sun.cookie = canopyCookie;
        }
        else if (canopyCookie != null)
        {
            sun.cookie = originalCookie;
            sun.cookieSize = originalCookieSize;
            ReleaseCookie();
        }
    }

    private void OnDisable() => RestoreSun();
    private void OnDestroy() => RestoreSun();

    private void RestoreSun()
    {
        if (styledSun != null)
        {
            styledSun.shadows = originalShadows;
            styledSun.shadowStrength = originalStrength;
            if (canopyCookie != null && styledSun.cookie == canopyCookie)
            {
                styledSun.cookie = originalCookie;
                styledSun.cookieSize = originalCookieSize;
            }
        }
        styledSun = null;
        ReleaseCookie();
    }

    private void ReleaseCookie()
    {
        if (canopyCookie == null) return;
        canopyCookie.Release();
        if (Application.isPlaying) Destroy(canopyCookie);
        else DestroyImmediate(canopyCookie);
        canopyCookie = null;
    }

    // Built-in pipeline image effect. Screen Space - Overlay UI renders afterwards.
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (effectMaterial == null || effectMaterial.shader == null || !effectMaterial.shader.isSupported)
        {
            Graphics.Blit(source, destination);
            return;
        }
        if (effectMaterial.passCount < 4 || !effectMaterial.HasProperty(NeonStrength) ||
            effectMaterial.GetFloat(NeonStrength) <= 0.001f)
        {
            effectMaterial.SetTexture(NeonTexture, Texture2D.blackTexture);
            Graphics.Blit(source, destination, effectMaterial, 0);
            return;
        }

        // Quarter-resolution glow keeps the scene's fine pixels and overlay text sharp.
        int width = Mathf.Max(1, source.width / 4);
        int height = Mathf.Max(1, source.height / 4);
        var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
            ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
        RenderTexture glow = null, scratch = null;
        try
        {
            glow = RenderTexture.GetTemporary(width, height, 0, format);
            scratch = RenderTexture.GetTemporary(width, height, 0, format);
            glow.filterMode = scratch.filterMode = FilterMode.Bilinear;
            glow.wrapMode = scratch.wrapMode = TextureWrapMode.Clamp;
            Graphics.Blit(source, glow, effectMaterial, 2);
            effectMaterial.SetVector(GlowDirection, new Vector4(1f, 0f, 0f, 0f));
            Graphics.Blit(glow, scratch, effectMaterial, 3);
            effectMaterial.SetVector(GlowDirection, new Vector4(0f, 1f, 0f, 0f));
            Graphics.Blit(scratch, glow, effectMaterial, 3);
            effectMaterial.SetTexture(NeonTexture, glow);
            Graphics.Blit(source, destination, effectMaterial, 0);
        }
        finally
        {
            effectMaterial.SetTexture(NeonTexture, Texture2D.blackTexture);
            if (scratch != null) RenderTexture.ReleaseTemporary(scratch);
            if (glow != null) RenderTexture.ReleaseTemporary(glow);
        }
    }
}
