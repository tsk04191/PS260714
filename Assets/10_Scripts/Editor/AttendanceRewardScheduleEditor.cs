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
            "The attendance cycle contains 28 sequential rewards. " +
            "Calendar dates do not change the reward order.",
            MessageType.Info);

        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("scheduleId"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("contentVersion"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Cycle", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("repeat"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("resetUtcOffsetMinutes"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("resetHour"));

        SerializedProperty days = serializedObject.FindProperty("days");
        if (days.arraySize != AttendanceRewardScheduleSO.CycleRewardCount)
            days.arraySize = AttendanceRewardScheduleSO.CycleRewardCount;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Sequential Rewards (4 x 7)",
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
            AttendanceRewardScheduleSO.CycleRewardCount - 1);
        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(
            days.GetArrayElementAtIndex(_selectedDay),
            new GUIContent($"Day {_selectedDay + 1} Contents"),
            includeChildren: true);

        serializedObject.ApplyModifiedProperties();

        AttendanceRewardScheduleSO schedule =
            target as AttendanceRewardScheduleSO;
        if (schedule != null && !schedule.TryValidate(out string reason))
            EditorGUILayout.HelpBox(reason, MessageType.Error);
    }
}
