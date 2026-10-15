using UnityEngine;
using UnityEngine.Rendering;

namespace ElementalGems
{
    /// <summary>A small eye light with a short-lived wake along its actual movement path.</summary>
    public sealed class EnemyEyeRibbon : MonoBehaviour
    {
        const int Segments = 40, Capacity = 128;
        const float MinimumSpeed = .08f, MinimumTrailLength = .035f;
        public float Length { get; private set; }
        public float Width { get; private set; }
        public float Brightness { get; private set; }
        public float Duration { get; private set; }
        public LineRenderer MainRibbon { get; private set; }
        public Renderer Core { get; private set; }
        Transform owner, coreTransform;
        LineRenderer halo, wisp;
        Mesh coreMesh;
        readonly Vector3[] history = new Vector3[Capacity];
        readonly float[] sampleTimes = new float[Capacity];
        readonly Vector3[] path = new Vector3[Capacity + 1];
        readonly float[] distances = new float[Capacity + 1];
        readonly Vector3[] points = new Vector3[Segments];
        Vector3 previousAnchor;
        float seed, previousTime, lastMotionTime;
        int sampleCount;
        bool initialized;

        public void Configure(Transform character, Material material, Color tint, float length, float width, float brightness, float eyeSide, float duration = .25f)
        {
            owner = character;
            Length = Mathf.Max(.1f, length); Width = Mathf.Max(.01f, width);
            Brightness = Mathf.Max(1, brightness); Duration = Mathf.Max(.05f, duration); seed = eyeSide * 1.7f;
            halo = Line("Soft eye trail glow", material, tint, Width * 3, .2f);
            MainRibbon = Line("Movement eye ribbon", material, tint, Width, 1);
            wisp = Line("Fine trailing wisp", material, tint, Width * .22f, .6f);
            coreMesh = new Mesh { name = "Eye core billboard" };
            coreMesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            coreMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            coreMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            coreMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; coreMesh.RecalculateBounds();
            var core = new GameObject("Bright eye core", typeof(MeshFilter), typeof(MeshRenderer));
            core.transform.SetParent(transform, false); core.layer = gameObject.layer;
            core.GetComponent<MeshFilter>().sharedMesh = coreMesh; Core = core.GetComponent<MeshRenderer>(); Core.sharedMaterial = material;
            Core.shadowCastingMode = ShadowCastingMode.Off; Core.receiveShadows = false; coreTransform = core.transform;
            Paint(Core, tint, 1, 1);
            UpdateGeometry(Time.time, Camera.main);
        }
        LineRenderer Line(string name, Material material, Color tint, float width, float alpha)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false); go.layer = gameObject.layer;
            var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.positionCount = Segments; line.enabled = false;
            line.sharedMaterial = material; line.alignment = LineAlignment.View; line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = width;
            line.widthCurve = new AnimationCurve(new Keyframe(0,.7f), new Keyframe(.07f,1), new Keyframe(.35f,.6f), new Keyframe(.7f,.22f), new Keyframe(1,0));
            line.numCapVertices = 4; line.numCornerVertices = 3; line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            Paint(line, tint, 0, alpha); return line;
        }
        void Paint(Renderer renderer, Color tint, float shape, float alpha)
        {
            tint.a = alpha; var properties = new MaterialPropertyBlock(); properties.SetColor("_Tint", tint);
            properties.SetFloat("_Intensity", Brightness); properties.SetFloat("_Shape", shape); renderer.SetPropertyBlock(properties);
        }
        void LateUpdate() => UpdateGeometry(Time.time, Camera.main);
        void OnEnable() { initialized = false; sampleCount = 0; }
        void OnDisable() { if (MainRibbon != null) ShowTrail(false, 0); }
        void AddSample(Vector3 position, float time)
        {
            if (sampleCount == Capacity)
            {
                System.Array.Copy(history, 1, history, 0, Capacity - 1);
                System.Array.Copy(sampleTimes, 1, sampleTimes, 0, Capacity - 1); sampleCount--;
            }
            history[sampleCount] = position; sampleTimes[sampleCount++] = time;
        }
        void ShowTrail(bool visible, float fade)
        {
            MainRibbon.enabled = halo.enabled = wisp.enabled = visible;
            var color = new Color(1, 1, 1, fade);
            MainRibbon.startColor = MainRibbon.endColor = halo.startColor = halo.endColor = wisp.startColor = wisp.endColor = color;
            if (!visible) return;
            MainRibbon.SetPositions(points); halo.SetPositions(points); wisp.SetPositions(points);
        }
        public void UpdateGeometry(float time, Camera camera)
        {
            if (owner == null || MainRibbon == null) return;
            // Record physical positions only: moving the camera must never create a trail.
            Vector3 anchor = transform.position;
            Vector3 viewOffset = camera != null ? (camera.transform.position - anchor).normalized * (Width * .8f) : Vector3.zero;
            float dt = time - previousTime;
            bool reset = !initialized || dt < 0 || dt > Duration || Vector3.Distance(anchor, previousAnchor) > Mathf.Max(Length * 2, 1);
            if (reset) { sampleCount = 0; lastMotionTime = time - Duration; }
            else if (dt > .00001f)
            {
                // Eye animation can leave a wake even when the character root stays in place.
                // The minimum speed and path length filter tiny stationary jitter.
                if (Vector3.Distance(anchor, previousAnchor) / dt >= MinimumSpeed)
                {
                    if (sampleCount == 0) AddSample(previousAnchor, previousTime);
                    AddSample(anchor, time); lastMotionTime = time;
                }
            }
            int expired = 0;
            while (expired < sampleCount && sampleTimes[expired] < time - Duration) expired++;
            if (expired > 0)
            {
                sampleCount -= expired;
                System.Array.Copy(history, expired, history, 0, sampleCount);
                System.Array.Copy(sampleTimes, expired, sampleTimes, 0, sampleCount);
            }
            path[0] = anchor; distances[0] = 0; int count = 1;
            for (int i = sampleCount - 1; i >= 0; i--)
            {
                Vector3 step = history[i] - path[count - 1]; float distance = step.magnitude;
                if (distance < .0001f) continue;
                float remaining = Length - distances[count - 1];
                path[count] = path[count - 1] + step * Mathf.Min(1, remaining / distance);
                distances[count] = distances[count - 1] + Mathf.Min(distance, remaining); count++;
                if (distance >= remaining) break;
            }
            float totalLength = distances[count - 1];
            float fade = Mathf.Clamp01(1 - (time - lastMotionTime) / Duration);
            bool visible = count > 1 && totalLength >= MinimumTrailLength && fade > .01f;
            if (visible)
            {
                int section = 1;
                for (int i = 0; i < Segments; i++)
                {
                    float distance = totalLength * i / (Segments - 1);
                    while (section < count - 1 && distances[section] < distance) section++;
                    float u = Mathf.InverseLerp(distances[section - 1], distances[section], distance);
                    points[i] = Vector3.Lerp(path[section - 1], path[section], u) + viewOffset;
                }
            }
            ShowTrail(visible, fade);
            coreTransform.position = anchor + viewOffset;
            if (camera != null) coreTransform.rotation = camera.transform.rotation;
            coreTransform.localScale = Vector3.one * (Width * 2.8f * (1 + .05f * Mathf.Sin(time * 5 + seed)));
            previousAnchor = anchor;
            previousTime = time; initialized = true;
        }
        void OnDestroy()
        {
            if (coreMesh == null) return;
            if (Application.isPlaying) Destroy(coreMesh); else DestroyImmediate(coreMesh);
        }
    }
}
