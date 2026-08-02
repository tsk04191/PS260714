using System;
using System.Collections.Generic;
using PS260714.Localization;
using UnityEngine;

public enum BattleItemTargetType
{
    Enemy = 0,
    Turret = 1,
}

public enum BattleItemUsePolicy
{
    SingleUse = 0,
    LimitedUse = 1,
    UnlimitedUse = 2,
}

public enum BattleItemEffectType
{
    ForcePriorityTarget = 0,
    ApplyFire = 1,
    FixedDamage = 2,
    AttackSpeedBoost = 3,
    PowerBoost = 4,
}

public static class CoreBattleItemIds
{
    public const string Focus = "battle.item.focus";
    public const string Molotov = "battle.item.molotov";
    public const string PrecisionShot = "battle.item.precision_shot";
    public const string OverSupply = "battle.item.over_supply";
    public const string Overheat = "battle.item.overheat";
}

[Serializable]
public sealed class BattleItemEffectDefinition
{
    [SerializeField] private BattleItemEffectType effectType;
    [SerializeField, Min(0)] private int amount = 1;
    [SerializeField, Min(0f)] private float duration = 1f;
    [SerializeField, Min(0.01f)] private float interval = 1f;
    [SerializeField, Min(0f)] private float multiplier = 1f;

    public BattleItemEffectType EffectType => effectType;
    public int Amount => Mathf.Max(0, amount);
    public float Duration => Mathf.Max(0f, duration);
    public float Interval => Mathf.Max(0.01f, interval);
    public float Multiplier => Mathf.Max(0f, multiplier);

    public void Validate()
    {
        amount = Mathf.Max(0, amount);
        duration = Mathf.Max(0f, duration);
        interval = Mathf.Max(0.01f, interval);
        multiplier = Mathf.Max(0f, multiplier);
    }
}

[CreateAssetMenu(
    fileName = "BattleItem",
    menuName = "PS260714/Items/Battle Item")]
public sealed class BattleItemSO : ItemDefinitionSO
{
    [Header("Battle Item")]
    [SerializeField] private BattleItemTargetType targetType;
    [SerializeField] private BattleItemUsePolicy usePolicy;
    [SerializeField, Min(2)] private int limitedUses = 2;
    [SerializeField, Min(0)] private int maximumRunUses;
    [SerializeField, Min(0)] private int energyCost;
    [SerializeField, Min(0f)] private float cooldown;
    [SerializeField] private bool availableAsDungeonReward = true;
    [SerializeField] private bool availableAsStartingItem = true;
    [SerializeField] private List<BattleItemEffectDefinition> effects = new();

    public BattleItemTargetType TargetType => targetType;
    public BattleItemUsePolicy UsePolicy => usePolicy;
    public bool HasUnlimitedUses =>
        usePolicy == BattleItemUsePolicy.UnlimitedUse;
    public int UsesPerAcquisition => usePolicy switch
    {
        BattleItemUsePolicy.SingleUse => 1,
        BattleItemUsePolicy.LimitedUse => Mathf.Max(2, limitedUses),
        _ => 0,
    };
    public int MaximumRunUses => Mathf.Max(0, maximumRunUses);
    public int EnergyCost => Mathf.Max(0, energyCost);
    public float Cooldown => Mathf.Max(0f, cooldown);
    public bool AvailableAsDungeonReward => availableAsDungeonReward;
    public bool AvailableAsStartingItem => availableAsStartingItem;
    public IReadOnlyList<BattleItemEffectDefinition> Effects =>
        effects ??= new List<BattleItemEffectDefinition>();
    public bool HasCompatibleEffects
    {
        get
        {
            if (Effects.Count == 0)
                return false;

            foreach (BattleItemEffectDefinition effect in Effects)
            {
                if (!IsEffectCompatible(effect))
                    return false;
            }
            return true;
        }
    }

    public int ClampRunUses(long uses)
    {
        uses = Math.Max(0L, uses);
        if (MaximumRunUses > 0)
            uses = Math.Min(uses, MaximumRunUses);
        return (int)Math.Min(uses, int.MaxValue);
    }

