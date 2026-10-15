using UnityEngine;

namespace Nusantara.World
{
    public sealed class WorldEncounterArea : MonoBehaviour
    {
        public string encounterId;
        public float radius = 40;
        public enum Element { Water, Nature, Neutral }
        public Element element;
        void OnDrawGizmosSelected()
        {
            Gizmos.color = element == Element.Water ? Color.cyan : Color.green;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
