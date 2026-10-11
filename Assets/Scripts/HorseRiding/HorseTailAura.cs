using ElementalGems;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoulsLike.Riding
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent, RequireComponent(typeof(HorseController))]
    public sealed class HorseTailAura : MonoBehaviour
    {
        public HorseController horse;
        [Tooltip("Tail bones ordered from root to tip. Sizes below are world-space metres.")]
        public Transform[] tailBones;
        public Material strandMaterial;
        [Header("Layered energy hair")]
        [Range(8,64)] public int strands = 36;
        [Range(8,36)] public int segments = 32;
        [Min(.1f)] public float minimumLength = .65f;
        [Min(.1f)] public float maximumLength = 1.9f;
        [Min(.005f)] public float strandWidth = .075f;
        [Min(0)] public float spread = .22f;
        [Tooltip("Size of the smooth flowing waves in metres. Roots stay attached and tips move freely. Use 0 for straight strands.")]
        [Min(0)] public float waveAmount = .35f;
        [Tooltip("Broad waves from root to tip. Lower values give longer, gentler curves.")]
        [Range(.2f,2f)] public float waveCycles = .85f;
        [Min(0)] public float flowSpeed = 1.4f;
        [Min(.1f)] public float brightness = 2.2f;
        public int randomSeed = 147;
        [Header("Idle / walk / run tail shape")]
        [Tooltip("Hanging depth relative to strand length when stationary. The ends curl gently upward.")]
        [Range(0,1.5f)] public float idleDroop = .7f;
        [Tooltip("Hanging depth at walking speed. Running blends to the original rearward flowing shape.")]
        [Range(0,1f)] public float walkDroop = .22f;
        [Tooltip("How quickly the tail lifts or settles as actual movement speed changes.")]
        [Min(.1f)] public float shapeBlendSpeed = 4f;
        [Header("Stored horse element (independent after dismount)")]
        [SerializeField] private ElementType element = ElementType.Normal;
        [SerializeField, ColorUsage(true,true)] private Color elementColor = new Color(.7f,.78f,.88f);
        public ElementType Element => element;
        public Color ElementColor => elementColor;
        public float Visibility { get; set; } = 1;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private GameObject strandsObject;
        private Vector3[] vertices, centers;
        private Vector2[] uv;
        private Color[] colors;
        private int[] triangles;
        private float[] lengths, phases, widths;
        private Vector2[] offsets;
        private int builtStrands, builtSegments, builtSeed;
        private float builtMin, builtMax;
        private Camera view;
        private GemManager riderGems;
        private MountedPlayerController lastRider;
        private float movementShape;

        public void CaptureGem(GemDefinition gem)
        {
            element = gem ? gem.element : ElementType.Normal;
            elementColor = gem ? gem.color : new Color(.7f,.78f,.88f,1);
            elementColor.a = 1;
        }
        private void Awake() { if (!horse) horse = GetComponent<HorseController>(); }
        private void LateUpdate()
        {
            // Do not follow the player's gem while on foot or already dismounting.
            var rider = horse ? horse.Rider : null;
            if (rider != lastRider)
            {
                lastRider = rider;
                riderGems = rider ? rider.GetComponent<GemManager>() : null;
            }
            if (riderGems && rider && (rider.Phase == RiderPhase.Mounting || rider.IsRiding)) CaptureGem(riderGems.Equipped);
            if (!strandMaterial || tailBones == null || tailBones.Length < 2 || !tailBones[0] || !tailBones[tailBones.Length-1]) return;
            if (!mesh || builtStrands != Mathf.Clamp(strands,8,64) || builtSegments != Mathf.Clamp(segments,8,36) || builtSeed != randomSeed || builtMin != minimumLength || builtMax != maximumLength) Build();
            if (!view) view = Camera.main;
            meshRenderer.enabled = Visibility > .001f;
            if (!meshRenderer.enabled) return;
            var origin = tailBones[0].position;
            var end = tailBones[tailBones.Length-1].position;
            // One continuous curve: a hanging hook at idle, a shallow curve at
            // walking speed, and the original rearward stream at running speed.
            Vector3 direction = -transform.forward;
            Vector3 right = transform.right, up = transform.up;
            float tailLength = Mathf.Max(.1f, Vector3.Distance(origin,end));
            float speed = horse ? horse.CurrentSpeed : 0;
            float walkingSpeed = horse ? Mathf.Max(.1f,horse.EffectiveWalkSpeed) : 3.5f;
            float runningSpeed = horse ? Mathf.Max(walkingSpeed+.1f,horse.EffectiveRunSpeed) : 8f;
            float targetShape = speed <= walkingSpeed ? Mathf.Clamp01(speed/walkingSpeed) : 1+Mathf.InverseLerp(walkingSpeed,runningSpeed,speed);
            movementShape = Mathf.Lerp(movementShape,targetShape,1-Mathf.Exp(-Mathf.Max(.1f,shapeBlendSpeed)*Time.deltaTime));
            float walkingBlend = Mathf.Clamp01(movementShape);
            float runningBlend = Mathf.Clamp01(movementShape-1);
            float droop = Mathf.Lerp(Mathf.Lerp(idleDroop,walkDroop,walkingBlend),0,runningBlend);
            float reach = Mathf.Lerp(Mathf.Lerp(.6f,.9f,walkingBlend),1,runningBlend);
            float rootReach = Mathf.Lerp(Mathf.Lerp(.025f,.2f,walkingBlend),1f/3f,runningBlend);
            float middleReach = Mathf.Lerp(Mathf.Lerp(.18f,.55f,walkingBlend),2f/3f,runningBlend);
            float flutter = Mathf.Lerp(Mathf.Lerp(.6f,.85f,walkingBlend),1,runningBlend);
            float speedStretch = Mathf.Min(.65f,speed*.05f);
            float time = Time.time * flowSpeed;
            Color tint = elementColor; // Keep saturated gem colour; only the thin core is lightened.
            int vertex = 0;
            for (int n=0;n<builtStrands;n++)
            {
                float tier = n % 3;
                float variation = .5f + .5f*Mathf.Sin(phases[n]);
                float frequency = Mathf.PI*2*waveCycles*Mathf.Lerp(.8f,1.2f,variation);
                float strandSpeed = Mathf.Lerp(.8f,1.15f,variation);
                float amplitude = waveAmount*flutter*Mathf.Lerp(.75f,1.2f,Mathf.InverseLerp(minimumLength,Mathf.Max(minimumLength+.001f,maximumLength),lengths[n]));
                for (int s=0;s<builtSegments;s++)
                {
                    float u = s / (float)(builtSegments-1);
                    // Stagger the hair layers, keeping their roots quiet while
                    // allowing increasingly broad curves and loose, wavy tips.
                    float totalLength = tailLength+lengths[n]+speedStretch;
                    float along = Mathf.Lerp(tailLength*(.05f+tier*.1f),totalLength,u);
                    float t = along/totalLength, inverse = 1-t;
                    float first = 3*inverse*inverse*t, second = 3*inverse*t*t, tip = t*t*t;
                    float rearward = first*rootReach+second*middleReach+tip*reach;
                    float hanging = (first*.75f+second*1.35f+tip*.95f)*droop;
                    Vector3 p = origin + direction*(totalLength*rearward)-up*(totalLength*hanging);
                    float wave = Mathf.SmoothStep(0,1,u)*amplitude;
                    float phase = u*frequency-time*strandSpeed;
                    p += (right*offsets[n].x + up*offsets[n].y)*spread*Mathf.Lerp(.4f,1f,u);
                    p += right*(.65f*Mathf.Sin(phase+phases[n]*.45f)+.35f*Mathf.Sin(phase*1.55f+phases[n]))*wave;
                    p += up*Mathf.Sin(phase*.9f+phases[n])*wave*.85f;
                    centers[s] = p;
                }
                for (int layer=0;layer<3;layer++)
                {
                    float thickness = layer==0 ? 2.8f : layer==1 ? 1f : .24f;
                    Color color = layer==2 ? Color.Lerp(tint,Color.white,.48f) : tint;
                    color *= brightness;
                    float opacity = (layer==0 ? .12f : layer==1 ? .72f : .9f)*Visibility;
                    for (int s=0;s<builtSegments;s++)
                    {
                        float u=s/(float)(builtSegments-1);
                        Vector3 tangent = centers[Mathf.Min(s+1,builtSegments-1)]-centers[Mathf.Max(0,s-1)];
                        Vector3 sight = view ? view.transform.position-centers[s] : -transform.forward;
                        Vector3 side = Vector3.Cross(tangent,sight).normalized;
                        if(side.sqrMagnitude<.01f)side=right;
                        float taper = Mathf.Pow(1-u,.65f)*Mathf.SmoothStep(.3f,1f,Mathf.Clamp01(u*8f));
                        float half = strandWidth*widths[n]*thickness*taper*.5f;
                        color.a = opacity*Mathf.Clamp01((1-u)*7f)*(.82f+.18f*Mathf.Sin(u*15-time*2+phases[n]));
                        vertices[vertex]=transform.InverseTransformPoint(centers[s]-side*half);
                        vertices[vertex+1]=transform.InverseTransformPoint(centers[s]+side*half);
                        colors[vertex]=colors[vertex+1]=color;
                        vertex+=2;
                    }
                }
            }
            mesh.vertices=vertices; mesh.colors=colors; mesh.RecalculateBounds();
        }
        private void Build()
        {
            if(mesh)Destroy(mesh);
            builtStrands=Mathf.Clamp(strands,8,64); builtSegments=Mathf.Clamp(segments,8,36);
            builtSeed=randomSeed; builtMin=minimumLength; builtMax=maximumLength;
            if(!strandsObject)
            {
                strandsObject=new GameObject("Layered Elemental Tail"); strandsObject.transform.SetParent(transform,false);
                strandsObject.AddComponent<MeshFilter>(); meshRenderer=strandsObject.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode=ShadowCastingMode.Off; meshRenderer.receiveShadows=false;
            }
            meshRenderer.sharedMaterial=strandMaterial;
            int count=builtStrands*3*builtSegments*2;
            vertices=new Vector3[count]; colors=new Color[count]; uv=new Vector2[count];
            triangles=new int[builtStrands*3*(builtSegments-1)*6]; centers=new Vector3[builtSegments];
            lengths=new float[builtStrands]; phases=new float[builtStrands]; widths=new float[builtStrands]; offsets=new Vector2[builtStrands];
            var random=new System.Random(randomSeed);
            for(int n=0;n<builtStrands;n++)
            {
                lengths[n]=Mathf.Lerp(Mathf.Max(.1f,minimumLength),Mathf.Max(minimumLength,maximumLength),(float)random.NextDouble());
                phases[n]=(float)random.NextDouble()*Mathf.PI*2;
                widths[n]=Mathf.Lerp(.6f,1.4f,(float)random.NextDouble());
                float radius=.35f+(n%3)*.35f;
                offsets[n]=new Vector2(Mathf.Cos(phases[n]),Mathf.Sin(phases[n]))*radius;
            }
            int tri=0;
            for(int strip=0;strip<builtStrands*3;strip++)
                for(int s=0;s<builtSegments;s++)
                {
                    int v=(strip*builtSegments+s)*2;
                    uv[v]=new Vector2(s/(float)(builtSegments-1),0);uv[v+1]=new Vector2(uv[v].x,1);
                    if(s==builtSegments-1)continue;
                    triangles[tri++]=v;triangles[tri++]=v+1;triangles[tri++]=v+2;
                    triangles[tri++]=v+2;triangles[tri++]=v+1;triangles[tri++]=v+3;
                }
            mesh=new Mesh{name="Layered horse tail energy"};mesh.MarkDynamic();mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            strandsObject.GetComponent<MeshFilter>().sharedMesh=mesh;
        }
        private void OnDisable(){if(meshRenderer)meshRenderer.enabled=false;}
        private void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
