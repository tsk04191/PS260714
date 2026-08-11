using UnityEditor;
using UnityEngine;

public sealed class CharacterStandingFramingEditorWindow : EditorWindow
{
    public const string MenuPath =
        "PS260714/Character Standing Framing Editor";

    private static readonly Vector2 HudViewportReferenceSize =
        new(152f, 140f);

    private CharacterSO character;
    private SerializedObject serializedCharacter;
    private bool isDragging;

    [MenuItem(MenuPath)]
    private static void OpenFromMenu()
    {
        CharacterSO selected = Selection.activeObject as CharacterSO;
        selected ??= CharacterDefinitionCatalog.GetAll().Count > 0
            ? CharacterDefinitionCatalog.GetAll()[0]
            : null;
        Open(selected);
    }

    public static void Open(CharacterSO target)
    {
        CharacterStandingFramingEditorWindow window =
            GetWindow<CharacterStandingFramingEditorWindow>();
        window.titleContent = new GUIContent("Standing Framing");
        window.minSize = new Vector2(820f, 620f);
        window.SetCharacter(target);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Standing Framing");
        minSize = new Vector2(820f, 620f);
        if (character == null && Selection.activeObject is CharacterSO selected)
            SetCharacter(selected);
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is not CharacterSO selected)
            return;

