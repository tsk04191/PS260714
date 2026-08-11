using System;
using System.Collections.Generic;
using UnityEngine;

public interface IActiveSkillResource
{
    int Current { get; }
    int Maximum { get; }
    event Action<int> Changed;

    bool CanSpend(int amount);
    bool TrySpend(int amount);
    bool TryGain(int amount);
}

public readonly struct BattleEffectResult
{
    public bool Attempted { get; }
    public bool Succeeded { get; }
    public bool Changed => Succeeded;
    public int DamageDealt { get; }

    public BattleEffectResult(
        bool attempted,
        bool succeeded,
        int damageDealt = 0)
    {
        Attempted = attempted;
        Succeeded = succeeded;
        DamageDealt = Mathf.Max(0, damageDealt);
    }

    public BattleEffectResult Combine(BattleEffectResult other)
    {
        return new BattleEffectResult(
            Attempted || other.Attempted,
            Succeeded || other.Succeeded,
            DamageDealt + other.DamageDealt);
    }

    public static BattleEffectResult Combine(
        BattleEffectResult left,
        BattleEffectResult right)
    {
        return left.Combine(right);
    }
}

public static class BattleEffectExecutor
{
    private readonly struct PreparedEffect
    {
        public bool IsPrepared { get; }
        public BattleEffectContext Context { get; }
        public int ResourceSpendAmount { get; }
        public int HealthSpendAmount { get; }

        public PreparedEffect(
            BattleEffectContext context,
            int resourceSpendAmount,
            int healthSpendAmount)
        {
            IsPrepared = true;
            Context = context;
            ResourceSpendAmount = Mathf.Max(0, resourceSpendAmount);
            HealthSpendAmount = Mathf.Max(0, healthSpendAmount);
        }
    }

    private readonly struct EnemyAmountSnapshot
    {
        public EnemyRuntime Target { get; }
        public int Amount { get; }

        public EnemyAmountSnapshot(EnemyRuntime target, int amount)
        {
            Target = target;
            Amount = Mathf.Max(0, amount);
        }
    }

    private readonly struct AllyAmountSnapshot
    {
        public IBattleCharacter Target { get; }
        public int Amount { get; }

        public AllyAmountSnapshot(
            IBattleCharacter target,
            int amount)
        {
            Target = target;
            Amount = Mathf.Max(0, amount);
        }
    }

    public static BattleEffectResult ExecuteAbility(
        BattleEffectContext context,
        IBattleAbilityDefinition ability,
        CharacterData sourceData = null,
        int amountMultiplier = 1,
        Func<int, IBattleCharacter, bool>
            inheritedEnemyDamageFallback = null)
    {
        if (ability == null || !ability.HasExecutableContent)
            return default;

        List<IBattleEffectDefinition> effects = new();
        IEnumerable<IBattleEffectDefinition> authoredEffects =
            ability.BattleEffects;
        if (authoredEffects != null)
        {
            foreach (IBattleEffectDefinition effect in authoredEffects)
            {
                if (effect != null)
                    effects.Add(effect);
            }
        }

        return ExecuteSequence(
            context,
            effects,
            sourceData,
            amountMultiplier,
            inheritedEnemyDamageFallback);
    }

    public static BattleEffectResult ExecuteSequence(
        BattleEffectContext context,
        IReadOnlyList<IBattleEffectDefinition> effects,
        CharacterData sourceData = null,
        int amountMultiplier = 1,
        Func<int, IBattleCharacter, bool>
            inheritedEnemyDamageFallback = null)
    {
        if (effects == null || effects.Count == 0)
            return default;

        PreparedEffect[] prepared = new PreparedEffect[effects.Count];
        bool hasPreparedEffect = false;
        for (int index = 0; index < effects.Count; index++)
        {
            IBattleEffectDefinition effect = effects[index];
            if (!IsUsable(effect) ||
                !TryResolveContext(
                    context,
                    effect,
                    out BattleEffectContext effectContext))
            {
                if (effect != null &&
                    effect.BattlePreconditionFailurePolicy ==
                        BattleEffectPreconditionFailurePolicy.AbortSequence)
                {
                    return default;
                }
                continue;
            }

            int resourceSpendAmount = 0;
            int healthSpendAmount = 0;
            if (effect.BattleEffectType ==
                BattleEffectType.SpendResource)
            {
                resourceSpendAmount = CalculateAmount(
                    effect,
                    effectContext,
                    sourceData,
                    false,
                    amountMultiplier);
                if (resourceSpendAmount <= 0 ||
                    effectContext.Resource?.CanSpend(
                        resourceSpendAmount) != true)
                {
                    if (effect.BattlePreconditionFailurePolicy ==
                        BattleEffectPreconditionFailurePolicy.AbortSequence)
                    {
                        return default;
                    }
                    continue;
                }
            }
            else if (effect.BattleEffectType ==
                     BattleEffectType.SpendHealth)
            {
                healthSpendAmount = CalculateAmount(
                    effect,
                    effectContext,
                    sourceData,
                    false,
                    amountMultiplier);
                if (healthSpendAmount <= 0 ||
                    !effectContext.SourceTarget.CanSpendHealth(
                        healthSpendAmount))
                {
                    if (effect.BattlePreconditionFailurePolicy ==
                        BattleEffectPreconditionFailurePolicy.AbortSequence)
                    {
                        return default;
                    }
                    continue;
                }
            }

            prepared[index] = new PreparedEffect(
                effectContext,
                resourceSpendAmount,
                healthSpendAmount);
            hasPreparedEffect = true;
        }

        if (!hasPreparedEffect)
            return default;

        BattleEffectResult combined = default;
        bool showAttackRange = true;
        for (int index = 0; index < effects.Count; index++)
        {
            if (!prepared[index].IsPrepared)
                continue;

            IBattleEffectDefinition effect = effects[index];
            BattleEffectResult current = ExecuteEffect(
                prepared[index].Context,
                effect,
                sourceData,
                amountMultiplier,
                showAttackRange,
                prepared[index].ResourceSpendAmount,
                prepared[index].HealthSpendAmount,
                inheritedEnemyDamageFallback);
            combined = combined.Combine(current);
            if (current.Attempted &&
                effect.BattleTargetMode ==
                    BattleEffectTargetMode.InheritContext &&
                prepared[index].Context.TargetFaction ==
                    CharacterTargetFaction.Enemy)
            {
                showAttackRange = false;
            }
            if (!current.Succeeded &&
                effect.BattleFailurePolicy ==
                    BattleEffectFailurePolicy.StopRemainingEffects)
            {
                break;
            }
        }

        return combined;
    }

