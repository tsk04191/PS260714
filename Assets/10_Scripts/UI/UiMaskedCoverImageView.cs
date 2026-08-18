using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(RectMask2D))]
public sealed class UiMaskedCoverImageView : MonoBehaviour
{
    [SerializeField] private RectTransform viewport;
    [SerializeField] private Image artwork;
    [SerializeField, HideInInspector] private Vector2 focusNormalized =
        new(0.5f, 0.5f);
    [SerializeField, HideInInspector] private float zoom = 1f;

    public RectTransform Viewport => viewport != null
        ? viewport
        : transform as RectTransform;
    public Image Artwork => artwork;
    public bool HasDesignerReferences => Viewport != null && artwork != null &&
        Viewport.GetComponent<RectMask2D>() != null;

    public void Configure(Sprite sprite, UiArtworkFraming framing)
    {
        Vector2 focus = framing?.FocusNormalized ??
                        UiArtworkFraming.DefaultFocus;
        focusNormalized = new Vector2(
            Mathf.Clamp01(focus.x),
            Mathf.Clamp01(focus.y));
        zoom = Mathf.Clamp(
            framing?.Zoom ?? UiArtworkFraming.MinimumZoom,
            UiArtworkFraming.MinimumZoom,
            UiArtworkFraming.MaximumZoom);
        if (artwork != null)
        {
            artwork.sprite = sprite;
            artwork.enabled = sprite != null;
            artwork.raycastTarget = false;
        }
        RefreshLayout();
    }

    public void RefreshLayout()
    {
        RectTransform resolvedViewport = Viewport;
        if (resolvedViewport == null || artwork == null)
            return;

        Sprite sprite = artwork.sprite;
        artwork.enabled = sprite != null;
        if (sprite == null)
            return;

        Vector2 renderedSize = CalculateRenderedSize(
            resolvedViewport.rect.size,
            sprite.rect.size,
            zoom);
        RectTransform artworkRect = artwork.rectTransform;
        artworkRect.anchorMin = new Vector2(0.5f, 0.5f);
        artworkRect.anchorMax = new Vector2(0.5f, 0.5f);
        artworkRect.pivot = new Vector2(0.5f, 0.5f);
        artworkRect.localScale = Vector3.one;
        artworkRect.localRotation = Quaternion.identity;
        artworkRect.sizeDelta = renderedSize;
        artworkRect.anchoredPosition = CalculateAnchoredPosition(
            resolvedViewport.rect.size,
            renderedSize,
            focusNormalized);
        artwork.type = Image.Type.Simple;
        artwork.preserveAspect = false;
    }

    public static Vector2 CalculateRenderedSize(
        Vector2 viewportSize,
        Vector2 sourceSize,
        float framingZoom)
    {
        if (viewportSize.x <= 0f || viewportSize.y <= 0f ||
            sourceSize.x <= 0f || sourceSize.y <= 0f)
        {
            return Vector2.zero;
        }

        float coverScale = Mathf.Max(
            viewportSize.x / sourceSize.x,
            viewportSize.y / sourceSize.y);
        float safeZoom = Mathf.Clamp(
            framingZoom,
            UiArtworkFraming.MinimumZoom,
            UiArtworkFraming.MaximumZoom);
        return sourceSize * coverScale * safeZoom;
    }

    public static Vector2 CalculateAnchoredPosition(
        Vector2 viewportSize,
        Vector2 renderedSize,
        Vector2 normalizedFocus)
    {
        Vector2 focus = new(
            Mathf.Clamp01(normalizedFocus.x),
            Mathf.Clamp01(normalizedFocus.y));
        Vector2 desired = -Vector2.Scale(
            focus - UiArtworkFraming.DefaultFocus,
            renderedSize);
        Vector2 limit = new(
            Mathf.Max(0f, (renderedSize.x - viewportSize.x) * 0.5f),
            Mathf.Max(0f, (renderedSize.y - viewportSize.y) * 0.5f));
        return new Vector2(
            Mathf.Clamp(desired.x, -limit.x, limit.x),
            Mathf.Clamp(desired.y, -limit.y, limit.y));
    }

    public static Rect CalculateVisibleSourceRect(
        Vector2 viewportSize,
        Vector2 renderedSize,
        Vector2 anchoredPosition)
    {
        if (viewportSize.x <= 0f || viewportSize.y <= 0f ||
            renderedSize.x <= 0f || renderedSize.y <= 0f)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        Vector2 imageMinimum = anchoredPosition - renderedSize * 0.5f;
        Vector2 viewportMinimum = -viewportSize * 0.5f;
        Vector2 viewportMaximum = viewportSize * 0.5f;
        float xMin = Mathf.Clamp01(
            (viewportMinimum.x - imageMinimum.x) / renderedSize.x);
        float yMin = Mathf.Clamp01(
            (viewportMinimum.y - imageMinimum.y) / renderedSize.y);
        float xMax = Mathf.Clamp01(
            (viewportMaximum.x - imageMinimum.x) / renderedSize.x);
        float yMax = Mathf.Clamp01(
            (viewportMaximum.y - imageMinimum.y) / renderedSize.y);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private void OnEnable()
    {
        RefreshLayout();
    }

    private void OnRectTransformDimensionsChange()
    {
        RefreshLayout();
    }
}
