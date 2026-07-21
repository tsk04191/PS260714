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
    event Action<int> Changed;

    bool CanSpend(int amount);
    bool TrySpend(int amount);
}

public interface IBattleBoard
{
    int InitialEnemyCapacity { get; }
    int LivingEnemyCount { get; }
    bool HasEmptyEnemyTile { get; }
    event Action<EnemyRuntime> EnemyDefeated;

    bool TryAddEnemy(EnemyRuntime enemy);
    bool TryAddEnemiesToDistinctTiles(
        IReadOnlyList<EnemyRuntime> enemies);
    void ClearAllEnemies();
    void TickStatusEffects(float deltaTime);
    void TickEnemyAbilities(
        float deltaTime,
        IReadOnlyList<IBattleCharacter> characters);

    bool TryPrepareLowestHealthAttack(
        IBattleCharacter source,
        out bool targetChanged);
    int TryResolveLowestHealthAttack(
        IBattleCharacter source,
        int damage);
    int TryAttackLowestHealthEnemy(
        IBattleCharacter source,
        int damage);
    bool TryPrepareRandomAttack(
        IBattleCharacter source,
        int targetCount,
        out bool targetChanged);
    int TryResolveRandomAttack(
        IBattleCharacter source,
        int damage);
    void ClearPreparedAttack(IBattleCharacter source);
    int TryAttackCrossAroundHighestHealthEnemy(
        IBattleCharacter source,
        int damage);
    int TryAttackCrossWithAdjacentSplash(
        IBattleCharacter source,
        int damage,
        int adjacentDamage);
    bool TryApplyFireToRandomEnemy(
        IBattleCharacter source,
        float duration,
        float tickInterval,
        int tickDamage);
    bool TryApplyFireAroundRandomEnemies(
        IBattleCharacter source,
        int centerTargetCount,
        float duration,
        float tickInterval,
        int tickDamage);
}

public interface IBattleCharacter
{
    int PartySlotIndex { get; }
    Color EffectColor { get; }
    Sprite TargetEffectSprite { get; }
    RuntimeAnimatorController TargetEffectController { get; }
    int TotalDamageDealt { get; }
    float DisabledTimeRemaining { get; }

    bool Initialize();
    void BindBattle(
        IActiveSkillResource activeSkillResource,
        IBattleBoard board);
    void ResetRuntime();
    void TickBattle(float deltaTime, IBattleBoard board);
    void RecordDamageDealt(int damage);
    void DisableFor(float duration);
}
