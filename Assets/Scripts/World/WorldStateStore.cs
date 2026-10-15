using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Nusantara.World
{
    public interface IWorldPersistentState
    {
        string PersistentId { get; }
        string CaptureState();
        void RestoreState(string json);
    }

    public sealed class WorldStateStore
    {
        [Serializable] sealed class Entry { public string key; public string value; }
        [Serializable] sealed class Document { public int version = 1; public string worldId; public List<Entry> entries = new List<Entry>(); }
        readonly Dictionary<string, string> states = new Dictionary<string, string>(StringComparer.Ordinal);
        public string WorldId { get; }
        public int Revision { get; private set; }
        public int Count => states.Count;
        public WorldStateStore(string worldId) { WorldId = worldId; }
        public static string Key(Vector2Int chunk, string localId) => chunk.x + "," + chunk.y + "/" + localId;
        public bool TryGet(string key, out string value) => states.TryGetValue(key, out value);
        public void Set(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("State requires a stable key.");
            if (states.TryGetValue(key, out var old) && old == value) return;
            states[key] = value;
            Revision++;
        }
        public string ToJson()
        {
            var doc = new Document { worldId = WorldId };
            var keys = new List<string>(states.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys) doc.entries.Add(new Entry { key = key, value = states[key] });
            return JsonUtility.ToJson(doc, true);
        }
        public void LoadJson(string json)
        {
            var doc = JsonUtility.FromJson<Document>(json);
            if (doc == null || doc.version != 1 || doc.worldId != WorldId || doc.entries == null)
                throw new InvalidDataException("Save version or world ID does not match.");
            var next = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in doc.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key) || next.ContainsKey(entry.key))
                    throw new InvalidDataException("Save contains duplicate or invalid object IDs.");
                next.Add(entry.key, entry.value);
            }
            states.Clear();
            foreach (var pair in next) states.Add(pair.Key, pair.Value);
            Revision++;
        }
        public static void WriteAtomic(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, json);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }
}
