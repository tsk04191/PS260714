using UnityEditor;
using UnityEngine;

/// <summary>
/// Owner-neutral entry point for the shared battle effect authoring UI.
/// Character, item, enemy, card, and status editors use this facade so effect
/// defaults and validation controls stay on one authoring path.
/// </summary>
internal static class BattleAbilityEditorGUI
{
    internal static void DrawAreaDefinition(
        SerializedProperty area,
        SerializedProperty subject,
        Object owner)
    {
        if (area == null)
        {
            EditorGUILayout.HelpBox(
                "World area definition was not found.",
                MessageType.Error);
            return;
        }

        SerializedProperty shape = area.FindPropertyRelative("shapeType");
        SerializedProperty origin = area.FindPropertyRelative("originMode");
        SerializedProperty radius = area.FindPropertyRelative("radius");
        SerializedProperty coneAngle = area.FindPropertyRelative("angle");
        SerializedProperty maxCastDistance =
            area.FindPropertyRelative("maxCastDistance");
        EditorGUILayout.PropertyField(shape, new GUIContent("Area Shape"));
        CharacterAreaShapeType shapeType = shape != null
            ? (CharacterAreaShapeType)shape.enumValueIndex
            : CharacterAreaShapeType.Target;
        if (shapeType == CharacterAreaShapeType.Target)
        {
            EditorGUILayout.HelpBox(
                "Target mode uses selection rules and target count; tile " +
                "offsets are not supported.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.PropertyField(origin, new GUIContent("Area Origin"));
        if (radius != null)
        {
            radius.floatValue = Mathf.Max(
                0.1f,
                EditorGUILayout.FloatField("Radius", radius.floatValue));
        }
        if (coneAngle != null)
        {
            coneAngle.floatValue = EditorGUILayout.Slider(
                "Area Angle",
                coneAngle.floatValue,
                0f,
                360f);
        }
        if (origin != null &&
            origin.enumValueIndex ==
                (int)CharacterAreaOriginMode.DesignatedPoint &&
            maxCastDistance != null)
        {
            maxCastDistance.floatValue = Mathf.Max(
                0.1f,
                EditorGUILayout.FloatField(
                    "Maximum Cast Distance",
                    maxCastDistance.floatValue));
        }
        EditorGUILayout.HelpBox(
            origin != null && origin.enumValueIndex ==
                (int)CharacterAreaOriginMode.DesignatedPoint
                ? "Click the origin, then drag to set the sector direction."
                : "Aim the sector from the caster toward the pointer.",
            MessageType.Info);
    }

    internal static void DrawTargetCount(
        SerializedProperty targetCount,
        SerializedProperty area)
    {
        if (targetCount == null)
            return;

        SerializedProperty shape = area?.FindPropertyRelative("shapeType");
        bool usesCircularArea = shape != null &&
            shape.enumValueIndex ==
                (int)CharacterAreaShapeType.CircleSector;
        targetCount.intValue = Mathf.Max(
            usesCircularArea ? 0 : 1,
            EditorGUILayout.IntField(
                usesCircularArea
                    ? "Target Count (0 = All)"
                    : "Target Count",
                targetCount.intValue));
    }

    internal static void DrawEffectList(
        SerializedProperty effects,
        Object owner,
        float? previewAttackPower = null)
    {
        CharacterEditorWindow.DrawEmbeddedEffectList(
            effects,
            owner,
            previewAttackPower);
    }

    internal static void AddDefaultEffect(SerializedProperty effects)
    {
        CharacterEditorWindow.AddEmbeddedDefaultEffect(effects);
    }
}
