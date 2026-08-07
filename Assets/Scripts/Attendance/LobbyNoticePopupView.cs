using System;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LobbyNoticePopupView : MonoBehaviour
{
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _message;
    private Button _close;
    private ResponsivePanelFitter _panelFitter;
    private bool _built;

    public static LobbyNoticePopupView BuildOrBind(RectTransform parent)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find("grpMainNoticePopup");
        GameObject root = existing != null
            ? existing.gameObject
            : new GameObject(
                "grpMainNoticePopup",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
        if (existing == null)
            root.transform.SetParent(parent, false);
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color =
            new Color(0.01f, 0.015f, 0.012f, 0.86f);

        LobbyNoticePopupView view =
            root.GetComponent<LobbyNoticePopupView>() ??
            root.AddComponent<LobbyNoticePopupView>();
        view.BuildUi();
        root.SetActive(false);
        return view;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Awake()
    {
        BuildUi();
    }

    private void OnEnable()
    {
        _panelFitter?.RefreshLayout();
        LocalizationService.LocaleChanged += HandleLocaleChanged;
        Refresh();
    }

    private void OnRectTransformDimensionsChange()
    {
        _panelFitter?.RefreshLayout();
    }

    private void OnDisable()
    {
        LocalizationService.LocaleChanged -= HandleLocaleChanged;
    }

    private void OnDestroy()
    {
        LocalizationService.LocaleChanged -= HandleLocaleChanged;
    }

    private void BuildUi()
    {
        if (_built)
            return;

        Button backdrop = GetComponent<Button>();
        backdrop.onClick.RemoveAllListeners();
        backdrop.onClick.AddListener(Hide);

        GameObject panel = LobbyNoticePopupViewHelper.GetOrCreate(
            transform,
            "grpMainNoticePanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(700f, 400f);
        _panelFitter = ResponsivePanelFitter.Bind(
            panelRect,
            transform as RectTransform);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.065f, 0.085f, 0.072f, 0.99f);
        panelImage.raycastTarget = true;

        _title = LobbyNoticePopupViewHelper.CreateText(
            panel.transform,
            "txtMainNoticeTitle",
            34f,
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        LobbyNoticePopupViewHelper.SetRect(
            _title.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(56f, -86f),
            new Vector2(-56f, -28f));

        _message = LobbyNoticePopupViewHelper.CreateText(
            panel.transform,
            "txtMainNoticeMessage",
            22f,
            TextAlignmentOptions.Center);
        LobbyNoticePopupViewHelper.SetRect(
            _message.rectTransform,
            Vector2.zero,
            Vector2.one,
            new Vector2(60f, 80f),
            new Vector2(-60f, -112f));

        _close = LobbyNoticePopupViewHelper.CreateButton(
            panel.transform,
            "btnMainNoticeClose",
            "OK",
            out _);
        RectTransform closeRect = (RectTransform)_close.transform;
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 28f);
        closeRect.sizeDelta = new Vector2(220f, 58f);
        _close.onClick.RemoveAllListeners();
        _close.onClick.AddListener(Hide);
        _built = true;
    }

    private void Refresh()
    {
        if (!_built)
            BuildUi();
        _title.text = LocalizationService.Get(
            LocalizationKeys.UiTitleNotice);
        _message.text = LocalizationService.Get(
            LocalizationKeys.UiTitleNoticeEmpty);
    }

    private void HandleLocaleChanged(string unusedLocale)
    {
        Refresh();
    }
}

internal static class LobbyNoticePopupViewHelper
{
    public static GameObject GetOrCreate(
        Transform parent,
        string objectName,
        params Type[] componentTypes)
    {
        Transform existing = parent.Find(objectName);
        GameObject result = existing != null
            ? existing.gameObject
            : new GameObject(objectName, componentTypes);
        if (existing == null)
            result.transform.SetParent(parent, false);
        for (int index = 0; index < componentTypes.Length; index++)
        {
            if (result.GetComponent(componentTypes[index]) == null)
                result.AddComponent(componentTypes[index]);
        }
        return result;
    }

    public static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        TextAlignmentOptions alignment,
        FontStyles style = FontStyles.Normal)
    {
        GameObject result = GetOrCreate(
            parent,
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        TextMeshProUGUI text = result.GetComponent<TextMeshProUGUI>();
        LocalizationFontResolver.ApplyGameDefault(text);
        text.fontSize = fontSize;
        text.fontSizeMin = Mathf.Max(11f, fontSize - 5f);
        text.fontSizeMax = fontSize;
        text.enableAutoSizing = true;
        text.alignment = alignment;
        text.fontStyle = style;
        text.color = new Color(0.94f, 0.91f, 0.78f, 1f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    public static Button CreateButton(
        Transform parent,
        string objectName,
        string label,
        out TextMeshProUGUI labelText)
    {
        Color color = new(0.24f, 0.36f, 0.27f, 1f);
        GameObject result = GetOrCreate(
            parent,
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        Image image = result.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        Button button = result.GetComponent<Button>();
        button.targetGraphic = image;
        labelText = CreateText(
            result.transform,
            "txtLabel",
            22f,
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        labelText.text = label;
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = Vector2.one;
        labelText.rectTransform.offsetMin = new Vector2(8f, 4f);
        labelText.rectTransform.offsetMax = new Vector2(-8f, -4f);
        return button;
    }

    public static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
