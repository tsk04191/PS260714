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

public enum BattleItemLifecycle
{
    Disposable = 0,
    Reusable = 1,
}

public enum BattleItemChargeMode
{
    Limited = 0,
    Unlimited = 1,
}

public enum BattleItemEffectScope
{
    CurrentBattle = 0,
    CurrentDungeon = 1,
}

public enum BattleItemEffectDurationMode
{
    Instant = 0,
    Timed = 1,
    Permanent = 2,
}

public enum BattleItemEffectType
{
    ForcePriorityTarget = 0,
    ApplyFire = 1,
    FixedDamage = 2,
    AttackSpeedBoost = 3,
    PowerBoost = 4,
    CharacterModifier = 5,
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
    [SerializeField, HideInInspector]
    private int schemaVersion;
    [SerializeField] private BattleItemEffectType effectType;
    [SerializeField]
    private BattleItemEffectScope scope;
    [SerializeField]
    private BattleItemEffectDurationMode durationMode;
    [SerializeField, Min(0)] private int amount = 1;
    [SerializeField, Min(0f)] private float duration = 1f;
    [SerializeField, Min(0.01f)] private float interval = 1f;
    [SerializeField, Min(0f)] private float multiplier = 1f;
    [SerializeField]
    private List<CharacterModifierModule> modifierModules = new();

    public BattleItemEffectType EffectType => effectType;
    public BattleItemEffectScope Scope => scope;
    public BattleItemEffectDurationMode DurationMode => schemaVersion > 0
        ? durationMode
        : effectType == BattleItemEffectType.FixedDamage
            ? BattleItemEffectDurationMode.Instant
            : BattleItemEffectDurationMode.Timed;
    public int Amount => Mathf.Max(0, amount);
    public float Duration => Mathf.Max(0f, duration);
    public float Interval => Mathf.Max(0.01f, interval);
    public float Multiplier => Mathf.Max(0f, multiplier);
    public IReadOnlyList<CharacterModifierModule> ModifierModules =>
        modifierModules != null
            ? modifierModules
            : Array.Empty<CharacterModifierModule>();
    public bool IsPermanent =>
        DurationMode == BattleItemEffectDurationMode.Permanent;
    public float RuntimeDuration => DurationMode switch
    {
        BattleItemEffectDurationMode.Permanent => float.PositiveInfinity,
        BattleItemEffectDurationMode.Timed => Duration,
        _ => 0f,
    };

    public CharacterModifierLifetimeScope ModifierLifetimeScope =>
        scope == BattleItemEffectScope.CurrentDungeon
            ? CharacterModifierLifetimeScope.Dungeon
            : CharacterModifierLifetimeScope.Battle;

