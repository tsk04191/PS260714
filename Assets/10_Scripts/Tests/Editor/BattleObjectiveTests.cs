using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleObjectiveTests
{
    [Test]
    public void ObjectiveTargetMode_AllowsOnlyHealAndShield()
    {
        TestEffect heal = new(
            BattleEffectType.Heal,
            BattleEffectTargetMode.Objective,
            ScalingValue.Fixed(25f));
        TestEffect shield = new(
            BattleEffectType.Shield,
            BattleEffectTargetMode.Objective,
            ScalingValue.Fixed(25f));
        TestEffect damage = new(
            BattleEffectType.Damage,
            BattleEffectTargetMode.Objective,
            ScalingValue.Fixed(25f));
        TestEffect targetScaledHeal = new(
            BattleEffectType.Heal,
            BattleEffectTargetMode.Objective,
            ScalingValue.TargetMaximumHealth(0.1f));

        Assert.That(
            BattleEffectRules.TryValidate(heal, out string healError),
            Is.True,
            healError);
        Assert.That(
            BattleEffectRules.TryValidate(shield, out string shieldError),
            Is.True,
            shieldError);
        Assert.That(
            BattleEffectRules.TryValidate(damage, out string damageError),
            Is.False);
        Assert.That(damageError, Does.Contain("Heal and Shield"));
        Assert.That(
            BattleEffectRules.TryValidate(
                targetScaledHeal,
                out string scalingError),
            Is.True,
            scalingError);
        Assert.That(heal.RequiresActionTargets, Is.False);
    }

    [Test]
    public void ObjectiveHealAndShield_BothRestoreCoreHealth()
    {
        BattleCoreRuntime core = new();
        core.Configure(100, true, 40);
        ObjectiveBoard board = new(core);
        BattleEffectContext context = BattleEffectContext.ForBattleCard(
            null,
            board,
            null,
            CharacterTargetFaction.Ally,
            Array.Empty<EnemyRuntime>(),
            Array.Empty<IBattleCharacter>(),
            usesCharacterUser: false);
        TestEffect heal = new(
            BattleEffectType.Heal,
            BattleEffectTargetMode.Objective,
            ScalingValue.Fixed(25f));
        TestEffect scaledHeal = new(
            BattleEffectType.Heal,
            BattleEffectTargetMode.Objective,
            ScalingValue.TargetMaximumHealth(0.1f));
        TestEffect shield = new(
            BattleEffectType.Shield,
            BattleEffectTargetMode.Objective,
            ScalingValue.Fixed(80f));
        TestEffect damage = new(
            BattleEffectType.Damage,
            BattleEffectTargetMode.Objective,
            ScalingValue.Fixed(80f));

        BattleEffectResult scaledResult =
            BattleEffectExecutor.ExecuteSequence(
                context,
                new[] { scaledHeal });

        Assert.That(scaledResult.Attempted, Is.True);
        Assert.That(scaledResult.Succeeded, Is.True);
        Assert.That(core.CurrentHealth, Is.EqualTo(50));

        BattleEffectResult healResult = BattleEffectExecutor.ExecuteSequence(
            context,
            new[] { heal });

        Assert.That(healResult.Attempted, Is.True);
        Assert.That(healResult.Succeeded, Is.True);
        Assert.That(core.CurrentHealth, Is.EqualTo(75));

        BattleEffectResult shieldResult =
            BattleEffectExecutor.ExecuteSequence(
                context,
                new[] { shield });

        Assert.That(shieldResult.Attempted, Is.True);
        Assert.That(shieldResult.Succeeded, Is.True);
        Assert.That(core.CurrentHealth, Is.EqualTo(100));

        BattleEffectResult unsupportedResult =
            BattleEffectExecutor.ExecuteSequence(
                context,
                new[] { damage });

        Assert.That(unsupportedResult.Attempted, Is.False);
        Assert.That(core.CurrentHealth, Is.EqualTo(100));
    }

    [Test]
    public void DamageImmunity_TicksSafely_AndCanBeCleared()
    {
        BattleCoreRuntime core = new();
        core.Configure(100, true);

        Assert.That(
            core.TryGrantDamageImmunity(
                BattleCoreRuntime.DefaultDamageImmunityDuration),
            Is.True);
        Assert.That(core.IsDamageImmune, Is.True);
        Assert.That(core.TakeDamage(20), Is.Zero);
        Assert.That(core.CurrentHealth, Is.EqualTo(100));

        core.Tick(float.NaN);
        core.Tick(float.PositiveInfinity);
        core.Tick(-1f);
        Assert.That(
            core.DamageImmunityRemaining,
            Is.EqualTo(3f).Within(0.001f));

        core.Tick(2.9f);
        Assert.That(core.IsDamageImmune, Is.True);
        core.Tick(0.1f);
        Assert.That(core.IsDamageImmune, Is.False);
        Assert.That(core.TakeDamage(20), Is.EqualTo(20));

        Assert.That(core.TryGrantDamageImmunity(3f), Is.True);
        core.ClearTransientDefenses();
        Assert.That(core.IsDamageImmune, Is.False);
        Assert.That(core.DamageImmunityRemaining, Is.Zero);
        Assert.That(core.TakeDamage(10), Is.EqualTo(10));

        Assert.That(core.TryGrantDamageImmunity(3f), Is.True);
        core.Configure(100, true, 50);
        Assert.That(core.IsDamageImmune, Is.False);
        Assert.That(core.DamageImmunityRemaining, Is.Zero);
    }

    [Test]
    public void DamageRedirect_AppliesThirtyPercentOnce_AndReturnsCoreDamage()
    {
        BattleCoreRuntime core = new();
        core.Configure(100, true);
        TestBattleCharacter target = new(100);

        Assert.That(
            core.TrySetNextDamageRedirect(
                target,
                BattleCoreRuntime.DefaultDamageRedirectRatio),
            Is.True);
        Assert.That(core.HasPendingDamageRedirect, Is.True);
        Assert.That(core.TakeDamage(10), Is.EqualTo(7));
        Assert.That(core.CurrentHealth, Is.EqualTo(93));
        Assert.That(target.CurrentHealth, Is.EqualTo(97));
        Assert.That(core.HasPendingDamageRedirect, Is.False);

        Assert.That(core.TakeDamage(10), Is.EqualTo(10));
        Assert.That(core.CurrentHealth, Is.EqualTo(83));
        Assert.That(target.CurrentHealth, Is.EqualTo(97));

        core.Configure(5, true);
        Assert.That(core.TrySetNextDamageRedirect(target, 0.3f), Is.True);
        Assert.That(core.TakeDamage(10), Is.EqualTo(5));
        Assert.That(core.IsDestroyed, Is.True);
        Assert.That(target.CurrentHealth, Is.EqualTo(94));
    }

    [Test]
    public void DamageRedirect_UsesFullDamageWhenTargetDiesBeforeHit()
    {
        BattleCoreRuntime core = new();
        core.Configure(100, true);
        TestBattleCharacter target = new(5);

        Assert.That(core.TrySetNextDamageRedirect(target, 0.3f), Is.True);
        Assert.That(target.TakeDamage(5), Is.EqualTo(5));
        Assert.That(target.CurrentHealth, Is.Zero);

        Assert.That(core.TakeDamage(10), Is.EqualTo(10));
        Assert.That(core.CurrentHealth, Is.EqualTo(90));
        Assert.That(core.HasPendingDamageRedirect, Is.False);
    }

    private sealed class TestEffect : IBattleEffectDefinition
    {
        public BattleEffectType BattleEffectType { get; }
        public BattleEffectTargetMode BattleTargetMode { get; }
        public ScalingValue AmountScaling { get; }
        public BattleEffectPreconditionFailurePolicy
            BattlePreconditionFailurePolicy =>
                BattleEffectPreconditionFailurePolicy.AbortSequence;
        public BattleEffectFailurePolicy BattleFailurePolicy =>
            BattleEffectFailurePolicy.Continue;
        public IBattleEffectTargetSelector BattleTargetSelector => null;
        public bool RequiresActionTargets =>
            BattleEffectRules.RequiresActionTargets(this);
        public CharacterAttackDamageType DamageType =>
            CharacterAttackDamageType.Fixed;
        public StatusEffectSO SourceStatusScalingEffect => null;
        public StatusEffectSO TargetStatusScalingEffect => null;
        public float StatusDuration => 1f;
        public float StatusStacks => 1f;
        public StatusEffectSO StatusEffect => null;
        public CharacterStatusRemovalTarget StatusRemovalTarget => default;
        public CharacterStatusRemovalSelection StatusRemovalSelection =>
            default;
        public CharacterStatusRemovalPickMode StatusRemovalPickMode =>
            default;
        public int StatusRemovalPickCount => 1;
        public CharacterStatusRemovalAmountMode StatusRemovalAmountMode =>
            default;
        public int StatusRemovalCount => 1;
        public float StatusRemovalRatio => 1f;
        public CharacterStatusRemovalAmount StatusRemovalAmount => default;

        public TestEffect(
            BattleEffectType effectType,
            BattleEffectTargetMode targetMode,
            ScalingValue amountScaling)
        {
            BattleEffectType = effectType;
            BattleTargetMode = targetMode;
            AmountScaling = amountScaling;
        }
    }

    private sealed class ObjectiveBoard :
        IBattleBoard,
        IBattleObjectiveProvider
    {
        public IBattleObjective Objective { get; }
        public int InitialEnemyCapacity => 0;
        public int LivingEnemyCount => 0;
        public bool HasEmptyEnemyTile => false;

#pragma warning disable CS0067
        public event Action OccupancyChanged;
        public event Action<BattleEnemyDefeatedEvent> EnemyDefeated;
        public event Action<BattleStatusAppliedEvent> StatusApplied;
#pragma warning restore CS0067

        public ObjectiveBoard(IBattleObjective objective)
        {
            Objective = objective;
        }

        public bool TryAddEnemy(EnemyRuntime enemy) => false;

        public bool TryAddEnemiesToDistinctTiles(
            IReadOnlyList<EnemyRuntime> enemies) => false;

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
            IReadOnlyList<CharacterNumericCondition> numericConditions) =>
            Array.Empty<EnemyRuntime>();

        public IReadOnlyList<IBattleCharacter> SelectAlliedCharacters(
            IBattleCharacter source,
            CharacterAttackSubject subject,
            CharacterAttackSubjectMetric metric,
            int targetCount,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions) =>
            Array.Empty<IBattleCharacter>();

        public IReadOnlyList<EnemyRuntime> FilterCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions) =>
            targets ?? Array.Empty<EnemyRuntime>();

        public IReadOnlyList<IBattleCharacter> FilterAlliedCharacters(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions) =>
            targets ?? Array.Empty<IBattleCharacter>();

        public IReadOnlyList<EnemyRuntime> ExpandCharacterAreaTargets(
            IReadOnlyList<EnemyRuntime> centerTargets,
            IReadOnlyList<CharacterTargetAreaOffset> areaOffsets) =>
            centerTargets ?? Array.Empty<EnemyRuntime>();

        public int TryDamageCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            int damage,
            CharacterAttackDamageType damageType,
            bool showAttackRange) => 0;

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
    }

    private sealed class TestBattleCharacter : IBattleCharacter
    {
        public int PartySlotIndex => 0;
        public int TotalDamageDealt { get; private set; }
        public int CurrentHealth { get; private set; }
        public int MaximumHealth { get; }
        public int CurrentShield => 0;
        public float DisabledTimeRemaining => 0f;
        public float CurrentAttackPower => 0f;
        public float CurrentAttackSpeed => 0f;

#pragma warning disable CS0067
        public event Action<BattleStatusChangedEvent> StatusChanged;
#pragma warning restore CS0067

        public TestBattleCharacter(int maximumHealth)
        {
            MaximumHealth = Mathf.Max(1, maximumHealth);
            CurrentHealth = MaximumHealth;
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
            if (amount <= 0 || CurrentHealth <= 0)
                return 0;
            int applied = Mathf.Min(MaximumHealth - CurrentHealth, amount);
            CurrentHealth += applied;
            return applied;
        }

        public int GainShield(int amount) => 0;

        public int TakeDamage(int amount)
        {
            if (amount <= 0 || CurrentHealth <= 0)
                return 0;
            int applied = Mathf.Min(CurrentHealth, amount);
            CurrentHealth -= applied;
            return applied;
        }

        public bool CanSpendHealth(int amount)
        {
            return amount > 0 && CurrentHealth > amount;
        }

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
            CurrentHealth = MaximumHealth;
            TotalDamageDealt = 0;
        }

        public void TickBattle(float deltaTime, IBattleBoard board)
        {
        }

        public void RecordDamageDealt(int damage)
        {
            TotalDamageDealt = BattleValueMath.SaturatingAddNonNegative(
                TotalDamageDealt,
                damage);
        }

        public void DisableFor(float duration)
        {
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
}
