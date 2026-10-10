using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[Serializable] public sealed class WeaponUISlot
{
    public Button button;
    public Image icon, background;
    public TMP_Text title, item;
}
[Serializable] public sealed class WeaponUICard
{
    public Button button;
    public Image icon, background;
    public TMP_Text title, bonus, status;
}

/// <summary>Image-based equipment menu. Catalog entries are available demo items; an inventory can supply a filtered catalog later.</summary>
public sealed class WeaponEquipmentUI : MonoBehaviour
{
    public PlayerWeaponEquipment equipment;
    public WeaponAccessoryDefinition[] catalog;
    public GameObject panel;
    public Button openButton, closeButton, attachButton, removeButton, upgradeButton;
    public Button[] tabs;
    public Image[] tabBackgrounds;
    public WeaponUISlot[] slots;
    public WeaponUICard[] cards;
    public Image weaponImage, selectedImage;
    public TMP_Text weaponName, weaponCaption, levelLabel, slotTitle, selectionTitle, selectionDescription, attachLabel, upgradeLabel, feedback;
    public TMP_Text[] liveStats, comparisonStats;
    public RectTransform safeContent;
    public RectTransform cardContent;
    public ScrollRect accessoryScroll;
    public PlayerCombatMode SelectedWeapon { get; private set; } = PlayerCombatMode.Sword;
    public WeaponAccessorySlot SelectedSlot { get; private set; }
    public WeaponAccessoryDefinition SelectedAccessory { get; private set; }
    public bool IsOpen => panel != null && panel.activeSelf;
    WeaponAccessoryDefinition[] visible = Array.Empty<WeaponAccessoryDefinition>();
    PlayerInput input;
    bool bound, ownsPause, restoreInput;
    float previousTimeScale;
    CursorLockMode previousCursorLock;
    bool previousCursorVisible;
    static readonly Color Gold = new Color(.86f,.70f,.43f);
    static readonly Color Dark = new Color(.065f,.082f,.094f);
    static readonly Color Selected = new Color(.16f,.19f,.19f);

