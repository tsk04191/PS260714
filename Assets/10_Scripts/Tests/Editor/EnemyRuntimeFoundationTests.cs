using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static TestReflection;

public sealed class EnemyRuntimeFoundationTests
{
    private sealed class TestBattleCharacter : IBattleCharacter
    {
        public int PartySlotIndex { get; }
        public int TotalDamageDealt { get; private set; }
        public int CurrentHealth { get; private set; } = 100;
        public int MaximumHealth => 100;
        public int CurrentShield { get; private set; }
        public float DisabledTimeRemaining { get; private set; }
        public float CurrentAttackPower => 10f;
        public float CurrentAttackSpeed => 1f;

        public event Action<BattleStatusChangedEvent> StatusChanged
        {
            add { }
            remove { }
        }

        public TestBattleCharacter(int partySlotIndex)
        {
            PartySlotIndex = partySlotIndex;
        }

        public bool HasStatusEffect(StatusEffectSO statusEffect) => false;
        public int GetStatusStackCount(StatusEffectSO statusEffect) => 0;
        public IReadOnlyList<BattleStatusSnapshot> GetActiveStatusEffects() =>
            Array.Empty<BattleStatusSnapshot>();
        public bool TryConsumeStatusStacks(
            StatusEffectSO statusEffect,
            int stackCount) => false;

        public int Heal(int amount)
        {
            int applied = Mathf.Min(
                Mathf.Max(0, amount),
                MaximumHealth - CurrentHealth);
            CurrentHealth += applied;
            return applied;
        }

        public int GainShield(int amount)
        {
            int applied = Mathf.Max(0, amount);
            CurrentShield += applied;
            return applied;
        }

        public int TakeDamage(int amount)
        {
            int applied = Mathf.Min(
                CurrentHealth,
                Mathf.Max(0, amount));
            CurrentHealth -= applied;
            return applied;
        }

        public bool CanSpendHealth(int amount) =>
            amount > 0 && CurrentHealth - amount >= 1;

        public bool TrySpendHealth(int amount)
        {
            if (!CanSpendHealth(amount))
                return false;
            CurrentHealth -= amount;
            return true;
        }

