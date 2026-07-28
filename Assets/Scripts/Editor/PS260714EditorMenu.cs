using System;
using UnityEditor;
using UnityEngine;

internal static class PS260714EditorMenu
{
    public const string Root = "PS260714/";

    public const string CharacterEditor =
        Root + "Character Editor";
    public const string EnemyEditor =
        Root + "Enemy Editor";
    public const string StatusEffectEditor =
        Root + "Status Effect Editor";
    public const string BattleVfxEditor =
        Root + "Effects/Battle VFX Editor";
    public const string ValidateBattleVfx =
        Root + "Effects/Validate Battle VFX";
    public const string LocalizationEditor =
        Root + "Localization/Localization Editor";
    public const string ValidateLocalization =
        Root + "Localization/Validate CSV";
    public const string GenerateLocalization =
        Root + "Localization/Generate C#";
}

internal static class PS260714AssetEditorToolbar
{
    internal static readonly string[] ButtonOrder =
    {
        "New",
        "Save",
        "Duplicate",
        "Rename",
        "Delete",
        "Ping",
        "Refresh"
    };

    public static void Draw(
        string summary,
        bool hasSelection,
        Action create,
        Action save,
        Action duplicate,
        Action rename,
        Action delete,
        Action ping,
        Action refresh)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label(
                summary,
                EditorStyles.miniLabel,
                GUILayout.Width(136f));
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(
                       EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button(
                        ButtonOrder[0],
                        EditorStyles.toolbarButton,
                        GUILayout.Width(56f)))
                {
                    create?.Invoke();
                }

                using (new EditorGUI.DisabledScope(!hasSelection))
                {
                    if (GUILayout.Button(
                            ButtonOrder[1],
                            EditorStyles.toolbarButton,
                            GUILayout.Width(56f)))
                    {
                        save?.Invoke();
                    }

                    if (GUILayout.Button(
                            ButtonOrder[2],
                            EditorStyles.toolbarButton,
                            GUILayout.Width(76f)))
                    {
                        duplicate?.Invoke();
                    }

                    if (GUILayout.Button(
                            ButtonOrder[3],
                            EditorStyles.toolbarButton,
                            GUILayout.Width(64f)))
                    {
                        rename?.Invoke();
                    }

                    if (GUILayout.Button(
                            ButtonOrder[4],
                            EditorStyles.toolbarButton,
                            GUILayout.Width(60f)))
                    {
                        delete?.Invoke();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUILayout.Button(
                        ButtonOrder[5],
                        EditorStyles.toolbarButton,
                        GUILayout.Width(52f)))
                {
                    ping?.Invoke();
                }
            }

            if (GUILayout.Button(
                    ButtonOrder[6],
                    EditorStyles.toolbarButton,
                    GUILayout.Width(64f)))
            {
                refresh?.Invoke();
            }
        }
    }
}

internal static class PS260714AssetEditorList
{
    internal const float Width = 230f;
    internal const float RowHeight = 42f;
    private const float IconSize = 34f;
    private const float ContentPadding = 5f;

    private static GUIStyle _leftLabelStyle;
    private static GUIStyle _centeredLabelStyle;

    internal static string DrawSearchField(string searchText)
    {
        EditorGUILayout.Space(4f);
        string result = EditorGUILayout.TextField(
            searchText,
            EditorStyles.toolbarSearchField);
        EditorGUILayout.Space(4f);
        return result;
    }

    internal static bool DrawRow(
        bool selected,
        GUIContent content,
        TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        Rect rowRect = GUILayoutUtility.GetRect(
            1f,
            RowHeight,
            GUILayout.ExpandWidth(true));
        bool toggled = GUI.Toggle(
            rowRect,
            selected,
            GUIContent.none,
            GUI.skin.button);

        Rect labelRect = new(
            rowRect.x + ContentPadding,
            rowRect.y,
            rowRect.width - ContentPadding * 2f,
            rowRect.height);
        if (content.image != null)
        {
            Rect iconRect = new(
                labelRect.x,
                rowRect.y + (rowRect.height - IconSize) * 0.5f,
                IconSize,
                IconSize);
            GUI.DrawTexture(
                iconRect,
                content.image,
                ScaleMode.ScaleToFit,
                true);
            labelRect.xMin = iconRect.xMax + ContentPadding;
        }

        GUIStyle labelStyle = alignment == TextAnchor.MiddleCenter
            ? CenteredLabelStyle
            : LeftLabelStyle;
        GUI.Label(
            labelRect,
            new GUIContent(content.text, content.tooltip),
            labelStyle);
        EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);
        return toggled && !selected;
    }

    internal static Texture GetAssetPreview(UnityEngine.Object asset)
    {
        if (asset == null)
            return null;

        return AssetPreview.GetAssetPreview(asset) ??
               AssetPreview.GetMiniThumbnail(asset);
    }

    internal static void Ping(UnityEngine.Object asset)
    {
        if (asset != null)
            EditorGUIUtility.PingObject(asset);
    }

    private static GUIStyle LeftLabelStyle =>
        _leftLabelStyle ??= CreateLabelStyle(TextAnchor.MiddleLeft);

    private static GUIStyle CenteredLabelStyle =>
        _centeredLabelStyle ??= CreateLabelStyle(TextAnchor.MiddleCenter);

    private static GUIStyle CreateLabelStyle(TextAnchor alignment)
    {
        return new GUIStyle(EditorStyles.label)
        {
            alignment = alignment,
            clipping = TextClipping.Clip,
            wordWrap = false
        };
    }
}
