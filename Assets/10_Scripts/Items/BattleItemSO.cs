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

public enum BattleItemStatusDurationMode
{
    EffectDuration = 0,
    UntilBattleEnd = 1,
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

[Serializable]
public sealed class BattleItemEnemyTargeting
{
    [SerializeField]
    private BattleAreaDefinition areaDefinition = new();

    public bool IncludeCenterTarget => true;
    public BattleAreaDefinition AreaDefinition =>
        areaDefinition ??= new BattleAreaDefinition();
    public bool HasAnyTargetCell
    {
        get
        {
            return true;
        }
    }

    public void Validate()
    {
        areaDefinition ??= new BattleAreaDefinition();
        areaDefinition.Validate();
    }
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
public sealed class BattleItemSO : ItemDefinitionSO,
    IBattleAbilityDefinition,
    IBattleAbilityProvider
{
    [Header("Battle Item")]
    [SerializeField] private BattleItemTargetType targetType;
    [SerializeField]
    private BattleItemEnemyTargeting enemyTargeting = new();
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
    [Header("Rest Room")]
    [SerializeField] private bool availableInRest;
    [SerializeField, Min(0), Tooltip(
        "이 아이템을 보유한 채 휴식방에 입장하면 추가되는 행동 횟수입니다.")]
    private int additionalRestActions;
    [SerializeField]
    private DungeonRestTargetEffectDefinition[] restEffects =
        Array.Empty<DungeonRestTargetEffectDefinition>();
    [Tooltip(
        "Controls the lifetime of Apply Status ability effects created by " +
        "this item. Until Battle End is cleared by the next battle reset.")]
    [SerializeField]
    private BattleItemStatusDurationMode appliedStatusDurationMode;
    [SerializeField]
    private List<CharacterEffectDefinition> abilityEffects = new();
    [Tooltip(
        "Legacy battle-item effects. New items use Ability Effects, which " +
        "share CharacterSkillDefinition's effect model.")]
    [SerializeField] private List<BattleItemEffectDefinition> effects = new();

    public BattleItemTargetType TargetType => targetType;
    public BattleItemEnemyTargeting EnemyTargeting =>
        enemyTargeting ??= new BattleItemEnemyTargeting();
    public bool HasUsableTargetArea =>
        targetType != BattleItemTargetType.Enemy ||
        EnemyTargeting.HasAnyTargetCell;
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
    public bool AvailableInRest => availableInRest &&
                                   RestEffects.Count > 0;
    public int AdditionalRestActions => Mathf.Max(0, additionalRestActions);
    public IReadOnlyList<DungeonRestTargetEffectDefinition> RestEffects =>
        restEffects ?? Array.Empty<DungeonRestTargetEffectDefinition>();
    public BattleItemStatusDurationMode AppliedStatusDurationMode =>
        appliedStatusDurationMode;
    public bool StatusEffectsLastUntilBattleEnd =>
        appliedStatusDurationMode ==
        BattleItemStatusDurationMode.UntilBattleEnd;
    public IReadOnlyList<CharacterEffectDefinition> AbilityEffects =>
        abilityEffects ??= new List<CharacterEffectDefinition>();
    public bool UsesUnifiedAbilityEffects => AbilityEffects.Count > 0;
    public IReadOnlyList<BattleItemEffectDefinition> Effects =>
        effects ??= new List<BattleItemEffectDefinition>();
    public string AbilityId => ItemId;
    public AbilityExecutionDomain ExecutionDomain =>
        AbilityExecutionDomain.Battle;
    public int AbilitySchemaVersion => UsesUnifiedAbilityEffects ? 1 : 0;
    public BattleEffectOriginKind OriginKind =>
        BattleEffectOriginKind.BattleItem;
    public BattleAbilityTargeting Targeting => new(
        targetType == BattleItemTargetType.Enemy
            ? BattleAbilityTargetRelation.Hostile
            : BattleAbilityTargetRelation.Friendly,
        BattleAbilitySelectionMode.Manual,
        BattleAbilityTargetMetric.None,
        1,
        0,
        false,
        EnemyTargeting.AreaDefinition);
    public IEnumerable<IBattleEffectDefinition> BattleEffects =>
        (IEnumerable<IBattleEffectDefinition>)abilityEffects ??
        Array.Empty<IBattleEffectDefinition>();
    public bool UsesLegacyEffectStorage =>
        !UsesUnifiedAbilityEffects && Effects.Count > 0;
    public bool HasExecutableContent =>
        UsesUnifiedAbilityEffects || Effects.Count > 0;

    public IEnumerable<IBattleAbilityDefinition> EnumerateBattleAbilities()
    {
        if (UsesUnifiedAbilityEffects)
            yield return this;
    }
    public bool HasCompatibleEffects
    {
        get
        {
            if (!HasUsableTargetArea)
                return false;
            if (targetType == BattleItemTargetType.Enemy &&
                EnemyTargeting.AreaDefinition.UsesWorldArea)
            {
                return false;
            }

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
        List<LocalizationArgument> arguments = new(
            BattleAbilityLocalizationArguments.Build(this));
        arguments.Add(LocalizationService.Arg("cost", EnergyCost));
        arguments.Add(LocalizationService.Arg("interval", interval));
        arguments.Add(LocalizationService.Arg(
            "multiplier",
            primaryEffect?.Multiplier ?? 0f));
        arguments.Add(LocalizationService.Arg(
            "uses",
            UsesPerAcquisition));
        if (primaryAbility == null)
        {
            arguments.Add(LocalizationService.Arg("duration", duration));
            arguments.Add(LocalizationService.Arg("damage", amount));
            arguments.Add(LocalizationService.Arg("amount", amount));
        }
        if (TryResolveCurrentLocale(
                DescriptionLocalizationKey,
                out string localized,
                arguments.ToArray()))
        {
            return localized;
        }

        return GetDescription(IsCurrentLocaleKorean());
    }

    public bool IsAbilityEffectCompatible(
        CharacterEffectDefinition effect)
    {
        if (!BattleEffectRules.TryValidate(effect, out _))
        {
            return false;
        }

        bool usesTargets = BattleEffectRules.RequiresTargets(
            effect.BattleEffectType);
        CharacterTargetFaction targetFaction = targetType ==
                                                BattleItemTargetType.Enemy
            ? CharacterTargetFaction.Enemy
            : CharacterTargetFaction.Ally;
        if (usesTargets && effect.TargetMode ==
            CharacterEffectTargetMode.Source)
        {
            targetFaction = CharacterTargetFaction.Ally;
        }
        else if (usesTargets && effect.TargetMode ==
                 CharacterEffectTargetMode.FreshSelection)
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
                return targetFaction == CharacterTargetFaction.Enemy;

            case CharacterEffectType.ApplyStatus:
                return CanStatusTargetFaction(
                    effect.StatusEffect,
                    targetFaction);

            case CharacterEffectType.RemoveStatus:
                if (effect.StatusRemovalTarget !=
                    CharacterStatusRemovalTarget.Single)
                {
                    return true;
                }
                CharacterStatusRemovalSelection selection =
                    effect.StatusRemovalSelection;
                for (int index = 0;
                     index < selection.ExplicitStatusCount;
                     index++)
                {
                    if (!CanStatusTargetFaction(
                            selection.GetExplicitStatus(index),
                            targetFaction))
                    {
                        return false;
                    }
                }
                return true;

            case CharacterEffectType.GainResource:
            case CharacterEffectType.SpendResource:
            case CharacterEffectType.Heal:
            case CharacterEffectType.SpendHealth:
            case CharacterEffectType.Shield:
            case CharacterEffectType.CardDraw:
                return true;

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
    }

    private static bool CanStatusTargetFaction(
        StatusEffectSO status,
        CharacterTargetFaction targetFaction)
    {
        return status != null &&
               (targetFaction == CharacterTargetFaction.Ally
                   ? status.CanTargetAlly
                   : status.CanTargetEnemy);
    }

}

public sealed class BattleItemRunState
{
    public string ItemId { get; }
    public bool IsOwned { get; private set; }
    public bool IsInDeck => IsOwned;
    public bool IsRemoved { get; private set; }
    public int OwnedCopies { get; private set; }
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

            return AcquireCopies(item, 1);
        }

        if (item.HasUnlimitedUses)
        {
            if (IsOwned)
                return false;

            IsOwned = true;
            OwnedCopies = 1;
            RemainingUses = 0;
            return true;
        }

        int nextUses = item.ClampRunUses(
            (long)RemainingUses + item.UsesPerAcquisition);
        if (nextUses <= RemainingUses)
            return false;

        RemainingUses = nextUses;
        IsOwned = true;
        OwnedCopies++;
        return true;
    }

    public bool AcquireCopies(BattleItemSO item, int copyCount)
    {
        if (!Matches(item) || copyCount <= 0 || IsOwned || IsRemoved)
            return false;

        int uses = item.HasUnlimitedUses
            ? 0
            : item.ClampRunUses(
                (long)item.UsesPerAcquisition * copyCount);
        if (!item.HasUnlimitedUses && uses <= 0)
            return false;

        IsOwned = true;
        OwnedCopies = copyCount;
        RemainingUses = uses;
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

    public bool CanUseInRest(BattleItemSO item)
    {
        return Matches(item) && item.AvailableInRest && IsOwned &&
               (item.HasUnlimitedUses || RemainingUses > 0);
    }

    public bool CompleteSuccessfulRestUse(BattleItemSO item)
    {
        if (!CanUseInRest(item))
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

        RemainingUses = item.ClampRunUses(
            (long)item.UsesPerBattle * Math.Max(1, OwnedCopies));
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
