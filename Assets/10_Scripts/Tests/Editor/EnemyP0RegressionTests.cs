using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static TestReflection;

public sealed class EnemyP0RegressionTests
{
    private const string EnemyAssetFolder = "Assets/06_Runtime/Resources/Enemies/";
    private const string FireStatusPath =
        "Assets/06_Runtime/Resources/StatusEffects/Fire.asset";
    private const string StunStatusPath =
        "Assets/06_Runtime/Resources/StatusEffects/Stun.asset";

    private static readonly BindingFlags InstanceNonPublic =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<UnityEngine.Object> _createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = _createdObjects.Count - 1; index >= 0; index--)
        {
            if (_createdObjects[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    _createdObjects[index]);
            }
        }

        _createdObjects.Clear();
    }

    [Test]
    public void Basic_HasNoSpecialRuntimeModifiers()
    {
        EnemySO basic = LoadEnemy("Basic");
        EnemyRuntime runtime = basic.CreateRuntime();

        Assert.That(runtime.SpawnIntervalMultiplier, Is.EqualTo(1f));
        Assert.That(runtime.Armor, Is.Zero);
        Assert.That(runtime.IsTargetPriorityExcluded, Is.False);
        Assert.That(runtime.AbilityCooldownRemaining, Is.Zero);
    }

    [Test]
    public void EnemyRuntime_AppliesAuthoredHealthAndInitialDefenseStats()
    {
        EnemySO definition = UnityEngine.Object.Instantiate(
            LoadEnemy("Basic"));
        definition.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(definition);
        SerializedObject serialized = new(definition);
        serialized.FindProperty("healthScale").floatValue = 1.5f;
        serialized.FindProperty("initialArmor").intValue = 3;
        serialized.FindProperty("initialShield").intValue = 4;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnemyRuntime runtime = definition.CreateRuntime(40);

        Assert.That(runtime.MaxHealth, Is.EqualTo(60));
        Assert.That(runtime.Health, Is.EqualTo(60));
        Assert.That(runtime.Armor, Is.EqualTo(3));
        Assert.That(runtime.CurrentShield, Is.EqualTo(4));
    }

    [Test]
    public void FixedDamage_BypassesShieldArmorAndIncomingProtection()
    {
        EnemySO definition = UnityEngine.Object.Instantiate(
            LoadEnemy("Basic"));
        definition.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(definition);
        SerializedObject serialized = new(definition);
        serialized.FindProperty("healthScale").floatValue = 1f;
        serialized.FindProperty("initialArmor").intValue = 10;
        serialized.FindProperty("initialShield").intValue = 10;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EnemyRuntime runtime = definition.CreateRuntime(50);
        StatusEffectSO protection = CreateIncomingDamageStatus(-0.5f);

        Assert.That(
            runtime.ApplyStatusEffect(protection, 10f, 1),
            Is.True);

        int applied = runtime.TakeDamage(
            20,
            CharacterAttackDamageType.Fixed);

        Assert.That(applied, Is.EqualTo(20));
        Assert.That(runtime.Health, Is.EqualTo(30));
        Assert.That(runtime.CurrentShield, Is.EqualTo(10));
        Assert.That(runtime.Armor, Is.EqualTo(10));
    }

    [Test]
    public void BattleItemEnemyArea_UsesClickedTargetOnly()
    {
        EnemyRuntime center = LoadEnemy("Basic").CreateRuntime(20);
        EnemyRuntime adjacent = LoadEnemy("Basic").CreateRuntime(20);
        DungeonBoardView board = CreateBoard(
            (1, 1, center),
            (1, 2, adjacent));
        BattleItemSO item = ScriptableObject.CreateInstance<BattleItemSO>();
        item.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(item);

        SerializedObject serialized = new(item);
        serialized.FindProperty("targetType").enumValueIndex =
            (int)BattleItemTargetType.Enemy;
        SerializedProperty effects = serialized.FindProperty("effects");
        effects.arraySize = 1;
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("effectType").enumValueIndex =
            (int)BattleItemEffectType.FixedDamage;
        effect.FindPropertyRelative("amount").intValue = 5;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        int centerHealth = center.Health;
        int adjacentHealth = adjacent.Health;
        bool applied = BattleItemUseExecutor.TryApplyToEnemy(
            item,
            board,
            center);

        Assert.That(applied, Is.True);
        Assert.That(center.Health, Is.EqualTo(centerHealth - 5));
        Assert.That(adjacent.Health, Is.EqualTo(adjacentHealth));
    }

    [Test]
    public void StatusCondition_BuffAndDebuffScopesCountActiveStatuses()
    {
        StatusEffectSO firstBuff =
            CreateStatusForConditionScope(
                "enemy-condition-buff-first",
                StatusEffectAlignment.Buff);
        StatusEffectSO secondBuff =
            CreateStatusForConditionScope(
                "enemy-condition-buff-second",
                StatusEffectAlignment.Buff);
        StatusEffectSO debuff =
            CreateStatusForConditionScope(
                "enemy-condition-debuff",
                StatusEffectAlignment.Debuff);
        IReadOnlyList<BattleStatusSnapshot> activeStatuses =
            new[]
            {
                new BattleStatusSnapshot(firstBuff, 1, 5f),
                new BattleStatusSnapshot(secondBuff, 1, 5f),
                new BattleStatusSnapshot(debuff, 1, 5f),
            };

        EnemyAbilityConditionDefinition condition = new();
        SetPrivateField(
            condition,
            "type",
            EnemyAbilityConditionType.SourceHasStatus);
        SetPrivateField(
            condition,
            "statusSelectionScope",
            CharacterStatusSelectionScope.AllBuffs);
        SetPrivateField(
            condition,
            "statusMatchMode",
            CharacterStatusConditionMatchMode.AtLeastCount);
        SetPrivateField(condition, "statusMatchCount", 2);
        SetPrivateField(condition, "expected", true);

        Assert.That(
            EvaluateEnemyStatusCondition(
                condition,
                _ => false,
                activeStatuses),
            Is.True);

        SetPrivateField(
            condition,
            "statusSelectionScope",
            CharacterStatusSelectionScope.AllDebuffs);
        Assert.That(
            EvaluateEnemyStatusCondition(
                condition,
                _ => false,
                activeStatuses),
            Is.False);

        SetPrivateField(condition, "statusMatchCount", 1);
        Assert.That(
            EvaluateEnemyStatusCondition(
                condition,
                _ => false,
                activeStatuses),
            Is.True);
    }

    [Test]
    public void EnemyCodex_DeduplicatesByIdButKeepsTypeVariants()
    {
        EnemySO basic = LoadEnemy("Basic");
        EnemySO duplicateId =
            UnityEngine.Object.Instantiate(basic);
        duplicateId.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(duplicateId);
        EnemySO duplicateType =
            UnityEngine.Object.Instantiate(basic);
        duplicateType.hideFlags = HideFlags.HideAndDontSave;
        SerializedObject serializedDuplicate =
            new(duplicateType);
        serializedDuplicate.FindProperty("enemyId").stringValue =
            "basic_variant";
        serializedDuplicate.ApplyModifiedPropertiesWithoutUndo();
        _createdObjects.Add(duplicateType);
        GameObject pageObject = new(
            "EnemyCodexDuplicateTest",
            typeof(RectTransform),
            typeof(EnemyCodexPage));
        _createdObjects.Add(pageObject);
        EnemyCodexPage page =
            pageObject.GetComponent<EnemyCodexPage>();
        SerializedObject serializedPage = new(page);
        SerializedProperty definitions =
            serializedPage.FindProperty("enemyDefinitions");
        definitions.arraySize = 3;
        definitions.GetArrayElementAtIndex(0).objectReferenceValue =
            basic;
        definitions.GetArrayElementAtIndex(1).objectReferenceValue =
            duplicateId;
        definitions.GetArrayElementAtIndex(2).objectReferenceValue =
            duplicateType;
        serializedPage.ApplyModifiedPropertiesWithoutUndo();

        MethodInfo refreshEntries = typeof(EnemyCodexPage).GetMethod(
            "RefreshEntries",
            InstanceNonPublic);
        Assert.That(refreshEntries, Is.Not.Null);
        refreshEntries.Invoke(page, null);

        object entries = typeof(EnemyCodexPage).GetField(
                "_entries",
                InstanceNonPublic)
            ?.GetValue(page);
        Assert.That(entries, Is.Not.Null);
        int count = (int)entries.GetType()
            .GetProperty("Count")
            ?.GetValue(entries);
        Assert.That(
            count,
            Is.EqualTo(
                Enum.GetValues(typeof(EEnemyType)).Length + 1));
    }

    [Test]
    public void AssaultRoster_UsesNeutralSpawnInterval()
    {
        BattleManager manager = CreateBattleManager();
        SetPrivateField(manager, "_spawnInterval", 10f);
        GetPrivateList<EnemyRuntime>(manager, "_spawnQueue").Add(
            LoadEnemy("Assault").CreateRuntime());

        Assert.That(manager.SpawnInterval, Is.EqualTo(10f));
    }

    [Test]
    public void SpawnInterval_IsRandomizedAroundAuthoredInterval()
    {
        Assert.That(
            BattleManager.ResolveRandomizedSpawnInterval(4f, 0f),
            Is.EqualTo(3f));
        Assert.That(
            BattleManager.ResolveRandomizedSpawnInterval(4f, 0.5f),
            Is.EqualTo(4f));
        Assert.That(
            BattleManager.ResolveRandomizedSpawnInterval(4f, 1f),
            Is.EqualTo(5f));
    }

    [Test]
    public void SpawnLine_UsesLongestHorizontalViewportEdge()
    {
        Vector2[] corners =
        {
            new(-3f, -2f),
            new(-5f, 4f),
            new(3f, -2f),
            new(5f, 4f),
        };

        float radius = DungeonWorldSpawnGeometry.ResolveSpawnLineRadius(
            corners,
            0.5f);

        Assert.That(
            radius,
            Is.EqualTo(Mathf.Sqrt(41f) + 0.5f).Within(0.0001f));
    }

    [Test]
    public void SpawnDirection_UsesRandomPointOnCircularLine()
    {
        Vector2 first =
            DungeonWorldSpawnGeometry.DirectionFromUnitSample(0f);
        Vector2 second =
            DungeonWorldSpawnGeometry.DirectionFromUnitSample(0.25f);

        Assert.That(first, Is.EqualTo(Vector2.right));
        Assert.That(second.x, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(second.y, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void MedicRoster_TargetsLowestHealthAllyInWorldRadius()
    {
        EnemySO medic = LoadEnemy("Medic");
        Assert.That(medic.EnemyId, Is.EqualTo("S001"));
        Assert.That(medic.Abilities, Has.Count.EqualTo(1));

        EnemyAbilityDefinition ability = medic.Abilities[0];
        Assert.That(ability.Cooldown, Is.EqualTo(4f));
        Assert.That(
            ability.Target.Faction,
            Is.EqualTo(EnemyAbilityTargetFaction.EnemyAllies));
        Assert.That(
            ability.Target.Subject,
            Is.EqualTo(EnemyAbilityTargetSubject.WorldRadius));
        Assert.That(
            ability.Target.Metric,
            Is.EqualTo(EnemyAbilityTargetMetric.HealthPercentage));
        Assert.That(ability.Target.TargetCount, Is.EqualTo(1));
        Assert.That(ability.Target.WorldRadius, Is.EqualTo(3f));
        CharacterEffectDefinition heal =
            ability.Operations[0].Effects[0];
        Assert.That(heal.Type, Is.EqualTo(CharacterEffectType.Heal));
        Assert.That(heal.TargetMaxHealthScale, Is.EqualTo(0.08f));
    }

    [Test]
    public void MedicRoster_UsesSuccessOnlyCooldownReset()
    {
        EnemySO medic = LoadEnemy("Medic");
        EnemyAbilityDefinition ability = medic.Abilities[0];

        Assert.That(
            ability.CooldownResetPolicy,
            Is.EqualTo(
                EnemyAbilityCooldownResetPolicy.OnSuccessfulActivation));
        Assert.That(
            ability.ChargeConsumptionPolicy,
            Is.EqualTo(
                EnemyAbilityChargeConsumptionPolicy
                    .OnSuccessfulActivation));
        Assert.That(ability.HasUnlimitedCharges, Is.True);
    }

    [Test]
    public void Mechanic_StunsFirstHighestDamageCharacter()
    {
        EnemyRuntime mechanic = LoadEnemy("Mechanic").CreateRuntime();
        DungeonBoardView board = CreateBoard((1, 1, mechanic));
        FakeBattleCharacter low = new(3);
        FakeBattleCharacter highest = new(12);
        FakeBattleCharacter tiedLater = new(12);

        board.TickEnemyAbilities(
            10f,
            new IBattleCharacter[] { low, highest, tiedLater });

        Assert.That(low.StatusApplicationCount, Is.Zero);
        Assert.That(highest.StatusApplicationCount, Is.EqualTo(1));
        Assert.That(tiedLater.StatusApplicationCount, Is.Zero);
        Assert.That(
            highest.LastAppliedStatus,
            Is.SameAs(StatusEffectDefinitionCatalog.FindById(
                StatusEffectIds.Stun)));
        Assert.That(highest.LastAppliedDuration, Is.EqualTo(5f));
        Assert.That(highest.LastAppliedStacks, Is.EqualTo(1));
        Assert.That(
            mechanic.AbilityCooldownRemaining,
            Is.EqualTo(10f).Within(0.0001f));
    }

    [Test]
    public void Mechanic_ZeroDamageTieSelectsFirstCandidate()
    {
        EnemyRuntime mechanic = LoadEnemy("Mechanic").CreateRuntime();
        DungeonBoardView board = CreateBoard((1, 1, mechanic));
        FakeBattleCharacter first = new(0);
        FakeBattleCharacter second = new(0);

        board.TickEnemyAbilities(
            10f,
            new IBattleCharacter[] { first, second });

        Assert.That(first.StatusApplicationCount, Is.EqualTo(1));
        Assert.That(second.StatusApplicationCount, Is.Zero);
        Assert.That(
            mechanic.AbilityCooldownRemaining,
            Is.EqualTo(10f).Within(0.0001f));
    }

    [Test]
    public void SpawnQueue_SkipsBlockedEnemyAndSpawnsNextEligibleEnemy()
    {
        BattleManager manager = CreateBattleManager();
        RecordingBattleBoard board = new();
        SetPrivateField(manager, "_board", board);
        EnemyRuntime blocked = LoadEnemy("Basic").CreateRuntime();
        EnemyRuntime eligible = LoadEnemy("Assault").CreateRuntime();
        board.SpawnEvaluator = enemy => ReferenceEquals(enemy, eligible);
        List<EnemyRuntime> queue =
            GetPrivateList<EnemyRuntime>(manager, "_spawnQueue");
        queue.Add(blocked);
        queue.Add(eligible);

        Assert.That(InvokeTrySpawn(manager), Is.True);

        Assert.That(board.LastSpawnGroup, Is.EqualTo(new[] { eligible }));
        Assert.That(manager.SpawnQueue, Is.EqualTo(new[] { blocked }));
        Assert.That(manager.SpawnedEnemyCount, Is.EqualTo(1));
        Assert.That(manager.IsBoardFull, Is.False);
    }

    [Test]
    public void SpawnQueue_WaitsUntilOccupancyChangeBeforeRetrying()
    {
        BattleManager manager = CreateBattleManager();
        RecordingBattleBoard board = new()
        {
            AllowSpawn = false,
        };
        SetPrivateField(manager, "_board", board);
        WireBoardOccupancyEvent(manager, board);
        GetPrivateList<EnemyRuntime>(manager, "_spawnQueue").Add(
            LoadEnemy("Basic").CreateRuntime());

        Assert.That(InvokeTrySpawn(manager), Is.False);
        Assert.That(manager.IsBoardFull, Is.True);

        InvokeTickSpawnQueue(manager, 10f);
        Assert.That(manager.PendingEnemyCount, Is.EqualTo(1));

        board.AllowSpawn = true;
        board.RaiseOccupancyChanged();
        InvokeTickSpawnQueue(manager, 0f);

        Assert.That(manager.PendingEnemyCount, Is.Zero);
        Assert.That(manager.SpawnedEnemyCount, Is.EqualTo(1));
        Assert.That(manager.IsBoardFull, Is.False);
    }

    [Test]
    public void ExclusiveTwoByTwoEnemy_ReservesAndReleasesEveryCell()
    {
        DungeonBoardView board = CreatePlaceableBoard();
        EnemySO definition = CreateFootprintDefinition(2, 2);
        EnemyRuntime largeEnemy = definition.CreateRuntime();

        Assert.That(board.TryAddEnemyCard(0, 0, largeEnemy), Is.True);
        Assert.That(board.GetStackCount(0, 0), Is.EqualTo(1));
        Assert.That(board.GetStackCount(0, 1), Is.EqualTo(1));
        Assert.That(board.GetStackCount(1, 0), Is.EqualTo(1));
        Assert.That(board.GetStackCount(1, 1), Is.EqualTo(1));
        Assert.That(
            board.TryAddEnemyCard(
                1,
                1,
                LoadEnemy("Basic").CreateRuntime()),
            Is.False);
        Assert.That(board.TryRemoveTopEnemyCard(1, 1), Is.True);
        Assert.That(board.GetStackCount(0, 0), Is.Zero);
        Assert.That(board.GetStackCount(1, 1), Is.Zero);
    }

    [Test]
    public void DistinctGroupPlacement_IsAtomicWhenFootprintsCannotFit()
    {
        DungeonBoardView board = CreatePlaceableBoard();
        EnemyRuntime first = CreateFootprintDefinition(2, 2)
            .CreateRuntime();
        EnemyRuntime second = CreateFootprintDefinition(2, 2)
            .CreateRuntime();

        Assert.That(
            board.TryAddEnemiesToDistinctTiles(new[] { first, second }),
            Is.False);
        Assert.That(board.LivingEnemyCount, Is.Zero);
        Assert.That(board.GetStackCount(0, 0), Is.Zero);
    }

    [Test]
    public void EnemyAbilityContext_PreservesEnemySourceAndStatusStacks()
    {
        EnemyRuntime source = LoadEnemy("Basic").CreateRuntime();
        StatusEffectSO fire =
            AssetDatabase.LoadAssetAtPath<StatusEffectSO>(
                FireStatusPath);
        Assert.That(fire, Is.Not.Null);
        Assert.That(
            ApplyEnemyStatus(source, fire, 3f, 2),
            Is.True);

        BattleEffectContext context =
            BattleEffectContext.ForEnemyAbility(
                source,
                null,
                CharacterTargetFaction.Enemy,
                Array.Empty<EnemyRuntime>(),
                null);
        BattleEffectContext snapshot =
            context.SnapshotSourceStatus(fire);
        BattleEffectContext self = snapshot.RetargetToSource();

        Assert.That(
            context.OriginKind,
            Is.EqualTo(BattleEffectOriginKind.EnemyAbility));
        Assert.That(context.SourceTarget.Enemy, Is.SameAs(source));
        Assert.That(context.Source, Is.Null);
        Assert.That(snapshot.SourceStatusStacks, Is.EqualTo(2));
        Assert.That(
            self.TargetFaction,
            Is.EqualTo(CharacterTargetFaction.Enemy));
        Assert.That(self.EnemyTargets, Is.EqualTo(new[] { source }));
    }

    [Test]
    public void EnemyAbility_SourceTargetModeHealsTheEnemySource()
    {
        EnemyRuntime source = LoadEnemy("Basic").CreateRuntime();
        SetEnemyHealth(source, 10);
        RecordingBattleBoard board = new();
        BattleEffectContext context =
            BattleEffectContext.ForEnemyAbility(
                source,
                board,
                CharacterTargetFaction.Ally,
                null,
                new IBattleCharacter[] { new FakeBattleCharacter(0) });
        CharacterEffectDefinition heal = CreateEffect(
            CharacterEffectType.Heal,
            CharacterEffectTargetMode.Source,
            3f);

        BattleEffectResult result =
            BattleEffectExecutor.ExecuteSequence(
                context,
                new IBattleEffectDefinition[] { heal });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(source.Health, Is.EqualTo(13));
        Assert.That(
            board.LastHealedEnemyTargets,
            Is.EqualTo(new[] { source }));
    }

    [Test]
    public void EnemyAbility_DamageEffectCanDamagePlayerCharacters()
    {
        EnemyRuntime source = LoadEnemy("Basic").CreateRuntime();
        FakeBattleCharacter target = new(0, 20);
        BattleEffectContext context =
            BattleEffectContext.ForEnemyAbility(
                source,
                null,
                CharacterTargetFaction.Ally,
                null,
                new IBattleCharacter[] { target });
        CharacterEffectDefinition damage = CreateEffect(
            CharacterEffectType.Damage,
            CharacterEffectTargetMode.InheritAction,
            7f);

        BattleEffectResult result =
            BattleEffectExecutor.ExecuteSequence(
                context,
                new IBattleEffectDefinition[] { damage });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.DamageDealt, Is.EqualTo(7));
        Assert.That(target.CurrentHealth, Is.EqualTo(13));
    }

    [Test]
    public void EnemyAbility_SpendHealthUsesEnemySourceHealth()
    {
        EnemyRuntime source = LoadEnemy("Basic").CreateRuntime();
        BattleEffectContext context =
            BattleEffectContext.ForEnemyAbility(
                source,
                null,
                CharacterTargetFaction.Enemy,
                Array.Empty<EnemyRuntime>(),
                null);
        CharacterEffectDefinition spendHealth = CreateEffect(
            CharacterEffectType.SpendHealth,
            CharacterEffectTargetMode.InheritAction,
            5f);

        BattleEffectResult result =
            BattleEffectExecutor.ExecuteSequence(
                context,
                new IBattleEffectDefinition[] { spendHealth });

        Assert.That(result.Succeeded, Is.True);
        Assert.That(source.Health, Is.EqualTo(15));
    }

    [Test]
    public void SharedExecutor_MultipleHealthCostsAbortWithoutPartialSpend()
    {
        EnemyRuntime source = LoadEnemy("Basic").CreateRuntime();
        BattleEffectContext context =
            BattleEffectContext.ForEnemyAbility(
                source,
                null,
                CharacterTargetFaction.Enemy,
                Array.Empty<EnemyRuntime>(),
                null);
        CharacterEffectDefinition first = CreateEffect(
            CharacterEffectType.SpendHealth,
            CharacterEffectTargetMode.InheritAction,
            12f);
        CharacterEffectDefinition second = CreateEffect(
            CharacterEffectType.SpendHealth,
            CharacterEffectTargetMode.InheritAction,
            12f);

        BattleEffectResult result =
            BattleEffectExecutor.ExecuteSequence(
                context,
                new IBattleEffectDefinition[] { first, second });

        Assert.That(result.Attempted, Is.False);
        Assert.That(source.Health, Is.EqualTo(20));
    }

    [Test]
    public void ModularCooldownAbility_HealsOrthogonalEnemyAllies()
    {
        CharacterEffectDefinition heal = CreateEffect(
            CharacterEffectType.Heal,
            CharacterEffectTargetMode.InheritAction,
            3f);
        EnemyRuntime source = CreateModularCooldownEnemy(
            EnemyAbilityIds.AdjacentHeal,
            2f,
            EnemyAbilityTargetFaction.EnemyAllies,
            EnemyAbilityTargetSubject.Adjacent,
            EnemyAbilityTargetMetric.Health,
            heal);
        EnemyRuntime up = CreateInjuredBasic();
        EnemyRuntime right = CreateInjuredBasic();
        EnemyRuntime diagonal = CreateInjuredBasic();
        DungeonBoardView board = CreateBoard(
            (1, 1, source),
            (0, 1, up),
            (1, 2, right),
            (0, 0, diagonal));

        board.TickEnemyAbilities(
            2f,
            Array.Empty<IBattleCharacter>());

        Assert.That(up.Health, Is.EqualTo(13));
        Assert.That(right.Health, Is.EqualTo(13));
        Assert.That(diagonal.Health, Is.EqualTo(10));
        Assert.That(source.AbilityCooldownRemaining, Is.EqualTo(2f));
    }

    [Test]
    public void MechanicAbilityAsset_RetriesUntilPlayerTargetExists()
    {
        EnemyRuntime mechanic =
            CreateAbilityAssetClone("Mechanic").CreateRuntime();
        DungeonBoardView board = CreateBoard((1, 1, mechanic));

        board.TickEnemyAbilities(
            10f,
            Array.Empty<IBattleCharacter>());

        Assert.That(mechanic.AbilityCooldownRemaining, Is.Zero);

        FakeBattleCharacter damageDealer = new(8);
        board.TickEnemyAbilities(
            0.1f,
            new[] { damageDealer });

        Assert.That(
            damageDealer.StatusApplicationCount,
            Is.EqualTo(1));
        Assert.That(
            mechanic.AbilityCooldownRemaining,
            Is.EqualTo(10f));
    }

    [Test]
    public void ModularCooldownAbility_TargetsHighestDamagePlayer()
    {
        StatusEffectSO stun =
            AssetDatabase.LoadAssetAtPath<StatusEffectSO>(
                StunStatusPath);
        Assert.That(stun, Is.Not.Null);
        CharacterEffectDefinition applyStun = CreateEffect(
            CharacterEffectType.ApplyStatus,
            CharacterEffectTargetMode.InheritAction,
            1f);
        SetPrivateField(applyStun, "statusEffect", stun);
        SetPrivateField(applyStun, "statusDuration", 5f);
        SetPrivateField(applyStun, "statusStacks", 1f);
        EnemyRuntime source = CreateModularCooldownEnemy(
            EnemyAbilityIds.DisableHighestDamage,
            3f,
            EnemyAbilityTargetFaction.PlayerCharacters,
            EnemyAbilityTargetSubject.HighestValue,
            EnemyAbilityTargetMetric.TotalDamageDealt,
            applyStun);
        DungeonBoardView board = CreateBoard((1, 1, source));
        FakeBattleCharacter low = new(2);
        FakeBattleCharacter highest = new(12);
        FakeBattleCharacter tiedLater = new(12);

        board.TickEnemyAbilities(
            3f,
            new IBattleCharacter[] { low, highest, tiedLater });

        Assert.That(low.StatusApplicationCount, Is.Zero);
        Assert.That(highest.StatusApplicationCount, Is.EqualTo(1));
        Assert.That(tiedLater.StatusApplicationCount, Is.Zero);
        Assert.That(highest.LastAppliedStatus, Is.SameAs(stun));
        Assert.That(highest.LastAppliedDuration, Is.EqualTo(5f));
    }

    [Test]
    public void ModularCooldownAbility_FailedSuccessPolicyRetriesReady()
    {
        CharacterEffectDefinition heal = CreateEffect(
            CharacterEffectType.Heal,
            CharacterEffectTargetMode.InheritAction,
            2f);
        EnemyRuntime source = CreateModularCooldownEnemy(
            "retry_heal",
            2f,
            EnemyAbilityTargetFaction.EnemyAllies,
            EnemyAbilityTargetSubject.Adjacent,
            EnemyAbilityTargetMetric.Health,
            heal);
        EnemyRuntime neighbor = LoadEnemy("Basic").CreateRuntime();
        DungeonBoardView board = CreateBoard(
            (1, 1, source),
            (1, 2, neighbor));

        board.TickEnemyAbilities(
            2f,
            Array.Empty<IBattleCharacter>());

        Assert.That(source.AbilityCooldownRemaining, Is.Zero);

        SetEnemyHealth(neighbor, neighbor.MaxHealth - 1);
        board.TickEnemyAbilities(
            0.1f,
            Array.Empty<IBattleCharacter>());

        Assert.That(neighbor.Health, Is.EqualTo(neighbor.MaxHealth));
        Assert.That(source.AbilityCooldownRemaining, Is.EqualTo(2f));
    }

    [Test]
    public void ModularCooldownAbility_OnAttemptConsumesFiniteCharge()
    {
        CharacterEffectDefinition heal = CreateEffect(
            CharacterEffectType.Heal,
            CharacterEffectTargetMode.InheritAction,
            2f);
        EnemyRuntime source = CreateModularCooldownEnemy(
            "single_attempt_heal",
            2f,
            EnemyAbilityTargetFaction.EnemyAllies,
            EnemyAbilityTargetSubject.Adjacent,
            EnemyAbilityTargetMetric.Health,
            heal,
            1,
            EnemyAbilityCooldownResetPolicy.OnAttempt,
            EnemyAbilityChargeConsumptionPolicy.OnAttempt);
        EnemyRuntime neighbor = LoadEnemy("Basic").CreateRuntime();
        DungeonBoardView board = CreateBoard(
            (1, 1, source),
            (1, 2, neighbor));

        board.TickEnemyAbilities(
            2f,
            Array.Empty<IBattleCharacter>());

        Assert.That(
            GetAbilityRemainingCharges(
                source,
                "single_attempt_heal"),
            Is.Zero);

        SetEnemyHealth(neighbor, neighbor.MaxHealth - 2);
        board.TickEnemyAbilities(
            2f,
            Array.Empty<IBattleCharacter>());

        Assert.That(
            neighbor.Health,
            Is.EqualTo(neighbor.MaxHealth - 2));
    }

    [Test]
    public void ModularOnSpawn_GrantsConfiguredArmorOnce()
    {
        EnemyRuntime source = CreateModularTriggeredEnemy(
            "spawn_armor",
            EnemyAbilityTrigger.OnSpawn,
            EnemyAbilityTargetFaction.None,
            EnemyAbilityTargetSubject.None,
            EnemyAbilityTargetMetric.None,
            EnemyAbilityOperationType.GrantArmor,
            initialCharges: 1,
            amount: 5,
            multiplier: 0f);
        DungeonBoardView board = CreateBoard((1, 1, source));

        InvokeSpawnAbilities(board, 1, 1, source);

        Assert.That(source.Armor, Is.EqualTo(5));
        Assert.That(
            GetAbilityRemainingCharges(source, "spawn_armor"),
            Is.Zero);

        InvokeSpawnAbilities(board, 1, 1, source);

        Assert.That(source.Armor, Is.EqualTo(5));
    }

    [Test]
    public void ModularBeforeSelfDamage_ModifiesMatchingDamageType()
    {
        EnemyRuntime source = CreateModularTriggeredEnemy(
            "guard_two_physical_hits",
            EnemyAbilityTrigger.BeforeSelfDamage,
            EnemyAbilityTargetFaction.None,
            EnemyAbilityTargetSubject.None,
            EnemyAbilityTargetMetric.None,
            EnemyAbilityOperationType.ModifyIncomingDamage,
            initialCharges: 2,
            amount: 0,
            multiplier: 0.1f,
            incomingDamageType: CharacterAttackDamageType.Physical);
        DungeonBoardView board = CreateBoard((1, 1, source));

        Assert.That(board.TryDamageEnemy(source, 10), Is.EqualTo(1));
        Assert.That(
            board.TryDamageCharacterTargets(
                null,
                new[] { source },
                5,
                CharacterAttackDamageType.Fixed,
                false),
            Is.EqualTo(5));
        Assert.That(board.TryDamageEnemy(source, 10), Is.EqualTo(1));
        Assert.That(board.TryDamageEnemy(source, 10), Is.EqualTo(10));

        Assert.That(source.Health, Is.EqualTo(3));
        Assert.That(
            GetAbilityRemainingCharges(
                source,
                "guard_two_physical_hits"),
            Is.Zero);
    }

    [Test]
    public void FixedDamage_BypassesGenericModularIncomingDamageModifier()
    {
        EnemyRuntime source = CreateModularTriggeredEnemy(
            "guard_any_damage_once",
            EnemyAbilityTrigger.BeforeSelfDamage,
            EnemyAbilityTargetFaction.None,
            EnemyAbilityTargetSubject.None,
            EnemyAbilityTargetMetric.None,
            EnemyAbilityOperationType.ModifyIncomingDamage,
            initialCharges: 1,
            amount: 0,
            multiplier: 0.2f);
        DungeonBoardView board = CreateBoard((1, 1, source));

        Assert.That(
            board.TryDamageCharacterTargets(
                null,
                new[] { source },
                5,
                CharacterAttackDamageType.Fixed,
                false),
            Is.EqualTo(5));
        Assert.That(
            GetAbilityRemainingCharges(source, "guard_any_damage_once"),
            Is.EqualTo(1));

        Assert.That(board.TryDamageEnemy(source, 5), Is.EqualTo(1));
        Assert.That(
            GetAbilityRemainingCharges(source, "guard_any_damage_once"),
            Is.Zero);
    }

    [Test]
    public void ModularBeforeAllyDamage_RedirectsWithinConfiguredRange()
    {
        EnemyRuntime target = LoadEnemy("Basic").CreateRuntime();
        EnemyRuntime protector = CreateModularTriggeredEnemy(
            "redirect_once",
            EnemyAbilityTrigger.BeforeAllyDamage,
            EnemyAbilityTargetFaction.None,
            EnemyAbilityTargetSubject.None,
            EnemyAbilityTargetMetric.None,
            EnemyAbilityOperationType.RedirectDamage,
            initialCharges: 1,
            range: 1,
            includeDiagonals: true);
        DungeonBoardView board = CreateBoard(
            (1, 1, target),
            (0, 0, protector));

        Assert.That(board.TryDamageEnemy(target, 5), Is.EqualTo(5));
        Assert.That(target.Health, Is.EqualTo(target.MaxHealth));
        Assert.That(
            protector.Health,
            Is.EqualTo(protector.MaxHealth - 5));

        Assert.That(board.TryDamageEnemy(target, 5), Is.EqualTo(5));
        Assert.That(target.Health, Is.EqualTo(target.MaxHealth - 5));
        Assert.That(
            protector.Health,
            Is.EqualTo(protector.MaxHealth - 5));
    }

    [Test]
    public void FixedDamage_BypassesModularDamageRedirect()
    {
        EnemyRuntime target = LoadEnemy("Basic").CreateRuntime();
        EnemyRuntime protector = CreateModularTriggeredEnemy(
            "redirect_fixed_once",
            EnemyAbilityTrigger.BeforeAllyDamage,
            EnemyAbilityTargetFaction.None,
            EnemyAbilityTargetSubject.None,
            EnemyAbilityTargetMetric.None,
            EnemyAbilityOperationType.RedirectDamage,
            initialCharges: 1,
            range: 1,
            includeDiagonals: true);
        DungeonBoardView board = CreateBoard(
            (1, 1, target),
            (0, 0, protector));

        Assert.That(
            board.TryDamageCharacterTargets(
                null,
                new[] { target },
                5,
                CharacterAttackDamageType.Fixed,
                false),
            Is.EqualTo(5));
        Assert.That(target.Health, Is.EqualTo(target.MaxHealth - 5));
        Assert.That(protector.Health, Is.EqualTo(protector.MaxHealth));
        Assert.That(
            GetAbilityRemainingCharges(
                protector,
                "redirect_fixed_once"),
            Is.EqualTo(1));
    }

    [Test]
    public void ModularOnDeath_ExecutesEffectsAgainstPlayers()
    {
        CharacterEffectDefinition damage = CreateEffect(
            CharacterEffectType.Damage,
            CharacterEffectTargetMode.InheritAction,
            4f);
        EnemyRuntime source = CreateModularTriggeredEnemy(
            "death_burst",
            EnemyAbilityTrigger.OnDeath,
            EnemyAbilityTargetFaction.PlayerCharacters,
            EnemyAbilityTargetSubject.All,
            EnemyAbilityTargetMetric.Health,
            EnemyAbilityOperationType.ExecuteEffects,
            damage);
        DungeonBoardView board = CreateBoard((1, 1, source));
        FakeBattleCharacter player = new(0, 20);
        board.SetBattleCharacters(new[] { player });

        Assert.That(
            board.TryDamageEnemy(source, source.MaxHealth),
            Is.EqualTo(source.MaxHealth));

        Assert.That(source.Health, Is.Zero);
        Assert.That(player.CurrentHealth, Is.EqualTo(16));
    }

    [Test]
    public void CharacterDamage_EnemyDefeatedEventPreservesKiller()
    {
        EnemyRuntime enemy = LoadEnemy("Basic").CreateRuntime();
        DungeonBoardView board = CreateBoard((1, 1, enemy));
        FakeBattleCharacter killer = new(0, 20);
        List<BattleEnemyDefeatedEvent> events = new();
        board.EnemyDefeated += events.Add;

        int damage = board.TryDamageCharacterTargets(
            killer,
            new[] { enemy },
            enemy.MaxHealth,
            CharacterAttackDamageType.Fixed,
            false);

        Assert.That(damage, Is.EqualTo(enemy.MaxHealth));
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Enemy, Is.SameAs(enemy));
        Assert.That(events[0].Killer, Is.SameAs(killer));
    }

    [Test]
    public void StatusDamage_EnemyDefeatedEventPreservesApplyingCharacter()
    {
        EnemyRuntime enemy = LoadEnemy("Basic").CreateRuntime();
        DungeonBoardView board = CreateBoard((1, 1, enemy));
        FakeBattleCharacter killer = new(0, 20);
        StatusEffectSO fire =
            AssetDatabase.LoadAssetAtPath<StatusEffectSO>(FireStatusPath);
        List<BattleEnemyDefeatedEvent> events = new();
        board.EnemyDefeated += events.Add;

        Assert.That(
            board.TryApplyCharacterStatus(
                killer,
                new[] { enemy },
                fire,
                3f,
                enemy.MaxHealth,
                1f,
                false),
            Is.True);
        board.TickStatusEffects(1f);

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Enemy, Is.SameAs(enemy));
        Assert.That(events[0].Killer, Is.SameAs(killer));
    }

    [Test]
    public void LethalStatusTick_ClearsStatusBeforePresentationUnbind()
    {
        EnemyRuntime enemy = LoadEnemy("Basic").CreateRuntime();
        DungeonBoardView board = CreateBoard((1, 1, enemy));
        MethodInfo synchronizePresentation =
            typeof(DungeonBoardView).GetMethod(
                "SynchronizeEnemyPresentationBindings",
                InstanceNonPublic);
        Assert.That(synchronizePresentation, Is.Not.Null);
        synchronizePresentation.Invoke(board, null);
        StatusEffectSO fire =
            AssetDatabase.LoadAssetAtPath<StatusEffectSO>(FireStatusPath);
        int fireRemovalCount = 0;
        board.StatusLifecycle += eventData =>
        {
            if (eventData.Definition == fire &&
                eventData.Trigger ==
                StatusEffectLifecycleTrigger.OnRemove)
            {
                fireRemovalCount++;
            }
        };

        Assert.That(
            board.TryApplyCharacterStatus(
                null,
                new[] { enemy },
                fire,
                3f,
                enemy.MaxHealth,
                1f,
                false),
            Is.True);
        Assert.That(enemy.HasFire, Is.True);

        board.TickStatusEffects(1f);

        Assert.That(enemy.Health, Is.Zero);
        Assert.That(enemy.HasFire, Is.False);
        Assert.That(enemy.GetActiveStatusEffects(), Is.Empty);
        Assert.That(fireRemovalCount, Is.EqualTo(1));
    }

    [Test]
    public void ChainedEnemyDefeats_AreDispatchedInQueueOrder()
    {
        EnemyRuntime firstEnemy = LoadEnemy("Basic").CreateRuntime();
        EnemyRuntime secondEnemy = LoadEnemy("Basic").CreateRuntime();
        DungeonBoardView board = CreateBoard(
            (1, 1, firstEnemy),
            (1, 2, secondEnemy));
        FakeBattleCharacter killer = new(0, 20);
        List<string> sequence = new();
        board.EnemyDefeated += eventData =>
        {
            bool isFirst = ReferenceEquals(eventData.Enemy, firstEnemy);
            sequence.Add(isFirst ? "first-handler:first" : "first-handler:second");
            if (isFirst)
            {
                board.TryDamageCharacterTargets(
                    killer,
                    new[] { secondEnemy },
                    secondEnemy.MaxHealth,
                    CharacterAttackDamageType.Fixed,
                    false);
            }
        };
        board.EnemyDefeated += eventData =>
        {
            sequence.Add(ReferenceEquals(eventData.Enemy, firstEnemy)
                ? "second-handler:first"
                : "second-handler:second");
        };

        board.TryDamageCharacterTargets(
            killer,
            new[] { firstEnemy },
            firstEnemy.MaxHealth,
            CharacterAttackDamageType.Fixed,
            false);

        Assert.That(
            sequence,
            Is.EqualTo(new[]
            {
                "first-handler:first",
                "second-handler:first",
                "first-handler:second",
                "second-handler:second",
            }));
    }

    [Test]
    public void ModularSpawnQueue_ModifiesPendingSpawnInterval()
    {
        EnemyRuntime source = CreateModularTriggeredEnemy(
            "fast_spawn",
            EnemyAbilityTrigger.OnSpawnQueueEvaluation,
            EnemyAbilityTargetFaction.None,
            EnemyAbilityTargetSubject.None,
            EnemyAbilityTargetMetric.None,
            EnemyAbilityOperationType.ModifySpawnInterval,
            multiplier: 0.5f);
        BattleManager manager = CreateBattleManager();
        SetPrivateField(manager, "_spawnInterval", 10f);
        GetPrivateList<EnemyRuntime>(
            manager,
            "_spawnQueue").Add(source);

        Assert.That(manager.SpawnInterval, Is.EqualTo(5f));
        Assert.That(manager.SpawnInterval, Is.EqualTo(5f));
    }

    [Test]
    public void ModularSpawnQueue_ExpandsSuccessfulSpawnGroup()
    {
        EnemyRuntime source = CreateModularTriggeredEnemy(
            "spawn_with_two",
            EnemyAbilityTrigger.OnSpawnQueueEvaluation,
            EnemyAbilityTargetFaction.None,
            EnemyAbilityTargetSubject.None,
            EnemyAbilityTargetMetric.None,
            EnemyAbilityOperationType.ExpandSpawnGroup,
            count: 2);
        EnemyRuntime firstCompanion =
            LoadEnemy("Basic").CreateRuntime();
        EnemyRuntime secondCompanion =
            LoadEnemy("Basic").CreateRuntime();
        EnemyRuntime remaining =
            LoadEnemy("Basic").CreateRuntime();
        BattleManager manager = CreateBattleManager();
        RecordingBattleBoard board = new();
        SetPrivateField(manager, "_board", board);
        List<EnemyRuntime> queue =
            GetPrivateList<EnemyRuntime>(manager, "_spawnQueue");
        queue.Add(source);
        queue.Add(firstCompanion);
        queue.Add(secondCompanion);
        queue.Add(remaining);

        Assert.That(InvokeTrySpawn(manager), Is.True);

        Assert.That(
            board.LastSpawnGroup,
            Is.EqualTo(new[]
            {
                source,
                firstCompanion,
                secondCompanion,
            }));
        Assert.That(manager.PendingEnemyCount, Is.EqualTo(1));
        Assert.That(manager.SpawnQueue[0], Is.SameAs(remaining));
    }

    [Test]
    public void ModularTargetPriority_ExcludesOnlyWithAlternateTarget()
    {
        EnemyRuntime ordinary = LoadEnemy("Basic").CreateRuntime();
        EnemyRuntime excluded = CreateModularTriggeredEnemy(
            "hide_with_alternate",
            EnemyAbilityTrigger.OnTargetPriorityEvaluation,
            EnemyAbilityTargetFaction.None,
            EnemyAbilityTargetSubject.None,
            EnemyAbilityTargetMetric.None,
            EnemyAbilityOperationType.ModifyTargetPriority,
            requireAlternateTarget: true);
        FakeBattleCharacter source = new(1);
        DungeonBoardView mixedBoard = CreateBoard(
            (1, 1, ordinary),
            (1, 2, excluded));

        IReadOnlyList<EnemyRuntime> mixedTargets =
            mixedBoard.SelectCharacterTargets(
                source,
                CharacterAttackSubject.All,
                CharacterAttackSubjectMetric.Health,
                10,
                CharacterConditionMatchMode.All,
                Array.Empty<CharacterNumericCondition>());

        Assert.That(mixedTargets, Is.EqualTo(new[] { ordinary }));

        EnemyRuntime lone = CreateModularTriggeredEnemy(
            "hide_with_alternate",
            EnemyAbilityTrigger.OnTargetPriorityEvaluation,
            EnemyAbilityTargetFaction.None,
            EnemyAbilityTargetSubject.None,
            EnemyAbilityTargetMetric.None,
            EnemyAbilityOperationType.ModifyTargetPriority,
            requireAlternateTarget: true);
        DungeonBoardView loneBoard = CreateBoard((1, 1, lone));

        IReadOnlyList<EnemyRuntime> loneTargets =
            loneBoard.SelectCharacterTargets(
                source,
                CharacterAttackSubject.All,
                CharacterAttackSubjectMetric.Health,
                10,
                CharacterConditionMatchMode.All,
                Array.Empty<CharacterNumericCondition>());

        Assert.That(loneTargets, Is.EqualTo(new[] { lone }));
    }

    [Test]
    public void ModularTargetPriority_AdjustmentPrecedesSubjectMetric()
    {
        EnemyRuntime lowestHealth = LoadEnemy("Basic").CreateRuntime();
        SetEnemyHealth(lowestHealth, 1);
        EnemyRuntime taunting = CreateModularTriggeredEnemy(
            "priority_taunt",
            EnemyAbilityTrigger.OnTargetPriorityEvaluation,
            EnemyAbilityTargetFaction.None,
            EnemyAbilityTargetSubject.None,
            EnemyAbilityTargetMetric.None,
            EnemyAbilityOperationType.ModifyTargetPriority,
            targetPriorityMode: EnemyTargetPriorityMode.Adjust,
            targetPriorityAdjustment: 10);
        FakeBattleCharacter source = new(1);
        DungeonBoardView board = CreateBoard(
            (1, 1, lowestHealth),
            (1, 2, taunting));

        IReadOnlyList<EnemyRuntime> targets =
            board.SelectCharacterTargets(
                source,
                CharacterAttackSubject.LowestValue,
                CharacterAttackSubjectMetric.Health,
                1,
                CharacterConditionMatchMode.All,
                Array.Empty<CharacterNumericCondition>());

        Assert.That(targets, Is.EqualTo(new[] { taunting }));
    }

    [Test]
    public void ModularTargetPriority_ForceFocusPrecedesAdjustments()
    {
        EnemyRuntime adjusted = CreateModularTriggeredEnemy(
            "priority_adjusted",
            EnemyAbilityTrigger.OnTargetPriorityEvaluation,
            EnemyAbilityTargetFaction.None,
            EnemyAbilityTargetSubject.None,
            EnemyAbilityTargetMetric.None,
            EnemyAbilityOperationType.ModifyTargetPriority,
            targetPriorityMode: EnemyTargetPriorityMode.Adjust,
            targetPriorityAdjustment: 100);
        EnemyRuntime forced = CreateModularTriggeredEnemy(
            "priority_forced",
            EnemyAbilityTrigger.OnTargetPriorityEvaluation,
            EnemyAbilityTargetFaction.None,
            EnemyAbilityTargetSubject.None,
            EnemyAbilityTargetMetric.None,
            EnemyAbilityOperationType.ModifyTargetPriority,
            targetPriorityMode: EnemyTargetPriorityMode.ForceFocus);
        FakeBattleCharacter source = new(1);
        DungeonBoardView board = CreateBoard(
            (1, 1, adjusted),
            (1, 2, forced));

        IReadOnlyList<EnemyRuntime> targets =
            board.SelectCharacterTargets(
                source,
                CharacterAttackSubject.LowestValue,
                CharacterAttackSubjectMetric.Health,
                1,
                CharacterConditionMatchMode.All,
                Array.Empty<CharacterNumericCondition>());

        Assert.That(targets, Is.EqualTo(new[] { forced }));
    }

    [Test]
    public void BattleItemFocus_OverridesTargetPriorityExclusion()
    {
        EnemyRuntime ordinary = LoadEnemy("Basic").CreateRuntime();
        EnemyRuntime excluded = CreateModularTriggeredEnemy(
            "priority_excluded",
            EnemyAbilityTrigger.OnTargetPriorityEvaluation,
            EnemyAbilityTargetFaction.None,
            EnemyAbilityTargetSubject.None,
            EnemyAbilityTargetMetric.None,
            EnemyAbilityOperationType.ModifyTargetPriority,
            requireAlternateTarget: true);
        FakeBattleCharacter source = new(1);
        DungeonBoardView board = CreateBoard(
            (1, 1, ordinary),
            (1, 2, excluded));

        Assert.That(
            board.TryForcePriorityTarget(excluded, 5f),
            Is.True);
        IReadOnlyList<EnemyRuntime> targets =
            board.SelectCharacterTargets(
                source,
                CharacterAttackSubject.Random,
                CharacterAttackSubjectMetric.Health,
                1,
                CharacterConditionMatchMode.All,
                Array.Empty<CharacterNumericCondition>());

        Assert.That(targets, Is.EqualTo(new[] { excluded }));
    }

    [Test]
    public void AbilityAppliedPriorityStatus_ForcesCharacterFocus()
    {
        StatusEffectSO priorityStatus =
            CreateTargetPriorityStatus(
                adjustment: 20f,
                forceTargeting: false,
                canTargetEnemy: true,
                canTargetAlly: false);
        EnemyRuntime lowestHealth = LoadEnemy("Basic").CreateRuntime();
        SetEnemyHealth(lowestHealth, 1);
        EnemyRuntime focused = LoadEnemy("Basic").CreateRuntime();
        FakeBattleCharacter source = new(1);
        DungeonBoardView board = CreateBoard(
            (1, 1, lowestHealth),
            (1, 2, focused));
        CharacterEffectDefinition applyPriority = CreateEffect(
            CharacterEffectType.ApplyStatus,
            CharacterEffectTargetMode.InheritAction,
            1f);
        SetPrivateField(
            applyPriority,
            "statusEffect",
            priorityStatus);
        SetPrivateField(applyPriority, "statusDuration", 5f);
        SetPrivateField(applyPriority, "statusStacks", 1f);
        BattleEffectContext context =
            BattleEffectContext.ForEnemyAbility(
                focused,
                board,
                CharacterTargetFaction.Enemy,
                new[] { focused },
                null);

        BattleEffectResult applied =
            BattleEffectExecutor.ExecuteSequence(
                context,
                new IBattleEffectDefinition[] { applyPriority });
        IReadOnlyList<EnemyRuntime> targets =
            board.SelectCharacterTargets(
                source,
                CharacterAttackSubject.LowestValue,
                CharacterAttackSubjectMetric.Health,
                1,
                CharacterConditionMatchMode.All,
                Array.Empty<CharacterNumericCondition>());

        Assert.That(applied.Succeeded, Is.True);
        Assert.That(targets, Is.EqualTo(new[] { focused }));
    }

    [Test]
    public void TargetPriorityStatus_TauntsEnemyAbilityFromLowerHealthAlly()
    {
        StatusEffectSO tauntStatus =
            CreateTargetPriorityStatus(
                adjustment: 10f,
                forceTargeting: false,
                canTargetEnemy: false,
                canTargetAlly: true);
        CharacterEffectDefinition damage = CreateEffect(
            CharacterEffectType.Damage,
            CharacterEffectTargetMode.InheritAction,
            7f);
        EnemyRuntime source = CreateModularCooldownEnemy(
            "target_priority_taunt_test",
            2f,
            EnemyAbilityTargetFaction.PlayerCharacters,
            EnemyAbilityTargetSubject.LowestValue,
            EnemyAbilityTargetMetric.Health,
            damage);
        FakeBattleCharacter lowestHealth = new(0, 20);
        lowestHealth.TakeDamage(10);
        FakeBattleCharacter taunting = new(0, 20);
        taunting.AddActiveStatus(tauntStatus, 1);
        DungeonBoardView board = CreateBoard((1, 1, source));

        board.TickEnemyAbilities(
            2f,
            new IBattleCharacter[] { lowestHealth, taunting });

        Assert.That(lowestHealth.CurrentHealth, Is.EqualTo(10));
        Assert.That(taunting.CurrentHealth, Is.EqualTo(13));
    }

    private EnemyRuntime CreateInjuredBasic()
    {
        EnemyRuntime enemy = LoadEnemy("Basic").CreateRuntime();
        SetEnemyHealth(enemy, 10);
        return enemy;
    }

    private EnemyRuntime CreateModularCooldownEnemy(
        string abilityId,
        float cooldown,
        EnemyAbilityTargetFaction targetFaction,
        EnemyAbilityTargetSubject targetSubject,
        EnemyAbilityTargetMetric targetMetric,
        CharacterEffectDefinition effect,
        int initialCharges = 0,
        EnemyAbilityCooldownResetPolicy cooldownResetPolicy =
            EnemyAbilityCooldownResetPolicy.OnSuccessfulActivation,
        EnemyAbilityChargeConsumptionPolicy chargeConsumptionPolicy =
            EnemyAbilityChargeConsumptionPolicy.OnSuccessfulActivation)
    {
        EnemySO definition = ScriptableObject.CreateInstance<EnemySO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(definition);
        SetPrivateField(
            definition,
            "enemyId",
            Guid.NewGuid().ToString("N"));

        EnemyAbilityTargetDefinition target = new();
        SetPrivateField(target, "faction", targetFaction);
        SetPrivateField(target, "subject", targetSubject);
        SetPrivateField(target, "metric", targetMetric);
        SetPrivateField(target, "targetCount", 1);
        SetPrivateField(target, "range", 1);
        SetPrivateField(target, "includeDiagonals", false);

        EnemyAbilityOperationDefinition operation = new();
        SetPrivateField(
            operation,
            "type",
            EnemyAbilityOperationType.ExecuteEffects);
        SetPrivateField(
            operation,
            "effects",
            new List<CharacterEffectDefinition> { effect });
        SetPrivateField(operation, "enabled", true);

        EnemyAbilityDefinition ability = new();
        SetPrivateField(ability, "abilityId", abilityId);
        SetPrivateField(
            ability,
            "trigger",
            EnemyAbilityTrigger.OnCooldown);
        SetPrivateField(ability, "cooldown", cooldown);
        SetPrivateField(
            ability,
            "cooldownResetPolicy",
            cooldownResetPolicy);
        SetPrivateField(ability, "initialCharges", initialCharges);
        SetPrivateField(
            ability,
            "chargeConsumptionPolicy",
            chargeConsumptionPolicy);
        SetPrivateField(ability, "target", target);
        SetPrivateField(
            ability,
            "operations",
            new List<EnemyAbilityOperationDefinition> { operation });
        SetPrivateField(
            definition,
            "abilities",
            new List<EnemyAbilityDefinition> { ability });
        return definition.CreateRuntime();
    }

    private EnemyRuntime CreateModularTriggeredEnemy(
        string abilityId,
        EnemyAbilityTrigger trigger,
        EnemyAbilityTargetFaction targetFaction,
        EnemyAbilityTargetSubject targetSubject,
        EnemyAbilityTargetMetric targetMetric,
        EnemyAbilityOperationType operationType,
        CharacterEffectDefinition effect = null,
        int initialCharges = 0,
        int amount = 1,
        float multiplier = 0f,
        int count = 1,
        int range = 1,
        bool includeDiagonals = false,
        CharacterAttackDamageType? incomingDamageType = null,
        bool requireAlternateTarget = false,
        EnemyTargetPriorityMode targetPriorityMode =
            EnemyTargetPriorityMode.Exclude,
        int targetPriorityAdjustment = 0)
    {
        EnemySO definition = ScriptableObject.CreateInstance<EnemySO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(definition);
        SetPrivateField(
            definition,
            "enemyId",
            Guid.NewGuid().ToString("N"));

        EnemyAbilityTargetDefinition target = new();
        SetPrivateField(target, "faction", targetFaction);
        SetPrivateField(target, "subject", targetSubject);
        SetPrivateField(target, "metric", targetMetric);
        SetPrivateField(target, "targetCount", 1);
        SetPrivateField(target, "range", range);
        SetPrivateField(target, "includeDiagonals", includeDiagonals);

        EnemyAbilityOperationDefinition operation = new();
        SetPrivateField(operation, "type", operationType);
        SetPrivateField(operation, "amount", amount);
        SetPrivateField(operation, "multiplier", multiplier);
        SetPrivateField(operation, "count", count);
        SetPrivateField(operation, "range", range);
        SetPrivateField(
            operation,
            "includeDiagonals",
            includeDiagonals);
        SetPrivateField(operation, "enabled", true);
        SetPrivateField(
            operation,
            "targetPriorityMode",
            targetPriorityMode);
        SetPrivateField(
            operation,
            "targetPriorityAdjustment",
            targetPriorityAdjustment);
        if (effect != null)
        {
            SetPrivateField(
                operation,
                "effects",
                new List<CharacterEffectDefinition> { effect });
        }

        List<EnemyAbilityConditionDefinition> conditions = new();
        if (incomingDamageType.HasValue)
        {
            EnemyAbilityConditionDefinition condition = new();
            SetPrivateField(
                condition,
                "type",
                EnemyAbilityConditionType.IncomingDamageType);
            SetPrivateField(
                condition,
                "incomingDamageType",
                incomingDamageType.Value);
            SetPrivateField(condition, "expected", true);
            conditions.Add(condition);
        }
        if (requireAlternateTarget)
        {
            EnemyAbilityConditionDefinition condition = new();
            SetPrivateField(
                condition,
                "type",
                EnemyAbilityConditionType.HasAlternateTarget);
            SetPrivateField(condition, "expected", true);
            conditions.Add(condition);
        }

        EnemyAbilityDefinition ability = new();
        SetPrivateField(ability, "abilityId", abilityId);
        SetPrivateField(ability, "trigger", trigger);
        SetPrivateField(ability, "initialCharges", initialCharges);
        SetPrivateField(ability, "conditions", conditions);
        SetPrivateField(ability, "target", target);
        SetPrivateField(
            ability,
            "operations",
            new List<EnemyAbilityOperationDefinition> { operation });
        SetPrivateField(
            definition,
            "abilities",
            new List<EnemyAbilityDefinition> { ability });
        return definition.CreateRuntime();
    }

    private StatusEffectSO CreateTargetPriorityStatus(
        float adjustment,
        bool forceTargeting,
        bool canTargetEnemy,
        bool canTargetAlly)
    {
        StatusEffectSO status =
            ScriptableObject.CreateInstance<StatusEffectSO>();
        status.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(status);
        status.RegenerateStatusId();
        SetPrivateField(status, "canTargetEnemy", canTargetEnemy);
        SetPrivateField(status, "canTargetAlly", canTargetAlly);
        SetPrivateField(
            status,
            "durationMode",
            StatusEffectDurationMode.Timed);
        SetPrivateField(status, "defaultDuration", 5f);

        List<StatusEffectStatModifierDefinition> modifiers = new();
        if (!Mathf.Approximately(adjustment, 0f))
        {
            StatusEffectStatModifierDefinition modifier = new();
            SetPrivateField(
                modifier,
                "statType",
                StatusEffectStatType.TargetPriority);
            SetPrivateField(
                modifier,
                "mode",
                StatusEffectStatModifierMode.Flat);
            SetPrivateField(modifier, "value", adjustment);
            SetPrivateField(modifier, "scaleWithStacks", true);
            modifiers.Add(modifier);
        }
        SetPrivateField(status, "statModifiers", modifiers);

        List<StatusEffectControlDefinition> controls = new();
        if (forceTargeting)
        {
            StatusEffectControlDefinition control = new();
            SetPrivateField(
                control,
                "controlType",
                StatusEffectControlType.ForceTargeting);
            controls.Add(control);
        }
        SetPrivateField(status, "controlEffects", controls);
        status.ValidateDefinition();
        return status;
    }

    private StatusEffectSO CreateIncomingDamageStatus(float ratio)
    {
        StatusEffectSO status =
            ScriptableObject.CreateInstance<StatusEffectSO>();
        status.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(status);
        status.RegenerateStatusId();
        SetPrivateField(
            status,
            "durationMode",
            StatusEffectDurationMode.Timed);
        SetPrivateField(status, "defaultDuration", 10f);

        StatusEffectStatModifierDefinition modifier = new();
        SetPrivateField(
            modifier,
            "statType",
            StatusEffectStatType.IncomingDamage);
        SetPrivateField(
            modifier,
            "mode",
            StatusEffectStatModifierMode.AdditiveRatio);
        SetPrivateField(modifier, "value", ratio);
        SetPrivateField(modifier, "scaleWithStacks", false);
        SetPrivateField(
            status,
            "statModifiers",
            new List<StatusEffectStatModifierDefinition> { modifier });
        status.ValidateDefinition();
        return status;
    }

    private EnemySO LoadEnemy(string assetName)
    {
        string fileName = assetName switch
        {
            "Basic" => "G001_BasicRemnant.asset",
            "Assault" => "G002_AssaultRemnant.asset",
            "Heavy" => "G003_HeavyRemnant.asset",
            "Medic" => "S001_MedicRemnant.asset",
            "Mechanic" => "S002_MechanicRemnant.asset",
            "Infiltrator" => "S003_InfiltratorRemnant.asset",
            "Pointman" => "S004_PointmanRemnant.asset",
            "ShieldBearer" => "S005_ShieldBearerRemnant.asset",
            _ => assetName + ".asset",
        };
        string path = EnemyAssetFolder + fileName;
        EnemySO definition = AssetDatabase.LoadAssetAtPath<EnemySO>(path);
        Assert.That(
            definition,
            Is.Not.Null,
            $"Missing EnemySO test asset: {path}");
        return definition;
    }

    private EnemySO CreateAbilityAssetClone(string assetName)
    {
        EnemySO definition =
            UnityEngine.Object.Instantiate(LoadEnemy(assetName));
        definition.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(definition);
        return definition;
    }

    private EnemySO CreateRuntimeDefault(EEnemyType enemyType)
    {
        MethodInfo method = typeof(EnemySO).GetMethod(
            "CreateRuntimeDefault",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        EnemySO definition = (EnemySO)method.Invoke(
            null,
            new object[] { enemyType, 20 });
        Assert.That(definition, Is.Not.Null);
        _createdObjects.Add(definition);
        return definition;
    }

    private BattleManager CreateBattleManager()
    {
        GameObject gameObject = new("Test_BattleManager");
        _createdObjects.Add(gameObject);
        return gameObject.AddComponent<BattleManager>();
    }

    private DungeonBoardView CreateBoard(
        params (int Row, int Column, EnemyRuntime Enemy)[] occupants)
    {
        GameObject boardObject = new(
            "Test_DungeonBoard",
            typeof(RectTransform));
        _createdObjects.Add(boardObject);
        DungeonBoardView board =
            boardObject.AddComponent<DungeonBoardView>();
        SetPrivateProperty(board, "GridSize", 3);

        Dictionary<(int Row, int Column), EnemyRuntime> occupantMap = new();
        foreach ((int row, int column, EnemyRuntime enemy) in occupants)
            occupantMap[(row, column)] = enemy;

        List<DungeonBoardSlot> tiles =
            GetPrivateList<DungeonBoardSlot>(board, "_tiles");
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                DungeonBoardSlot tile = new();
                tile.Initialize(row, column, 8);

                if (occupantMap.TryGetValue(
                        (row, column),
                        out EnemyRuntime enemy))
                {
                    GetPrivateList<EnemyRuntime>(
                        tile,
                        "_enemies").Add(enemy);
                }

                tiles.Add(tile);
            }
        }

        return board;
    }

    private DungeonBoardView CreatePlaceableBoard()
    {
        return CreateBoard();
    }

    private EnemySO CreateFootprintDefinition(int width, int height)
    {
        EnemySO definition = UnityEngine.Object.Instantiate(
            LoadEnemy("Basic"));
        definition.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(definition);
        SerializedObject serialized = new(definition);
        serialized.FindProperty("enemyId").stringValue =
            $"footprint_{width}x{height}_{Guid.NewGuid():N}";
        serialized.FindProperty("footprintWidth").intValue = width;
        serialized.FindProperty("footprintHeight").intValue = height;
        serialized.FindProperty("stackingPolicy").enumValueIndex =
            (int)EnemyStackingPolicy.Exclusive;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static int TakeDamage(
        EnemyRuntime enemy,
        int damage,
        CharacterAttackDamageType damageType =
            CharacterAttackDamageType.Physical)
    {
        MethodInfo method = typeof(EnemyRuntime).GetMethod(
            "TakeDamage",
            InstanceNonPublic,
            null,
            new[] { typeof(int), typeof(CharacterAttackDamageType) },
            null);
        Assert.That(method, Is.Not.Null);
        return (int)method.Invoke(enemy, new object[] { damage, damageType });
    }

    private static void SetEnemyHealth(EnemyRuntime enemy, int health)
    {
        MethodInfo method = typeof(EnemyRuntime).GetMethod(
            "SetHealth",
            InstanceNonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(enemy, new object[] { health });
    }

    private static bool InvokeTrySpawn(BattleManager manager)
    {
        MethodInfo method = typeof(BattleManager).GetMethod(
            "TrySpawnNextQueuedEnemy",
            InstanceNonPublic);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(manager, null);
    }

    private static void InvokeTickSpawnQueue(
        BattleManager manager,
        float deltaTime)
    {
        MethodInfo method = typeof(BattleManager).GetMethod(
            "TickEnemySpawnQueue",
            InstanceNonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(manager, new object[] { deltaTime });
    }

    private static void WireBoardOccupancyEvent(
        BattleManager manager,
        RecordingBattleBoard board)
    {
        MethodInfo method = typeof(BattleManager).GetMethod(
            "HandleBoardOccupancyChanged",
            InstanceNonPublic);
        Assert.That(method, Is.Not.Null);
        Action handler = (Action)Delegate.CreateDelegate(
            typeof(Action),
            manager,
            method);
        board.OccupancyChanged += handler;
    }

    private static bool ApplyEnemyStatus(
        EnemyRuntime enemy,
        StatusEffectSO statusEffect,
        float duration,
        int stacks)
    {
        MethodInfo method = typeof(EnemyRuntime).GetMethod(
            "ApplyStatusEffect",
            InstanceNonPublic,
            null,
            new[]
            {
                typeof(StatusEffectSO),
                typeof(float),
                typeof(int),
                typeof(IBattleCharacter),
                typeof(float),
            },
            null);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(
            enemy,
            new object[]
            {
                statusEffect,
                duration,
                stacks,
                null,
                statusEffect.TickInterval,
            });
    }

    private static int GetAbilityRemainingCharges(
        EnemyRuntime enemy,
        string abilityId)
    {
        MethodInfo method = typeof(EnemyRuntime).GetMethod(
            "GetAbilityRemainingCharges",
            InstanceNonPublic);
        Assert.That(method, Is.Not.Null);
        return (int)method.Invoke(enemy, new object[] { abilityId });
    }

    private static void InvokeSpawnAbilities(
        DungeonBoardView board,
        int row,
        int column,
        EnemyRuntime source)
    {
        List<DungeonBoardSlot> tiles =
            GetPrivateList<DungeonBoardSlot>(board, "_tiles");
        DungeonBoardSlot tile = tiles[row * board.GridSize + column];
        MethodInfo method = typeof(DungeonBoardView).GetMethod(
            "ExecuteSpawnAbilities",
            InstanceNonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(board, new object[] { tile, source });
    }

    private static CharacterEffectDefinition CreateEffect(
        CharacterEffectType type,
        CharacterEffectTargetMode targetMode,
        float amount)
    {
        CharacterEffectDefinition effect = new();
        SetPrivateField(effect, "type", type);
        SetPrivateField(effect, "targetMode", targetMode);
        SetPrivateField(
            effect,
            "damageAmountMode",
            CharacterDamageAmountMode.Fixed);
        SetPrivateField(effect, "damageAmount", amount);
        return effect;
    }

    private StatusEffectSO CreateStatusForConditionScope(
        string statusId,
        StatusEffectAlignment alignment)
    {
        StatusEffectSO status =
            ScriptableObject.CreateInstance<StatusEffectSO>();
        status.hideFlags = HideFlags.HideAndDontSave;
        status.name = statusId;
        _createdObjects.Add(status);
        SetPrivateField(status, "statusId", statusId);
        SetPrivateField(status, "alignment", alignment);
        return status;
    }

    private static bool EvaluateEnemyStatusCondition(
        EnemyAbilityConditionDefinition condition,
        Func<StatusEffectSO, bool> hasStatus,
        IReadOnlyList<BattleStatusSnapshot> activeStatuses)
    {
        Type evaluatorType = typeof(EnemyAbilityConditionDefinition)
            .Assembly
            .GetType("EnemyAbilityConditionEvaluator");
        Assert.That(evaluatorType, Is.Not.Null);
        MethodInfo method = evaluatorType.GetMethod(
            "MatchesStatusSelection",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(
            null,
            new object[] { condition, hasStatus, activeStatuses });
    }

    private static void SetPrivateProperty(
        object target,
        string propertyName,
        object value)
    {
        PropertyInfo property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);
        Assert.That(
            property,
            Is.Not.Null,
            $"Missing property '{propertyName}'.");
        property.SetValue(target, value);
    }

    private sealed class RecordingBattleBoard : IBattleBoard
    {
        public bool AllowSpawn { get; set; } = true;
        public Func<EnemyRuntime, bool> SpawnEvaluator { get; set; }
        public int AddGroupCallCount { get; private set; }
        public IReadOnlyList<EnemyRuntime> LastSpawnGroup
            { get; private set; } = Array.Empty<EnemyRuntime>();
        public IReadOnlyList<EnemyRuntime> LastHealedEnemyTargets
            { get; private set; } = Array.Empty<EnemyRuntime>();

        public int InitialEnemyCapacity => 9;
        public int LivingEnemyCount => 0;
        public bool HasEmptyEnemyTile => true;

        public event Action OccupancyChanged;

        public event Action<BattleEnemyDefeatedEvent> EnemyDefeated
        {
            add { }
            remove { }
        }

        public event Action<BattleStatusAppliedEvent> StatusApplied
        {
            add { }
            remove { }
        }

        public void RaiseOccupancyChanged()
        {
            OccupancyChanged?.Invoke();
        }

        public bool TryAddEnemy(EnemyRuntime enemy)
        {
            LastSpawnGroup = enemy != null
                ? new[] { enemy }
                : Array.Empty<EnemyRuntime>();
            return AllowSpawn && enemy != null &&
                   (SpawnEvaluator?.Invoke(enemy) ?? true);
        }

        public bool TryAddEnemiesToDistinctTiles(
            IReadOnlyList<EnemyRuntime> enemies)
        {
            AddGroupCallCount++;
            LastSpawnGroup = enemies != null
                ? new List<EnemyRuntime>(enemies)
                : Array.Empty<EnemyRuntime>();
            return AllowSpawn;
        }

        public void ClearAllEnemies()
        {
        }

        public void TickStatusEffects(float deltaTime)
        {
        }

        public void TickEnemyAbilities(
            float deltaTime,
            IReadOnlyList<IBattleCharacter> characters)
        {
        }

        public void SetBattleCharacters(
            IReadOnlyList<IBattleCharacter> characters)
        {
        }

        public void NotifyStatusApplied(BattleStatusAppliedEvent eventData)
        {
        }

        public IReadOnlyList<EnemyRuntime> SelectCharacterTargets(
            IBattleCharacter source,
            CharacterAttackSubject subject,
            CharacterAttackSubjectMetric metric,
            int targetCount,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions)
        {
            return Array.Empty<EnemyRuntime>();
        }

        public IReadOnlyList<IBattleCharacter> SelectAlliedCharacters(
            IBattleCharacter source,
            CharacterAttackSubject subject,
            CharacterAttackSubjectMetric metric,
            int targetCount,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions)
        {
            return Array.Empty<IBattleCharacter>();
        }

        public IReadOnlyList<EnemyRuntime> FilterCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions)
        {
            return targets ?? Array.Empty<EnemyRuntime>();
        }

        public IReadOnlyList<IBattleCharacter> FilterAlliedCharacters(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions)
        {
            return targets ?? Array.Empty<IBattleCharacter>();
        }

        public IReadOnlyList<EnemyRuntime> ExpandCharacterAreaTargets(
            IReadOnlyList<EnemyRuntime> centerTargets,
            IReadOnlyList<CharacterTargetAreaOffset> areaOffsets)
        {
            return centerTargets ?? Array.Empty<EnemyRuntime>();
        }

        public int TryDamageCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            int damage,
            CharacterAttackDamageType damageType,
            bool showAttackRange)
        {
            return 0;
        }

        public int TryHealCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            int amount,
            bool showAttackRange)
        {
            LastHealedEnemyTargets = targets != null
                ? new List<EnemyRuntime>(targets)
                : Array.Empty<EnemyRuntime>();
            int healed = 0;
            if (targets == null)
                return healed;
            foreach (EnemyRuntime target in targets)
            {
                if (target != null)
                    healed += target.Heal(amount);
            }
            return healed;
        }

        public int TryHealAlliedCharacters(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            int amount)
        {
            return 0;
        }

        public int TryGrantShieldToCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            int amount,
            bool showAttackRange)
        {
            return 0;
        }

        public int TryGrantShieldToAlliedCharacters(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            int amount)
        {
            return 0;
        }

        public bool TryApplyCharacterStatus(
            BattleAbilityUser user,
            IReadOnlyList<EnemyRuntime> targets,
            StatusEffectSO statusEffect,
            float duration,
            float stacks,
            float tickInterval,
            bool showAttackRange)
        {
            return false;
        }

        public bool TryApplyAlliedCharacterStatus(
            BattleAbilityUser user,
            IReadOnlyList<IBattleCharacter> targets,
            StatusEffectSO statusEffect,
            float duration,
            float stacks)
        {
            return false;
        }

        public bool TryRemoveCharacterStatus(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            CharacterStatusRemovalSelection removalSelection,
            CharacterStatusRemovalAmount removalAmount,
            bool showAttackRange)
        {
            return false;
        }

        public bool TryRemoveAlliedCharacterStatus(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            CharacterStatusRemovalSelection removalSelection,
            CharacterStatusRemovalAmount removalAmount)
        {
            return false;
        }
    }

    private sealed class FakeBattleCharacter : IBattleCharacter
    {
        private int _currentHealth;
        private readonly List<BattleStatusSnapshot> _activeStatuses = new();

        public int PartySlotIndex => 0;
        public int TotalDamageDealt { get; }
        public int CurrentHealth => _currentHealth;
        public int MaximumHealth { get; }
        public int CurrentShield => 0;
        public float DisabledTimeRemaining => 0f;
        public float CurrentAttackPower => 1f;
        public float CurrentAttackSpeed => 1f;
        public int StatusApplicationCount { get; private set; }
        public StatusEffectSO LastAppliedStatus { get; private set; }
        public float LastAppliedDuration { get; private set; }
        public int LastAppliedStacks { get; private set; }

        public event Action<BattleStatusChangedEvent> StatusChanged
        {
            add { }
            remove { }
        }

        public FakeBattleCharacter(
            int totalDamageDealt,
            int maximumHealth = 100)
        {
            TotalDamageDealt = totalDamageDealt;
            MaximumHealth = Mathf.Max(1, maximumHealth);
            _currentHealth = MaximumHealth;
        }

        public bool HasStatusEffect(StatusEffectSO statusEffect)
        {
            foreach (BattleStatusSnapshot status in _activeStatuses)
            {
                if (ReferenceEquals(status.Definition, statusEffect))
                    return true;
            }

            return false;
        }

        public int GetStatusStackCount(StatusEffectSO statusEffect)
        {
            foreach (BattleStatusSnapshot status in _activeStatuses)
            {
                if (ReferenceEquals(status.Definition, statusEffect))
                    return status.StackCount;
            }

            return 0;
        }

        public IReadOnlyList<BattleStatusSnapshot> GetActiveStatusEffects()
        {
            return _activeStatuses;
        }

        public void AddActiveStatus(
            StatusEffectSO statusEffect,
            int stacks)
        {
            _activeStatuses.Add(
                new BattleStatusSnapshot(
                    statusEffect,
                    stacks,
                    5f));
        }

        public bool TryConsumeStatusStacks(
            StatusEffectSO statusEffect,
            int stackCount)
        {
            return false;
        }

        public int Heal(int amount)
        {
            int previous = _currentHealth;
            _currentHealth = Mathf.Min(
                MaximumHealth,
                _currentHealth + Mathf.Max(0, amount));
            return _currentHealth - previous;
        }

        public int GainShield(int amount)
        {
            return 0;
        }

        public int TakeDamage(int amount)
        {
            int damage = Mathf.Min(
                _currentHealth,
                Mathf.Max(0, amount));
            _currentHealth -= damage;
            return damage;
        }

        public bool CanSpendHealth(int amount)
        {
            return amount > 0 && _currentHealth - amount >= 1;
        }

        public bool TrySpendHealth(int amount)
        {
            if (!CanSpendHealth(amount))
                return false;
            _currentHealth -= amount;
            return true;
        }

        public bool Initialize()
        {
            return true;
        }

        public void BindBattle(
            IActiveSkillResource activeSkillResource,
            IBattleBoard board)
        {
        }

        public void ResetRuntime()
        {
        }

        public void TickBattle(float deltaTime, IBattleBoard board)
        {
        }

        public void RecordDamageDealt(int damage)
        {
        }

        public void DisableFor(float duration)
        {
        }

        public bool ApplyStatusEffect(
            StatusEffectSO definition,
            float duration,
            int stacks)
        {
            StatusApplicationCount++;
            LastAppliedStatus = definition;
            LastAppliedDuration = duration;
            LastAppliedStacks = stacks;
            return definition != null && duration > 0f && stacks > 0;
        }

        public bool ApplyStatusEffect(
            StatusEffectSO definition,
            float duration,
            int stacks,
            IBattleCharacter source)
        {
            return ApplyStatusEffect(definition, duration, stacks);
        }

        public int RemoveStatusEffects(
            CharacterStatusRemovalSelection removalSelection,
            CharacterStatusRemovalAmount removalAmount)
        {
            return 0;
        }
    }
}

