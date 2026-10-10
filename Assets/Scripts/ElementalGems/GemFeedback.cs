using TMPro;
using UnityEngine;

namespace ElementalGems
{
    public sealed class GemFeedback : MonoBehaviour
    {
        private float life = 1.2f;
        private TextMeshPro text;
        public static void Show(Vector3 point, string message, Color color)
        {
            var go = new GameObject("Gem Hit Feedback"); go.transform.position = point + Vector3.up * 0.6f;
            var feedback = go.AddComponent<GemFeedback>();
            feedback.text = go.AddComponent<TextMeshPro>();
            feedback.text.text = message; feedback.text.fontSize = 3.5f;
            feedback.text.color = Color.Lerp(color, Color.white, 0.5f);
            feedback.text.alignment = TextAlignmentOptions.Center;
            feedback.text.rectTransform.sizeDelta = new Vector2(5, 1);
            feedback.text.outlineWidth = 0.2f;
        }
        private void Update()
        {
            life -= Time.deltaTime; if (life <= 0) { Destroy(gameObject); return; }
            transform.position += Vector3.up * Time.deltaTime * 0.4f;
            if (Camera.main != null) transform.rotation = Camera.main.transform.rotation;
            if (text != null) text.alpha = Mathf.Clamp01(life * 2);
        }
    }
}
