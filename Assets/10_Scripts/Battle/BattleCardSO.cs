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

    [Header("Targeting")]
    [SerializeField] private CharacterTargetFaction targetFaction =
        CharacterTargetFaction.Enemy;
    [SerializeField] private CharacterAttackSubject subject =
        CharacterAttackSubject.Manual;
    [SerializeField] private CharacterAttackSubjectMetric subjectMetric;
    [SerializeField, Min(0)] private int targetCount = 1;
    [SerializeField] private BattleAreaDefinition areaDefinition = new();

    [Header("Ability")]
    [SerializeField] private List<CharacterEffectDefinition> abilityEffects =
        new();

    public string CardId => (cardId ?? string.Empty).Trim();
    public ItemRarity Rarity => rarity;
    public int SortOrder => sortOrder;
    public Sprite Icon => icon;
    public Sprite Illustration => illustration;
    public BattleCardAffiliation Affiliation => affiliation;
    public CharacterSO OwnerCharacter => ownerCharacter;
    public IReadOnlyList<CharacterSO> RequiredCharacters =>
        requiredCharacters ??= new List<CharacterSO>();
    public BattleCardRequirementMatchMode RequirementMode => requirementMode;
    public BattleCardSourcePolicy SourcePolicy => sourcePolicy;
    public int EnergyCost => Mathf.Max(0, energyCost);
    public BattleCardRecyclePolicy RecyclePolicy => recyclePolicy;
    public bool AvailableAsStartingCard => availableAsStartingCard;
    public bool AvailableAsDungeonReward => availableAsDungeonReward;
    public CharacterTargetFaction TargetFaction => targetFaction;
    public CharacterAttackSubject Subject => subject;
    public CharacterAttackSubjectMetric SubjectMetric => subjectMetric;
    public int TargetCount => AreaDefinition.UsesWorldArea
        ? Mathf.Max(0, targetCount)
        : Mathf.Max(1, targetCount);
    public BattleAreaDefinition AreaDefinition =>
        areaDefinition ??= new BattleAreaDefinition();
    public IReadOnlyList<CharacterEffectDefinition> AbilityEffects =>
        abilityEffects ??= new List<CharacterEffectDefinition>();

    public string AbilityId => CardId;
    public AbilityExecutionDomain ExecutionDomain =>
        AbilityExecutionDomain.Battle;
    public int AbilitySchemaVersion => 1;
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
        (IEnumerable<IBattleEffectDefinition>)AbilityEffects;
    public bool UsesLegacyEffectStorage => false;
    public bool HasExecutableContent => AbilityEffects.Count > 0;
    public bool RequiresActionTargets =>
        BattleAbilityRules.RequiresActionTargets(this);

    public IEnumerable<IBattleAbilityDefinition> EnumerateBattleAbilities()
    {
        yield return this;
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
                if (!AbilityDefinitionValidator.TryValidate(
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