public sealed class DungeonDefinitionRegressionTests
{
    private const string FreeBattlePath =
        "Assets/06_Runtime/Resources/Dungeons/FreeBattle.asset";
    private const string TutorialFieldPath =
        "Assets/06_Runtime/Resources/Dungeons/TutorialField.asset";

    [Test]
    public void FreeBattle_DoesNotUseTutorialBattleSetup()
    {
        DungeonDefinition definition =
            AssetDatabase.LoadAssetAtPath<DungeonDefinition>(
                FreeBattlePath);

        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.HasTutorial, Is.False);
        Assert.That(definition.UseIntroBattleBalance, Is.False);
        Assert.That(definition.UsesTutorialBattleSetup, Is.False);
        Assert.That(definition.TryValidate(out string error), Is.True, error);
    }

    [Test]
    public void TutorialField_UsesTutorialBattleSetup()
    {
        DungeonDefinition definition =
            AssetDatabase.LoadAssetAtPath<DungeonDefinition>(
                TutorialFieldPath);

        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.HasTutorial, Is.True);
        Assert.That(definition.UseIntroBattleBalance, Is.True);
        Assert.That(definition.UsesTutorialBattleSetup, Is.True);
        Assert.That(definition.TryValidate(out string error), Is.True, error);
    }

    [Test]
    public void FreeBattle_RotatesEventRestAndShopBetweenBattles()
    {
        DungeonDefinition definition =
            AssetDatabase.LoadAssetAtPath<DungeonDefinition>(
                FreeBattlePath);

        Assert.That(definition, Is.Not.Null);
        IReadOnlyList<EDungeonPhase> phases =
            definition.BuildPhaseSequence(5, 260714);

        Assert.That(phases.Count, Is.EqualTo(9));
        Assert.That(phases[0], Is.EqualTo(EDungeonPhase.Battle));
        Assert.That(phases[1], Is.EqualTo(EDungeonPhase.Event));
        Assert.That(phases[3], Is.EqualTo(EDungeonPhase.Rest));
        Assert.That(phases[5], Is.EqualTo(EDungeonPhase.Shop));
        Assert.That(phases[8], Is.EqualTo(EDungeonPhase.Battle));
    }

    [Test]
    public void DungeonRunSession_TracksRunCurrencyWithoutGoingNegative()
    {
        DungeonDefinition definition =
            ScriptableObject.CreateInstance<DungeonDefinition>();
        DungeonRunSession session = new();
        try
        {
            session.Begin(
                definition,
                260714,
                1,
                new[] { EDungeonPhase.Battle },
                100);

            Assert.That(session.RunCurrency, Is.EqualTo(100));
            Assert.That(session.TrySpendRunCurrency(40), Is.True);
            Assert.That(session.RunCurrency, Is.EqualTo(60));
            Assert.That(session.TrySpendRunCurrency(61), Is.False);
            session.AddRunCurrency(-1000);
            Assert.That(session.RunCurrency, Is.Zero);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void DungeonCostRecoveryInterval_DefaultsToTenSecondsAndIsEditable()
    {
        DungeonDefinition definition =
            ScriptableObject.CreateInstance<DungeonDefinition>();
        try
        {
            Assert.That(
                definition.ActiveSkillCostRecoveryDuration,
                Is.EqualTo(10f).Within(0.001f));

            SerializedObject serialized = new(definition);
            serialized.FindProperty("activeSkillCostRecoveryDuration")
                .floatValue = 7.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                definition.ActiveSkillCostRecoveryDuration,
                Is.EqualTo(7.5f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void DungeonBattleHealthCost_DefaultsToDifficultyScaleDividedByTen()
    {
        DungeonDefinition definition =
            ScriptableObject.CreateInstance<DungeonDefinition>();
        try
        {
            Assert.That(
                definition.HasClearedBattleHealthCostOverride,
                Is.False);
            Assert.That(
                definition.ResolveClearedBattleHealthCost(0),
                Is.Zero);
            Assert.That(
                definition.ResolveClearedBattleHealthCost(9),
                Is.Zero);
            Assert.That(
                definition.ResolveClearedBattleHealthCost(10),
                Is.EqualTo(1));
            Assert.That(
                definition.ResolveClearedBattleHealthCost(55),
                Is.EqualTo(5));
            Assert.That(
                definition.ResolveClearedBattleHealthCost(100),
                Is.EqualTo(10));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void DungeonBattleHealthCost_AuthoredOverrideTakesPriority()
    {
        DungeonDefinition definition =
            ScriptableObject.CreateInstance<DungeonDefinition>();
        try
        {
            SerializedObject serialized = new(definition);
            serialized.FindProperty("clearedBattleHealthCost").intValue = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                definition.HasClearedBattleHealthCostOverride,
                Is.True);
            Assert.That(
                definition.ResolveClearedBattleHealthCost(100),
                Is.EqualTo(4));

            serialized.Update();
            serialized.FindProperty("clearedBattleHealthCost").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                definition.ResolveClearedBattleHealthCost(100),
                Is.Zero);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void DungeonEvent_ValidatesAuthoredChoiceAndEffect()
    {
        DungeonEventSO dungeonEvent =
            ScriptableObject.CreateInstance<DungeonEventSO>();
        try
        {
            SerializedObject serialized = new(dungeonEvent);
            SerializedProperty choices = serialized.FindProperty("choices");
            choices.arraySize = 1;
            SerializedProperty choice = choices.GetArrayElementAtIndex(0);
            choice.FindPropertyRelative("choiceId").stringValue = "search";
            choice.FindPropertyRelative("fallbackTitle").stringValue =
                "Search the room";
            SerializedProperty effects =
                choice.FindPropertyRelative("effects");
            effects.arraySize = 1;
            SerializedProperty effect = effects.GetArrayElementAtIndex(0);
            effect.FindPropertyRelative("effectType").enumValueIndex =
                (int)EDungeonRoomEffectType.RunCurrency;
            effect.FindPropertyRelative("amount").intValue = 25;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                dungeonEvent.TryValidate(out string error),
                Is.True,
                error);
            Assert.That(dungeonEvent.Choices.Count, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(dungeonEvent);
        }
    }

    [Test]
    public void DungeonEventGraph_ValidatesLinkedChoiceNodes()
    {
        DungeonEventSO dungeonEvent =
            ScriptableObject.CreateInstance<DungeonEventSO>();
        try
        {
            SerializedObject serialized = new(dungeonEvent);
            serialized.FindProperty("choiceGraphVersion").intValue = 1;
            SerializedProperty choices = serialized.FindProperty("choices");
            choices.arraySize = 3;
            ConfigureEventNode(
                choices.GetArrayElementAtIndex(0),
                "node_search",
                "search",
                false,
                "node_open");
            ConfigureEventNode(
                choices.GetArrayElementAtIndex(1),
                "node_leave",
                "leave",
                true);
            ConfigureEventNode(
                choices.GetArrayElementAtIndex(2),
                "node_open",
                "open",
                true);

            SerializedProperty entries =
                serialized.FindProperty("entryChoiceNodeIds");
            entries.arraySize = 2;
            entries.GetArrayElementAtIndex(0).stringValue = "node_search";
            entries.GetArrayElementAtIndex(1).stringValue = "node_leave";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                dungeonEvent.TryValidate(out string error),
                Is.True,
                error);
            List<DungeonEventChoiceNodeDefinition> entryNodes = new();
            dungeonEvent.GetEntryChoices(entryNodes);
            Assert.That(entryNodes.Count, Is.EqualTo(2));
            Assert.That(entryNodes[0].NextChoiceNodeIds,
                Is.EqualTo(new[] { "node_open" }));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(dungeonEvent);
        }
    }

    [Test]
    public void DungeonEventGraph_RejectsChoiceCycles()
    {
        DungeonEventSO dungeonEvent =
            ScriptableObject.CreateInstance<DungeonEventSO>();
        try
        {
            SerializedObject serialized = new(dungeonEvent);
            serialized.FindProperty("choiceGraphVersion").intValue = 1;
            SerializedProperty choices = serialized.FindProperty("choices");
            choices.arraySize = 2;
            ConfigureEventNode(
                choices.GetArrayElementAtIndex(0),
                "node_a",
                "a",
                false,
                "node_b");
            ConfigureEventNode(
                choices.GetArrayElementAtIndex(1),
                "node_b",
                "b",
                false,
                "node_a");

            SerializedProperty entries =
                serialized.FindProperty("entryChoiceNodeIds");
            entries.arraySize = 1;
            entries.GetArrayElementAtIndex(0).stringValue = "node_a";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(dungeonEvent.TryValidate(out string error), Is.False);
            StringAssert.Contains("cycle", error);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(dungeonEvent);
        }
    }

    [Test]
    public void DungeonEventGraph_RepairsNonFiniteEditorPosition()
    {
        DungeonEventSO dungeonEvent =
            ScriptableObject.CreateInstance<DungeonEventSO>();
        try
        {
            SerializedObject serialized = new(dungeonEvent);
            SerializedProperty choices = serialized.FindProperty("choices");
            choices.arraySize = 1;
            SerializedProperty node = choices.GetArrayElementAtIndex(0);
            ConfigureEventNode(
                node,
                "node_invalid_position",
                "invalid_position",
                true);
            node.FindPropertyRelative("editorPosition").vector2Value =
                new Vector2(float.NaN, float.PositiveInfinity);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DungeonEventEditorWindow.EnsureGraphData(dungeonEvent);

            Vector2 repaired = dungeonEvent.Choices[0].EditorPosition;
            Assert.That(float.IsNaN(repaired.x), Is.False);
            Assert.That(float.IsInfinity(repaired.x), Is.False);
            Assert.That(float.IsNaN(repaired.y), Is.False);
            Assert.That(float.IsInfinity(repaired.y), Is.False);
            Assert.That(repaired, Is.EqualTo(new Vector2(90f, 90f)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(dungeonEvent);
        }
    }

    [Test]
    public void EditorAssetUtility_RenamesDuplicatesLoadsAndRestoresSelection()
    {
        string folder = $"Assets/TempEditorUtil_{Guid.NewGuid():N}";
        AssetDatabase.CreateFolder("Assets", folder.Substring(7));
        try
        {
            DungeonEventSO source =
                ScriptableObject.CreateInstance<DungeonEventSO>();
            AssetDatabase.CreateAsset(source, $"{folder}/Original.asset");
            AssetDatabase.SaveAssets();

            Assert.That(
                PS260714EditorAssetUtility.TryRename(
                    source,
                    "Renamed.asset",
                    out string renameError),
                Is.True,
                renameError);
            Assert.That(
                AssetDatabase.GetAssetPath(source),
                Is.EqualTo($"{folder}/Renamed.asset"));

            Assert.That(
                PS260714EditorAssetUtility.TryDuplicate(
                    source,
                    null,
                    " Copy",
                    out DungeonEventSO duplicate,
                    out string duplicateError),
                Is.True,
                duplicateError);
            Assert.That(duplicate, Is.Not.Null);

            List<DungeonEventSO> loaded = new();
            PS260714EditorAssetUtility.LoadAssets(
                loaded,
                "t:DungeonEventSO",
                new[] { folder });
            Assert.That(loaded.Count, Is.EqualTo(2));
            Assert.That(loaded[0], Is.SameAs(source));
            Assert.That(loaded[1], Is.SameAs(duplicate));

            string duplicatePath =
                PS260714EditorAssetUtility.CapturePath(duplicate);
            Assert.That(
                PS260714EditorAssetUtility.RestoreSelection(
                    duplicatePath,
                    loaded),
                Is.SameAs(duplicate));
        }
        finally
        {
            AssetDatabase.DeleteAsset(folder);
            AssetDatabase.Refresh();
        }
    }

    [TestCase("")]
    [TestCase(".")]
    [TestCase("../invalid")]
    public void EditorAssetUtility_RejectsInvalidRename(string requested)
    {
        Assert.That(
            PS260714EditorAssetUtility.TryRename(
                null,
                requested,
                out string error),
            Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void DungeonEventDelete_ClearsDungeonReferencesBeforeDeleting()
    {
        string folder =
            $"Assets/TempDungeonEventDelete_{Guid.NewGuid():N}";
        AssetDatabase.CreateFolder("Assets", folder.Substring(7));
        string eventPath = $"{folder}/DeleteTarget.asset";
        try
        {
            DungeonEventSO target =
                ScriptableObject.CreateInstance<DungeonEventSO>();
            DungeonEventSO survivor =
                ScriptableObject.CreateInstance<DungeonEventSO>();
            DungeonDefinition definition =
                ScriptableObject.CreateInstance<DungeonDefinition>();
            AssetDatabase.CreateAsset(target, eventPath);
            AssetDatabase.CreateAsset(
                survivor,
                $"{folder}/Survivor.asset");

            SerializedObject serialized = new(definition);
            serialized.FindProperty("defaultEvent").objectReferenceValue =
                target;
            SerializedProperty fixedEvents =
                serialized.FindProperty("fixedEvents");
            fixedEvents.arraySize = 2;
            fixedEvents.GetArrayElementAtIndex(0).objectReferenceValue =
                target;
            fixedEvents.GetArrayElementAtIndex(1).objectReferenceValue =
                survivor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(
                definition,
                $"{folder}/Dungeon.asset");
            AssetDatabase.SaveAssets();

            string movedPath = null;
            bool deleted = DungeonEventAssetDelete.TryMoveToTrash(
                target,
                false,
                out string error,
                path =>
                {
                    movedPath = path;
                    return true;
                });

            Assert.That(deleted, Is.True, error);
            Assert.That(movedPath, Is.EqualTo(eventPath));
            serialized = new SerializedObject(definition);
            Assert.That(
                serialized.FindProperty("defaultEvent")
                    .objectReferenceValue,
                Is.Null);
            fixedEvents = serialized.FindProperty("fixedEvents");
            Assert.That(
                fixedEvents.GetArrayElementAtIndex(0)
                    .objectReferenceValue,
                Is.Null);
            Assert.That(
                fixedEvents.GetArrayElementAtIndex(1)
                    .objectReferenceValue,
                Is.SameAs(survivor));
        }
        finally
        {
            AssetDatabase.DeleteAsset(folder);
            AssetDatabase.Refresh();
        }
    }

    [Test]
    public void DungeonEventDelete_RestoresReferencesWhenTrashMoveFails()
    {
        string folder =
            $"Assets/TempDungeonEventRestore_{Guid.NewGuid():N}";
        AssetDatabase.CreateFolder("Assets", folder.Substring(7));
        try
        {
            DungeonEventSO target =
                ScriptableObject.CreateInstance<DungeonEventSO>();
            DungeonDefinition definition =
                ScriptableObject.CreateInstance<DungeonDefinition>();
            AssetDatabase.CreateAsset(
                target,
                $"{folder}/DeleteTarget.asset");

            SerializedObject serialized = new(definition);
            serialized.FindProperty("defaultEvent").objectReferenceValue =
                target;
            SerializedProperty fixedEvents =
                serialized.FindProperty("fixedEvents");
            fixedEvents.arraySize = 1;
            fixedEvents.GetArrayElementAtIndex(0).objectReferenceValue =
                target;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(
                definition,
                $"{folder}/Dungeon.asset");
            AssetDatabase.SaveAssets();

            bool deleted = DungeonEventAssetDelete.TryMoveToTrash(
                target,
                false,
                out string error,
                _ => false);

            Assert.That(deleted, Is.False);
            StringAssert.Contains("Failed to move", error);
            serialized = new SerializedObject(definition);
            Assert.That(
                serialized.FindProperty("defaultEvent")
                    .objectReferenceValue,
                Is.SameAs(target));
            Assert.That(
                serialized.FindProperty("fixedEvents")
                    .GetArrayElementAtIndex(0)
                    .objectReferenceValue,
                Is.SameAs(target));
        }
        finally
        {
            AssetDatabase.DeleteAsset(folder);
            AssetDatabase.Refresh();
        }
    }

    [Test]
    public void BattleArenaSetup_CircularValuesAreClampedToPlayableRange()
    {
        BattleArenaSetup setup = new(
            BattleArenaMode.CircularDefense,
            0,
            2,
            0.48f,
            0.1f);

        Assert.That(setup.UsesBattleCore, Is.True);
        Assert.That(setup.CoreMaximumHealth, Is.EqualTo(1));
        Assert.That(setup.LaneCount, Is.EqualTo(4));
        Assert.That(setup.WallRadiusNormalized, Is.EqualTo(0.4f));
        Assert.That(
            setup.SpawnRadiusNormalized,
            Is.EqualTo(0.45f).Within(0.0001f));
    }

    [Test]
    public void DungeonShieldMaximumHealth_OverridesCircularArenaCoreHealth()
    {
        DungeonDefinition definition =
            ScriptableObject.CreateInstance<DungeonDefinition>();
        try
        {
            SerializedObject serialized = new(definition);
            serialized.FindProperty("battleShieldMaximumHealth").intValue =
                240;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            BattleArenaSetup arena = BattleArenaSetup.CreateCircular()
                .WithCoreMaximumHealth(
                    definition.BattleShieldMaximumHealth);

            Assert.That(definition.BattleShieldMaximumHealth, Is.EqualTo(240));
            Assert.That(arena.CoreMaximumHealth, Is.EqualTo(240));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void BattleCoreRuntime_DamageHealAndDestructionAreBounded()
    {
        BattleCoreRuntime core = new();
        int destroyedCount = 0;
        int healthEventCount = 0;
        core.Destroyed += () => destroyedCount++;
        core.HealthChanged += (_, _) => healthEventCount++;

        core.Configure(100, true);
        Assert.That(core.TakeDamage(30), Is.EqualTo(30));
        Assert.That(core.Heal(10), Is.EqualTo(10));
        Assert.That(core.CurrentHealth, Is.EqualTo(80));
        Assert.That(core.TakeDamage(999), Is.EqualTo(80));
        Assert.That(core.IsDestroyed, Is.True);
        Assert.That(core.TakeDamage(1), Is.Zero);
        Assert.That(core.Heal(1), Is.Zero);
        Assert.That(destroyedCount, Is.EqualTo(1));
        Assert.That(healthEventCount, Is.EqualTo(4));
    }

    [Test]
    public void BattleCoreRuntime_ConfigureCanPreserveRunShieldHealth()
    {
        BattleCoreRuntime core = new();

        core.Configure(100, true, 37);

        Assert.That(core.MaximumHealth, Is.EqualTo(100));
        Assert.That(core.CurrentHealth, Is.EqualTo(37));
        Assert.That(core.IsDestroyed, Is.False);
    }

    [Test]
    public void DungeonShieldRecoveryRule_ResolvesFixedAndPercentValues()
    {
        DungeonShieldRecoveryRule fixedRule = new();
        SetRuleField(
            fixedRule,
            "amountMode",
            DungeonShieldRecoveryAmountMode.Fixed);
        SetRuleField(fixedRule, "amount", 23f);
        DungeonShieldRecoveryRule percentRule = new();
        SetRuleField(
            percentRule,
            "amountMode",
            DungeonShieldRecoveryAmountMode.PercentOfMaximum);
        SetRuleField(percentRule, "amount", 12.5f);

        Assert.That(fixedRule.ResolveAmount(200), Is.EqualTo(23));
        Assert.That(percentRule.ResolveAmount(200), Is.EqualTo(25));
        Assert.That(percentRule.ResolveAmount(101), Is.EqualTo(13));
    }

    [Test]
    public void BattleSetup_LegacyConstructorKeepsGridCapacity()
    {
        List<EnemyRuntime> enemies = new();
        for (int index = 0; index < 12; index++)
            enemies.Add(null);

        BattleSetup setup = new(
            3,
            8,
            1f,
            30f,
            default,
            enemies);

        Assert.That(setup.Arena.UsesBattleCore, Is.False);
        Assert.That(setup.InitialEnemyCount, Is.EqualTo(9));
    }

    private static void SetRuleField<T>(
        DungeonShieldRecoveryRule rule,
        string fieldName,
        T value)
    {
        FieldInfo field = typeof(DungeonShieldRecoveryRule).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(rule, value);
    }

    private static void ConfigureEventNode(
        SerializedProperty node,
        string nodeId,
        string choiceId,
        bool endsEvent,
        params string[] nextNodeIds)
    {
        node.FindPropertyRelative("nodeId").stringValue = nodeId;
        node.FindPropertyRelative("choiceId").stringValue = choiceId;
        node.FindPropertyRelative("fallbackTitle").stringValue = choiceId;
        node.FindPropertyRelative("conditions").arraySize = 0;
        node.FindPropertyRelative("effects").arraySize = 0;
        node.FindPropertyRelative("endsEvent").boolValue = endsEvent;
        SerializedProperty next =
            node.FindPropertyRelative("nextChoiceNodeIds");
        next.arraySize = nextNodeIds.Length;
        for (int index = 0; index < nextNodeIds.Length; index++)
            next.GetArrayElementAtIndex(index).stringValue = nextNodeIds[index];
    }
}