    public static BattleEffectResult ExecuteEffect(
        BattleEffectContext context,
        IBattleEffectDefinition effect,
        CharacterData sourceData = null,
        int amountMultiplier = 1,
        bool showAttackRange = true,
        int preparedResourceSpendAmount = 0,
        int preparedHealthSpendAmount = 0,
        Func<int, IBattleCharacter, bool>
            inheritedEnemyDamageFallback = null,
        string actionId = null)
    {
        if (!IsUsable(effect))
            return default;

        amountMultiplier = Mathf.Max(1, amountMultiplier);
        BattleEffectContext effectContext =
            context.SnapshotSourceStatus(
                effect.SourceStatusScalingEffect);
        IReadOnlyList<BattleStatusTarget> livingDamageTargets =
            CaptureLivingDamageTargets(effectContext, effect);
        BattleEffectResult result;
        switch (effect.BattleEffectType)
        {
            case BattleEffectType.Damage:
                result = ExecuteDamage(
                    effectContext,
                    effect,
                    sourceData,
                    amountMultiplier,
                    showAttackRange,
                    inheritedEnemyDamageFallback,
                    actionId);
                break;

            case BattleEffectType.ApplyStatus:
            {
                if (effect.StatusEffect == null ||
                    effectContext.Board == null)
                {
                    return default;
                }

                string effectId = effect is CharacterEffectDefinition
                    characterEffect
                        ? characterEffect.EffectId
                        : string.Empty;
                CharacterActionKind actionKind = ResolveActionKind(
                    effectContext.OriginKind);
                float duration = sourceData != null
                    ? sourceData.ResolveStatusDuration(
                        effect.StatusDuration,
                        actionKind,
                        actionId,
                        effectId)
                    : effect.StatusDuration;
                if (effectContext.StatusEffectsLastUntilBattleEnd)
                    duration = float.PositiveInfinity;
                float resolvedStacks = sourceData != null
                    ? sourceData.ResolveStatusStacks(
                        effect.StatusStacks,
                        actionKind,
                        actionId,
                        effectId)
                    : effect.StatusStacks;
                float stacks = Mathf.Min(
                    float.MaxValue,
                    resolvedStacks * amountMultiplier);
                bool changed = effectContext.TargetFaction ==
                               CharacterTargetFaction.Ally
                    ? effectContext.Board.TryApplyAlliedCharacterStatus(
                        effectContext.Source,
                        effectContext.AllyTargets,
                        effect.StatusEffect,
                        duration,
                        stacks)
                    : effectContext.Board.TryApplyCharacterStatus(
                        effectContext.Source,
                        effectContext.EnemyTargets,
                        effect.StatusEffect,
                        duration,
                        stacks,
                        effect.StatusEffect.TickInterval,
                        showAttackRange);
                result = new BattleEffectResult(true, changed);
                break;
            }

            case BattleEffectType.RemoveStatus:
            {
                if (effectContext.Board == null ||
                    (effect.StatusRemovalTarget ==
                         CharacterStatusRemovalTarget.Single &&
                     !effect.StatusRemovalSelection.HasExplicitStatus))
                {
                    return default;
                }

                CharacterStatusRemovalAmount removalAmount =
                    effect.StatusRemovalAmount.Multiply(amountMultiplier);
                bool changed = effectContext.TargetFaction ==
                               CharacterTargetFaction.Ally
                    ? effectContext.Board.TryRemoveAlliedCharacterStatus(
                        effectContext.Source,
                        effectContext.AllyTargets,
                        effect.StatusRemovalSelection,
                        removalAmount)
                    : effectContext.Board.TryRemoveCharacterStatus(
                        effectContext.Source,
                        effectContext.EnemyTargets,
                        effect.StatusRemovalSelection,
                        removalAmount,
                        showAttackRange);
                result = new BattleEffectResult(true, changed);
                break;
            }

            case BattleEffectType.GainResource:
            {
                int amount = CalculateAmount(
                    effect,
                    effectContext,
                    sourceData,
                    false,
                    amountMultiplier,
                    actionId);
                if (amount <= 0)
                {
                    result = new BattleEffectResult(true, false);
                    break;
                }
                bool changed =
                    effectContext.Resource?.TryGain(amount) == true;
                result = new BattleEffectResult(true, changed);
                break;
            }

            case BattleEffectType.SpendResource:
            {
                int amount = preparedResourceSpendAmount > 0
                    ? preparedResourceSpendAmount
                    : CalculateAmount(
                        effect,
                        effectContext,
                        sourceData,
                        false,
                        amountMultiplier,
                        actionId);
                if (amount <= 0)
                {
                    result = new BattleEffectResult(true, false);
                    break;
                }
                bool changed =
                    effectContext.Resource?.TrySpend(amount) == true;
                result = new BattleEffectResult(true, changed);
                break;
            }

            case BattleEffectType.Heal:
                result = ExecuteRestore(
                    effectContext,
                    effect,
                    sourceData,
                    amountMultiplier,
                    false,
                    showAttackRange,
                    actionId);
                break;

            case BattleEffectType.SpendHealth:
            {
                int amount = preparedHealthSpendAmount > 0
                    ? preparedHealthSpendAmount
                    : CalculateAmount(
                        effect,
                        effectContext,
                        sourceData,
                        false,
                        amountMultiplier,
                        actionId);
                if (amount <= 0)
                {
                    result = new BattleEffectResult(true, false);
                    break;
                }
                bool changed =
                    effectContext.SourceTarget.TrySpendHealth(amount);
                result = new BattleEffectResult(true, changed);
                break;
            }

            case BattleEffectType.Shield:
                result = ExecuteRestore(
                    effectContext,
                    effect,
                    sourceData,
                    amountMultiplier,
                    true,
                    showAttackRange,
                    actionId);
                break;

            default:
                return default;
        }

        if (result.Attempted &&
            effectContext.Board is IBattlePresentationEventPublisher publisher)
        {
            publisher.PublishEffectResolved(
                new BattleEffectResolvedEvent(
                    effectContext,
                    effect,
                    result));
            if (result.Succeeded)
            {
                PublishDefeatedTargets(
                    publisher,
                    effect,
                    livingDamageTargets);
            }
        }

        return result;
    }

