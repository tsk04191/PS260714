using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(DungeonPage))]
public sealed class DungeonPageEditor : Editor
{
    private const string ChangeGridUndoName = "Change Dungeon Grid Size";

    private SerializedProperty _initialGridSizeProperty;
    private SerializedProperty _maximumStackSizeProperty;
    private SerializedProperty _boardProperty;

    private void OnEnable()
    {
        _initialGridSizeProperty = serializedObject.FindProperty("initialGridSize");
        _maximumStackSizeProperty = serializedObject.FindProperty("maximumStackSize");
        _boardProperty = serializedObject.FindProperty("board");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        int previousGridSize = _initialGridSizeProperty.intValue;

        EditorGUI.BeginChangeCheck();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        bool inspectorChanged = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        if (!inspectorChanged || Application.isPlaying ||
            serializedObject.isEditingMultipleObjects)
        {
            return;
        }

        serializedObject.Update();
        int currentGridSize = _initialGridSizeProperty.intValue;
        if (currentGridSize == previousGridSize)
            return;

        DungeonPage page = (DungeonPage)target;
        DungeonBoardView board = _boardProperty.objectReferenceValue as DungeonBoardView;
        RebuildGridPreview(
            page,
            board,
            currentGridSize,
            _maximumStackSizeProperty.intValue);
    }

    private static void RebuildGridPreview(
        DungeonPage page,
        DungeonBoardView board,
        int gridSize,
        int maximumStackSize)
    {
        if (page == null || board == null || EditorUtility.IsPersistent(page))
            return;

        SerializedObject boardObject = new(board);
        GridLayoutGroup gridLayout = boardObject
            .FindProperty("gridLayout")
            .objectReferenceValue as GridLayoutGroup;
        DungeonTileView tilePrefab = boardObject
            .FindProperty("tilePrefab")
            .objectReferenceValue as DungeonTileView;

        if (gridLayout == null || tilePrefab == null)
        {
            Debug.LogError(
                "Dungeon grid preview requires configured grid layout and tile prefab references.",
                board);
            return;
        }

        gridSize = Mathf.Clamp(
            gridSize,
            DungeonBoardView.MinimumGridSize,
            DungeonBoardView.MaximumGridSize);
        maximumStackSize = Mathf.Max(1, maximumStackSize);

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(ChangeGridUndoName);
        Undo.RecordObject(gridLayout, ChangeGridUndoName);

        Transform gridRoot = gridLayout.transform;
        for (int index = gridRoot.childCount - 1; index >= 0; index--)
        {
            Transform child = gridRoot.GetChild(index);
            if (child.TryGetComponent(out DungeonTileView _))
                Undo.DestroyObjectImmediate(child.gameObject);
        }

        for (int row = 0; row < gridSize; row++)
        {
            for (int column = 0; column < gridSize; column++)
            {
                GameObject tileObject = PrefabUtility.InstantiatePrefab(
                    tilePrefab.gameObject,
                    gridRoot) as GameObject;

                if (tileObject == null)
                {
                    Debug.LogError("Failed to create a dungeon grid preview tile.", board);
                    Undo.RevertAllDownToGroup(undoGroup);
                    return;
                }

                Undo.RegisterCreatedObjectUndo(tileObject, ChangeGridUndoName);
                tileObject.name = $"grpDungeonTile_{row}_{column}";
                PrefabUtility.RecordPrefabInstancePropertyModifications(tileObject);
            }
        }

        board.Initialize(gridSize, maximumStackSize);
        RecordTilePreviewOverrides(gridRoot);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)gridRoot);

        EditorUtility.SetDirty(board);
        EditorUtility.SetDirty(gridLayout);
        EditorSceneManager.MarkSceneDirty(page.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    private static void RecordTilePreviewOverrides(Transform gridRoot)
    {
        for (int index = 0; index < gridRoot.childCount; index++)
        {
            Transform child = gridRoot.GetChild(index);
            if (!child.TryGetComponent(out DungeonTileView tile))
                continue;

            SerializedObject tileObject = new(tile);
            Image slotSurface = tileObject
                .FindProperty("slotSurface")
                .objectReferenceValue as Image;
            if (slotSurface == null)
                continue;

            EditorUtility.SetDirty(slotSurface);
            PrefabUtility.RecordPrefabInstancePropertyModifications(slotSurface);
        }
    }
}
