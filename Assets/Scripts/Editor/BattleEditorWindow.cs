using System.Collections.Generic;
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
    private string _balanceMessage;

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
        DrawProgressBalance();
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
        DrawDetailedEnemyCounts(
            rule.FindPropertyRelative("detailedEnemies"),
            usesFixedCounts ? Mathf.Max(0, amount.intValue) : -1);
        EditorGUILayout.EndVertical();
    }

    private void DrawProgressBalance()
    {
        DrawSection(
            "Progress Balance",
            "difficultyPercent",
            "balanceSeed");
        EditorGUILayout.HelpBox(
            "Difficulty uses the run progress scale: 0% is the start and " +
            "100% is the final objective. Auto Balance keeps field size and " +
            "time limit, then safely randomizes enemy pressure.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("New Seed", GUILayout.Height(26f)))
        {
            _serializedBattle.FindProperty("balanceSeed").intValue =
                System.Environment.TickCount;
        }

        if (GUILayout.Button("Auto Balance", GUILayout.Height(26f)))
        {
            ApplyAutomaticBalance();
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(_balanceMessage))
            EditorGUILayout.HelpBox(_balanceMessage, MessageType.Info);
    }

    private static void DrawDetailedEnemyCounts(
        SerializedProperty details,
        int resolvedGradeCount)
    {
        if (details == null)
            return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            "Exact Enemy Counts",
            EditorStyles.miniBoldLabel);

        int exactTotal = 0;
        int removeIndex = -1;
        for (int index = 0; index < details.arraySize; index++)
        {
            SerializedProperty detail = details.GetArrayElementAtIndex(index);
            SerializedProperty enemy = detail.FindPropertyRelative("enemy");
            SerializedProperty count = detail.FindPropertyRelative("count");
            exactTotal += Mathf.Max(0, count.intValue);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(enemy, GUIContent.none);
            GUILayout.Label("Count", GUILayout.Width(38f));
            EditorGUILayout.PropertyField(
                count,
                GUIContent.none,
                GUILayout.Width(52f));
            if (GUILayout.Button("-", GUILayout.Width(24f)))
                removeIndex = index;
            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
            details.DeleteArrayElementAtIndex(removeIndex);

        if (GUILayout.Button("Add Exact Enemy Count"))
        {
            int index = details.arraySize;
            details.InsertArrayElementAtIndex(index);
            SerializedProperty detail = details.GetArrayElementAtIndex(index);
            detail.FindPropertyRelative("enemy").objectReferenceValue = null;
            detail.FindPropertyRelative("count").intValue = 0;
        }

        if (resolvedGradeCount >= 0)
        {
            int randomRemaining = Mathf.Max(0, resolvedGradeCount - exactTotal);
            EditorGUILayout.LabelField(
                $"Exact {exactTotal} / Grade {resolvedGradeCount} / " +
                $"Random Remaining {randomRemaining}",
                EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField(
                $"Exact Count Total: {exactTotal}",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.HelpBox(
            "Listed enemies use an exact count. The remaining grade count " +
            "is selected only from unlisted enemies in the pool.",
            MessageType.None);
    }

    private void ApplyAutomaticBalance()
    {
        SerializedProperty difficultyProperty =
            _serializedBattle.FindProperty("difficultyPercent");
        SerializedProperty seedProperty =
            _serializedBattle.FindProperty("balanceSeed");
        int difficulty = Mathf.Clamp(difficultyProperty.intValue, 0, 100);
        float progress = difficulty / 100f;
        int balanceSeed = seedProperty.intValue;
        System.Random random = new(balanceSeed);

        SerializedProperty fieldProperty =
            _serializedBattle.FindProperty("fieldSize");
        int fieldSize = Mathf.Clamp(
            fieldProperty.intValue,
            DungeonBoardView.MinimumGridSize,
            DungeonBoardView.MaximumGridSize);
        int tileCount = fieldSize * fieldSize;

        int totalEnemyCount = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Lerp(16f, 32f, progress)) +
            random.Next(-2, 3),
            10,
            40);
        int minimumHealth = Mathf.Max(
            1,
            Mathf.RoundToInt(
                Mathf.Lerp(18f, 38f, progress) *
                Mathf.Lerp(0.95f, 1.05f, (float)random.NextDouble())));
        int randomHealthBonus = Mathf.RoundToInt(
            Mathf.Lerp(4f, 10f, progress));
        float spawnInterval = TimePrecision.Normalize(
            Mathf.Lerp(4.5f, 2.75f, progress) *
            Mathf.Lerp(0.95f, 1.05f, (float)random.NextDouble()),
            1.5f);
        int maximumStackSize = Mathf.Clamp(
            Mathf.CeilToInt(totalEnemyCount / (float)tileCount) + 1,
            2,
            4);

        _serializedBattle.FindProperty("totalEnemyCount").intValue =
            totalEnemyCount;
        _serializedBattle.FindProperty("minimumEnemyHealth").intValue =
            minimumHealth;
        _serializedBattle.FindProperty("randomHealthBonus").intValue =
            randomHealthBonus;
        _serializedBattle.FindProperty("spawnInterval").floatValue =
            spawnInterval;
        _serializedBattle.FindProperty("maximumStackSize").intValue =
            maximumStackSize;
        _serializedBattle.FindProperty("compositionMode").enumValueIndex =
            (int)EEnemyCompositionMode.FixedCount;

        bool hasNormal = HasEnemyPool("normalEnemies");
        bool hasSpecial = HasEnemyPool("specialEnemies");
        bool hasElite = HasEnemyPool("eliteEnemies");
        bool hasBoss = HasEnemyPool("bossEnemies");

        int bossCount = hasBoss && difficulty >= 100 ? 1 : 0;
        int eliteCount = 0;
        if (hasElite && difficulty >= 45)
        {
            eliteCount = Mathf.Min(
                difficulty >= 75 ? 2 : 1,
                Mathf.RoundToInt(totalEnemyCount *
                    Mathf.Lerp(0.03f, 0.1f, progress)));
        }

        int specialCount = hasSpecial && difficulty >= 15
            ? Mathf.Clamp(
                Mathf.RoundToInt(totalEnemyCount *
                    Mathf.Lerp(0.06f, 0.22f, progress)),
                1,
                Mathf.CeilToInt(totalEnemyCount * 0.25f))
            : 0;
        int normalCount = totalEnemyCount -
                          specialCount - eliteCount - bossCount;

        if (!hasNormal)
        {
            int unassigned = normalCount;
            normalCount = 0;
            if (hasSpecial)
                specialCount += unassigned;
            else if (hasElite)
                eliteCount += unassigned;
            else if (hasBoss)
                bossCount += unassigned;
        }

        SetGradeCount("normalEnemies", normalCount);
        SetGradeCount("specialEnemies", specialCount);
        SetGradeCount("eliteEnemies", eliteCount);
        SetGradeCount("bossEnemies", bossCount);

        bool usedFallback = false;
        usedFallback |= !GenerateDetailedCounts(
            "normalEnemies", normalCount, difficulty, fieldSize,
            totalEnemyCount, random);
        usedFallback |= !GenerateDetailedCounts(
            "specialEnemies", specialCount, difficulty, fieldSize,
            totalEnemyCount, random);
        usedFallback |= !GenerateDetailedCounts(
            "eliteEnemies", eliteCount, difficulty, fieldSize,
            totalEnemyCount, random);
        usedFallback |= !GenerateDetailedCounts(
            "bossEnemies", bossCount, difficulty, fieldSize,
            totalEnemyCount, random);

        Undo.RecordObject(_battle, "Auto Balance Battle");
        _serializedBattle.ApplyModifiedProperties();
        EditorUtility.SetDirty(_battle);
        _serializedBattle.Update();

        float totalThreat = 0f;
        if (_battle.TryCreateSetup(
                balanceSeed,
                out BattleSetup generatedSetup,
                out _))
        {
            foreach (EnemyRuntime enemy in generatedSetup.Enemies)
                totalThreat += enemy.Definition.ThreatCost;
        }

        _balanceMessage = usedFallback
            ? $"Generated difficulty {difficulty}% with seed {balanceSeed}. " +
              "A pool required a low-threat fallback because its safe type " +
              $"caps were too small. Threat {totalThreat:0.0}."
            : $"Generated difficulty {difficulty}% with seed {balanceSeed}. " +
              $"Threat {totalThreat:0.0}. Field size and time limit were preserved.";
    }

    private bool HasEnemyPool(string ruleName)
    {
        SerializedProperty rule = _serializedBattle.FindProperty(ruleName);
        SerializedProperty pool = rule?.FindPropertyRelative("enemyPool");
        if (pool == null)
            return false;

        for (int index = 0; index < pool.arraySize; index++)
        {
            if (pool.GetArrayElementAtIndex(index)
                    .objectReferenceValue is EnemySO)
            {
                return true;
            }
        }

        return false;
    }

    private void SetGradeCount(string ruleName, int count)
    {
        SerializedProperty rule = _serializedBattle.FindProperty(ruleName);
        if (rule != null)
            rule.FindPropertyRelative("count").intValue = Mathf.Max(0, count);
    }

    private bool GenerateDetailedCounts(
        string ruleName,
        int gradeCount,
        int difficulty,
        int fieldSize,
        int totalEnemyCount,
        System.Random random)
    {
        SerializedProperty rule = _serializedBattle.FindProperty(ruleName);
        SerializedProperty poolProperty =
            rule?.FindPropertyRelative("enemyPool");
        SerializedProperty detailsProperty =
            rule?.FindPropertyRelative("detailedEnemies");
        if (poolProperty == null || detailsProperty == null)
            return gradeCount == 0;

        detailsProperty.arraySize = 0;
        if (gradeCount <= 0)
            return true;

        List<EnemySO> pool = new();
        for (int index = 0; index < poolProperty.arraySize; index++)
        {
            EnemySO enemy = poolProperty
                .GetArrayElementAtIndex(index)
                .objectReferenceValue as EnemySO;
            if (enemy != null && !pool.Contains(enemy))
                pool.Add(enemy);
        }

        if (pool.Count == 0)
            return false;

        Dictionary<EnemySO, int> counts = new();
        bool usedFallback = false;
        float minimumThreat = float.MaxValue;
        foreach (EnemySO enemy in pool)
        {
            if (GetEnemyTypeCap(
                    enemy.Type,
                    difficulty,
                    fieldSize,
                    totalEnemyCount) > 0)
            {
                minimumThreat = Mathf.Min(minimumThreat, enemy.ThreatCost);
            }
        }

        if (minimumThreat == float.MaxValue)
            minimumThreat = pool[0].ThreatCost;

        float gradeThreatBudget = Mathf.Max(
            minimumThreat * gradeCount,
            GetGradeAverageThreatCap(ruleName, difficulty / 100f) *
            gradeCount);
        float currentThreat = 0f;
        for (int slot = 0; slot < gradeCount; slot++)
        {
            List<EnemySO> candidates = new();
            foreach (EnemySO enemy in pool)
            {
                counts.TryGetValue(enemy, out int currentCount);
                int cap = GetEnemyTypeCap(
                    enemy.Type,
                    difficulty,
                    fieldSize,
                    totalEnemyCount);
                int remainingSlots = gradeCount - slot - 1;
                float minimumRemainingThreat =
                    remainingSlots * minimumThreat;
                bool fitsThreatBudget = currentThreat +
                    enemy.ThreatCost + minimumRemainingThreat <=
                    gradeThreatBudget + 0.001f;
                if (currentCount < cap && fitsThreatBudget)
                    candidates.Add(enemy);
            }

            if (candidates.Count == 0)
            {
                usedFallback = true;
                EnemySO safestEnemy = pool[0];
                foreach (EnemySO enemy in pool)
                {
                    if (enemy.ThreatCost < safestEnemy.ThreatCost)
                        safestEnemy = enemy;
                }

                candidates.Add(safestEnemy);
            }

            EnemySO selected = SelectWeightedEnemy(
                candidates,
                difficulty / 100f,
                random);
            counts.TryGetValue(selected, out int selectedCount);
            counts[selected] = selectedCount + 1;
            currentThreat += selected.ThreatCost;
        }

        List<EnemySO> orderedEnemies = new(counts.Keys);
        orderedEnemies.Sort((left, right) =>
        {
            int typeOrder = left.Type.CompareTo(right.Type);
            return typeOrder != 0
                ? typeOrder
                : string.CompareOrdinal(left.name, right.name);
        });

        detailsProperty.arraySize = orderedEnemies.Count;
        for (int index = 0; index < orderedEnemies.Count; index++)
        {
            EnemySO enemy = orderedEnemies[index];
            SerializedProperty detail =
                detailsProperty.GetArrayElementAtIndex(index);
            detail.FindPropertyRelative("enemy").objectReferenceValue = enemy;
            detail.FindPropertyRelative("count").intValue = counts[enemy];
        }

        return !usedFallback;
    }

    private static float GetGradeAverageThreatCap(
        string ruleName,
        float progress)
    {
        return ruleName switch
        {
            "specialEnemies" => Mathf.Lerp(1.4f, 1.9f, progress),
            "eliteEnemies" => Mathf.Lerp(1.7f, 2.3f, progress),
            "bossEnemies" => Mathf.Lerp(2f, 3f, progress),
            _ => Mathf.Lerp(1.1f, 1.5f, progress),
        };
    }

    private static EnemySO SelectWeightedEnemy(
        IReadOnlyList<EnemySO> candidates,
        float progress,
        System.Random random)
    {
        double exponent = Mathf.Lerp(-1.5f, 1.25f, progress);
        double totalWeight = 0d;
        double[] weights = new double[candidates.Count];
        for (int index = 0; index < candidates.Count; index++)
        {
            double cost = Mathf.Max(0.1f, candidates[index].ThreatCost);
            double jitter = 0.85d + random.NextDouble() * 0.3d;
            weights[index] = System.Math.Pow(cost, exponent) * jitter;
            totalWeight += weights[index];
        }

        double value = random.NextDouble() * totalWeight;
        for (int index = 0; index < candidates.Count; index++)
        {
            value -= weights[index];
            if (value <= 0d)
                return candidates[index];
        }

        return candidates[candidates.Count - 1];
    }

    private static int GetEnemyTypeCap(
        EEnemyType type,
        int difficulty,
        int fieldSize,
        int totalEnemyCount)
    {
        return type switch
        {
            EEnemyType.Heavy => Mathf.Max(
                1,
                Mathf.CeilToInt(totalEnemyCount * 0.3f)),
            EEnemyType.Medic => difficulty < 10
                ? 0
                : Mathf.Max(1, Mathf.CeilToInt(totalEnemyCount * 0.15f)),
            EEnemyType.Mechanic => difficulty < 20
                ? 0
                : difficulty >= 85 ? 2 : 1,
            EEnemyType.Pointman => difficulty < 20
                ? 0
                : difficulty >= 70 ? 2 : 1,
            EEnemyType.ShieldBearer => difficulty < 30 || fieldSize <= 3
                ? 0
                : fieldSize >= 6 && difficulty >= 85 ? 2 : 1,
            EEnemyType.Infiltrator => difficulty < 15
                ? 0
                : Mathf.Max(1, Mathf.FloorToInt(totalEnemyCount * 0.2f)),
            _ => totalEnemyCount,
        };
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
