using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyFormationRegressionTests
{
    private const int TestMaximumEnemiesPerLayer = 4;
    private const int TestLayerCount = 3;
    private const int TestMaximumActiveEnemies = 12;
    private const float TestLayerSpacing = 0.55f;
    private const float TestSeparationRatio = 0.75f;
    private const float PositionTolerance = 0.01f;
    private const int TestFormationSeed = 260714;

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
    public void CircularCapacity_UsesMaximumActiveEnemyCount()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestFormationSeed);

        Assert.That(
            board.InitialEnemyCapacity,
            Is.EqualTo(TestMaximumEnemiesPerLayer));
        Assert.That(
            board.TotalEnemyCapacity,
            Is.EqualTo(setup.MaximumActiveEnemies));

        for (int index = 0;
             index < setup.MaximumEnemyCapacity;
             index++)
        {
            Assert.That(
                board.TryAddEnemy(CreateEnemy($"capacity_{index}")),
                Is.True,
                $"Enemy {index} should occupy the natural formation.");
        }

        Assert.That(
            board.TryAddEnemy(CreateEnemy("capacity_overflow")),
            Is.False);
        Assert.That(
            board.LivingEnemyCount,
            Is.EqualTo(setup.MaximumEnemyCapacity));
    }

    [Test]
    public void SameSeed_ProducesIdenticalSpawnDirectionsPositionsAndTargets()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView firstBoard = CreateBoard(
            setup,
            TestFormationSeed);
        DungeonBoardView secondBoard = CreateBoard(
            setup,
            TestFormationSeed);
        List<EnemyRuntime> first = AddEnemies(
            firstBoard,
            setup.MaximumEnemyCapacity,
            "same_seed_first");
        List<EnemyRuntime> second = AddEnemies(
            secondBoard,
            setup.MaximumEnemyCapacity,
            "same_seed_second");

        for (int index = 0; index < first.Count; index++)
        {
            Assert.That(
                Vector2.Distance(
                    GetStateVector2(
                        firstBoard,
                        first[index],
                        "ApproachDirection"),
                    GetStateVector2(
                        secondBoard,
                        second[index],
                        "ApproachDirection")),
                Is.LessThan(0.000001f),
                $"Approach direction {index}");
            Assert.That(
                Vector2.Distance(
                    GetStateVector2(
                        firstBoard,
                        first[index],
                        "TargetPosition"),
                    GetStateVector2(
                        secondBoard,
                        second[index],
                        "TargetPosition")),
                Is.LessThan(0.000001f),
                $"Target position {index}");
            Assert.That(
                Vector2.Distance(
                    GetPosition(firstBoard, first[index]),
                    GetPosition(secondBoard, second[index])),
                Is.LessThan(0.000001f),
                $"Spawn position {index}");
        }
    }

    [Test]
    public void DifferentSeed_ChangesAtLeastOneApproachDirection()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView firstBoard = CreateBoard(setup, 1001);
        DungeonBoardView secondBoard = CreateBoard(setup, 1002);
        List<EnemyRuntime> first = AddEnemies(
            firstBoard,
            TestMaximumEnemiesPerLayer,
            "different_seed_first");
        List<EnemyRuntime> second = AddEnemies(
            secondBoard,
            TestMaximumEnemiesPerLayer,
            "different_seed_second");

        bool foundDifference = false;
        for (int index = 0; index < first.Count; index++)
        {
            Vector2 left = GetStateVector2(
                firstBoard,
                first[index],
                "ApproachDirection");
            Vector2 right = GetStateVector2(
                secondBoard,
                second[index],
                "ApproachDirection");
            foundDifference |= Vector2.Distance(left, right) > 0.001f;
        }

        Assert.That(foundDifference, Is.True);
    }

    [Test]
    public void ApproachDirections_AreContinuousRatherThanQuantizedSectors()
    {
        BattleArenaSetup setup = BattleArenaSetup.CreateCircular(
            maximumLayerCount: TestLayerCount,
            layerSpacing: TestLayerSpacing,
            formationSeparationRatio: TestSeparationRatio,
            maximumEnemiesPerLayer: 12);
        DungeonBoardView board = CreateBoard(setup, TestFormationSeed);
        List<EnemyRuntime> enemies = AddEnemies(
            board,
            12,
            "continuous_angle");

        int nonQuantizedCount = 0;
        foreach (EnemyRuntime enemy in enemies)
        {
            Vector2 direction = GetStateVector2(
                board,
                enemy,
                "ApproachDirection");
            float angle = Mathf.Repeat(
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg,
                360f);
            if (DistanceToNearestLegacySector(angle, 12) > 0.5f)
                nonQuantizedCount++;
        }

        Assert.That(
            nonQuantizedCount,
            Is.GreaterThan(0),
            "At least one approach must be outside the old fixed sectors.");
    }

    [Test]
    public void ForwardSearch_UsesAuthoredConeAndDeterministicSideBias()
    {
        Vector2 start = new(0f, 2.5f);
        Vector2 requestedEnd = new(0f, 2.4f);
        CircularEnemyFormationBody[] blockers =
        {
            new(new Vector2(0f, 1.5f), 0.25f, 0),
        };

        Vector2 evenOrder =
            CircularEnemyFormationSolver.ResolveForwardSearchEnd(
                start,
                requestedEnd,
                60f,
                0,
                0.25f,
                0f,
                1f,
                blockers);
        Vector2 oddOrder =
            CircularEnemyFormationSolver.ResolveForwardSearchEnd(
                start,
                requestedEnd,
                60f,
                1,
                0.25f,
                0f,
                1f,
                blockers);
        Vector2 narrow =
            CircularEnemyFormationSolver.ResolveForwardSearchEnd(
                start,
                requestedEnd,
                10f,
                0,
                0.25f,
                0f,
                1f,
                blockers);
        Vector2 disabled =
            CircularEnemyFormationSolver.ResolveForwardSearchEnd(
                start,
                requestedEnd,
                0f,
                0,
                0.25f,
                0f,
                1f,
                blockers);

        Assert.That(evenOrder.x, Is.GreaterThan(0.001f));
        Assert.That(oddOrder.x, Is.LessThan(-0.001f));
        Assert.That(evenOrder.y, Is.LessThan(start.y));
        Assert.That(oddOrder.y, Is.LessThan(start.y));
        Assert.That(
            Vector2.Distance(start, evenOrder),
            Is.EqualTo(0.1f).Within(0.0001f));
        Assert.That(
            Vector2.Distance(start, oddOrder),
            Is.EqualTo(0.1f).Within(0.0001f));
        Vector2 requestedMovement = requestedEnd - start;
        float narrowSteeringAngle = Vector2.Angle(
            requestedMovement,
            narrow - start);
        float wideSteeringAngle = Vector2.Angle(
            requestedMovement,
            evenOrder - start);
        Assert.That(
            narrowSteeringAngle,
            Is.LessThanOrEqualTo(5f + 0.001f),
            "A 10-degree cone must remain within five degrees per side.");
        Assert.That(
            narrowSteeringAngle,
            Is.LessThan(wideSteeringAngle),
            "The authored narrow cone must steer less than 60 degrees.");
        Assert.That(
            Vector2.Distance(disabled, requestedEnd),
            Is.LessThan(0.0001f),
            "A zero-degree cone must keep the direct path while it is open.");
    }

    [Test]
    public void RandomSpawnRing_StartsSeparatedWithoutAuthoredLayers()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestFormationSeed);
        List<EnemyRuntime> enemies = AddEnemies(
            board,
            setup.MaximumEnemyCapacity,
            "contact_depth");
        HashSet<int> radiusBuckets = new();
        foreach (EnemyRuntime enemy in enemies)
        {
            Assert.That(
                GetStateInt(board, enemy, "ContactDepth"),
                Is.Zero);
            Assert.That(GetStateEnemy(board, enemy, "Blocker"), Is.Null);
            radiusBuckets.Add(
                Mathf.RoundToInt(GetPosition(board, enemy).magnitude * 100f));
        }
        Assert.That(
            radiusBuckets.Count,
            Is.GreaterThan(1),
            "The spawn band should vary both angle and radius.");
        AssertPairwiseFormationSeparation(board, enemies, setup);
    }

    [TestCase(12)]
    [TestCase(24)]
    [TestCase(36)]
    public void MixedRadii_DoNotIllegallyOverlapWhileMovingOrAtRest(
        int enemyCount)
    {
        BattleArenaSetup setup = BattleArenaSetup.CreateCircular(
            coreMaximumHealth: 1000,
            maximumLayerCount: 3,
            layerSpacing: TestLayerSpacing,
            formationSeparationRatio: TestSeparationRatio,
            maximumEnemiesPerLayer: 12);
        DungeonBoardView board = CreateBoard(setup, TestFormationSeed);
        List<EnemyRuntime> enemies = new(enemyCount);
        float[] radii = { 0.24f, 0.35f, 0.46f };
        for (int index = 0; index < enemyCount; index++)
        {
            EnemyRuntime enemy = CreateEnemy(
                $"mixed_{enemyCount}_{index}",
                attackInterval: 1000f,
                approachSpeed: 0.35f,
                formationRadius: radii[index % radii.Length]);
            Assert.That(board.TryAddEnemy(enemy), Is.True, index.ToString());
            enemies.Add(enemy);
            AssertPairwiseFormationSeparation(
                board,
                enemies,
                setup,
                $"afterAdd={index}");
        }

        AdvanceToFormation(board, enemies, setup);
        AssertPairwiseFormationSeparation(board, enemies, setup);
    }

    [Test]
    public void LargeDelta_DoesNotTunnelThroughBlockingEnemies()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestFormationSeed);
        List<EnemyRuntime> enemies = AddEnemies(
            board,
            setup.MaximumEnemyCapacity,
            "large_delta",
            attackInterval: 1000f,
            approachSpeed: 0.1f);

        board.TickEnemyAbilities(
            100f,
            Array.Empty<IBattleCharacter>());

        AssertPairwiseFormationSeparation(board, enemies, setup);
        foreach (EnemyRuntime enemy in enemies)
        {
            EnemyRuntime blocker = GetStateEnemy(
                board,
                enemy,
                "Blocker");
            if (blocker == null)
                continue;

            Vector2 position = GetPosition(board, enemy);
            Vector2 blockerPosition = GetPosition(board, blocker);
            Assert.That(
                position.magnitude,
                Is.GreaterThanOrEqualTo(
                    blockerPosition.magnitude - PositionTolerance),
                enemy.Definition.name);
        }
    }

    [Test]
    public void RemovingClosestEnemy_DoesNotTeleportSurvivors()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestFormationSeed);
        List<EnemyRuntime> enemies = AddEnemies(
            board,
            setup.MaximumEnemyCapacity,
            "smooth_advance",
            attackInterval: 1000f,
            approachSpeed: 0.08f);
        AdvanceToFormation(board, enemies, setup);

        EnemyRuntime front = FindClosestEnemy(board, enemies);
        Dictionary<EnemyRuntime, Vector2> previousPositions = new();
        foreach (EnemyRuntime enemy in enemies)
        {
            if (ReferenceEquals(enemy, front))
                continue;
            previousPositions.Add(enemy, GetPosition(board, enemy));
        }

        int frontHealth = front.Health;
        Assert.That(
            board.TryDamageEnemy(front, frontHealth),
            Is.EqualTo(frontHealth));

        List<EnemyRuntime> survivors = GetLivingEnemies(board, enemies);
        foreach (EnemyRuntime enemy in survivors)
        {
            Assert.That(
                Vector2.Distance(
                    GetPosition(board, enemy),
                    previousPositions[enemy]),
                Is.LessThan(0.000001f),
                $"{enemy.Definition.name} teleported during compaction.");
        }

        AssertPairwiseFormationSeparation(board, survivors, setup);

        board.TickEnemyAbilities(
            0.1f,
            Array.Empty<IBattleCharacter>());

        AssertPairwiseFormationSeparation(board, survivors, setup);
    }

    [Test]
    public void CrowdedEnemies_AdvanceInwardAndStopWithoutOverlap()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestFormationSeed);
        List<EnemyRuntime> enemies = AddEnemies(
            board,
            setup.MaximumEnemyCapacity,
            "crowd_stop",
            attackInterval: 1000f,
            approachSpeed: 0.35f);
        Dictionary<EnemyRuntime, float> spawnRadii = new();
        foreach (EnemyRuntime enemy in enemies)
            spawnRadii[enemy] = GetPosition(board, enemy).magnitude;

        AdvanceToFormation(board, enemies, setup);

        bool anyAdvanced = false;
        bool anyStoppedBehindFront = false;
        float defenseRadius = GetStateFloat(
            board,
            enemies[0],
            "DefenseLineRadius");
        foreach (EnemyRuntime enemy in enemies)
        {
            float radius = GetPosition(board, enemy).magnitude;
            anyAdvanced |= radius < spawnRadii[enemy] - PositionTolerance;
            anyStoppedBehindFront |= radius >
                defenseRadius + PositionTolerance;
        }

        Assert.That(anyAdvanced, Is.True);
        Assert.That(
            anyStoppedBehindFront,
            Is.True,
            "A crowded group should naturally wait behind occupied space.");
        AssertPairwiseFormationSeparation(board, enemies, setup);
    }

    [Test]
    public void CoreRange_DefenseLineMeleeAndRearRangedAttackOnly()
    {
        BattleArenaSetup setup = CreateFormationSetup(
            coreMaximumHealth: 1000);
        DungeonBoardView board = CreateBoard(setup, TestFormationSeed);
        List<EnemyRuntime> enemies = AddEnemies(
            board,
            setup.MaximumEnemyCapacity,
            "core_range",
            coreAttackDamage: 1,
            attackInterval: 1000f);
        AdvanceToFormation(board, enemies, setup);

        List<EnemyRuntime> front = new(board.SelectDefenseLineEnemies());
        List<EnemyRuntime> rear = new();
        foreach (EnemyRuntime enemy in enemies)
        {
            if (!ContainsReference(front, enemy))
                rear.Add(enemy);
        }
        Assert.That(front.Count, Is.GreaterThan(0));
        Assert.That(rear.Count, Is.GreaterThanOrEqualTo(1));

        EnemyRuntime rearEnemy = rear[0];
        foreach (EnemyRuntime enemy in enemies)
            SetStateField(board, enemy, "AttackTimeRemaining", 1f);

        int previousCoreHealth = board.Objective.CurrentHealth;
        float rearMeleeTimer = GetStateFloat(
            board,
            rearEnemy,
            "AttackTimeRemaining");

        board.TickEnemyAbilities(1f, Array.Empty<IBattleCharacter>());

        Assert.That(
            board.Objective.CurrentHealth,
            Is.EqualTo(previousCoreHealth - front.Count));
        Assert.That(
            GetStateFloat(board, rearEnemy, "AttackTimeRemaining"),
            Is.EqualTo(rearMeleeTimer).Within(0.0001f));
        Assert.That(
            ContainsReference(
                board.SelectRecentCoreAttackers(),
                rearEnemy),
            Is.False);

        SetField(rearEnemy.Definition, "coreAttackRange", 10f);
        SetField(rearEnemy.Definition, "coreAttackDamage", 7);
        previousCoreHealth = board.Objective.CurrentHealth;
        board.TickEnemyAbilities(1f, Array.Empty<IBattleCharacter>());

        Assert.That(
            board.Objective.CurrentHealth,
            Is.EqualTo(
                previousCoreHealth - rearEnemy.CoreAttackDamage));
        Assert.That(
            ContainsReference(
                board.SelectRecentCoreAttackers(),
                rearEnemy),
            Is.True);
    }

    [Test]
    public void LegacyWorldLayerScopes_UsePhysicalProximity()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestFormationSeed);
        List<EnemyRuntime> enemies = AddEnemies(
            board,
            setup.MaximumEnemyCapacity,
            "layer_scope");

        AdvanceToFormation(board, enemies, setup);
        EnemyRuntime source = enemies[0];
        foreach (EnemyRuntime target in enemies)
        {
            if (ReferenceEquals(source, target))
                continue;
            float contactDistance = Mathf.Max(
                setup.LayerSpacing,
                (source.FormationRadius + target.FormationRadius) *
                setup.FormationSeparationRatio);
            float distance = Vector2.Distance(
                GetPosition(board, source),
                GetPosition(board, target));
            Assert.That(
                InvokeLayerScope(
                    board,
                    source,
                    target,
                    EnemyWorldLayerScope.Same),
                Is.EqualTo(distance <= contactDistance * 1.1f + 0.001f));
            Assert.That(
                InvokeLayerScope(
                    board,
                    source,
                    target,
                    EnemyWorldLayerScope.All),
                Is.True);
        }
    }

    [Test]
    public void PullTowardSharedPoint_RebuildsWithoutIllegalOverlap()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestFormationSeed);
        List<EnemyRuntime> enemies = AddEnemies(
            board,
            TestMaximumEnemiesPerLayer * 2,
            "pull",
            attackInterval: 1000f);
        AdvanceToFormation(board, enemies, setup);

        board.PullEnemiesTowardPoint(
            enemies,
            new Vector2(0.01f, 0f),
            100f);

        AssertPairwiseFormationSeparation(board, enemies, setup);
    }

    [Test]
    public void PullTowardPoint_BoundsEachMoveAndInterruptsOnlyMovedTargets()
    {
        BattleArenaSetup setup = CreateFormationSetup(
            coreMaximumHealth: 1000);
        DungeonBoardView board = CreateBoard(setup, TestFormationSeed);
        EnemyAbilityDefinition chargeAbility =
            CreateForcedMovementChargeAbility("pull_charge");
        List<EnemyRuntime> enemies = new();
        for (int index = 0; index < TestMaximumEnemiesPerLayer; index++)
        {
            EnemyRuntime enemy = CreateEnemy(
                $"bounded_pull_{index}",
                abilities: new[] { chargeAbility });
            Assert.That(board.TryAddEnemy(enemy), Is.True);
            enemies.Add(enemy);
        }
        AdvanceToFormation(board, enemies, setup);

        const float requestedDistance = 0.1f;
        FindSafePullPair(
            board,
            enemies,
            setup,
            requestedDistance,
            out EnemyRuntime stationary,
            out EnemyRuntime mover);
        Vector2 pullPoint = GetPosition(board, stationary);
        Vector2 stationaryBefore = pullPoint;
        Vector2 moverBefore = GetPosition(board, mover);
        float previousPointDistance = Vector2.Distance(
            moverBefore,
            pullPoint);
        BeginCharge(stationary);
        BeginCharge(mover);

        int changed = board.PullEnemiesTowardPoint(
            new[] { stationary, mover },
            pullPoint,
            requestedDistance);

        Vector2 stationaryAfter = GetPosition(board, stationary);
        Vector2 moverAfter = GetPosition(board, mover);
        float moverDisplacement = Vector2.Distance(
            moverBefore,
            moverAfter);
        Assert.That(changed, Is.EqualTo(1));
        Assert.That(
            Vector2.Distance(stationaryBefore, stationaryAfter),
            Is.LessThan(0.000001f));
        Assert.That(moverDisplacement, Is.GreaterThan(0f));
        Assert.That(
            moverDisplacement,
            Is.LessThanOrEqualTo(requestedDistance + PositionTolerance));
        Assert.That(
            Vector2.Distance(moverAfter, pullPoint),
            Is.LessThan(previousPointDistance));
        Assert.That(stationary.IsCharging, Is.True);
        Assert.That(mover.IsCharging, Is.False);
        AssertPairwiseFormationSeparation(board, enemies, setup);
    }

    [Test]
    public void PullAtCurrentPosition_ReturnsZeroWithoutInterruptingCharge()
    {
        BattleArenaSetup setup = CreateFormationSetup();
        DungeonBoardView board = CreateBoard(setup, TestFormationSeed);
        EnemyRuntime enemy = CreateEnemy(
            "zero_pull",
            abilities: new[]
            {
                CreateForcedMovementChargeAbility("zero_pull_charge"),
            });
        Assert.That(board.TryAddEnemy(enemy), Is.True);
        List<EnemyRuntime> enemies = new() { enemy };
        AdvanceToFormation(board, enemies, setup);
        Vector2 position = GetPosition(board, enemy);
        BeginCharge(enemy);

        int changed = board.PullEnemiesTowardPoint(
            enemies,
            position,
            1f);

        Assert.That(changed, Is.Zero);
        Assert.That(
            Vector2.Distance(GetPosition(board, enemy), position),
            Is.LessThan(0.000001f));
        Assert.That(enemy.IsCharging, Is.True);
    }

    [Test]
    public void RemovingEnemy_AdversarialSeedsRemainSeparated()
    {
        int[] seeds = { 0, 1, 7, 31, 101, 997 };
        float[] radii = { 0.2f, 0.52f, 0.31f };
        foreach (int seed in seeds)
        {
            BattleArenaSetup setup = CreateFormationSetup(
                coreMaximumHealth: 1000);
            DungeonBoardView board = CreateBoard(setup, seed);
            List<EnemyRuntime> enemies = new(
                setup.MaximumEnemyCapacity);
            for (int index = 0;
                 index < setup.MaximumEnemyCapacity;
                 index++)
            {
                EnemyRuntime enemy = CreateEnemy(
                    $"adversarial_{seed}_{index}",
                    formationRadius: radii[index % radii.Length]);
                Assert.That(board.TryAddEnemy(enemy), Is.True);
                enemies.Add(enemy);
            }
            AdvanceToFormation(board, enemies, setup);
            EnemyRuntime front = FindClosestEnemy(board, enemies);

            Assert.DoesNotThrow(() =>
                board.TryDamageEnemy(front, front.Health));

            List<EnemyRuntime> survivors = GetLivingEnemies(board, enemies);
            Assert.That(
                survivors.Count,
                Is.EqualTo(setup.MaximumEnemyCapacity - 1));
            AssertPairwiseFormationSeparation(board, survivors, setup);
        }
    }

    [Test]
    public void LegacyGrid_KeepsStackAndCoordinateSemantics()
    {
        DungeonBoardView board = CreateBoard(
            BattleArenaSetup.Legacy,
            TestFormationSeed,
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

    private static BattleArenaSetup CreateFormationSetup(
        int coreMaximumHealth = 100)
    {
        return BattleArenaSetup.CreateCircular(
            coreMaximumHealth: coreMaximumHealth,
            maximumLayerCount: TestLayerCount,
            layerSpacing: TestLayerSpacing,
            formationSeparationRatio: TestSeparationRatio,
            maximumEnemiesPerLayer: TestMaximumEnemiesPerLayer,
            maximumActiveEnemies: TestMaximumActiveEnemies);
    }

    private DungeonBoardView CreateBoard(
        BattleArenaSetup setup,
        int formationSeed,
        int stackSize = TestLayerCount)
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

        board.ConfigureArena(setup, formationSeed: formationSeed);
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
        float attackInterval = 1000f,
        float approachSpeed = 0.5f,
        float formationRadius = EnemySO.DefaultFormationRadius)
    {
        List<EnemyRuntime> result = new(count);
        for (int index = 0; index < count; index++)
        {
            EnemyRuntime enemy = CreateEnemy(
                $"{namePrefix}_{index}",
                coreAttackRange,
                coreAttackDamage,
                attackInterval,
                approachSpeed,
                formationRadius);
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
        float attackInterval = 1000f,
        float approachSpeed = 0.5f,
        float formationRadius = EnemySO.DefaultFormationRadius,
        IReadOnlyList<EnemyAbilityDefinition> abilities = null)
    {
        EnemySO definition = ScriptableObject.CreateInstance<EnemySO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.name = enemyId;
        _createdObjects.Add(definition);
        SetField(definition, "enemyId", enemyId);
        SetField(definition, "baseHealth", 100);
        SetField(definition, "approachSpeed", approachSpeed);
        SetField(definition, "formationRadius", formationRadius);
        SetField(
            definition,
            "combatStatSchemaVersion",
            EnemySO.CurrentCombatStatSchemaVersion);
        SetField(definition, "coreAttackDamage", coreAttackDamage);
        SetField(definition, "coreAttackInterval", attackInterval);
        SetField(definition, "coreAttackRange", coreAttackRange);
        if (abilities != null)
        {
            SetField(
                definition,
                "abilities",
                new List<EnemyAbilityDefinition>(abilities));
        }
        return definition.CreateRuntime();
    }

    private static EnemyAbilityDefinition
        CreateForcedMovementChargeAbility(string abilityId)
    {
        EnemyAbilityDefinition ability =
            EnemyAbilityDefinition.CreateRuntimePreset(
                abilityId,
                abilityId,
                string.Empty,
                EnemyAbilityTrigger.OnCooldown,
                new EnemyAbilityTargetDefinition(),
                Array.Empty<EnemyAbilityOperationDefinition>(),
                1000f);
        EnemyAbilityChargeDefinition charge = new();
        SetField(charge, "enabled", true);
        SetField(charge, "duration", 1000f);
        SetField(charge, "interruptible", true);
        SetField(
            charge,
            "interrupts",
            EnemyChargeInterruptFlags.ForcedMovement);
        SetField(ability, "charge", charge);
        ability.Validate();
        return ability;
    }

    private static void BeginCharge(EnemyRuntime enemy)
    {
        Assert.That(enemy, Is.Not.Null);
        Assert.That(enemy.AbilityStates, Has.Count.EqualTo(1));
        Assert.That(
            enemy.TryBeginAbilityCharge(
                enemy.AbilityStates[0],
                out _),
            Is.True);
        Assert.That(enemy.IsCharging, Is.True);
    }

    private static void FindSafePullPair(
        DungeonBoardView board,
        IReadOnlyList<EnemyRuntime> enemies,
        BattleArenaSetup setup,
        float requestedDistance,
        out EnemyRuntime stationary,
        out EnemyRuntime mover)
    {
        for (int stationaryIndex = 0;
             stationaryIndex < enemies.Count;
             stationaryIndex++)
        {
            EnemyRuntime candidateStationary = enemies[stationaryIndex];
            Vector2 point = GetPosition(board, candidateStationary);
            for (int moverIndex = 0;
                 moverIndex < enemies.Count;
                 moverIndex++)
            {
                if (moverIndex == stationaryIndex)
                    continue;
                EnemyRuntime candidateMover = enemies[moverIndex];
                Vector2 current = GetPosition(board, candidateMover);
                Vector2 requested = Vector2.MoveTowards(
                    current,
                    point,
                    requestedDistance);
                if (Vector2.Distance(current, requested) <= 0.0001f ||
                    !IsSeparatedFromEveryOther(
                        board,
                        enemies,
                        candidateMover,
                        requested,
                        setup))
                {
                    continue;
                }

                stationary = candidateStationary;
                mover = candidateMover;
                return;
            }
        }

        Assert.Fail("No collision-safe pull pair was found.");
        stationary = null;
        mover = null;
    }

    private static bool IsSeparatedFromEveryOther(
        DungeonBoardView board,
        IReadOnlyList<EnemyRuntime> enemies,
        EnemyRuntime moving,
        Vector2 requestedPosition,
        BattleArenaSetup setup)
    {
        foreach (EnemyRuntime other in enemies)
        {
            if (ReferenceEquals(other, moving))
                continue;
            float minimumDistance = Mathf.Max(
                setup.LayerSpacing,
                (moving.FormationRadius + other.FormationRadius) *
                setup.FormationSeparationRatio);
            if (Vector2.Distance(
                    requestedPosition,
                    GetPosition(board, other)) <
                minimumDistance + PositionTolerance)
            {
                return false;
            }
        }

        return true;
    }

    private static bool RebuildFormationWithDirections(
        DungeonBoardView board,
        IReadOnlyDictionary<EnemyRuntime, Vector2> directions)
    {
        MethodInfo method = typeof(DungeonBoardView).GetMethod(
            "RebuildNaturalFormationTopology",
            InstanceFields);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(
            board,
            new object[]
            {
                directions,
                false,
                true,
            });
    }

    private static void AdvanceToFormation(
        DungeonBoardView board,
        IReadOnlyList<EnemyRuntime> enemies,
        BattleArenaSetup setup)
    {
        const int maximumSteps = 400;
        const int requiredStableSteps = 5;
        int stableSteps = 0;
        for (int step = 0; step < maximumSteps; step++)
        {
            AssertPairwiseFormationSeparation(
                board,
                enemies,
                setup,
                $"advanceStep={step}");
            List<Vector2> previousPositions = new(enemies.Count);
            foreach (EnemyRuntime enemy in enemies)
            {
                previousPositions.Add(
                    board.ContainsTargetableEnemy(enemy)
                        ? GetPosition(board, enemy)
                        : Vector2.zero);
            }
            board.TickEnemyAbilities(
                0.05f,
                Array.Empty<IBattleCharacter>());
            bool moved = false;
            for (int index = 0; index < enemies.Count; index++)
            {
                EnemyRuntime enemy = enemies[index];
                if (!board.ContainsTargetableEnemy(enemy))
                    continue;
                moved |= Vector2.Distance(
                    previousPositions[index],
                    GetPosition(board, enemy)) > 0.0001f;
            }
            stableSteps = moved ? 0 : stableSteps + 1;
            if (stableSteps >= requiredStableSteps)
                return;
        }

        Assert.Fail(
            "Circular enemies did not settle into a stable crowd. " +
            DescribeUnarrivedFormationEnemies(board));
    }

    private static string DescribeUnarrivedFormationEnemies(
        DungeonBoardView board)
    {
        List<string> descriptions = new();
        foreach (DictionaryEntry entry in GetCircularStates(board))
        {
            if (entry.Key is not EnemyRuntime enemy || entry.Value == null)
                continue;

            object state = entry.Value;
            Vector2 position = GetMemberValue<Vector2>(
                state,
                "ResolvedPosition");
            Vector2 target = GetMemberValue<Vector2>(
                state,
                "TargetPosition");
            float remaining = Vector2.Distance(position, target);
            if (remaining <= PositionTolerance)
                continue;

            EnemyRuntime blocker = GetMemberValue<EnemyRuntime>(
                state,
                "Blocker");
            descriptions.Add(
                $"id={ResolveEnemyDebugId(enemy)}, " +
                $"current={position.ToString("F4")}, " +
                $"target={target.ToString("F4")}, " +
                $"remaining={remaining:F4}, " +
                $"depth={GetMemberValue<int>(state, "ContactDepth")}, " +
                $"blocker={ResolveEnemyDebugId(blocker)}, " +
                "potentialBlockers=" +
                $"{GetStateCollectionCount(state, "PotentialBlockers")}, " +
                "collisionNeighbors=" +
                $"{GetStateCollectionCount(state, "CollisionNeighbors")}");
        }

        return descriptions.Count > 0
            ? "Stuck enemies: " + string.Join(" | ", descriptions)
            : "No unarrived enemy state was found.";
    }

    private static int GetStateCollectionCount(
        object state,
        string memberName)
    {
        object value = GetMemberValue<object>(state, memberName);
        return value is ICollection collection
            ? collection.Count
            : -1;
    }

    private static string ResolveEnemyDebugId(EnemyRuntime enemy)
    {
        if (enemy?.Definition == null)
            return "none";

        return !string.IsNullOrWhiteSpace(enemy.Definition.EnemyId)
            ? enemy.Definition.EnemyId
            : enemy.Definition.name;
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
            Vector2 target =
                GetMemberValue<Vector2>(entry.Value, "TargetPosition");
            if (Vector2.Distance(position, target) > PositionTolerance)
                return false;
        }

        return true;
    }

    private static EnemyRuntime FindFrontWithDescendant(
        DungeonBoardView board,
        IReadOnlyList<EnemyRuntime> enemies)
    {
        foreach (EnemyRuntime candidate in enemies)
        {
            if (GetStateInt(board, candidate, "ContactDepth") != 0)
                continue;
            foreach (EnemyRuntime enemy in enemies)
            {
                if (ReferenceEquals(
                        GetStateEnemy(board, enemy, "Blocker"),
                        candidate))
                {
                    return candidate;
                }
            }
        }

        Assert.Fail("No defense-line enemy has a blocking descendant.");
        return null;
    }

    private static EnemyRuntime FindClosestEnemy(
        DungeonBoardView board,
        IReadOnlyList<EnemyRuntime> enemies)
    {
        EnemyRuntime result = null;
        float closestRadius = float.PositiveInfinity;
        foreach (EnemyRuntime enemy in enemies)
        {
            if (enemy == null || !board.ContainsTargetableEnemy(enemy))
                continue;
            float radius = GetPosition(board, enemy).magnitude;
            if (radius < closestRadius)
            {
                closestRadius = radius;
                result = enemy;
            }
        }

        Assert.That(result, Is.Not.Null);
        return result;
    }

    private static List<EnemyRuntime> FindEnemiesAtDepth(
        DungeonBoardView board,
        IReadOnlyList<EnemyRuntime> enemies,
        int contactDepth)
    {
        List<EnemyRuntime> result = new();
        foreach (EnemyRuntime enemy in enemies)
        {
            if (GetStateInt(board, enemy, "ContactDepth") == contactDepth)
                result.Add(enemy);
        }
        return result;
    }

    private static List<EnemyRuntime> GetLivingEnemies(
        DungeonBoardView board,
        IReadOnlyList<EnemyRuntime> enemies)
    {
        List<EnemyRuntime> result = new();
        foreach (EnemyRuntime enemy in enemies)
        {
            if (board.ContainsTargetableEnemy(enemy))
                result.Add(enemy);
        }
        return result;
    }

    private static void AssertDepthBounds(
        DungeonBoardView board,
        IReadOnlyList<EnemyRuntime> enemies,
        BattleArenaSetup setup)
    {
        int[] counts = new int[setup.MaximumLayerCount];
        foreach (EnemyRuntime enemy in enemies)
        {
            int depth = GetStateInt(board, enemy, "ContactDepth");
            Assert.That(
                depth,
                Is.InRange(0, setup.MaximumLayerCount - 1));
            EnemyRuntime blocker = GetStateEnemy(board, enemy, "Blocker");
            if (depth > 0)
            {
                Assert.That(
                    blocker,
                    Is.Not.Null,
                    $"{enemy.Definition.name} has no live blocker at " +
                    $"contact depth {depth}.");
                Assert.That(
                    GetCircularStates(board).Contains(blocker),
                    Is.True,
                    $"{enemy.Definition.name} references a removed blocker.");
            }
            counts[depth]++;
        }

        foreach (int count in counts)
        {
            Assert.That(
                count,
                Is.LessThanOrEqualTo(setup.MaximumEnemiesPerLayer));
        }
    }

    private static void AssertPairwiseFormationSeparation(
        DungeonBoardView board,
        IReadOnlyList<EnemyRuntime> enemies,
        BattleArenaSetup setup,
        string context = null)
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
                float requiredDistance = Mathf.Max(
                    setup.LayerSpacing,
                    (left.FormationRadius + right.FormationRadius) *
                    setup.FormationSeparationRatio);

                Assert.That(
                    Vector2.Distance(leftPosition, rightPosition),
                    Is.GreaterThanOrEqualTo(
                        requiredDistance - PositionTolerance),
                    $"Enemies {left.Definition.name} and " +
                    $"{right.Definition.name} overlap beyond the " +
                    "configured allowance. " +
                    $"Context={context ?? "direct"}. " +
                    $"Left[{DescribeFormationState(board, left)}] " +
                    $"Right[{DescribeFormationState(board, right)}]");
            }
        }
    }

    private static string DescribeFormationState(
        DungeonBoardView board,
        EnemyRuntime enemy)
    {
        Vector2 position = GetPosition(board, enemy);
        Vector2 target = GetStateVector2(
            board,
            enemy,
            "TargetPosition");
        Vector2 direction = GetStateVector2(
            board,
            enemy,
            "ApproachDirection");
        int depth = GetStateInt(board, enemy, "ContactDepth");
        EnemyRuntime blocker = GetStateEnemy(board, enemy, "Blocker");
        IDictionary states = GetCircularStates(board);
        bool blockerIsLive = blocker != null && states.Contains(blocker);
        int blockerOrder = blockerIsLive
            ? GetStateInt(board, blocker, "StableOrder")
            : -1;
        return $"position={position.ToString("F4")}, " +
               $"target={target.ToString("F4")}, " +
               $"direction={direction.ToString("F4")}, " +
               $"depth={depth}, blockerOrder={blockerOrder}, " +
               $"blockerLive={blockerIsLive}";
    }

    private static float DistanceToNearestLegacySector(
        float angle,
        int sectorCount)
    {
        float step = 360f / Mathf.Max(1, sectorCount);
        float offset = Mathf.Repeat(angle - step * 0.5f, step);
        return Mathf.Min(offset, step - offset);
    }

    private static bool InvokeLayerScope(
        DungeonBoardView board,
        EnemyRuntime source,
        EnemyRuntime target,
        EnemyWorldLayerScope scope)
    {
        MethodInfo method = typeof(DungeonBoardView).GetMethod(
            "IsWithinWorldLayerScope",
            InstanceFields);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(
            board,
            new object[] { source, target, scope });
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

    private static int GetBlockerStableOrder(
        DungeonBoardView board,
        EnemyRuntime enemy)
    {
        EnemyRuntime blocker = GetStateEnemy(
            board,
            enemy,
            "Blocker");
        return blocker != null
            ? GetStateInt(board, blocker, "StableOrder")
            : -1;
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

    private static Vector2 GetStateVector2(
        DungeonBoardView board,
        EnemyRuntime enemy,
        string memberName)
    {
        return GetStateValue<Vector2>(board, enemy, memberName);
    }

    private static EnemyRuntime GetStateEnemy(
        DungeonBoardView board,
        EnemyRuntime enemy,
        string memberName)
    {
        return GetStateValue<EnemyRuntime>(board, enemy, memberName);
    }

    private static T GetStateValue<T>(
        DungeonBoardView board,
        EnemyRuntime enemy,
        string memberName)
    {
        object state = GetState(board, enemy);
        return GetMemberValue<T>(state, memberName);
    }

    private static object GetState(
        DungeonBoardView board,
        EnemyRuntime enemy)
    {
        IDictionary states = GetCircularStates(board);
        Assert.That(states.Contains(enemy), Is.True, enemy.Definition.name);
        object state = states[enemy];
        Assert.That(state, Is.Not.Null);
        return state;
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

    private static void SetStateField<T>(
        DungeonBoardView board,
        EnemyRuntime enemy,
        string fieldName,
        T value)
    {
        SetField(GetState(board, enemy), fieldName, value);
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
