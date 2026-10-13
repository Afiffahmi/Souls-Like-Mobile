using UnityEngine;

namespace Nusantara.World
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Light)), DefaultExecutionOrder(150)]
    public sealed class WorldCloudShadows : MonoBehaviour
    {
        public Shader cloudShadowShader;
        public WorldDayNightCycle timeCycle;
        [Header("Moving cloud banks")]
        [Tooltip("World X/Z wind direction. The whole cloud bank moves along this direction.")]
        public Vector2 windDirection = new Vector2(1,.35f);
        [Min(0), Tooltip("Cloud travel speed in metres per second, independent of day/night length.")]
        public float windSpeed = 4;
        [Min(10)] public float cloudSize = 180;
        [Range(0,1)] public float cloudCoverage = .52f;
        [Range(0,.8f)] public float shadowStrength = .32f;
        [Min(20)] public float cloudAltitude = 500;
        [Min(128)] public float shadowArea = 2048;
        [Range(2,30)] public int updatesPerSecond = 12;
        [Tooltip("Enable only when cloud movement should continue while gameplay is paused.")]
        public bool useUnscaledTime;
        [Tooltip("Keep cloud shadow coverage around the gameplay camera as it travels through the world.")]
        public bool followCamera = true;
        Light sunlight;
        Material material;
        RenderTexture cookie;
        Texture originalCookie;
        Vector2 originalSize;
        Vector3 originalPosition;
        double driftSeconds;
        double nextUpdate;
        public Vector2 CloudOffset => OffsetAt(driftSeconds);
        public Vector2 OffsetAt(double seconds) => windDirection.sqrMagnitude > .0001f
            ? windDirection.normalized * (float)(seconds * Mathf.Max(0,windSpeed)) : Vector2.zero;

        void OnEnable()
        {
            sunlight=GetComponent<Light>(); originalCookie=sunlight.cookie; originalSize=sunlight.cookieSize2D;originalPosition=sunlight.transform.position;
            driftSeconds=0; nextUpdate=0;
        }
        void LateUpdate()
        {
            if(!Application.isPlaying)return;
            Advance(useUnscaledTime?Time.unscaledDeltaTime:Time.deltaTime);
        }
        public void Advance(double seconds)
        {
            driftSeconds+=System.Math.Max(0,seconds);
            UpdateShadows(false);
        }
        public void UpdateShadows(bool force)
        {
            if(sunlight==null)sunlight=GetComponent<Light>();
            var sky=timeCycle!=null?timeCycle.RuntimeSky:null;
            if(sky!=null)ApplyCloudSettings(sky);
            if(sunlight.type!=LightType.Directional || cloudShadowShader==null)return;
            // Respect an authored cookie; still allow the moving sky.
            if(originalCookie!=null)return;
            if(sunlight.intensity<.001f || sunlight.transform.forward.y>=-.02f)
            {
                if(sunlight.cookie==cookie)sunlight.cookie=null;
                return;
            }
            if(material==null)material=new Material(cloudShadowShader){hideFlags=HideFlags.HideAndDontSave};
            if(cookie==null)
            {
                cookie=new RenderTexture(256,256,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){
                    name="Travelling cloud shadows",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Repeat,hideFlags=HideFlags.HideAndDontSave};
                cookie.Create();
            }
            double now=Time.unscaledTimeAsDouble;
            if(force || now>=nextUpdate)
            {
                var view=Camera.main;
                if(followCamera && view!=null && view.gameObject.scene==gameObject.scene)
                    sunlight.transform.position=new Vector3(view.transform.position.x,originalPosition.y,view.transform.position.z);
                ApplyCloudSettings(material);
                material.SetVector("_CloudOrigin",sunlight.transform.position);
                material.SetVector("_CloudRight",sunlight.transform.right);
                material.SetVector("_CloudUp",sunlight.transform.up);
                material.SetVector("_CloudForward",sunlight.transform.forward);
                material.SetFloat("_CookieSpan",Mathf.Max(128,shadowArea));
                material.SetFloat("_CloudStrength",shadowStrength);
                Graphics.Blit(null,cookie,material);
                nextUpdate=now+1d/Mathf.Max(2,updatesPerSecond);
            }
            sunlight.cookieSize2D=Vector2.one*Mathf.Max(128,shadowArea);sunlight.cookie=cookie;
        }
        void ApplyCloudSettings(Material target)
        {
            var offset=CloudOffset;target.SetVector("_CloudOffset",new Vector4(offset.x,offset.y,0,0));
            target.SetFloat("_CloudScale",Mathf.Max(10,cloudSize));target.SetFloat("_CloudCoverage",cloudCoverage);
            target.SetFloat("_CloudHeight",Mathf.Max(20,cloudAltitude));
        }
        void OnDisable()
        {
            if(sunlight!=null && (sunlight.cookie==cookie || (sunlight.cookie==null && originalCookie==null))){sunlight.cookie=originalCookie;sunlight.cookieSize2D=originalSize;}
            if(sunlight!=null)sunlight.transform.position=originalPosition;
            if(cookie!=null){cookie.Release();DestroyOwned(cookie);cookie=null;}
            if(material!=null){DestroyOwned(material);material=null;}
        }
        static void DestroyOwned(Object target){if(Application.isPlaying)Destroy(target);else DestroyImmediate(target);}
    }
}
