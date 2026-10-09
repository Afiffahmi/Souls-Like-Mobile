using UnityEngine;
namespace ElementalGems
{
    /// <summary>Visual-only observer of the projectile's immutable element and flight state.</summary>
    [DefaultExecutionOrder(220)]
    public sealed class GemArrowVisuals : MonoBehaviour
    {
        public GemVfxStyle[] styles;
        private BowArrowProjectile arrow;
        private GemArrowPayload payload;
        private GameObject aura;
        private bool initialized, stopped;
        private void Awake(){arrow=GetComponent<BowArrowProjectile>();}
        private void LateUpdate()
        {
            if(!initialized)
            {
                payload=GetComponent<GemArrowPayload>();
                if(payload==null||payload.Attack==null)return;
                initialized=true;
                var style=System.Array.Find(styles,s=>s!=null&&s.element==payload.Element);
                if(style==null||style.element==ElementType.Normal||!arrow.IsFlying)return;
                if(style.arrowheadAura!=null)
                {
                    aura=Instantiate(style.arrowheadAura,transform,false);
                    aura.transform.localPosition=Vector3.forward*arrow.tipOffset;
                    var s=transform.lossyScale;
                    aura.transform.localScale=new Vector3(1/Mathf.Max(.001f,Mathf.Abs(s.x)),1/Mathf.Max(.001f,Mathf.Abs(s.y)),1/Mathf.Max(.001f,Mathf.Abs(s.z)))*style.size;
                }
            }
            if(!stopped&&!arrow.IsFlying)
            {
                stopped=true;
                if(aura!=null){aura.SetActive(false);Destroy(aura);}
            }
        }
        private void OnDisable(){if(aura!=null)aura.SetActive(false);}
    }
}
