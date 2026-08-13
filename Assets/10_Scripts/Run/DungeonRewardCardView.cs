using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DungeonRewardCardView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image accentImage;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI footerText;

    public void Bind(
        string category,
        string title,
        string description,
        string footer,
        Color accent,
        Action action)
    {
        if (button == null || accentImage == null ||
            categoryText == null || titleText == null ||
            descriptionText == null || footerText == null)
        {
            Debug.LogError(
                "Dungeon reward card prefab references are incomplete.",
                this);
            return;
        }

        accentImage.color = accent;
        categoryText.text = category ?? string.Empty;
        titleText.text = title ?? string.Empty;
        descriptionText.text = description ?? string.Empty;
        footerText.text = footer ?? string.Empty;
        button.onClick.RemoveAllListeners();
        if (action != null)
            button.onClick.AddListener(() => action());
    }
}
