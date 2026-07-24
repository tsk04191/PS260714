using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EnemyP0RegressionTests
{
    private const string EnemyAssetFolder = "Assets/Data/Enemies/";
    private const string FireStatusPath =
        "Assets/Resources/StatusEffects/Fire.asset";
    private const string StunStatusPath =
        "Assets/Resources/StatusEffects/Stun.asset";

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
    public void Assault_MultipliesThePendingSpawnIntervalByOneHalf()
    {
        BattleManager manager = CreateBattleManager();
        SetPrivateField(manager, "_spawnInterval", 10f);
        GetPrivateList<EnemyRuntime>(manager, "_spawnQueue").Add(
            LoadEnemy("Assault").CreateRuntime());

        Assert.That(manager.SpawnInterval, Is.EqualTo(5f));
    }

    [Test]
    public void Heavy_FirstThreePhysicalHitsDealOneDamage()
    {
        EnemyRuntime heavy = LoadEnemy("Heavy").CreateRuntime();
        DungeonBoardView board = CreateBoard((1, 1, heavy));

        Assert.That(board.TryDamageEnemy(heavy, 10), Is.EqualTo(1));
        Assert.That(board.TryDamageEnemy(heavy, 10), Is.EqualTo(1));
        Assert.That(board.TryDamageEnemy(heavy, 10), Is.EqualTo(1));
        Assert.That(
            GetAbilityRemainingCharges(
                heavy,
                EnemyAbilityIds.GuardedHits),
            Is.Zero);
        Assert.That(board.TryDamageEnemy(heavy, 10), Is.EqualTo(10));
        Assert.That(heavy.Health, Is.EqualTo(7));
    }

    [Test]
    public void Heavy_FixedDamageDoesNotConsumeGuardedHits()
    {
        EnemyRuntime heavy = LoadEnemy("Heavy").CreateRuntime();
        DungeonBoardView board = CreateBoard((1, 1, heavy));

        Assert.That(
            board.TryDamageCharacterTargets(
                null,
                new[] { heavy },
                5,
                CharacterAttackDamageType.Fixed,
                false),
            Is.EqualTo(5));
        Assert.That(
            GetAbilityRemainingCharges(
                heavy,
                EnemyAbilityIds.GuardedHits),
            Is.EqualTo(3));

        Assert.That(board.TryDamageEnemy(heavy, 10), Is.EqualTo(1));
        Assert.That(
            GetAbilityRemainingCharges(
                heavy,
                EnemyAbilityIds.GuardedHits),
            Is.EqualTo(2));
    }

    [Test]
    public void Medic_HealsEveryOrthogonalNeighborButNotDiagonal()
    {
        EnemyRuntime medic = LoadEnemy("Medic").CreateRuntime();
        EnemyRuntime up = CreateInjuredBasic();
        EnemyRuntime down = CreateInjuredBasic();
        EnemyRuntime left = CreateInjuredBasic();
        EnemyRuntime right = CreateInjuredBasic();
        EnemyRuntime diagonal = CreateInjuredBasic();
        DungeonBoardView board = CreateBoard(
            (1, 1, medic),
            (0, 1, up),
            (2, 1, down),
            (1, 0, left),
            (1, 2, right),
            (0, 0, diagonal));

        board.TickEnemyAbilities(4f, Array.Empty<IBattleCharacter>());

        Assert.That(up.Health, Is.EqualTo(11));
        Assert.That(down.Health, Is.EqualTo(11));
        Assert.That(left.Health, Is.EqualTo(11));
        Assert.That(right.Health, Is.EqualTo(11));
        Assert.That(diagonal.Health, Is.EqualTo(10));
        Assert.That(
            medic.AbilityCooldownRemaining,
            Is.EqualTo(4f).Within(0.0001f));
    }

    [Test]
    public void Medic_FailedActivationStaysReadyAndRetriesImmediately()
    {
        EnemyRuntime medic = LoadEnemy("Medic").CreateRuntime();
        EnemyRuntime neighbor = LoadEnemy("Basic").CreateRuntime();
        DungeonBoardView board = CreateBoard(
            (1, 1, medic),
            (1, 2, neighbor));

        board.TickEnemyAbilities(4f, Array.Empty<IBattleCharacter>());

        Assert.That(medic.AbilityCooldownRemaining, Is.Zero);

        SetEnemyHealth(neighbor, neighbor.MaxHealth - 1);
        board.TickEnemyAbilities(0.1f, Array.Empty<IBattleCharacter>());

        Assert.That(neighbor.Health, Is.EqualTo(neighbor.MaxHealth));
        Assert.That(
            medic.AbilityCooldownRemaining,
            Is.EqualTo(4f).Within(0.0001f));
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
    public void Mechanic_WithNoPositiveDamageTargetStaysReady()
    {
        EnemyRuntime mechanic = LoadEnemy("Mechanic").CreateRuntime();
        DungeonBoardView board = CreateBoard((1, 1, mechanic));
        FakeBattleCharacter first = new(0);
        FakeBattleCharacter second = new(0);

        board.TickEnemyAbilities(
            10f,
            new IBattleCharacter[] { first, second });

        Assert.That(first.StatusApplicationCount, Is.Zero);
        Assert.That(second.StatusApplicationCount, Is.Zero);
        Assert.That(mechanic.AbilityCooldownRemaining, Is.Zero);
    }

    [Test]
    public void Pointman_SpawnsWithTheNextTwoQueuedEnemies()
    {
        BattleManager manager = CreateBattleManager();
        RecordingBattleBoard board = new();
        SetPrivateField(manager, "_board", board);
        SetPrivateField(manager, "_spawnInterval", 1f);

        EnemyRuntime pointman = LoadEnemy("Pointman").CreateRuntime();
        EnemyRuntime firstCompanion = LoadEnemy("Basic").CreateRuntime();
        EnemyRuntime secondCompanion = LoadEnemy("Basic").CreateRuntime();
        EnemyRuntime remaining = LoadEnemy("Basic").CreateRuntime();
        List<EnemyRuntime> queue =
            GetPrivateList<EnemyRuntime>(manager, "_spawnQueue");
        queue.Add(pointman);
        queue.Add(firstCompanion);
        queue.Add(secondCompanion);
        queue.Add(remaining);

        Assert.That(InvokeTrySpawn(manager), Is.True);

        Assert.That(board.AddGroupCallCount, Is.EqualTo(1));
        Assert.That(
            board.LastSpawnGroup,
            Is.EqualTo(new[]
            {
                pointman,
                firstCompanion,
                secondCompanion,
            }));
        Assert.That(manager.PendingEnemyCount, Is.EqualTo(1));
        Assert.That(manager.SpawnQueue[0], Is.SameAs(remaining));
        Assert.That(manager.SpawnedEnemyCount, Is.EqualTo(3));
    }

    [Test]
    public void Pointman_FailedGroupSpawnKeepsTheQueueIntact()
    {
        BattleManager manager = CreateBattleManager();
        RecordingBattleBoard board = new()
        {
            AllowSpawn = false,
        };
        SetPrivateField(manager, "_board", board);

        EnemyRuntime pointman = LoadEnemy("Pointman").CreateRuntime();
        EnemyRuntime firstCompanion = LoadEnemy("Basic").CreateRuntime();
        EnemyRuntime secondCompanion = LoadEnemy("Basic").CreateRuntime();
        List<EnemyRuntime> queue =
            GetPrivateList<EnemyRuntime>(manager, "_spawnQueue");
        queue.Add(pointman);
        queue.Add(firstCompanion);
        queue.Add(secondCompanion);

        Assert.That(InvokeTrySpawn(manager), Is.False);

        Assert.That(manager.PendingEnemyCount, Is.EqualTo(3));
        Assert.That(manager.SpawnQueue[0], Is.SameAs(pointman));
        Assert.That(manager.SpawnedEnemyCount, Is.Zero);
        Assert.That(manager.IsBoardFull, Is.True);
    }

    [Test]
    public void ShieldBearer_GainsFullHealthArmorAndRedirectsDiagonalDamage()
    {
        EnemyRuntime target = LoadEnemy("Basic").CreateRuntime();
        EnemyRuntime shieldBearer =
            LoadEnemy("ShieldBearer").CreateRuntime();
        DungeonBoardView board = CreateBoard(
            (1, 1, target),
            (0, 0, shieldBearer));

        Assert.That(shieldBearer.Armor, Is.Zero);
        InvokeSpawnAbilities(board, 0, 0, shieldBearer);
        Assert.That(
            shieldBearer.Armor,
            Is.EqualTo(shieldBearer.MaxHealth));
        Assert.That(board.TryDamageEnemy(target, 5), Is.EqualTo(5));
        Assert.That(target.Health, Is.EqualTo(target.MaxHealth));
        Assert.That(
            shieldBearer.Armor,
            Is.EqualTo(shieldBearer.MaxHealth - 5));
        Assert.That(
            shieldBearer.Health,
            Is.EqualTo(shieldBearer.MaxHealth));
    }

    [Test]
    public void Infiltrator_IsExcludedOnlyWhileAnotherTargetExists()
    {
        FakeBattleCharacter source = new(1);
        EnemyRuntime basic = LoadEnemy("Basic").CreateRuntime();
        EnemyRuntime infiltrator =
            LoadEnemy("Infiltrator").CreateRuntime();
        DungeonBoardView mixedBoard = CreateBoard(
            (1, 1, basic),
            (1, 2, infiltrator));

        IReadOnlyList<EnemyRuntime> mixedTargets =
            mixedBoard.SelectCharacterTargets(
                source,
                CharacterAttackSubject.All,
                CharacterAttackSubjectMetric.Health,
                10,
                CharacterConditionMatchMode.All,
                Array.Empty<CharacterNumericCondition>());

        Assert.That(mixedTargets, Is.EqualTo(new[] { basic }));

        EnemyRuntime loneInfiltrator =
            LoadEnemy("Infiltrator").CreateRuntime();
        DungeonBoardView loneBoard = CreateBoard(
            (1, 1, loneInfiltrator));

        IReadOnlyList<EnemyRuntime> loneTargets =
            loneBoard.SelectCharacterTargets(
                source,
                CharacterAttackSubject.All,
                CharacterAttackSubjectMetric.Health,
                10,
                CharacterConditionMatchMode.All,
                Array.Empty<CharacterNumericCondition>());

        Assert.That(loneTargets, Is.EqualTo(new[] { loneInfiltrator }));
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
    public void HeavyAbilityAsset_PreservesGuardedDamageBehavior()
    {
        EnemyRuntime heavy =
            CreateAbilityAssetClone("Heavy").CreateRuntime();
        DungeonBoardView board = CreateBoard((1, 1, heavy));

        Assert.That(board.TryDamageEnemy(heavy, 10), Is.EqualTo(1));
        Assert.That(
            board.TryDamageCharacterTargets(
                null,
                new[] { heavy },
                10,
                CharacterAttackDamageType.Magical,
                false),
            Is.EqualTo(1));
        Assert.That(
            board.TryDamageCharacterTargets(
                null,
                new[] { heavy },
                5,
                CharacterAttackDamageType.Fixed,
                false),
            Is.EqualTo(5));
        Assert.That(
            GetAbilityRemainingCharges(
                heavy,
                EnemyAbilityIds.GuardedHits),
            Is.EqualTo(1));
        Assert.That(board.TryDamageEnemy(heavy, 10), Is.EqualTo(1));
        Assert.That(board.TryDamageEnemy(heavy, 10), Is.EqualTo(10));
    }

    [Test]
    public void RuntimeDefaultHeavy_UsesTheSameModularGuardPath()
    {
        EnemyRuntime heavy =
            CreateRuntimeDefault(EEnemyType.Heavy).CreateRuntime();
        DungeonBoardView board = CreateBoard((1, 1, heavy));

        Assert.That(board.TryDamageEnemy(heavy, 10), Is.EqualTo(1));
        Assert.That(
            GetAbilityRemainingCharges(
                heavy,
                EnemyAbilityIds.GuardedHits),
            Is.EqualTo(2));
    }

    [Test]
    public void MechanicAbilityAsset_RetriesUntilDamageTargetExists()
    {
        EnemyRuntime mechanic =
            CreateAbilityAssetClone("Mechanic").CreateRuntime();
        DungeonBoardView board = CreateBoard((1, 1, mechanic));
        FakeBattleCharacter noDamage = new(0);

        board.TickEnemyAbilities(
            10f,
            new[] { noDamage });

        Assert.That(noDamage.StatusApplicationCount, Is.Zero);
        Assert.That(mechanic.AbilityCooldownRemaining, Is.Zero);

        FakeBattleCharacter damageDealer = new(8);
        board.TickEnemyAbilities(
            0.1f,
            new[] { noDamage, damageDealer });

        Assert.That(
            damageDealer.StatusApplicationCount,
            Is.EqualTo(1));
        Assert.That(
            mechanic.AbilityCooldownRemaining,
            Is.EqualTo(10f));
    }

    [Test]
    public void ShieldBearerAbilityAsset_GrantsArmorOnceAndRedirects()
    {
        EnemyRuntime shield =
            CreateAbilityAssetClone("ShieldBearer").CreateRuntime();
        EnemyRuntime ally = LoadEnemy("Basic").CreateRuntime();
        DungeonBoardView board = CreateBoard(
            (1, 1, shield),
            (1, 2, ally));

        Assert.That(shield.Armor, Is.Zero);
        InvokeSpawnAbilities(board, 1, 1, shield);
        Assert.That(shield.Armor, Is.EqualTo(shield.MaxHealth));

        Assert.That(board.TryDamageEnemy(ally, 5), Is.EqualTo(5));
        Assert.That(ally.Health, Is.EqualTo(ally.MaxHealth));
        Assert.That(
            shield.Armor,
            Is.EqualTo(shield.MaxHealth - 5));
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
            amount: 1,
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
        bool requireAlternateTarget = false)
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

    private EnemySO LoadEnemy(string assetName)
    {
        string path = EnemyAssetFolder + assetName + ".asset";
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

        List<DungeonTileView> tiles =
            GetPrivateList<DungeonTileView>(board, "_tiles");
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                GameObject tileObject = new(
                    $"Test_Tile_{row}_{column}",
                    typeof(RectTransform));
                tileObject.transform.SetParent(boardObject.transform, false);
                DungeonTileView tile =
                    tileObject.AddComponent<DungeonTileView>();
                tile.Initialize(row, column, 8);

                if (occupantMap.TryGetValue(
                        (row, column),
                        out EnemyRuntime enemy))
                {
                    GetPrivateList<EnemyRuntime>(
                        tile,
                        "_enemies").Add(enemy);
                    GetPrivateList<EnemyCard>(
                        tile,
                        "_cards").Add(null);
                }

                tiles.Add(tile);
            }
        }

        return board;
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
        List<DungeonTileView> tiles =
            GetPrivateList<DungeonTileView>(board, "_tiles");
        DungeonTileView tile = tiles[row * board.GridSize + column];
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

    private static List<T> GetPrivateList<T>(
        object target,
        string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            InstanceNonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
        return (List<T>)field.GetValue(target);
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            InstanceNonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
        field.SetValue(target, value);
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
        public int AddGroupCallCount { get; private set; }
        public IReadOnlyList<EnemyRuntime> LastSpawnGroup
            { get; private set; } = Array.Empty<EnemyRuntime>();
        public IReadOnlyList<EnemyRuntime> LastHealedEnemyTargets
            { get; private set; } = Array.Empty<EnemyRuntime>();

        public int InitialEnemyCapacity => 9;
        public int LivingEnemyCount => 0;
        public bool HasEmptyEnemyTile => true;

        public event Action<EnemyRuntime> EnemyDefeated
        {
            add { }
            remove { }
        }

        public event Action<BattleStatusAppliedEvent> StatusApplied
        {
            add { }
            remove { }
        }

        public bool TryAddEnemy(EnemyRuntime enemy)
        {
            LastSpawnGroup = enemy != null
                ? new[] { enemy }
                : Array.Empty<EnemyRuntime>();
            return AllowSpawn && enemy != null;
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
            IBattleCharacter source,
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
            IBattleCharacter source,
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
            CharacterStatusRemovalTarget removalTarget,
            StatusEffectSO statusEffect,
            int removalCount,
            bool showAttackRange)
        {
            return false;
        }

        public bool TryRemoveAlliedCharacterStatus(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            CharacterStatusRemovalTarget removalTarget,
            StatusEffectSO statusEffect,
            int removalCount)
        {
            return false;
        }
    }

    private sealed class FakeBattleCharacter : IBattleCharacter
    {
        private int _currentHealth;

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
            return false;
        }

        public int GetStatusStackCount(StatusEffectSO statusEffect)
        {
            return 0;
        }

        public IReadOnlyList<BattleStatusSnapshot> GetActiveStatusEffects()
        {
            return Array.Empty<BattleStatusSnapshot>();
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
            CharacterStatusRemovalTarget removalTarget,
            StatusEffectSO statusEffect,
            int removalCount)
        {
            return 0;
        }
    }
}
