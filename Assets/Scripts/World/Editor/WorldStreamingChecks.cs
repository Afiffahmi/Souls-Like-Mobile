using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace Nusantara.World.Editor
{
    public static class WorldStreamingChecks
    {
        const string StatusKey = "Nusantara.StreamingChecks";
        static string folder;
        static Scene preview;
        static WorldDefinition definition;
        static WorldChunkStreamer streamer;
        static Transform target;
        static AddressableAssetSettings settings;
        static AddressableAssetGroup group;
        static double deadline;
        static int phase;
        static bool collisionObserved;
        static string failure;
        static Action<float> pumpResources;
        static Func<float, float, bool> advanceDelayedActions;

        [MenuItem("Tools/Nusantara/Run Addressables Streaming Checks")]
        public static void Start()
        {
            if (streamer != null) throw new InvalidOperationException("Streaming checks already running.");
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run these isolated checks in Edit Mode.");
            SessionState.SetString(StatusKey, "Running");
            folder = "Assets/NusantaraWorld/StreamingFixture_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            preview = EditorSceneManager.NewPreviewScene();
            settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            group = settings.CreateGroup("Nusantara checks " + Guid.NewGuid().ToString("N"), false, false, true, null, typeof(BundledAssetGroupSchema));
            try
            {
                // Unity's internal Edit Mode test suites pump this too. Hidden callback objects
                // do not reliably tick in preview scenes. This is test-only and never shipped.
                var update = typeof(UnityEngine.ResourceManagement.ResourceManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                if (update == null) throw new NotSupportedException("This Addressables version needs a new Edit Mode test pump.");
                pumpResources = (Action<float>)update.CreateDelegate(typeof(Action<float>), Addressables.ResourceManager);
                var delayed = typeof(UnityEngine.ResourceManagement.ResourceManager).Assembly.GetType("UnityEngine.ResourceManagement.Util.DelayedActionManager");
                var wait = delayed?.GetMethod("Wait", BindingFlags.Static | BindingFlags.Public);
                if (wait == null) throw new NotSupportedException("This Addressables version needs a new simulated-delay test pump.");
                advanceDelayedActions = (Func<float, float, bool>)wait.CreateDelegate(typeof(Func<float, float, bool>));
                string MakePrefab(string name, bool state)
                {
                    var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, preview);
                    if (state) { var persistent = go.AddComponent<WorldPersistentObject>(); persistent.AssignId("test-chest"); }
                    else { var floor = go.AddComponent<BoxCollider>(); floor.size = new Vector3(256, 1, 256); floor.center = new Vector3(128, -.5f, 128); }
                    string path = folder + "/" + name + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(go, path); UnityEngine.Object.DestroyImmediate(go);
                    var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
                    entry.address = folder + "/" + name;
                    return entry.address;
                }
                string essential = MakePrefab("Essential", false), distant = MakePrefab("Distant", false), gameplay = MakePrefab("Gameplay", true);
                definition = ScriptableObject.CreateInstance<WorldDefinition>(); definition.layoutApproved = true; definition.origin = Vector2.zero;
                definition.playerStart = new Vector3(640, 0, 640); definition.playerStartConfirmed = true;
                definition.regions = Enumerable.Range(1, 6).Select(id => new WorldRegion { id = id, name = "Fixture " + id,
                    boundary = new[] { Vector2.zero, Vector2.right, Vector2.up } }).ToArray();
                definition.chunks = new[] {
                    new WorldChunkRecord { coordinate = new Vector2Int(2, 2), region = 1, essentialAddress = essential, distantAddress = distant },
                    new WorldChunkRecord { coordinate = new Vector2Int(3, 2), region = 5, essentialAddress = essential, distantAddress = distant, gameplayAddresses = new[] { gameplay } },
                    new WorldChunkRecord { coordinate = new Vector2Int(20, 20), region = 6, essentialAddress = essential, distantAddress = distant } };
                var actor = new GameObject("Fixture target"); SceneManager.MoveGameObjectToScene(actor, preview); target = actor.transform; target.position = definition.playerStart;
                var root = new GameObject("Fixture World"); SceneManager.MoveGameObjectToScene(root, preview);
                streamer = root.AddComponent<WorldChunkStreamer>(); streamer.definition = definition; streamer.player = target; streamer.persistToDisk = false;
                collisionObserved = false; failure = null; phase = 0;
                streamer.CollisionReady += coordinate => { if (coordinate == new Vector2Int(3, 2)) collisionObserved = true; };
                streamer.LoadFailed += address => failure = "Unexpected load failure: " + address;
                AssetDatabase.SaveAssets();
                if (!streamer.Begin()) throw new InvalidOperationException("Fixture streamer failed to start.");
                deadline = EditorApplication.timeSinceStartup + 60;
                EditorApplication.update += Tick;
                AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            }
            catch (Exception error) { Finish("FAILED: " + error); }
        }
        static void Tick()
        {
            try
            {
                if (failure != null) throw new InvalidOperationException(failure);
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Streaming fixture timed out at phase " + phase);
                EditorApplication.QueuePlayerLoopUpdate();
                advanceDelayedActions(.01f, .1f);
                pumpResources(.02f);
                streamer.Tick();
                if (streamer.PendingLoadCount > definition.maxConcurrentLoads) throw new InvalidOperationException("Concurrent load budget exceeded.");
                var state = streamer.GetComponentInChildren<WorldPersistentObject>(true);
                if (phase == 0 && state != null && streamer.PendingLoadCount == 0)
                {
                    if (!collisionObserved || !streamer.IsCollisionReady(target.position)) throw new InvalidOperationException("Gameplay loaded before collision.");
                    state.SetState("{\"opened\":true}");
                    target.position = new Vector3(5248, 0, 5248); streamer.RequestReplan(); phase = 1;
                }
                else if (phase == 1 && streamer.IsCollisionReady(target.position) && streamer.PendingLoadCount == 0)
                {
                    if (state != null || streamer.ResidentChunkCount != 1 || streamer.State.Count != 1)
                        throw new InvalidOperationException("Distant chunks were not released with their gameplay state captured.");
                    target.position = definition.playerStart; streamer.RequestReplan(); phase = 2;
                }
                else if (phase == 2 && state != null && streamer.PendingLoadCount == 0)
                {
                    if (state.CaptureState() != "{\"opened\":true}") throw new InvalidOperationException("Revisited chunk lost persistent state.");
                    streamer.End();
                    if (streamer.ResidentChunkCount != 0 || streamer.transform.childCount != 0) throw new InvalidOperationException("Streamer shutdown left chunk objects behind.");
                    // Cancel immediately after starting another real asynchronous request.
                    if (!streamer.Begin()) throw new InvalidOperationException("Streamer restart failed.");
                    streamer.Tick(); streamer.End(); phase = 3;
                }
                else if (phase == 3)
                {
                    // Completion callbacks must release late instances after shutdown.
                    if (preview.GetRootGameObjects().Any(go => go.name.Contains("Essential(Clone)") || go.name.Contains("Gameplay(Clone)")))
                        throw new InvalidOperationException("Cancelled load left an orphan instance.");
                    if (streamer.PendingLoadCount != 0) return;
                    Finish("6 Addressables streaming checks passed: essential-first loading, bounded concurrency, distant unload, persistent revisit, shutdown cleanup, cancellation cleanup.");
                }
            }
            catch (Exception error) { Finish("FAILED: " + error); }
        }
        static void BeforeReload() => Finish("Cancelled by assembly reload; rerun streaming checks.");
        static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            try
            {
                if (streamer != null) streamer.End();
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
                if (settings != null && group != null) settings.RemoveGroup(group);
                if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                streamer = null; definition = null; group = null; target = null;
                SessionState.SetString(StatusKey, result);
                Directory.CreateDirectory("Library/Nusantara"); File.WriteAllText("Library/Nusantara/StreamingChecks.txt", result);
                Debug.Log(result);
            }
        }
    }
}