    private static IReadOnlyList<BattleStatusTarget>
        CaptureLivingDamageTargets(
            BattleEffectContext context,
            IBattleEffectDefinition effect)
    {
        if (effect == null ||
            effect.BattleEffectType != BattleEffectType.Damage)
        {
            return Array.Empty<BattleStatusTarget>();
        }

        List<BattleStatusTarget> living = new();
        if (context.TargetFaction == CharacterTargetFaction.Ally)
        {
            HashSet<IBattleCharacter> unique = new();
            foreach (IBattleCharacter target in context.AllyTargets)
            {
                if (target != null &&
                    target.CurrentHealth > 0 &&
                    unique.Add(target))
                {
                    living.Add(BattleStatusTarget.FromAlly(target));
                }
            }
        }
        else
        {
            HashSet<EnemyRuntime> unique = new();
            foreach (EnemyRuntime target in context.EnemyTargets)
            {
                if (target != null &&
                    target.Health > 0 &&
                    unique.Add(target))
                {
                    living.Add(BattleStatusTarget.FromEnemy(target));
                }
            }
        }

        return living.Count > 0
            ? living.ToArray()
            : Array.Empty<BattleStatusTarget>();
    }

    private static void PublishDefeatedTargets(
        IBattlePresentationEventPublisher publisher,
        IBattleEffectDefinition effect,
        IReadOnlyList<BattleStatusTarget> livingTargets)
    {
        if (publisher == null ||
            livingTargets == null ||
            livingTargets.Count == 0)
        {
            return;
        }

        float delaySeconds = GetImpactDelay(effect);
        foreach (BattleStatusTarget target in livingTargets)
        {
            if (!IsDefeated(target) ||
                !TryGetPresentationDefinition(
                    target,
                    out IBattlePresentationUnitDefinition definition))
            {
                continue;
            }

            publisher.PublishUnitLifecycle(
                new BattleUnitLifecycleEvent(
                    BattleUnitLifecycleType.Defeated,
                    target,
                    definition,
                    delaySeconds));
        }
    }

    private static float GetImpactDelay(IBattleEffectDefinition effect)
    {
        if (effect is not IBattlePresentationEffectDefinition presentation)
            return 0f;

        float delay = presentation.CastVfxCue != null
            ? presentation.CastVfxCue.StageDuration
            : 0f;
        if (presentation.ProjectileVfxCue != null)
            delay += presentation.ProjectileVfxCue.StageDuration;
        return delay;
    }

    private static bool IsDefeated(BattleStatusTarget target)
    {
        if (target.Enemy != null)
            return target.Enemy.Health <= 0;
        return target.Ally != null &&
               target.Ally.CurrentHealth <= 0;
    }

    private static bool TryGetPresentationDefinition(
        BattleStatusTarget target,
        out IBattlePresentationUnitDefinition definition)
    {
        if (target.Enemy?.Definition is
            IBattlePresentationUnitDefinition enemyDefinition)
        {
            definition = enemyDefinition;
            return true;
        }
        if (target.Ally is CharacterRuntime character &&
            character.Definition is
                IBattlePresentationUnitDefinition characterDefinition)
        {
            definition = characterDefinition;
            return true;
        }

        definition = null;
        return false;
    }

    private static BattleEffectResult ExecuteDamage(
        BattleEffectContext context,
        IBattleEffectDefinition effect,
        CharacterData sourceData,
        int amountMultiplier,
        bool showAttackRange,
        Func<int, IBattleCharacter, bool> fallback,
        string actionId)
    {
        if (!IsDirectDamageType(effect.DamageType))
        {
            return new BattleEffectResult(true, false);
        }

        if (context.TargetFaction == CharacterTargetFaction.Ally)
        {
            return ExecuteAlliedDamage(
                context,
                effect,
                sourceData,
                amountMultiplier,
                actionId);
        }

        if (effect.AmountScaling.HasTargetDependentTerm)
        {
            List<EnemyAmountSnapshot> snapshots = new(
                context.EnemyTargets.Count);
            HashSet<EnemyRuntime> uniqueTargets = new();
            foreach (EnemyRuntime target in context.EnemyTargets)
            {
                if (target == null || !uniqueTargets.Add(target))
                    continue;

                snapshots.Add(new EnemyAmountSnapshot(
                    target,
                    CalculateAmount(
                        effect,
                        context.BindEnemyTarget(
                            target,
                            effect.TargetStatusScalingEffect),
                        sourceData,
                        true,
                        amountMultiplier,
                        actionId)));
            }
            if (snapshots.Count == 0)
                return default;

            int totalDamage = 0;
            int groupedDamage = 0;
            List<EnemyRuntime> groupedTargets = new();
            foreach (EnemyAmountSnapshot snapshot in snapshots)
            {
                if (snapshot.Amount <= 0)
                    continue;

                if (groupedTargets.Count > 0 &&
                    snapshot.Amount != groupedDamage)
                {
                    totalDamage += ApplyDamageGroup(
                        context,
                        effect,
                        groupedTargets,
                        groupedDamage,
                        showAttackRange,
                        fallback);
                    groupedTargets.Clear();
                }

                groupedDamage = snapshot.Amount;
                groupedTargets.Add(snapshot.Target);
            }
            if (groupedTargets.Count > 0)
            {
                totalDamage += ApplyDamageGroup(
                    context,
                    effect,
                    groupedTargets,
                    groupedDamage,
                    showAttackRange,
                    fallback);
            }

            return new BattleEffectResult(
                true,
                totalDamage > 0,
                totalDamage);
        }

        int sharedDamage = CalculateAmount(
            effect,
            context,
            sourceData,
            true,
            amountMultiplier,
            actionId);
        if (sharedDamage <= 0)
            return default;
        if (context.Board != null)
        {
            int damageDealt =
                context.Board.TryDamageCharacterTargets(
                    context.Source,
                    context.EnemyTargets,
                    sharedDamage,
                    effect.DamageType,
                    showAttackRange);
            return new BattleEffectResult(
                true,
                damageDealt > 0,
                damageDealt);
        }

        bool fallbackSucceeded =
            fallback?.Invoke(sharedDamage, context.Source) == true;
        return new BattleEffectResult(
            true,
            fallbackSucceeded,
            fallbackSucceeded ? sharedDamage : 0);
    }

