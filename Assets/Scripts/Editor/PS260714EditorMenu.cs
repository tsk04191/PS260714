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

            if (GUILayout.Button(
                    ButtonOrder[5],
                    EditorStyles.toolbarButton,
                    GUILayout.Width(64f)))
            {
                refresh?.Invoke();
            }
        }
    }
}
