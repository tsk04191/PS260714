using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DungeonCategorySO))]
public sealed class DungeonCategoryEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.UpdateIfRequiredOrScript();
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "cardSprite",
            "cardFraming",
            "backdropSprite",
            "backdropFraming");
        EditorGUILayout.Space(8f);
        UiArtworkFramingEditorGUI.Draw(
            serializedObject.FindProperty("cardSprite"),
            serializedObject.FindProperty("cardFraming"),
            "Selection Card Framing",
            DungeonSelectArtworkLayout.CategoryCardViewportSize);
        EditorGUILayout.Space(8f);
        UiArtworkFramingEditorGUI.Draw(
            serializedObject.FindProperty("backdropSprite"),
            serializedObject.FindProperty("backdropFraming"),
            "Full-Screen Backdrop Framing",
            DungeonSelectArtworkLayout.FullScreenViewportSize);
        serializedObject.ApplyModifiedProperties();

        DungeonCategorySO category = target as DungeonCategorySO;
        if (category == null)
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Resolved Dungeon List",
            EditorStyles.boldLabel);
        IReadOnlyList<DungeonDefinition> dungeons =
            category.ResolveDungeons();
        if (dungeons.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "This category currently resolves to no listed dungeons.",
                MessageType.Warning);
        }
        else
        {
            using (new EditorGUI.DisabledScope(true))
            {
                for (int index = 0; index < dungeons.Count; index++)
                {
                    EditorGUILayout.ObjectField(
                        $"{index + 1:00}",
                        dungeons[index],
                        typeof(DungeonDefinition),
                        false);
                }
            }
        }

        if (category.TryValidate(out string error))
        {
            EditorGUILayout.HelpBox(
                $"Valid category · {dungeons.Count} dungeon(s)",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
}