    private static BattleEffectResult ExecuteAlliedDamage(
        BattleEffectContext context,
        IBattleEffectDefinition effect,
        CharacterData sourceData,
        int amountMultiplier,
        string actionId)
    {
        List<AllyAmountSnapshot> snapshots = new(
            context.AllyTargets.Count);
        HashSet<IBattleCharacter> uniqueTargets = new();
        int sharedAmount = effect.AmountScaling.HasTargetDependentTerm
            ? 0
            : CalculateAmount(
                effect,
                context,
                sourceData,
                true,
                amountMultiplier,
                actionId);
        foreach (IBattleCharacter target in context.AllyTargets)
        {
            if (target == null || !uniqueTargets.Add(target))
                continue;

            int amount = effect.AmountScaling.HasTargetDependentTerm
                ? CalculateAmount(
                    effect,
                    context.BindAllyTarget(
                        target,
                        effect.TargetStatusScalingEffect),
                    sourceData,
                    true,
                    amountMultiplier,
                    actionId)
                : sharedAmount;
            snapshots.Add(new AllyAmountSnapshot(target, amount));
        }

        if (snapshots.Count == 0)
            return default;

        int totalDamage = 0;
        bool attempted = false;
        foreach (AllyAmountSnapshot snapshot in snapshots)
        {
            if (snapshot.Amount <= 0)
                continue;

            attempted = true;
            totalDamage += Mathf.Max(
                0,
                snapshot.Target.TakeDamage(snapshot.Amount));
        }

        return new BattleEffectResult(
            attempted,
            totalDamage > 0,
            totalDamage);
    }

    private static int ApplyDamageGroup(
        BattleEffectContext context,
        IBattleEffectDefinition effect,
        IReadOnlyList<EnemyRuntime> targets,
        int damage,
        bool showAttackRange,
        Func<int, IBattleCharacter, bool> fallback)
    {
        if (damage <= 0 || targets == null || targets.Count == 0)
            return 0;
        if (context.Board != null)
        {
            return context.Board.TryDamageCharacterTargets(
                context.Source,
                targets,
                damage,
                effect.DamageType,
                showAttackRange);
        }

        return fallback?.Invoke(damage, context.Source) == true
            ? damage
            : 0;
    }

    private static BattleEffectResult ExecuteRestore(
        BattleEffectContext context,
        IBattleEffectDefinition effect,
        CharacterData sourceData,
        int amountMultiplier,
        bool shield,
        bool showAttackRange,
        string actionId)
    {
        if (context.Board == null)
            return default;

        if (!effect.AmountScaling.HasTargetDependentTerm)
        {
            int amount = CalculateAmount(
                effect,
                context,
                sourceData,
                false,
                amountMultiplier,
                actionId);
            if (amount <= 0)
                return default;

            int changed = context.TargetFaction ==
                          CharacterTargetFaction.Ally
                ? shield
                    ? context.Board.TryGrantShieldToAlliedCharacters(
                        context.Source,
                        context.AllyTargets,
                        amount)
                    : context.Board.TryHealAlliedCharacters(
                        context.Source,
                        context.AllyTargets,
                        amount)
                : shield
                    ? context.Board.TryGrantShieldToCharacterTargets(
                        context.Source,
                        context.EnemyTargets,
                        amount,
                        showAttackRange)
                    : context.Board.TryHealCharacterTargets(
                        context.Source,
                        context.EnemyTargets,
                        amount,
                        showAttackRange);
            return new BattleEffectResult(true, changed > 0);
        }

        bool attempted = false;
        int totalChanged = 0;
        if (context.TargetFaction == CharacterTargetFaction.Ally)
        {
            HashSet<IBattleCharacter> uniqueTargets = new();
            foreach (IBattleCharacter target in context.AllyTargets)
            {
                if (target == null || !uniqueTargets.Add(target))
                    continue;

                int amount = CalculateAmount(
                    effect,
                    context.BindAllyTarget(
                        target,
                        effect.TargetStatusScalingEffect),
                    sourceData,
                    false,
                    amountMultiplier,
                    actionId);
                if (amount <= 0)
                    continue;

                attempted = true;
                totalChanged += shield
                    ? context.Board.TryGrantShieldToAlliedCharacters(
                        context.Source,
                        new[] { target },
                        amount)
                    : context.Board.TryHealAlliedCharacters(
                        context.Source,
                        new[] { target },
                        amount);
            }
        }
        else
        {
            HashSet<EnemyRuntime> uniqueTargets = new();
            foreach (EnemyRuntime target in context.EnemyTargets)
            {
                if (target == null || !uniqueTargets.Add(target))
                    continue;

                int amount = CalculateAmount(
                    effect,
                    context.BindEnemyTarget(
                        target,
                        effect.TargetStatusScalingEffect),
                    sourceData,
                    false,
                    amountMultiplier,
                    actionId);
                if (amount <= 0)
                    continue;

                attempted = true;
                totalChanged += shield
                    ? context.Board.TryGrantShieldToCharacterTargets(
                        context.Source,
                        new[] { target },
                        amount,
                        showAttackRange)
                    : context.Board.TryHealCharacterTargets(
                        context.Source,
                        new[] { target },
                        amount,
                        showAttackRange);
            }
        }

        return new BattleEffectResult(attempted, totalChanged > 0);
    }

    private static int CalculateAmount(
        IBattleEffectDefinition effect,
        BattleEffectContext context,
        CharacterData sourceData,
        bool damage,
        int amountMultiplier,
        string actionId = null)
    {
        ScalingValue scaling = effect.AmountScaling;
        if (damage && sourceData != null)
        {
            scaling += context.OriginKind switch
            {
                BattleEffectOriginKind.CharacterAttack =>
                    ScalingValue.Fixed(sourceData.AttackDamageFlatBonus),
                BattleEffectOriginKind.CharacterPassive =>
                    IsLegacyRatioDamage(effect)
                        ? ScalingValue.SourceAttackPower(
                            sourceData.PassiveDamageAmountBonus)
                        : ScalingValue.Fixed(
                            sourceData.PassiveDamageAmountBonus),
                BattleEffectOriginKind.CharacterSkill =>
                    ScalingValue.Fixed(sourceData.SkillDamageFlatBonus),
                _ => default
            };
        }

        double value = scaling.EvaluateBattle(context) *
                       (double)Mathf.Max(1, amountMultiplier);
        if (sourceData != null)
        {
            string effectId = effect is CharacterEffectDefinition
                characterEffect
                    ? characterEffect.EffectId
                    : string.Empty;
            value = sourceData.ResolveModifier(
                (float)value,
                damage
                    ? CharacterModifierStat.Damage
                    : CharacterModifierStat.EffectAmount,
                ResolveActionKind(context.OriginKind),
                actionId,
                effectId);
        }
        if (double.IsNaN(value) || value <= 0d)
            return 0;
        if (double.IsInfinity(value) || value >= int.MaxValue)
            return int.MaxValue;
        return Mathf.Max(0, Mathf.RoundToInt((float)value));
    }

