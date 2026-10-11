using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace SoulsLike.Riding
{
    [DisallowMultipleComponent, RequireComponent(typeof(HorseController))]
    public sealed class HorseInteractionSystem : MonoBehaviour
    {
        [Tooltip("Assign your player here. Empty prefab references resolve the local player at runtime.")]
        public MountedPlayerController player;
        public HorseController horse;
        [Min(.1f)] public float interactionDistance = 3f;
        public Key interactionKey = Key.E;
        public Canvas promptCanvas;
        public UnityEngine.UI.Button promptButton;
        public TMP_Text promptText;
        public Vector3 promptOffset = new Vector3(0,2.5f,0);
        private Camera view;
        private static int nextPriority;
        private int priority;
        private static readonly HashSet<HorseInteractionSystem> active = new HashSet<HorseInteractionSystem>();
        private void OnEnable() { priority = ++nextPriority; active.Add(this); }
        private void OnDisable() { active.Remove(this); if (promptCanvas) promptCanvas.gameObject.SetActive(false); }
        private void Awake()
        {
            if (!horse) horse = GetComponent<HorseController>();
            if (promptButton) promptButton.onClick.AddListener(Interact);
        }
        private void Start() { if (!player) player = Object.FindAnyObjectByType<MountedPlayerController>(); }
        public bool Available
        {
            get
            {
                if (!player || !horse || horse.IsDead || Time.timeScale <= 0) return false;
                if (player.Horse == horse) return player.IsRiding && horse.IsIdle;
                if (!player.CanMount || horse.Rider || Vector3.Distance(player.transform.position,transform.position)>interactionDistance) return false;
                // Only one horse owns the keyboard/prompt when interaction ranges overlap.
                float distance = (player.transform.position-transform.position).sqrMagnitude;
                foreach(var other in active)
                    if (other != this && other.player == player && other.horse && !other.horse.IsDead && !other.horse.Rider)
                    {
                        float d = (player.transform.position-other.transform.position).sqrMagnitude;
                        if (d <= other.interactionDistance*other.interactionDistance && (d<distance || (Mathf.Approximately(d,distance) && other.priority<priority))) return false;
                    }
                return true;
            }
        }
        private void Update()
        {
            if (Available && interactionKey != Key.None && Keyboard.current != null && Keyboard.current[interactionKey].wasPressedThisFrame) Interact();
        }
        private void LateUpdate()
        {
            if (!promptCanvas) return;
            if (!view) view = Camera.main;
            bool visible = Available;
            if (view)
            {
                Vector3 v = view.WorldToViewportPoint(transform.position+promptOffset);
                visible &= v.z>0 && v.x>=0 && v.x<=1 && v.y>=0 && v.y<=1;
                promptCanvas.worldCamera = view;
                promptCanvas.transform.SetPositionAndRotation(transform.position+promptOffset,view.transform.rotation);
            }
            promptCanvas.gameObject.SetActive(visible);
            if (visible && promptText) promptText.text = player.Horse == horse ? "Dismount" : "Mount";
        }
        public void Interact()
        {
            if (!Available) return;
            if (player.Horse == horse) player.TryDismount(); else player.TryMount(horse,interactionDistance);
        }
        private void OnDestroy() { if (promptButton) promptButton.onClick.RemoveListener(Interact); }
    }
}
