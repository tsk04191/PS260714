using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RuntimeMenuPageBase), true)]
[CanEditMultipleObjects]
public sealed class RuntimeMenuPageBaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField(
            "Designer UI",
            EditorStyles.boldLabel);

        RuntimeMenuPageBase page = target as RuntimeMenuPageBase;
        if (page == null)
            return;

        MessageType messageType = page.HasDesignerLayout
            ? MessageType.Info
            : MessageType.Warning;
        string message = page.HasDesignerLayout
            ? "This page uses the designer-owned scene layout. " +
              "Runtime code will not overwrite its RectTransforms."
            : "This page still uses a generated layout. Run the migration " +
              "before editing RectTransforms.";
        EditorGUILayout.HelpBox(message, messageType);

        using (new EditorGUI.DisabledScope(
                   Application.isPlaying ||
                   targets.Length != 1))
        {
            if (GUILayout.Button("Migrate / Unlock Runtime UI"))
            {
                MenuPageSceneBuilder.MigrateRuntimeUiForDesigner(
                    page.gameObject.scene);
            }

            if (page is StageSelectPage stageSelectPage &&
                GUILayout.Button("Sync Stage Select UI & Save Scene"))
            {
                if (stageSelectPage.SyncEditorUi(out string error))
                {
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
                        page.gameObject.scene);
                }
                else
                {
                    Debug.LogError(error, stageSelectPage);
                }
            }

            if (GUILayout.Button("Validate Designer UI References"))
            {
                IReadOnlyList<string> issues =
                    MenuPageSceneBuilder.ValidateDesignerUiForScene(
                        page.gameObject.scene);
                if (issues.Count == 0)
                {
                    Debug.Log(
                        "Designer UI validation passed.",
                        page);
                }
                else
                {
                    Debug.LogWarning(
                        "Designer UI validation found:\n- " +
                        string.Join("\n- ", issues),
                        page);
                }
            }
        }
    }
}
