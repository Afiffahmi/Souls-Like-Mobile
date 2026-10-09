using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ElementalGems
{
    [Serializable] public sealed class GemCard
    {
        public GemDefinition gem;
        public UnityEngine.UI.Button button;
        public UnityEngine.UI.Image background;
    }
    public sealed class GemSelectionUI : MonoBehaviour
    {
        public GemManager manager;
        public GameObject panel;
        public UnityEngine.UI.Button openButton, closeButton, equipButton;
        public UnityEngine.UI.Image selectedIcon;
        public TMP_Text selectedName, description, matchup, equippedLabel, equipLabel;
        public GemCard[] cards;
        [Header("Compact equipped slot")]
        public bool compact;
        public UnityEngine.UI.Button previousButton, nextButton;
        public UnityEngine.UI.Image equippedIcon;
        public TMP_Text dropdownIndicator;
        public RectTransform slot;
        private GemDefinition selected;
        private PlayerInput input;
        private bool restoreInput;
        private CursorLockMode cursorMode;
        private bool cursorVisible;
        private void Awake()
        {
            input = manager.GetComponent<PlayerInput>();
            openButton.onClick.AddListener(Toggle);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (equipButton != null) equipButton.onClick.AddListener(EquipSelected);
            if (previousButton != null) previousButton.onClick.AddListener(() => Cycle(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Cycle(1));
            foreach (var card in cards) { var captured = card; card.button.onClick.AddListener(() => { Select(captured.gem); if (compact) { EquipSelected(); Close(); } }); }
            manager.GemChanged += OnGemChanged;
            OnGemChanged(manager.Equipped);
        }
        private void Update()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.gKey.wasPressedThisFrame) Toggle();
                if (panel.activeSelf && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
            }
            if (!compact || !panel.activeSelf) return;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) CloseOutside(Mouse.current.position.ReadValue());
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) CloseOutside(Touchscreen.current.primaryTouch.position.ReadValue());
        }
        public void Toggle() { if (panel.activeSelf) Close(); else Open(); }
        private void CloseOutside(Vector2 point)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)panel.transform, point) &&
                !RectTransformUtility.RectangleContainsScreenPoint(slot, point)) Close();
        }
        public void Cycle(int direction)
        {
            if (manager.gems == null || manager.gems.Length == 0) return;
            int index = Array.FindIndex(manager.gems, g => g != null && g.element == manager.EquippedElement);
            for (int i = 0; i < manager.gems.Length; i++)
            {
                index = (index + (direction < 0 ? -1 : 1) + manager.gems.Length) % manager.gems.Length;
                if (manager.gems[index] == null) continue;
                Select(manager.gems[index]); EquipSelected(); Close(); return;
            }
        }
        public void Open()
        {
            if (panel.activeSelf) return;
            cursorMode = Cursor.lockState; cursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            restoreInput = input != null && input.inputIsActive;
            if (restoreInput && !compact) input.DeactivateInput();
            panel.SetActive(true); Select(manager.Equipped);
            if (dropdownIndicator != null) dropdownIndicator.text = "-";
        }
        public void Close()
        {
            if (!panel.activeSelf) return;
            panel.SetActive(false);
            if (restoreInput && input != null && !compact) input.ActivateInput();
            restoreInput = false; Cursor.lockState = cursorMode; Cursor.visible = cursorVisible;
            if (dropdownIndicator != null) dropdownIndicator.text = "+";
        }
        public void Select(GemDefinition gem)
        {
            if (gem == null) return;
            selected = gem;
            if (selectedName != null) selectedName.text = gem.displayName;
            if (description != null) description.text = gem.description;
            if (selectedIcon != null) { selectedIcon.sprite = gem.icon; selectedIcon.color = Color.white; }
            string relations = "";
            foreach (var row in gem.matchups) relations += $"{row.defender}  {row.multiplier:0.##}x\n";
            if (matchup != null) matchup.text = relations.Length > 0 ? relations.TrimEnd() : "Neutral matchups  1x";
            foreach (var card in cards) card.background.color = card.gem == gem ? new Color(0.18f, 0.26f, 0.36f) : new Color(0.075f, 0.105f, 0.16f);
            bool current = manager.EquippedElement == gem.element;
            if (equipButton != null) equipButton.interactable = !current;
            if (equipLabel != null) equipLabel.text = current ? "EQUIPPED" : gem.element == ElementType.Normal ? "REMOVE GEM" : "EQUIP GEM";
        }
        public void EquipSelected() { if (selected != null) manager.Equip(selected.element); }
        private void OnGemChanged(GemDefinition gem)
        {
            equippedLabel.text = (compact ? "" : "GEMS  /  ") + (gem != null ? gem.displayName : "Normal");
            if (equippedIcon != null) { equippedIcon.sprite = gem != null ? gem.icon : null; equippedIcon.color = Color.white; }
            Select(compact ? gem : selected != null ? selected : gem);
        }
        private void OnDisable() { if (panel != null) Close(); }
        private void OnDestroy() { if (manager != null) manager.GemChanged -= OnGemChanged; }
    }
}
