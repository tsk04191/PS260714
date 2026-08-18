using UnityEditor;
using UnityEngine;

public static class UiArtworkFramingEditorGUI
{
    private const float PreviewMaximumHeight = 230f;

    public static void Draw(
        SerializedProperty spriteProperty,
        SerializedProperty framingProperty,
        string label,
        Vector2 viewportSize)
    {
        if (spriteProperty == null || framingProperty == null)
            return;

        SerializedProperty focusProperty = framingProperty
            .FindPropertyRelative("focusNormalized");
        SerializedProperty zoomProperty = framingProperty
            .FindPropertyRelative("zoom");
        if (focusProperty == null || zoomProperty == null)
            return;

        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(spriteProperty, new GUIContent("Sprite"));
        Vector2 focus = EditorGUILayout.Vector2Field(
            "Focus",
            focusProperty.vector2Value);
        focusProperty.vector2Value = new Vector2(
            Mathf.Clamp01(focus.x),
            Mathf.Clamp01(focus.y));
        zoomProperty.floatValue = EditorGUILayout.Slider(
            "Zoom",
            zoomProperty.floatValue,
            UiArtworkFraming.MinimumZoom,
            UiArtworkFraming.MaximumZoom);

        Sprite sprite = spriteProperty.objectReferenceValue as Sprite;
        Vector2 safeViewport = new(
            Mathf.Max(1f, viewportSize.x),
            Mathf.Max(1f, viewportSize.y));
        float safeAspect = safeViewport.x / safeViewport.y;
        float width = Mathf.Max(1f, EditorGUIUtility.currentViewWidth - 72f);
        float height = Mathf.Min(PreviewMaximumHeight, width / safeAspect);
        Rect preview = GUILayoutUtility.GetRect(
            120f,
            height,
            GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(preview, new Color(0.025f, 0.03f, 0.035f, 1f));
        if (sprite == null)
        {
            GUI.Label(preview, "No artwork", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Rect visible = ResolveVisibleSourceRect(
            sprite,
            safeViewport,
            focusProperty.vector2Value,
            zoomProperty.floatValue);
        Rect textureRect = sprite.textureRect;
        Texture2D texture = sprite.texture;
        Rect uv = new(
            (textureRect.x + visible.x * textureRect.width) / texture.width,
            (textureRect.y + visible.y * textureRect.height) / texture.height,
            visible.width * textureRect.width / texture.width,
            visible.height * textureRect.height / texture.height);
        GUI.DrawTextureWithTexCoords(preview, texture, uv, true);
        DrawBorder(preview, new Color(0.72f, 0.88f, 0.74f, 1f));
        HandleInput(
            preview,
            visible,
            focusProperty,
            zoomProperty);
        EditorGUILayout.LabelField(
            "Drag to reframe · Mouse wheel to zoom",
            EditorStyles.centeredGreyMiniLabel);
    }

    internal static Rect ResolveVisibleSourceRect(
        Sprite sprite,
        Vector2 viewportSize,
        Vector2 focus,
        float zoom)
    {
        Vector2 rendered = UiMaskedCoverImageView.CalculateRenderedSize(
            viewportSize,
            sprite.rect.size,
            zoom);
        Vector2 anchored = UiMaskedCoverImageView.CalculateAnchoredPosition(
            viewportSize,
            rendered,
            focus);
        return UiMaskedCoverImageView.CalculateVisibleSourceRect(
            viewportSize,
            rendered,
            anchored);
    }

    private static void HandleInput(
        Rect preview,
        Rect visible,
        SerializedProperty focusProperty,
        SerializedProperty zoomProperty)
    {
        Event current = Event.current;
        int controlId = GUIUtility.GetControlID(
            "UiArtworkFramingPreview".GetHashCode(),
            FocusType.Passive,
            preview);
        if (current.type == EventType.MouseDown && current.button == 0 &&
            preview.Contains(current.mousePosition))
        {
            GUIUtility.hotControl = controlId;
            current.Use();
        }
        else if (current.type == EventType.MouseDrag &&
                 GUIUtility.hotControl == controlId)
        {
            Vector2 focus = focusProperty.vector2Value;
            focus.x -= current.delta.x / Mathf.Max(1f, preview.width) *
                       visible.width;
            focus.y += current.delta.y / Mathf.Max(1f, preview.height) *
                       visible.height;
            focusProperty.vector2Value = new Vector2(
                Mathf.Clamp01(focus.x),
                Mathf.Clamp01(focus.y));
            GUI.changed = true;
            current.Use();
        }
        else if (current.type == EventType.MouseUp && current.button == 0 &&
                 GUIUtility.hotControl == controlId)
        {
            GUIUtility.hotControl = 0;
            current.Use();
        }
        else if (current.type == EventType.ScrollWheel &&
                 preview.Contains(current.mousePosition))
        {
            zoomProperty.floatValue = Mathf.Clamp(
                zoomProperty.floatValue * (1f - current.delta.y * 0.05f),
                UiArtworkFraming.MinimumZoom,
                UiArtworkFraming.MaximumZoom);
            GUI.changed = true;
            current.Use();
        }
    }

    private static void DrawBorder(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
        EditorGUI.DrawRect(
            new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
            color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
        EditorGUI.DrawRect(
            new Rect(rect.xMax - 1f, rect.y, 1f, rect.height),
            color);
    }
}
