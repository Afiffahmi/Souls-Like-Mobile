using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Nusantara.World
{
    [DefaultExecutionOrder(-9000), DisallowMultipleComponent]
    public sealed class WorldChunkStreamer : MonoBehaviour
    {
        enum PartKind { Essential, Gameplay, Decoration, Distant }
        sealed class Part
        {
            public string address;
            public string statePrefix;
            public PartKind kind;
            public GameObject holder;
            public AsyncOperationHandle<GameObject> handle;
            public bool requested, ready, cancelled;
            public float retryAt;
            public IWorldPersistentState[] participants = Array.Empty<IWorldPersistentState>();
        }
        sealed class Resident
        {
            public WorldChunkRecord record;
            public ChunkTier tier;
            public GameObject root;
            public bool regionGroundReady;
            public float collisionPreloadUntil;
            public List<Part> parts = new List<Part>();
        }

        public WorldDefinition definition;
        [Tooltip("Assign the existing player explicitly. The streamer never replaces the player or camera.")]
        public Transform player;
        [Min(1)] public float autosaveSeconds = 10;
        public bool persistToDisk = true;
        public bool Running { get; private set; }
        public int ResidentChunkCount => residents.Count;
        public int PendingLoadCount => pending;
        public WorldStateStore State { get; private set; }
        public event Action<Vector2Int> CollisionReady;
        public event Action NavigationGeometryChanged;
        public event Action<string> LoadFailed;

        readonly Dictionary<Vector2Int, WorldChunkRecord> catalog = new Dictionary<Vector2Int, WorldChunkRecord>();
        readonly Dictionary<Vector2Int, Resident> residents = new Dictionary<Vector2Int, Resident>();
        readonly Dictionary<int, Transform> regionRoots = new Dictionary<int, Transform>();
        readonly List<GameObject> ownedGrounds = new List<GameObject>();
        readonly HashSet<int> readyGrounds = new HashSet<int>();
        readonly HashSet<Vector2Int> candidates = new HashSet<Vector2Int>();
        readonly List<Vector2Int> removals = new List<Vector2Int>();
        Vector3 previousPosition, velocity;
        float nextPlan, nextSave, previousTime;
        int pending, epoch, savedRevision = -1;
        string savePath;
        bool saveFault;
        Task saveTask;

        void OnEnable() { if (Application.isPlaying) Begin(); }
        public bool Begin()
        {
            if (Running) return true;
            if (definition == null || player == null) { Debug.LogError("World streaming requires a definition and player.", this); return false; }
            if (Vector3.Distance(transform.lossyScale, Vector3.one) > .0001f || Quaternion.Angle(transform.rotation, Quaternion.identity) > .001f)
            { Debug.LogError("The World root must have unit world scale and no rotation to preserve metre coordinates.", this); return false; }
            var errors = definition.ConfigurationErrors(true).ToArray();
            if (errors.Length > 0) { Debug.LogWarning(string.Join("\n", errors), this); return false; }
            readyGrounds.Clear();
            if(definition.regionalGroundPlanes)
            {
                foreach(var ground in FindObjectsByType<WorldRegionGround>(FindObjectsSortMode.None))
                    if(ground.gameObject.scene==gameObject.scene)readyGrounds.Add(ground.region);
                foreach(var region in definition.regions)
                {
                    if(readyGrounds.Contains(region.id))continue;
                    var ground=Instantiate(region.groundPrefab,definition.ChunkOrigin(region.chunkOffset),Quaternion.identity,transform);
                    ownedGrounds.Add(ground);readyGrounds.Add(region.id);
                }
            }
            catalog.Clear();
            foreach (var record in definition.chunks) catalog.Add(record.coordinate, record);
            State = new WorldStateStore(definition.worldId);
            var safeId = string.Concat(definition.worldId.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_'));
            savePath = Path.Combine(Application.persistentDataPath, "Nusantara", safeId + ".json");
            saveFault = false;
            if (persistToDisk && File.Exists(savePath))
            {
                try { State.LoadJson(File.ReadAllText(savePath)); }
                catch (Exception error) { saveFault = true; Debug.LogError("World save could not be read; it will not be overwritten: " + error.Message, this); }
            }
            savedRevision = State.Revision;
            previousPosition = player.position;
            previousTime = Time.unscaledTime;
            velocity = Vector3.zero;
            nextPlan = 0;
            nextSave = Time.unscaledTime + autosaveSeconds;
            epoch++;
            Running = true;
            return true;
        }

        void Update() => Tick();
        public void RequestReplan() { nextPlan = 0; }
        public void Tick()
        {
            if (!Running || player == null) return;
            float now = Time.unscaledTime;
            if (now >= nextPlan)
            {
                float elapsed = now - previousTime;
                velocity = elapsed > .001f ? (player.position - previousPosition) / elapsed : Vector3.zero;
                previousPosition = player.position;
                previousTime = now;
                nextPlan = now + definition.planningInterval;
                Reconcile();
            }
            ScheduleOne();
            if (now >= nextSave)
            {
                nextSave = now + autosaveSeconds;
                CaptureAll();
                Save(false);
            }
        }

        void Reconcile()
        {
            WorldStreamingPlan.GetCandidates(definition, player.position, candidates);
            foreach (var coordinate in candidates)
            {
                var tier = WorldStreamingPlan.DesiredTier(definition, coordinate, player.position, velocity);
                if (tier == ChunkTier.Unloaded || residents.ContainsKey(coordinate) || !catalog.TryGetValue(coordinate, out var record)) continue;
                var resident = CreateResident(record);
                resident.tier = tier;
                residents.Add(coordinate, resident);
            }
            removals.Clear();
            foreach (var pair in residents)
            {
                var resident = pair.Value;
                resident.tier = WorldStreamingPlan.RetainedTier(definition, pair.Key, resident.tier, player.position, velocity);
                if(resident.collisionPreloadUntil>Time.unscaledTime&&resident.tier<ChunkTier.Visual)resident.tier=ChunkTier.Visual;
                if (resident.tier == ChunkTier.Unloaded) { removals.Add(pair.Key); continue; }
                foreach (var part in resident.parts)
                {
                    bool want = Wants(resident, part.kind);
                    // Preserve visible terrain until its replacement has arrived.
                    if (part.kind == PartKind.Essential && resident.tier == ChunkTier.Distant && !Ready(resident, PartKind.Distant)) want = true;
                    if (part.kind == PartKind.Distant && resident.tier >= ChunkTier.Visual && !Ready(resident, PartKind.Essential)) want = true;
                    if (!want && part.requested) ReleasePart(part, resident.record.coordinate);
                }
            }
            foreach (var coordinate in removals)
            {
                ReleaseResident(residents[coordinate]);
                residents.Remove(coordinate);
            }
        }

        Resident CreateResident(WorldChunkRecord record)
        {
            if (!regionRoots.TryGetValue(record.region, out var parent))
            {
                parent = new GameObject("Region " + record.region).transform;
                parent.SetParent(transform, false);
                regionRoots.Add(record.region, parent);
            }
            var resident = new Resident { record = record, regionGroundReady=readyGrounds.Contains(record.region), root = new GameObject($"Chunk {record.coordinate.x:D2}_{record.coordinate.y:D2}") };
            resident.root.transform.SetParent(parent, false);
            resident.root.transform.position = definition.ChunkOrigin(record.coordinate);
            AddPart(resident, PartKind.Essential, record.essentialAddress, "essential");
            AddPart(resident, PartKind.Distant, record.distantAddress, "distant");
            for (int i = 0; i < record.decorationAddresses.Length; i++) AddPart(resident, PartKind.Decoration, record.decorationAddresses[i], "decoration/" + i);
            for (int i = 0; i < record.gameplayAddresses.Length; i++) AddPart(resident, PartKind.Gameplay, record.gameplayAddresses[i], "gameplay/" + i);
            return resident;
        }
        static void AddPart(Resident resident, PartKind kind, string address, string prefix)
        {
            if (!string.IsNullOrWhiteSpace(address)) resident.parts.Add(new Part { kind = kind, address = address, statePrefix = prefix });
        }
        static bool Wants(Resident resident, PartKind kind) => kind == PartKind.Distant ? resident.tier == ChunkTier.Distant :
            kind == PartKind.Gameplay ? resident.tier == ChunkTier.Gameplay : resident.tier >= ChunkTier.Visual;
        static bool Ready(Resident resident, PartKind kind)
        {
            if(resident.regionGroundReady && ((kind==PartKind.Essential&&string.IsNullOrEmpty(resident.record.essentialAddress))||(kind==PartKind.Distant&&string.IsNullOrEmpty(resident.record.distantAddress))))return true;
            foreach (var part in resident.parts) if (part.kind == kind && part.ready) return true;
            return false;
        }
        public bool PreloadCollision(Vector3 position)
        {
            if(!Running||!catalog.TryGetValue(definition.WorldToChunk(position),out var record))return false;
            if(!residents.TryGetValue(record.coordinate,out var resident))
            {resident=CreateResident(record);residents.Add(record.coordinate,resident);}
            resident.collisionPreloadUntil=Time.unscaledTime+30;
            if(resident.tier<ChunkTier.Visual)resident.tier=ChunkTier.Visual;
            RequestReplan();return true;
        }
        public void FinishCollisionPreload(Vector3 position)
        {if(definition!=null&&residents.TryGetValue(definition.WorldToChunk(position),out var resident))resident.collisionPreloadUntil=0;}
        public bool IsCollisionReady(Vector3 position) => definition != null && residents.TryGetValue(definition.WorldToChunk(position), out var resident)
            && Ready(resident, PartKind.Essential);

        void ScheduleOne()
        {
            if (pending >= definition.maxConcurrentLoads) return;
            Part selected = null;
            Resident owner = null;
            int best = int.MinValue;
            var center = definition.WorldToChunk(player.position);
            foreach (var resident in residents.Values)
            {
                foreach (var part in resident.parts)
                {
                    if (part.requested || Time.unscaledTime < part.retryAt || !Wants(resident, part.kind)) continue;
                    if ((part.kind == PartKind.Decoration || part.kind == PartKind.Gameplay) && !Ready(resident, PartKind.Essential)) continue;
                    int priority = part.kind == PartKind.Essential ? 4000 : part.kind == PartKind.Gameplay ? 3000 : part.kind == PartKind.Decoration ? 2000 : 1000;
                    priority -= WorldStreamingPlan.Distance(center, resident.record.coordinate);
                    if (priority <= best) continue;
                    best = priority; selected = part; owner = resident;
                }
            }
            if (selected != null) StartPart(owner, selected);
        }

        void StartPart(Resident resident, Part part)
        {
            int requestEpoch = epoch;
            part.cancelled = false;
            part.requested = true;
            part.holder = new GameObject(part.statePrefix);
            part.holder.transform.SetParent(resident.root.transform, false);
            // Restore saved states before any gameplay OnEnable/Start can run.
            part.holder.SetActive(false);
            pending++;
            try { part.handle = Addressables.InstantiateAsync(part.address, part.holder.transform, false, true); }
            catch (Exception error)
            {
                pending--;
                Fail(part, error.Message);
                return;
            }
            part.handle.Completed += operation =>
            {
                bool current = this != null && Running && requestEpoch == epoch;
                if (this != null) pending--;
                if (!current || part.cancelled || part.holder == null)
                {
                    ReleaseHandle(operation);
                    // A cancelled Part can be requested again only after this callback finishes.
                    part.requested = false;
                    return;
                }
                if (operation.Status != AsyncOperationStatus.Succeeded || operation.Result == null)
                {
                    ReleaseHandle(operation);
                    Fail(part, operation.OperationException?.Message ?? "Addressables returned no instance.");
                    return;
                }
                try
                {
                    part.participants = operation.Result.GetComponentsInChildren<MonoBehaviour>(true).OfType<IWorldPersistentState>().ToArray();
                    var ids = new HashSet<string>();
                    foreach (var participant in part.participants)
                    {
                        if (string.IsNullOrWhiteSpace(participant.PersistentId) || !ids.Add(participant.PersistentId))
                            throw new InvalidOperationException("Missing or duplicate persistent placement ID in " + part.address);
                        string key = StateKey(resident.record.coordinate, part, participant);
                        if (participant is WorldPersistentObject adapter) adapter.BindState(State, key);
                        if (State.TryGet(key, out var value)) participant.RestoreState(value);
                    }
                    part.ready = true;
                    part.holder.SetActive(true);
                    if (part.kind == PartKind.Essential) { CollisionReady?.Invoke(resident.record.coordinate); NavigationGeometryChanged?.Invoke(); }
                }
                catch (Exception error) { ReleaseHandle(operation); Fail(part, error.Message); }
            };
        }

        static string StateKey(Vector2Int coordinate, Part part, IWorldPersistentState participant) =>
            WorldStateStore.Key(coordinate, part.address + "/" + participant.PersistentId);
        void Fail(Part part, string message)
        {
            part.ready = part.requested = false;
            part.retryAt = Time.unscaledTime + definition.retryDelaySeconds;
            DestroyOwned(part.holder);
            part.holder = null;
            Debug.LogError("World chunk load failed: " + part.address + " — " + message, this);
            LoadFailed?.Invoke(part.address);
        }
        static void ReleaseHandle(AsyncOperationHandle<GameObject> handle)
        {
            if (!handle.IsValid()) return;
            if (handle.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(handle);
            else Addressables.Release(handle);
        }
        void Capture(Part part, Vector2Int coordinate)
        {
            if (!part.ready || State == null) return;
            foreach (var participant in part.participants)
            {
                if (participant is UnityEngine.Object obj && obj == null) continue;
                try { State.Set(StateKey(coordinate, part, participant), participant.CaptureState()); }
                catch (Exception error) { Debug.LogException(error, this); }
            }
        }
        void ReleasePart(Part part, Vector2Int coordinate)
        {
            if (!part.requested || part.cancelled) return;
            if (part.kind == PartKind.Essential && part.ready) NavigationGeometryChanged?.Invoke();
            Capture(part, coordinate);
            part.cancelled = true;
            part.ready = false;
            part.participants = Array.Empty<IWorldPersistentState>();
            if (part.handle.IsValid() && part.handle.IsDone)
            {
                ReleaseHandle(part.handle);
                part.requested = false;
            }
            DestroyOwned(part.holder);
            part.holder = null;
        }
        void ReleaseResident(Resident resident)
        {
            foreach (var part in resident.parts) ReleasePart(part, resident.record.coordinate);
            DestroyOwned(resident.root);
        }
        static void DestroyOwned(GameObject target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
        public void CaptureAll()
        {
            foreach (var resident in residents.Values)
                foreach (var part in resident.parts) Capture(part, resident.record.coordinate);
        }
        void Save(bool synchronous)
        {
            if (!persistToDisk || saveFault || State == null) return;
            if (saveTask != null)
            {
                if (!synchronous && !saveTask.IsCompleted) return;
                try { saveTask.GetAwaiter().GetResult(); }
                catch (Exception error) { savedRevision = -1; Debug.LogError("World save failed: " + error.Message, this); }
                saveTask = null;
            }
            if (State.Revision == savedRevision) return;
            string snapshot = State.ToJson();
            string path = savePath;
            savedRevision = State.Revision;
            if (synchronous)
            {
                try { WorldStateStore.WriteAtomic(path, snapshot); }
                catch (Exception error) { savedRevision = -1; Debug.LogError("World save failed: " + error.Message, this); }
            }
            else saveTask = Task.Run(() => WorldStateStore.WriteAtomic(path, snapshot));
        }
        void OnApplicationPause(bool paused) { if (paused && Running) { CaptureAll(); Save(true); } }
        void OnApplicationQuit() { if (Running) { CaptureAll(); Save(true); } }
        void OnDisable() => End();
        public void End()
        {
            if (!Running) return;
            Running = false;
            epoch++;
            foreach (var resident in residents.Values) ReleaseResident(resident);
            Save(true);
            residents.Clear();
            foreach (var root in regionRoots.Values) if (root != null) DestroyOwned(root.gameObject);
            regionRoots.Clear();
            foreach(var ground in ownedGrounds)if(ground!=null)DestroyOwned(ground);ownedGrounds.Clear();readyGrounds.Clear();
        }
    }
}
