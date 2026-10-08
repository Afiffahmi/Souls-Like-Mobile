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
        private GemDefinition selected;
        private PlayerInput input;
        private bool restoreInput;
        private CursorLockMode cursorMode;
        private bool cursorVisible;
        private void Awake()
        {
            input = manager.GetComponent<PlayerInput>();
            openButton.onClick.AddListener(Open); closeButton.onClick.AddListener(Close);
            equipButton.onClick.AddListener(EquipSelected);
            foreach (var card in cards) { var captured = card; card.button.onClick.AddListener(() => Select(captured.gem)); }
            manager.GemChanged += OnGemChanged;
            OnGemChanged(manager.Equipped);
        }
        private void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.gKey.wasPressedThisFrame) { if (panel.activeSelf) Close(); else Open(); }
            if (panel.activeSelf && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }
        public void Open()
        {
            if (panel.activeSelf) return;
            cursorMode = Cursor.lockState; cursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            restoreInput = input != null && input.inputIsActive;
            if (restoreInput) input.DeactivateInput();
            panel.SetActive(true); Select(manager.Equipped);
        }
        public void Close()
        {
            if (!panel.activeSelf) return;
            panel.SetActive(false);
            if (restoreInput && input != null) input.ActivateInput();
            restoreInput = false; Cursor.lockState = cursorMode; Cursor.visible = cursorVisible;
        }
        public void Select(GemDefinition gem)
        {
            if (gem == null) return;
            selected = gem; selectedName.text = gem.displayName; description.text = gem.description;
            selectedIcon.sprite = gem.icon; selectedIcon.color = Color.white;
            string relations = "";
            foreach (var row in gem.matchups) relations += $"{row.defender}  {row.multiplier:0.##}x\n";
            matchup.text = relations.Length > 0 ? relations.TrimEnd() : "Neutral matchups  1x";
            foreach (var card in cards) card.background.color = card.gem == gem ? new Color(0.18f, 0.26f, 0.36f) : new Color(0.075f, 0.105f, 0.16f);
            bool current = manager.EquippedElement == gem.element;
            equipButton.interactable = !current; equipLabel.text = current ? "EQUIPPED" : gem.element == ElementType.Normal ? "REMOVE GEM" : "EQUIP GEM";
        }
        public void EquipSelected() { if (selected != null) manager.Equip(selected.element); }
        private void OnGemChanged(GemDefinition gem)
        {
            equippedLabel.text = "GEMS  /  " + (gem != null ? gem.displayName.ToUpperInvariant() : "NORMAL");
            Select(selected != null ? selected : gem);
        }
        private void OnDisable() { if (panel != null) Close(); }
        private void OnDestroy() { if (manager != null) manager.GemChanged -= OnGemChanged; }
    }
}
