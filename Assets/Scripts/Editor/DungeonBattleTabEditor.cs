using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(DungeonBattleTab))]
public sealed class DungeonBattleTabEditor : Editor
{
    private const string BattleFolder = "Assets/Resources/Battles";
    private const string FirstBattlePath = BattleFolder + "/FirstBattle.asset";

    private SerializedProperty _dungeonPageProperty;

    private void OnEnable()
    {
        _dungeonPageProperty = serializedObject.FindProperty("dungeonPage");
    }

    public override void OnInspectorGUI()
    {
        PS260714EditorText.DrawDefaultInspector(serializedObject);
        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("First Battle Settings", EditorStyles.boldLabel);

        DungeonPage page = ResolveDungeonPage();
        if (page == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a DungeonPage GameObject to enable the first battle editor.",
                MessageType.Warning);
            return;
        }

        SerializedObject pageObject = new(page);
        SerializedProperty firstBattleProperty =
            pageObject.FindProperty("firstBattle");
        BattleSO firstBattle =
            firstBattleProperty.objectReferenceValue as BattleSO;

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(
                "First Battle",
                firstBattle,
                typeof(BattleSO),
                false);
        }

        string buttonLabel = firstBattle == null
            ? "Create First Battle Settings"
            : "Open First Battle Editor";
        if (!GUILayout.Button(buttonLabel, GUILayout.Height(32f)))
            return;

        Debug.Log("[FirstBattleEditor] Inspector button clicked.");

        if (firstBattle == null)
        {
            firstBattle = CreateOrLoadFirstBattleAsset();
            if (firstBattle == null)
                return;

            Undo.RecordObject(page, "Assign First Battle Settings");
            pageObject.Update();
            firstBattleProperty.objectReferenceValue = firstBattle;
            pageObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(page);
            if (page.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(page.gameObject.scene);
        }

        BattleSO battleToOpen = firstBattle;
        EditorApplication.delayCall += () =>
        {
            if (battleToOpen == null)
                return;

            BattleEditorWindow.Open(battleToOpen);
        };
        GUIUtility.ExitGUI();
    }

    private DungeonPage ResolveDungeonPage()
    {
        serializedObject.Update();
        GameObject pageObject =
            _dungeonPageProperty.objectReferenceValue as GameObject;
        return pageObject != null
            ? pageObject.GetComponent<DungeonPage>()
            : null;
    }

    private static BattleSO CreateOrLoadFirstBattleAsset()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(BattleFolder);

        BattleSO battle = AssetDatabase.LoadAssetAtPath<BattleSO>(
            FirstBattlePath);
        if (battle != null)
            return battle;

        battle = CreateInstance<BattleSO>();
        battle.name = "FirstBattle";
        AssetDatabase.CreateAsset(battle, FirstBattlePath);
        PopulateEnemyPools(battle);
        EditorUtility.SetDirty(battle);
        AssetDatabase.SaveAssets();
        return battle;
    }

    private static void PopulateEnemyPools(BattleSO battle)
    {
        SerializedObject battleObject = new(battle);
        string[] enemyGuids = AssetDatabase.FindAssets(
            "t:EnemySO",
            new[] { "Assets/Resources/Enemies" });
        Array.Sort(enemyGuids, StringComparer.Ordinal);

        foreach (string enemyGuid in enemyGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(enemyGuid);
            EnemySO enemy = AssetDatabase.LoadAssetAtPath<EnemySO>(path);
            if (enemy == null)
                continue;

            string ruleName = enemy.Grade switch
            {
                EEnemyGrade.Special => "specialEnemies",
                EEnemyGrade.Elite => "eliteEnemies",
                EEnemyGrade.Boss => "bossEnemies",
                _ => "normalEnemies",
            };
            SerializedProperty pool = battleObject
                .FindProperty(ruleName)
                .FindPropertyRelative("enemyPool");
            int index = pool.arraySize;
            pool.InsertArrayElementAtIndex(index);
            pool.GetArrayElementAtIndex(index).objectReferenceValue = enemy;
        }

        battleObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int separatorIndex = path.LastIndexOf('/');
        string parent = path.Substring(0, separatorIndex);
        string folderName = path.Substring(separatorIndex + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
