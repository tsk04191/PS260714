using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class OperatorRosterCardHighlight :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    private static readonly Color NormalColor =
        new(0.10f, 0.135f, 0.125f, 1f);
    private static readonly Color HoverColor =
        new(0.16f, 0.30f, 0.27f, 1f);
    private static readonly Color HoverOutlineColor =
        new(0.64f, 0.96f, 0.88f, 1f);

    private Image _background;
    private Outline _outline;
    private Color _normalColor = NormalColor;
    private Color _hoverColor = HoverColor;
    private Color _outlineColor = HoverOutlineColor;
    private bool _hovered;
    private bool _pressed;

    public void Configure(Image background, Outline outline)
    {
        _background = background;
        _outline = outline;
        ApplyVisualState();
    }

    public void SetPalette(
        Color normalColor,
        Color accentColor,
        Color outlineColor)
    {
        _normalColor = normalColor;
        _hoverColor = Color.Lerp(normalColor, accentColor, 0.32f);
        _outlineColor = outlineColor;
        ApplyVisualState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        ApplyVisualState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        _pressed = false;
        ApplyVisualState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        _pressed = true;
        ApplyVisualState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        _pressed = false;
        ApplyVisualState();
    }

    private void OnDisable()
    {
        _hovered = false;
        _pressed = false;
        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        if (_background != null)
        {
            Color color = _hovered ? _hoverColor : _normalColor;
            if (_pressed)
                color = Color.Lerp(color, Color.black, 0.22f);
            _background.color = color;
        }
        if (_outline == null)
            return;
        _outline.enabled = _hovered;
        _outline.effectColor = _outlineColor;
        _outline.effectDistance = new Vector2(4f, -4f);
    }
}
