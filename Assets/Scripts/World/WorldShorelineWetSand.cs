using UnityEngine;

namespace Nusantara.World
{
    // Keep the wet beach and the sea on precisely the same wash cycle in edit and play mode.
    [ExecuteAlways, RequireComponent(typeof(MeshRenderer))]
    public sealed class WorldShorelineWetSand : MonoBehaviour
    {
        public Material seaMaterial;
        MaterialPropertyBlock properties;
        Renderer target;
        void OnEnable() { target = GetComponent<Renderer>(); Apply(); }
        void LateUpdate() => Apply();
        void OnValidate() => Apply();
        void Apply()
        {
            if (!seaMaterial) return;
            if (!target) target = GetComponent<Renderer>();
            if (!target) return;
            properties ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(properties);
            properties.SetTexture("_ShoreMap", seaMaterial.GetTexture("_ShoreMap"));
            properties.SetVector("_ShoreRect", seaMaterial.GetVector("_ShoreRect"));
            properties.SetFloat("_ShoreEnabled", seaMaterial.GetFloat("_ShoreEnabled"));
            properties.SetFloat("_ShoreLevel", seaMaterial.GetFloat("_ShoreLevel"));
            properties.SetFloat("_ShoreAmplitude", seaMaterial.GetFloat("_ShoreAmplitude"));
            properties.SetFloat("_ShorePeriod", seaMaterial.GetFloat("_ShorePeriod"));
            target.SetPropertyBlock(properties);
        }
    }
}
