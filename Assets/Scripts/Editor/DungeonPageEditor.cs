using UnityEditor;

[CustomEditor(typeof(DungeonPage))]
public sealed class DungeonPageEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "The dungeon grid size now configures logical battle slots only. " +
            "The removed tile-board preview is no longer generated in the scene.",
            MessageType.Info);
    }
}
