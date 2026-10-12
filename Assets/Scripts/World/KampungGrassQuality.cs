using UnityEngine;
namespace Nusantara.World
{
    [RequireComponent(typeof(Renderer))]
    public sealed class KampungGrassQuality:MonoBehaviour
    {
        void OnEnable()
        {
            var block=new MaterialPropertyBlock();int q=QualitySettings.GetQualityLevel();
            block.SetFloat("_Density",q<=1?.45f:q<=3?.7f:1);
            block.SetFloat("_FadeEnd",q<=1?48:76);GetComponent<Renderer>().SetPropertyBlock(block);
        }
    }
}
