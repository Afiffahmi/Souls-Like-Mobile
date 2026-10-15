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
        private RectTransform overlayPrompt;
        private Vector3 promptWorldScale;
        private static int nextPriority;
        private int priority;
        private static readonly HashSet<HorseInteractionSystem> active = new HashSet<HorseInteractionSystem>();
        private void OnEnable() { priority = ++nextPriority; active.Add(this); }
        private void OnDisable() { active.Remove(this); if (promptCanvas) promptCanvas.gameObject.SetActive(false); }
        private void Awake()
        {
            if (!horse) horse = GetComponent<HorseController>();
            if (promptButton) promptButton.onClick.AddListener(Interact);
            PrepareOverlayPrompt();
        }
        private void PrepareOverlayPrompt()
        {
            if (!promptCanvas || !promptButton || promptCanvas.renderMode != RenderMode.WorldSpace) return;
            var content = promptButton.transform as RectTransform;
            var canvasRect = promptCanvas.transform as RectTransform;
            if (!content || !canvasRect || content.parent != canvasRect) return;

            // Keep the existing interactive prompt, but draw it after scene effects.
            Vector2 size = canvasRect.rect.size;
            promptWorldScale = canvasRect.lossyScale;
            canvasRect.SetParent(null, false);
            canvasRect.localScale = Vector3.one;
            canvasRect.localRotation = Quaternion.identity;
            promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            promptCanvas.worldCamera = null;
            content.anchorMin = content.anchorMax = new Vector2(.5f, .5f);
            content.sizeDelta = size;
            content.anchoredPosition = Vector2.zero;
            overlayPrompt = content;
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
            if (Available && !Nusantara.World.WorldRegionPortal.OwnsInteractionKeyFor(player.transform) && interactionKey != Key.None && Keyboard.current != null && Keyboard.current[interactionKey].wasPressedThisFrame) Interact();
        }
        private void LateUpdate()
        {
            if (!promptCanvas) return;
            if (!view || !view.isActiveAndEnabled) view = Camera.main;
            bool visible = Available && view;
            if (view)
            {
                Vector3 worldPosition = transform.position + promptOffset;
                Vector3 v = view.WorldToViewportPoint(worldPosition);
                visible &= v.z>0 && v.x>=0 && v.x<=1 && v.y>=0 && v.y<=1;
                bool becomingVisible = visible && !promptCanvas.gameObject.activeSelf;
                promptCanvas.gameObject.SetActive(visible);
                if (overlayPrompt && visible)
                {
                    promptCanvas.targetDisplay = view.targetDisplay;
                    if (becomingVisible) Canvas.ForceUpdateCanvases();
                    Vector3 screen = view.WorldToScreenPoint(worldPosition);
                    var canvasRect = (RectTransform)promptCanvas.transform;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
                    overlayPrompt.anchoredPosition = local;
                    // Preserve the original apparent size, including perspective/distance.
                    float x = Vector3.Distance(screen, view.WorldToScreenPoint(worldPosition + view.transform.right * Mathf.Abs(promptWorldScale.x)));
                    float y = Vector3.Distance(screen, view.WorldToScreenPoint(worldPosition + view.transform.up * Mathf.Abs(promptWorldScale.y)));
                    float canvasScale = Mathf.Max(.001f, promptCanvas.scaleFactor);
                    overlayPrompt.localScale = new Vector3(x / canvasScale, y / canvasScale, 1);
                }
                else if (!overlayPrompt)
                {
                    promptCanvas.worldCamera = view;
                    promptCanvas.transform.SetPositionAndRotation(worldPosition, view.transform.rotation);
                }
            }
            promptCanvas.gameObject.SetActive(visible);
            if (visible && promptText) promptText.text = player.Horse == horse ? "Dismount" : "Mount";
        }
        public void Interact()
        {
            if (!Available) return;
            if (player.Horse == horse) player.TryDismount(); else player.TryMount(horse,interactionDistance);
        }
        private void OnDestroy()
        {
            if (promptButton) promptButton.onClick.RemoveListener(Interact);
            if (overlayPrompt && promptCanvas) Destroy(promptCanvas.gameObject);
        }
    }
}