        public bool Initialize() => true;
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
            TotalDamageDealt += Mathf.Max(0, damage);
        }
        public void DisableFor(float duration)
        {
            DisabledTimeRemaining = Mathf.Max(
                DisabledTimeRemaining,
                duration);
        }
        public bool ApplyStatusEffect(
            StatusEffectSO definition,
            float duration,
            int stacks) => false;
        public bool ApplyStatusEffect(
            StatusEffectSO definition,
            float duration,
            int stacks,
            IBattleCharacter source) => false;
        public int RemoveStatusEffects(
            CharacterStatusRemovalSelection removalSelection,
            CharacterStatusRemovalAmount removalAmount) => 0;
    }

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
    public void PreciseCoreDamage_AccumulatesFractionWithoutRoundingLoss()
    {
        EnemyRuntime runtime = CreateEnemy(
            "fractional",
            preciseCoreDamage: 1.75f,
            damagePolicy: EnemyCoreAttackDamagePolicy.AccumulateFraction);

        int[] resolved =
        {
            runtime.ResolveCoreAttackDamageForHit(),
            runtime.ResolveCoreAttackDamageForHit(),
            runtime.ResolveCoreAttackDamageForHit(),
            runtime.ResolveCoreAttackDamageForHit(),
        };

        Assert.That(resolved, Is.EqualTo(new[] { 1, 2, 2, 2 }));
    }

    [Test]
    public void CoreModifiers_SupportTimedAndNextAttackValues()
    {
        EnemyRuntime runtime = CreateEnemy("modifiers", coreDamage: 10);
        Assert.That(runtime.ApplyCombatModifier(new EnemyCombatModifier(
            "timed-damage",
            EnemyCombatModifierType.CoreAttackDamage,
            percentage: 0.2f,
            duration: 5f)), Is.True);
        Assert.That(runtime.ApplyCombatModifier(new EnemyCombatModifier(
            "timed-interval",
            EnemyCombatModifierType.CoreAttackInterval,
            percentage: -0.25f,
            duration: 5f)), Is.True);
        Assert.That(runtime.ReserveNextCoreAttackModifier(
            new EnemyCombatModifier(
                "next-hit",
                EnemyCombatModifierType.CoreAttackDamage,
                percentage: 0.5f)), Is.True);

        Assert.That(runtime.CoreAttackDamageValue, Is.EqualTo(12f));
        Assert.That(runtime.CoreAttackInterval, Is.EqualTo(1.5f));
        Assert.That(runtime.ResolveCoreAttackDamageForHit(), Is.EqualTo(18));
        Assert.That(runtime.ResolveCoreAttackDamageForHit(), Is.EqualTo(12));

        runtime.TickCombatRuntime(5.1f, out _);

        Assert.That(runtime.CoreAttackDamageValue, Is.EqualTo(10f));
        Assert.That(runtime.CoreAttackInterval, Is.EqualTo(2f));
    }

    [Test]
    public void StatusPolicy_ResistsControlsAndExpiresImmunity()
    {
        EnemyRuntime runtime = CreateEnemy("status-policy");
        StatusEffectSO stun = CreateStatus("test-stun", true);
        StatusEffectSO debuff = CreateStatus("test-debuff", false);
        StatusEffectSO laterDebuff = CreateStatus("later-debuff", false);
        runtime.ApplyCombatModifier(new EnemyCombatModifier(
            "control-resistance",
            EnemyCombatModifierType.StatusDuration,
            percentage: -0.5f,
            statusScope: EnemyStatusModifierScope.Controls));

        Assert.That(runtime.ApplyStatusEffect(stun, 10f, 1), Is.True);
        Assert.That(
            runtime.GetStatusRemainingDuration(stun),
            Is.EqualTo(5f));
        Assert.That(runtime.ApplyStatusEffect(debuff, 10f, 1), Is.True);
        Assert.That(
            runtime.GetStatusRemainingDuration(debuff),
            Is.EqualTo(10f));

        runtime.ApplyCombatModifier(new EnemyCombatModifier(
            "temporary-immunity",
            EnemyCombatModifierType.StatusImmunity,
            duration: 1f,
            statusScope: EnemyStatusModifierScope.Debuffs));
        Assert.That(
            runtime.ApplyStatusEffect(laterDebuff, 10f, 1),
            Is.False);

        runtime.TickCombatRuntime(1.1f, out _);

        Assert.That(
            runtime.ApplyStatusEffect(laterDebuff, 10f, 1),
            Is.True);
    }

    [Test]
    public void AfterNoDamage_RepeatsUsingOperationInterval()
    {
        EnemyAbilityOperationDefinition operation = CreateOperation(
            EnemyAbilityOperationType.ExecuteEffects,
            interval: 2f);
        EnemyAbilityDefinition ability = CreateAbility(
            "no-damage",
            EnemyAbilityTrigger.AfterNoDamage,
            new[] { operation });
        SetField(ability, "noDamageDuration", 3f);
        ability.Validate();
        EnemyRuntime runtime = CreateEnemy(
            "no-damage-runtime",
            abilities: new[] { ability });

        runtime.TickCombatRuntime(2.9f, out _);
        Assert.That(
            runtime.TryMarkNoDamageDurationReached(ability),
            Is.False);
        runtime.TickCombatRuntime(0.1f, out _);
        Assert.That(
            runtime.TryMarkNoDamageDurationReached(ability),
            Is.True);
        runtime.TickCombatRuntime(1.9f, out _);
        Assert.That(
            runtime.TryMarkNoDamageDurationReached(ability),
            Is.False);
        runtime.TickCombatRuntime(0.1f, out _);
        Assert.That(
            runtime.TryMarkNoDamageDurationReached(ability),
            Is.True);

        runtime.RecordDamageTaken();
        runtime.TickCombatRuntime(2.9f, out _);
        Assert.That(
            runtime.TryMarkNoDamageDurationReached(ability),
            Is.False);
    }

    [Test]
    public void DamageSourceHistory_ExpiresPerSourceAfterConfiguredWindow()
    {
        EnemyRuntime runtime = CreateEnemy("damage-source-history");

        runtime.RecordDamageTaken("player-slot:0");

        Assert.That(
            runtime.WasDamagedBySourceWithin("player-slot:0", 2f),
            Is.True);
        Assert.That(
            runtime.WasDamagedBySourceWithin("player-slot:1", 2f),
            Is.False);

        runtime.TickCombatRuntime(1.9f, out _);
        Assert.That(
            runtime.WasDamagedBySourceWithin("player-slot:0", 2f),
            Is.True);
        runtime.TickCombatRuntime(0.2f, out _);
        Assert.That(
            runtime.WasDamagedBySourceWithin("player-slot:0", 2f),
            Is.False);
    }

    [Test]
    public void Charge_OnlyConfiguredInterruptReasonCancelsExecution()
    {
        EnemyAbilityDefinition ability = CreateAbility(
            "charged",
            EnemyAbilityTrigger.OnCooldown,
            Array.Empty<EnemyAbilityOperationDefinition>());
        EnemyAbilityChargeDefinition charge =
            new EnemyAbilityChargeDefinition();
        SetField(charge, "enabled", true);
        SetField(charge, "duration", 2f);
        SetField(charge, "interruptible", true);
        SetField(
            charge,
            "interrupts",
            EnemyChargeInterruptFlags.DirectDamage);
        SetField(ability, "charge", charge);
        ability.Validate();
        EnemyRuntime runtime = CreateEnemy(
            "charge-runtime",
            abilities: new[] { ability });
        EnemyAbilityRuntimeState state = runtime.AbilityStates[0];

        Assert.That(runtime.TryBeginAbilityCharge(state, out _), Is.True);
        Assert.That(
            runtime.TryInterruptCharge(
                EnemyChargeInterruptReason.Stun,
                out _),
            Is.False);
        Assert.That(runtime.IsCharging, Is.True);
        Assert.That(
            runtime.TryInterruptCharge(
                EnemyChargeInterruptReason.DirectDamage,
                out EnemyActiveChargeRuntimeState interrupted),
            Is.True);
        Assert.That(interrupted.AbilityState, Is.SameAs(state));
        Assert.That(runtime.IsCharging, Is.False);
    }

    [Test]
    public void AuthoredAbilityCharge_CompletesWithoutStartingSecondCharge()
    {
        EnemyAbilityOperationDefinition operation = CreateOperation(
            EnemyAbilityOperationType.ChargeCoreAttack,
            multiplier: 1.8f);
        SetField(operation, "duration", 1.2f);
        operation.Validate();
        EnemyAbilityDefinition ability = CreateAbility(
            "single-charge",
            EnemyAbilityTrigger.OnCooldown,
            new[] { operation });
        EnemyAbilityChargeDefinition charge = new();
        SetField(charge, "enabled", true);
        SetField(charge, "duration", 1.2f);
        SetField(charge, "interruptible", true);
        charge.Validate();
        SetField(ability, "charge", charge);
        SetField(ability, "cooldown", 8f);
        ability.Validate();

        DungeonBoardView board = CreateCircularBoard(4, 1);
        EnemyRuntime enemy = CreateEnemy(
            "single-charge-enemy",
            attackInterval: 100f,
            abilities: new[] { ability });
        Assert.That(board.TryAddEnemy(enemy), Is.True);
        EnemyAbilityRuntimeState state = enemy.AbilityStates[0];
        Assert.That(
            state.TickCooldown(ability.Cooldown, false, 100f),
            Is.True);

        board.TickEnemyAbilities(0.01f, Array.Empty<IBattleCharacter>());
        Assert.That(enemy.IsCharging, Is.True);
        board.TickEnemyAbilities(1.21f, Array.Empty<IBattleCharacter>());

        Assert.That(enemy.IsCharging, Is.False);
        Assert.That(enemy.HasReadyChargedCoreAttack, Is.True);
        Assert.That(enemy.ResolveCoreAttackDamageForHit(), Is.EqualTo(9));
        Assert.That(enemy.HasReadyChargedCoreAttack, Is.False);
    }

    [Test]
    public void BossPhase_AdvancesMonotonicallyAndGatesAbilities()
    {
        EnemyAbilityDefinition firstAbility = CreateAbility(
            "phase-one",
            EnemyAbilityTrigger.OnCooldown,
            Array.Empty<EnemyAbilityOperationDefinition>());
        EnemyAbilityDefinition secondAbility = CreateAbility(
            "phase-two",
            EnemyAbilityTrigger.OnCooldown,
            Array.Empty<EnemyAbilityOperationDefinition>());
        EnemyAbilityDefinition thirdAbility = CreateAbility(
            "phase-three",
            EnemyAbilityTrigger.OnCooldown,
            Array.Empty<EnemyAbilityOperationDefinition>());
        EnemyBossPhaseDefinition first = CreatePhase(
            "p1", 67, 100, true, "phase-one");
        EnemyBossPhaseDefinition second = CreatePhase(
            "p2", 34, 66, false, "phase-two");
        EnemyBossPhaseDefinition third = CreatePhase(
            "p3", 0, 33, false, "phase-three");
        EnemyRuntime runtime = CreateEnemy(
            "boss",
            abilities: new[]
            {
                firstAbility,
                secondAbility,
                thirdAbility,
            },
            phases: new[] { first, second, third });

        Assert.That(runtime.CurrentPhaseId, Is.EqualTo("p1"));
        Assert.That(
            runtime.IsAbilityEnabledInCurrentPhase(firstAbility),
            Is.True);
        Assert.That(
            runtime.IsAbilityEnabledInCurrentPhase(secondAbility),
            Is.False);

        runtime.SetHealth(50);
        Assert.That(
            runtime.TryAdvancePhaseForHealth(out _, out _),
            Is.True);
        Assert.That(runtime.CurrentPhaseId, Is.EqualTo("p2"));
        runtime.SetHealth(20);
        Assert.That(
            runtime.TryAdvancePhaseForHealth(out _, out _),
            Is.True);
        Assert.That(runtime.CurrentPhaseId, Is.EqualTo("p3"));
        runtime.SetHealth(90);
        Assert.That(
            runtime.TryAdvancePhaseForHealth(out _, out _),
            Is.False);
        Assert.That(runtime.CurrentPhaseId, Is.EqualTo("p3"));

        EnemyRuntime contactRuntime = CreateEnemy(
            "contact-boss",
            abilities: new[] { firstAbility, secondAbility },
            phases: new[] { first, second });
        Assert.That(
            contactRuntime.TryAdvancePhaseOnCoreContact(out _, out _),
            Is.True);
        Assert.That(contactRuntime.CurrentPhaseId, Is.EqualTo("p2"));
    }

    [Test]
    public void RadiusEntryComposite_DoesNotRepeatOnFirstTickButFiresForLateEntry()
    {
        EnemyAbilityOperationDefinition link = CreateLinkOperation();
        EnemyAbilityTargetDefinition target =
            EnemyAbilityTargetDefinition.CreateRuntimePreset(
                EnemyAbilityTargetFaction.EnemyAllies,
                EnemyAbilityTargetSubject.WorldRadius,
                count: 3,
                radius: 100f,
                includesSource: true);
        EnemyAbilityDefinition composite = CreateAbility(
            "spawn-and-radius-link",
            EnemyAbilityTrigger.OnSpawn,
            new[] { link },
            target);
        SetField(
            composite,
            "triggerEvents",
            new List<EnemyAbilityTrigger>
            {
                EnemyAbilityTrigger.OnAllyEnteredRadius,
            });
        SetField(composite, "initialCharges", 2);
        composite.Validate();
        Assert.That(composite.Targeting.IsValid, Is.True);
        Assert.That(composite.Targeting.HasTarget, Is.True);
        Assert.That(
            composite.Targeting.SelectionMode,
            Is.EqualTo(BattleAbilitySelectionMode.Inherit));

        DungeonBoardView board = CreateCircularBoard(4, 1);
        EnemyRuntime existing = CreateEnemy("radius-existing");
        EnemyRuntime source = CreateEnemy(
            "radius-source",
            abilities: new[] { composite });
        Assert.That(board.TryAddEnemy(existing), Is.True);
        Assert.That(board.TryAddEnemy(source), Is.True);
        EnemyAbilityRuntimeState state = source.AbilityStates[0];
        Assert.That(state.RemainingCharges, Is.EqualTo(1));

        board.TickEnemyAbilities(
            0.1f,
            Array.Empty<IBattleCharacter>());
        Assert.That(
            state.RemainingCharges,
            Is.EqualTo(1),
            "The first radius snapshot must not repeat OnSpawn.");

        EnemyRuntime lateEntry = CreateEnemy("radius-late-entry");
        Assert.That(board.TryAddEnemy(lateEntry), Is.True);
        board.TickEnemyAbilities(
            0.1f,
            Array.Empty<IBattleCharacter>());

        Assert.That(
            state.RemainingCharges,
            Is.Zero,
            "A newly entered ally must activate the composite trigger.");
    }

    [Test]
    public void ResourceRecoveryMultiplier_DistinguishesGlobalAndPlayerRadiusTargets()
    {
        EnemyAbilityOperationDefinition globalOperation = CreateOperation(
            EnemyAbilityOperationType.ModifyResourceRecovery,
            multiplier: 0.8f);
        EnemyAbilityDefinition globalAura = CreateAbility(
            "global-resource-aura",
            EnemyAbilityTrigger.AlwaysWhileActive,
            new[] { globalOperation },
            EnemyAbilityTargetDefinition.CreateRuntimePreset(
                EnemyAbilityTargetFaction.None,
                EnemyAbilityTargetSubject.None));
        DungeonBoardView board = CreateCircularBoard(4, 1);
        EnemyRuntime globalSource = CreateEnemy(
            "global-resource-source",
            abilities: new[] { globalAura });
        Assert.That(board.TryAddEnemy(globalSource), Is.True);
        Assert.That(
            board.ResolveResourceRecoveryMultiplier(),
            Is.EqualTo(0.8f));

        EnemyAbilityDefinition insideAura = CreateAbility(
            "inside-player-radius",
            EnemyAbilityTrigger.AlwaysWhileActive,
            new[]
            {
                CreateOperation(
                    EnemyAbilityOperationType.ModifyResourceRecovery,
                    multiplier: 0.5f),
            },
            EnemyAbilityTargetDefinition.CreateRuntimePreset(
                EnemyAbilityTargetFaction.PlayerCharacters,
                EnemyAbilityTargetSubject.WorldRadius,
                count: 1,
                radius: 100f));
        EnemyAbilityDefinition outsideAura = CreateAbility(
            "outside-player-radius",
            EnemyAbilityTrigger.AlwaysWhileActive,
            new[]
            {
                CreateOperation(
                    EnemyAbilityOperationType.ModifyResourceRecovery,
                    multiplier: 0.25f),
            },
            EnemyAbilityTargetDefinition.CreateRuntimePreset(
                EnemyAbilityTargetFaction.PlayerCharacters,
                EnemyAbilityTargetSubject.WorldRadius,
                count: 1,
                radius: 0.001f));
        EnemyRuntime insideSource = CreateEnemy(
            "inside-resource-source",
            abilities: new[] { insideAura });
        EnemyRuntime outsideSource = CreateEnemy(
            "outside-resource-source",
            abilities: new[] { outsideAura });
        Assert.That(board.TryAddEnemy(insideSource), Is.True);
        Assert.That(board.TryAddEnemy(outsideSource), Is.True);
        TestBattleCharacter player = new(0);
        board.SetBattleCharacters(new IBattleCharacter[] { player });
        Assert.That(
            board.TryGetUnitPosition(
                BattleStatusTarget.FromAlly(player),
                out Vector2 playerPosition),
            Is.True);
        Assert.That(
            board.TryGetUnitPosition(
                BattleStatusTarget.FromEnemy(outsideSource),
                out Vector2 outsidePosition),
            Is.True);
        Assert.That(
            Vector2.Distance(playerPosition, outsidePosition),
            Is.GreaterThan(0.001f));

        Assert.That(
            board.ResolveResourceRecoveryMultiplier(),
            Is.EqualTo(0.4f),
            "Global and in-radius modifiers apply; out-of-radius does not.");
    }

    [Test]
    public void CreateWorldZone_CountDoesNotSquareGlobalHealingMultiplier()
    {
        DungeonBoardView board = CreateCircularBoard(4, 1);
        IBattleObjective core = board.Objective;
        Assert.That(core.TakeDamage(50), Is.EqualTo(50));

        EnemyAbilityOperationDefinition operation = CreateOperation(
            EnemyAbilityOperationType.CreateWorldZone,
            multiplier: 0.7f);
        SetField(operation, "count", 2);
        SetField(operation, "duration", 8f);
        SetField(operation, "worldRadius", 2.5f);
        operation.Validate();
        EnemyAbilityDefinition zoneAbility = CreateAbility(
            "two-global-healing-zones",
            EnemyAbilityTrigger.OnSpawn,
            new[] { operation },
            EnemyAbilityTargetDefinition.CreateRuntimePreset(
                EnemyAbilityTargetFaction.None,
                EnemyAbilityTargetSubject.None));
        EnemyRuntime source = CreateEnemy(
            "two-zone-source",
            abilities: new[] { zoneAbility });

        Assert.That(board.TryAddEnemy(source), Is.True);
        IBattleObjectiveModifierService modifierService =
            core as IBattleObjectiveModifierService;
        Assert.That(modifierService, Is.Not.Null);
        Assert.That(modifierService.ActiveModifierCount, Is.EqualTo(1));
        Assert.That(
            modifierService.HealingReceivedMultiplier,
            Is.EqualTo(0.7f));
        Assert.That(core.Heal(10), Is.EqualTo(7));
        Assert.That(
            core.CurrentHealth,
            Is.EqualTo(57),
            "Two presentation zones must not square the global modifier.");
    }

    [Test]
    public void LinkedDeathBuff_UsesOnlyTheSourcesOwnedLink()
    {
        EnemyAbilityDefinition ownedLink = CreateLinkAbility("owned-link");
        EnemyAbilityDefinition foreignLink = CreateLinkAbility("foreign-link");
        EnemyAbilityDefinition survivorBuff =
            CreateLinkedDeathBuffAbility("owned-survivor-buff");
        DungeonBoardView board = CreateCircularBoard(4, 2);
        EnemyRuntime ownedSurvivor = CreateEnemy(
            "owned-survivor",
            coreDamage: 10);
        EnemyRuntime foreignSurvivor = CreateEnemy(
            "foreign-survivor",
            coreDamage: 10);
        EnemyRuntime defeated = CreateEnemy("shared-defeated");
        defeated.SetHealth(10);
        EnemyRuntime owner = CreateEnemy(
            "link-owner",
            abilities: new[] { ownedLink, survivorBuff });
        EnemyRuntime foreignOwner = CreateEnemy(
            "foreign-link-owner",
            abilities: new[] { foreignLink });
        Assert.That(board.TryAddEnemy(ownedSurvivor), Is.True);
        Assert.That(board.TryAddEnemy(foreignSurvivor), Is.True);
        Assert.That(board.TryAddEnemy(defeated), Is.True);
        Assert.That(board.TryAddEnemy(owner), Is.True);
        Assert.That(board.TryAddEnemy(foreignOwner), Is.True);
        ExecuteLinkOperation(
            board,
            owner,
            ownedLink,
            ownedSurvivor,
            defeated);
        ExecuteLinkOperation(
            board,
            foreignOwner,
            foreignLink,
            foreignSurvivor,
            defeated);

        Assert.That(board.TryDamageEnemy(defeated, 15), Is.GreaterThan(0));
        Assert.That(defeated.Health, Is.Zero);
        Assert.That(
            ownedSurvivor.CoreAttackDamageValue,
            Is.EqualTo(12f));
        Assert.That(
            foreignSurvivor.CoreAttackDamageValue,
            Is.EqualTo(10f),
            "A foreign owner's link must not supply survivor targets.");
    }

    [Test]
    public void LinkedDeathBuff_OwnerDeathBuffsItsLivingMembers()
    {
        EnemyAbilityDefinition link = CreateLinkAbility("owner-death-link");
        EnemyAbilityDefinition survivorBuff =
            CreateLinkedDeathBuffAbility("owner-death-survivor-buff");
        DungeonBoardView board = CreateCircularBoard(4, 1);
        EnemyRuntime firstSurvivor = CreateEnemy(
            "owner-death-first",
            coreDamage: 10);
        EnemyRuntime secondSurvivor = CreateEnemy(
            "owner-death-second",
            coreDamage: 10);
        EnemyRuntime owner = CreateEnemy(
            "defeated-link-owner",
            abilities: new[] { link, survivorBuff });
        owner.SetHealth(10);
        Assert.That(board.TryAddEnemy(firstSurvivor), Is.True);
        Assert.That(board.TryAddEnemy(secondSurvivor), Is.True);
        Assert.That(board.TryAddEnemy(owner), Is.True);
        ExecuteLinkOperation(
            board,
            owner,
            link,
            firstSurvivor,
            secondSurvivor);

        Assert.That(board.TryDamageEnemy(owner, 15), Is.GreaterThan(0));
        Assert.That(owner.Health, Is.Zero);
        Assert.That(firstSurvivor.CoreAttackDamageValue, Is.EqualTo(12f));
        Assert.That(secondSurvivor.CoreAttackDamageValue, Is.EqualTo(12f));
    }

    [Test]
    public void SummonService_QueuesScaledChildrenAndEnforcesLimits()
    {
        GameObject managerObject = new("SummonServiceManager");
        managerObject.SetActive(false);
        _createdObjects.Add(managerObject);
        BattleManager manager = managerObject.AddComponent<BattleManager>();
        DungeonBoardView board = CreateCircularBoard(4, 2);
        SetField(manager, "_board", board);
        SetField(manager, "<State>k__BackingField", EBattleState.Running);

        EnemyAbilityDefinition deathSummonAbility = CreateAbility(
            "summon-pack",
            EnemyAbilityTrigger.OnDeath,
            Array.Empty<EnemyAbilityOperationDefinition>());
        EnemyRuntime source = CreateEnemy(
            "summoner",
            abilities: new[] { deathSummonAbility });
        source.TakeDamage(source.Health);
        EnemyRuntime candidateRuntime = CreateEnemy(
            "summoned-candidate",
            coreDamage: 3);
        EnemyReferenceDefinition reference = new();
        SetField(reference, "enemy", candidateRuntime.Definition);
        EnemySummonDefinition summon = new();
        SetField(
            summon,
            "candidates",
            new List<EnemyReferenceDefinition> { reference });
        SetField(summon, "minimumCount", 2);
        SetField(summon, "maximumCount", 2);
        SetField(summon, "maximumActive", 2);
        SetField(summon, "allowRecursiveSummon", false);
        SetField(summon, "childHealthMultiplier", 0.5f);
        SetField(summon, "childCoreAttackMultiplier", 2f);
        summon.Validate();

        Assert.That(
            manager.TrySummonEnemies(source, "summon-pack", summon),
            Is.EqualTo(2));
        Assert.That(manager.ActiveSummonCount, Is.EqualTo(2));
        Assert.That(manager.SpawnQueue, Has.Count.EqualTo(2));
        foreach (EnemyRuntime child in manager.SpawnQueue)
        {
            Assert.That(child.IsSummoned, Is.True);
            Assert.That(child.SummonDepth, Is.EqualTo(1));
            Assert.That(child.SummonerEnemyId, Is.EqualTo("summoner"));
            Assert.That(child.OriginAbilityId, Is.EqualTo("summon-pack"));
            Assert.That(child.MaxHealth, Is.EqualTo(50));
            Assert.That(child.CoreAttackDamageValue, Is.EqualTo(6f));
        }

        Assert.That(
            manager.TrySummonEnemies(source, "summon-pack", summon),
            Is.Zero);
        Assert.That(
            manager.TrySummonEnemies(
                manager.SpawnQueue[0],
                "summon-pack",
                summon),
            Is.Zero);
        Assert.That(manager.EndBattle(board), Is.True);
    }

    [Test]
    public void DelayedSummon_FiresAfterDelayAndClearsWithSession()
    {
        GameObject managerObject = new("DelayedSummonManager");
        managerObject.SetActive(false);
        _createdObjects.Add(managerObject);
        BattleManager manager = managerObject.AddComponent<BattleManager>();
        DungeonBoardView board = CreateCircularBoard(4, 2);
        SetField(manager, "_board", board);
        SetField(manager, "<State>k__BackingField", EBattleState.Running);

        EnemyAbilityDefinition deathSummonAbility = CreateAbility(
            "delayed-summon",
            EnemyAbilityTrigger.OnDeath,
            Array.Empty<EnemyAbilityOperationDefinition>());
        EnemyRuntime source = CreateEnemy(
            "delayed-summoner",
            abilities: new[] { deathSummonAbility });
        source.TakeDamage(source.Health);
        EnemyRuntime candidateRuntime = CreateEnemy("delayed-child");
        EnemyReferenceDefinition reference = new();
        SetField(reference, "enemy", candidateRuntime.Definition);
        EnemySummonDefinition summon = new();
        SetField(
            summon,
            "candidates",
            new List<EnemyReferenceDefinition> { reference });
        SetField(summon, "minimumCount", 2);
        SetField(summon, "maximumCount", 2);
        SetField(summon, "maximumActive", 4);
        SetField(summon, "allowRecursiveSummon", false);
        SetField(summon, "childHealthMultiplier", 1f);
        SetField(summon, "childCoreAttackMultiplier", 1f);
        summon.Validate();

        Assert.That(
            manager.TryScheduleSummon(
                source,
                "delayed-summon",
                summon,
                2f),
            Is.True);
        Assert.That(manager.PendingScheduledSummonCount, Is.EqualTo(1));
        InvokeMethod(manager, "CheckForCompletion");
        Assert.That(manager.State, Is.EqualTo(EBattleState.Running));
        InvokeMethod(manager, "TickScheduledEnemySummons", 1.9f);
        Assert.That(manager.SpawnQueue, Is.Empty);
        InvokeMethod(manager, "TickScheduledEnemySummons", 0.11f);
        Assert.That(manager.PendingScheduledSummonCount, Is.Zero);
        Assert.That(manager.SpawnQueue, Has.Count.EqualTo(2));

        Assert.That(
            manager.TryScheduleSummon(
                source,
                "delayed-summon",
                summon,
                6f),
            Is.True);
        Assert.That(manager.PendingScheduledSummonCount, Is.EqualTo(1));
        Assert.That(manager.EndBattle(board), Is.True);
        Assert.That(manager.PendingScheduledSummonCount, Is.Zero);
    }

    [Test]
    public void SpawnIntervalModifier_RescalesActiveTimerAndExpires()
    {
        GameObject managerObject = new("SpawnIntervalModifierManager");
        managerObject.SetActive(false);
        _createdObjects.Add(managerObject);
        BattleManager manager = managerObject.AddComponent<BattleManager>();
        DungeonBoardView board = CreateCircularBoard(4, 1);
        SetField(manager, "_board", board);
        SetField(manager, "<State>k__BackingField", EBattleState.Running);
        SetField(manager, "_spawnInterval", 10f);
        SetField(manager, "_scheduledSpawnInterval", 10f);
        SetField(manager, "_spawnTimeRemaining", 5f);

        Assert.That(
            manager.TryAddSpawnIntervalModifier("e002", 0.8f, 5f),
            Is.True);
        Assert.That(manager.SpawnInterval, Is.EqualTo(8f));
        Assert.That(manager.SpawnTimeRemaining, Is.EqualTo(4f));

        InvokeMethod(manager, "TickSpawnIntervalModifiers", 4.9f);
        Assert.That(manager.SpawnInterval, Is.EqualTo(8f));
        InvokeMethod(manager, "TickSpawnIntervalModifiers", 0.2f);
        Assert.That(manager.SpawnInterval, Is.EqualTo(10f));
        Assert.That(manager.SpawnTimeRemaining, Is.EqualTo(5f));
        Assert.That(manager.EndBattle(board), Is.True);
    }

    [Test]
    public void WorldRadiusAura_RespectsCircularFormationLayerScope()
    {
        EnemyAbilityTargetDefinition target =
            EnemyAbilityTargetDefinition.CreateRuntimePreset(
                EnemyAbilityTargetFaction.EnemyAllies,
                EnemyAbilityTargetSubject.WorldRadius,
                count: 16,
                radius: 100f,
                worldLayerScope: EnemyWorldLayerScope.Same);
        EnemyAbilityOperationDefinition operation = CreateOperation(
            EnemyAbilityOperationType.ModifyCoreAttackDamage,
            multiplier: 1.5f);
        EnemyAbilityDefinition aura = CreateAbility(
            "same-layer-aura",
            EnemyAbilityTrigger.AlwaysWhileActive,
            new[] { operation },
            target);
        DungeonBoardView board = CreateCircularBoard(4, 2);
        EnemyRuntime source = CreateEnemy(
            "aura-source",
            coreDamage: 10,
            abilities: new[] { aura });
        EnemyRuntime sameLayer = CreateEnemy("same-layer", coreDamage: 10);
        Assert.That(board.TryAddEnemy(source), Is.True);
        Assert.That(board.TryAddEnemy(sameLayer), Is.True);
        Assert.That(board.TryAddEnemy(CreateEnemy("filler-a")), Is.True);
        Assert.That(board.TryAddEnemy(CreateEnemy("filler-b")), Is.True);
        EnemyRuntime rearLayer = CreateEnemy("rear-layer", coreDamage: 10);
        Assert.That(board.TryAddEnemy(rearLayer), Is.True);

        Assert.That(sameLayer.CoreAttackDamageValue, Is.EqualTo(15f));
        Assert.That(rearLayer.CoreAttackDamageValue, Is.EqualTo(10f));
    }

    [Test]
    public void WorldRadiusMetric_SelectsLowestValueAfterRadiusFilter()
    {
        EnemyAbilityTargetDefinition target =
            EnemyAbilityTargetDefinition.CreateRuntimePreset(
                EnemyAbilityTargetFaction.EnemyAllies,
                EnemyAbilityTargetSubject.WorldRadius,
                EnemyAbilityTargetMetric.HealthPercentage,
                count: 1,
                radius: 100f);
        EnemyAbilityDefinition aura = CreateAbility(
            "lowest-health-aura",
            EnemyAbilityTrigger.AlwaysWhileActive,
            new[]
            {
                CreateOperation(
                    EnemyAbilityOperationType.ModifyCoreAttackDamage,
                    multiplier: 1.5f),
            },
            target);
        DungeonBoardView board = CreateCircularBoard(4, 1);
        EnemyRuntime lowestButFarther = CreateEnemy(
            "lowest-but-farther",
            coreDamage: 10);
        lowestButFarther.SetHealth(10);
        EnemyRuntime higherButNearer = CreateEnemy(
            "higher-but-nearer",
            coreDamage: 10);
        higherButNearer.SetHealth(90);
        EnemyRuntime source = CreateEnemy(
            "metric-aura-source",
            coreDamage: 10,
            abilities: new[] { aura });

        Assert.That(board.TryAddEnemy(lowestButFarther), Is.True);
        Assert.That(board.TryAddEnemy(higherButNearer), Is.True);
        Assert.That(board.TryAddEnemy(source), Is.True);

        Assert.That(
            lowestButFarther.CoreAttackDamageValue,
            Is.EqualTo(15f));
        Assert.That(
            higherButNearer.CoreAttackDamageValue,
            Is.EqualTo(10f));
    }

    [Test]
    public void MedicAsset_WorldRadiusHealsLowestHealthPercentage()
    {
        EnemySO definition = AssetDatabase.LoadAssetAtPath<EnemySO>(
            "Assets/06_Runtime/Resources/Enemies/S001_MedicRemnant.asset");
        Assert.That(definition, Is.Not.Null);

        DungeonBoardView board = CreateCircularBoard(4, 2);
        List<EnemyRuntime> candidates = new()
        {
            CreateEnemy("medic-target-a"),
            CreateEnemy("medic-target-b"),
            CreateEnemy("medic-target-c"),
            CreateEnemy("medic-target-d"),
        };
        EnemyRuntime medic = definition.CreateRuntime();

        foreach (EnemyRuntime candidate in candidates)
            Assert.That(board.TryAddEnemy(candidate), Is.True);
        Assert.That(board.TryAddEnemy(medic), Is.True);
        Assert.That(
            board.TryGetUnitPosition(
                BattleStatusTarget.FromEnemy(medic),
                out Vector2 medicPosition),
            Is.True);

        EnemyRuntime lowest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (EnemyRuntime candidate in candidates)
        {
            Assert.That(
                board.TryGetUnitPosition(
                    BattleStatusTarget.FromEnemy(candidate),
                    out Vector2 candidatePosition),
                Is.True);
            float distance = Vector2.Distance(
                candidatePosition,
                medicPosition);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                lowest = candidate;
            }
        }
        Assert.That(lowest, Is.Not.Null);
        Assert.That(
            nearestDistance,
            Is.LessThanOrEqualTo(3f));
        lowest.SetHealth(20);

        board.TickEnemyAbilities(4f, Array.Empty<IBattleCharacter>());

        Assert.That(lowest.Health, Is.EqualTo(28));
        foreach (EnemyRuntime candidate in candidates)
        {
            if (!ReferenceEquals(candidate, lowest))
                Assert.That(candidate.Health, Is.EqualTo(100));
        }
        Assert.That(
            medic.AbilityCooldownRemaining,
            Is.EqualTo(4f).Within(0.0001f));
    }

    [Test]
    public void CircularCoreAttack_PublishesContactAndDamageEvents()
    {
        DungeonBoardView board = CreateCircularBoard(4, 1);
        EnemyRuntime enemy = CreateEnemy(
            "event-enemy",
            coreDamage: 3,
            coreRange: 100f,
            attackInterval: 1f);
        List<EnemyCombatEventType> eventTypes = new();
        board.EnemyCombatEventRaised += eventData =>
            eventTypes.Add(eventData.Type);
        Assert.That(board.TryAddEnemy(enemy), Is.True);

        board.TickEnemyAbilities(1f, Array.Empty<IBattleCharacter>());

        Assert.That(eventTypes, Does.Contain(
            EnemyCombatEventType.FirstCoreContact));
        Assert.That(eventTypes, Does.Contain(
            EnemyCombatEventType.CoreContact));
        Assert.That(eventTypes, Does.Contain(
            EnemyCombatEventType.CoreAttackPreparing));
        Assert.That(eventTypes, Does.Contain(
            EnemyCombatEventType.CoreAttackResolved));
        Assert.That(eventTypes, Does.Contain(
            EnemyCombatEventType.CoreDamageApplied));
    }

    private DungeonBoardView CreateCircularBoard(
        int laneCount,
        int layerCount)
    {
        GameObject boardObject = new(
            $"RuntimeFoundationBoard{_createdObjects.Count}",
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
        board.ConfigureArena(BattleArenaSetup.CreateCircular(
            coreMaximumHealth: 100,
            laneCount: laneCount,
            maximumLayerCount: layerCount));
        BindTemporaryWorldReferences(board, boardObject);
        board.Initialize(3, 1);
        SetField(board, "worldPresentationRoot", null);
        return board;
    }

    private static void BindTemporaryWorldReferences(
        DungeonBoardView board,
        GameObject boardObject)
    {
        GameObject world = new("RuntimeFoundationTemporaryWorld");
        world.transform.SetParent(boardObject.transform, false);
        Camera worldCamera = world.AddComponent<Camera>();
        DungeonWorldInputView input =
            world.AddComponent<DungeonWorldInputView>();
        DungeonBattleCoreWorldGaugeView coreGauge =
            world.AddComponent<DungeonBattleCoreWorldGaugeView>();
        GameObject foreground = new(
            "RuntimeFoundationTemporaryForegroundCamera");
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

    private EnemyRuntime CreateEnemy(
        string enemyId,
        int coreDamage = 5,
        float preciseCoreDamage = 0f,
        EnemyCoreAttackDamagePolicy damagePolicy =
            EnemyCoreAttackDamagePolicy.LegacyInteger,
        float coreRange = 0f,
        float attackInterval = 2f,
        IReadOnlyList<EnemyAbilityDefinition> abilities = null,
        IReadOnlyList<EnemyBossPhaseDefinition> phases = null)
    {
        EnemySO definition = ScriptableObject.CreateInstance<EnemySO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.name = enemyId;
        _createdObjects.Add(definition);
        SetField(definition, "enemyId", enemyId);
        SetField(definition, "baseHealth", 100);
        SetField(definition, "healthScale", 1f);
        SetField(definition, "approachSpeed", 10f);
        SetField(definition, "formationRadius", 0.35f);
        SetField(
            definition,
            "combatStatSchemaVersion",
            EnemySO.CurrentCombatStatSchemaVersion);
        SetField(definition, "attackPower", (float)coreDamage);
        SetField(definition, "coreAttackDamage", coreDamage);
        SetField(definition, "preciseCoreAttackDamage", preciseCoreDamage);
        SetField(definition, "coreAttackDamagePolicy", damagePolicy);
        SetField(definition, "coreAttackInterval", attackInterval);
        SetField(definition, "coreAttackRange", coreRange);
        SetField(
            definition,
            "abilities",
            abilities != null
                ? new List<EnemyAbilityDefinition>(abilities)
                : new List<EnemyAbilityDefinition>());
        SetField(
            definition,
            "phaseDefinitions",
            phases != null
                ? new List<EnemyBossPhaseDefinition>(phases)
                : new List<EnemyBossPhaseDefinition>());
        return definition.CreateRuntime();
    }

    private StatusEffectSO CreateStatus(string id, bool control)
    {
        StatusEffectSO status =
            ScriptableObject.CreateInstance<StatusEffectSO>();
        status.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(status);
        SetField(status, "statusId", id);
        SetField(status, "canTargetEnemy", true);
        SetField(status, "alignment", StatusEffectAlignment.Debuff);
        SetField(status, "durationMode", StatusEffectDurationMode.Timed);
        SetField(status, "defaultDuration", 10f);
        List<StatusEffectControlDefinition> controls = new();
        if (control)
        {
            StatusEffectControlDefinition definition = new();
            SetField(
                definition,
                "controlType",
                StatusEffectControlType.DisableAllActions);
            controls.Add(definition);
        }
        SetField(status, "controlEffects", controls);
        status.ValidateDefinition();
        return status;
    }

    private static EnemyAbilityDefinition CreateAbility(
        string id,
        EnemyAbilityTrigger trigger,
        IReadOnlyList<EnemyAbilityOperationDefinition> operations,
        EnemyAbilityTargetDefinition target = null)
    {
        EnemyAbilityDefinition ability = new();
        SetField(ability, "abilityId", id);
        SetField(ability, "abilityTypeId", id);
        SetField(ability, "trigger", trigger);
        SetField(
            ability,
            "target",
            target ?? EnemyAbilityTargetDefinition.CreateRuntimePreset(
                EnemyAbilityTargetFaction.Self,
                EnemyAbilityTargetSubject.Self));
        SetField(
            ability,
            "operations",
            new List<EnemyAbilityOperationDefinition>(operations));
        ability.Validate();
        return ability;
    }

    private static EnemyAbilityOperationDefinition CreateOperation(
        EnemyAbilityOperationType type,
        float multiplier = 1f,
        float interval = 0f)
    {
        EnemyAbilityOperationDefinition operation = new();
        SetField(operation, "type", type);
        SetField(operation, "enabled", true);
        SetField(operation, "amount", 0);
        SetField(operation, "multiplier", multiplier);
        SetField(operation, "interval", interval);
        operation.Validate();
        return operation;
    }

    private static EnemyAbilityOperationDefinition CreateLinkOperation()
    {
        EnemyAbilityOperationDefinition operation = CreateOperation(
            EnemyAbilityOperationType.LinkTargets);
        SetField(operation, "percentage", 0.3f);
        SetField(operation, "worldRadius", 100f);
        operation.Validate();
        return operation;
    }

    private static EnemyAbilityDefinition CreateLinkAbility(string id)
    {
        return CreateAbility(
            id,
            EnemyAbilityTrigger.OnSpawn,
            new[] { CreateLinkOperation() },
            EnemyAbilityTargetDefinition.CreateRuntimePreset(
                EnemyAbilityTargetFaction.EnemyAllies,
                EnemyAbilityTargetSubject.WorldRadius,
                count: 3,
                radius: 100f,
                includesSource: true));
    }

    private static EnemyAbilityDefinition CreateLinkedDeathBuffAbility(
        string id)
    {
        EnemyAbilityOperationDefinition operation = CreateOperation(
            EnemyAbilityOperationType.ModifyCoreAttackDamage,
            multiplier: 1.2f);
        SetField(operation, "duration", 5f);
        operation.Validate();
        EnemyAbilityDefinition ability = CreateAbility(
            id,
            EnemyAbilityTrigger.OnNearbyEnemyDeath,
            new[] { operation },
            EnemyAbilityTargetDefinition.CreateRuntimePreset(
                EnemyAbilityTargetFaction.EnemyAllies,
                EnemyAbilityTargetSubject.WorldRadius,
                count: 3,
                radius: 100f,
                includesSource: false));
        EnemyAbilityParameterDefinition linkedOnly = new();
        SetField(linkedOnly, "key", "linkedOnly");
        SetField(
            linkedOnly,
            "valueType",
            EnemyAbilityParameterValueType.Boolean);
        SetField(linkedOnly, "boolValue", true);
        linkedOnly.Validate();
        SetField(
            ability,
            "parameters",
            new List<EnemyAbilityParameterDefinition> { linkedOnly });
        ability.Validate();
        return ability;
    }

    private static void ExecuteLinkOperation(
        DungeonBoardView board,
        EnemyRuntime owner,
        EnemyAbilityDefinition ability,
        params EnemyRuntime[] members)
    {
        Assert.That(board, Is.Not.Null);
        Assert.That(owner, Is.Not.Null);
        Assert.That(ability, Is.Not.Null);
        EnemyAbilityRuntimeState state = null;
        foreach (EnemyAbilityRuntimeState candidate in owner.AbilityStates)
        {
            if (ReferenceEquals(candidate?.Definition, ability))
            {
                state = candidate;
                break;
            }
        }
        Assert.That(state, Is.Not.Null);
        Assert.That(ability.Operations, Has.Count.GreaterThan(0));
        InvokeMethod(
            board,
            "ExecuteEnemyRuntimeOperation",
            owner,
            state,
            ability,
            ability.Operations[0],
            members);
    }

    private static EnemyBossPhaseDefinition CreatePhase(
        string id,
        int minimumHealth,
        int maximumHealth,
        bool advanceOnCoreContact,
        params string[] abilityIds)
    {
        EnemyBossPhaseDefinition phase = new();
        SetField(phase, "phaseId", id);
        SetField(phase, "minimumHealthPercent", minimumHealth);
        SetField(phase, "maximumHealthPercent", maximumHealth);
        SetField(phase, "advanceOnCoreContact", advanceOnCoreContact);
        SetField(phase, "abilityIds", new List<string>(abilityIds));
        phase.Validate();
        return phase;
    }
}
