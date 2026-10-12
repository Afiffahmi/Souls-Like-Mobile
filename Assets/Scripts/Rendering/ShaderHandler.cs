using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class ShaderHandler : MonoBehaviour
{
    [Tooltip("Tune Pixel Size, Detail Preservation, Soft Cel Shading and Lighting Bands on this material.")]
    public Material effectMaterial;

    // Built-in pipeline image effect. Screen Space - Overlay UI renders afterwards.
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (effectMaterial == null || effectMaterial.shader == null || !effectMaterial.shader.isSupported)
        {
            Graphics.Blit(source, destination);
            return;
        }

        Graphics.Blit(source, destination, effectMaterial, 0);
    }
}
