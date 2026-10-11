using ElementalGems;
using UnityEngine;
using UnityEngine.UI;

namespace SoulsLike.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(Canvas), typeof(HealthBar))]
    public sealed class EnemyHealthBarBillboard : MonoBehaviour
    {
        public Enemy health;
        public Vector3 worldOffset = new Vector3(0, 3.15f, 0);
        [Header("Enemy element icon")]
        [Tooltip("Optional player gem library. Automatically uses the player gem UI's manager when empty.")]
        public GemManager gemIconSource;
        [Min(.1f), Tooltip("Icon size relative to the full health bar height.")]
        public float elementIconSize = 1.5f;
        [Min(0), Tooltip("Space to the right of the full bar, relative to its height.")]
        public float elementIconGap = .3f;
        private Canvas canvas;
        private HealthBar view;
        private Camera facingCamera;
        private float nextCameraLookup;
        private ElementalEnemy elemental;
        private Image elementIcon;
        private RectTransform barRect;
        private static GemManager sharedIconSource;
        private static float nextIconSourceLookup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetIconSource() { sharedIconSource = null; nextIconSourceLookup = 0; }

        private void Awake()
        {
            canvas = GetComponent<Canvas>(); view = GetComponent<HealthBar>();
            if (health == null) health = GetComponentInParent<Enemy>();
        }
        private void OnEnable()
        {
            if (health != null) health.OnDeath += HideOnDeath;
        }
        private void Start()
        {
            if (health == null) return;
            view.SetMaxHealth(health.MaxHealth); view.SetHealth(health.CurrentHealth);
            canvas.enabled = !health.IsDead;
        }
        private void OnDisable()
        {
            if (health != null) health.OnDeath -= HideOnDeath;
            if (elementIcon != null) elementIcon.enabled = false;
        }
        private void HideOnDeath()
        {
            view.SetHealth(0);
            canvas.enabled = false;
            if (elementIcon != null) elementIcon.enabled = false;
        }
        private GemManager FindIconSource()
        {
            if (gemIconSource != null) return gemIconSource;
            if (sharedIconSource != null) return sharedIconSource;
            if (Time.unscaledTime < nextIconSourceLookup) return null;
            nextIconSourceLookup = Time.unscaledTime + 1f;
            // Use the same GemDefinition.icon assets displayed by the player's UI.
            foreach (var ui in FindObjectsByType<GemSelectionUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (ui.manager != null) { sharedIconSource = ui.manager; return sharedIconSource; }
            sharedIconSource = FindFirstObjectByType<GemManager>();
            return sharedIconSource;
        }
        private void RefreshElementIcon()
        {
            // Also handles enemies that gain ElementalEnemy on their first hit and
            // element-born enemies returning from their temporary Normal window.
            if (elemental == null) elemental = health.GetComponent<ElementalEnemy>();
            ElementType current = elemental != null && elemental.isActiveAndEnabled && !health.IsDead
                ? elemental.CurrentElement : ElementType.Normal;
            Sprite sprite = null;
            if (current != ElementType.Normal)
            {
                var source = FindIconSource();
                if (source != null && source.gems != null)
                    foreach (var gem in source.gems)
                        if (gem != null && gem.element == current) { sprite = gem.icon; break; }
            }
            if (sprite == null)
            {
                if (elementIcon != null) elementIcon.enabled = false;
                return;
            }
            if (barRect == null) barRect = view.slider != null ? view.slider.transform as RectTransform : transform as RectTransform;
            if (barRect == null) return;
            if (elementIcon == null)
            {
                var existing = barRect.Find("Enemy Element Icon");
                if (existing != null) elementIcon = existing.GetComponent<Image>();
                if (elementIcon == null)
                {
                    var go = new GameObject("Enemy Element Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.layer = gameObject.layer;
                    go.transform.SetParent(barRect, false);
                    elementIcon = go.GetComponent<Image>();
                }
                elementIcon.raycastTarget = false;
                elementIcon.maskable = false;
                elementIcon.preserveAspect = true;
                elementIcon.type = Image.Type.Simple;
            }
            var rect = elementIcon.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
            rect.pivot = new Vector2(0, .5f);
            float height = Mathf.Max(1, barRect.rect.height);
            rect.sizeDelta = Vector2.one * height * Mathf.Max(.1f, elementIconSize);
            rect.anchoredPosition = new Vector2(height * Mathf.Max(0, elementIconGap), 0);
            elementIcon.sprite = sprite;
            elementIcon.color = Color.white;
            elementIcon.enabled = true;
        }
        private void LateUpdate()
        {
            if (health == null || !canvas.enabled) return;
            RefreshElementIcon();
            if ((facingCamera == null || !facingCamera.isActiveAndEnabled) && Time.unscaledTime >= nextCameraLookup)
            {
                facingCamera = Camera.main;
                canvas.worldCamera = facingCamera;
                nextCameraLookup = Time.unscaledTime + .5f;
            }
            transform.position = health.transform.position + worldOffset;
            if (facingCamera != null) transform.rotation = facingCamera.transform.rotation;
        }
    }
}
