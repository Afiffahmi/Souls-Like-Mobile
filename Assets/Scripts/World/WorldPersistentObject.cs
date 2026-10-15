using System;
using UnityEngine;
using UnityEngine.Events;

namespace Nusantara.World
{
    [DisallowMultipleComponent]
    public sealed class WorldPersistentObject : MonoBehaviour, IWorldPersistentState
    {
        [Tooltip("Unique per authored placement within a chunk, across all content batches. Never derive this from an instance ID.")]
        [SerializeField] string persistentId;
        [SerializeField] string stateJson = "{}";
        WorldStateStore store;
        string storeKey;
        public UnityEvent<string> restored;
        public string PersistentId => persistentId;
        public string CaptureState() => stateJson;
        public void SetState(string json)
        {
            stateJson = json;
            store?.Set(storeKey, json);
        }
        public void BindState(WorldStateStore stateStore, string key) { store = stateStore; storeKey = key; }
        public void RestoreState(string json) { stateJson = json; restored?.Invoke(json); }
        public void AssignId(string id) { persistentId = id; }
#if UNITY_EDITOR
        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(persistentId)) persistentId = Guid.NewGuid().ToString("N");
        }
#endif
    }
}