        SetCharacter(selected);
        Repaint();
    }

    private void SetCharacter(CharacterSO target)
    {
        character = target;
        serializedCharacter = character != null
            ? new SerializedObject(character)
            : null;
        isDragging = false;
    }

    internal static bool DrawEmbedded(
        CharacterSO target,
        SerializedObject serializedTarget,
        ref bool dragging,
        bool showSpriteField,
        float previewHeight)
    {
        if (target == null || serializedTarget == null)
            return false;

        serializedTarget.UpdateIfRequiredOrScript();
        SerializedProperty spriteProperty = serializedTarget
            .FindProperty("standingSprite");
        SerializedProperty framingProperty = serializedTarget
            .FindProperty("dungeonHudStandingFraming");
        SerializedProperty focusProperty = framingProperty?
            .FindPropertyRelative("focusNormalized");
        SerializedProperty zoomProperty = framingProperty?
            .FindPropertyRelative("zoom");
        if (spriteProperty == null || focusProperty == null ||
            zoomProperty == null)
        {
            EditorGUILayout.HelpBox(
                "스탠딩 일러스트 구도 데이터를 찾지 못했습니다.",
                MessageType.Error);
            return false;
        }

        EditorGUILayout.HelpBox(
            "원본 이미지는 그대로 저장됩니다. 오른쪽 HUD 미리보기에서 " +
            "좌클릭 드래그로 위치를 옮기고, 마우스 휠로 확대/축소하세요.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        if (showSpriteField)
        {
            EditorGUILayout.PropertyField(
                spriteProperty,
                new GUIContent("Original Standing Sprite"));
        }

        Vector2 focus = EditorGUILayout.Vector2Field(
            "Focus (Normalized)",
            focusProperty.vector2Value);
        focusProperty.vector2Value = new Vector2(
            Mathf.Clamp01(focus.x),
            Mathf.Clamp01(focus.y));
        zoomProperty.floatValue = EditorGUILayout.Slider(
            "Zoom",
            zoomProperty.floatValue,
            CharacterStandingFraming.MinimumZoom,
            CharacterStandingFraming.MaximumZoom);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("기본 구도로 초기화", GUILayout.Width(150f)))
        {
            Undo.RecordObject(target, "Reset standing framing");
            focusProperty.vector2Value =
                CharacterStandingFraming.DefaultFocus;
            zoomProperty.floatValue = CharacterStandingFraming.DefaultZoom;
            GUI.changed = true;
        }
        EditorGUILayout.EndHorizontal();

        bool changed = EditorGUI.EndChangeCheck();
        if (changed)
            ApplyChanges(target, serializedTarget);

        Sprite sprite = spriteProperty.objectReferenceValue as Sprite;
        EditorGUILayout.Space(6f);
        Rect previewArea = GUILayoutUtility.GetRect(
            320f,
            Mathf.Max(260f, previewHeight),
            GUILayout.ExpandWidth(true));
        changed |= DrawPreviews(
            previewArea,
            sprite,
            focusProperty,
            zoomProperty,
            target,
            serializedTarget,
            ref dragging);
        return changed;
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        CharacterSO selected = EditorGUILayout.ObjectField(
            "Character",
            character,
            typeof(CharacterSO),
            false) as CharacterSO;
        if (EditorGUI.EndChangeCheck())
            SetCharacter(selected);

        if (character == null || serializedCharacter == null)
        {
            EditorGUILayout.HelpBox(
                "CharacterSO를 선택하면 원본 스탠딩 일러스트의 HUD 구도를 " +
                "편집할 수 있습니다.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.Space(4f);
        if (DrawEmbedded(
                character,
                serializedCharacter,
                ref isDragging,
                true,
                450f))
        {
            Repaint();
        }
    }

    private static bool DrawPreviews(
        Rect area,
        Sprite sprite,
        SerializedProperty focusProperty,
        SerializedProperty zoomProperty,
        CharacterSO target,
        SerializedObject serializedTarget,
        ref bool dragging)
    {
        const float gap = 18f;
        const float labelHeight = 22f;
        float columnWidth = Mathf.Max(120f, (area.width - gap) * 0.5f);
        Rect sourceColumn = new(
            area.x,
            area.y,
            columnWidth,
            area.height);
        Rect hudColumn = new(
            sourceColumn.xMax + gap,
            area.y,
            columnWidth,
            area.height);

        GUI.Label(
            new Rect(sourceColumn.x, sourceColumn.y, columnWidth, labelHeight),
            "원본 일러스트 / HUD 노출 영역",
            EditorStyles.boldLabel);
        GUI.Label(
            new Rect(hudColumn.x, hudColumn.y, columnWidth, labelHeight),
            "CharacterInfo 마스크 결과",
            EditorStyles.boldLabel);

        Rect sourceBounds = new(
            sourceColumn.x,
            sourceColumn.y + labelHeight,
            sourceColumn.width,
            Mathf.Max(1f, sourceColumn.height - labelHeight));
        Rect hudBounds = new(
            hudColumn.x,
            hudColumn.y + labelHeight,
            hudColumn.width,
            Mathf.Max(1f, hudColumn.height - labelHeight));
        EditorGUI.DrawRect(sourceBounds, new Color(0.09f, 0.1f, 0.12f, 1f));
        EditorGUI.DrawRect(hudBounds, new Color(0.09f, 0.1f, 0.12f, 1f));

        Rect hudViewport = CharacterEditorWindow.CalculateAspectFitRect(
            new Rect(
                hudBounds.x + 8f,
                hudBounds.y + 8f,
                Mathf.Max(1f, hudBounds.width - 16f),
                Mathf.Max(1f, hudBounds.height - 16f)),
            HudViewportReferenceSize.x / HudViewportReferenceSize.y);
        DrawHudPreview(
            hudViewport,
            sprite,
            focusProperty.vector2Value,
            zoomProperty.floatValue);
        bool changed = HandleHudInput(
            hudViewport,
            sprite,
            focusProperty,
            zoomProperty,
            target,
            serializedTarget,
            ref dragging);
        DrawSourcePreview(
            sourceBounds,
            hudViewport.size,
            sprite,
            focusProperty.vector2Value,
            zoomProperty.floatValue);
        return changed;
    }

    private static void DrawHudPreview(
        Rect viewport,
        Sprite sprite,
        Vector2 focus,
        float zoom)
    {
        EditorGUI.DrawRect(viewport, new Color(0.22f, 0.24f, 0.28f, 1f));
        if (!TryGetSprite(sprite, out Texture2D texture, out Rect uv))
        {
            GUI.Label(
                viewport,
                "Standing Sprite 없음",
                EditorStyles.centeredGreyMiniLabel);
            DrawBorder(viewport, new Color(0.2f, 0.9f, 0.9f, 1f), 2f);
            return;
        }

        Vector2 sourceSize = sprite.rect.size;
        Vector2 renderedSize = CharacterStandingPortraitView
            .CalculateRenderedSize(viewport.size, sourceSize, zoom);
        Vector2 anchoredPosition = CharacterStandingPortraitView
            .CalculateAnchoredPosition(viewport.size, renderedSize, focus);
        Rect localArtwork = new(
            viewport.width * 0.5f + anchoredPosition.x -
            renderedSize.x * 0.5f,
            viewport.height * 0.5f - anchoredPosition.y -
            renderedSize.y * 0.5f,
            renderedSize.x,
            renderedSize.y);

        GUI.BeginGroup(viewport);
        GUI.DrawTextureWithTexCoords(localArtwork, texture, uv, true);
        GUI.EndGroup();
        DrawBorder(viewport, new Color(0.2f, 0.9f, 0.9f, 1f), 2f);
    }

    private static void DrawSourcePreview(
        Rect bounds,
        Vector2 viewportSize,
        Sprite sprite,
        Vector2 focus,
        float zoom)
    {
        if (!TryGetSprite(sprite, out Texture2D texture, out Rect uv))
            return;

        Rect content = CharacterEditorWindow.CalculateAspectFitRect(
            new Rect(
                bounds.x + 8f,
                bounds.y + 8f,
                Mathf.Max(1f, bounds.width - 16f),
                Mathf.Max(1f, bounds.height - 16f)),
            sprite.rect.width / sprite.rect.height);
        GUI.DrawTextureWithTexCoords(content, texture, uv, true);

        Vector2 renderedSize = CharacterStandingPortraitView
            .CalculateRenderedSize(viewportSize, sprite.rect.size, zoom);
        Vector2 anchoredPosition = CharacterStandingPortraitView
            .CalculateAnchoredPosition(viewportSize, renderedSize, focus);
        Rect visible = CharacterStandingPortraitView.CalculateVisibleSourceRect(
            viewportSize,
            renderedSize,
            anchoredPosition);
        Rect overlay = new(
            content.x + visible.xMin * content.width,
            content.y + (1f - visible.yMax) * content.height,
            visible.width * content.width,
            visible.height * content.height);
        EditorGUI.DrawRect(overlay, new Color(0.1f, 0.9f, 0.95f, 0.12f));
        DrawBorder(overlay, new Color(0.2f, 0.95f, 1f, 1f), 2f);
    }

    private static bool HandleHudInput(
        Rect viewport,
        Sprite sprite,
        SerializedProperty focusProperty,
        SerializedProperty zoomProperty,
        CharacterSO target,
        SerializedObject serializedTarget,
        ref bool dragging)
    {
        if (sprite == null)
            return false;

        bool changed = false;
        Event current = Event.current;
        int controlId = GUIUtility.GetControlID(
            "CharacterStandingFramingPreview".GetHashCode(),
            FocusType.Passive,
            viewport);
        if (current.type == EventType.MouseDown && current.button == 0 &&
            viewport.Contains(current.mousePosition))
        {
            GUIUtility.hotControl = controlId;
            dragging = true;
            Undo.RecordObject(target, "Move standing framing");
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && dragging &&
                 GUIUtility.hotControl == controlId)
        {
            Vector2 renderedSize = CharacterStandingPortraitView
                .CalculateRenderedSize(
                    viewport.size,
                    sprite.rect.size,
                    zoomProperty.floatValue);
            if (renderedSize.x > 0f && renderedSize.y > 0f)
            {
                Vector2 focus = focusProperty.vector2Value;
                focus.x -= current.delta.x / renderedSize.x;
                focus.y += current.delta.y / renderedSize.y;
                focusProperty.vector2Value = new Vector2(
                    Mathf.Clamp01(focus.x),
                    Mathf.Clamp01(focus.y));
                changed = ApplyChanges(target, serializedTarget);
            }
            current.Use();
        }
        else if (current.type == EventType.MouseUp && dragging &&
                 GUIUtility.hotControl == controlId)
        {
            dragging = false;
            GUIUtility.hotControl = 0;
            current.Use();
        }
        else if (current.type == EventType.ScrollWheel &&
                 viewport.Contains(current.mousePosition))
        {
            Undo.RecordObject(target, "Zoom standing framing");
            float factor = Mathf.Exp(-current.delta.y * 0.05f);
            zoomProperty.floatValue = Mathf.Clamp(
                zoomProperty.floatValue * factor,
                CharacterStandingFraming.MinimumZoom,
                CharacterStandingFraming.MaximumZoom);
            changed = ApplyChanges(target, serializedTarget);
            current.Use();
        }

        if (viewport.Contains(current.mousePosition))
            EditorGUIUtility.AddCursorRect(viewport, MouseCursor.Pan);
        return changed;
    }

    private static bool ApplyChanges(
        CharacterSO target,
        SerializedObject serializedTarget)
    {
        if (serializedTarget == null || target == null)
            return false;

        bool changed = serializedTarget.ApplyModifiedProperties();
        if (changed)
        {
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }
        GUI.changed |= changed;
        return changed;
    }

    private static bool TryGetSprite(
        Sprite sprite,
        out Texture2D texture,
        out Rect textureCoordinates)
    {
        return CharacterEditorWindow.TryGetSpriteTextureCoordinates(
            sprite,
            out texture,
            out textureCoordinates);
    }

    private static void DrawBorder(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(
            new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(
            new Rect(rect.x, rect.yMax - thickness, rect.width, thickness),
            color);
        EditorGUI.DrawRect(
            new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(
            new Rect(rect.xMax - thickness, rect.y, thickness, rect.height),
            color);
    }
}
