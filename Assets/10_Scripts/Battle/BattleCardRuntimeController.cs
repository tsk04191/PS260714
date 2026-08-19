using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional action hooks supplied by the battle-card runtime. Character
/// actions query this service through the board's common ability modifier
/// service, keeping card effects out of character definitions.
/// </summary>
public interface IBattleCardActionRuntimeService
{
    int ResolveActiveSkillEnergyCost(
        IBattleCharacter source,
        int baseCost);

    void NotifyActiveSkillResolved(
        IBattleCharacter source,
        bool succeeded);

    bool TryGetForcedTarget(
        IBattleCharacter source,
        out EnemyRuntime target);
}

/// <summary>
/// Executes ordered card operations and owns transient effects created by
/// cards for the current battle. It deliberately contains no UI; selection
/// is exposed through BattleCardDeckRuntime and resumed by DungeonPage.
/// </summary>
public sealed class BattleCardRuntimeController :
    IBattleAbilityUserModifierService,
    IBattleCardActionRuntimeService
{
    private readonly struct OperationOutcome
    {
        public bool Attempted { get; }
        public bool Succeeded { get; }
        public int ChangedCount { get; }
        public int DefeatedCount { get; }

        public OperationOutcome(
            bool attempted,
            bool succeeded,
            int changedCount = 0,
            int defeatedCount = 0)
        {
            Attempted = attempted;
            Succeeded = succeeded;
            ChangedCount = Mathf.Max(0, changedCount);
            DefeatedCount = Mathf.Max(0, defeatedCount);
        }
    }

    private readonly struct TargetSet
    {
        public CharacterTargetFaction Faction { get; }
        public IReadOnlyList<EnemyRuntime> Enemies { get; }
        public IReadOnlyList<IBattleCharacter> Allies { get; }
        public int Count => Faction == CharacterTargetFaction.Ally
            ? Allies.Count
            : Enemies.Count;

        private TargetSet(
            CharacterTargetFaction faction,
            IReadOnlyList<EnemyRuntime> enemies,
            IReadOnlyList<IBattleCharacter> allies)
        {
            Faction = faction;
            Enemies = enemies ?? Array.Empty<EnemyRuntime>();
            Allies = allies ?? Array.Empty<IBattleCharacter>();
        }

        public static TargetSet FromEnemies(
            IReadOnlyList<EnemyRuntime> targets)
        {
            return new TargetSet(
                CharacterTargetFaction.Enemy,
                targets,
                null);
        }

        public static TargetSet FromAllies(
            IReadOnlyList<IBattleCharacter> targets)
        {
            return new TargetSet(
                CharacterTargetFaction.Ally,
                null,
                targets);
        }

        public static TargetSet EmptyEnemies =>
            FromEnemies(Array.Empty<EnemyRuntime>());
    }

    private sealed class ActiveExecution
    {
        public BattleCardInstance Instance;
        public BattleCardSO Card;
        public CharacterRuntime Source;
        public IReadOnlyList<EnemyRuntime> PrimaryEnemies;
        public IReadOnlyList<IBattleCharacter> PrimaryAllies;
        public IReadOnlyList<EnemyRuntime> SecondaryEnemies;
        public IReadOnlyList<IBattleCharacter> SecondaryAllies;
        public Vector2 PrimaryPoint;
        public bool HasPrimaryPoint;
        public Vector2 SecondaryPoint;
        public bool HasSecondaryPoint;
        public int EnergyCost;
        public int OperationIndex;
        public bool AnyAttempted;
        public bool AnySucceeded;
        public int PreviousChangedCount;
        public int PreviousDefeatedCount;
        public BattleCardOperationDefinition PendingSelectionOperation;
    }

    private sealed class ActionModifierRuntime
    {
        public readonly List<IBattleCharacter> Sources = new();
        public CharacterActionKind ActionKind;
        public int FlatDamage;
        public float RepeatRatio;
        public StatusEffectSO AppliedStatus;
        public int AppliedStatusStacks;
        public float AppliedStatusDuration;
        public int StatusStackBonus;
        public int SkillCostReduction;
        public StatusEffectSO RequiredTargetStatus;
        public int RemainingUses;
        public float RemainingDuration;
        public long ActiveStatusExecutionId;
        public long LastConsumedStatusExecutionId;
        public long LastProcessedDamageExecutionId;

        public bool AppliesTo(IBattleCharacter source)
        {
            return source != null && Sources.Contains(source) &&
                   RemainingUses > 0 &&
                   (RemainingDuration > 0f ||
                    float.IsPositiveInfinity(RemainingDuration));
        }
    }

    private sealed class ForcedTargetRuntime
    {
        public IBattleCharacter Source;
        public EnemyRuntime Target;
        public float RemainingDuration;
    }

    private sealed class HealthTriggerRuntime
    {
        public readonly List<IBattleCharacter> Targets = new();
        public float HealthRatio;
        public int HealAmount;
        public int HarmfulRemovalCount;
        public StatusEffectSO RequiredResourceStatus;
    }

    private sealed class KillTriggerRuntime
    {
        public readonly List<IBattleCharacter> Sources = new();
        public int EnergyGain;
        public float RemainingDuration;
    }

    private sealed class ZoneRuntime
    {
        public BattleCardOperationDefinition Operation;
        public CharacterRuntime Source;
        public Vector2 Point;
        public float DelayRemaining;
        public float DurationRemaining;
        public bool Triggered;
        public readonly HashSet<EnemyRuntime> Inside = new();
        public readonly HashSet<EnemyRuntime> Affected = new();
    }

    private readonly List<IBattleCharacter> _allies = new();
    private readonly List<ActionModifierRuntime> _actionModifiers = new();
    private readonly List<ForcedTargetRuntime> _forcedTargets = new();
    private readonly List<HealthTriggerRuntime> _healthTriggers = new();
    private readonly List<KillTriggerRuntime> _killTriggers = new();
    private readonly List<ZoneRuntime> _zones = new();

    private IBattleBoard _board;
    private IBattleSpatialService _spatial;
    private IBattleObjective _objective;
    private IActiveSkillResource _resource;
    private BattleCardDeckRuntime _deck;
    private IBattlePresentationEventSource _presentationEvents;
    private ActiveExecution _active;
    private bool _handlingFollowUp;

    public bool IsExecutionPending => _active != null;
    public bool IsCardSelectionPending =>
        _active?.PendingSelectionOperation != null &&
        _deck?.IsZoneSelectionPending == true;

    public void Bind(
        IBattleBoard board,
        IActiveSkillResource resource,
        BattleCardDeckRuntime deck,
        IReadOnlyList<IBattleCharacter> allies)
    {
        Clear();
        _board = board;
        _resource = resource;
        _deck = deck;
        _spatial = (board as IBattleSpatialServiceProvider)?.SpatialService;
        _objective = (board as IBattleObjectiveProvider)?.Objective;
        if (allies != null)
        {
            foreach (IBattleCharacter ally in allies)
            {
                if (ally != null && !_allies.Contains(ally))
                    _allies.Add(ally);
            }
        }

        _board.EnemyDefeated += HandleEnemyDefeated;
        _presentationEvents = board as IBattlePresentationEventSource;
        if (_presentationEvents != null)
            _presentationEvents.EffectResolved += HandleEffectResolved;
    }

    public void Clear()
    {
        if (_board != null)
            _board.EnemyDefeated -= HandleEnemyDefeated;
        if (_presentationEvents != null)
            _presentationEvents.EffectResolved -= HandleEffectResolved;
        if (_deck?.IsZoneSelectionPending == true)
            _deck.CancelZoneSelection();

        _board = null;
        _spatial = null;
        _objective = null;
        _resource = null;
        _deck = null;
        _presentationEvents = null;
        _active = null;
        _allies.Clear();
        _actionModifiers.Clear();
        _forcedTargets.Clear();
        _healthTriggers.Clear();
        _killTriggers.Clear();
        _zones.Clear();
        _handlingFollowUp = false;
    }

    public bool TryBeginExecution(
        BattleCardInstance instance,
        CharacterRuntime source,
        IReadOnlyList<EnemyRuntime> primaryEnemies,
        IReadOnlyList<IBattleCharacter> primaryAllies,
        IReadOnlyList<EnemyRuntime> secondaryEnemies = null,
        IReadOnlyList<IBattleCharacter> secondaryAllies = null,
        Vector2 primaryPoint = default,
        bool hasPrimaryPoint = false,
        Vector2 secondaryPoint = default,
        bool hasSecondaryPoint = false)
    {
        BattleCardSO card = instance?.Definition;
        if (_active != null || card == null || _board == null ||
            _resource == null || _deck == null ||
            !_deck.CanPlay(instance))
        {
            return false;
        }

        int cost = _deck.GetEffectiveCost(instance);
        if (!_resource.TrySpend(cost))
            return false;

        _active = new ActiveExecution
        {
            Instance = instance,
            Card = card,
            Source = source,
            PrimaryEnemies = primaryEnemies ?? Array.Empty<EnemyRuntime>(),
            PrimaryAllies = primaryAllies ?? Array.Empty<IBattleCharacter>(),
            SecondaryEnemies = secondaryEnemies ?? Array.Empty<EnemyRuntime>(),
            SecondaryAllies = secondaryAllies ?? Array.Empty<IBattleCharacter>(),
            PrimaryPoint = primaryPoint,
            HasPrimaryPoint = hasPrimaryPoint,
            SecondaryPoint = secondaryPoint,
            HasSecondaryPoint = hasSecondaryPoint,
            EnergyCost = cost,
        };

        bool started = ContinueExecution();
        if (!started)
            AbortActiveExecution(true);
        return started;
    }

    public bool TryToggleCardSelection(BattleCardInstance instance)
    {
        return IsCardSelectionPending &&
               _deck.TryToggleZoneSelection(instance);
    }

    public bool TryConfirmCardSelection()
    {
        if (!IsCardSelectionPending ||
            !_deck.TryConfirmZoneSelection(
                out IReadOnlyList<BattleCardInstance> selected))
        {
            return false;
        }

        BattleCardOperationDefinition operation =
            _active.PendingSelectionOperation;
        _active.PendingSelectionOperation = null;
        int changed = operation.Type switch
        {
            BattleCardOperationType.DiscardSelected =>
                _deck.TryDiscardSelectedHandCards(
                    selected,
                    _active.Instance),
            BattleCardOperationType.ExhaustSelected =>
                _deck.TryExhaustSelectedHandCards(
                    selected,
                    _active.Instance),
            BattleCardOperationType.ReturnDiscarded =>
                MoveDiscardSelectionToHand(selected),
            _ => 0,
        };
        _active.PreviousChangedCount = changed;
        _active.PreviousDefeatedCount = 0;
        _active.AnyAttempted = true;
        _active.AnySucceeded |= changed > 0 || selected.Count == 0;
        _active.OperationIndex++;
        return ContinueExecution();
    }

    public bool CancelPendingExecution()
    {
        if (_active == null)
            return false;
        if (_active.PendingSelectionOperation != null &&
            _active.OperationIndex > 0)
        {
            return false;
        }
        if (_deck?.IsZoneSelectionPending == true)
            _deck.CancelZoneSelection();
        AbortActiveExecution(true);
        return true;
    }

    public void Tick(float deltaTime)
    {
        deltaTime = Mathf.Max(0f, deltaTime);
        if (deltaTime <= 0f)
            return;

        TickActionModifiers(deltaTime);
        TickForcedTargets(deltaTime);
        TickHealthTriggers();
        TickKillTriggers(deltaTime);
        TickZones(deltaTime);
    }

    public float Resolve(
        BattleAbilityUser user,
        BattleEffectOriginKind originKind,
        long actionExecutionId,
        IBattleEffectDefinition effect,
        BattleAbilityModifierValueKind valueKind,
        float baseValue)
    {
        if (_handlingFollowUp || user.Unit.Ally == null || effect == null)
            return baseValue;

        CharacterActionKind actionKind = ToActionKind(originKind);
        if (valueKind != BattleAbilityModifierValueKind.StatusStacks ||
            effect.BattleEffectType != BattleEffectType.ApplyStatus)
        {
            return baseValue;
        }

        int bonus = 0;
        foreach (ActionModifierRuntime modifier in _actionModifiers)
        {
            if (modifier.StatusStackBonus > 0 &&
                (modifier.AppliesTo(user.Unit.Ally) ||
                 actionExecutionId > 0 &&
                 modifier.ActiveStatusExecutionId == actionExecutionId) &&
                modifier.ActionKind == actionKind)
            {
                modifier.ActiveStatusExecutionId = actionExecutionId;
                bonus = BattleValueMath.SaturatingAddNonNegative(
                    bonus,
                    modifier.StatusStackBonus);
            }
        }
        return Mathf.Max(0f, baseValue + bonus);
    }

    public int ResolveActiveSkillEnergyCost(
        IBattleCharacter source,
        int baseCost)
    {
        int reduction = 0;
        foreach (ActionModifierRuntime modifier in _actionModifiers)
        {
            if (modifier.SkillCostReduction > 0 &&
                modifier.AppliesTo(source))
            {
                reduction = BattleValueMath.SaturatingAddNonNegative(
                    reduction,
                    modifier.SkillCostReduction);
            }
        }
        return Mathf.Max(0, baseCost - reduction);
    }

    public void NotifyActiveSkillResolved(
        IBattleCharacter source,
        bool succeeded)
    {
        if (!succeeded || source == null)
            return;
        for (int index = _actionModifiers.Count - 1; index >= 0; index--)
        {
            ActionModifierRuntime modifier = _actionModifiers[index];
            if (modifier.SkillCostReduction <= 0 ||
                !modifier.AppliesTo(source))
            {
                continue;
            }
            modifier.RemainingUses--;
            if (modifier.RemainingUses <= 0)
                _actionModifiers.RemoveAt(index);
        }
    }

    public bool TryGetForcedTarget(
        IBattleCharacter source,
        out EnemyRuntime target)
    {
        for (int index = _forcedTargets.Count - 1; index >= 0; index--)
        {
            ForcedTargetRuntime forced = _forcedTargets[index];
            if (forced.Source == null || forced.Target == null ||
                forced.Target.Health <= 0 ||
                forced.RemainingDuration <= 0f)
            {
                _forcedTargets.RemoveAt(index);
                continue;
            }
            if (ReferenceEquals(forced.Source, source))
            {
                target = forced.Target;
                return true;
            }
        }
        target = null;
        return false;
    }

    private bool ContinueExecution()
    {
        while (_active != null &&
               _active.OperationIndex < _active.Card.Operations.Count)
        {
            BattleCardOperationDefinition operation =
                _active.Card.Operations[_active.OperationIndex];
            if (operation == null)
            {
                _active.OperationIndex++;
                continue;
            }

            TargetSet targets = ResolveTargets(operation);
            if (!AllowsOperation(operation, targets))
            {
                _active.PreviousChangedCount = 0;
                _active.PreviousDefeatedCount = 0;
                _active.OperationIndex++;
                continue;
            }

            if (operation.RequiresCardSelection)
            {
                if (!BeginCardSelection(operation))
                    return false;
                return true;
            }

            OperationOutcome outcome = ExecuteOperation(operation, targets);
            _active.AnyAttempted |= outcome.Attempted;
            _active.AnySucceeded |= outcome.Succeeded;
            _active.PreviousChangedCount = outcome.ChangedCount;
            _active.PreviousDefeatedCount = outcome.DefeatedCount;
            _active.OperationIndex++;
        }

        if (_active == null)
            return false;
        return FinishActiveExecution();
    }

    private bool FinishActiveExecution()
    {
        ActiveExecution completed = _active;
        _active = null;
        if (!completed.AnySucceeded)
        {
            _resource.TryGain(completed.EnergyCost);
            return false;
        }
        if (_deck.CompleteSuccessfulPlay(completed.Instance))
            return true;

        _resource.TryGain(completed.EnergyCost);
        return false;
    }

    private void AbortActiveExecution(bool refund)
    {
        ActiveExecution cancelled = _active;
        _active = null;
        if (refund && cancelled?.EnergyCost > 0)
            _resource?.TryGain(cancelled.EnergyCost);
    }

    private bool BeginCardSelection(
        BattleCardOperationDefinition operation)
    {
        BattleCardZone zone = operation.Type ==
                              BattleCardOperationType.ReturnDiscarded
            ? BattleCardZone.DiscardPile
            : BattleCardZone.Hand;
        if (!_deck.TryBeginZoneSelection(
                zone,
                operation.MinimumSelectionCount,
                operation.MaximumSelectionCount,
                _active.Instance))
        {
            return false;
        }
        _active.PendingSelectionOperation = operation;
        return true;
    }

    private int MoveDiscardSelectionToHand(
        IReadOnlyList<BattleCardInstance> selected)
    {
        int changed = 0;
        if (selected == null)
            return changed;
        foreach (BattleCardInstance card in selected)
        {
            if (_deck.TryMoveDiscardCardToHand(card))
                changed++;
        }
        return changed;
    }

    private OperationOutcome ExecuteOperation(
        BattleCardOperationDefinition operation,
        TargetSet targets)
    {
        switch (operation.Type)
        {
            case BattleCardOperationType.SharedEffect:
                return ExecuteSharedEffect(operation, targets);

            case BattleCardOperationType.ObjectiveRestore:
            {
                int changed = _objective?.Heal(ResolveAmount(operation)) ?? 0;
                return new OperationOutcome(true, changed > 0, changed);
            }

            case BattleCardOperationType.ObjectiveInvulnerability:
            {
                bool changed = _objective?.TryGrantDamageImmunity(
                    operation.Duration) == true;
                return new OperationOutcome(true, changed, changed ? 1 : 0);
            }

            case BattleCardOperationType.ObjectiveDamageRedirect:
            {
                IBattleCharacter target = FirstAlly(targets);
                bool changed = target != null &&
                    _objective?.TrySetNextDamageRedirect(
                        target,
                        operation.Ratio) == true;
                return new OperationOutcome(true, changed, changed ? 1 : 0);
            }

            case BattleCardOperationType.SpendTargetHealth:
                return SpendTargetHealth(targets, ResolveAmount(operation));

            case BattleCardOperationType.Revive:
                return ReviveTargets(targets, operation);

            case BattleCardOperationType.Draw:
            {
                int count = operation.UsePreviousChangedCount
                    ? _active.PreviousChangedCount + operation.Count
                    : operation.Count;
                int changed = _deck.TryDrawCards(count);
                return new OperationOutcome(true, changed > 0, changed);
            }

            case BattleCardOperationType.ShuffleDiscardIntoDraw:
            {
                bool changed = _deck.TryShuffleDiscardIntoDrawPile();
                return new OperationOutcome(true, changed, changed ? 1 : 0);
            }

            case BattleCardOperationType.ShuffleDrawAndDiscard:
            {
                bool changed =
                    _deck.TryCombineAndShuffleDrawAndDiscardPiles();
                return new OperationOutcome(true, changed, changed ? 1 : 0);
            }

            case BattleCardOperationType.DiscardHand:
            {
                int changed = _deck.DiscardEntireHand(_active.Instance);
                return new OperationOutcome(true, true, changed);
            }

            case BattleCardOperationType.GainEnergy:
            {
                bool changed = _resource.TryGain(ResolveAmount(operation));
                return new OperationOutcome(true, changed, changed ? 1 : 0);
            }

            case BattleCardOperationType.ModifyCardCost:
            {
                int value = operation.CostModifierMode ==
                            BattleCardCostModifierMode.Add
                    ? -operation.Amount
                    : operation.Amount;
                bool changed = _deck.TryAddCostModifier(
                    operation.CostModifierMode,
                    value,
                    Mathf.Max(1, operation.Count),
                    _active.Instance);
                return new OperationOutcome(true, changed, changed ? 1 : 0);
            }

            case BattleCardOperationType.ProtectHand:
            {
                bool changed = _deck.TrySkipNextAutomaticRedraw();
                return new OperationOutcome(true, changed, changed ? 1 : 0);
            }

            case BattleCardOperationType.Move:
                return MoveTargets(operation, targets);

            case BattleCardOperationType.Swap:
                return SwapTargets(targets);

            case BattleCardOperationType.PullEnemies:
                return PullTargets(operation, targets);

            case BattleCardOperationType.CreateZone:
                return CreateZone(operation);

            case BattleCardOperationType.ApplyAttackModifier:
                return AddActionModifier(
                    operation,
                    targets,
                    CharacterActionKind.Attack);

            case BattleCardOperationType.ApplySkillModifier:
                return AddActionModifier(
                    operation,
                    targets,
                    CharacterActionKind.Skill);

            case BattleCardOperationType.ApplyHealthTrigger:
                return AddHealthOrKillTrigger(operation, targets);

            case BattleCardOperationType.ExtendStatusDuration:
                return ExtendStatusDuration(operation, targets);

            case BattleCardOperationType.ForceTarget:
                return AddForcedTarget(operation, targets);

            case BattleCardOperationType.ReadyBasicAttack:
                return ReadyBasicAttacks(targets);

            default:
                return default;
        }
    }

    private OperationOutcome ExecuteSharedEffect(
        BattleCardOperationDefinition operation,
        TargetSet targets)
    {
        CharacterEffectDefinition effect = operation.SharedEffect;
        if (effect == null)
            return default;

        int removableStacksBefore = CountExplicitRemovalStacks(
            effect,
            targets);

        List<EnemyRuntime> livingBefore = new();
        foreach (EnemyRuntime enemy in targets.Enemies)
        {
            if (enemy != null && enemy.Health > 0)
                livingBefore.Add(enemy);
        }

        BattleEffectContext context = BattleEffectContext.ForBattleCard(
            _active.Source,
            _board,
            _resource,
            targets.Faction,
            targets.Enemies,
            targets.Allies,
            _active.Source?.CurrentAttackPower ?? 0f,
            _deck,
            _active.Card.Affiliation != BattleCardAffiliation.Neutral);
        BattleEffectResult result = BattleEffectExecutor.ExecuteEffect(
            context,
            effect,
            _active.Source?.Data,
            operation.UsePreviousChangedCount
                ? Mathf.Max(1, _active.PreviousChangedCount)
                : 1,
            true,
            actionId: _active.Card.CardId);
        int defeated = 0;
        foreach (EnemyRuntime enemy in livingBefore)
        {
            if (enemy.Health <= 0)
                defeated++;
        }
        int removedStacks = Mathf.Max(
            0,
            removableStacksBefore - CountExplicitRemovalStacks(
                effect,
                targets));
        int changed = removedStacks > 0
            ? removedStacks
            : result.DamageDealt > 0
                ? result.DamageDealt
                : result.Succeeded ? 1 : 0;
        return new OperationOutcome(
            result.Attempted,
            result.Succeeded,
            changed,
            defeated);
    }

    private OperationOutcome SpendTargetHealth(
        TargetSet targets,
        int amount)
    {
        int changed = 0;
        foreach (IBattleCharacter target in targets.Allies)
        {
            if (target?.TrySpendHealth(amount) == true)
                changed++;
        }
        return new OperationOutcome(true, changed > 0, changed);
    }

    private OperationOutcome ReviveTargets(
        TargetSet targets,
        BattleCardOperationDefinition operation)
    {
        int changed = 0;
        foreach (IBattleCharacter target in targets.Allies)
        {
            if (target is not CharacterRuntime runtime)
                continue;
            int amount = runtime.CurrentHealth <= 0 &&
                         operation.Ratio > 0f && operation.Ratio <= 1f
                ? Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        runtime.MaximumHealth * operation.Ratio))
                : ResolveAmount(operation);
            changed = BattleValueMath.SaturatingAddNonNegative(
                changed,
                runtime.RestoreHealth(amount, true));
        }
        return new OperationOutcome(true, changed > 0, changed);
    }

    private static OperationOutcome ReadyBasicAttacks(TargetSet targets)
    {
        int changed = 0;
        foreach (IBattleCharacter target in targets.Allies)
        {
            if (target is CharacterRuntime runtime &&
                runtime.TryReadyBasicAttack())
            {
                changed++;
            }
        }
        return new OperationOutcome(true, changed > 0, changed);
    }

    private OperationOutcome MoveTargets(
        BattleCardOperationDefinition operation,
        TargetSet targets)
    {
        if (_spatial?.IsAvailable != true ||
            targets.Faction != CharacterTargetFaction.Ally)
        {
            return default;
        }

        int changed = operation.MovementMode switch
        {
            BattleCardMovementMode.CorewardByDistance =>
                _spatial.MoveAlliesCoreward(
                    targets.Allies,
                    operation.Radius > 0f
                        ? operation.Radius
                        : BattleSpatialDefaults.MovementStep),
            BattleCardMovementMode.OutwardByDistance =>
                _spatial.MoveAlliesOutward(
                    targets.Allies,
                    operation.Radius > 0f
                        ? operation.Radius
                        : BattleSpatialDefaults.MovementStep),
            BattleCardMovementMode.ToOuterZone =>
                _spatial.MoveAlliesToOuterZone(targets.Allies),
            BattleCardMovementMode.ToWorldPoint when
                TryGetOperationPoint(out Vector2 point) =>
                _spatial.MoveAlliesToPoint(targets.Allies, point, true),
            BattleCardMovementMode.ToTargetFlank when
                FirstEnemy(ResolveTargets(
                    BattleCardTargetScope.Secondary)) is EnemyRuntime enemy =>
                _spatial.MoveAlliesToEnemyFlank(
                    targets.Allies,
                    enemy,
                    BattleSpatialDefaults.MovementStep,
                    true),
            _ => 0,
        };
        return new OperationOutcome(true, changed > 0, changed);
    }

    private OperationOutcome SwapTargets(TargetSet targets)
    {
        List<IBattleCharacter> candidates = new();
        foreach (IBattleCharacter ally in targets.Allies)
        {
            if (ally != null && !candidates.Contains(ally))
                candidates.Add(ally);
        }
        TargetSet secondary = ResolveTargets(BattleCardTargetScope.Secondary);
        foreach (IBattleCharacter ally in secondary.Allies)
        {
            if (ally != null && !candidates.Contains(ally))
                candidates.Add(ally);
        }
        bool changed = candidates.Count >= 2 &&
                       _spatial?.TrySwapAllies(
                           candidates[0],
                           candidates[1]) == true;
        return new OperationOutcome(true, changed, changed ? 2 : 0);
    }

    private OperationOutcome PullTargets(
        BattleCardOperationDefinition operation,
        TargetSet targets)
    {
        if (_spatial?.IsAvailable != true ||
            !TryGetOperationPoint(out Vector2 point))
        {
            return default;
        }
        int changed = _spatial.PullEnemiesTowardPoint(
            targets.Enemies,
            point,
            operation.Radius > 0f
                ? operation.Radius
                : BattleSpatialDefaults.MovementStep);
        return new OperationOutcome(true, changed > 0, changed);
    }

    private OperationOutcome CreateZone(
        BattleCardOperationDefinition operation)
    {
        if (!TryGetOperationPoint(out Vector2 point))
            return default;

        ZoneRuntime zone = new()
        {
            Operation = operation,
            Source = _active.Source,
            Point = point,
            DelayRemaining = operation.DelaySeconds,
            DurationRemaining = operation.Duration,
        };
        foreach (EnemyRuntime enemy in SelectEnemiesAtPoint(
                     point,
                     operation.Radius))
        {
            zone.Inside.Add(enemy);
        }
        _zones.Add(zone);
        return new OperationOutcome(true, true, 1);
    }

    private OperationOutcome AddActionModifier(
        BattleCardOperationDefinition operation,
        TargetSet targets,
        CharacterActionKind actionKind)
    {
        List<IBattleCharacter> sources = CollectAllies(targets);
        if (sources.Count == 0)
            return default;

        ActionModifierRuntime modifier = new()
        {
            ActionKind = actionKind,
            FlatDamage = actionKind == CharacterActionKind.Attack
                ? operation.Amount
                : 0,
            RepeatRatio = actionKind == CharacterActionKind.Attack &&
                          operation.Amount == 0 &&
                          operation.Ratio > 0f && operation.Ratio < 1f
                ? operation.Ratio
                : 0f,
            AppliedStatus = actionKind == CharacterActionKind.Attack
                ? operation.StatusEffect
                : null,
            AppliedStatusStacks = Mathf.Max(
                1,
                Mathf.RoundToInt(operation.StatusStacks)),
            AppliedStatusDuration = operation.StatusEffect == null
                ? 0f
                : operation.StatusEffect.DurationMode ==
                  StatusEffectDurationMode.Permanent
                    ? float.PositiveInfinity
                    : operation.StatusDuration,
            RequiredTargetStatus = operation.RequiredStatus,
            RemainingUses = Mathf.Max(1, operation.Count),
            RemainingDuration = operation.Duration > 0f
                ? operation.Duration
                : float.PositiveInfinity,
        };

        if (actionKind == CharacterActionKind.Skill)
        {
            if (operation.RequiredRole != null &&
                operation.RequiredCharacter == null)
            {
                modifier.StatusStackBonus = operation.Amount;
            }
            else
            {
                modifier.SkillCostReduction = operation.Amount;
            }
        }
        modifier.Sources.AddRange(sources);
        _actionModifiers.Add(modifier);
        return new OperationOutcome(true, true, sources.Count);
    }

    private OperationOutcome AddHealthOrKillTrigger(
        BattleCardOperationDefinition operation,
        TargetSet targets)
    {
        List<IBattleCharacter> allies = CollectAllies(targets);
        if (allies.Count == 0)
            return default;

        if (operation.Ratio > 0f)
        {
            HealthTriggerRuntime trigger = new()
            {
                HealthRatio = Mathf.Clamp01(operation.Ratio),
                HealAmount = operation.Amount,
                HarmfulRemovalCount = operation.Count,
                RequiredResourceStatus = operation.StatusEffect,
            };
            trigger.Targets.AddRange(allies);
            _healthTriggers.Add(trigger);
        }
        else
        {
            KillTriggerRuntime trigger = new()
            {
                EnergyGain = operation.Amount,
                RemainingDuration = operation.Duration,
            };
            trigger.Sources.AddRange(allies);
            _killTriggers.Add(trigger);
        }
        return new OperationOutcome(true, true, allies.Count);
    }

    private OperationOutcome ExtendStatusDuration(
        BattleCardOperationDefinition operation,
        TargetSet targets)
    {
        int changed = 0;
        foreach (EnemyRuntime enemy in targets.Enemies)
        {
            if (enemy?.TryExtendStatusDuration(
                    operation.StatusEffect,
                    operation.Duration) == true)
            {
                changed++;
            }
        }
        foreach (IBattleCharacter ally in targets.Allies)
        {
            if (ally is CharacterRuntime runtime &&
                runtime.TryExtendStatusDuration(
                    operation.StatusEffect,
                    operation.Duration))
            {
                changed++;
            }
        }
        return new OperationOutcome(true, changed > 0, changed);
    }

    private OperationOutcome AddForcedTarget(
        BattleCardOperationDefinition operation,
        TargetSet targets)
    {
        EnemyRuntime enemy = FirstEnemy(
            ResolveTargets(BattleCardTargetScope.Secondary));
        if (enemy == null)
            enemy = FirstEnemy(targets);
        TargetSet sourceTargets = targets.Faction == CharacterTargetFaction.Ally
            ? targets
            : ResolveTargets(BattleCardTargetScope.Primary);
        List<IBattleCharacter> allies = CollectAllies(sourceTargets);
        if (allies.Count == 0 &&
            operation.TargetScope == BattleCardTargetScope.Primary &&
            targets.Faction == CharacterTargetFaction.Enemy)
        {
            allies = CollectAllies(TargetSet.FromAllies(_allies));
        }
        if (enemy == null || allies.Count == 0)
            return default;

        foreach (IBattleCharacter ally in allies)
        {
            _forcedTargets.Add(new ForcedTargetRuntime
            {
                Source = ally,
                Target = enemy,
                RemainingDuration = operation.Duration,
            });
        }
        return new OperationOutcome(true, true, allies.Count);
    }

    private TargetSet ResolveTargets(
        BattleCardOperationDefinition operation)
    {
        if (operation == null)
            return TargetSet.EmptyEnemies;

        TargetSet targets = operation.TargetScope ==
                            BattleCardTargetScope.EnemiesAtDesignatedPoint
            ? ResolveEnemiesAtDesignatedPoint(operation.Radius)
            : ResolveTargets(operation.TargetScope);
        targets = FilterOperationTargets(operation, targets);
        return FilterTargetsByHealthCondition(operation.Condition, targets);
    }

    private TargetSet ResolveEnemiesAtDesignatedPoint(float radius)
    {
        return TryGetOperationPoint(out Vector2 point)
            ? TargetSet.FromEnemies(SelectEnemiesAtPoint(point, radius))
            : TargetSet.EmptyEnemies;
    }

    private TargetSet ResolveTargets(BattleCardTargetScope scope)
    {
        if (_active == null)
            return TargetSet.EmptyEnemies;

        switch (scope)
        {
            case BattleCardTargetScope.Primary:
                return _active.Card.TargetFaction ==
                       CharacterTargetFaction.Ally
                    ? TargetSet.FromAllies(_active.PrimaryAllies)
                    : TargetSet.FromEnemies(_active.PrimaryEnemies);

            case BattleCardTargetScope.Secondary:
                return _active.Card.SecondaryTarget.TargetFaction ==
                       CharacterTargetFaction.Ally
                    ? TargetSet.FromAllies(_active.SecondaryAllies)
                    : TargetSet.FromEnemies(_active.SecondaryEnemies);

            case BattleCardTargetScope.Source:
                return TargetSet.FromAllies(
                    _active.Source != null
                        ? new IBattleCharacter[] { _active.Source }
                        : Array.Empty<IBattleCharacter>());

            case BattleCardTargetScope.AllEnemies:
                return TargetSet.FromEnemies(SelectAllEnemies());

            case BattleCardTargetScope.AllAllies:
                return TargetSet.FromAllies(_allies.ToArray());

            case BattleCardTargetScope.RandomEnemies:
                return TargetSet.FromEnemies(
                    _board.SelectCharacterTargets(
                        _active.Source,
                        CharacterAttackSubject.Random,
                        CharacterAttackSubjectMetric.Health,
                        int.MaxValue,
                        CharacterConditionMatchMode.All,
                        Array.Empty<CharacterNumericCondition>()));

            case BattleCardTargetScope.EnemiesWithStatus:
                return TargetSet.FromEnemies(SelectAllEnemies());

            case BattleCardTargetScope.AlliesWithRole:
                return TargetSet.FromAllies(_allies.ToArray());

            case BattleCardTargetScope.NearbyPrimaryEnemies:
            {
                EnemyRuntime primary = FirstEnemy(
                    ResolveTargets(BattleCardTargetScope.Primary));
                return TargetSet.FromEnemies(
                    primary != null && _spatial != null
                        ? _spatial.SelectNearbyEnemies(
                            BattleStatusTarget.FromEnemy(primary),
                            BattleSpatialDefaults.NearbyRadius,
                            0,
                            false)
                        : Array.Empty<EnemyRuntime>());
            }

            case BattleCardTargetScope.BehindPrimaryEnemy:
            {
                EnemyRuntime primary = FirstEnemy(
                    ResolveTargets(BattleCardTargetScope.Primary));
                return TargetSet.FromEnemies(
                    primary != null && _spatial != null
                        ? _spatial.SelectEnemiesBehind(primary)
                        : Array.Empty<EnemyRuntime>());
            }

            case BattleCardTargetScope.DefenseLineEnemies:
                return TargetSet.FromEnemies(
                    _spatial?.SelectDefenseLineEnemies() ??
                    Array.Empty<EnemyRuntime>());

            case BattleCardTargetScope.RecentObjectiveAttackers:
                return TargetSet.FromEnemies(
                    _spatial?.SelectRecentCoreAttackers() ??
                    Array.Empty<EnemyRuntime>());

            case BattleCardTargetScope.LowestHealthAlly:
                return TargetSet.FromAllies(SelectLowestHealthAlly(false));

            case BattleCardTargetScope.DeadOrLowestHealthAlly:
                return TargetSet.FromAllies(SelectLowestHealthAlly(true));

            case BattleCardTargetScope.SpecificCharacter:
                return TargetSet.FromAllies(_allies.ToArray());

            case BattleCardTargetScope.EnemiesAtDesignatedPoint:
                return ResolveEnemiesAtDesignatedPoint(
                    BattleSpatialDefaults.NearbyRadius);

            default:
                return TargetSet.EmptyEnemies;
        }
    }

    private TargetSet FilterOperationTargets(
        BattleCardOperationDefinition operation,
        TargetSet targets)
    {
        if (targets.Faction == CharacterTargetFaction.Ally)
        {
            List<IBattleCharacter> result = new();
            foreach (IBattleCharacter ally in targets.Allies)
            {
                if (ally == null ||
                    !MatchesCharacter(
                        ally,
                        operation.RequiredCharacter,
                        operation.RequiredRole) ||
                    (FiltersCurrentTargetByRequiredStatus(operation) &&
                     !ally.HasStatusEffect(operation.RequiredStatus)))
                {
                    continue;
                }
                result.Add(ally);
                if (operation.TargetScope ==
                        BattleCardTargetScope.LowestHealthAlly ||
                    operation.TargetScope ==
                        BattleCardTargetScope.DeadOrLowestHealthAlly)
                {
                    break;
                }
            }
            return TargetSet.FromAllies(result);
        }

        List<EnemyRuntime> enemies = new();
        foreach (EnemyRuntime enemy in targets.Enemies)
        {
            if (enemy == null ||
                (FiltersCurrentTargetByRequiredStatus(operation) &&
                 !enemy.HasStatusEffect(operation.RequiredStatus)))
            {
                continue;
            }
            enemies.Add(enemy);
            if ((operation.TargetScope ==
                     BattleCardTargetScope.RandomEnemies ||
                 operation.TargetScope ==
                     BattleCardTargetScope.NearbyPrimaryEnemies) &&
                operation.Count > 0 &&
                enemies.Count >= Mathf.Max(1, operation.Count))
            {
                break;
            }
        }
        return TargetSet.FromEnemies(enemies);
    }

    private static bool FiltersCurrentTargetByRequiredStatus(
        BattleCardOperationDefinition operation)
    {
        return operation.RequiredStatus != null &&
               operation.Type !=
                   BattleCardOperationType.ApplyAttackModifier &&
               operation.Type !=
                   BattleCardOperationType.ApplySkillModifier;
    }

    private TargetSet FilterTargetsByHealthCondition(
        BattleCardConditionDefinition condition,
        TargetSet targets)
    {
        if (condition?.Type !=
            BattleCardConditionType.TargetHealthPercentage)
        {
            return targets;
        }

        if (targets.Faction == CharacterTargetFaction.Ally)
        {
            List<IBattleCharacter> allies = new();
            foreach (IBattleCharacter ally in targets.Allies)
            {
                float percentage = ally?.MaximumHealth > 0
                    ? ally.CurrentHealth * 100f / ally.MaximumHealth
                    : 0f;
                if (Compare(
                        percentage,
                        condition.Threshold,
                        condition.Comparison))
                {
                    allies.Add(ally);
                }
            }
            return TargetSet.FromAllies(allies);
        }

        List<EnemyRuntime> enemies = new();
        foreach (EnemyRuntime enemy in targets.Enemies)
        {
            float percentage = enemy?.MaxHealth > 0
                ? enemy.Health * 100f / enemy.MaxHealth
                : 0f;
            if (Compare(
                    percentage,
                    condition.Threshold,
                    condition.Comparison))
            {
                enemies.Add(enemy);
            }
        }
        return TargetSet.FromEnemies(enemies);
    }

    private bool AllowsOperation(
        BattleCardOperationDefinition operation,
        TargetSet targets)
    {
        BattleCardConditionDefinition condition = operation.Condition;
        if (condition == null || !condition.IsConfigured ||
            condition.Type == BattleCardConditionType.TargetHealthPercentage)
        {
            return condition?.Type !=
                       BattleCardConditionType.TargetHealthPercentage ||
                   targets.Count > 0;
        }

        float actual;
        switch (condition.Type)
        {
            case BattleCardConditionType.ObjectiveHealthPercentage:
                actual = _objective?.MaximumHealth > 0
                    ? _objective.CurrentHealth * 100f /
                      _objective.MaximumHealth
                    : 0f;
                break;

            case BattleCardConditionType.HandCount:
                actual = Mathf.Max(0, _deck.Hand.Count - 1);
                break;

            case BattleCardConditionType.PartyRoleCount:
                actual = CountAlliesWithRole(condition.Role);
                break;

            case BattleCardConditionType.DistinctAllyZoneCount:
                actual = CountDistinctAllyZones();
                break;

            case BattleCardConditionType.TargetZone:
                return AnyTargetInZone(targets, condition.Zone);

            case BattleCardConditionType.TargetHasStatus:
                return AnyTargetHasStatus(targets, condition.StatusEffect);

            case BattleCardConditionType.PreviousOperationSucceeded:
                return _active.PreviousChangedCount > 0;

            case BattleCardConditionType.PreviousOperationFailed:
                return _active.PreviousChangedCount == 0;

            case BattleCardConditionType.PreviousOperationDefeatedAny:
                return _active.PreviousDefeatedCount > 0;

            case BattleCardConditionType.MatchingTargetCount:
                actual = targets.Count;
                break;

            default:
                return true;
        }
        return Compare(actual, condition.Threshold, condition.Comparison);
    }

    private void TickActionModifiers(float deltaTime)
    {
        for (int index = _actionModifiers.Count - 1; index >= 0; index--)
        {
            ActionModifierRuntime modifier = _actionModifiers[index];
            if (!float.IsPositiveInfinity(modifier.RemainingDuration))
            {
                modifier.RemainingDuration = Mathf.Max(
                    0f,
                    modifier.RemainingDuration - deltaTime);
            }
            if (modifier.RemainingUses <= 0 ||
                modifier.RemainingDuration <= 0f)
            {
                _actionModifiers.RemoveAt(index);
            }
        }
    }

    private void TickForcedTargets(float deltaTime)
    {
        for (int index = _forcedTargets.Count - 1; index >= 0; index--)
        {
            ForcedTargetRuntime forced = _forcedTargets[index];
            forced.RemainingDuration = Mathf.Max(
                0f,
                forced.RemainingDuration - deltaTime);
            if (forced.RemainingDuration <= 0f ||
                forced.Source == null || forced.Target == null ||
                forced.Target.Health <= 0)
            {
                _forcedTargets.RemoveAt(index);
            }
        }
    }

    private void TickHealthTriggers()
    {
        for (int triggerIndex = _healthTriggers.Count - 1;
             triggerIndex >= 0;
             triggerIndex--)
        {
            HealthTriggerRuntime trigger = _healthTriggers[triggerIndex];
            bool consumed = false;
            foreach (IBattleCharacter target in trigger.Targets)
            {
                if (target == null || target.CurrentHealth <= 0 ||
                    target.MaximumHealth <= 0 ||
                    target.CurrentHealth >
                    target.MaximumHealth * trigger.HealthRatio ||
                    trigger.RequiredResourceStatus != null &&
                    !target.HasStatusEffect(
                        trigger.RequiredResourceStatus))
                {
                    continue;
                }

                int healed = target.Heal(trigger.HealAmount);
                if (trigger.HarmfulRemovalCount > 0)
                {
                    _board.TryRemoveAlliedCharacterStatus(
                        target,
                        new[] { target },
                        new CharacterStatusRemovalSelection(
                            CharacterStatusRemovalTarget.Debuff,
                            null,
                            null,
                            CharacterStatusRemovalPickMode.RandomCount,
                            trigger.HarmfulRemovalCount),
                        CharacterStatusRemovalAmount.Fixed(0));
                }
                if (healed > 0)
                {
                    if (trigger.RequiredResourceStatus != null)
                    {
                        target.TryConsumeStatusStacks(
                            trigger.RequiredResourceStatus,
                            1);
                    }
                    consumed = true;
                }
            }
            if (consumed)
                _healthTriggers.RemoveAt(triggerIndex);
        }
    }

    private void TickKillTriggers(float deltaTime)
    {
        for (int index = _killTriggers.Count - 1; index >= 0; index--)
        {
            KillTriggerRuntime trigger = _killTriggers[index];
            trigger.RemainingDuration = Mathf.Max(
                0f,
                trigger.RemainingDuration - deltaTime);
            if (trigger.RemainingDuration <= 0f)
                _killTriggers.RemoveAt(index);
        }
    }

    private void TickZones(float deltaTime)
    {
        for (int index = _zones.Count - 1; index >= 0; index--)
        {
            ZoneRuntime zone = _zones[index];
            BattleCardOperationDefinition operation = zone.Operation;
            if (operation.ZoneTrigger == BattleCardZoneTrigger.AfterDelay)
            {
                zone.DelayRemaining = Mathf.Max(
                    0f,
                    zone.DelayRemaining - deltaTime);
                if (!zone.Triggered && zone.DelayRemaining <= 0f)
                {
                    zone.Triggered = true;
                    ApplyZoneEffect(
                        zone,
                        SelectEnemiesAtPoint(zone.Point, operation.Radius));
                }
                if (zone.Triggered)
                    _zones.RemoveAt(index);
                continue;
            }

            bool expiresByDuration = operation.Duration > 0f;
            if (expiresByDuration)
            {
                zone.DurationRemaining = Mathf.Max(
                    0f,
                    zone.DurationRemaining - deltaTime);
            }
            IReadOnlyList<EnemyRuntime> current = SelectEnemiesAtPoint(
                zone.Point,
                operation.Radius);
            HashSet<EnemyRuntime> currentSet = new(current);
            List<EnemyRuntime> entrants = new();
            foreach (EnemyRuntime enemy in current)
            {
                if (enemy == null || zone.Inside.Contains(enemy) ||
                    (operation.OncePerTarget && zone.Affected.Contains(enemy)))
                {
                    continue;
                }
                entrants.Add(enemy);
                zone.Affected.Add(enemy);
                if (operation.Duration <= 0f)
                    break;
            }
            zone.Inside.Clear();
            foreach (EnemyRuntime enemy in currentSet)
                zone.Inside.Add(enemy);
            if (entrants.Count > 0)
            {
                ApplyZoneEffect(zone, entrants);
                if (operation.Duration <= 0f)
                    zone.Triggered = true;
            }
            if (zone.Triggered ||
                expiresByDuration && zone.DurationRemaining <= 0f)
                _zones.RemoveAt(index);
        }
    }

    private void ApplyZoneEffect(
        ZoneRuntime zone,
        IReadOnlyList<EnemyRuntime> enemies)
    {
        CharacterEffectDefinition effect = zone.Operation.SharedEffect;
        if (effect == null || enemies == null || enemies.Count == 0)
            return;
        BattleEffectContext context = BattleEffectContext.ForBattleCard(
            zone.Source,
            _board,
            _resource,
            CharacterTargetFaction.Enemy,
            enemies,
            null,
            zone.Source?.CurrentAttackPower ?? 0f,
            _deck,
            zone.Source != null);
        BattleEffectExecutor.ExecuteEffect(
            context,
            effect,
            zone.Source?.Data,
            actionId: "battle_card_zone");
    }

    private void HandleEffectResolved(BattleEffectResolvedEvent eventData)
    {
        if (_handlingFollowUp || !eventData.IsValid ||
            !eventData.Result.Succeeded || eventData.Source.Ally == null)
        {
            return;
        }

        CharacterActionKind actionKind = ToActionKind(eventData.OriginKind);
        bool isDamage = eventData.Effect.BattleEffectType ==
                        BattleEffectType.Damage;
        bool isStatus = eventData.Effect.BattleEffectType ==
                        BattleEffectType.ApplyStatus;
        if (!isDamage && !isStatus)
            return;

        for (int index = _actionModifiers.Count - 1; index >= 0; index--)
        {
            ActionModifierRuntime modifier = _actionModifiers[index];
            if (!modifier.AppliesTo(eventData.Source.Ally) ||
                modifier.ActionKind != actionKind)
            {
                continue;
            }

            if (isStatus && modifier.StatusStackBonus > 0)
            {
                if (modifier.LastConsumedStatusExecutionId !=
                    eventData.ActionExecutionId)
                {
                    modifier.LastConsumedStatusExecutionId =
                        eventData.ActionExecutionId;
                    modifier.RemainingUses--;
                }
                continue;
            }
            if (!isDamage)
                continue;

            List<EnemyRuntime> targets = new();
            foreach (BattleStatusTarget target in eventData.Targets)
            {
                if (target.Enemy == null || target.Enemy.Health <= 0 ||
                    (modifier.RequiredTargetStatus != null &&
                     !target.Enemy.HasStatusEffect(
                         modifier.RequiredTargetStatus)))
                {
                    continue;
                }
                targets.Add(target.Enemy);
            }
            if (targets.Count == 0)
                continue;
            if (modifier.LastProcessedDamageExecutionId ==
                eventData.ActionExecutionId)
            {
                continue;
            }
            modifier.LastProcessedDamageExecutionId =
                eventData.ActionExecutionId;

            _handlingFollowUp = true;
            try
            {
                if (modifier.FlatDamage > 0)
                {
                    _board.TryDamageCharacterTargets(
                        eventData.Source.Ally,
                        targets,
                        modifier.FlatDamage,
                        eventData.Effect.DamageType,
                        false);
                }
                if (modifier.RepeatRatio > 0f)
                {
                    int repeated = Mathf.Max(
                        1,
                        Mathf.RoundToInt(
                            (eventData.Result.ResolvedAmount > 0
                                ? eventData.Result.ResolvedAmount
                                : eventData.Result.DamageDealt /
                                  Mathf.Max(1, targets.Count)) *
                            modifier.RepeatRatio));
                    _board.TryDamageCharacterTargets(
                        eventData.Source.Ally,
                        targets,
                        repeated,
                        eventData.Effect.DamageType,
                        false);
                }
                if (modifier.AppliedStatus != null)
                {
                    _board.TryApplyCharacterStatus(
                        eventData.Source.Ally,
                        targets,
                        modifier.AppliedStatus,
                        modifier.AppliedStatus.DurationMode ==
                            StatusEffectDurationMode.Permanent
                                ? float.PositiveInfinity
                                : Mathf.Max(
                                    TimePrecision.Step,
                                    modifier.AppliedStatusDuration),
                        modifier.AppliedStatusStacks,
                        modifier.AppliedStatus.TickInterval,
                        false);
                }
            }
            finally
            {
                _handlingFollowUp = false;
            }
            ConsumeModifier(index, modifier);
        }
    }

    private void ConsumeModifier(
        int index,
        ActionModifierRuntime modifier)
    {
        modifier.RemainingUses--;
        if (modifier.RemainingUses <= 0 &&
            index >= 0 && index < _actionModifiers.Count &&
            ReferenceEquals(_actionModifiers[index], modifier))
        {
            _actionModifiers.RemoveAt(index);
        }
    }

    private void HandleEnemyDefeated(BattleEnemyDefeatedEvent eventData)
    {
        if (!eventData.IsValid || eventData.Killer == null)
            return;
        for (int index = _killTriggers.Count - 1; index >= 0; index--)
        {
            KillTriggerRuntime trigger = _killTriggers[index];
            if (!trigger.Sources.Contains(eventData.Killer))
                continue;
            _resource?.TryGain(trigger.EnergyGain);
            _killTriggers.RemoveAt(index);
        }
    }

    private IReadOnlyList<EnemyRuntime> SelectAllEnemies()
    {
        CharacterRuntime source = _active?.Source;
        if (source == null)
        {
            foreach (IBattleCharacter ally in _allies)
            {
                if (ally is CharacterRuntime runtime &&
                    runtime.CurrentHealth > 0)
                {
                    source = runtime;
                    break;
                }
            }
        }
        return _board?.SelectCharacterTargets(
                   source,
                   CharacterAttackSubject.All,
                   CharacterAttackSubjectMetric.Health,
                   int.MaxValue,
                   CharacterConditionMatchMode.All,
                   Array.Empty<CharacterNumericCondition>()) ??
               Array.Empty<EnemyRuntime>();
    }

    private IReadOnlyList<EnemyRuntime> SelectEnemiesAtPoint(
        Vector2 point,
        float radius)
    {
        if (_spatial?.IsAvailable != true)
            return Array.Empty<EnemyRuntime>();
        radius = Mathf.Max(0f, radius);
        float radiusSquared = radius * radius;
        List<EnemyRuntime> result = new();
        foreach (EnemyRuntime enemy in SelectAllEnemies())
        {
            if (enemy != null &&
                _spatial.TryGetUnitPosition(
                    BattleStatusTarget.FromEnemy(enemy),
                    out Vector2 position) &&
                (position - point).sqrMagnitude <= radiusSquared)
            {
                result.Add(enemy);
            }
        }
        return result;
    }

    private IReadOnlyList<IBattleCharacter> SelectLowestHealthAlly(
        bool preferDefeated)
    {
        IBattleCharacter selected = null;
        float selectedRatio = float.MaxValue;
        if (preferDefeated)
        {
            foreach (IBattleCharacter ally in _allies)
            {
                if (ally != null && ally.CurrentHealth <= 0)
                    return new[] { ally };
            }
        }
        foreach (IBattleCharacter ally in _allies)
        {
            if (ally == null || ally.CurrentHealth <= 0 ||
                ally.MaximumHealth <= 0)
            {
                continue;
            }
            float ratio = ally.CurrentHealth / (float)ally.MaximumHealth;
            if (ratio < selectedRatio)
            {
                selected = ally;
                selectedRatio = ratio;
            }
        }
        return selected != null
            ? new[] { selected }
            : Array.Empty<IBattleCharacter>();
    }

    private bool TryGetOperationPoint(out Vector2 point)
    {
        if (_active?.HasSecondaryPoint == true)
        {
            point = _active.SecondaryPoint;
            return true;
        }
        if (_active?.HasPrimaryPoint == true)
        {
            point = _active.PrimaryPoint;
            return true;
        }
        point = default;
        return false;
    }

    private int ResolveAmount(BattleCardOperationDefinition operation)
    {
        return operation.UsePreviousChangedCount
            ? BattleValueMath.SaturatingAddNonNegative(
                operation.Amount,
                _active?.PreviousChangedCount ?? 0)
            : operation.Amount;
    }

    private static int CountExplicitRemovalStacks(
        CharacterEffectDefinition effect,
        TargetSet targets)
    {
        if (effect == null ||
            effect.BattleEffectType != BattleEffectType.RemoveStatus)
        {
            return 0;
        }

        CharacterStatusRemovalSelection selection =
            effect.StatusRemovalSelection;
        if (!selection.HasExplicitStatus)
            return 0;

        int total = 0;
        for (int statusIndex = 0;
             statusIndex < selection.ExplicitStatusCount;
             statusIndex++)
        {
            StatusEffectSO status = selection.GetExplicitStatus(statusIndex);
            if (status == null)
                continue;

            foreach (EnemyRuntime enemy in targets.Enemies)
            {
                if (enemy == null)
                    continue;
                total = BattleValueMath.SaturatingAddNonNegative(
                    total,
                    BattleStatusTarget.FromEnemy(enemy)
                        .GetStatusStackCount(status));
            }
            foreach (IBattleCharacter ally in targets.Allies)
            {
                total = BattleValueMath.SaturatingAddNonNegative(
                    total,
                    ally?.GetStatusStackCount(status) ?? 0);
            }
        }
        return total;
    }

    private int CountAlliesWithRole(CharacterRoleSO role)
    {
        int count = 0;
        foreach (IBattleCharacter ally in _allies)
        {
            if (MatchesCharacter(ally, null, role))
                count++;
        }
        return count;
    }

    private int CountDistinctAllyZones()
    {
        if (_spatial == null)
            return 0;
        HashSet<BattleSpatialZone> zones = new();
        foreach (IBattleCharacter ally in _allies)
        {
            if (ally == null || ally.CurrentHealth <= 0)
                continue;
            BattleSpatialZone zone = _spatial.GetUnitZone(
                BattleStatusTarget.FromAlly(ally));
            if (zone != BattleSpatialZone.Unknown)
                zones.Add(zone);
        }
        return zones.Count;
    }

    private bool AnyTargetInZone(
        TargetSet targets,
        BattleCardSpatialZone zone)
    {
        if (_spatial == null)
            return false;
        BattleSpatialZone expected = zone switch
        {
            BattleCardSpatialZone.Inner => BattleSpatialZone.Inner,
            BattleCardSpatialZone.Outer => BattleSpatialZone.Outer,
            BattleCardSpatialZone.DefenseLine =>
                BattleSpatialZone.DefenseLine,
            _ => BattleSpatialZone.Unknown,
        };
        foreach (IBattleCharacter ally in targets.Allies)
        {
            if (_spatial.GetUnitZone(BattleStatusTarget.FromAlly(ally)) ==
                expected)
            {
                return true;
            }
        }
        foreach (EnemyRuntime enemy in targets.Enemies)
        {
            if (_spatial.GetUnitZone(BattleStatusTarget.FromEnemy(enemy)) ==
                expected)
            {
                return true;
            }
        }
        return false;
    }

    private static bool AnyTargetHasStatus(
        TargetSet targets,
        StatusEffectSO status)
    {
        if (status == null)
            return false;
        foreach (IBattleCharacter ally in targets.Allies)
        {
            if (ally?.HasStatusEffect(status) == true)
                return true;
        }
        foreach (EnemyRuntime enemy in targets.Enemies)
        {
            if (enemy?.HasStatusEffect(status) == true)
                return true;
        }
        return false;
    }

    private static bool MatchesCharacter(
        IBattleCharacter ally,
        CharacterSO character,
        CharacterRoleSO role)
    {
        if (ally is not CharacterRuntime runtime ||
            runtime.Definition == null)
        {
            return character == null && role == null;
        }
        if (character != null &&
            !ReferenceEquals(runtime.Definition, character) &&
            !string.Equals(
                runtime.Definition.CharacterId,
                character.CharacterId,
                StringComparison.Ordinal))
        {
            return false;
        }
        return role == null || ReferenceEquals(runtime.Definition.Role, role);
    }

    private static List<IBattleCharacter> CollectAllies(TargetSet targets)
    {
        List<IBattleCharacter> result = new();
        foreach (IBattleCharacter ally in targets.Allies)
        {
            if (ally != null && ally.CurrentHealth > 0 &&
                !result.Contains(ally))
            {
                result.Add(ally);
            }
        }
        return result;
    }

    private static IBattleCharacter FirstAlly(TargetSet targets)
    {
        foreach (IBattleCharacter ally in targets.Allies)
        {
            if (ally != null)
                return ally;
        }
        return null;
    }

    private static EnemyRuntime FirstEnemy(TargetSet targets)
    {
        foreach (EnemyRuntime enemy in targets.Enemies)
        {
            if (enemy != null)
                return enemy;
        }
        return null;
    }

    private static CharacterActionKind ToActionKind(
        BattleEffectOriginKind originKind)
    {
        return originKind switch
        {
            BattleEffectOriginKind.CharacterAttack =>
                CharacterActionKind.Attack,
            BattleEffectOriginKind.CharacterSkill =>
                CharacterActionKind.Skill,
            BattleEffectOriginKind.CharacterPassive =>
                CharacterActionKind.Passive,
            _ => default,
        };
    }

    private static bool Compare(
        float actual,
        float threshold,
        CharacterNumericComparison comparison)
    {
        return comparison switch
        {
            CharacterNumericComparison.LessThanOrEqual =>
                actual <= threshold,
            CharacterNumericComparison.GreaterThan => actual > threshold,
            CharacterNumericComparison.LessThan => actual < threshold,
            CharacterNumericComparison.Equal =>
                Mathf.Approximately(actual, threshold),
            CharacterNumericComparison.NotEqual =>
                !Mathf.Approximately(actual, threshold),
            _ => actual >= threshold,
        };
    }
}
