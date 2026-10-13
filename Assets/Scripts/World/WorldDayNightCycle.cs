using UnityEngine;
using UnityEngine.Rendering;

namespace Nusantara.World
{
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class WorldDayNightCycle : MonoBehaviour
    {
        [Header("Cycle length in minutes")]
        [Min(.01f), Tooltip("Minutes from sunrise to sunset (06:00–18:00).")]
        public float dayMinutes = 10;
        [Min(.01f), Tooltip("Minutes from sunset to the next sunrise (18:00–06:00).")]
        public float nightMinutes = 5;
        [Range(0,24)] public float startingHour = 10;
        public bool runCycle = true;
        [Tooltip("Enable to keep time moving while gameplay is paused.")]
        public bool useUnscaledTime;
        [Header("Lighting")]
        public Light sun;
        public Light moon;
        public Material skyMaterial;
        public float sunHeading = -30;
        [Min(0)] public float daylightIntensity = 1;
        [Min(0)] public float moonlightIntensity = .3f;
        public Color dayAmbient = new Color(.24f,.28f,.31f);
        public Color nightAmbient = new Color(.075f,.09f,.14f);
        [Header("Editor preview")]
        [Tooltip("Context menu: Preview Day / Preview Night / Restore Lighting. Preview does not advance game time.")]
        [Range(0,24)] public float previewHour = 12;
        double elapsed;
        Material runtimeSky;
        bool captured;
        Material originalSky;
        Light originalSun;
        AmbientMode originalAmbientMode;
        Color originalSkyAmbient,originalEquator,originalGround,originalFog;
        Quaternion originalSunRotation,originalMoonRotation;
        float originalSunIntensity,originalMoonIntensity;
        Color originalSunColor,originalMoonColor;
        public Material RuntimeSky => runtimeSky;
        public float CurrentHour { get; private set; }
        public bool IsDay => CurrentHour>=6 && CurrentHour<18;

        public static float HourAt(double seconds,float day,float night)
        {
            double d=System.Math.Max(.01,day)*60,n=System.Math.Max(.01,night)*60;
            double total=d+n,t=seconds%total;if(t<0)t+=total;
            return t<d?(float)(6+12*t/d):(float)((18+12*(t-d)/n)%24);
        }
        public static double SecondsAt(float hour,float day,float night)
        {
            hour=Mathf.Repeat(hour,24);double d=System.Math.Max(.01,day)*60,n=System.Math.Max(.01,night)*60;
            return hour>=6 && hour<18?(hour-6)/12d*d:d+Mathf.Repeat(hour-18,24)/12d*n;
        }
        void OnEnable()
        {
            if(!Application.isPlaying)return;
            CaptureLighting();SetHour(startingHour);
        }
        void Update()
        {
            if(!Application.isPlaying)return;
            if(runCycle)Advance(useUnscaledTime?Time.unscaledDeltaTime:Time.deltaTime);
            else ApplyHour(CurrentHour);
        }
        public void Advance(double seconds)
        {
            elapsed=(elapsed+System.Math.Max(0,seconds))%((System.Math.Max(.01,dayMinutes)+System.Math.Max(.01,nightMinutes))*60);
            ApplyHour(HourAt(elapsed,dayMinutes,nightMinutes));
        }
        public void SetHour(float hour)
        {
            elapsed=SecondsAt(hour,dayMinutes,nightMinutes);ApplyHour(Mathf.Repeat(hour,24));
        }
        public void ApplyHour(float hour)
        {
            CaptureLighting();CurrentHour=Mathf.Repeat(hour,24);
            float angle=(CurrentHour-6)*15,elevation=Mathf.Sin(angle*Mathf.Deg2Rad);
            float daylight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.12f,.25f,elevation));
            if(sun!=null)
            {
                sun.transform.rotation=Quaternion.Euler(angle,sunHeading,0);
                sun.intensity=daylightIntensity*Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.2f,elevation));
                sun.color=Color.Lerp(new Color(1,.49f,.24f),new Color(1,.957f,.839f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.5f,elevation)));
                RenderSettings.sun=sun;
            }
            if(moon!=null)
            {
                moon.transform.rotation=Quaternion.Euler(angle+180,sunHeading+20,0);
                moon.intensity=moonlightIntensity*Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.2f,-elevation));
                moon.color=new Color(.57f,.68f,1);
            }
            Color ambient=Color.Lerp(nightAmbient,dayAmbient,daylight);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=ambient;
            RenderSettings.ambientEquatorColor=ambient*.8f;RenderSettings.ambientGroundColor=ambient*.55f;
            RenderSettings.fogColor=Color.Lerp(new Color(.02f,.03f,.065f),new Color(.48f,.59f,.63f),daylight);
            if(runtimeSky!=null)
            {
                runtimeSky.SetFloat("_Daylight",daylight);
                if(sun!=null)runtimeSky.SetVector("_SunDirection",-sun.transform.forward);
                if(moon!=null)runtimeSky.SetVector("_MoonDirection",-moon.transform.forward);
                RenderSettings.skybox=runtimeSky;
            }
        }
        void CaptureLighting()
        {
            if(captured)return;captured=true;
            originalSky=RenderSettings.skybox;originalSun=RenderSettings.sun;originalAmbientMode=RenderSettings.ambientMode;
            originalSkyAmbient=RenderSettings.ambientSkyColor;originalEquator=RenderSettings.ambientEquatorColor;originalGround=RenderSettings.ambientGroundColor;originalFog=RenderSettings.fogColor;
            if(sun!=null){originalSunRotation=sun.transform.rotation;originalSunIntensity=sun.intensity;originalSunColor=sun.color;}
            if(moon!=null){originalMoonRotation=moon.transform.rotation;originalMoonIntensity=moon.intensity;originalMoonColor=moon.color;}
            if(skyMaterial!=null)runtimeSky=new Material(skyMaterial){name="Day night sky (instance)",hideFlags=HideFlags.HideAndDontSave};
        }
        [ContextMenu("Preview Day")] void PreviewDay(){previewHour=12;ApplyHour(previewHour);}
        [ContextMenu("Preview Night")] void PreviewNight(){previewHour=0;ApplyHour(previewHour);}
        [ContextMenu("Preview Selected Hour")] void PreviewSelected(){ApplyHour(previewHour);}
        [ContextMenu("Restore Lighting")] public void RestoreLighting()
        {
            if(!captured)return;
            RenderSettings.skybox=originalSky;RenderSettings.sun=originalSun;RenderSettings.ambientMode=originalAmbientMode;
            RenderSettings.ambientSkyColor=originalSkyAmbient;RenderSettings.ambientEquatorColor=originalEquator;RenderSettings.ambientGroundColor=originalGround;RenderSettings.fogColor=originalFog;
            if(sun!=null){sun.transform.rotation=originalSunRotation;sun.intensity=originalSunIntensity;sun.color=originalSunColor;}
            if(moon!=null){moon.transform.rotation=originalMoonRotation;moon.intensity=originalMoonIntensity;moon.color=originalMoonColor;}
            if(runtimeSky!=null){if(Application.isPlaying)Destroy(runtimeSky);else DestroyImmediate(runtimeSky);runtimeSky=null;}
            captured=false;
        }
        void OnDisable(){RestoreLighting();}
    }
}
