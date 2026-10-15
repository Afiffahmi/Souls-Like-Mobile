using ElementalGems;
using UnityEngine;

namespace SoulsLike.Enemies
{
    /// <summary>Uses the enemy's current combat element, including temporary Normal windows.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ElementalEnemy))]
    public sealed class ElementalEnemyAppearance : MonoBehaviour
    {
        [Tooltip("Creature mesh renderers only; exclude particles, shields and health bars.")]
        public Renderer[] modelRenderers = new Renderer[0];
        [Tooltip("Indexed by ElementType: Normal, Fire, Water, Nature, Earth, Lightning, Wind, Darkness.")]
        public Material[] elementMaterials = new Material[8];
        private ElementalEnemy identity;
        private ElementType applied;
        private bool hasApplied;

        private void Awake() => identity = GetComponent<ElementalEnemy>();
        private void OnEnable()
        {
            if (identity == null) identity = GetComponent<ElementalEnemy>();
            identity.ElementChanged += Refresh;
            hasApplied = false;
            Refresh();
        }
        private void OnDisable()
        {
            if (identity != null) identity.ElementChanged -= Refresh;
            hasApplied = false;
        }
        // Timed link-up recovery and Inspector changes can change CurrentElement without an event.
        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (identity == null) identity = GetComponent<ElementalEnemy>();
            var current = identity.CurrentElement;
            if (hasApplied && current == applied) return;
            int index = (int)current;
            if (elementMaterials == null || index >= elementMaterials.Length || elementMaterials[index] == null) return;
            foreach (var renderer in modelRenderers)
            {
                if (renderer == null) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = elementMaterials[index];
                renderer.sharedMaterials = materials;
            }
            applied = current;
            hasApplied = true;
        }
    }
}
