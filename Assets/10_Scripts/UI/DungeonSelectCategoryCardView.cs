using System;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DungeonSelectCategoryCardView :
    MonoBehaviour,
    IPointerEnterHandler,
    ISelectHandler
{
    [SerializeField] private Button button;
    [SerializeField] private UiMaskedCoverImageView coverView;
    [SerializeField] private Image informationPanel;
    [SerializeField] private Image selectionBar;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI countText;

    private static readonly Color NormalPanelColor =
        new(0.035f, 0.045f, 0.04f, 0.98f);
    private static readonly Color SelectedPanelColor =
        new(0.91f, 0.90f, 0.84f, 1f);
    private static readonly Color NormalTextColor =
        new(0.91f, 0.93f, 0.88f, 1f);
    private static readonly Color SelectedTextColor =
        new(0.08f, 0.10f, 0.09f, 1f);

    private DungeonCategorySO _category;
    private Action<DungeonCategorySO> _preview;
    private Action<DungeonCategorySO> _open;
    private bool _selected;

    public DungeonCategorySO Category => _category;
    public Button Button => button;
    public bool HasDesignerReferences => button != null && coverView != null &&
        coverView.HasDesignerReferences &&
        informationPanel != null && selectionBar != null &&
        titleText != null && descriptionText != null && countText != null;

    public void Configure(
        DungeonCategorySO category,
        Action<DungeonCategorySO> preview,
        Action<DungeonCategorySO> open)
    {
        _category = category;
        _preview = preview;
        _open = open;
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
        RefreshContent();
        SetSelected(false);
    }

    public void RefreshContent()
    {
        if (_category == null)
            return;
        coverView?.Configure(_category.CardSprite, _category.CardFraming);
        if (titleText != null)
        {
            titleText.text = ResolveText(
                _category.TitleLocalizationKey,
                _category.FallbackTitle);
        }
        if (descriptionText != null)
        {
            descriptionText.text = ResolveText(
                _category.DescriptionLocalizationKey,
                _category.FallbackDescription);
        }
        if (countText != null)
        {
            countText.text = _category.ResolveDungeons().Count.ToString("D2");
        }
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (informationPanel != null)
        {
            informationPanel.color = selected
                ? SelectedPanelColor
                : NormalPanelColor;
        }
        if (selectionBar != null)
        {
            selectionBar.color = _category != null
                ? _category.AccentColor
                : Color.white;
            selectionBar.gameObject.SetActive(selected);
        }
        if (titleText != null)
            titleText.color = selected ? SelectedTextColor : NormalTextColor;
        if (descriptionText != null)
        {
            descriptionText.color = selected
                ? SelectedTextColor
                : NormalTextColor;
        }
        if (countText != null)
            countText.color = selected ? SelectedTextColor : NormalTextColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Preview();
    }

    public void OnSelect(BaseEventData eventData)
    {
        Preview();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    private void Preview()
    {
        if (_category != null)
            _preview?.Invoke(_category);
    }

    private void HandleClick()
    {
        if (_category != null)
            _open?.Invoke(_category);
    }

    private static string ResolveText(string key, string fallback)
    {
        return !string.IsNullOrWhiteSpace(key) &&
               LocalizationService.TryGet(key, out string localized)
            ? localized
            : fallback ?? string.Empty;
    }
}