    void Awake()
    {
        if (equipment == null) equipment = FindFirstObjectByType<PlayerWeaponEquipment>();
        if (equipment == null || panel == null) { enabled = false; return; }
        input = equipment.GetComponent<PlayerInput>();
        Bind();
        panel.SetActive(false);
        SelectWeapon(PlayerCombatMode.Sword);
    }
    void OnEnable() { if (equipment != null) equipment.EquipmentChanged += OnEquipmentChanged; }
    void OnDisable()
    {
        Close();
        if (equipment != null) equipment.EquipmentChanged -= OnEquipmentChanged;
    }
    void Bind()
    {
        if (bound) return;
        bound = true;
        openButton.onClick.AddListener(Toggle);
        closeButton.onClick.AddListener(Close);
        attachButton.onClick.AddListener(AttachSelected);
        removeButton.onClick.AddListener(RemoveSelectedSlot);
        upgradeButton.onClick.AddListener(UpgradeSelectedWeapon);
        var modes = new[] { PlayerCombatMode.Sword, PlayerCombatMode.Bow, PlayerCombatMode.Magic };
        for (int i = 0; i < tabs.Length; i++) { var mode = modes[i]; tabs[i].onClick.AddListener(() => SelectWeapon(mode)); }
        for (int i = 0; i < slots.Length; i++) { int index = i; slots[i].button.onClick.AddListener(() => SelectSlot(index)); }
        for (int i = 0; i < cards.Length; i++) { int index = i; cards[i].button.onClick.AddListener(() => SelectAccessory(index)); }
    }
    void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.iKey.wasPressedThisFrame) Toggle();
        else if (IsOpen && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }
    void LateUpdate()
    {
        if (safeContent == null) return;
        var canvas = GetComponent<Canvas>();
        float scale = Mathf.Max(.001f, canvas.scaleFactor);
        Rect area = Screen.safeArea;
        safeContent.localScale = Vector3.one * Mathf.Min(1, area.width / scale / 1600, area.height / scale / 900);
        safeContent.anchoredPosition = (area.center - new Vector2(Screen.width, Screen.height) * .5f) / scale;
    }
    public void Toggle() { if (IsOpen) Close(); else Open(); }
    public void Open()
    {
        if (equipment == null || IsOpen) return;
        // The compact gem dropdown releases its cursor ownership before this modal takes it.
        foreach (var gems in FindObjectsByType<ElementalGems.GemSelectionUI>()) gems.Close();
        if (Application.isPlaying)
        {
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState; previousCursorVisible = Cursor.visible;
            restoreInput = input != null && input.inputIsActive;
            if (restoreInput) input.DeactivateInput();
            ownsPause = true;
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        panel.SetActive(true);
        openButton.gameObject.SetActive(false);
        Refresh();
        feedback.text = "DMG per hit / TIME per action / KB before resistance. Ranges cover combo steps or tap / held attacks.";
    }
    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        if (openButton != null) openButton.gameObject.SetActive(true);
        if (!ownsPause) return;
        ownsPause = false;
        Time.timeScale = previousTimeScale;
        if (restoreInput && input != null) input.ActivateInput();
        restoreInput = false;
        Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
    }
    public void SelectWeapon(PlayerCombatMode mode)
    {
        var loadout = equipment != null ? equipment.Loadout(mode) : null;
        if (loadout?.definition == null || !loadout.definition.IsValid) return;
        SelectedWeapon = mode;
        SelectSlot(0);
    }
    public void SelectSlot(int index)
    {
        var definition = equipment.Loadout(SelectedWeapon)?.definition;
        if (definition == null || index < 0 || index >= definition.slots.Length) return;
        SelectedSlot = definition.slots[index];
        visible = (catalog ?? Array.Empty<WeaponAccessoryDefinition>()).Where(a => a != null && a.weapon == SelectedWeapon && a.slot == SelectedSlot).Distinct().ToArray();
        SelectedAccessory = equipment.Loadout(SelectedWeapon).Equipped(SelectedSlot);
        if (SelectedAccessory == null || !visible.Contains(SelectedAccessory)) SelectedAccessory = visible.FirstOrDefault();
        if (accessoryScroll != null) accessoryScroll.verticalNormalizedPosition = 1;
        feedback.text = "DMG per hit / TIME per action / KB before resistance. Ranges cover combo steps or tap / held attacks.";
        Refresh();
    }
    public void SelectAccessory(int index)
    {
        if (index < 0 || index >= visible.Length) return;
        SelectedAccessory = visible[index]; Refresh();
    }
    public void AttachSelected()
    {
        if (SelectedAccessory == null || !visible.Contains(SelectedAccessory)) return;
        if (equipment.TryEquip(SelectedWeapon, SelectedSlot, SelectedAccessory))
        { Refresh(); feedback.text = DisplayName(SelectedAccessory) + " attached. Applies to the next attack."; }
    }
    public void RemoveSelectedSlot()
    {
        if (equipment.TryEquip(SelectedWeapon, SelectedSlot, null))
        { Refresh(); feedback.text = SelectedSlot + " cleared. Accessory remains in your collection."; }
    }
    public void UpgradeSelectedWeapon()
    {
        var loadout = equipment.Loadout(SelectedWeapon);
        if (loadout != null && equipment.TrySetUpgradeLevel(SelectedWeapon, loadout.upgradeLevel + 1))
        { Refresh(); feedback.text = WeaponTitle(loadout.definition) + " upgraded to level " + loadout.upgradeLevel + "."; }
    }
    void OnEquipmentChanged(PlayerCombatMode _) { if (equipment != null) Refresh(); }
    public void Refresh()
    {
        var loadout = equipment?.Loadout(SelectedWeapon);
        var definition = loadout?.definition;
        if (definition == null || !definition.IsValid) return;
        weaponImage.sprite = definition.artwork;
        weaponImage.color = Color.white;
        weaponName.text = WeaponTitle(definition);
        weaponCaption.text = SelectedWeapon == PlayerCombatMode.Magic ? "ARCANE BRACELET  /  " + definition.slots.Length + " ATTACHMENT SLOTS" : "MODULAR " + SelectedWeapon.ToString().ToUpperInvariant() + "  /  " + definition.slots.Length + " ATTACHMENT SLOTS";
        levelLabel.text = "LEVEL " + loadout.upgradeLevel.ToString("00") + " / " + definition.maxUpgradeLevel.ToString("00");
        upgradeButton.interactable = loadout.upgradeLevel < definition.maxUpgradeLevel;
        upgradeLabel.text = upgradeButton.interactable ? "UPGRADE  +1" : "MAX LEVEL";
        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = i == (SelectedWeapon == PlayerCombatMode.Sword ? 0 : SelectedWeapon == PlayerCombatMode.Bow ? 1 : 2);
            tabBackgrounds[i].color = active ? Selected : Dark;
            tabs[i].GetComponentInChildren<TMP_Text>().color = active ? Gold : new Color(.92f,.91f,.86f);
        }
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].button.gameObject.SetActive(i < definition.slots.Length);
            if (i >= definition.slots.Length) continue;
            var slot = definition.slots[i];
            var accessory = loadout.Equipped(slot);
            slots[i].title.text = slot.ToString().ToUpperInvariant();
            slots[i].item.text = accessory != null ? "ATTACHED" : "EMPTY  +";
            slots[i].item.color = accessory != null ? new Color(.42f,.86f,.77f) : Gold;
            var example = accessory != null ? accessory : catalog?.FirstOrDefault(a => a != null && a.weapon == SelectedWeapon && a.slot == slot);
            slots[i].icon.sprite = example != null ? example.icon : null;
            slots[i].icon.color = accessory != null ? Color.white : new Color(1,1,1,.25f);
            slots[i].background.color = SelectedSlot == slot ? Selected : Dark;
        }
        slotTitle.text = SelectedSlot.ToString().ToUpperInvariant() + " ACCESSORIES";
        var equipped = loadout.Equipped(SelectedSlot);
        if (cardContent != null) cardContent.sizeDelta = new Vector2(cardContent.sizeDelta.x, visible.Length * 106);
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].button.gameObject.SetActive(i < visible.Length);
            if (i >= visible.Length) continue;
            var accessory = visible[i];
            cards[i].icon.sprite = accessory.icon;
            cards[i].title.text = DisplayName(accessory);
            cards[i].bonus.text = BonusSummary(accessory);
            cards[i].status.text = accessory == equipped ? "ATTACHED" : accessory == SelectedAccessory ? "SELECTED" : "AVAILABLE";
            cards[i].background.color = accessory == SelectedAccessory ? Selected : Dark;
        }
        selectedImage.sprite = SelectedAccessory != null ? SelectedAccessory.icon : null;
        selectedImage.enabled = SelectedAccessory != null;
        selectionTitle.text = SelectedAccessory != null ? DisplayName(SelectedAccessory) : "No accessory available";
        selectionDescription.text = SelectedAccessory != null ? SelectedAccessory.description : "No accessories are available for this slot yet.";
        var preview = new WeaponLoadout { definition = definition, upgradeLevel = loadout.upgradeLevel,
            accessories = loadout.accessories != null ? new System.Collections.Generic.List<WeaponAccessoryDefinition>(loadout.accessories) : new System.Collections.Generic.List<WeaponAccessoryDefinition>() };
        if (SelectedAccessory != null) preview.TryEquip(SelectedSlot, SelectedAccessory);
        for (int i = 0; i < 3; i++)
        {
            var inputKind = (CombatAttackInput)i;
            var current = WeaponAttackReadout.Capture(equipment, loadout, inputKind);
            var next = WeaponAttackReadout.Capture(equipment, preview, inputKind);
            string title = i == 0 ? "LIGHT" : i == 1 ? "HEAVY" : "SPECIAL";
            liveStats[i].fontSize = 12;
            comparisonStats[i].fontSize = 12;
            liveStats[i].text = title + "\n" + StatLine(current);
            comparisonStats[i].text = title + "   " + Compare(current,next);
        }
        attachButton.interactable = SelectedAccessory != null && SelectedAccessory != equipped;
        attachLabel.text = SelectedAccessory == null ? "NO ACCESSORY" : SelectedAccessory == equipped ? "ATTACHED" : "ATTACH ACCESSORY";
        removeButton.interactable = equipped != null;
    }
    static string StatLine(WeaponAttackReadout stats) => stats.Available
        ? $"DMG {stats.Damage}\nTIME {stats.Time}   KB {stats.Knockback}"
        : "NOT CONFIGURED\nAttack values unavailable";
    static string Compare(WeaponAttackReadout a, WeaponAttackReadout b)
    {
        return a.Available && b.Available
            ? $"DMG {a.Damage} > {b.Damage}\nTIME {a.Time} > {b.Time}   KB {a.Knockback} > {b.Knockback}"
            : "NOT CONFIGURED\nNo gameplay attack values yet";
    }
    public static string WeaponTitle(WeaponDefinition weapon) => !string.IsNullOrWhiteSpace(weapon.displayName) ? weapon.displayName : weapon.weapon.ToString();
    public static string DisplayName(WeaponAccessoryDefinition item) => !string.IsNullOrWhiteSpace(item.displayName) ? item.displayName : item.name.Replace('_',' ');
    public static string BonusSummary(WeaponAccessoryDefinition item)
    {
        if (item.modifiers == null) return "No stat change";
        return string.Join(" / ", item.modifiers.Where(m=>m != null).Select(m =>
            (m.attacks == WeaponAttackMask.All ? "" : m.attacks + (m.attackNumber > 0 ? " " + m.attackNumber : "") + ": ") +
            $"{m.damagePercent:+0.#;-0.#;0}% DMG  {m.agilityPercent:+0.#;-0.#;0}% SPD\n{m.knockbackDurationPercent:+0.#;-0.#;0}% KNOCKBACK TIME" +
            (Mathf.Abs(m.flatDamage) > .001f ? $"  {m.flatDamage:+0.#;-0.#} flat" : "")));
    }
}
