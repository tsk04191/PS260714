using UnityEditor;
using UnityEngine;

public sealed class BattleEditorWindow : EditorWindow
{
    public const string MenuPath = "Tools/Dungeon/First Battle Editor";

    private const string FirstBattlePath =
        "Assets/Data/Battles/FirstBattle.asset";

    private BattleSO _battle;
    private SerializedObject _serializedBattle;
    private Vector2 _scrollPosition;

    public static void Open(BattleSO battle)
    {
        Debug.Log(
            $"[FirstBattleEditor] Open requested. Battle: " +
            $"{(battle != null ? battle.name : "None")}");

        // 저장된 레이아웃에 숨은 창이 남아 있으면 GetWindow가 그 인스턴스를
        // 재사용하므로, 독립된 새 창을 만들기 전에 기존 창을 정리한다.
        foreach (BattleEditorWindow existingWindow in
                 Resources.FindObjectsOfTypeAll<BattleEditorWindow>())
        {
            existingWindow.Close();
        }

        BattleEditorWindow window = CreateInstance<BattleEditorWindow>();
        window.titleContent = new GUIContent("First Battle Editor");
        window.minSize = new Vector2(460f, 620f);
        window.SetBattle(battle);

        Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
        float width = Mathf.Min(560f, Mathf.Max(460f, mainWindow.width - 40f));
        float height = Mathf.Min(720f, Mathf.Max(620f, mainWindow.height - 80f));
        window.position = new Rect(
            mainWindow.x + (mainWindow.width - width) * 0.5f,
            mainWindow.y + (mainWindow.height - height) * 0.5f,
            width,
            height);

        window.ShowAuxWindow();
        window.Focus();
        window.Repaint();

        Debug.Log(
            $"[FirstBattleEditor] Window shown. Instance: " +
            $"{window.GetInstanceID()}");
    }

    [MenuItem(MenuPath)]
    public static void OpenFromMenu()
    {
        BattleSO battle = Selection.activeObject as BattleSO;
        if (battle == null)
        {
            battle = AssetDatabase.LoadAssetAtPath<BattleSO>(
                FirstBattlePath);
        }

        Open(battle);
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("First Battle Editor");
        Debug.Log(
            $"[FirstBattleEditor] Window enabled. Instance: " +
            $"{GetInstanceID()}");
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        BattleSO selectedBattle = (BattleSO)EditorGUILayout.ObjectField(
            "Battle Asset",
            _battle,
            typeof(BattleSO),
            false);
        if (EditorGUI.EndChangeCheck())
            SetBattle(selectedBattle);

        if (_battle == null || _serializedBattle == null)
        {
            EditorGUILayout.HelpBox(
                "Select a BattleSO or open this window from DungeonBattleTab.",
                MessageType.Info);
            return;
        }

        _serializedBattle.Update();
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        DrawSection("Identity", "battleId", "displayName");
        DrawSection("Field", "fieldSize", "maximumStackSize");
        DrawSection(
            "Enemy Spawn",
            "totalEnemyCount",
            "minimumEnemyHealth",
            "randomHealthBonus",
            "spawnInterval",
            "compositionMode");

        SerializedProperty compositionMode =
            _serializedBattle.FindProperty("compositionMode");
        bool usesFixedCounts = compositionMode.enumValueIndex ==
                               (int)EEnemyCompositionMode.FixedCount;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Grade Composition", EditorStyles.boldLabel);
        DrawGradeRule("Normal", "normalEnemies", usesFixedCounts);
        DrawGradeRule("Special", "specialEnemies", usesFixedCounts);
        DrawGradeRule("Elite", "eliteEnemies", usesFixedCounts);
        DrawGradeRule("Boss", "bossEnemies", usesFixedCounts);

        DrawSection("Time Limit", "timeLimit");
        bool changed = _serializedBattle.ApplyModifiedProperties();
        if (changed)
            EditorUtility.SetDirty(_battle);

        DrawValidationPreview();
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(!_battle.TryValidate(out _)))
        {
            if (GUILayout.Button("Save First Battle Settings", GUILayout.Height(30f)))
            {
                EditorUtility.SetDirty(_battle);
                AssetDatabase.SaveAssets();
            }
        }
    }

    private void SetBattle(BattleSO battle)
    {
        _battle = battle;
        _serializedBattle = _battle != null
            ? new SerializedObject(_battle)
            : null;
        Repaint();
    }

    private void DrawSection(string title, params string[] propertyNames)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        foreach (string propertyName in propertyNames)
        {
            SerializedProperty property =
                _serializedBattle.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property);
        }
    }

    private void DrawGradeRule(
        string label,
        string propertyName,
        bool usesFixedCounts)
    {
        SerializedProperty rule = _serializedBattle.FindProperty(propertyName);
        if (rule == null)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        SerializedProperty amount = rule.FindPropertyRelative(
            usesFixedCounts ? "count" : "ratio");
        EditorGUILayout.PropertyField(
            amount,
            new GUIContent(usesFixedCounts ? "Count" : "Ratio Weight"));
        EditorGUILayout.PropertyField(
            rule.FindPropertyRelative("enemyPool"),
            new GUIContent("Enemy Pool"),
            true);
        EditorGUILayout.EndVertical();
    }

    private void DrawValidationPreview()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Resolved Enemy Counts", EditorStyles.boldLabel);
        if (!_battle.TryGetGradeCounts(
                out BattleEnemyGradeCounts counts,
                out string countError))
        {
            EditorGUILayout.HelpBox(countError, MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Normal", counts.Normal.ToString());
        EditorGUILayout.LabelField("Special", counts.Special.ToString());
        EditorGUILayout.LabelField("Elite", counts.Elite.ToString());
        EditorGUILayout.LabelField("Boss", counts.Boss.ToString());
        EditorGUILayout.LabelField("Total", counts.Total.ToString());

        int initialCapacity = _battle.FieldSize * _battle.FieldSize;
        int stackCapacity = initialCapacity * _battle.MaximumStackSize;
        EditorGUILayout.LabelField("Initial Field Capacity", initialCapacity.ToString());
        EditorGUILayout.LabelField("Maximum Stack Capacity", stackCapacity.ToString());

        if (_battle.TryValidate(out string error))
        {
            EditorGUILayout.HelpBox(
                "First battle configuration is valid.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
}
