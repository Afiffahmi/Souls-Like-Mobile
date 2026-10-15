using UnityEngine;
namespace Nusantara.World
{
    [ExecuteAlways, RequireComponent(typeof(Renderer))]
    public sealed class KampungGrassQuality:MonoBehaviour
    {
        void OnEnable()
        {
            var renderer=GetComponent<Renderer>();
            var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
            int q=QualitySettings.GetQualityLevel();
            block.SetFloat("_Density",q<=1?.55f:q<=3?.8f:1);
            block.SetFloat("_FadeStart",q<=1?30:42);
            block.SetFloat("_FadeEnd",q<=1?48:76);renderer.SetPropertyBlock(block);
        }
    }
}
