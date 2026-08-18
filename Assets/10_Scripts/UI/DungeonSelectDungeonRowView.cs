using System;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DungeonSelectDungeonRowView :
    MonoBehaviour,
    IPointerEnterHandler,
    ISelectHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image selectionBar;
    [SerializeField] private TextMeshProUGUI sequenceText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI stateText;

    private static readonly Color NormalColor =
        new(0.055f, 0.07f, 0.06f, 0.92f);
    private static readonly Color SelectedColor =
        new(0.84f, 0.84f, 0.78f, 1f);
    private static readonly Color NormalTextColor =
        new(0.91f, 0.93f, 0.88f, 1f);
    private static readonly Color SelectedTextColor =
        new(0.08f, 0.10f, 0.09f, 1f);

    private DungeonDefinition _definition;
    private Action<DungeonDefinition> _select;

    public DungeonDefinition Definition => _definition;
    public Button Button => button;
    public bool HasDesignerReferences => button != null &&
        background != null && selectionBar != null &&
        sequenceText != null && titleText != null && stateText != null;

    public void Configure(
        DungeonDefinition definition,
        int index,
        string state,
        Action<DungeonDefinition> select)
    {
        _definition = definition;
        _select = select;
        if (button != null)
        {
            button.onClick.RemoveListener(HandleSelect);
            button.onClick.AddListener(HandleSelect);
        }
        if (sequenceText != null)
        {
            sequenceText.text = definition != null && definition.IsPractice
                ? "P"
                : (index + 1).ToString("D2");
        }
        if (titleText != null && definition != null)
        {
            titleText.text = ResolveText(
                definition.TitleLocalizationKey,
                definition.FallbackTitle);
        }
        if (stateText != null)
            stateText.text = state ?? string.Empty;
        SetSelected(false);
    }

    public void RefreshLocalizedContent(string state)
    {
        if (_definition == null)
            return;
        if (titleText != null)
        {
            titleText.text = ResolveText(
                _definition.TitleLocalizationKey,
                _definition.FallbackTitle);
        }
        if (stateText != null)
            stateText.text = state ?? string.Empty;
    }

    public void SetSelected(bool selected)
    {
        if (background != null)
            background.color = selected ? SelectedColor : NormalColor;
        if (selectionBar != null)
            selectionBar.gameObject.SetActive(selected);
        Color textColor = selected ? SelectedTextColor : NormalTextColor;
        if (sequenceText != null)
            sequenceText.color = textColor;
        if (titleText != null)
            titleText.color = textColor;
        if (stateText != null)
            stateText.color = textColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        HandleSelect();
    }

    public void OnSelect(BaseEventData eventData)
    {
        HandleSelect();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleSelect);
    }

    private void HandleSelect()
    {
        if (_definition != null)
            _select?.Invoke(_definition);
    }

    private static string ResolveText(string key, string fallback)
    {
        return !string.IsNullOrWhiteSpace(key) &&
               LocalizationService.TryGet(key, out string localized)
            ? localized
            : fallback ?? string.Empty;
    }
}