    private static CharacterActionKind ResolveActionKind(
        BattleEffectOriginKind originKind)
    {
        return originKind switch
        {
            BattleEffectOriginKind.CharacterAttack =>
                CharacterActionKind.Attack,
            BattleEffectOriginKind.CharacterPassive =>
                CharacterActionKind.Passive,
            BattleEffectOriginKind.CharacterSkill =>
                CharacterActionKind.Skill,
            BattleEffectOriginKind.BattleItem =>
                CharacterActionKind.Skill,
            _ => default,
        };
    }

    private static bool TryResolveContext(
        BattleEffectContext context,
        IBattleEffectDefinition effect,
        out BattleEffectContext resolved)
    {
        resolved = default;
        if (effect == null)
            return false;
        if (!RequiresTargets(effect.BattleEffectType))
        {
            resolved = context;
            return true;
        }

        switch (effect.BattleTargetMode)
        {
            case BattleEffectTargetMode.InheritContext:
                resolved = context;
                return resolved.HasTargets;

            case BattleEffectTargetMode.Source:
                resolved = context.RetargetToSource();
                return resolved.HasTargets;

            case BattleEffectTargetMode.FreshSelection:
                return TrySelectFreshTargets(context, effect, out resolved);

            default:
                return false;
        }
    }

    private static bool TrySelectFreshTargets(
        BattleEffectContext context,
        IBattleEffectDefinition effect,
        out BattleEffectContext resolved)
    {
        resolved = default;
        IBattleEffectTargetSelector selector =
            effect.BattleTargetSelector;
        if (context.Board == null || selector == null ||
            selector.Subject == CharacterAttackSubject.None ||
            selector.Subject == CharacterAttackSubject.Manual)
        {
            return false;
        }

        IReadOnlyList<CharacterNumericCondition> conditions =
            selector.HasNumericConditions
                ? selector.NumericConditions
                : Array.Empty<CharacterNumericCondition>();
        if (selector.TargetFaction == CharacterTargetFaction.Ally)
        {
            IReadOnlyList<IBattleCharacter> allies =
                context.Board.SelectAlliedCharacters(
                    context.Source,
                    selector.Subject,
                    selector.SubjectMetric,
                    selector.SubjectCount,
                    selector.ConditionMatchMode,
                    conditions);
            resolved = context.RetargetTo(
                CharacterTargetFaction.Ally,
                null,
                allies);
            return resolved.HasTargets;
        }

        IReadOnlyList<EnemyRuntime> enemies =
            context.Board.SelectCharacterTargets(
                context.Source,
                selector.Subject,
                selector.SubjectMetric,
                selector.SubjectCount,
                selector.ConditionMatchMode,
                conditions);
        if (enemies != null && enemies.Count > 0 &&
            selector.AreaOffsets != null &&
            selector.AreaOffsets.Count > 0)
        {
            enemies = context.Board.ExpandCharacterAreaTargets(
                enemies,
                selector.AreaOffsets);
        }
        resolved = context.RetargetTo(
            CharacterTargetFaction.Enemy,
            enemies,
            null);
        return resolved.HasTargets;
    }

    private static bool IsUsable(IBattleEffectDefinition effect)
    {
        return effect != null &&
               Enum.IsDefined(
                   typeof(BattleEffectType),
                   effect.BattleEffectType) &&
               Enum.IsDefined(
                   typeof(BattleEffectTargetMode),
                   effect.BattleTargetMode) &&
               effect.AmountScaling.IsFinite;
    }

    private static bool RequiresTargets(BattleEffectType effectType)
    {
        return effectType != BattleEffectType.GainResource &&
               effectType != BattleEffectType.SpendResource &&
               effectType != BattleEffectType.SpendHealth;
    }

    private static bool IsDirectDamageType(
        CharacterAttackDamageType damageType)
    {
        return damageType == CharacterAttackDamageType.Physical ||
               damageType == CharacterAttackDamageType.Magical ||
               damageType == CharacterAttackDamageType.Fixed;
    }

    private static bool IsLegacyRatioDamage(
        IBattleEffectDefinition effect)
    {
        return effect is CharacterEffectDefinition characterEffect &&
               characterEffect.DamageAmountMode ==
                   CharacterDamageAmountMode.Ratio;
    }

    private static int SaturatingMultiply(int left, int right)
    {
        long value = (long)Mathf.Max(0, left) * Mathf.Max(0, right);
        return value >= int.MaxValue ? int.MaxValue : (int)value;
    }
}

public readonly struct BattleStatusTarget
{
    public CharacterTargetFaction Faction { get; }
    public IBattleCharacter Ally { get; }
    public EnemyRuntime Enemy { get; }
    public bool IsValid => Ally != null || Enemy != null;
    public bool IsAlly => Ally != null;
    public bool IsEnemy => Enemy != null;
    public int CurrentHealth => Ally != null
        ? Ally.CurrentHealth
        : Enemy?.Health ?? 0;
    public int MaximumHealth => Ally != null
        ? Ally.MaximumHealth
        : Enemy?.MaxHealth ?? 0;

    private BattleStatusTarget(
        CharacterTargetFaction faction,
        IBattleCharacter ally,
        EnemyRuntime enemy)
    {
        Faction = faction;
        Ally = ally;
        Enemy = enemy;
    }

    public static BattleStatusTarget FromAlly(IBattleCharacter target)
    {
        return new BattleStatusTarget(
            CharacterTargetFaction.Ally,
            target,
            null);
    }

    public static BattleStatusTarget FromEnemy(EnemyRuntime target)
    {
        return new BattleStatusTarget(
            CharacterTargetFaction.Enemy,
            null,
            target);
    }

    public int GetStatusStackCount(StatusEffectSO statusEffect)
    {
        if (statusEffect == null)
            return 0;
        return Ally != null
            ? Ally.GetStatusStackCount(statusEffect)
            : Enemy?.GetStatusStackCount(statusEffect) ?? 0;
    }

    public bool CanSpendHealth(int amount)
    {
        return Ally != null
            ? Ally.CanSpendHealth(amount)
            : Enemy?.CanSpendHealth(amount) == true;
    }

    public bool TrySpendHealth(int amount)
    {
        return Ally != null
            ? Ally.TrySpendHealth(amount)
            : Enemy?.TrySpendHealth(amount) == true;
    }
}