    public void Validate()
    {
        if (schemaVersion <= 0)
        {
            scope = BattleItemEffectScope.CurrentBattle;
            durationMode = effectType == BattleItemEffectType.FixedDamage
                ? BattleItemEffectDurationMode.Instant
                : BattleItemEffectDurationMode.Timed;
            schemaVersion = 1;
        }
        amount = Mathf.Max(0, amount);
        duration = Mathf.Max(0f, duration);
        interval = Mathf.Max(0.01f, interval);
        multiplier = Mathf.Max(0f, multiplier);
        modifierModules ??= new List<CharacterModifierModule>();
        foreach (CharacterModifierModule module in modifierModules)
            module?.Validate();
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
    [SerializeField, HideInInspector]
    private int usageSchemaVersion;
    [SerializeField]
    private BattleItemLifecycle lifecycle;
    [SerializeField]
    private BattleItemChargeMode chargeMode;
    [SerializeField, Min(1)] private int limitedUses = 2;
    [SerializeField, Min(0)] private int maximumRunUses;
    [SerializeField, Min(0)] private int energyCost;
    [SerializeField, Min(0f)] private float cooldown;
    [SerializeField] private bool availableAsDungeonReward = true;
    [SerializeField] private bool availableAsStartingItem = true;
    [SerializeField]
    private List<CharacterEffectDefinition> abilityEffects = new();
    [Tooltip(
        "Legacy battle-item effects. New items use Ability Effects, which " +
        "share CharacterSkillDefinition's effect model.")]
    [SerializeField] private List<BattleItemEffectDefinition> effects = new();

    public BattleItemTargetType TargetType => targetType;
    public BattleItemUsePolicy UsePolicy => UsesLegacyUsagePolicy
        ? usePolicy
        : lifecycle == BattleItemLifecycle.Disposable
            ? BattleItemUsePolicy.SingleUse
            : chargeMode == BattleItemChargeMode.Unlimited
                ? BattleItemUsePolicy.UnlimitedUse
                : BattleItemUsePolicy.LimitedUse;
    public bool UsesLegacyUsagePolicy => usageSchemaVersion <= 0;
    public BattleItemLifecycle Lifecycle => UsesLegacyUsagePolicy
        ? usePolicy == BattleItemUsePolicy.SingleUse
            ? BattleItemLifecycle.Disposable
            : BattleItemLifecycle.Reusable
        : lifecycle;
    public BattleItemChargeMode ChargeMode => UsesLegacyUsagePolicy
        ? usePolicy == BattleItemUsePolicy.UnlimitedUse
            ? BattleItemChargeMode.Unlimited
            : BattleItemChargeMode.Limited
        : chargeMode;
    public bool IsDisposable => Lifecycle == BattleItemLifecycle.Disposable;
    public bool IsReusable => Lifecycle == BattleItemLifecycle.Reusable;
    public bool HasUnlimitedUses =>
        ChargeMode == BattleItemChargeMode.Unlimited;
    public int UsesPerBattle => IsDisposable
        ? 1
        : HasUnlimitedUses
            ? 0
            : UsesLegacyUsagePolicy &&
              usePolicy == BattleItemUsePolicy.LimitedUse
                ? Mathf.Max(2, limitedUses)
                : Mathf.Max(1, limitedUses);
    public int UsesPerAcquisition => UsesPerBattle;
    public int MaximumRunUses => Mathf.Max(0, maximumRunUses);
    public int EnergyCost => Mathf.Max(0, energyCost);
    public float Cooldown => Mathf.Max(0f, cooldown);
    public bool AvailableAsDungeonReward => availableAsDungeonReward;
    public bool AvailableAsStartingItem => availableAsStartingItem;
    public IReadOnlyList<CharacterEffectDefinition> AbilityEffects =>
        abilityEffects ??= new List<CharacterEffectDefinition>();
    public bool UsesUnifiedAbilityEffects => AbilityEffects.Count > 0;
    public IReadOnlyList<BattleItemEffectDefinition> Effects =>
        effects ??= new List<BattleItemEffectDefinition>();
    public bool HasCompatibleEffects
    {
        get
        {
            if (UsesUnifiedAbilityEffects)
            {
                foreach (CharacterEffectDefinition effect in AbilityEffects)
                {
                    if (!IsAbilityEffectCompatible(effect))
                        return false;
                }
                return true;
            }

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

    public override string GetLocalizedDescription()
    {
        CharacterEffectDefinition primaryAbility =
            AbilityEffects.Count > 0 ? AbilityEffects[0] : null;
        BattleItemEffectDefinition primaryEffect =
            Effects.Count > 0 ? Effects[0] : null;
        float duration = primaryAbility != null
            ? primaryAbility.StatusDuration
            : primaryEffect?.Duration ?? 0f;
        float interval = primaryAbility?.StatusEffect?.TickInterval ??
                         primaryEffect?.Interval ?? 0f;
        int amount = primaryAbility != null
            ? Mathf.Max(0, Mathf.RoundToInt(primaryAbility.Amount))
            : primaryEffect?.Amount ?? 0;
        LocalizationArgument[] arguments =
        {
            LocalizationService.Arg("cost", EnergyCost),
            LocalizationService.Arg("duration", duration),
            LocalizationService.Arg("interval", interval),
            LocalizationService.Arg(
                "multiplier",
                primaryEffect?.Multiplier ?? 0f),
            LocalizationService.Arg("damage", amount),
            LocalizationService.Arg("amount", amount),
            LocalizationService.Arg("uses", UsesPerAcquisition),
        };
        if (TryResolveCurrentLocale(
                DescriptionLocalizationKey,
                out string localized,
                arguments))
        {
            return localized;
        }

        return GetDescription(IsCurrentLocaleKorean());
    }

    public bool IsAbilityEffectCompatible(
        CharacterEffectDefinition effect)
    {
        if (effect == null ||
            !Enum.IsDefined(typeof(CharacterEffectType), effect.Type) ||
            !Enum.IsDefined(
                typeof(CharacterEffectTargetMode),
                effect.TargetMode) ||
            !Enum.IsDefined(
                typeof(CharacterEffectPreconditionFailurePolicy),
                effect.PreconditionFailurePolicy) ||
            !Enum.IsDefined(
                typeof(CharacterEffectFailurePolicy),
                effect.FailurePolicy) ||
            !effect.AmountScaling.IsFinite)
        {
            return false;
        }

        bool usesTargets = effect.Type != CharacterEffectType.GainResource &&
                           effect.Type != CharacterEffectType.SpendResource &&
                           effect.Type != CharacterEffectType.SpendHealth;
        CharacterTargetFaction targetFaction = targetType ==
                                                BattleItemTargetType.Enemy
            ? CharacterTargetFaction.Enemy
            : CharacterTargetFaction.Ally;
        if (usesTargets &&
            effect.TargetMode == CharacterEffectTargetMode.FreshSelection)
        {
            // Enemy-targeted items have no allied character source for the
            // board's character-skill target selector. Their manually chosen
            // enemy remains available through InheritAction or Source.
            if (targetType == BattleItemTargetType.Enemy ||
                effect.TargetSelector == null ||
                effect.TargetSelector.Subject ==
                    CharacterAttackSubject.None ||
                effect.TargetSelector.Subject ==
                    CharacterAttackSubject.Manual)
            {
                return false;
            }
            targetFaction = effect.TargetSelector.TargetFaction;
        }

        switch (effect.Type)
        {
            case CharacterEffectType.Damage:
                return IsDirectDamageType(effect.DamageType) &&
                       effect.AmountScaling.HasNonZeroTerm;

            case CharacterEffectType.ApplyStatus:
                return effect.StatusEffect != null &&
                       (targetFaction == CharacterTargetFaction.Ally
                           ? effect.StatusEffect.CanTargetAlly
                           : effect.StatusEffect.CanTargetEnemy);

            case CharacterEffectType.RemoveStatus:
                return effect.StatusRemovalTarget !=
                           CharacterStatusRemovalTarget.Single ||
                       effect.StatusRemovalSelection.HasExplicitStatus;

            case CharacterEffectType.GainResource:
            case CharacterEffectType.Heal:
            case CharacterEffectType.Shield:
                return effect.AmountScaling.HasNonZeroTerm;

            case CharacterEffectType.SpendResource:
            case CharacterEffectType.SpendHealth:
                return effect.AmountMode == CharacterDamageAmountMode.Fixed &&
                       effect.Amount >= 1f;

            default:
                return false;
        }
    }

    public bool IsEffectCompatible(BattleItemEffectDefinition effect)
    {
        if (effect == null)
            return false;

        if (targetType == BattleItemTargetType.Enemy &&
            (effect.Scope == BattleItemEffectScope.CurrentDungeon ||
             effect.DurationMode ==
                 BattleItemEffectDurationMode.Permanent))
        {
            return false;
        }
        if (effect.EffectType ==
                BattleItemEffectType.CharacterModifier &&
            (effect.ModifierModules.Count == 0 ||
             effect.DurationMode ==
                 BattleItemEffectDurationMode.Instant))
        {
            return false;
        }
        if ((effect.EffectType ==
                 BattleItemEffectType.AttackSpeedBoost ||
             effect.EffectType == BattleItemEffectType.PowerBoost) &&
            effect.DurationMode == BattleItemEffectDurationMode.Instant)
        {
            return false;
        }
        if (effect.DurationMode == BattleItemEffectDurationMode.Timed &&
            effect.Duration <= 0f)
        {
            return false;
        }
        if (effect.EffectType == BattleItemEffectType.FixedDamage &&
            effect.DurationMode != BattleItemEffectDurationMode.Instant)
        {
            return false;
        }
        if ((effect.EffectType ==
                 BattleItemEffectType.ForcePriorityTarget ||
             effect.EffectType == BattleItemEffectType.ApplyFire) &&
            effect.DurationMode != BattleItemEffectDurationMode.Timed)
        {
            return false;
        }

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
                effect.EffectType == BattleItemEffectType.PowerBoost ||
                effect.EffectType ==
                    BattleItemEffectType.CharacterModifier,
            _ => false,
        };
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        if (usageSchemaVersion <= 0)
        {
            lifecycle = usePolicy == BattleItemUsePolicy.SingleUse
                ? BattleItemLifecycle.Disposable
                : BattleItemLifecycle.Reusable;
            chargeMode = usePolicy == BattleItemUsePolicy.UnlimitedUse
                ? BattleItemChargeMode.Unlimited
                : BattleItemChargeMode.Limited;
            usageSchemaVersion = 1;
        }
        if (lifecycle == BattleItemLifecycle.Disposable)
        {
            chargeMode = BattleItemChargeMode.Limited;
            limitedUses = 1;
        }
        else
        {
            limitedUses = Mathf.Max(1, limitedUses);
        }
        usePolicy = lifecycle == BattleItemLifecycle.Disposable
            ? BattleItemUsePolicy.SingleUse
            : chargeMode == BattleItemChargeMode.Unlimited
                ? BattleItemUsePolicy.UnlimitedUse
                : BattleItemUsePolicy.LimitedUse;
        maximumRunUses = Mathf.Max(0, maximumRunUses);
        energyCost = Mathf.Max(0, energyCost);
        cooldown = Mathf.Max(0f, cooldown);
        abilityEffects ??= new List<CharacterEffectDefinition>();
        foreach (CharacterEffectDefinition effect in abilityEffects)
            effect?.Validate();
        effects ??= new List<BattleItemEffectDefinition>();
        foreach (BattleItemEffectDefinition effect in effects)
            effect?.Validate();
    }

    private static bool IsDirectDamageType(
        CharacterAttackDamageType damageType)
    {
        return damageType == CharacterAttackDamageType.Physical ||
               damageType == CharacterAttackDamageType.Magical ||
               damageType == CharacterAttackDamageType.Fixed;
    }

}

public sealed class BattleItemRunState
{
    public string ItemId { get; }
    public bool IsOwned { get; private set; }
    public bool IsInDeck => IsOwned;
    public bool IsRemoved { get; private set; }
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

        if (!item.UsesLegacyUsagePolicy)
        {
            if (IsOwned || IsRemoved)
                return false;

            IsOwned = true;
            RemainingUses = item.HasUnlimitedUses
                ? 0
                : item.UsesPerBattle;
            return true;
        }

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
            if (item.UsesLegacyUsagePolicy || item.IsDisposable)
            {
                IsOwned = RemainingUses > 0;
                IsRemoved = item.IsDisposable && RemainingUses <= 0;
            }
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

    public void BeginBattle(BattleItemSO item)
    {
        ResetCooldown();
        if (!Matches(item) || item.UsesLegacyUsagePolicy ||
            !IsOwned || !item.IsReusable || item.HasUnlimitedUses)
        {
            return;
        }

        RemainingUses = item.UsesPerBattle;
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
