using System;
using System.Collections.Generic;
using PS260714.Localization;
using UnityEngine;

public enum EBattleItemType
{
    Focus,
    Molotov,
    PrecisionShot,
    OverSupply,
    Overheat,
}

public enum EBattleItemTargetType
{
    Enemy,
    Turret,
}

public readonly struct BattleItemDefinition
{
    public EBattleItemType Type { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public int EnergyCost { get; }
    public EBattleItemTargetType TargetType { get; }
    public bool IsReusable { get; }
    public float Cooldown { get; }

    public BattleItemDefinition(
        EBattleItemType type,
        string displayName,
        string description,
        int energyCost,
        EBattleItemTargetType targetType,
        bool isReusable = false,
        float cooldown = 0f)
    {
        Type = type;
        DisplayName = displayName ?? type.ToString();
        Description = description ?? string.Empty;
        EnergyCost = Mathf.Max(0, energyCost);
        TargetType = targetType;
        IsReusable = isReusable;
        Cooldown = Mathf.Max(0f, cooldown);
    }
}

public static class BattleItemCatalog
{
    private static readonly EBattleItemType[] ConsumableItemTypes =
    {
        EBattleItemType.Molotov,
        EBattleItemType.PrecisionShot,
        EBattleItemType.OverSupply,
        EBattleItemType.Overheat,
    };

    public static IReadOnlyList<EBattleItemType> Consumables =>
        ConsumableItemTypes;

    public static BattleItemDefinition Get(EBattleItemType type)
    {
        return type switch
        {
            EBattleItemType.Molotov => new BattleItemDefinition(
                type,
                LocalizationService.Get(LocalizationKeys.ItemMolotovName),
                LocalizationService.Get(
                    LocalizationKeys.ItemMolotovEffect,
                    LocalizationService.Arg("duration", 3f)),
                3,
                EBattleItemTargetType.Enemy),
            EBattleItemType.PrecisionShot => new BattleItemDefinition(
                type,
                LocalizationService.Get(
                    LocalizationKeys.ItemPrecisionShotName),
                LocalizationService.Get(
                    LocalizationKeys.ItemPrecisionShotEffect,
                    LocalizationService.Arg("damage", 5)),
                2,
                EBattleItemTargetType.Enemy),
            EBattleItemType.OverSupply => new BattleItemDefinition(
                type,
                LocalizationService.Get(
                    LocalizationKeys.ItemOverSupplyName),
                LocalizationService.Get(
                    LocalizationKeys.ItemOverSupplyEffect,
                    LocalizationService.Arg("multiplier", 2f),
                    LocalizationService.Arg("duration", 5f)),
                3,
                EBattleItemTargetType.Turret),
            EBattleItemType.Overheat => new BattleItemDefinition(
                type,
                LocalizationService.Get(
                    LocalizationKeys.ItemOverheatName),
                LocalizationService.Get(
                    LocalizationKeys.ItemOverheatEffect,
                    LocalizationService.Arg("multiplier", 2f),
                    LocalizationService.Arg("duration", 3f)),
                3,
                EBattleItemTargetType.Turret),
            _ => new BattleItemDefinition(
                EBattleItemType.Focus,
                LocalizationService.Get(LocalizationKeys.SkillFocusName),
                LocalizationService.Get(
                    LocalizationKeys.SkillFocusEffect,
                    LocalizationService.Arg("duration", 5f),
                    LocalizationService.Arg("cooldown", 10f)),
                1,
                EBattleItemTargetType.Enemy,
                true,
                10f),
        };
    }

    public static bool IsConsumable(EBattleItemType type)
    {
        return !Get(type).IsReusable;
    }
}

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

public readonly struct BattleStatusTarget
{
    public CharacterTargetFaction Faction { get; }
    public IBattleCharacter Ally { get; }
    public EnemyRuntime Enemy { get; }
    public bool IsValid => Ally != null || Enemy != null;

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
        Definition.DurationMode == StatusEffectDurationMode.Permanent;

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

public interface IBattleBoard
{
    int InitialEnemyCapacity { get; }
    int LivingEnemyCount { get; }
    bool HasEmptyEnemyTile { get; }
    event Action<EnemyRuntime> EnemyDefeated;
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
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        int removalCount,
        bool showAttackRange);
    bool TryRemoveAlliedCharacterStatus(
        IBattleCharacter source,
        IReadOnlyList<IBattleCharacter> targets,
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        int removalCount);
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
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        int removalCount);
}