public readonly struct BattleStatusAppliedEvent
{
    public BattleStatusTarget Target { get; }
    public IBattleCharacter Source { get; }
    public StatusEffectSO StatusEffect { get; }
    public int PreviousStacks { get; }
    public int CurrentStacks { get; }
    public bool IsValid => Target.IsValid && StatusEffect != null;
    public int AddedStacks => Mathf.Max(0, CurrentStacks - PreviousStacks);
    public bool HasStackChange => PreviousStacks != CurrentStacks;

    public BattleStatusAppliedEvent(
        BattleStatusTarget target,
        StatusEffectSO statusEffect,
        int previousStacks,
        int currentStacks,
        IBattleCharacter source = null)
    {
        Target = target;
        Source = source;
        StatusEffect = statusEffect;
        PreviousStacks = Mathf.Max(0, previousStacks);
        CurrentStacks = Mathf.Max(0, currentStacks);
    }
}

public enum BattleStatusChangeType
{
    Applied = 0,
    Reapplied = 1,
    StackChanged = 2,
    Removed = 3,
    Expired = 4
}

public readonly struct BattleStatusSnapshot
{
    public StatusEffectSO Definition { get; }
    public int StackCount { get; }
    public float RemainingDuration { get; }
    public IBattleCharacter ActiveSource { get; }
    public bool IsValid => Definition != null && StackCount > 0;
    public bool IsPermanent =>
        IsValid &&
        (Definition.DurationMode == StatusEffectDurationMode.Permanent ||
         float.IsPositiveInfinity(RemainingDuration));

    public BattleStatusSnapshot(
        StatusEffectSO definition,
        int stackCount,
        float remainingDuration,
        IBattleCharacter activeSource = null)
    {
        Definition = definition;
        StackCount = Mathf.Max(0, stackCount);
        ActiveSource = StackCount > 0 ? activeSource : null;
        if (StackCount == 0)
        {
            RemainingDuration = 0f;
        }
        else if (definition != null &&
            definition.DurationMode == StatusEffectDurationMode.Permanent)
        {
            RemainingDuration = float.PositiveInfinity;
        }
        else
        {
            RemainingDuration =
                float.IsNaN(remainingDuration) ||
                float.IsNegativeInfinity(remainingDuration)
                    ? 0f
                    : Mathf.Max(0f, remainingDuration);
        }
    }
}

public readonly struct BattleStatusChangedEvent
{
    public BattleStatusTarget Target { get; }
    public BattleStatusChangeType ChangeType { get; }
    public BattleStatusSnapshot Previous { get; }
    public BattleStatusSnapshot Current { get; }
    public StatusEffectSO StatusEffect =>
        Current.Definition != null ? Current.Definition : Previous.Definition;
    public int PreviousStacks => Previous.StackCount;
    public int CurrentStacks => Current.StackCount;
    public bool IsValid =>
        Target.IsValid &&
        StatusEffect != null &&
        (Previous.IsValid || Current.IsValid);

    public BattleStatusChangedEvent(
        BattleStatusTarget target,
        BattleStatusChangeType changeType,
        BattleStatusSnapshot previous,
        BattleStatusSnapshot current)
    {
        Target = target;
        ChangeType = changeType;
        Previous = previous;
        Current = current;
    }
}

public readonly struct BattleEnemyDefeatedEvent
{
    public EnemyRuntime Enemy { get; }
    public IBattleCharacter Killer { get; }
    public bool IsValid => Enemy != null;
    public bool HasCharacterKiller => Killer != null;

    public BattleEnemyDefeatedEvent(
        EnemyRuntime enemy,
        IBattleCharacter killer)
    {
        Enemy = enemy;
        Killer = killer;
    }
}

public readonly struct BattleManualTargetSelectionResult
{
    public CharacterTargetFaction Faction { get; }
    public IReadOnlyList<EnemyRuntime> EnemyTargets { get; }
    public IReadOnlyList<IBattleCharacter> AllyTargets { get; }
    public bool Cancelled { get; }
    public bool HasTargets =>
        Faction == CharacterTargetFaction.Ally
            ? AllyTargets != null && AllyTargets.Count > 0
            : EnemyTargets != null && EnemyTargets.Count > 0;

    public BattleManualTargetSelectionResult(
        CharacterTargetFaction faction,
        IReadOnlyList<EnemyRuntime> enemyTargets,
        IReadOnlyList<IBattleCharacter> allyTargets,
        bool cancelled = false)
    {
        Faction = faction;
        EnemyTargets = enemyTargets ?? Array.Empty<EnemyRuntime>();
        AllyTargets = allyTargets ?? Array.Empty<IBattleCharacter>();
        Cancelled = cancelled;
    }
}

public sealed class BattleManualTargetSelectionRequest
{
    private readonly Action<BattleManualTargetSelectionResult> _complete;

    public IBattleCharacter Source { get; }
    public CharacterTargetFaction Faction { get; }
    public int TargetCount { get; }
    public IReadOnlyList<EnemyRuntime> EnemyCandidates { get; }
    public IReadOnlyList<IBattleCharacter> AllyCandidates { get; }
    public bool AllowCancel { get; }
    public BattleAreaDefinition AreaDefinition { get; }
    public bool UsesWorldArea => AreaDefinition?.UsesWorldArea == true;

    public BattleManualTargetSelectionRequest(
        IBattleCharacter source,
        CharacterTargetFaction faction,
        int targetCount,
        IReadOnlyList<EnemyRuntime> enemyCandidates,
        IReadOnlyList<IBattleCharacter> allyCandidates,
        bool allowCancel,
        Action<BattleManualTargetSelectionResult> complete,
        BattleAreaDefinition areaDefinition = null)
    {
        Source = source;
        Faction = faction;
        TargetCount = Mathf.Max(1, targetCount);
        EnemyCandidates =
            enemyCandidates ?? Array.Empty<EnemyRuntime>();
        AllyCandidates =
            allyCandidates ?? Array.Empty<IBattleCharacter>();
        AllowCancel = allowCancel;
        AreaDefinition = areaDefinition;
        _complete = complete;
    }

    public int CandidateCount =>
        Faction == CharacterTargetFaction.Ally
            ? AllyCandidates.Count
            : EnemyCandidates.Count;
    public int RequiredCount => Mathf.Min(TargetCount, CandidateCount);

    public void Complete(BattleManualTargetSelectionResult result)
    {
        _complete?.Invoke(result);
    }
}

public interface IBattleManualTargetSelectionService
{
    bool IsManualTargetSelectionPending { get; }
    BattleManualTargetSelectionRequest CurrentManualTargetRequest { get; }
    int CurrentManualSelectedCount { get; }
    event Action<bool> ManualTargetSelectionPendingChanged;
    event Action ManualTargetSelectionProgressChanged;

