using UnityEngine;

namespace ElementalGems
{
    [DisallowMultipleComponent, RequireComponent(typeof(GemManager)), DefaultExecutionOrder(200)]
    public sealed class GemWeaponEffects : MonoBehaviour
    {
        public Transform sword;
        public Vector3 swordAuraPosition = new Vector3(0.12f, 0.1f, 0);
        public Vector3 swordTrailPosition = new Vector3(0.48f, 0.1f, 0);
        public Vector3 bowAuraPosition;
        [Header("Visual-only style overrides")]
        public GemVfxStyle[] styles;
        private GemManager manager;
        private PlayerBowVisuals bow;
        private PlayerSwordVisuals swordVisuals;
        private GemSwordCombat melee;
        private GemLightSlashEffects slashTiming;
        private GameObject swordEffect, bowEffect;
        private TrailRenderer slash;
        private GemDefinition gem;
        private Transform bowRoot;
        private GemVfxStyle style;
        private void Awake()
        {
            manager = GetComponent<GemManager>(); bow = GetComponent<PlayerBowVisuals>();
            swordVisuals = GetComponent<PlayerSwordVisuals>(); melee = GetComponent<GemSwordCombat>();
        }
        private void OnEnable() { manager.GemChanged += Refresh; Refresh(manager.Equipped); }
        private void OnDisable() { manager.GemChanged -= Refresh; Clear(); }
        private static void Dispose(ref GameObject obj) { if (obj != null) { obj.SetActive(false); Object.Destroy(obj); obj = null; } }
        private void Clear()
        {
            Dispose(ref swordEffect); Dispose(ref bowEffect);
            if (slash != null) { slash.emitting = false; slash.Clear(); Destroy(slash.gameObject); slash = null; }
        }
        private void Refresh(GemDefinition next)
        {
            Clear(); gem = next; bowRoot = null;
            if (gem == null || gem.element == ElementType.Normal) return;
            style = styles == null ? null : System.Array.Find(styles, s => s != null && s.element == gem.element);
            if (sword != null)
            {
                swordEffect = Aura(style != null ? style.swordAura : gem.swordAura, sword, swordAuraPosition);
                var go = new GameObject("Elemental Slash Trail"); go.transform.SetParent(sword, false); go.transform.localPosition = swordTrailPosition;
                slash = go.AddComponent<TrailRenderer>(); slash.sharedMaterial = style != null ? style.trailMaterial : gem.trailMaterial;
                slash.time = style != null ? style.trailDuration : gem.trailLifetime;
                slash.startWidth = style != null ? style.swordTrailWidth : gem.swordTrailWidth; slash.endWidth = 0;
                var color = style != null ? style.trailColor : gem.color;
                slash.startColor = color; slash.endColor = new Color(color.r,color.g,color.b,0);
                slash.numCornerVertices=3;slash.numCapVertices=2;slash.textureMode=LineTextureMode.Stretch;
                slash.emitting = false; slash.minVertexDistance = 0.02f;
                slash.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; slash.receiveShadows = false;
            }
            RefreshActive();
        }
        private GameObject Aura(GameObject prefab, Transform parent, Vector3 offset)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, parent, false); go.transform.localPosition = offset;
            // Imported bow has a large FBX scale. Keep particle sizes in world metres.
            var scale=parent.lossyScale;
            go.transform.localScale=new Vector3(1/Mathf.Max(.001f,Mathf.Abs(scale.x)),1/Mathf.Max(.001f,Mathf.Abs(scale.y)),1/Mathf.Max(.001f,Mathf.Abs(scale.z))) * (style != null ? style.size : 1);
            return go;
        }
        private void LateUpdate() => RefreshActive();
        private void RefreshActive()
        {
            if (gem == null || gem.element == ElementType.Normal) return;
            if (bow != null && bow.BowModel != bowRoot)
            {
                Dispose(ref bowEffect); bowRoot = bow.BowModel;
                if (bowRoot != null)
                {
                    var renderers=bowRoot.GetComponentsInChildren<Renderer>(true);
                    Vector3 center=Vector3.zero;
                    if(renderers.Length>0)
                    {
                        var bounds=renderers[0].bounds;
                        for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
                        center=bowRoot.InverseTransformPoint(bounds.center);
                    }
                    bowEffect = Aura(style != null ? style.bowAura : gem.bowAura, bowRoot, center+bowAuraPosition);
                }
            }
            bool swordOn = swordVisuals != null && swordVisuals.IsSwordDrawn;
            bool bowOn = bowRoot != null && bowRoot.parent == bow.handSocket && bow.isActiveAndEnabled;
            SetAuraActive(swordEffect,swordOn);
            SetAuraActive(bowEffect,bowOn);
            if (slashTiming == null) slashTiming = GetComponent<GemLightSlashEffects>();
            if (slash != null)
            {
                bool configured = slashTiming != null && slashTiming.isActiveAndEnabled && slashTiming.HasConfiguredSlash;
                slash.emitting = swordOn && (configured ? slashTiming.TrailActive : melee != null && melee.TrailActive);
                if (!swordOn || (configured && !slash.emitting)) slash.Clear();
            }
        }
        private static void SetAuraActive(GameObject effect,bool active)
        {
            if(effect==null||effect.activeSelf==active)return;
            if(!active)foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>())ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.SetActive(active);
            if(active)foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>())ps.Play(false);
        }
    }
}
