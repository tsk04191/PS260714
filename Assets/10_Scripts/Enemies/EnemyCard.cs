using System;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class EnemyCard : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RectTransform cardShadow;
    [SerializeField] private RectTransform tileFace;
    [SerializeField] private TMP_Text healthText;

    private RectTransform _rectTransform;
    private Image _tileFaceImage;
    private Color _defaultFaceColor;
    private Color _hitFlashColor;
    private float _hitFlashDuration;
    private float _hitFlashRemaining;

    public EnemyRuntime Runtime { get; private set; }
    public event Action<EnemyRuntime> Clicked;
    public RectTransform RectTransform =>
        _rectTransform != null ? _rectTransform : _rectTransform = (RectTransform)transform;

    internal void ApplyGameDefaultFont()
    {
        LocalizationFontResolver.ApplyGameDefault(healthText, "number");
    }

    private void OnEnable()
    {
        LocalizationService.LocaleChanged += HandleLocaleChanged;
        LocalizationService.FontChanged += HandleFontChanged;
        RefreshHealth();
    }

    private void OnDisable()
    {
        LocalizationService.LocaleChanged -= HandleLocaleChanged;
        LocalizationService.FontChanged -= HandleFontChanged;
        ClearHitFlash();
    }

    private void Update()
    {
        if (_hitFlashRemaining <= 0f)
            return;

        _hitFlashRemaining = Mathf.Max(
            0f,
            _hitFlashRemaining - Time.unscaledDeltaTime);
        RefreshFaceColor();
    }

    public void Bind(EnemyRuntime runtime)
    {
        if (runtime == null)
        {
            Debug.LogError("EnemyCard requires an enemy runtime.", this);
            return;
        }

        Runtime = runtime;
        RefreshHealth();
    }

    public void RefreshHealth()
    {
        if (healthText != null && Runtime != null)
        {
            string typeCode = Runtime.Definition.CardCode;
            string displayName = EnemyLocalization.GetName(
                Runtime.Definition);
            string health = Runtime.Health.ToString();
            if (Runtime.Armor > 0)
                health += $" A{Runtime.Armor}";
            if (Runtime.CurrentShield > 0)
                health += $" S{Runtime.CurrentShield}";
            healthText.text = string.IsNullOrEmpty(typeCode)
                ? $"{displayName}\n{health}"
                : $"{displayName}\n{typeCode} {health}";
            ApplyGameDefaultFont();
        }

        RefreshStatus();
    }

    public void RefreshStatus()
    {
        CacheFaceImage();
        if (_tileFaceImage == null || Runtime == null)
            return;

        if (Runtime.Definition.BoardSprite != null)
            _tileFaceImage.sprite = Runtime.Definition.BoardSprite;
        RefreshFaceColor();
    }

    internal void ShowHitFlash(Color color, float duration)
    {
        CacheFaceImage();
        if (_tileFaceImage == null)
            return;

        _hitFlashColor = new Color(
            Mathf.Clamp01(color.r),
            Mathf.Clamp01(color.g),
            Mathf.Clamp01(color.b),
            _defaultFaceColor.a * Mathf.Clamp01(color.a));
        _hitFlashDuration = Mathf.Max(0.01f, duration);
        _hitFlashRemaining = _hitFlashDuration;
        RefreshFaceColor();
    }

    public void ApplyLayout(float edge, float sideDepth)
    {
        if (cardShadow != null)
        {
            cardShadow.offsetMin = new Vector2(edge * 2f, -edge * 2.5f);
            cardShadow.offsetMax = new Vector2(edge * 2f, -edge * 2.5f);
        }

        if (tileFace != null)
        {
            tileFace.offsetMin = new Vector2(edge, sideDepth);
            tileFace.offsetMax = new Vector2(-edge, -edge);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null &&
            eventData.button == PointerEventData.InputButton.Left &&
            Runtime != null)
        {
            Clicked?.Invoke(Runtime);
        }
    }

    private void HandleLocaleChanged(string unusedLocale)
    {
        RefreshHealth();
    }

    private void HandleFontChanged(string unusedFontId)
    {
        ApplyGameDefaultFont();
    }

    private void CacheFaceImage()
    {
        if (_tileFaceImage != null || tileFace == null)
            return;

        _tileFaceImage = tileFace.GetComponent<Image>();
        if (_tileFaceImage == null)
            return;

        _defaultFaceColor = _tileFaceImage.color;
    }

    private void RefreshFaceColor()
    {
        if (_tileFaceImage == null)
            return;

        if (_hitFlashRemaining <= 0f)
        {
            _tileFaceImage.color = _defaultFaceColor;
            return;
        }

        float normalizedRemaining = _hitFlashDuration > 0f
            ? Mathf.Clamp01(_hitFlashRemaining / _hitFlashDuration)
            : 0f;
        float restoreAmount = 1f - normalizedRemaining;
        _tileFaceImage.color = Color.Lerp(
            _hitFlashColor,
            _defaultFaceColor,
            restoreAmount);
    }

    private void ClearHitFlash()
    {
        _hitFlashDuration = 0f;
        _hitFlashRemaining = 0f;
        if (_tileFaceImage != null)
            _tileFaceImage.color = _defaultFaceColor;
    }
}
