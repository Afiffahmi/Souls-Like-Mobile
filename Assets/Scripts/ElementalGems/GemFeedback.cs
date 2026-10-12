using TMPro;
using UnityEngine;

namespace ElementalGems
{
    [DefaultExecutionOrder(1000)]
    public sealed class GemFeedback : MonoBehaviour
    {
        private static Canvas overlayCanvas;
        private static RectTransform overlayRect;

        private float life = 1.2f;
        private Vector3 worldPosition;
        private TextMeshProUGUI text;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            overlayCanvas = null;
            overlayRect = null;
        }

        public static void Show(Vector3 point, string message, Color color)
        {
            EnsureOverlay();

            var go = new GameObject("Gem Hit Feedback", typeof(RectTransform));
            go.transform.SetParent(overlayRect, false);
            var feedback = go.AddComponent<GemFeedback>();
            feedback.worldPosition = point + Vector3.up * 0.6f;
            feedback.text = go.AddComponent<TextMeshProUGUI>();
            feedback.text.text = message;
            feedback.text.fontSize = 30f;
            feedback.text.enableAutoSizing = false;
            feedback.text.textWrappingMode = TextWrappingModes.NoWrap;
            feedback.text.color = Color.Lerp(color, Color.white, 0.5f);
            feedback.text.alignment = TextAlignmentOptions.Center;
            feedback.text.raycastTarget = false;
            feedback.text.outlineWidth = 0.2f;

            var rect = feedback.text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 90f);
            feedback.UpdateScreenPosition();
        }

        private static void EnsureOverlay()
        {
            // Scene unload destroys this canvas; Unity's null check then recreates it.
            if (overlayCanvas != null) return;

            var root = new GameObject("Combat Feedback Overlay", typeof(RectTransform),
                typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            overlayCanvas = root.GetComponent<Canvas>();
            overlayRect = root.GetComponent<RectTransform>();
            // Overlay UI is composited after camera image effects, keeping text sharp.
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 100;

            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            worldPosition += Vector3.up * Time.deltaTime * 0.4f;
            if (text != null) text.alpha = Mathf.Clamp01(life * 2f);
        }

        private void LateUpdate()
        {
            UpdateScreenPosition();
        }

        private void UpdateScreenPosition()
        {
            if (text == null) return;

            var camera = Camera.main;
            if (camera == null || overlayRect == null)
            {
                text.enabled = false;
                return;
            }

            var screenPoint = camera.WorldToScreenPoint(worldPosition);
            bool visible = screenPoint.z >= camera.nearClipPlane &&
                screenPoint.z <= camera.farClipPlane && camera.pixelRect.Contains(screenPoint);
            text.enabled = visible;
            if (!visible) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect,
                screenPoint, null, out var localPoint))
                text.rectTransform.anchoredPosition = localPoint;
        }
    }
}
