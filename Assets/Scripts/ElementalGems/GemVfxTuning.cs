using UnityEngine;
namespace ElementalGems
{
    /// <summary>Applies only visual settings; never reads or writes combat values.</summary>
    public sealed class GemVfxTuning : MonoBehaviour
    {
        public GemVfxStyle style;
        private void Awake()
        {
            if(style==null)return;
            transform.localScale*=style.size;
            foreach(var p in GetComponentsInChildren<ParticleSystem>(true))
            {
                var m=p.main;m.simulationSpeed*=style.speed;m.startLifetimeMultiplier*=style.duration;
                m.startSizeMultiplier*=Mathf.Sqrt(style.intensity);
                var e=p.emission;e.rateOverTimeMultiplier*=style.intensity;
            }
        }
    }
}
