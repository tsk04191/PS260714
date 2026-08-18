using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class BattleEditorWindow : EditorWindow
{
    public const string MenuPath = PS260714EditorMenu.BattleEditor;

    private const string AssetFolder = "Assets/06_Runtime/Resources/Battles";
    private const string RenameControlName = "BattleAssetRenameField";

    private readonly List<BattleSO> _battles = new();
    private BattleSO _battle;
    private SerializedObject _serializedBattle;
    private Vector2 _listScroll;
    private Vector2 _scrollPosition;
    private string _searchText = string.Empty;
    private string _balanceMessage;
    private string _renameAssetName = string.Empty;
    private bool _isRenaming;
    private bool _focusRenameField;

    [MenuItem(
        MenuPath,
        false,
        PS260714EditorMenu.BattleEditorPriority)]
    public static void OpenFromMenu()
    {
        Open(null);
    }

    public static void Open(BattleSO battle)
    {
        BattleEditorWindow window = GetWindow<BattleEditorWindow>();
        window.titleContent = new GUIContent("Battle Editor");
        window.minSize = new Vector2(820f, 560f);
        window.RefreshList();
        if (battle != null)
            window.SetBattle(battle);
        window.Focus();
        window.Repaint();
    }

    [MenuItem(
        MenuPath,
        true,
        PS260714EditorMenu.BattleEditorPriority)]
    private static bool ValidateOpenFromMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Battle Editor");
        minSize = new Vector2(820f, 560f);
        RefreshList();

        if (Selection.activeObject is BattleSO selected)
            SetBattle(selected);
        else if (_battle == null && _battles.Count > 0)
            SetBattle(_battles[0]);
    }

    private void OnProjectChange()
    {
        RefreshList();
        Repaint();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is BattleSO selected)
        {
            SetBattle(selected);
            Repaint();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();
        if (_isRenaming)
            DrawRenameRow();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawBattleList();
            DrawDetails();
        }
    }

    private void DrawToolbar()
    {
        PS260714AssetEditorToolbar.Draw(
            $"Battles: {_battles.Count}",
            _battle != null,
            CreateBattle,
            SaveSelected,
            DuplicateSelected,
            BeginRename,
            DeleteSelected,
            () => PS260714AssetEditorList.Ping(_battle),
            RefreshList);
    }

    private void DrawRenameRow()
    {
        PS260714AssetRenameCommand command =
            PS260714EditorAssetUtility.DrawRenameRow(
                "SO File Name",
                RenameControlName,
                ref _renameAssetName,
                ref _focusRenameField);
        if (command == PS260714AssetRenameCommand.Apply)
            RenameSelected();
        else if (command == PS260714AssetRenameCommand.Cancel)
            CancelRename();
    }

    private void DrawBattleList()
    {
        using (new EditorGUILayout.VerticalScope(
                   GUILayout.Width(PS260714AssetEditorList.Width)))
        {
            _searchText =
                PS260714AssetEditorList.DrawSearchField(_searchText);
            int visibleCount = 0;
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            foreach (BattleSO battle in _battles)
            {
                if (battle == null || !MatchesSearch(battle))
                    continue;

                visibleCount++;
                if (PS260714AssetEditorList.DrawAssetRow(
                        battle == _battle,
                        battle,
                        null,
                        battle.DisplayName,
                        $"{battle.BattleId} · {battle.TotalEnemyCount} enemies",
                        AssetDatabase.GetAssetPath(battle)))
                {
                    SetBattle(battle);
                }
            }
            EditorGUILayout.EndScrollView();
            PS260714AssetEditorList.DrawCountFooter(
                visibleCount,
                _battles.Count);
        }
    }

    private void DrawDetails()
    {
        using (new EditorGUILayout.VerticalScope(
                   GUILayout.ExpandWidth(true)))
        {

        if (_battle == null || _serializedBattle == null)
        {
            EditorGUILayout.HelpBox(
                "Select a BattleSO or create one with New.",
                MessageType.Info);
            return;
        }

        _serializedBattle.Update();
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        DrawSection("Identity", "battleId", "displayName");
        DrawProgressBalance();
        DrawSection("Field", "fieldSize", "maximumStackSize");
        DrawArenaSection();
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

        DrawCompletionRewards();
        DrawSection("Time Limit", "timeLimit");
        DrawSection("Presentation Override", "bgmOverride");
        DrawSection(
            "2.5D Environment",
            "environmentBackdrop",
            "environmentBackdropTint",
            "environmentClearColor",
            "environmentCameraFov");
        bool changed = _serializedBattle.ApplyModifiedProperties();
        if (changed)
            EditorUtility.SetDirty(_battle);

        DrawValidationPreview();
        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(!_battle.TryValidate(out _)))
        {
            if (GUILayout.Button("Save Battle Settings", GUILayout.Height(30f)))
            {
                SaveSelected();
            }
        }
        }
    }

    private void DrawCompletionRewards()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Completion Rewards",
            EditorStyles.boldLabel);
        SerializedProperty overrideRecovery =
            _serializedBattle.FindProperty(
                "overrideShieldRecoveryReward");
        EditorGUILayout.PropertyField(
            overrideRecovery,
            new GUIContent("Override Shield Recovery"));
        if (overrideRecovery.boolValue)
        {
            EditorGUILayout.PropertyField(
                _serializedBattle.FindProperty("shieldRecoveryReward"),
                new GUIContent("Shield Recovery"),
                true);
        }
        EditorGUILayout.PropertyField(
            _serializedBattle.FindProperty("cardRewardPool"),
            new GUIContent("Card Reward Pool"),
            true);
        EditorGUILayout.PropertyField(
            _serializedBattle.FindProperty("consumableRewardPool"),
            new GUIContent("Consumable Reward Pool"),
            true);
        EditorGUILayout.HelpBox(
            "Empty battle pools fall back to the dungeon reward pools. " +
            "Only disposable battle items are offered as consumable " +
            "rewards.",
            MessageType.Info);
    }

    private void SetBattle(BattleSO battle)
    {
        if (!ReferenceEquals(_battle, battle))
            CancelRename();
        _battle = battle;
        _serializedBattle = _battle != null
            ? new SerializedObject(_battle)
            : null;
        _scrollPosition = Vector2.zero;
        Repaint();
    }

    private void RefreshList()
    {
        string selectedPath =
            PS260714EditorAssetUtility.CapturePath(_battle);
        PS260714EditorAssetUtility.LoadAssets(
            _battles,
            "t:BattleSO");
        SetBattle(PS260714EditorAssetUtility.RestoreSelection(
            selectedPath,
            _battles));
    }

    private bool MatchesSearch(BattleSO battle)
    {
        string search = (_searchText ?? string.Empty).Trim();
        return string.IsNullOrEmpty(search) ||
               battle.name.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               battle.DisplayName.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               battle.BattleId.IndexOf(
                   search,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void CreateBattle()
    {
        EnsureAssetFolder();
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Battle",
            "NewBattle",
            "asset",
            "Choose a location for the new BattleSO.",
            AssetFolder);
        if (string.IsNullOrEmpty(path))
            return;

        BattleSO battle = CreateInstance<BattleSO>();
        battle.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(battle, path);
        SetIdentity(
            battle,
            CreateUniqueBattleId(NormalizeId(battle.name)),
            battle.name);
        AssetDatabase.SaveAssetIfDirty(battle);
        RefreshList();
        SetBattle(battle);
    }

    private void SaveSelected()
    {
        if (_battle == null)
            return;

        _serializedBattle?.ApplyModifiedProperties();
        EditorUtility.SetDirty(_battle);
        AssetDatabase.SaveAssetIfDirty(_battle);
        ShowNotification(new GUIContent($"Saved {_battle.name}.asset"));
    }

    private void DuplicateSelected()
    {
        if (_battle == null)
            return;

        SaveSelected();
        if (!PS260714EditorAssetUtility.TryDuplicate(
                _battle,
                null,
                " Copy",
                out BattleSO duplicate,
                out string duplicateError))
        {
            EditorUtility.DisplayDialog(
                "Duplicate Battle",
                duplicateError,
                "OK");
            return;
        }
        if (duplicate != null)
        {
            SetIdentity(
                duplicate,
                CreateUniqueBattleId(
                    NormalizeId(duplicate.BattleId + "_copy")),
                duplicate.DisplayName + " Copy");
            AssetDatabase.SaveAssetIfDirty(duplicate);
        }
        RefreshList();
        if (duplicate != null)
            SetBattle(duplicate);
    }

    private void BeginRename()
    {
        if (_battle == null)
            return;
        _renameAssetName = Path.GetFileNameWithoutExtension(
            AssetDatabase.GetAssetPath(_battle));
        _isRenaming = true;
        _focusRenameField = true;
    }

    private void CancelRename()
    {
        _isRenaming = false;
        _focusRenameField = false;
        _renameAssetName = string.Empty;
    }

    private void RenameSelected()
    {
        if (_battle == null)
        {
            CancelRename();
            return;
        }

        if (!PS260714EditorAssetUtility.TryRename(
                _battle,
                _renameAssetName,
                out string error))
        {
            EditorUtility.DisplayDialog("Rename Battle", error, "OK");
            _focusRenameField = true;
            return;
        }

        CancelRename();
        RefreshList();
    }

    private void DeleteSelected()
    {
        if (_battle == null)
            return;

        if (!PS260714SafeAssetDelete.TryMoveToTrash(
                _battle,
                "BattleSO"))
        {
            return;
        }

        _battle = null;
        _serializedBattle = null;
        CancelRename();
        RefreshList();
    }

    private string CreateUniqueBattleId(string baseId)
    {
        string candidate = string.IsNullOrWhiteSpace(baseId)
            ? "battle"
            : baseId;
        string root = candidate;
        int suffix = 2;
        while (_battles.Exists(battle =>
                   battle != null &&
                   string.Equals(
                       battle.BattleId,
                       candidate,
                       StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{root}_{suffix++}";
        }
        return candidate;
    }

    private static string NormalizeId(string value)
    {
        string normalized = (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace(' ', '_');
        return string.IsNullOrWhiteSpace(normalized)
            ? "battle"
            : normalized;
    }

    private static void SetIdentity(
        BattleSO battle,
        string battleId,
        string displayName)
    {
        SerializedObject serialized = new(battle);
        serialized.FindProperty("battleId").stringValue = battleId;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(battle);
    }

    private static void EnsureAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/06_Runtime/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder("Assets/06_Runtime/Resources", "Battles");
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

    private void DrawArenaSection()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Arena", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            _serializedBattle.FindProperty("arenaMode"));
        EditorGUILayout.PropertyField(
            _serializedBattle.FindProperty("coreMaximumHealth"));

        SerializedProperty overrideMaximumEnemies =
            _serializedBattle.FindProperty(
                "overrideDungeonMaximumActiveEnemies");
        EditorGUILayout.PropertyField(
            overrideMaximumEnemies,
            new GUIContent(
                "Override Dungeon Maximum Active Enemies"));
        if (overrideMaximumEnemies.boolValue)
        {
            EditorGUILayout.PropertyField(
                _serializedBattle.FindProperty(
                    "circularMaximumActiveEnemies"),
                new GUIContent("Maximum Active Enemies"));
        }

        EditorGUILayout.PropertyField(
            _serializedBattle.FindProperty("circularLayerSpacing"),
            new GUIContent("Minimum Enemy Spacing"));
        EditorGUILayout.PropertyField(
            _serializedBattle.FindProperty("formationSeparationRatio"));
        EditorGUILayout.PropertyField(
            _serializedBattle.FindProperty("wallRadiusNormalized"));
        EditorGUILayout.PropertyField(
            _serializedBattle.FindProperty("spawnRadiusNormalized"));
        EditorGUILayout.HelpBox(
            "Maximum Active Enemies uses the dungeon setting unless " +
            "this battle explicitly overrides it. Enemies spawn at " +
            "deterministic random positions on the outer ring and move " +
            "inward until another enemy blocks them.",
            MessageType.Info);
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
        DrawEnemyPool(rule.FindPropertyRelative("enemyPool"));
        DrawDetailedEnemyCounts(
            rule.FindPropertyRelative("detailedEnemies"),
            usesFixedCounts ? Mathf.Max(0, amount.intValue) : -1);
        EditorGUILayout.EndVertical();
    }

    private static void DrawEnemyPool(SerializedProperty pool)
    {
        if (pool == null)
            return;

        pool.isExpanded = EditorGUILayout.Foldout(
            pool.isExpanded,
            $"Enemy Pool ({pool.arraySize})",
            true);
        if (!pool.isExpanded)
            return;

        int removeIndex = -1;
        EditorGUI.indentLevel++;
        for (int index = 0; index < pool.arraySize; index++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                SerializedProperty enemy =
                    pool.GetArrayElementAtIndex(index);
                PS260714AssetReferenceField.Draw(
                    enemy,
                    new GUIContent($"Enemy {index + 1}"));
                if (GUILayout.Button("−", GUILayout.Width(24f)))
                    removeIndex = index;
            }
        }
        EditorGUI.indentLevel--;

        if (removeIndex >= 0)
            DeleteArrayElement(pool, removeIndex);
        if (GUILayout.Button("Add Enemy"))
        {
            int index = pool.arraySize;
            pool.InsertArrayElementAtIndex(index);
            pool.GetArrayElementAtIndex(index).objectReferenceValue = null;
        }
    }

    private static void DeleteArrayElement(
        SerializedProperty array,
        int index)
    {
        int previousSize = array.arraySize;
        array.DeleteArrayElementAtIndex(index);
        if (array.arraySize == previousSize)
            array.DeleteArrayElementAtIndex(index);
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
            using (new EditorGUI.DisabledScope(
                       enemy.objectReferenceValue is not EnemySO))
            {
                if (GUILayout.Button("Edit", GUILayout.Width(38f)))
                {
                    EnemyEditorWindow.Open(
                        enemy.objectReferenceValue as EnemySO);
                }
            }
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
        SerializedProperty coreHealth =
            _serializedBattle.FindProperty("coreMaximumHealth");
        if (coreHealth != null)
        {
            coreHealth.intValue = Mathf.RoundToInt(
                Mathf.Lerp(100f, 180f, progress));
        }
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
