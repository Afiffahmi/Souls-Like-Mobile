using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Small geometry mask: only reveal-capable props in front of the player's torso count.</summary>
internal sealed class CharacterRevealOcclusion : System.IDisposable
{
    const int Resolution = 16;
    static readonly int ProbeMatrix = Shader.PropertyToID("_CharacterRevealProbeVP");
    static readonly int ProbeCenter = Shader.PropertyToID("_CharacterRevealProbeCenter");
    static readonly int ProbeForward = Shader.PropertyToID("_CharacterRevealProbeForward");
    readonly List<Material> materials = new List<Material>();
    readonly Plane[] planes = new Plane[6];
    Renderer[] renderers;
    RenderTexture mask;
    Texture2D readback;
    CommandBuffer commands;
    float nextScan, nextProbe;
    bool pending, blocked, disposed;
    int revision;

    public bool Evaluate(Camera camera, Transform target, Vector3 center, CharacterController controller)
    {
        if (disposed) return false;
        if (pending || Time.realtimeSinceStartup < nextProbe) return blocked;
        nextProbe = Time.realtimeSinceStartup + 0.05f;
        if (renderers == null || Time.realtimeSinceStartup >= nextScan)
        {
            renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            nextScan = Time.realtimeSinceStartup + 0.5f;
        }
        Vector3 probeCenter = Vector3.Lerp(target.position, center, 0.6f);
        float halfWidth = controller ? Mathf.Max(0.15f, controller.bounds.extents.x * 0.65f) : 0.2f;
        float halfHeight = controller ? Mathf.Max(0.25f, controller.bounds.extents.y * 0.55f) : 0.35f;
        Vector3 vp = camera.WorldToViewportPoint(probeCenter);
        if (vp.z <= camera.nearClipPlane) return blocked = false;
        float width = Mathf.Abs(camera.WorldToViewportPoint(probeCenter + camera.transform.right * halfWidth).x - vp.x) * 2f;
        float height = Mathf.Abs(camera.WorldToViewportPoint(probeCenter + camera.transform.up * halfHeight).y - vp.y) * 2f;
        Matrix4x4 crop = Matrix4x4.identity;
        crop.m00 = 1f / Mathf.Max(width, 0.0001f);
        crop.m11 = 1f / Mathf.Max(height, 0.0001f);
        crop.m03 = -(vp.x * 2f - 1f) * crop.m00;
        crop.m13 = -(vp.y * 2f - 1f) * crop.m11;
        Matrix4x4 projection = crop * camera.projectionMatrix;
        GeometryUtility.CalculateFrustumPlanes(projection * camera.worldToCameraMatrix, planes);
        if (!mask)
        {
            mask = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                { name = "Character reveal occlusion", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            mask.Create();
            commands = new CommandBuffer { name = "Check player occlusion" };
        }
        commands.Clear();
        commands.SetRenderTarget(mask);
        commands.ClearRenderTarget(false, true, Color.clear);
        commands.SetGlobalMatrix(ProbeMatrix, GL.GetGPUProjectionMatrix(projection, true) * camera.worldToCameraMatrix);
        commands.SetGlobalVector(ProbeCenter, probeCenter);
        commands.SetGlobalVector(ProbeForward, new Vector4(camera.transform.forward.x, camera.transform.forward.y, camera.transform.forward.z, 0.15f));
        int draws = 0;
        foreach (var renderer in renderers)
        {
            if (!renderer || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy ||
                renderer.transform.IsChildOf(target) || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0 ||
                !GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) continue;
            // Bounds are just the broad phase. The mask tests actual triangles, not the empty space in a house's bounds.
            renderer.GetSharedMaterials(materials);
            for (int i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
                if (!material) continue;
                int pass = material.FindPass("CharacterRevealProbe");
                if (pass < 0) continue; // Trees and all other excluded shaders never activate reveal.
                commands.DrawRenderer(renderer, material, i, pass);
                draws++;
            }
        }
        if (draws == 0) { revision++; return blocked = false; }
        // Restore the target after this offscreen probe; the main camera owns its normal render state.
        commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        Graphics.ExecuteCommandBuffer(commands);
        if (SystemInfo.supportsAsyncGPUReadback)
        {
            pending = true;
            int requestedRevision = ++revision;
            AsyncGPUReadback.Request(mask, 0, TextureFormat.RGBA32, request =>
            {
                pending = false;
                if (disposed) { ReleaseMask(); return; }
                if (requestedRevision != revision) return;
                if (request.hasError) { blocked = false; return; }
                var pixels = request.GetData<Color32>();
                int covered = 0;
                for (int i = 0; i < pixels.Length; i++) if (pixels[i].r > 127) covered++;
                blocked = covered >= (blocked ? 16 : 32);
            });
        }
        else
        {
            if (!readback) readback = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true)
                { hideFlags = HideFlags.HideAndDontSave };
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = mask;
                readback.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
                readback.Apply();
                int covered = 0;
                foreach (var pixel in readback.GetPixels32()) if (pixel.r > 127) covered++;
                blocked = covered >= (blocked ? 16 : 32);
            }
            finally { RenderTexture.active = previous; }
        }
        return blocked;
    }

    public void Dispose()
    {
        disposed = true;
        commands?.Release();
        commands = null;
        if (readback) Object.DestroyImmediate(readback);
        if (!pending) ReleaseMask(); // The GPU must finish before its readback texture is released.
    }

    void ReleaseMask()
    {
        if (!mask) return;
        mask.Release();
        Object.DestroyImmediate(mask);
        mask = null;
    }
}
