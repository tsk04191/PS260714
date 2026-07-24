using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleEditorWindow : EditorWindow
{
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
            "Manual Battle Preview",
            "difficultyPercent",
            "balanceSeed");
        EditorGUILayout.HelpBox(
            "Difficulty Percent and Balance Seed are used only by this " +
            "manual BattleSO preview. A dungeon run generates every battle " +
            "scale and seed when the run starts. Runtime battles use this " +
            "asset's enemy pools, field, stack, spawn, and time settings, " +
            "then calculate enemy count, health, and composition from that " +
            "generated scale.",
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

public static class FireStatusEffectAssetGenerator
{
    private const string EffectFolder =
        "Assets/Animations/Battle/FireStatus";
    private const string HiddenClipPath =
        EffectFolder + "/FireStatusHidden.anim";
    private const string LoopClipPath =
        EffectFolder + "/FireStatusLoop.anim";
    private const string ControllerPath =
        EffectFolder + "/FireStatus.controller";
    private const string TilePrefabPath =
        "Assets/Prefabs/UI/DungeonTile.prefab";
    private const string EffectRootName = "grpFireStatusEffect";

    private static readonly Vector2[] AnchorMins =
    {
        new Vector2(0.03f, 0.02f),
        new Vector2(0.37f, 0.02f),
        new Vector2(0.72f, 0.02f),
    };

    private static readonly Vector2[] AnchorMaxs =
    {
        new Vector2(0.39f, 0.38f),
        new Vector2(0.49f, 0.14f),
        new Vector2(0.96f, 0.26f),
    };

    [InitializeOnLoadMethod]
    private static void ScheduleAutomaticGeneration()
    {
        EditorApplication.delayCall -= GenerateIfRequired;
        EditorApplication.delayCall += GenerateIfRequired;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.delayCall -= GenerateIfRequired;
        EditorApplication.delayCall += GenerateIfRequired;
    }

    private static void GenerateIfRequired()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            !IsGenerationRequired())
        {
            return;
        }

        Generate();
    }

    private static bool IsGenerationRequired()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(HiddenClipPath) == null ||
            AssetDatabase.LoadAssetAtPath<AnimationClip>(LoopClipPath) == null ||
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null)
        {
            return true;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            TilePrefabPath);
        DungeonTileView tile = prefab != null
            ? prefab.GetComponent<DungeonTileView>()
            : null;
        if (tile == null || prefab.transform.Find(EffectRootName) == null)
            return true;

        SerializedObject serializedTile = new(tile);
        return !IsAssignedObjectArray(
                   serializedTile.FindProperty("fireStatusImages"),
                   3) ||
               !IsAssignedObjectArray(
                   serializedTile.FindProperty("fireStatusAnimators"),
                   3);
    }

    private static void Generate()
    {
        try
        {
            EnsureFolder("Assets", "Animations");
            EnsureFolder("Assets/Animations", "Battle");
            EnsureFolder("Assets/Animations/Battle", "FireStatus");

            AnimationClip hidden = GetOrCreateHiddenClip();
            AnimationClip loop = GetOrCreateLoopClip();
            AnimatorController controller = GetOrCreateController(
                hidden,
                loop);
            ConfigureTilePrefab(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static AnimationClip GetOrCreateHiddenClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            HiddenClipPath);
        if (clip != null)
            return clip;

        clip = CreateClip("FireStatusHidden", 1f / 60f, false);
        SetConstantCurve(clip, typeof(CanvasGroup), "m_Alpha", 0f);
        SetConstantCurve(clip, typeof(RectTransform), "m_LocalScale.x", 0.9f);
        SetConstantCurve(clip, typeof(RectTransform), "m_LocalScale.y", 0.9f);
        AssetDatabase.CreateAsset(clip, HiddenClipPath);
        return clip;
    }

    private static AnimationClip GetOrCreateLoopClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            LoopClipPath);
        if (clip != null)
            return clip;

        clip = CreateClip("FireStatusLoop", 1f, true);
        SetConstantCurve(clip, typeof(CanvasGroup), "m_Alpha", 1f);
        AnimationCurve pulse = new(
            new Keyframe(0f, 0.9f),
            new Keyframe(0.5f, 1.1f),
            new Keyframe(1f, 0.9f));
        SetCurve(clip, typeof(RectTransform), "m_LocalScale.x", pulse);
        SetCurve(
            clip,
            typeof(RectTransform),
            "m_LocalScale.y",
            new AnimationCurve(pulse.keys));
        AssetDatabase.CreateAsset(clip, LoopClipPath);
        return clip;
    }

    private static AnimationClip CreateClip(
        string name,
        float duration,
        bool loop)
    {
        AnimationClip clip = new()
        {
            name = name,
            frameRate = 60f,
            wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever,
        };
        AnimationClipSettings settings =
            AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.stopTime = duration;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static void SetConstantCurve(
        AnimationClip clip,
        System.Type componentType,
        string propertyName,
        float value)
    {
        float duration = Mathf.Max(clip.length, 1f / 60f);
        SetCurve(
            clip,
            componentType,
            propertyName,
            new AnimationCurve(
                new Keyframe(0f, value, 0f, 0f),
                new Keyframe(duration, value, 0f, 0f)));
    }

    private static void SetCurve(
        AnimationClip clip,
        System.Type componentType,
        string propertyName,
        AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(
                string.Empty,
                componentType,
                propertyName),
            curve);
    }

    private static AnimatorController GetOrCreateController(
        AnimationClip hiddenClip,
        AnimationClip loopClip)
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
            return controller;

        controller = AnimatorController.CreateAnimatorControllerAtPath(
            ControllerPath);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState hidden = stateMachine.AddState(
            "FireStatusHidden",
            new Vector3(250f, 20f));
        AnimatorState loop = stateMachine.AddState(
            "FireStatusLoop",
            new Vector3(500f, 20f));
        hidden.motion = hiddenClip;
        loop.motion = loopClip;
        stateMachine.defaultState = hidden;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ConfigureTilePrefab(
        RuntimeAnimatorController controller)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(
            TilePrefabPath);
        if (prefabRoot == null)
            throw new System.InvalidOperationException("DungeonTile prefab not found.");

        try
        {
            DungeonTileView tile = prefabRoot.GetComponent<DungeonTileView>();
            if (tile == null)
            {
                throw new System.InvalidOperationException(
                    "DungeonTileView component not found on DungeonTile prefab.");
            }

            RectTransform root = GetOrCreateRectTransform(
                prefabRoot.transform,
                EffectRootName,
                prefabRoot.layer);
            SetStretch(root);
            root.SetAsLastSibling();

            Image[] images = new Image[3];
            Animator[] animators = new Animator[3];
            for (int index = 0; index < 3; index++)
            {
                RectTransform flame = GetOrCreateRectTransform(
                    root,
                    $"imgFireStatus_{index + 1}",
                    prefabRoot.layer);
                flame.anchorMin = AnchorMins[index];
                flame.anchorMax = AnchorMaxs[index];
                flame.anchoredPosition = Vector2.zero;
                flame.sizeDelta = Vector2.zero;
                flame.localRotation = Quaternion.identity;
                flame.localScale = Vector3.one;

                Image image = GetOrAddComponent<Image>(flame.gameObject);
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.color = BattleStatusColors.Fire;

                CanvasGroup canvasGroup =
                    GetOrAddComponent<CanvasGroup>(flame.gameObject);
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;

                Animator animator = GetOrAddComponent<Animator>(
                    flame.gameObject);
                animator.runtimeAnimatorController = controller;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                images[index] = image;
                animators[index] = animator;
            }

            SerializedObject serializedTile = new(tile);
            AssignObjectArray(
                serializedTile.FindProperty("fireStatusImages"),
                images);
            AssignObjectArray(
                serializedTile.FindProperty("fireStatusAnimators"),
                animators);
            serializedTile.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, TilePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static RectTransform GetOrCreateRectTransform(
        Transform parent,
        string objectName,
        int layer)
    {
        Transform existing = parent.Find(objectName);
        if (existing is RectTransform existingRect)
            return existingRect;

        GameObject created = new(objectName, typeof(RectTransform));
        created.layer = layer;
        RectTransform rect = created.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void AssignObjectArray<T>(
        SerializedProperty property,
        T[] values)
        where T : UnityEngine.Object
    {
        if (property == null)
            return;

        property.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue =
                values[index];
        }
    }

    private static bool IsAssignedObjectArray(
        SerializedProperty property,
        int expectedSize)
    {
        if (property == null || property.arraySize != expectedSize)
            return false;

        for (int index = 0; index < property.arraySize; index++)
        {
            if (property.GetArrayElementAtIndex(index).objectReferenceValue ==
                null)
            {
                return false;
            }
        }

        return true;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
