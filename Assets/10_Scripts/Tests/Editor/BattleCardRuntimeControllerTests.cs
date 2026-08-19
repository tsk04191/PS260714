using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleCardRuntimeControllerTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new();
    private readonly List<BattleCardRuntimeController> _controllers = new();
    private int _nextCardId;

    [TearDown]
    public void TearDown()
    {
        foreach (BattleCardRuntimeController controller in _controllers)
            controller?.Clear();
        _controllers.Clear();

        for (int index = _createdObjects.Count - 1; index >= 0; index--)
        {
            if (_createdObjects[index] != null)
                UnityEngine.Object.DestroyImmediate(_createdObjects[index]);
        }
        _createdObjects.Clear();
    }

    [Test]
    public void OrderedOperations_UsePreviousCountAndSkippedFailureResetsIt()
    {
        BattleCardOperationDefinition restore = Operation(
            BattleCardOperationType.ObjectiveRestore,
            BattleCardTargetScope.None,
            amount: 10);
        BattleCardOperationDefinition gainFromPrevious = Operation(
            BattleCardOperationType.GainEnergy,
            BattleCardTargetScope.None,
            amount: 2,
            usePreviousChangedCount: true);
        BattleCardOperationDefinition skipped = Operation(
            BattleCardOperationType.GainEnergy,
            BattleCardTargetScope.None,
            amount: 50,
            condition: Condition(
                BattleCardConditionType.PreviousOperationFailed));
        BattleCardOperationDefinition gainAfterSkip = Operation(
            BattleCardOperationType.GainEnergy,
            BattleCardTargetScope.None,
            amount: 3,
            condition: Condition(
                BattleCardConditionType.PreviousOperationFailed));
        BattleCardSO card = Card(
            0,
            restore,
            gainFromPrevious,
            skipped,
            gainAfterSkip);
        BattleCoreRuntime objective = CreateObjective(100, 50);
        FakeSpatialService spatial = new();
        FakeBoard board = new(objective, spatial);
        TestResource resource = new(0, 100);
        BattleCardDeckRuntime deck = Deck(card);
        BattleCardRuntimeController controller = Controller(
            board,
            resource,
            deck,
            Array.Empty<IBattleCharacter>());

        Assert.That(
            controller.TryBeginExecution(
                Find(deck, card),
                null,
                null,
                null),
            Is.True);

        Assert.That(objective.CurrentHealth, Is.EqualTo(60));
        Assert.That(
            resource.Current,
            Is.EqualTo(15),
            "10 restored + operation amount 2, then 3 after skipped " +
            "condition must be applied in order.");
        Assert.That(controller.IsExecutionPending, Is.False);
    }

    [Test]
    public void ObjectiveOperations_RestoreImmunityAndRedirectInOrder()
    {
        CharacterRuntime redirectTarget = RuntimeCharacter(null, "redirect");
        BattleCardSO card = Card(
            0,
            Operation(
                BattleCardOperationType.ObjectiveRestore,
                BattleCardTargetScope.None,
                amount: 20),
            Operation(
                BattleCardOperationType.ObjectiveInvulnerability,
                BattleCardTargetScope.None,
                duration: 3f),
            Operation(
                BattleCardOperationType.ObjectiveDamageRedirect,
                BattleCardTargetScope.AllAllies,
                ratio: 0.3f));
        BattleCoreRuntime objective = CreateObjective(100, 40);
        FakeBoard board = new(objective, new FakeSpatialService());
        TestResource resource = new(10, 10);
        BattleCardDeckRuntime deck = Deck(card);
        BattleCardRuntimeController controller = Controller(
            board,
            resource,
            deck,
            new IBattleCharacter[] { redirectTarget });

        Assert.That(
            controller.TryBeginExecution(
                Find(deck, card),
                null,
                null,
                null),
            Is.True);

        Assert.That(objective.CurrentHealth, Is.EqualTo(60));
        Assert.That(objective.IsDamageImmune, Is.True);
        Assert.That(
            objective.DamageImmunityRemaining,
            Is.EqualTo(3f).Within(0.001f));
        Assert.That(objective.HasPendingDamageRedirect, Is.True);
        Assert.That(
            objective.PendingDamageRedirectTarget,
            Is.SameAs(redirectTarget));
        Assert.That(
            objective.PendingDamageRedirectRatio,
            Is.EqualTo(0.3f).Within(0.001f));
    }

    [Test]
    public void SpatialScopes_NearbyAndBehindForwardExactTargets()
    {
        EnemyRuntime primary = Enemy();
        EnemyRuntime nearbyFirst = Enemy();
        EnemyRuntime nearbySecond = Enemy();
        EnemyRuntime behind = Enemy();
        FakeSpatialService spatial = new()
        {
            NearbyTargets = new[] { nearbyFirst, nearbySecond },
            BehindTargets = new[] { behind },
        };
        FakeBoard board = new(CreateObjective(), spatial);
        board.AllEnemies.AddRange(new[]
        {
            primary,
            nearbyFirst,
            nearbySecond,
            behind,
        });
        BattleCardSO card = Card(
            0,
            Operation(
                BattleCardOperationType.PullEnemies,
                BattleCardTargetScope.NearbyPrimaryEnemies,
                count: 0,
                radius: 0.75f),
            Operation(
                BattleCardOperationType.PullEnemies,
                BattleCardTargetScope.BehindPrimaryEnemy,
                radius: 0.5f));
        TestResource resource = new(10, 10);
        BattleCardDeckRuntime deck = Deck(card);
        BattleCardRuntimeController controller = Controller(
            board,
            resource,
            deck,
            Array.Empty<IBattleCharacter>());

        Assert.That(
            controller.TryBeginExecution(
                Find(deck, card),
                null,
                new[] { primary },
                null,
                primaryPoint: Vector2.zero,
                hasPrimaryPoint: true),
            Is.True);

        Assert.That(spatial.NearbyAnchor.Enemy, Is.SameAs(primary));
        Assert.That(spatial.BehindAnchor, Is.SameAs(primary));
        Assert.That(spatial.PullTargetSnapshots, Has.Count.EqualTo(2));
        Assert.That(
            spatial.PullTargetSnapshots[0],
            Is.EqualTo(new[] { nearbyFirst, nearbySecond }));
        Assert.That(
            spatial.PullTargetSnapshots[1],
            Is.EqualTo(new[] { behind }));
    }

    [Test]
    public void EnemiesAtDesignatedPoint_UsesOperationRadius()
    {
        EnemyRuntime insideDefaultRadius = Enemy();
        EnemyRuntime insideAuthoredRadius = Enemy();
        EnemyRuntime outsideAuthoredRadius = Enemy();
        FakeSpatialService spatial = new();
        spatial.SetPosition(insideDefaultRadius, new Vector2(1f, 0f));
        spatial.SetPosition(insideAuthoredRadius, new Vector2(1.75f, 0f));
        spatial.SetPosition(outsideAuthoredRadius, new Vector2(2.25f, 0f));
        FakeBoard board = new(CreateObjective(), spatial);
        board.AllEnemies.AddRange(new[]
        {
            insideDefaultRadius,
            insideAuthoredRadius,
            outsideAuthoredRadius,
        });
        BattleCardSO card = Card(
            0,
            Operation(
                BattleCardOperationType.PullEnemies,
                BattleCardTargetScope.EnemiesAtDesignatedPoint,
                radius: 2f));
        BattleCardDeckRuntime deck = Deck(card);
        BattleCardRuntimeController controller = Controller(
            board,
            new TestResource(10, 10),
            deck,
            Array.Empty<IBattleCharacter>());

        Assert.That(
            controller.TryBeginExecution(
                Find(deck, card),
                null,
                null,
                null,
                primaryPoint: Vector2.zero,
                hasPrimaryPoint: true),
            Is.True);

        Assert.That(spatial.PullTargetSnapshots, Has.Count.EqualTo(1));
        Assert.That(
            spatial.PullTargetSnapshots[0],
            Is.EqualTo(new[]
            {
                insideDefaultRadius,
                insideAuthoredRadius,
            }));
    }

    [Test]
    public void AttackModifier_ConsumesOncePerActionExecution()
    {
        CharacterRuntime source = RuntimeCharacter(null, "modifier_source");
        EnemyRuntime target = Enemy();
        BattleCardSO card = Card(
            0,
            Operation(
                BattleCardOperationType.ApplyAttackModifier,
                BattleCardTargetScope.AllAllies,
                amount: 12,
                count: 2));
        FakeBoard board = new(CreateObjective(), new FakeSpatialService());
        board.AllEnemies.Add(target);
        BattleCardDeckRuntime deck = Deck(card);
        BattleCardRuntimeController controller = Controller(
            board,
            new TestResource(10, 10),
            deck,
            new IBattleCharacter[] { source });
        Assert.That(
            controller.TryBeginExecution(
                Find(deck, card),
                source,
                null,
                null),
            Is.True);

        CharacterEffectDefinition damage =
            CharacterEffectDefinition.CreateFixedRuntimeEffect(
                CharacterEffectType.Damage,
                10f);
        EffectContext firstAction = new(
            source,
            board,
            null,
            CharacterActionKind.Attack,
            CharacterTargetFaction.Enemy,
            new[] { target },
            null,
            source.CurrentAttackPower);
        BattleEffectContext firstEffect =
            BattleEffectContext.FromCharacter(firstAction);
        BattleEffectContext secondEffect =
            BattleEffectContext.FromCharacter(
                firstAction.WithSourceAttackPower(
                    source.CurrentAttackPower + 1f));
        Assert.That(
            secondEffect.ActionExecutionId,
            Is.EqualTo(firstEffect.ActionExecutionId));

        RaiseResolved(controller, firstEffect, damage, 10);
        RaiseResolved(controller, secondEffect, damage, 10);
        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(1));

        EffectContext secondAction = new(
            source,
            board,
            null,
            CharacterActionKind.Attack,
            CharacterTargetFaction.Enemy,
            new[] { target },
            null,
            source.CurrentAttackPower);
        RaiseResolved(
            controller,
            BattleEffectContext.FromCharacter(secondAction),
            damage,
            10);
        RaiseResolved(
            controller,
            BattleEffectContext.FromCharacter(new EffectContext(
                source,
                board,
                null,
                CharacterActionKind.Attack,
                CharacterTargetFaction.Enemy,
                new[] { target },
                null,
                source.CurrentAttackPower)),
            damage,
            10);

        Assert.That(
            board.DamageTargetSnapshots,
            Has.Count.EqualTo(2),
            "A two-use modifier must apply once to each of two attacks, " +
            "not twice to multiple effects in the first attack.");
    }

    [Test]
    public void ReadyBasicAttack_OnlyClearsEligibleAlliedCooldowns()
    {
        CharacterRuntime waiting = RuntimeCharacter(null, "ready_waiting");
        CharacterRuntime recovering = RuntimeCharacter(
            null,
            "ready_recovering");
        SetField(waiting, "_remainingCooldown", 2f);
        SetField(recovering, "_remainingCooldown", 2f);
        SetField(recovering, "_attackRecoveryRemaining", 0.5f);
        BattleCardSO card = Card(
            1,
            Operation(
                BattleCardOperationType.ReadyBasicAttack,
                BattleCardTargetScope.Primary));
        SetField(card, "targetFaction", CharacterTargetFaction.Ally);
        BattleCardDeckRuntime deck = Deck(card);
        TestResource resource = new(10, 10);
        BattleCardRuntimeController controller = Controller(
            new FakeBoard(CreateObjective(), new FakeSpatialService()),
            resource,
            deck,
            new IBattleCharacter[] { waiting, recovering });

        Assert.That(
            controller.TryBeginExecution(
                Find(deck, card),
                waiting,
                null,
                new IBattleCharacter[] { waiting, recovering }),
            Is.True);

        Assert.That(GetField<float>(waiting, "_remainingCooldown"), Is.Zero);
        Assert.That(
            GetField<float>(recovering, "_remainingCooldown"),
            Is.EqualTo(2f));
        Assert.That(resource.Current, Is.EqualTo(9));
        Assert.That(deck.DiscardPile, Has.Count.EqualTo(1));
    }

    [Test]
    public void StatusStackModifier_CoversEveryEffectInOneSkillAndConsumesOnce()
    {
        CharacterRoleSO role = Create<CharacterRoleSO>();
        CharacterRuntime source = RuntimeCharacter(role, "status_modifier");
        BattleCardOperationDefinition modifier = Operation(
            BattleCardOperationType.ApplySkillModifier,
            BattleCardTargetScope.AllAllies,
            amount: 1,
            count: 1);
        SetField(modifier, "requiredRole", role);
        BattleCardSO card = Card(0, modifier);
        FakeBoard board = new(CreateObjective(), new FakeSpatialService());
        BattleCardDeckRuntime deck = Deck(card);
        BattleCardRuntimeController controller = Controller(
            board,
            new TestResource(10, 10),
            deck,
            new IBattleCharacter[] { source });
        Assert.That(
            controller.TryBeginExecution(
                Find(deck, card),
                source,
                null,
                null),
            Is.True);

        StatusEffectSO status = Create<StatusEffectSO>();
        CharacterEffectDefinition applyStatus = new();
        SetField(applyStatus, "type", CharacterEffectType.ApplyStatus);
        SetField(applyStatus, "statusEffect", status);
        SetField(applyStatus, "statusDuration", 1f);
        SetField(applyStatus, "statusStacks", 1f);
        EffectContext action = new(
            source,
            board,
            null,
            CharacterActionKind.Skill,
            CharacterTargetFaction.Ally,
            null,
            new IBattleCharacter[] { source },
            source.CurrentAttackPower);
        BattleEffectContext first = BattleEffectContext.FromCharacter(action);

        Assert.That(
            controller.Resolve(
                first.User,
                first.OriginKind,
                first.ActionExecutionId,
                applyStatus,
                BattleAbilityModifierValueKind.StatusStacks,
                1f),
            Is.EqualTo(2f));
        RaiseResolved(controller, first, applyStatus, 0);

        BattleEffectContext second = BattleEffectContext.FromCharacter(
            action.WithSourceAttackPower(source.CurrentAttackPower + 1f));
        Assert.That(
            controller.Resolve(
                second.User,
                second.OriginKind,
                second.ActionExecutionId,
                applyStatus,
                BattleAbilityModifierValueKind.StatusStacks,
                1f),
            Is.EqualTo(2f));

        BattleEffectContext nextAction = BattleEffectContext.FromCharacter(
            new EffectContext(
                source,
                board,
                null,
                CharacterActionKind.Skill,
                CharacterTargetFaction.Ally,
                null,
                new IBattleCharacter[] { source },
                source.CurrentAttackPower));
        Assert.That(
            controller.Resolve(
                nextAction.User,
                nextAction.OriginKind,
                nextAction.ActionExecutionId,
                applyStatus,
                BattleAbilityModifierValueKind.StatusStacks,
                1f),
            Is.EqualTo(1f));
    }

    [Test]
    public void RepeatAttack_UsesResolvedPowerBeforeIncomingProtection()
    {
        CharacterRuntime source = RuntimeCharacter(null, "repeat_source");
        EnemyRuntime target = Enemy();
        BattleCardSO card = Card(
            0,
            Operation(
                BattleCardOperationType.ApplyAttackModifier,
                BattleCardTargetScope.AllAllies,
                amount: 0,
                count: 1,
                ratio: 0.5f));
        FakeBoard board = new(CreateObjective(), new FakeSpatialService());
        board.AllEnemies.Add(target);
        BattleCardDeckRuntime deck = Deck(card);
        BattleCardRuntimeController controller = Controller(
            board,
            new TestResource(10, 10),
            deck,
            new IBattleCharacter[] { source });
        Assert.That(
            controller.TryBeginExecution(
                Find(deck, card),
                source,
                null,
                null),
            Is.True);

        CharacterEffectDefinition damage =
            CharacterEffectDefinition.CreateFixedRuntimeEffect(
                CharacterEffectType.Damage,
                10f);
        BattleEffectContext context = BattleEffectContext.FromCharacter(
            new EffectContext(
                source,
                board,
                null,
                CharacterActionKind.Attack,
                CharacterTargetFaction.Enemy,
                new[] { target },
                null,
                source.CurrentAttackPower));
        RaiseResolved(
            controller,
            context,
            damage,
            damageDealt: 5,
            resolvedAmount: 10);

        Assert.That(board.DamageAmounts, Is.EqualTo(new[] { 5 }));
    }

    [Test]
    public void AlliesWithRole_FiltersBeforeObjectiveRedirect()
    {
        CharacterRoleSO requiredRole = Create<CharacterRoleSO>();
        CharacterRoleSO otherRole = Create<CharacterRoleSO>();
        CharacterRuntime required = RuntimeCharacter(requiredRole, "required");
        CharacterRuntime other = RuntimeCharacter(otherRole, "other");
        BattleCardOperationDefinition redirect = Operation(
            BattleCardOperationType.ObjectiveDamageRedirect,
            BattleCardTargetScope.AlliesWithRole,
            ratio: 0.4f);
        SetField(redirect, "requiredRole", requiredRole);
        BattleCardSO card = Card(0, redirect);
        SetField(card, "targetFaction", CharacterTargetFaction.Ally);
        BattleCoreRuntime objective = CreateObjective();
        FakeBoard board = new(objective, new FakeSpatialService());
        BattleCardDeckRuntime deck = Deck(card);
        BattleCardRuntimeController controller = Controller(
            board,
            new TestResource(10, 10),
            deck,
            new IBattleCharacter[] { other, required });

        Assert.That(
            controller.TryBeginExecution(
                Find(deck, card),
                null,
                null,
                null),
            Is.True);

        Assert.That(
            objective.PendingDamageRedirectTarget,
            Is.SameAs(required));
        Assert.That(
            objective.PendingDamageRedirectRatio,
            Is.EqualTo(0.4f).Within(0.001f));
    }

    [Test]
    public void CostModifier_ExcludesGrantingCardAndConsumesOnNextPlay()
    {
        BattleCardOperationDefinition modifier = Operation(
            BattleCardOperationType.ModifyCardCost,
            BattleCardTargetScope.None,
            amount: 2,
            count: 1);
        SetField(
            modifier,
            "costModifierMode",
            BattleCardCostModifierMode.Add);
        BattleCardSO grant = Card(0, modifier);
        BattleCardSO first = Card(
            5,
            Operation(
                BattleCardOperationType.ProtectHand,
                BattleCardTargetScope.None));
        BattleCardSO second = Card(
            5,
            Operation(
                BattleCardOperationType.ProtectHand,
                BattleCardTargetScope.None));
        BattleCardDeckRuntime deck = Deck(grant, first, second);
        TestResource resource = new(20, 20);
        BattleCardRuntimeController controller = Controller(
            new FakeBoard(CreateObjective(), new FakeSpatialService()),
            resource,
            deck,
            Array.Empty<IBattleCharacter>());

        Assert.That(
            controller.TryBeginExecution(
                Find(deck, grant),
                null,
                null,
                null),
            Is.True);
        BattleCardInstance firstInstance = Find(deck, first);
        BattleCardInstance secondInstance = Find(deck, second);
        Assert.That(deck.ActiveCostModifierCount, Is.EqualTo(1));
        Assert.That(deck.GetEffectiveCost(firstInstance), Is.EqualTo(3));

        Assert.That(
            controller.TryBeginExecution(
                firstInstance,
                null,
                null,
                null),
            Is.True);

        Assert.That(resource.Current, Is.EqualTo(17));
        Assert.That(deck.ActiveCostModifierCount, Is.Zero);
        Assert.That(deck.GetEffectiveCost(secondInstance), Is.EqualTo(5));
    }

    [Test]
    public void Selection_PausesDeckAndConfirmResumesOrderedExecution()
    {
        BattleCardSO candidate = Card(0);
        BattleCardSO resolving = Card(
            3,
            SelectionOperation(BattleCardOperationType.DiscardSelected),
            Operation(
                BattleCardOperationType.GainEnergy,
                BattleCardTargetScope.None,
                amount: 4));
        BattleCardDeckRuntime deck = Deck(resolving, candidate);
        BattleCardInstance resolvingInstance = Find(deck, resolving);
        BattleCardInstance candidateInstance = Find(deck, candidate);
        TestResource resource = new(10, 20);
        BattleCardRuntimeController controller = Controller(
            new FakeBoard(CreateObjective(), new FakeSpatialService()),
            resource,
            deck,
            Array.Empty<IBattleCharacter>());

        Assert.That(
            controller.TryBeginExecution(
                resolvingInstance,
                null,
                null,
                null),
            Is.True);
        Assert.That(resource.Current, Is.EqualTo(7));
        Assert.That(controller.IsExecutionPending, Is.True);
        Assert.That(controller.IsCardSelectionPending, Is.True);
        float cooldown = deck.CooldownRemaining;

        deck.Tick(cooldown + 10f);

        Assert.That(deck.CooldownRemaining, Is.EqualTo(cooldown));
        Assert.That(controller.TryToggleCardSelection(candidateInstance), Is.True);
        Assert.That(controller.TryConfirmCardSelection(), Is.True);
        Assert.That(controller.IsExecutionPending, Is.False);
        Assert.That(deck.IsZoneSelectionPending, Is.False);
        Assert.That(resource.Current, Is.EqualTo(11));
        Assert.That(deck.Hand, Is.Empty);
        Assert.That(deck.DiscardPile, Has.Count.EqualTo(2));
    }

    [Test]
    public void Selection_CancelRefundsCostAndKeepsHandUnchanged()
    {
        BattleCardSO candidate = Card(0);
        BattleCardSO resolving = Card(
            3,
            SelectionOperation(BattleCardOperationType.ExhaustSelected));
        BattleCardDeckRuntime deck = Deck(resolving, candidate);
        TestResource resource = new(10, 20);
        BattleCardRuntimeController controller = Controller(
            new FakeBoard(CreateObjective(), new FakeSpatialService()),
            resource,
            deck,
            Array.Empty<IBattleCharacter>());

        Assert.That(
            controller.TryBeginExecution(
                Find(deck, resolving),
                null,
                null,
                null),
            Is.True);
        Assert.That(resource.Current, Is.EqualTo(7));

        Assert.That(controller.CancelPendingExecution(), Is.True);

        Assert.That(resource.Current, Is.EqualTo(10));
        Assert.That(controller.IsExecutionPending, Is.False);
        Assert.That(deck.IsZoneSelectionPending, Is.False);
        Assert.That(deck.Hand, Has.Count.EqualTo(2));
        Assert.That(deck.DiscardPile, Is.Empty);
        Assert.That(deck.ExhaustPile, Is.Empty);
    }

    [Test]
    public void TimedZone_TriggersOnReentryAndOnlyOncePerTarget()
    {
        EnemyRuntime first = Enemy();
        EnemyRuntime second = Enemy();
        FakeSpatialService spatial = new();
        spatial.SetPosition(first, new Vector2(0.25f, 0f));
        spatial.SetPosition(second, new Vector2(2f, 0f));
        FakeBoard board = new(CreateObjective(), spatial);
        board.AllEnemies.AddRange(new[] { first, second });
        CharacterEffectDefinition damage =
            CharacterEffectDefinition.CreateFixedRuntimeEffect(
                CharacterEffectType.Damage,
                1f);
        BattleCardOperationDefinition zone = Operation(
            BattleCardOperationType.CreateZone,
            BattleCardTargetScope.None,
            duration: 5f,
            radius: 1f);
        SetField(zone, "zoneTrigger", BattleCardZoneTrigger.OnEnemyEnter);
        SetField(zone, "oncePerTarget", true);
        SetField(zone, "sharedEffect", damage);
        BattleCardSO card = Card(0, zone);
        BattleCardDeckRuntime deck = Deck(card);
        BattleCardRuntimeController controller = Controller(
            board,
            new TestResource(10, 10),
            deck,
            Array.Empty<IBattleCharacter>());

        Assert.That(
            controller.TryBeginExecution(
                Find(deck, card),
                null,
                null,
                null,
                primaryPoint: Vector2.zero,
                hasPrimaryPoint: true),
            Is.True);

        controller.Tick(0.1f);
        Assert.That(
            board.DamageTargetSnapshots,
            Is.Empty,
            "An enemy already inside when the zone is created is not an " +
            "entrant yet.");

        spatial.SetPosition(first, new Vector2(2f, 0f));
        controller.Tick(0.1f);
        spatial.SetPosition(first, new Vector2(0.25f, 0f));
        controller.Tick(0.1f);
        controller.Tick(0.1f);
        spatial.SetPosition(first, new Vector2(2f, 0f));
        controller.Tick(0.1f);
        spatial.SetPosition(first, new Vector2(0.25f, 0f));
        controller.Tick(0.1f);
        spatial.SetPosition(second, new Vector2(0.25f, 0f));
        controller.Tick(0.1f);

        Assert.That(board.DamageTargetSnapshots, Has.Count.EqualTo(2));
        Assert.That(
            board.DamageTargetSnapshots[0],
            Is.EqualTo(new[] { first }));
        Assert.That(
            board.DamageTargetSnapshots[1],
            Is.EqualTo(new[] { second }));
    }

    private BattleCardRuntimeController Controller(
        FakeBoard board,
        TestResource resource,
        BattleCardDeckRuntime deck,
        IReadOnlyList<IBattleCharacter> allies)
    {
        BattleCardRuntimeController controller = new();
        controller.Bind(board, resource, deck, allies);
        _controllers.Add(controller);
        return controller;
    }

    private BattleCoreRuntime CreateObjective(
        int maximumHealth = 100,
        int currentHealth = 100)
    {
        BattleCoreRuntime objective = new();
        objective.Configure(maximumHealth, true, currentHealth);
        return objective;
    }

    private BattleCardDeckRuntime Deck(params BattleCardSO[] cards)
    {
        BattleCardDeckRuntime deck = new();
        Assert.That(
            deck.ConfigureResolvedDeck(
                new BattleCardDeckRules(),
                cards,
                260714,
                cards.Length,
                100f,
                100f),
            Is.True);
        Assert.That(deck.BeginBattle(), Is.True);
        Assert.That(deck.Hand, Has.Count.EqualTo(cards.Length));
        return deck;
    }

    private static BattleCardInstance Find(
        BattleCardDeckRuntime deck,
        BattleCardSO definition)
    {
        foreach (BattleCardInstance instance in deck.Hand)
        {
            if (ReferenceEquals(instance.Definition, definition))
                return instance;
        }
        Assert.Fail($"Card '{definition?.name}' was not drawn.");
        return null;
    }

    private BattleCardSO Card(
        int energyCost,
        params BattleCardOperationDefinition[] operations)
    {
        BattleCardSO card = Create<BattleCardSO>();
        card.name = $"RuntimeControllerCard{_nextCardId}";
        SetField(card, "cardId", $"test.runtime.{_nextCardId++}");
        SetField(card, "energyCost", energyCost);
        SetField(
            card,
            "operations",
            new List<BattleCardOperationDefinition>(
                operations ?? Array.Empty<BattleCardOperationDefinition>()));
        return card;
    }

    private static BattleCardOperationDefinition SelectionOperation(
        BattleCardOperationType type)
    {
        BattleCardOperationDefinition operation = Operation(
            type,
            BattleCardTargetScope.None);
        SetField(operation, "minimumSelectionCount", 1);
        SetField(operation, "maximumSelectionCount", 1);
        return operation;
    }

    private static void RaiseResolved(
        BattleCardRuntimeController controller,
        BattleEffectContext context,
        CharacterEffectDefinition effect,
        int damageDealt,
        int resolvedAmount = 0)
    {
        MethodInfo handler = typeof(BattleCardRuntimeController).GetMethod(
            "HandleEffectResolved",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(handler, Is.Not.Null);
        handler.Invoke(
            controller,
            new object[]
            {
                new BattleEffectResolvedEvent(
                    context,
                    effect,
                    new BattleEffectResult(
                        true,
                        true,
                        damageDealt,
                        resolvedAmount)),
            });
    }

    private static BattleCardOperationDefinition Operation(
        BattleCardOperationType type,
        BattleCardTargetScope scope,
        int amount = 0,
        int count = 1,
        float ratio = 1f,
        float duration = 0f,
        float radius = 1.5f,
        bool usePreviousChangedCount = false,
        BattleCardConditionDefinition condition = null)
    {
        BattleCardOperationDefinition operation = new();
        SetField(operation, "type", type);
        SetField(operation, "targetScope", scope);
        SetField(operation, "amount", amount);
        SetField(operation, "count", count);
        SetField(operation, "ratio", ratio);
        SetField(operation, "duration", duration);
        SetField(operation, "radius", radius);
        SetField(
            operation,
            "usePreviousChangedCount",
            usePreviousChangedCount);
        if (condition != null)
            SetField(operation, "condition", condition);
        return operation;
    }

    private static BattleCardConditionDefinition Condition(
        BattleCardConditionType type,
        float threshold = 0f,
        CharacterNumericComparison comparison =
            CharacterNumericComparison.GreaterThanOrEqual)
    {
        BattleCardConditionDefinition condition = new();
        SetField(condition, "type", type);
        SetField(condition, "threshold", threshold);
        SetField(condition, "comparison", comparison);
        return condition;
    }

    private EnemyRuntime Enemy()
    {
        EnemySO definition = Create<EnemySO>();
        return new EnemyRuntime(definition);
    }

    private CharacterRuntime RuntimeCharacter(
        CharacterRoleSO role,
        string suffix)
    {
        CharacterSO definition = Create<CharacterSO>();
        definition.name = $"RuntimeCharacterDefinition_{suffix}";
        SetField(definition, "role", role);
        GameObject gameObject = new(
            $"RuntimeCharacter_{suffix}",
            typeof(RectTransform),
            typeof(AudioSource));
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(gameObject);
        CharacterRuntime runtime =
            gameObject.AddComponent<CharacterRuntime>();
        SetField(runtime, "original", definition);
        SetField(runtime, "_currentHealth", 1);
        return runtime;
    }

    private T Create<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        value.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(value);
        return value;
    }

    private static void SetField(object target, string name, object value)
    {
        Assert.That(target, Is.Not.Null);
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(
            field,
            Is.Not.Null,
            $"Missing test field '{target.GetType().Name}.{name}'.");
        field.SetValue(target, value);
    }

    private static T GetField<T>(object target, string name)
    {
        Assert.That(target, Is.Not.Null);
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(
            field,
            Is.Not.Null,
            $"Missing test field '{target.GetType().Name}.{name}'.");
        return (T)field.GetValue(target);
    }

    private sealed class TestResource : IActiveSkillResource
    {
        public int Current { get; private set; }
        public int Maximum { get; }
        public int SpendCallCount { get; private set; }
        public int GainCallCount { get; private set; }

#pragma warning disable CS0067
        public event Action<int> Changed;
#pragma warning restore CS0067

        public TestResource(int current, int maximum)
        {
            Maximum = Mathf.Max(1, maximum);
            Current = Mathf.Clamp(current, 0, Maximum);
        }

        public bool CanSpend(int amount)
        {
            return amount >= 0 && Current >= amount;
        }

        public bool TrySpend(int amount)
        {
            SpendCallCount++;
            if (!CanSpend(amount))
                return false;
            Current -= amount;
            return true;
        }

        public bool TryGain(int amount)
        {
            GainCallCount++;
            if (amount <= 0 || Current >= Maximum)
                return false;
            int previous = Current;
            Current = Mathf.Min(Maximum, Current + amount);
            return Current > previous;
        }
    }

    private sealed class FakeBoard :
        IBattleBoard,
        IBattleObjectiveProvider,
        IBattleSpatialServiceProvider
    {
        public readonly List<EnemyRuntime> AllEnemies = new();
        public readonly List<EnemyRuntime[]> DamageTargetSnapshots = new();
        public readonly List<int> DamageAmounts = new();

        public IBattleObjective Objective { get; }
        public IBattleSpatialService SpatialService { get; }
        public int InitialEnemyCapacity => 16;
        public int LivingEnemyCount => AllEnemies.Count;
        public bool HasEmptyEnemyTile => true;

#pragma warning disable CS0067
        public event Action OccupancyChanged;
        public event Action<BattleEnemyDefeatedEvent> EnemyDefeated;
        public event Action<BattleStatusAppliedEvent> StatusApplied;
#pragma warning restore CS0067

        public FakeBoard(
            IBattleObjective objective,
            IBattleSpatialService spatial)
        {
            Objective = objective;
            SpatialService = spatial;
        }

        public bool TryAddEnemy(EnemyRuntime enemy) => false;

        public bool TryAddEnemiesToDistinctTiles(
            IReadOnlyList<EnemyRuntime> enemies) => false;

        public void ClearAllEnemies()
        {
            AllEnemies.Clear();
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
            return AllEnemies.ToArray();
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
            EnemyRuntime[] snapshot = CopyEnemies(targets);
            DamageTargetSnapshots.Add(snapshot);
            DamageAmounts.Add(damage);
            long total = (long)Mathf.Max(0, damage) * snapshot.Length;
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        public int TryHealCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            int amount,
            bool showAttackRange) => 0;

        public int TryHealAlliedCharacters(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            int amount) => 0;

        public int TryGrantShieldToCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            int amount,
            bool showAttackRange) => 0;

        public int TryGrantShieldToAlliedCharacters(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            int amount) => 0;

        public bool TryApplyCharacterStatus(
            BattleAbilityUser user,
            IReadOnlyList<EnemyRuntime> targets,
            StatusEffectSO statusEffect,
            float duration,
            float stacks,
            float tickInterval,
            bool showAttackRange) => false;

        public bool TryApplyAlliedCharacterStatus(
            BattleAbilityUser user,
            IReadOnlyList<IBattleCharacter> targets,
            StatusEffectSO statusEffect,
            float duration,
            float stacks) => false;

        public bool TryRemoveCharacterStatus(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            CharacterStatusRemovalSelection removalSelection,
            CharacterStatusRemovalAmount removalAmount,
            bool showAttackRange) => false;

        public bool TryRemoveAlliedCharacterStatus(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            CharacterStatusRemovalSelection removalSelection,
            CharacterStatusRemovalAmount removalAmount) => false;

        private static EnemyRuntime[] CopyEnemies(
            IReadOnlyList<EnemyRuntime> targets)
        {
            if (targets == null || targets.Count == 0)
                return Array.Empty<EnemyRuntime>();
            List<EnemyRuntime> result = new(targets.Count);
            foreach (EnemyRuntime target in targets)
            {
                if (target != null)
                    result.Add(target);
            }
            return result.ToArray();
        }
    }

    private sealed class FakeSpatialService : IBattleSpatialService
    {
        private readonly Dictionary<EnemyRuntime, Vector2>
            _enemyPositions = new();
        private readonly Dictionary<EnemyRuntime, BattleSpatialZone>
            _enemyZones = new();
        private readonly Dictionary<IBattleCharacter, BattleSpatialZone>
            _allyZones = new();

        public IReadOnlyList<EnemyRuntime> NearbyTargets { get; set; } =
            Array.Empty<EnemyRuntime>();
        public IReadOnlyList<EnemyRuntime> BehindTargets { get; set; } =
            Array.Empty<EnemyRuntime>();
        public BattleStatusTarget NearbyAnchor { get; private set; }
        public EnemyRuntime BehindAnchor { get; private set; }
        public readonly List<EnemyRuntime[]> PullTargetSnapshots = new();

        public bool IsAvailable => true;
        public float ArenaRadius => 5f;
        public float InnerZoneBoundaryRadius => 2.5f;

        public void SetPosition(EnemyRuntime enemy, Vector2 position)
        {
            _enemyPositions[enemy] = position;
        }

        public bool TryGetUnitPosition(
            BattleStatusTarget target,
            out Vector2 position)
        {
            if (target.Enemy != null &&
                _enemyPositions.TryGetValue(target.Enemy, out position))
            {
                return true;
            }
            position = Vector2.zero;
            return false;
        }

        public BattleSpatialZone GetUnitZone(BattleStatusTarget target)
        {
            if (target.Enemy != null &&
                _enemyZones.TryGetValue(
                    target.Enemy,
                    out BattleSpatialZone enemyZone))
            {
                return enemyZone;
            }
            if (target.Ally != null &&
                _allyZones.TryGetValue(
                    target.Ally,
                    out BattleSpatialZone allyZone))
            {
                return allyZone;
            }
            return BattleSpatialZone.Unknown;
        }

        public IReadOnlyList<EnemyRuntime> SelectNearbyEnemies(
            BattleStatusTarget anchor,
            float radius = BattleSpatialDefaults.NearbyRadius,
            int maximumCount = 0,
            bool includeAnchor = false)
        {
            NearbyAnchor = anchor;
            return NearbyTargets;
        }

        public IReadOnlyList<EnemyRuntime> SelectEnemiesBehind(
            EnemyRuntime anchor,
            float maximumDistance = BattleSpatialDefaults.NearbyRadius,
            int maximumCount = 1,
            float halfAngle = BattleSpatialDefaults.BehindHalfAngle)
        {
            BehindAnchor = anchor;
            return BehindTargets;
        }

        public IReadOnlyList<EnemyRuntime> SelectDefenseLineEnemies()
        {
            return Array.Empty<EnemyRuntime>();
        }

        public IReadOnlyList<EnemyRuntime> SelectRecentCoreAttackers(
            float lookbackSeconds =
                BattleSpatialDefaults.RecentCoreAttackWindow)
        {
            return Array.Empty<EnemyRuntime>();
        }

        public int MoveAlliesCoreward(
            IReadOnlyList<IBattleCharacter> targets,
            float distance = BattleSpatialDefaults.MovementStep)
        {
            return targets?.Count ?? 0;
        }

        public int MoveAlliesOutward(
            IReadOnlyList<IBattleCharacter> targets,
            float distance = BattleSpatialDefaults.MovementStep)
        {
            return targets?.Count ?? 0;
        }

        public int MoveAlliesToOuterZone(
            IReadOnlyList<IBattleCharacter> targets)
        {
            return targets?.Count ?? 0;
        }

        public int MoveAlliesToPoint(
            IReadOnlyList<IBattleCharacter> targets,
            Vector2 point,
            bool instant = false)
        {
            return targets?.Count ?? 0;
        }

        public int MoveAlliesToEnemyFlank(
            IReadOnlyList<IBattleCharacter> targets,
            EnemyRuntime enemy,
            float flankDistance = BattleSpatialDefaults.MovementStep,
            bool instant = false)
        {
            return targets?.Count ?? 0;
        }

        public bool TrySwapAllies(
            IBattleCharacter first,
            IBattleCharacter second)
        {
            return first != null && second != null;
        }

        public int PullEnemiesTowardPoint(
            IReadOnlyList<EnemyRuntime> targets,
            Vector2 point,
            float distance = BattleSpatialDefaults.MovementStep)
        {
            EnemyRuntime[] snapshot = targets != null
                ? Copy(targets)
                : Array.Empty<EnemyRuntime>();
            PullTargetSnapshots.Add(snapshot);
            return snapshot.Length;
        }

        private static EnemyRuntime[] Copy(
            IReadOnlyList<EnemyRuntime> targets)
        {
            EnemyRuntime[] result = new EnemyRuntime[targets.Count];
            for (int index = 0; index < targets.Count; index++)
                result[index] = targets[index];
            return result;
        }
    }
}
