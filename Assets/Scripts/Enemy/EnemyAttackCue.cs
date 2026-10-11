using UnityEngine;
using UnityEngine.Rendering;

namespace SoulsLike.Enemies
{
    /// <summary>Reused camera-facing warning rings; gameplay timing belongs to EnemyBrain.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAttackCue : MonoBehaviour
    {
        private Transform rings, anchor;
        private LineRenderer inner, outer;
        private Camera view;
        private float age, duration, radius;
        private Color color;
        private Vector3 anchorOffset;

        public void Show(EnemyAttackDefinition attack, Animator animator)
        {
            if (rings == null)
            {
                var go = new GameObject("Attack warning rings");
                go.transform.SetParent(transform, false);
                rings = go.transform;
                inner = MakeRing("Warning", 1f, .075f);
                outer = MakeRing("Warning echo", 1f, .035f);
            }
            Material material = attack.cueMaterial != null ? attack.cueMaterial : Resources.Load<Material>("EnemyAttackCue");
            if (material == null) { Hide(); return; }
            inner.sharedMaterial = outer.sharedMaterial = material;
            anchor = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
            anchorOffset = Vector3.zero;
            if (anchor == null)
            {
                var skin = GetComponentInChildren<SkinnedMeshRenderer>();
                anchor = skin != null ? skin.rootBone : null;
                anchorOffset = Vector3.up * .5f;
            }
            if (anchor == null) { anchor = transform; anchorOffset = Vector3.up * 1.4f; }
            color = attack.canBeParried ? new Color(1f, .8f, .03f) : new Color(1f, .035f, .015f);
            age = 0; duration = Mathf.Max(.1f, attack.cueDuration); radius = Mathf.Max(.1f, attack.cueRadius);
            rings.gameObject.SetActive(true);
            Draw();
        }
        private LineRenderer MakeRing(string label, float size, float width)
        {
            var go = new GameObject(label);
            go.transform.SetParent(rings, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.loop = true; line.positionCount = 64;
            line.widthMultiplier = width; line.alignment = LineAlignment.TransformZ;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * size);
            }
            return line;
        }
        private void LateUpdate()
        {
            if (rings == null || !rings.gameObject.activeSelf) return;
            age += Time.deltaTime;
            if (age >= duration) { Hide(); return; }
            Draw();
        }
        private void Draw()
        {
            if (view == null) view = Camera.main;
            Vector3 point = anchor.position + anchorOffset;
            if (view != null)
            {
                Vector3 towardCamera = view.orthographic ? -view.transform.forward : (view.transform.position - point).normalized;
                rings.SetPositionAndRotation(point + towardCamera * .5f, view.transform.rotation);
            }
            else rings.position = point;
            float t = Mathf.Clamp01(age / duration);
            float size = radius * Mathf.Lerp(.6f, 1.15f, 1 - Mathf.Pow(1 - t, 3));
            // World size stays readable even on scaled enemy prefabs.
            Vector3 parentScale = transform.lossyScale;
            rings.localScale = new Vector3(size / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
                size / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), size / Mathf.Max(.001f, Mathf.Abs(parentScale.z)));
            outer.transform.localScale = Vector3.one * Mathf.Lerp(.88f, 1.3f, t);
            Color c = color; c.a = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 1, t));
            inner.startColor = inner.endColor = c;
            c.a *= .4f; outer.startColor = outer.endColor = c;
        }
        public void Hide() { if (rings != null) rings.gameObject.SetActive(false); }
        private void OnDisable() => Hide();
    }
}
