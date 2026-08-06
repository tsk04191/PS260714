using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AttendanceRewardScheduleSO))]
public sealed class AttendanceRewardScheduleEditor : Editor
{
    private int _selectedDay;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "The monthly calendar always contains 28 reward cells. " +
            "Days 29-31 use the fixed currency reward below.",
            MessageType.Info);

        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("scheduleId"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("contentVersion"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Monthly Reset", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("resetUtcOffsetMinutes"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("resetHour"));

        SerializedProperty days = serializedObject.FindProperty("days");
        if (days.arraySize != AttendanceRewardScheduleSO.MonthlyRewardCount)
            days.arraySize = AttendanceRewardScheduleSO.MonthlyRewardCount;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Monthly Rewards (4 x 7)",
            EditorStyles.boldLabel);
        for (int row = 0; row < 4; row++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int column = 0; column < 7; column++)
            {
                int index = row * 7 + column;
                bool selected = GUILayout.Toggle(
                    _selectedDay == index,
                    $"{index + 1}",
                    "Button",
                    GUILayout.MinWidth(34f));
                if (selected)
                    _selectedDay = index;
            }
            EditorGUILayout.EndHorizontal();
        }

        _selectedDay = Mathf.Clamp(
            _selectedDay,
            0,
            AttendanceRewardScheduleSO.MonthlyRewardCount - 1);
        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(
            days.GetArrayElementAtIndex(_selectedDay),
            new GUIContent($"Day {_selectedDay + 1} Contents"),
            includeChildren: true);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Days 29-31 Fixed Currency",
            EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("extraDayReward"),
            new GUIContent("Fixed Currency Contents"),
            includeChildren: true);

        serializedObject.ApplyModifiedProperties();

        AttendanceRewardScheduleSO schedule =
            target as AttendanceRewardScheduleSO;
        if (schedule != null && !schedule.TryValidate(out string reason))
            EditorGUILayout.HelpBox(reason, MessageType.Error);
    }
}