    bool TryBeginManualTargetSelection(
        BattleManualTargetSelectionRequest request);
    void CancelManualTargetSelection();
}

public enum CharacterAreaShapeType
{
    LegacyTileOffsets = 0,
    Circle = 1,
    Semicircle = 2,
    Cone = 3
}

public enum CharacterAreaOriginMode
{
    Caster = 0,
    Cursor = 1
}

[Serializable]
public class BattleAreaDefinition
{
    [SerializeField]
    private CharacterAreaShapeType shapeType =
        CharacterAreaShapeType.LegacyTileOffsets;
    [SerializeField]
    private CharacterAreaOriginMode originMode =
        CharacterAreaOriginMode.Cursor;
    [SerializeField, Min(0.1f)]
    private float radius = 1.5f;
    [SerializeField, Range(1f, 179f)]
    private float coneAngle = 60f;
    [SerializeField, Min(0.1f)]
    private float maxCastDistance = 4.25f;

    public CharacterAreaShapeType ShapeType => shapeType;
    public CharacterAreaOriginMode OriginMode => originMode;
    public float Radius => Mathf.Max(0.1f, radius);
    public float ConeAngle => Mathf.Clamp(coneAngle, 1f, 179f);
    public float MaxCastDistance => Mathf.Max(0.1f, maxCastDistance);
    public bool UsesWorldArea =>
        shapeType != CharacterAreaShapeType.LegacyTileOffsets;

    public void Validate()
    {
        radius = Mathf.Max(0.1f, radius);
        coneAngle = Mathf.Clamp(coneAngle, 1f, 179f);
        maxCastDistance = Mathf.Max(0.1f, maxCastDistance);
    }
}

/// <summary>
/// Serialized compatibility name for assets and integrations created before
/// targeting became ability-owner neutral.
/// </summary>
[Serializable]
public sealed class CharacterAreaDefinition : BattleAreaDefinition
{
}

public static class BattleAreaGeometry
{
    private const float DirectionEpsilon = 0.0001f;

    public static bool Contains(
        Vector2 point,
        Vector2 origin,
        Vector2 direction,
        CharacterAreaShapeType shapeType,
        float radius,
        float coneAngle)
    {
        Vector2 offset = point - origin;
        float appliedRadius = Mathf.Max(0.1f, radius);
        if (offset.sqrMagnitude > appliedRadius * appliedRadius)
            return false;

        if (shapeType == CharacterAreaShapeType.Circle)
            return true;

        Vector2 forward = direction.sqrMagnitude > DirectionEpsilon
            ? direction.normalized
            : Vector2.up;
        if (shapeType == CharacterAreaShapeType.Semicircle)
            return Vector2.Dot(forward, offset) >= 0f;

        if (shapeType == CharacterAreaShapeType.Cone)
        {
            if (offset.sqrMagnitude <= DirectionEpsilon)
                return true;

            float halfAngle = Mathf.Clamp(coneAngle, 1f, 179f) * 0.5f;
            return Vector2.Angle(forward, offset) <= halfAngle;
        }

        return false;
    }

    public static Vector2 ClampToRadius(
        Vector2 point,
        Vector2 center,
        float radius)
    {
        Vector2 offset = point - center;
        float appliedRadius = Mathf.Max(0f, radius);
        return offset.sqrMagnitude <= appliedRadius * appliedRadius
            ? point
            : center + offset.normalized * appliedRadius;
    }
}

public static class BattleArenaRingMeshBuilder
{
    private const int MinimumSegments = 3;

    public static void Populate(
        Mesh mesh,
        float innerRadius,
        float outerRadius,
        float height,
        int segmentCount = 96)
    {
        if (mesh == null)
            throw new ArgumentNullException(nameof(mesh));

        int segments = Mathf.Max(MinimumSegments, segmentCount);
        float inner = Mathf.Max(0f, innerRadius);
        float outer = Mathf.Max(inner + 0.01f, outerRadius);
        float top = Mathf.Max(0.01f, height);
        int loopSize = segments + 1;
        const int surfaceCount = 4;
        Vector3[] vertices = new Vector3[loopSize * 2 * surfaceCount];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6 * surfaceCount];

        int vertexOffset = 0;
        int triangleOffset = 0;
        BuildHorizontalSurface(
            vertices,
            normals,
            uv,
            triangles,
            ref vertexOffset,
            ref triangleOffset,
            inner,
            outer,
            top,
            segments,
            true);
        BuildHorizontalSurface(
            vertices,
            normals,
            uv,
            triangles,
            ref vertexOffset,
            ref triangleOffset,
            inner,
            outer,
            0f,
            segments,
            false);
        BuildVerticalSurface(
            vertices,
            normals,
            uv,
            triangles,
            ref vertexOffset,
            ref triangleOffset,
            outer,
            top,
            segments,
            false);
        BuildVerticalSurface(
            vertices,
            normals,
            uv,
            triangles,
            ref vertexOffset,
            ref triangleOffset,
            inner,
            top,
            segments,
            true);

        mesh.Clear();
        mesh.name = "Dungeon Arena Ring";
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private static void BuildHorizontalSurface(
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uv,
        int[] triangles,
        ref int vertexOffset,
        ref int triangleOffset,
        float innerRadius,
        float outerRadius,
        float y,
        int segments,
        bool facesUp)
    {
        int surfaceOffset = vertexOffset;
        Vector3 normal = facesUp ? Vector3.up : Vector3.down;
        for (int index = 0; index <= segments; index++)
        {
            float normalized = index / (float)segments;
            float angle = normalized * Mathf.PI * 2f;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            int outerIndex = surfaceOffset + index * 2;
            vertices[outerIndex] = radial * outerRadius + Vector3.up * y;
            vertices[outerIndex + 1] =
                radial * innerRadius + Vector3.up * y;
            normals[outerIndex] = normal;
            normals[outerIndex + 1] = normal;
            uv[outerIndex] = new Vector2(normalized, 1f);
            uv[outerIndex + 1] = new Vector2(normalized, 0f);
        }

        for (int index = 0; index < segments; index++)
        {
            int outer = surfaceOffset + index * 2;
            int inner = outer + 1;
            int nextOuter = outer + 2;
            int nextInner = outer + 3;
            if (facesUp)
            {
                AddTriangle(triangles, ref triangleOffset, outer, inner, nextInner);
                AddTriangle(
                    triangles,
                    ref triangleOffset,
                    outer,
                    nextInner,
                    nextOuter);
            }
            else
            {
                AddTriangle(
                    triangles,
                    ref triangleOffset,
                    outer,
                    nextOuter,
                    nextInner);
                AddTriangle(triangles, ref triangleOffset, outer, nextInner, inner);
            }
        }

        vertexOffset += (segments + 1) * 2;
    }

