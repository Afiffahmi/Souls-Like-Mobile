using TMPro;
using UnityEngine;

/// <summary>Fits the third weapon stat into both new and existing equipment menus.</summary>
public static class WeaponKnockbackUILayout
{
    public static void Apply(WeaponEquipmentUI ui)
    {
        foreach (var card in ui.cards)
        {
            Place(card.bonus, 96, 36, 358, 39);
            card.bonus.fontSize = 12;
            Place(card.status, 96, 79, 358, 14);
        }
        Place(ui.selectionDescription, 94, 319, 390, 46);
        foreach (var label in ui.panel.GetComponentsInChildren<TMP_Text>(true))
            if (label.text.StartsWith("ON ATTACH")) Place(label, 20, 370, 466, 18);
        for (int i = 0; i < ui.comparisonStats.Length; i++)
            Place(ui.comparisonStats[i], 20, 390 + i * 39, 466, 37);
    }
    static void Place(TMP_Text label, float x, float y, float width, float height)
    {
        // Builder labels stretch to a dedicated rectangle parent.
        var rect = (RectTransform)label.transform.parent;
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
