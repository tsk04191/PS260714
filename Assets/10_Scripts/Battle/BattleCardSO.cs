using System;
using System.Collections.Generic;
using PS260714.Localization;
using UnityEngine;
using UnityEngine.Serialization;

public enum BattleCardAffiliation
{
    Neutral = 0,
    CharacterExclusive = 1,
    CharacterDependent = 2,
}

public enum BattleCardRequirementMatchMode
{
    Any = 0,
    All = 1,
}

public enum BattleCardSourcePolicy
{
    FirstAvailableCharacter = 0,
    FixedCharacter = 1,
    FirstRequiredCharacter = 2,
}

public enum BattleCardRecyclePolicy
{
    Discard = 0,
    Exhaust = 1,
}

[CreateAssetMenu(
    fileName = "BattleCard",
    menuName = "PS260714/Cards/Battle Card")]
public sealed class BattleCardSO : ScriptableObject,
    IBattleAbilityDefinition,
    IBattleAbilityProvider
{
    [Header("Identity")]
    [SerializeField] private string cardId;
    [SerializeField] private ItemRarity rarity;
    [SerializeField] private int sortOrder;

    [Header("Localization")]
    [SerializeField] private string nameLocalizationKey;
    [SerializeField] private string descriptionLocalizationKey;
    [FormerlySerializedAs("koreanName")]
    [SerializeField] private string fallbackName;
    [FormerlySerializedAs("koreanDescription")]
    [SerializeField, TextArea] private string fallbackDescription;

    [Header("Presentation")]
    [SerializeField] private Sprite icon;
    [SerializeField] private Sprite illustration;

    [Header("Card Affiliation")]
    [SerializeField] private BattleCardAffiliation affiliation;
    [SerializeField] private CharacterSO ownerCharacter;
    [SerializeField] private List<CharacterSO> requiredCharacters = new();
    [SerializeField] private BattleCardRequirementMatchMode requirementMode;
    [SerializeField] private BattleCardSourcePolicy sourcePolicy;

    [Header("Play Rules")]
    [SerializeField, Min(0)] private int energyCost;
    [SerializeField] private BattleCardRecyclePolicy recyclePolicy;
    [SerializeField] private bool availableAsStartingCard = true;
    [SerializeField] private bool availableAsDungeonReward = true;
    [SerializeField, Min(1)] private int minimumMaximumEnergy = 1;

    [Header("Targeting")]
    [SerializeField] private CharacterTargetFaction targetFaction =
        CharacterTargetFaction.Enemy;
    [SerializeField] private CharacterAttackSubject subject =
        CharacterAttackSubject.Manual;
    [SerializeField] private CharacterAttackSubjectMetric subjectMetric;
    [SerializeField, Min(0)] private int targetCount = 1;
    [SerializeField] private BattleAreaDefinition areaDefinition = new();
    [SerializeField] private BattleCardTargetFilter primaryTargetFilter = new();
    [SerializeField]
    private BattleCardSecondaryTargetDefinition secondaryTarget = new();

    [Header("Ability")]
    [SerializeField] private List<CharacterEffectDefinition> abilityEffects =
        new();
    [SerializeField]
    private List<BattleCardOperationDefinition> operations = new();

    public string CardId => (cardId ?? string.Empty).Trim();
    public ItemRarity Rarity => rarity;
    public int SortOrder => sortOrder;
    public Sprite Icon => icon;
    public Sprite Illustration => illustration;
    public BattleCardAffiliation Affiliation => affiliation;
    public CharacterSO OwnerCharacter => ownerCharacter;
    public IReadOnlyList<CharacterSO> RequiredCharacters =>
        requiredCharacters ?? (IReadOnlyList<CharacterSO>)
            Array.Empty<CharacterSO>();
    public BattleCardRequirementMatchMode RequirementMode => requirementMode;
    public BattleCardSourcePolicy SourcePolicy => sourcePolicy;
    public int EnergyCost => Mathf.Max(0, energyCost);
    public BattleCardRecyclePolicy RecyclePolicy => recyclePolicy;
    public bool AvailableAsStartingCard => availableAsStartingCard;
    public bool AvailableAsDungeonReward => availableAsDungeonReward;
    public int MinimumMaximumEnergy => Mathf.Max(1, minimumMaximumEnergy);
    internal bool HasValidRawPlayRules =>
        energyCost >= 0 && minimumMaximumEnergy >= 1;
    internal bool HasAreaDefinition => areaDefinition != null;
    internal bool HasPrimaryTargetFilterDefinition =>
        primaryTargetFilter != null;
    internal bool HasSecondaryTargetDefinition => secondaryTarget != null;
    internal bool HasAbilityEffectStorage => abilityEffects != null;
    internal bool HasOperationStorage => operations != null;
    public CharacterTargetFaction TargetFaction => targetFaction;
    public CharacterAttackSubject Subject => subject;
    public CharacterAttackSubjectMetric SubjectMetric => subjectMetric;
    public int TargetCount => AreaDefinition.UsesWorldArea
        ? Mathf.Max(0, targetCount)
        : Mathf.Max(1, targetCount);
    public BattleAreaDefinition AreaDefinition =>
        areaDefinition ?? new BattleAreaDefinition();
    public BattleCardTargetFilter PrimaryTargetFilter =>
        primaryTargetFilter ?? new BattleCardTargetFilter();
    public BattleCardSecondaryTargetDefinition SecondaryTarget =>
        secondaryTarget ?? new BattleCardSecondaryTargetDefinition();
    public IReadOnlyList<CharacterEffectDefinition> AbilityEffects =>
        abilityEffects ?? (IReadOnlyList<CharacterEffectDefinition>)
            Array.Empty<CharacterEffectDefinition>();
    public IReadOnlyList<BattleCardOperationDefinition> Operations =>
        operations ?? (IReadOnlyList<BattleCardOperationDefinition>)
            Array.Empty<BattleCardOperationDefinition>();

    public string AbilityId => CardId;
    public AbilityExecutionDomain ExecutionDomain =>
        AbilityExecutionDomain.Battle;
    public int AbilitySchemaVersion => Operations.Count > 0 ? 2 : 1;
    public BattleEffectOriginKind OriginKind =>
        BattleEffectOriginKind.BattleCard;
    public BattleAbilityTargeting Targeting =>
        BattleAbilityTargeting.FromCharacter(
            targetFaction,
            subject,
            subjectMetric,
            TargetCount,
            AreaDefinition);
    public IEnumerable<IBattleEffectDefinition> BattleEffects =>
        EnumerateBattleEffects();
    public bool UsesLegacyEffectStorage => false;
    public bool HasExecutableContent =>
        AbilityEffects.Count > 0 || Operations.Count > 0;
    public bool RequiresActionTargets
    {
        get
        {
            if (BattleAbilityRules.RequiresActionTargets(this))
                return true;
            foreach (BattleCardOperationDefinition operation in Operations)
            {
                if (operation?.UsesPrimaryTarget == true ||
                    operation?.UsesSecondaryTarget == true ||
                    AreaDefinition.UsesWorldArea &&
                    operation?.UsesDesignatedPoint == true)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public IEnumerable<IBattleAbilityDefinition> EnumerateBattleAbilities()
    {
        yield return this;
    }

    public bool AllowsOperationPrimaryTarget(CharacterSO character)
    {
        bool hasCharacterRestrictedPrimaryOperation = false;
        bool matchesRestrictedOperation = false;
        foreach (BattleCardOperationDefinition operation in Operations)
        {
            if (operation?.TargetScope != BattleCardTargetScope.Primary)
                continue;
            if (operation.RequiredCharacter == null)
                return true;

            hasCharacterRestrictedPrimaryOperation = true;
            if (ReferenceEquals(operation.RequiredCharacter, character))
                matchesRestrictedOperation = true;
        }

        return !hasCharacterRestrictedPrimaryOperation ||
               matchesRestrictedOperation;
    }

    private IEnumerable<IBattleEffectDefinition> EnumerateBattleEffects()
    {
        foreach (CharacterEffectDefinition effect in AbilityEffects)
        {
            if (effect != null)
                yield return effect;
        }
    }

    public bool IsEligible(IReadOnlyList<CharacterSO> party)
    {
        if (sourcePolicy == BattleCardSourcePolicy.FixedCharacter &&
            !ContainsCharacter(party, ownerCharacter))
        {
            return false;
        }
        if (affiliation == BattleCardAffiliation.Neutral)
            return true;
        if (affiliation == BattleCardAffiliation.CharacterExclusive)
            return ownerCharacter != null && ContainsCharacter(party, ownerCharacter);

        if (RequiredCharacters.Count == 0)
            return false;
        if (requirementMode == BattleCardRequirementMatchMode.All)
        {
            foreach (CharacterSO required in RequiredCharacters)
            {
                if (required == null || !ContainsCharacter(party, required))
                    return false;
            }
            return true;
        }

        foreach (CharacterSO required in RequiredCharacters)
        {
            if (required != null && ContainsCharacter(party, required))
                return true;
        }
        return false;
    }

    public CharacterSO ResolveSourceDefinition(
        IReadOnlyList<CharacterSO> party)
    {
        if (affiliation == BattleCardAffiliation.CharacterExclusive ||
            sourcePolicy == BattleCardSourcePolicy.FixedCharacter)
        {
            return ContainsCharacter(party, ownerCharacter)
                ? ownerCharacter
                : null;
        }

        if (sourcePolicy == BattleCardSourcePolicy.FirstRequiredCharacter)
        {
            foreach (CharacterSO required in RequiredCharacters)
            {
                if (required != null && ContainsCharacter(party, required))
                    return required;
            }
        }

        if (party != null)
        {
            foreach (CharacterSO character in party)
            {
                if (character != null)
                    return character;
            }
        }
        return null;
    }

    public string GetLocalizedDisplayName()
    {
        if (TryGetLocalized(nameLocalizationKey, out string localized))
            return localized;
        if (!string.IsNullOrWhiteSpace(fallbackName))
            return fallbackName.Trim();
        return CardId;
    }

    public string GetLocalizedDescription()
    {
        LocalizationArgument[] arguments =
            BattleAbilityLocalizationArguments.Build(this);
        if (TryGetLocalized(
                descriptionLocalizationKey,
                out string localized,
                arguments))
            return localized;
        if (!string.IsNullOrWhiteSpace(fallbackDescription))
            return fallbackDescription.Trim();
        return string.Empty;
    }

    private void OnValidate()
    {
        BattleCardCatalog.Invalidate();
    }

    private static bool ContainsCharacter(
        IReadOnlyList<CharacterSO> party,
        CharacterSO target)
    {
        if (party == null || target == null)
            return false;
        foreach (CharacterSO character in party)
        {
            if (ReferenceEquals(character, target))
                return true;
        }
        return false;
    }

    private static bool TryGetLocalized(
        string key,
        out string value,
        params LocalizationArgument[] arguments)
    {
        key = (key ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(key) &&
            LocalizationService.TryGet(key, out value, arguments))
        {
            return !string.IsNullOrWhiteSpace(value);
        }
        value = string.Empty;
        return false;
    }

}

public static class BattleCardDefinitionValidator
{
    public static bool TryValidate(BattleCardSO card, out string error)
    {
        if (card == null)
        {
            error = "Battle card is null.";
            return false;
        }
        if (!AbilityDefinitionValidator.TryValidate(card, out error))
            return false;
        if (!TryValidateCardSchema(card, out error))
            return false;
        if (card.Affiliation != BattleCardAffiliation.Neutral)
            return true;

        if (card.OwnerCharacter != null ||
            card.RequiredCharacters.Count > 0 ||
            card.SourcePolicy != BattleCardSourcePolicy.FirstAvailableCharacter)
        {
            error = "A neutral common card cannot declare a character " +
                    "owner, requirement, or source policy.";
            return false;
        }
        if (card.AreaDefinition.UsesWorldArea &&
            card.AreaDefinition.OriginMode == CharacterAreaOriginMode.Caster)
        {
            error = "A neutral common card must place world areas from a " +
                    "designated pointer point.";
            return false;
        }

        foreach (IBattleEffectDefinition effect in card.BattleEffects)
        {
            if (UsesCharacterOnlySource(effect))
            {
                error = "A neutral common card cannot use character-only " +
                        "source targeting or source-unit scaling.";
                return false;
            }
        }
        foreach (BattleCardOperationDefinition operation in card.Operations)
        {
            if (operation != null &&
                (operation.Type == BattleCardOperationType.SharedEffect ||
                 operation.Type == BattleCardOperationType.CreateZone) &&
                operation.SharedEffect != null &&
                UsesCharacterOnlySource(operation.SharedEffect))
            {
                error = "A neutral common card operation cannot use " +
                        "character-only source targeting or source-unit " +
                        "scaling.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateCardSchema(
        BattleCardSO card,
        out string error)
    {
        if (!Enum.IsDefined(typeof(ItemRarity), card.Rarity) ||
            !Enum.IsDefined(
                typeof(BattleCardAffiliation),
                card.Affiliation) ||
            !Enum.IsDefined(
                typeof(BattleCardRequirementMatchMode),
                card.RequirementMode) ||
            !Enum.IsDefined(
                typeof(BattleCardSourcePolicy),
                card.SourcePolicy) ||
            !Enum.IsDefined(
                typeof(BattleCardRecyclePolicy),
                card.RecyclePolicy) ||
            !Enum.IsDefined(
                typeof(CharacterTargetFaction),
                card.TargetFaction) ||
            !Enum.IsDefined(
                typeof(CharacterAttackSubject),
                card.Subject) ||
            !Enum.IsDefined(
                typeof(CharacterAttackSubjectMetric),
                card.SubjectMetric))
        {
            error = "Battle card contains an undefined card enum value.";
            return false;
        }

        if (!card.HasValidRawPlayRules)
        {
            error = "Battle card energy values cannot be negative or zero " +
                    "where a positive maximum is required.";
            return false;
        }

        int requiredMaximumEnergy = card.EnergyCost >= 4
            ? card.EnergyCost
            : 1;
        if (card.MinimumMaximumEnergy < requiredMaximumEnergy)
        {
            error = $"A cost-{card.EnergyCost} card requires minimum " +
                    $"maximum energy {requiredMaximumEnergy} or greater.";
            return false;
        }

        if (!card.HasAreaDefinition ||
            !card.HasPrimaryTargetFilterDefinition ||
            !card.HasSecondaryTargetDefinition ||
            !card.HasAbilityEffectStorage ||
            !card.HasOperationStorage)
        {
            error = "Battle card targeting and operation collections must " +
                    "not be null.";
            return false;
        }

        if (!TryValidateTargetFilter(
                card.PrimaryTargetFilter,
                card.TargetFaction,
                "Primary target",
                out error) ||
            !TryValidateSecondaryTarget(card.SecondaryTarget, out error))
        {
            return false;
        }

        for (int index = 0; index < card.AbilityEffects.Count; index++)
        {
            if (card.AbilityEffects[index] == null)
            {
                error = $"Battle card shared effect {index + 1} is null.";
                return false;
            }
        }

        HashSet<string> operationIds = new(StringComparer.Ordinal);
        for (int index = 0; index < card.Operations.Count; index++)
        {
            BattleCardOperationDefinition operation =
                card.Operations[index];
            if (!TryValidateOperation(
                    card,
                    operation,
                    index,
                    operationIds,
                    out error))
            {
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateSecondaryTarget(
        BattleCardSecondaryTargetDefinition secondary,
        out string error)
    {
        if (secondary == null || !secondary.HasAreaDefinition ||
            !secondary.HasFilterDefinition)
        {
            error = "Secondary target definition, area, and filter must " +
                    "not be null.";
            return false;
        }
        if (!secondary.Enabled)
        {
            error = string.Empty;
            return true;
        }
        if (!Enum.IsDefined(
                typeof(CharacterTargetFaction),
                secondary.TargetFaction) ||
            !Enum.IsDefined(
                typeof(CharacterAttackSubject),
                secondary.Subject) ||
            !Enum.IsDefined(
                typeof(CharacterAttackSubjectMetric),
                secondary.SubjectMetric) ||
            !secondary.HasValidTargetCount ||
            !secondary.AreaDefinition.IsValid)
        {
            error = "Secondary target selection is invalid.";
            return false;
        }

        if (secondary.UsesWorldPoint)
        {
            if (!secondary.AreaDefinition.UsesWorldArea ||
                secondary.AreaDefinition.OriginMode !=
                    CharacterAreaOriginMode.DesignatedPoint)
            {
                error = "A secondary world point requires a designated " +
                        "world-area definition.";
                return false;
            }
        }
        else if (secondary.Subject == CharacterAttackSubject.None ||
                 secondary.AreaDefinition.UsesWorldArea)
        {
            error = "A secondary unit target requires a unit selection " +
                    "subject and a target-shaped area.";
            return false;
        }

        return TryValidateTargetFilter(
            secondary.Filter,
            secondary.TargetFaction,
            "Secondary target",
            out error);
    }

    private static bool TryValidateTargetFilter(
        BattleCardTargetFilter filter,
        CharacterTargetFaction faction,
        string label,
        out string error)
    {
        if (filter == null)
        {
            error = $"{label} filter is null.";
            return false;
        }
        if (faction != CharacterTargetFaction.Ally &&
            (filter.RequiredRole != null ||
             filter.RequiredCharacter != null ||
             filter.IncludeDefeated))
        {
            error = $"{label} character, role, and defeated filters " +
                    "require an allied target faction.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateOperation(
        BattleCardSO card,
        BattleCardOperationDefinition operation,
        int index,
        HashSet<string> operationIds,
        out string error)
    {
        string path = $"Operation {index + 1}";
        if (operation == null)
        {
            error = $"{path} is null.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(operation.OperationId) ||
            !operationIds.Add(operation.OperationId))
        {
            error = $"{path} requires a unique non-empty operation ID.";
            return false;
        }
        if (!Enum.IsDefined(
                typeof(BattleCardOperationType),
                operation.Type) ||
            !Enum.IsDefined(
                typeof(BattleCardTargetScope),
                operation.TargetScope) ||
            !Enum.IsDefined(
                typeof(BattleCardMovementMode),
                operation.MovementMode) ||
            !Enum.IsDefined(
                typeof(BattleCardZoneTrigger),
                operation.ZoneTrigger) ||
            !Enum.IsDefined(
                typeof(BattleCardCostModifierMode),
                operation.CostModifierMode) ||
            !Enum.IsDefined(
                typeof(BattleCardSpatialZone),
                operation.SpatialZone))
        {
            error = $"{path} contains an undefined operation enum value.";
            return false;
        }
        if (!operation.HasValidNumericValues ||
            !operation.HasValidSelectionRange)
        {
            error = $"{path} contains an invalid numeric or selection " +
                    "range value.";
            return false;
        }
        if (!operation.HasConditionDefinition)
        {
            error = $"{path} condition definition is null.";
            return false;
        }
        if (!TryValidateCondition(
                operation.Condition,
                index,
                operation.TargetScope,
                path,
                out error))
        {
            return false;
        }

        if (operation.UsesPrimaryTarget &&
            (card.Subject == CharacterAttackSubject.None ||
             !card.Targeting.HasTarget))
        {
            error = $"{path} requires a configured primary target.";
            return false;
        }
        if (operation.UsesSecondaryTarget &&
            !card.SecondaryTarget.Enabled)
        {
            error = $"{path} requires an enabled secondary target.";
            return false;
        }
        if ((operation.TargetScope ==
                 BattleCardTargetScope.NearbyPrimaryEnemies ||
             operation.TargetScope ==
                 BattleCardTargetScope.BehindPrimaryEnemy) &&
            card.TargetFaction != CharacterTargetFaction.Enemy)
        {
            error = $"{path} requires an enemy primary target.";
            return false;
        }
        if (operation.TargetScope ==
                BattleCardTargetScope.EnemiesAtDesignatedPoint &&
            !HasDesignatedPoint(card))
        {
            error = $"{path} requires a designated primary or secondary " +
                    "world point.";
            return false;
        }
        if (operation.TargetScope ==
                BattleCardTargetScope.EnemiesWithStatus &&
            operation.RequiredStatus == null)
        {
            error = $"{path} requires a status filter.";
            return false;
        }
        if (operation.TargetScope == BattleCardTargetScope.AlliesWithRole &&
            operation.RequiredRole == null)
        {
            error = $"{path} requires a role filter.";
            return false;
        }
        if (operation.TargetScope ==
                BattleCardTargetScope.SpecificCharacter &&
            operation.RequiredCharacter == null)
        {
            error = $"{path} requires a character reference.";
            return false;
        }
        if (TryResolveScopeFaction(
                card,
                operation.TargetScope,
                out CharacterTargetFaction scopeFaction) &&
            scopeFaction != CharacterTargetFaction.Ally &&
            (operation.RequiredRole != null ||
             operation.RequiredCharacter != null))
        {
            error = $"{path} character and role filters require allied " +
                    "targets.";
            return false;
        }

        return TryValidateOperationType(
            card,
            operation,
            path,
            out error);
    }

    private static bool TryValidateCondition(
        BattleCardConditionDefinition condition,
        int operationIndex,
        BattleCardTargetScope targetScope,
        string path,
        out string error)
    {
        if (condition == null ||
            !Enum.IsDefined(typeof(BattleCardConditionType), condition.Type) ||
            !Enum.IsDefined(
                typeof(CharacterNumericComparison),
                condition.Comparison) ||
            !Enum.IsDefined(
                typeof(BattleCardSpatialZone),
                condition.Zone) ||
            !condition.HasFiniteThreshold)
        {
            error = $"{path} has an invalid condition definition.";
            return false;
        }
        if (condition.Type == BattleCardConditionType.PartyRoleCount &&
            condition.Role == null)
        {
            error = $"{path} party-role condition requires a role.";
            return false;
        }
        if (condition.Type == BattleCardConditionType.TargetHasStatus &&
            condition.StatusEffect == null)
        {
            error = $"{path} target-status condition requires a status.";
            return false;
        }
        if ((condition.Type ==
                 BattleCardConditionType.PreviousOperationSucceeded ||
             condition.Type ==
                 BattleCardConditionType.PreviousOperationFailed ||
             condition.Type ==
                 BattleCardConditionType.PreviousOperationDefeatedAny) &&
            operationIndex == 0)
        {
            error = $"{path} cannot query a previous operation at index 1.";
            return false;
        }
        if ((condition.Type ==
                 BattleCardConditionType.TargetHealthPercentage ||
             condition.Type == BattleCardConditionType.TargetZone ||
             condition.Type == BattleCardConditionType.TargetHasStatus) &&
            targetScope == BattleCardTargetScope.None)
        {
            error = $"{path} target condition requires a target scope.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateOperationType(
        BattleCardSO card,
        BattleCardOperationDefinition operation,
        string path,
        out string error)
    {
        switch (operation.Type)
        {
            case BattleCardOperationType.SharedEffect:
            case BattleCardOperationType.CreateZone:
                string effectError = "Shared effect is missing.";
                if (!operation.HasSharedEffectDefinition ||
                    !BattleEffectRules.TryValidate(
                        operation.SharedEffect,
                        out effectError))
                {
                    error = $"{path} has an invalid shared effect: " +
                            effectError;
                    return false;
                }
                if (operation.Type == BattleCardOperationType.CreateZone &&
                    (!HasDesignatedPoint(card) || operation.Radius <= 0f))
                {
                    error = $"{path} zone requires a designated point and " +
                            "a positive radius.";
                    return false;
                }
                break;

            case BattleCardOperationType.ObjectiveRestore:
                if (operation.Amount <= 0 &&
                    !operation.UsePreviousChangedCount)
                {
                    error = $"{path} objective restore requires an amount.";
                    return false;
                }
                break;

            case BattleCardOperationType.ObjectiveInvulnerability:
                if (operation.Duration <= 0f)
                {
                    error = $"{path} objective immunity requires a " +
                            "positive duration.";
                    return false;
                }
                break;

            case BattleCardOperationType.ObjectiveDamageRedirect:
                if (operation.Ratio <= 0f || operation.Ratio > 1f ||
                    !TargetsAllies(card, operation.TargetScope))
                {
                    error = $"{path} objective redirect requires an allied " +
                            "target and a ratio in (0, 1].";
                    return false;
                }
                break;

            case BattleCardOperationType.SpendTargetHealth:
            case BattleCardOperationType.Revive:
                if (!TargetsAllies(card, operation.TargetScope) ||
                    operation.Type ==
                        BattleCardOperationType.SpendTargetHealth &&
                    operation.Amount <= 0 ||
                    operation.Type == BattleCardOperationType.Revive &&
                    operation.Amount <= 0 && operation.Ratio <= 0f)
                {
                    error = $"{path} requires an allied target and a " +
                            "positive amount or ratio.";
                    return false;
                }
                break;

            case BattleCardOperationType.Draw:
                if (operation.Count <= 0 &&
                    !operation.UsePreviousChangedCount)
                {
                    error = $"{path} draw requires a positive count.";
                    return false;
                }
                break;

            case BattleCardOperationType.DiscardSelected:
            case BattleCardOperationType.ExhaustSelected:
            case BattleCardOperationType.ReturnDiscarded:
                if (operation.MaximumSelectionCount <= 0)
                {
                    error = $"{path} card selection requires a positive " +
                            "maximum count.";
                    return false;
                }
                break;

            case BattleCardOperationType.GainEnergy:
                if (operation.Amount <= 0 &&
                    !operation.UsePreviousChangedCount)
                {
                    error = $"{path} energy gain requires an amount.";
                    return false;
                }
                break;

            case BattleCardOperationType.ModifyCardCost:
                if (operation.Count <= 0 ||
                    operation.CostModifierMode ==
                        BattleCardCostModifierMode.Add &&
                    operation.Amount <= 0)
                {
                    error = $"{path} cost modifier requires a positive use " +
                            "count and additive amount.";
                    return false;
                }
                break;

            case BattleCardOperationType.Move:
                if (!TargetsAllies(card, operation.TargetScope) ||
                    operation.MovementMode ==
                        BattleCardMovementMode.ToWorldPoint &&
                    !HasDesignatedPoint(card) ||
                    operation.MovementMode ==
                        BattleCardMovementMode.ToTargetFlank &&
                    (!card.SecondaryTarget.Enabled ||
                     card.SecondaryTarget.UsesWorldPoint ||
                     card.SecondaryTarget.TargetFaction !=
                        CharacterTargetFaction.Enemy))
                {
                    error = $"{path} movement targeting is incomplete.";
                    return false;
                }
                break;

            case BattleCardOperationType.Swap:
                if (!TargetsAllies(card, operation.TargetScope))
                {
                    error = $"{path} swap requires allied targets.";
                    return false;
                }
                break;

            case BattleCardOperationType.PullEnemies:
                if (!HasDesignatedPoint(card))
                {
                    error = $"{path} pull requires a designated point.";
                    return false;
                }
                break;

            case BattleCardOperationType.ApplyAttackModifier:
            case BattleCardOperationType.ApplySkillModifier:
            case BattleCardOperationType.ApplyHealthTrigger:
                if (!TargetsAllies(card, operation.TargetScope))
                {
                    error = $"{path} modifier requires allied targets.";
                    return false;
                }
                break;

            case BattleCardOperationType.ExtendStatusDuration:
                if (operation.StatusEffect == null ||
                    operation.Duration <= 0f)
                {
                    error = $"{path} status extension requires a status " +
                            "and positive duration.";
                    return false;
                }
                break;

            case BattleCardOperationType.ForceTarget:
                if (operation.Duration <= 0f)
                {
                    error = $"{path} forced target requires a positive " +
                            "duration.";
                    return false;
                }
                break;
        }

        error = string.Empty;
        return true;
    }

    private static bool HasDesignatedPoint(BattleCardSO card)
    {
        return card.AreaDefinition.UsesWorldArea &&
                   card.AreaDefinition.OriginMode ==
                       CharacterAreaOriginMode.DesignatedPoint ||
               card.SecondaryTarget.Enabled &&
                   card.SecondaryTarget.UsesWorldPoint &&
                   card.SecondaryTarget.AreaDefinition.UsesWorldArea &&
                   card.SecondaryTarget.AreaDefinition.OriginMode ==
                       CharacterAreaOriginMode.DesignatedPoint;
    }

    private static bool TargetsAllies(
        BattleCardSO card,
        BattleCardTargetScope scope)
    {
        return TryResolveScopeFaction(card, scope, out
                   CharacterTargetFaction faction) &&
               faction == CharacterTargetFaction.Ally;
    }

    private static bool TryResolveScopeFaction(
        BattleCardSO card,
        BattleCardTargetScope scope,
        out CharacterTargetFaction faction)
    {
        switch (scope)
        {
            case BattleCardTargetScope.Primary:
                faction = card.TargetFaction;
                return true;
            case BattleCardTargetScope.Secondary:
                faction = card.SecondaryTarget.TargetFaction;
                return card.SecondaryTarget.Enabled &&
                       !card.SecondaryTarget.UsesWorldPoint;
            case BattleCardTargetScope.Source:
            case BattleCardTargetScope.AllAllies:
            case BattleCardTargetScope.AlliesWithRole:
            case BattleCardTargetScope.LowestHealthAlly:
            case BattleCardTargetScope.DeadOrLowestHealthAlly:
            case BattleCardTargetScope.SpecificCharacter:
                faction = CharacterTargetFaction.Ally;
                return true;
            case BattleCardTargetScope.AllEnemies:
            case BattleCardTargetScope.RandomEnemies:
            case BattleCardTargetScope.EnemiesWithStatus:
            case BattleCardTargetScope.NearbyPrimaryEnemies:
            case BattleCardTargetScope.BehindPrimaryEnemy:
            case BattleCardTargetScope.DefenseLineEnemies:
            case BattleCardTargetScope.RecentObjectiveAttackers:
            case BattleCardTargetScope.EnemiesAtDesignatedPoint:
                faction = CharacterTargetFaction.Enemy;
                return true;
            default:
                faction = default;
                return false;
        }
    }

    private static bool UsesAmountScaling(BattleEffectType type)
    {
        return type == BattleEffectType.Damage ||
               type == BattleEffectType.GainResource ||
               type == BattleEffectType.SpendResource ||
               type == BattleEffectType.Heal ||
               type == BattleEffectType.SpendHealth ||
               type == BattleEffectType.Shield ||
               type == BattleEffectType.CardDraw;
    }

    private static bool UsesCharacterOnlySource(
        IBattleEffectDefinition effect)
    {
        if (effect == null)
            return false;
        ScalingValue scaling = effect.AmountScaling;
        return effect.BattleTargetMode == BattleEffectTargetMode.Source ||
               effect.BattleEffectType == BattleEffectType.SpendHealth ||
               UsesAmountScaling(effect.BattleEffectType) &&
               (scaling.SourceAttackPowerScale != 0f ||
                scaling.SourceCurrentHealthScale != 0f ||
                scaling.SourceMaximumHealthScale != 0f ||
                scaling.SourceStatusStacksScale != 0f);
    }
}

[Serializable]
public sealed class BattleCardDeckEntry
{
    [SerializeField] private BattleCardSO card;
    [SerializeField, Min(1)] private int copies = 1;

    public BattleCardSO Card => card;
    public int Copies => Mathf.Max(1, copies);
}

[Serializable]
public sealed class BattleCardDeckRules
{
    public const int DefaultBaseDrawCount = 5;
    public const int DefaultHandSize = DefaultBaseDrawCount;
    public const float DefaultRedrawCooldown = 10f;

    [FormerlySerializedAs("handSize")]
    [SerializeField, Range(1, 10), Tooltip(
        "Cards drawn each turn before adding the participating party's " +
        "total Judgment stat.")]
    private int baseDrawCount = DefaultBaseDrawCount;
    [SerializeField, Min(TimePrecision.Step),
     InspectorName("Base Draw Cooldown (Seconds)"), Tooltip(
         "Base automatic redraw interval before adding Knowledge speed. " +
         "Effective cooldown = base cooldown / (1 + party Knowledge).")]
    private float redrawCooldown = DefaultRedrawCooldown;
    [SerializeField, Min(TimePrecision.Step)]
    private float mulliganCooldown = 3f;
    [SerializeField, Min(1)] private int catalogCardCopies = 2;
    [SerializeField, Tooltip(
        "When empty, all eligible catalog cards marked as starting cards " +
        "are used.")]
    private List<BattleCardDeckEntry> startingDeck = new();

    public int BaseDrawCount => Mathf.Clamp(baseDrawCount, 1, 10);
    public int HandSize => BaseDrawCount;
    public float RedrawCooldown => TimePrecision.Normalize(
        redrawCooldown,
        TimePrecision.Step);
    public float MulliganCooldown => TimePrecision.Normalize(
        mulliganCooldown,
        TimePrecision.Step);
    public int CatalogCardCopies => Mathf.Max(1, catalogCardCopies);
    public IReadOnlyList<BattleCardDeckEntry> StartingDeck =>
        startingDeck ??= new List<BattleCardDeckEntry>();

    public int ResolveCardsDrawnPerTurn(int partyJudgment)
    {
        long resolved = (long)BaseDrawCount + Mathf.Max(0, partyJudgment);
        return resolved >= int.MaxValue ? int.MaxValue : (int)resolved;
    }

    public float ResolveRedrawCooldown(int partyKnowledge)
    {
        return ResolveKnowledgeCooldown(RedrawCooldown, partyKnowledge);
    }

    public float ResolveMulliganCooldown(int partyKnowledge)
    {
        return ResolveKnowledgeCooldown(MulliganCooldown, partyKnowledge);
    }

    private static float ResolveKnowledgeCooldown(
        float baseCooldown,
        int partyKnowledge)
    {
        double speedMultiplier = 1d + Math.Max(0, partyKnowledge);
        double resolved = baseCooldown / speedMultiplier;
        return TimePrecision.Normalize(
            (float)resolved,
            TimePrecision.Step);
    }

    public void BuildDeck(
        IReadOnlyList<CharacterSO> party,
        List<BattleCardSO> destination)
    {
        destination?.Clear();
        if (destination == null)
            return;

        if (StartingDeck.Count > 0)
        {
            foreach (BattleCardDeckEntry entry in StartingDeck)
            {
                if (entry?.Card == null || !entry.Card.IsEligible(party))
                    continue;
                for (int copy = 0; copy < entry.Copies; copy++)
                    destination.Add(entry.Card);
            }
            return;
        }

        foreach (BattleCardSO card in BattleCardCatalog.GetAll())
        {
            if (card == null || !card.AvailableAsStartingCard ||
                !card.IsEligible(party))
            {
                continue;
            }
            for (int copy = 0; copy < CatalogCardCopies; copy++)
                destination.Add(card);
        }
    }

    public void Validate()
    {
        baseDrawCount = Mathf.Clamp(baseDrawCount, 1, 10);
        redrawCooldown = TimePrecision.Normalize(
            redrawCooldown,
            TimePrecision.Step);
        mulliganCooldown = TimePrecision.Normalize(
            mulliganCooldown,
            TimePrecision.Step);
        catalogCardCopies = Mathf.Max(1, catalogCardCopies);
        startingDeck ??= new List<BattleCardDeckEntry>();
    }
}

public static class BattleCardCatalog
{
    private const string ResourcesPath = "Cards";
    private static readonly List<BattleCardSO> Cards = new();
    private static bool loaded;

    public static IReadOnlyList<BattleCardSO> GetAll()
    {
        if (!loaded)
        {
            Cards.Clear();
            HashSet<string> cardIds = new(StringComparer.OrdinalIgnoreCase);
            foreach (BattleCardSO card in
                     Resources.LoadAll<BattleCardSO>(ResourcesPath))
            {
                if (card == null)
                {
                    Debug.LogError(
                        "A null battle card was excluded from the catalog.");
                    continue;
                }
                if (!BattleCardDefinitionValidator.TryValidate(
                        card,
                        out string error))
                {
                    Debug.LogError(
                        $"Battle card '{card.name}' was " +
                        $"excluded: {error}",
                        card);
                    continue;
                }
                if (!cardIds.Add(card.CardId))
                {
                    Debug.LogError(
                        $"Battle card ID '{card.CardId}' is duplicated. " +
                        "The duplicate asset was excluded.",
                        card);
                    continue;
                }
                Cards.Add(card);
            }
            Cards.Sort((left, right) =>
            {
                int order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.CompareOrdinal(left.CardId, right.CardId);
            });
            loaded = true;
        }
        return Cards;
    }

    public static void Invalidate()
    {
        loaded = false;
        Cards.Clear();
    }
}
