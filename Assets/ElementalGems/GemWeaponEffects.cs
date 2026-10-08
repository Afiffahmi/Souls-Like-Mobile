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
        private GemManager manager;
        private PlayerBowVisuals bow;
        private PlayerSwordVisuals swordVisuals;
        private GemSwordCombat melee;
        private GameObject swordEffect, bowEffect;
        private TrailRenderer slash;
        private GemDefinition gem;
        private Transform bowRoot;
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
            if (sword != null)
            {
                swordEffect = Aura(gem.swordAura, sword, swordAuraPosition);
                var go = new GameObject("Elemental Slash Trail"); go.transform.SetParent(sword, false); go.transform.localPosition = swordTrailPosition;
                slash = go.AddComponent<TrailRenderer>(); slash.sharedMaterial = gem.trailMaterial;
                slash.time = gem.trailLifetime; slash.startWidth = gem.swordTrailWidth; slash.endWidth = 0;
                slash.startColor = gem.color; slash.endColor = new Color(gem.color.r, gem.color.g, gem.color.b, 0);
                slash.emitting = false; slash.minVertexDistance = 0.02f;
                slash.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; slash.receiveShadows = false;
            }
            RefreshActive();
        }
        private GameObject Aura(GameObject prefab, Transform parent, Vector3 offset)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, parent, false); go.transform.localPosition = offset; return go;
        }
        private void LateUpdate() => RefreshActive();
        private void RefreshActive()
        {
            if (gem == null || gem.element == ElementType.Normal) return;
            if (bow != null && bow.BowModel != bowRoot)
            {
                Dispose(ref bowEffect); bowRoot = bow.BowModel;
                if (bowRoot != null) bowEffect = Aura(gem.bowAura, bowRoot, bowAuraPosition);
            }
            bool swordOn = swordVisuals != null && swordVisuals.IsSwordDrawn;
            bool bowOn = bowRoot != null && bowRoot.parent == bow.handSocket && bow.isActiveAndEnabled;
            if (swordEffect != null && swordEffect.activeSelf != swordOn) swordEffect.SetActive(swordOn);
            if (bowEffect != null && bowEffect.activeSelf != bowOn) bowEffect.SetActive(bowOn);
            if (slash != null) { slash.emitting = swordOn && melee != null && melee.TrailActive; if (!swordOn) slash.Clear(); }
        }
    }
}