    private static void BuildVerticalSurface(
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uv,
        int[] triangles,
        ref int vertexOffset,
        ref int triangleOffset,
        float radius,
        float height,
        int segments,
        bool facesInward)
    {
        int surfaceOffset = vertexOffset;
        for (int index = 0; index <= segments; index++)
        {
            float normalized = index / (float)segments;
            float angle = normalized * Mathf.PI * 2f;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 normal = facesInward ? -radial : radial;
            int bottomIndex = surfaceOffset + index * 2;
            vertices[bottomIndex] = radial * radius;
            vertices[bottomIndex + 1] =
                radial * radius + Vector3.up * height;
            normals[bottomIndex] = normal;
            normals[bottomIndex + 1] = normal;
            uv[bottomIndex] = new Vector2(normalized, 0f);
            uv[bottomIndex + 1] = new Vector2(normalized, 1f);
        }

        for (int index = 0; index < segments; index++)
        {
            int bottom = surfaceOffset + index * 2;
            int top = bottom + 1;
            int nextBottom = bottom + 2;
            int nextTop = bottom + 3;
            if (facesInward)
            {
                AddTriangle(
                    triangles,
                    ref triangleOffset,
                    bottom,
                    nextBottom,
                    nextTop);
                AddTriangle(triangles, ref triangleOffset, bottom, nextTop, top);
            }
            else
            {
                AddTriangle(triangles, ref triangleOffset, bottom, top, nextTop);
                AddTriangle(
                    triangles,
                    ref triangleOffset,
                    bottom,
                    nextTop,
                    nextBottom);
            }
        }

        vertexOffset += (segments + 1) * 2;
    }

    private static void AddTriangle(
        int[] triangles,
        ref int offset,
        int first,
        int second,
        int third)
    {
        triangles[offset++] = first;
        triangles[offset++] = second;
        triangles[offset++] = third;
    }
}

public interface IDungeonStageProgressProvider
{
    /// <summary>
    /// Number of dungeon stages completed before the current stage. Battle,
    /// event, rest, and shop stages all contribute one step. The first stage
    /// is 0. Non-dungeon boards should omit this interface.
    /// </summary>
    float DungeonStageProgress { get; }
}

public interface IBattleBoard
{
    int InitialEnemyCapacity { get; }
    int LivingEnemyCount { get; }
    bool HasEmptyEnemyTile { get; }
    event Action OccupancyChanged;
    event Action<BattleEnemyDefeatedEvent> EnemyDefeated;
    event Action<BattleStatusAppliedEvent> StatusApplied;

    bool TryAddEnemy(EnemyRuntime enemy);
    bool TryAddEnemiesToDistinctTiles(
        IReadOnlyList<EnemyRuntime> enemies);
    void ClearAllEnemies();
    void TickStatusEffects(float deltaTime);
    void TickEnemyAbilities(
        float deltaTime,
        IReadOnlyList<IBattleCharacter> characters);
    void SetBattleCharacters(IReadOnlyList<IBattleCharacter> characters);
    void NotifyStatusApplied(BattleStatusAppliedEvent eventData);

    IReadOnlyList<EnemyRuntime> SelectCharacterTargets(
        IBattleCharacter source,
        CharacterAttackSubject subject,
        CharacterAttackSubjectMetric metric,
        int targetCount,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions);
    IReadOnlyList<IBattleCharacter> SelectAlliedCharacters(
        IBattleCharacter source,
        CharacterAttackSubject subject,
        CharacterAttackSubjectMetric metric,
        int targetCount,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions);
    IReadOnlyList<EnemyRuntime> FilterCharacterTargets(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions);
    IReadOnlyList<IBattleCharacter> FilterAlliedCharacters(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions);
    IReadOnlyList<EnemyRuntime> ExpandCharacterAreaTargets(
        IReadOnlyList<EnemyRuntime> centerTargets,
        IReadOnlyList<CharacterTargetAreaOffset> areaOffsets);
    int TryDamageCharacterTargets(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        int damage,
        CharacterAttackDamageType damageType,
        bool showAttackRange);
    int TryHealCharacterTargets(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        int amount,
        bool showAttackRange);
    int TryHealAlliedCharacters(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        int amount);
    int TryGrantShieldToCharacterTargets(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        int amount,
        bool showAttackRange);
    int TryGrantShieldToAlliedCharacters(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        int amount);
    bool TryApplyCharacterStatus(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        StatusEffectSO statusEffect,
        float duration,
        float stacks,
        float tickInterval,
        bool showAttackRange);
    bool TryApplyAlliedCharacterStatus(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        StatusEffectSO statusEffect,
        float duration,
        float stacks);
    bool TryRemoveCharacterStatus(
        IBattleCharacter source,
        IReadOnlyList<EnemyRuntime> targets,
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount,
        bool showAttackRange);
    bool TryRemoveAlliedCharacterStatus(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount);
}

public interface IBattleCharacter
{
    int PartySlotIndex { get; }
    int TotalDamageDealt { get; }
    int CurrentHealth { get; }
    int MaximumHealth { get; }
    int CurrentShield { get; }
    float DisabledTimeRemaining { get; }
    float CurrentAttackPower { get; }
    float CurrentAttackSpeed { get; }
    event Action<BattleStatusChangedEvent> StatusChanged;
    bool HasStatusEffect(StatusEffectSO statusEffect);
    int GetStatusStackCount(StatusEffectSO statusEffect);
    IReadOnlyList<BattleStatusSnapshot> GetActiveStatusEffects();
    bool TryConsumeStatusStacks(StatusEffectSO statusEffect, int stackCount);
    int Heal(int amount);
    int GainShield(int amount);
    int TakeDamage(int amount);
    bool CanSpendHealth(int amount);
    bool TrySpendHealth(int amount);

    bool Initialize();
    void BindBattle(
        IActiveSkillResource activeSkillResource,
        IBattleBoard board);
    void ResetRuntime();
    void TickBattle(float deltaTime, IBattleBoard board);
    void RecordDamageDealt(int damage);
    void DisableFor(float duration);
    bool ApplyStatusEffect(
        StatusEffectSO definition,
        float duration,
        int stacks);
    bool ApplyStatusEffect(
        StatusEffectSO definition,
        float duration,
        int stacks,
        IBattleCharacter source);
    int RemoveStatusEffects(
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount);
}
