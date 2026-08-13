using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RecruitBannerDesignerBindings))]
public sealed class RecruitBannerDesignerBindingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PS260714EditorText.DrawDefaultInspector(serializedObject);
        RecruitBannerDesignerBindings bindings =
            (RecruitBannerDesignerBindings)target;
        DrawStatus(
            bindings.HasDesignerLayout,
            bindings.HasRequiredReferences);
        if (GUILayout.Button("Capture References From Hierarchy"))
        {
            Undo.RecordObject(bindings, "Capture Recruit Banner Bindings");
            if (bindings.CaptureReferencesFromHierarchy())
            {
                bindings.MarkDesignerLayoutCurrent();
                EditorUtility.SetDirty(bindings);
            }
        }
    }

    private static void DrawStatus(bool designerOwned, bool valid)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            designerOwned && valid
                ? "Designer-owned fixed scene UI. Runtime code updates " +
                  "content only and preserves this layout."
                : "Bindings are incomplete. Capture the current hierarchy " +
                  "before entering Play Mode.",
            designerOwned && valid
                ? MessageType.Info
                : MessageType.Warning);
    }
}

[CustomEditor(typeof(RecruitRevealDesignerBindings))]
public sealed class RecruitRevealDesignerBindingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PS260714EditorText.DrawDefaultInspector(serializedObject);
        RecruitRevealDesignerBindings bindings =
            (RecruitRevealDesignerBindings)target;
        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            bindings.HasDesignerLayout &&
            bindings.HasRequiredReferences
                ? "Designer-owned result overlay. Edit the fixed rows, " +
                  "buttons and background directly in the scene."
                : "Result overlay bindings are incomplete. Ten fixed result " +
                  "rows are required.",
            bindings.HasDesignerLayout &&
            bindings.HasRequiredReferences
                ? MessageType.Info
                : MessageType.Warning);
        if (GUILayout.Button("Capture References From Hierarchy"))
        {
            Undo.RecordObject(bindings, "Capture Recruit Reveal Bindings");
            if (bindings.CaptureReferencesFromHierarchy())
            {
                bindings.MarkDesignerLayoutCurrent();
                EditorUtility.SetDirty(bindings);
            }
        }
    }
}
