using System;
using UnityEngine;

[Serializable]
public sealed class UiArtworkFraming
{
    public const float MinimumZoom = 1f;
    public const float MaximumZoom = 3f;
    public static readonly Vector2 DefaultFocus = new(0.5f, 0.5f);

    [SerializeField] private Vector2 focusNormalized = new(0.5f, 0.5f);
    [SerializeField, Range(MinimumZoom, MaximumZoom)] private float zoom = 1f;

    public Vector2 FocusNormalized => new(
        Mathf.Clamp01(focusNormalized.x),
        Mathf.Clamp01(focusNormalized.y));
    public float Zoom => Mathf.Clamp(zoom, MinimumZoom, MaximumZoom);

    public bool TryValidate(out string error)
    {
        if (float.IsNaN(focusNormalized.x) ||
            float.IsInfinity(focusNormalized.x) ||
            float.IsNaN(focusNormalized.y) ||
            float.IsInfinity(focusNormalized.y) ||
            focusNormalized.x < 0f || focusNormalized.x > 1f ||
            focusNormalized.y < 0f || focusNormalized.y > 1f)
        {
            error = "Artwork focus must be normalized between zero and one.";
            return false;
        }
        if (float.IsNaN(zoom) || float.IsInfinity(zoom) ||
            zoom < MinimumZoom || zoom > MaximumZoom)
        {
            error = $"Artwork zoom must be between {MinimumZoom} and " +
                    $"{MaximumZoom}.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

public static class DungeonSelectArtworkLayout
{
    public const float ReferenceBrowserWidth = 1624f;
    public const float DetailLeftInset = 468f;
    public const float DetailCoverHeight = 340f;
    public static readonly Vector2 CategoryCardViewportSize =
        new(410f, 340f);
    public static readonly Vector2 DetailCoverViewportSize = new(
        ReferenceBrowserWidth - DetailLeftInset,
        DetailCoverHeight);
    public static readonly Vector2 FullScreenViewportSize =
        new(1920f, 1080f);
}
