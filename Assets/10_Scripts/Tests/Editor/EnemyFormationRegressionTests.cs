using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyFormationRegressionTests
{
    private const int TestLaneCount = 4;
    private const int TestLayerCount = 3;
    private const float TestLayerSpacing = 0.55f;
    private const float TestSeparationRatio = 0.75f;
    private const float PositionTolerance = 0.01f;

    private static readonly BindingFlags InstanceFields =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    private readonly List<UnityEngine.Object> _createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = _createdObjects.Count - 1;
             index >= 0;
             index--)
        {
            if (_createdObjects[index] != null)
                UnityEngine.Object.DestroyImmediate(_createdObjects[index]);
        }

        _createdObjects.Clear();
    }

    [Test]
    public void ArenaSetup_FormationDefaultsAndInvalidValuesAreNormalized()
    {
        BattleArenaSetup defaults = BattleArenaSetup.CreateCircular(
            laneCount: TestLaneCount);

        Assert.That(
            defaults.MaximumLayerCount,
            Is.EqualTo(BattleArenaSetup.DefaultMaximumLayerCount));
        Assert.That(
            defaults.LayerSpacing,
            Is.EqualTo(BattleArenaSetup.DefaultLayerSpacing));
        Assert.That(
            defaults.FormationSeparationRatio,
            Is.EqualTo(
                BattleArenaSetup.DefaultFormationSeparationRatio));
        Assert.That(
            defaults.MaximumEnemyCapacity,
            Is.EqualTo(
                TestLaneCount *
                BattleArenaSetup.DefaultMaximumLayerCount));

        BattleArenaSetup bounded = BattleArenaSetup.CreateCircular(
            laneCount: 2,
            maximumLayerCount: 100,
            layerSpacing: 100f,
            formationSeparationRatio: 0.1f);

        Assert.That(bounded.LaneCount, Is.EqualTo(4));
        Assert.That(
            bounded.MaximumLayerCount,
            Is.EqualTo(BattleArenaSetup.MaximumLayerCountLimit));
        Assert.That(
            bounded.LayerSpacing,
            Is.EqualTo(BattleArenaSetup.MaximumLayerSpacing));
        Assert.That(
            bounded.FormationSeparationRatio,
            Is.EqualTo(
                BattleArenaSetup.MinimumFormationSeparationRatio));

        BattleArenaSetup nonFinite = BattleArenaSetup.CreateCircular(
            laneCount: TestLaneCount,
            maximumLayerCount: 0,
            layerSpacing: float.NaN,
            formationSeparationRatio: float.PositiveInfinity);

        Assert.That(
            nonFinite.MaximumLayerCount,
            Is.EqualTo(BattleArenaSetup.MinimumLayerCount));
        Assert.That(
            nonFinite.LayerSpacing,
            Is.EqualTo(BattleArenaSetup.DefaultLayerSpacing));
        Assert.That(
            nonFinite.FormationSeparationRatio,
            Is.EqualTo(
                BattleArenaSetup.DefaultFormationSeparationRatio));
    }

    [Test]
    public void CircularCapacity_SeparatesInitialLanesFromTotalLayers()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestLayerCount);

        Assert.That(board.InitialEnemyCapacity, Is.EqualTo(TestLaneCount));
        Assert.That(
            board.TotalEnemyCapacity,
            Is.EqualTo(TestLaneCount * TestLayerCount));

        for (int index = 0;
             index < setup.MaximumEnemyCapacity;
             index++)
        {
            Assert.That(
                board.TryAddEnemy(CreateEnemy($"capacity_{index}")),
                Is.True,
                $"Enemy {index} should occupy a formation slot.");
        }

        EnemyRuntime overflow = CreateEnemy("capacity_overflow");
        Assert.That(board.TryAddEnemy(overflow), Is.False);
        Assert.That(
            board.LivingEnemyCount,
            Is.EqualTo(setup.MaximumEnemyCapacity));
    }

    [Test]
    public void FirstLayer_UsesDistinctSectorsAndMinimumCenterSeparation()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestLayerCount);
        List<EnemyRuntime> enemies = AddEnemies(
            board,
            TestLaneCount,
            "first_layer");

        HashSet<int> sectors = new();
        foreach (EnemyRuntime enemy in enemies)
        {
            Assert.That(GetStateInt(board, enemy, "LayerIndex"), Is.Zero);
            Assert.That(
                sectors.Add(GetStateInt(board, enemy, "SectorIndex")),
                Is.True,
                "Every first-layer enemy must use a distinct sector.");
        }

        AssertPairwiseFormationSeparation(board, enemies, setup);
    }

    [Test]
    public void RemovingFrontEnemy_CompactsRearEnemyTowardDefenseLine()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestLayerCount);
        List<EnemyRuntime> enemies = AddEnemies(
            board,
            TestLaneCount + 1,
            "compact",
            attackInterval: 1000f);
        AdvanceToFormation(board);

        EnemyRuntime rear = FindEnemyAtLayer(board, enemies, 1);
        int sector = GetStateInt(board, rear, "SectorIndex");
        EnemyRuntime front = FindEnemyAtSectorAndLayer(
            board,
            enemies,
            sector,
            0);
        float previousRadius = GetPosition(board, rear).magnitude;
        float previousTargetRadius =
            GetStateFloat(board, rear, "TargetRadius");
        int frontHealth = front.Health;

        Assert.That(
            board.TryDamageEnemy(front, frontHealth),
            Is.EqualTo(frontHealth));
        Assert.That(board.ContainsTargetableEnemy(front), Is.False);
        Assert.That(GetStateInt(board, rear, "SectorIndex"), Is.EqualTo(sector));
        Assert.That(GetStateInt(board, rear, "LayerIndex"), Is.Zero);
        Assert.That(
            GetStateFloat(board, rear, "TargetRadius"),
            Is.LessThan(previousTargetRadius));

        board.TickEnemyAbilities(0.25f, Array.Empty<IBattleCharacter>());

        Assert.That(
            GetPosition(board, rear).magnitude,
            Is.LessThan(previousRadius - PositionTolerance));
    }

    [Test]
    public void CoreRange_FrontMeleeAndRearRangedAttackButRearMeleeWaits()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestLayerCount);
        List<EnemyRuntime> enemies = new();
        for (int index = 0; index < TestLaneCount; index++)
        {
            EnemyRuntime front = CreateEnemy(
                $"front_melee_{index}",
                coreAttackRange: 0f,
                coreAttackDamage: 1,
                attackInterval: 1f);
            Assert.That(board.TryAddEnemy(front), Is.True);
            enemies.Add(front);
        }

        EnemyRuntime rearRanged = CreateEnemy(
            "rear_ranged",
            coreAttackRange: 10f,
            coreAttackDamage: 7,
            attackInterval: 1f);
        EnemyRuntime rearMelee = CreateEnemy(
            "rear_melee",
            coreAttackRange: 0f,
            coreAttackDamage: 11,
            attackInterval: 1f);
        Assert.That(board.TryAddEnemy(rearRanged), Is.True);
        Assert.That(board.TryAddEnemy(rearMelee), Is.True);
        enemies.Add(rearRanged);
        enemies.Add(rearMelee);
        AdvanceToFormation(board);

        Assert.That(
            GetStateInt(board, rearRanged, "LayerIndex"),
            Is.GreaterThan(0));
        Assert.That(
            GetStateInt(board, rearMelee, "LayerIndex"),
            Is.GreaterThan(0));
        float rearMeleeTimer =
            GetStateFloat(board, rearMelee, "AttackTimeRemaining");
        int previousCoreHealth = board.Objective.CurrentHealth;

        board.TickEnemyAbilities(1f, Array.Empty<IBattleCharacter>());

        int expectedDamage = TestLaneCount + rearRanged.CoreAttackDamage;
        Assert.That(
            board.Objective.CurrentHealth,
            Is.EqualTo(previousCoreHealth - expectedDamage));
        Assert.That(
            GetStateFloat(board, rearMelee, "AttackTimeRemaining"),
            Is.EqualTo(rearMeleeTimer).Within(0.0001f));
        Assert.That(
            ContainsReference(
                board.SelectRecentCoreAttackers(),
                rearRanged),
            Is.True);
        Assert.That(
            ContainsReference(
                board.SelectRecentCoreAttackers(),
                rearMelee),
            Is.False);
    }

    [Test]
    public void OutOfRangeEnemy_DoesNotAdvanceAttackTimerOrDamageCore()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestLayerCount);
        List<EnemyRuntime> enemies = AddEnemies(
            board,
            TestLaneCount,
            "range_blocker",
            attackInterval: 1000f);
        EnemyRuntime rearMelee = CreateEnemy(
            "out_of_range_melee",
            coreAttackRange: 0f,
            coreAttackDamage: 20,
            attackInterval: 1f);
        Assert.That(board.TryAddEnemy(rearMelee), Is.True);
        enemies.Add(rearMelee);
        AdvanceToFormation(board);

        Assert.That(GetStateInt(board, rearMelee, "LayerIndex"), Is.EqualTo(1));
        int previousCoreHealth = board.Objective.CurrentHealth;
        float previousTimer =
            GetStateFloat(board, rearMelee, "AttackTimeRemaining");

        board.TickEnemyAbilities(0.5f, Array.Empty<IBattleCharacter>());

        Assert.That(board.Objective.CurrentHealth, Is.EqualTo(previousCoreHealth));
        Assert.That(
            GetStateFloat(board, rearMelee, "AttackTimeRemaining"),
            Is.EqualTo(previousTimer).Within(0.0001f));
    }

    [Test]
    public void PullTowardSharedPoint_ReassignsSlotsWithoutFullOverlap()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestLayerCount);
        List<EnemyRuntime> enemies = AddEnemies(
            board,
            TestLaneCount * 2,
            "pull",
            attackInterval: 1000f);
        AdvanceToFormation(board);

        int changed = board.PullEnemiesTowardPoint(
            enemies,
            new Vector2(0.01f, 0f),
            100f);

        Assert.That(changed, Is.GreaterThan(0));
        AssertPairwiseFormationSeparation(board, enemies, setup);
    }

    [Test]
    public void SpatialSelectors_UseResolvedLayeredPositions()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestLayerCount);
        List<EnemyRuntime> enemies = AddEnemies(
            board,
            TestLaneCount + 1,
            "spatial",
            attackInterval: 1000f);
        AdvanceToFormation(board);

        EnemyRuntime rear = FindEnemyAtLayer(board, enemies, 1);
        EnemyRuntime front = FindEnemyAtSectorAndLayer(
            board,
            enemies,
            GetStateInt(board, rear, "SectorIndex"),
            0);
        IReadOnlyList<EnemyRuntime> defenseLine =
            board.SelectDefenseLineEnemies();
        IReadOnlyList<EnemyRuntime> nearby = board.SelectNearbyEnemies(
            BattleStatusTarget.FromEnemy(front),
            setup.LayerSpacing + 0.1f,
            0,
            false);
        IReadOnlyList<EnemyRuntime> behind = board.SelectEnemiesBehind(
            front,
            setup.LayerSpacing + 0.1f,
            0,
            20f);

        Assert.That(defenseLine.Count, Is.EqualTo(TestLaneCount));
        Assert.That(ContainsReference(defenseLine, front), Is.True);
        Assert.That(ContainsReference(defenseLine, rear), Is.False);
        Assert.That(ContainsReference(nearby, rear), Is.True);
        Assert.That(ContainsReference(behind, rear), Is.True);
    }

    [Test]
    public void LegacyGrid_KeepsStackAndCoordinateSemantics()
    {
        DungeonBoardView board = CreateBoard(
            BattleArenaSetup.Legacy,
            2);
        EnemyRuntime first = CreateEnemy("legacy_first");
        EnemyRuntime second = CreateEnemy("legacy_second");

        Assert.That(board.InitialEnemyCapacity, Is.EqualTo(9));
        Assert.That(board.TryAddEnemyCard(1, 1, first), Is.True);
        Assert.That(board.TryAddEnemyCard(1, 1, second), Is.True);
        Assert.That(board.GetStackCount(1, 1), Is.EqualTo(2));
        Assert.That(GetPosition(board, first), Is.EqualTo(Vector2.zero));
        Assert.That(GetPosition(board, second), Is.EqualTo(Vector2.zero));
        Assert.That(board.SelectDefenseLineEnemies().Count, Is.Zero);
        Assert.That(board.Objective.IsActive, Is.False);

        int secondHealth = second.Health;
        Assert.That(
            board.TryDamageEnemy(second, secondHealth),
            Is.EqualTo(secondHealth));
        Assert.That(board.GetStackCount(1, 1), Is.EqualTo(1));
        Assert.That(board.ContainsTargetableEnemy(first), Is.True);
    }

    private static BattleArenaSetup CreateFormationSetup()
    {
        return BattleArenaSetup.CreateCircular(
            coreMaximumHealth: 100,
            laneCount: TestLaneCount,
            maximumLayerCount: TestLayerCount,
            layerSpacing: TestLayerSpacing,
            formationSeparationRatio: TestSeparationRatio);
    }

    private DungeonBoardView CreateBoard(
        BattleArenaSetup setup,
        int stackSize)
    {
        GameObject boardObject = new(
            $"Test_EnemyFormation_{_createdObjects.Count}",
            typeof(RectTransform));
        boardObject.SetActive(false);
        _createdObjects.Add(boardObject);
        boardObject.AddComponent<BattleVfxPlayer>();
        DungeonBoardView board =
            boardObject.AddComponent<DungeonBoardView>();
        SetField(
            board,
            "boardRect",
            boardObject.GetComponent<RectTransform>());

        board.ConfigureArena(setup);
        if (setup.UsesBattleCore)
            BindTemporaryWorldReferences(board, boardObject);
        board.Initialize(3, Mathf.Max(1, stackSize));
        if (setup.UsesBattleCore)
            SetField<GameObject>(board, "worldPresentationRoot", null);
        return board;
    }

    private static void BindTemporaryWorldReferences(
        DungeonBoardView board,
        GameObject boardObject)
    {
        GameObject world = new("Test_TemporaryWorldReferences");
        world.transform.SetParent(boardObject.transform, false);
        Camera worldCamera = world.AddComponent<Camera>();
        DungeonWorldInputView input =
            world.AddComponent<DungeonWorldInputView>();
        DungeonBattleCoreWorldGaugeView coreGauge =
            world.AddComponent<DungeonBattleCoreWorldGaugeView>();
        GameObject foreground = new("Test_TemporaryForegroundCamera");
        foreground.transform.SetParent(boardObject.transform, false);
        Camera foregroundCamera = foreground.AddComponent<Camera>();

        SetField(board, "worldPresentationRoot", world);
        SetField(board, "worldOutput", world);
        SetField(board, "worldCamera", worldCamera);
        SetField(board, "worldForegroundCamera", foregroundCamera);
        SetField(board, "worldActorRoot", world.transform);
        SetField(board, "worldActorPrefab", world);
        SetField(board, "worldAreaPreviewPrefab", world);
        SetField(board, "worldInputView", input);
        SetField(board, "worldGround", world.transform);
        SetField(board, "worldArenaRing", world.transform);
        SetField(board, "worldBattleCoreGauge", coreGauge);
    }

    private List<EnemyRuntime> AddEnemies(
        DungeonBoardView board,
        int count,
        string namePrefix,
        float coreAttackRange = 0f,
        int coreAttackDamage = 1,
        float attackInterval = 2f)
    {
        List<EnemyRuntime> result = new(count);
        for (int index = 0; index < count; index++)
        {
            EnemyRuntime enemy = CreateEnemy(
                $"{namePrefix}_{index}",
                coreAttackRange,
                coreAttackDamage,
                attackInterval);
            Assert.That(
                board.TryAddEnemy(enemy),
                Is.True,
                $"Could not add enemy {index} for {namePrefix}.");
            result.Add(enemy);
        }

        return result;
    }

    private EnemyRuntime CreateEnemy(
        string enemyId,
        float coreAttackRange = 0f,
        int coreAttackDamage = 1,
        float attackInterval = 2f,
        float formationRadius = EnemySO.DefaultFormationRadius)
    {
        EnemySO definition = ScriptableObject.CreateInstance<EnemySO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.name = enemyId;
        _createdObjects.Add(definition);
        SetField(definition, "enemyId", enemyId);
        SetField(definition, "baseHealth", 100);
        SetField(definition, "approachSpeed", 10f);
        SetField(definition, "formationRadius", formationRadius);
        SetField(
            definition,
            "combatStatSchemaVersion",
            EnemySO.CurrentCombatStatSchemaVersion);
        SetField(definition, "coreAttackDamage", coreAttackDamage);
        SetField(definition, "coreAttackInterval", attackInterval);
        SetField(definition, "coreAttackRange", coreAttackRange);
        return definition.CreateRuntime();
    }

    private static void AdvanceToFormation(DungeonBoardView board)
    {
        const int maximumSteps = 200;
        for (int step = 0; step < maximumSteps; step++)
        {
            if (AreAllFormationEnemiesArrived(board))
                return;
            board.TickEnemyAbilities(
                0.05f,
                Array.Empty<IBattleCharacter>());
        }

        Assert.Fail("Circular enemies did not reach their formation slots.");
    }

    private static bool AreAllFormationEnemiesArrived(
        DungeonBoardView board)
    {
        IDictionary states = GetCircularStates(board);
        if (states.Count == 0)
            return true;
        foreach (DictionaryEntry entry in states)
        {
            Vector2 position =
                GetMemberValue<Vector2>(entry.Value, "ResolvedPosition");
            float targetRadius =
                GetMemberValue<float>(entry.Value, "TargetRadius");
            if (Mathf.Abs(position.magnitude - targetRadius) >
                PositionTolerance)
            {
                return false;
            }
        }

        return true;
    }

    private static EnemyRuntime FindEnemyAtLayer(
        DungeonBoardView board,
        IReadOnlyList<EnemyRuntime> enemies,
        int layerIndex)
    {
        foreach (EnemyRuntime enemy in enemies)
        {
            if (GetStateInt(board, enemy, "LayerIndex") == layerIndex)
                return enemy;
        }

        Assert.Fail($"No enemy occupies layer {layerIndex}.");
        return null;
    }

    private static EnemyRuntime FindEnemyAtSectorAndLayer(
        DungeonBoardView board,
        IReadOnlyList<EnemyRuntime> enemies,
        int sectorIndex,
        int layerIndex)
    {
        foreach (EnemyRuntime enemy in enemies)
        {
            if (GetStateInt(board, enemy, "SectorIndex") == sectorIndex &&
                GetStateInt(board, enemy, "LayerIndex") == layerIndex)
            {
                return enemy;
            }
        }

        Assert.Fail(
            $"No enemy occupies sector {sectorIndex}, layer {layerIndex}.");
        return null;
    }

    private static void AssertPairwiseFormationSeparation(
        DungeonBoardView board,
        IReadOnlyList<EnemyRuntime> enemies,
        BattleArenaSetup setup)
    {
        for (int leftIndex = 0;
             leftIndex < enemies.Count;
             leftIndex++)
        {
            EnemyRuntime left = enemies[leftIndex];
            Vector2 leftPosition = GetPosition(board, left);
            for (int rightIndex = leftIndex + 1;
                 rightIndex < enemies.Count;
                 rightIndex++)
            {
                EnemyRuntime right = enemies[rightIndex];
                Vector2 rightPosition = GetPosition(board, right);
                float requiredDistance =
                    (left.FormationRadius + right.FormationRadius) *
                    setup.FormationSeparationRatio;

                Assert.That(
                    Vector2.Distance(leftPosition, rightPosition),
                    Is.GreaterThanOrEqualTo(
                        requiredDistance - PositionTolerance),
                    $"Enemies {leftIndex} and {rightIndex} overlap " +
                    "beyond the configured formation allowance.");
            }
        }
    }

    private static Vector2 GetPosition(
        DungeonBoardView board,
        EnemyRuntime enemy)
    {
        Assert.That(
            board.TryGetUnitPosition(
                BattleStatusTarget.FromEnemy(enemy),
                out Vector2 position),
            Is.True,
            enemy.Definition.name);
        return position;
    }

    private static int GetStateInt(
        DungeonBoardView board,
        EnemyRuntime enemy,
        string memberName)
    {
        return GetStateValue<int>(board, enemy, memberName);
    }

    private static float GetStateFloat(
        DungeonBoardView board,
        EnemyRuntime enemy,
        string memberName)
    {
        return GetStateValue<float>(board, enemy, memberName);
    }

    private static T GetStateValue<T>(
        DungeonBoardView board,
        EnemyRuntime enemy,
        string memberName)
    {
        IDictionary states = GetCircularStates(board);
        Assert.That(states.Contains(enemy), Is.True, enemy.Definition.name);
        object state = states[enemy];
        Assert.That(state, Is.Not.Null);

        return GetMemberValue<T>(state, memberName);
    }

    private static IDictionary GetCircularStates(DungeonBoardView board)
    {
        FieldInfo statesField = typeof(DungeonBoardView).GetField(
            "_circularEnemyStates",
            InstanceFields);
        Assert.That(statesField, Is.Not.Null);
        IDictionary states = statesField.GetValue(board) as IDictionary;
        Assert.That(states, Is.Not.Null);
        return states;
    }

    private static T GetMemberValue<T>(object target, string memberName)
    {
        Assert.That(target, Is.Not.Null);
        FieldInfo field = target.GetType().GetField(
            memberName,
            InstanceFields);
        if (field != null)
            return (T)field.GetValue(target);

        PropertyInfo property = target.GetType().GetProperty(
            memberName,
            InstanceFields);
        Assert.That(property, Is.Not.Null, memberName);
        return (T)property.GetValue(target);
    }

    private static bool ContainsReference(
        IReadOnlyList<EnemyRuntime> enemies,
        EnemyRuntime expected)
    {
        if (enemies == null)
            return false;
        foreach (EnemyRuntime enemy in enemies)
        {
            if (ReferenceEquals(enemy, expected))
                return true;
        }

        return false;
    }

    private static void SetField<T>(
        object target,
        string fieldName,
        T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            InstanceFields);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }
}
