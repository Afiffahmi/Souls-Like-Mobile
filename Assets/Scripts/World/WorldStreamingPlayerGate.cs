using UnityEngine;

namespace Nusantara.World
{
    [DefaultExecutionOrder(-8000)]
    public sealed class WorldStreamingPlayerGate : MonoBehaviour
    {
        public WorldChunkStreamer streamer;
        public CharacterController controller;
        [Tooltip("Assign movement scripts only. These are suspended during initial loading or a teleport to unloaded ground.")]
        public Behaviour[] movementScripts = System.Array.Empty<Behaviour>();
        bool[] previousEnabled;
        bool suspended, controllerWasEnabled, hasSafePosition, started;
        Vector3 safePosition;

        void OnEnable() { hasSafePosition = false; if (started) Suspend(); }
        // Wait for the existing player's Awake before disabling its movement component.
        void Start() { started = true; Suspend(); }
        void Update()
        {
            if (streamer == null || streamer.player == null || !streamer.Running) return;
            if (streamer.IsCollisionReady(streamer.player.position)) Resume(); else Suspend();
        }
        void LateUpdate()
        {
            if (streamer == null || streamer.player == null || !streamer.Running) return;
            if (streamer.IsCollisionReady(streamer.player.position)) { safePosition = streamer.player.position; hasSafePosition = true; return; }
            Suspend();
            if (hasSafePosition) streamer.player.position = safePosition;
        }
        void Suspend()
        {
            if (suspended) return;
            suspended = true;
            previousEnabled = new bool[movementScripts.Length];
            for (int i = 0; i < movementScripts.Length; i++)
                if (movementScripts[i] != null && movementScripts[i] != this && movementScripts[i] != streamer)
                { previousEnabled[i] = movementScripts[i].enabled; movementScripts[i].enabled = false; }
            if (controller != null) { controllerWasEnabled = controller.enabled; controller.enabled = false; }
        }
        void Resume()
        {
            if (!suspended) return;
            suspended = false;
            if (controller != null) controller.enabled = controllerWasEnabled;
            for (int i = 0; i < movementScripts.Length && i < previousEnabled.Length; i++)
                if (movementScripts[i] != null && movementScripts[i] != this && movementScripts[i] != streamer) movementScripts[i].enabled = previousEnabled[i];
        }
        void OnDisable() => Resume();
    }
}