    public string GetLocalizedDisplayName()
    {
        return GetDisplayName(IsKoreanLocale());
    }

    public string GetLocalizedDescription()
    {
        return GetDescription(IsKoreanLocale());
    }

    public bool IsEffectCompatible(BattleItemEffectDefinition effect)
    {
        if (effect == null)
            return false;

        return targetType switch
        {
            BattleItemTargetType.Enemy =>
                effect.EffectType ==
                    BattleItemEffectType.ForcePriorityTarget ||
                effect.EffectType == BattleItemEffectType.ApplyFire ||
                effect.EffectType == BattleItemEffectType.FixedDamage,
            BattleItemTargetType.Turret =>
                effect.EffectType ==
                    BattleItemEffectType.AttackSpeedBoost ||
                effect.EffectType == BattleItemEffectType.PowerBoost,
            _ => false,
        };
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        limitedUses = Mathf.Max(2, limitedUses);
        maximumRunUses = Mathf.Max(0, maximumRunUses);
        energyCost = Mathf.Max(0, energyCost);
        cooldown = Mathf.Max(0f, cooldown);
        effects ??= new List<BattleItemEffectDefinition>();
        foreach (BattleItemEffectDefinition effect in effects)
            effect?.Validate();
    }

    private static bool IsKoreanLocale()
    {
        return LocalizationService.CurrentLocale?.StartsWith(
            "ko",
            StringComparison.OrdinalIgnoreCase) == true;
    }
}

public sealed class BattleItemRunState
{
    public string ItemId { get; }
    public bool IsOwned { get; private set; }
    public int RemainingUses { get; private set; }
    public float CooldownRemaining { get; private set; }

    public BattleItemRunState(BattleItemSO item)
    {
        ItemId = item != null ? item.ItemId : string.Empty;
    }

    public bool Acquire(BattleItemSO item)
    {
        if (!Matches(item))
            return false;

        if (item.HasUnlimitedUses)
        {
            if (IsOwned)
                return false;

            IsOwned = true;
            RemainingUses = 0;
            return true;
        }

        int nextUses = item.ClampRunUses(
            (long)RemainingUses + item.UsesPerAcquisition);
        if (nextUses <= RemainingUses)
            return false;

        RemainingUses = nextUses;
        IsOwned = true;
        return true;
    }

    public bool CanUse(BattleItemSO item)
    {
        return Matches(item) &&
               IsOwned &&
               CooldownRemaining <= 0f &&
               (item.HasUnlimitedUses || RemainingUses > 0);
    }

    public bool CompleteSuccessfulUse(BattleItemSO item)
    {
        if (!CanUse(item))
            return false;

        if (!item.HasUnlimitedUses)
        {
            RemainingUses = Mathf.Max(0, RemainingUses - 1);
            IsOwned = RemainingUses > 0;
        }

        CooldownRemaining = item.Cooldown;
        return true;
    }

    public bool TickCooldown(float deltaTime)
    {
        if (CooldownRemaining <= 0f || deltaTime <= 0f)
            return false;

        float previous = CooldownRemaining;
        CooldownRemaining = Mathf.Max(0f, previous - deltaTime);
        return !Mathf.Approximately(previous, CooldownRemaining);
    }

    public void ResetCooldown()
    {
        CooldownRemaining = 0f;
    }

    private bool Matches(BattleItemSO item)
    {
        return item != null &&
               !string.IsNullOrWhiteSpace(ItemId) &&
               string.Equals(ItemId, item.ItemId, StringComparison.Ordinal);
    }
}

public static class BattleItemCatalog
{
    public static IReadOnlyList<BattleItemSO> GetAll()
    {
        List<BattleItemSO> battleItems = new();
        foreach (ItemDefinitionSO item in ItemDefinitionCatalog.GetAll())
        {
            if (item is BattleItemSO battleItem)
                battleItems.Add(battleItem);
        }
        return battleItems;
    }

    public static BattleItemSO Get(string itemId)
    {
        return ItemDefinitionCatalog.Get(itemId) as BattleItemSO;
    }
}
