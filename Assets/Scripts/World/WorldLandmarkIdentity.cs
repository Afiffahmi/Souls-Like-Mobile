using System.Collections.Generic;
using UnityEngine;

namespace Nusantara.World
{
    [DisallowMultipleComponent]
    public sealed class WorldLandmarkIdentity : MonoBehaviour
    {
        public string stableId;
        static readonly Dictionary<string, WorldLandmarkIdentity> loaded = new Dictionary<string, WorldLandmarkIdentity>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => loaded.Clear();
        void OnEnable()
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(stableId)) return;
            if (loaded.TryGetValue(stableId, out var other) && other != null && other != this)
            {
                Debug.LogError("Duplicate world landmark rejected: " + stableId, this);
                gameObject.SetActive(false);
                return;
            }
            loaded[stableId] = this;
        }
        void OnDisable()
        {
            if (!string.IsNullOrEmpty(stableId) && loaded.TryGetValue(stableId, out var other) && other == this)
                loaded.Remove(stableId);
        }
    }
}
