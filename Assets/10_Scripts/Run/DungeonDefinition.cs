using System;
using System.Collections.Generic;
using PS260714.Localization;
using UnityEngine;
using UnityEngine.Serialization;

public enum EDungeonCompletionDestination
{
    Main,
    StageSelect,
}

public enum EDungeonStageSelectVisibility
{
    Listed = 0,
    Hidden = 1,
}

public enum EDungeonRunMode
{
    Standard = 0,
    Practice = 1,
}

public enum DungeonShieldRecoveryAmountMode
{
    Fixed = 0,
    PercentOfMaximum = 1,
}

[Serializable]
public sealed class DungeonShieldRecoveryRule
{
    [SerializeField] private DungeonShieldRecoveryAmountMode amountMode;
    [SerializeField, Min(0f)] private float amount = 20f;

    public DungeonShieldRecoveryAmountMode AmountMode => amountMode;
    public float Amount => amountMode ==
                           DungeonShieldRecoveryAmountMode.PercentOfMaximum
        ? Mathf.Clamp(amount, 0f, 100f)
        : Mathf.Max(0f, amount);

    public int ResolveAmount(int maximumShield)
    {
        maximumShield = Mathf.Max(0, maximumShield);
        if (maximumShield == 0)
            return 0;

        float resolved = amountMode ==
                         DungeonShieldRecoveryAmountMode.PercentOfMaximum
            ? maximumShield * Amount / 100f
            : Amount;
        return Mathf.Max(0, Mathf.CeilToInt(resolved));
    }
}

[CreateAssetMenu(
    fileName = "DungeonDefinition",
    menuName = "Dungeon/Definition")]
public sealed class DungeonDefinition : ScriptableObject
{
    public const int AutomaticClearedBattleHealthCost = -1;
    public const float DefaultActiveSkillCostRecoveryDuration = 10f;
    public const float DefaultBattleArenaRadius =
        BattleArenaSetup.DefaultWorldRadius;
    public const int DefaultBattleShieldMaximumHealth =
        BattleArenaSetup.DefaultCoreMaximumHealth;

    [Header("Identity")]
    [SerializeField] private string dungeonId = "free_battle";
    [SerializeField, Min(1)] private int contentVersion = 1;
    [SerializeField] private EDungeonRunMode runMode;

    [Header("Stage Select")]
    [SerializeField]
    private EDungeonStageSelectVisibility stageSelectVisibility;
    [SerializeField] private int stageOrder;
    [SerializeField] private string titleLocalizationKey;
    [SerializeField] private string fallbackTitle;
    [SerializeField] private Sprite stageCoverSprite;

    [Header("Flow")]
    [SerializeField, Min(1)] private int minimumBattleCount = 5;
    [SerializeField, Min(1)] private int maximumBattleCount = 8;
    [SerializeField] private bool insertEventBetweenBattles = true;
    [SerializeField] private DungeonFlowPolicy flowPolicy;
    [SerializeField, Tooltip(
        "Round-robin room pattern used when no Flow Policy is assigned.")]
    private EDungeonPhase[] roomPattern =
    {
        EDungeonPhase.Event,
        EDungeonPhase.Rest,
        EDungeonPhase.Shop,
    };

    [Header("Run Rules")]
    [SerializeField] private bool selectStartingCharacter = true;
    [FormerlySerializedAs("includeStartingConsumable")]
    [SerializeField, Tooltip(
        "Shows the starting-item loadout step after character selection.")]
    private bool selectStartingItems = true;
    [SerializeField] private DungeonStartingItemRule startingItemRule = new();
    [SerializeField, Tooltip(
        "Uses the draw/hand/discard battle-card loop instead of exposing " +
        "every owned battle item in the combat hand.")]
    private bool useBattleCards = true;
    [SerializeField] private BattleCardDeckRules battleCardDeckRules = new();
    [SerializeField, Tooltip(
        "Uses the tutorial encounter setup for the first battle. " +
        "This requires a Tutorial definition.")]
    private bool useIntroBattleBalance;
    [SerializeField] private EDungeonCompletionDestination completionDestination =
        EDungeonCompletionDestination.Main;
    [SerializeField, Min(0)] private int initialRunCurrency = 100;
    [SerializeField, Min(TimePrecision.Step), Tooltip(
        "Seconds required to recover one active-skill cost during battle.")]
    private float activeSkillCostRecoveryDuration =
        DefaultActiveSkillCostRecoveryDuration;
    [SerializeField, Min(AutomaticClearedBattleHealthCost), Tooltip(
        "Absolute health lost by each participating character after a " +
        "cleared battle. -1 calculates the cost from the current battle " +
        "difficulty scale divided by 10. Set 0 to disable the cost.")]
    private int clearedBattleHealthCost =
        AutomaticClearedBattleHealthCost;

    [Header("Battle Arena")]
    [SerializeField, Min(1), Tooltip(
        "Maximum health of the projected shield defended during dungeon " +
        "battles.")]
    private int battleShieldMaximumHealth =
        DefaultBattleShieldMaximumHealth;
    [SerializeField, Min(BattleArenaSetup.MinimumWorldRadius), Tooltip(
        "World-space radius shared by the circular wall, movement bounds, " +
        "enemy approach, and shield health ring.")]
    private float battleArenaRadius = DefaultBattleArenaRadius;

    [Header("Battle Completion Rewards")]
    [SerializeField]
    private DungeonShieldRecoveryRule shieldRecoveryReward = new();
    [SerializeField, Tooltip(
        "Fallback card reward pool used when the completed BattleSO has " +
        "no card rewards. Empty uses the eligible card catalog.")]
    private BattleCardSO[] battleCardRewardPool =
        Array.Empty<BattleCardSO>();
    [SerializeField, Tooltip(
        "Fallback disposable battle-item pool used when the completed " +
        "BattleSO has no consumable rewards. Empty uses the eligible " +
        "disposable battle-item catalog.")]
    private BattleItemSO[] consumableRewardPool =
        Array.Empty<BattleItemSO>();

    [Header("Encounters")]
    [SerializeField] private BattleSO[] fixedBattles =
        Array.Empty<BattleSO>();
    [SerializeField, Tooltip(
        "Optional authored events in event-occurrence order. Empty slots " +
        "use the built-in fallback event.")]
    private DungeonEventSO[] fixedEvents =
        Array.Empty<DungeonEventSO>();
    [SerializeField, Tooltip(
        "Event used when the occurrence-specific slot is empty.")]
    private DungeonEventSO defaultEvent;
    [SerializeField, Tooltip(
        "Optional authored rest rooms in rest-occurrence order.")]
    private DungeonRestSO[] fixedRests =
        Array.Empty<DungeonRestSO>();
    [SerializeField, Tooltip(
        "Rest room used when the occurrence-specific slot is empty.")]
    private DungeonRestSO defaultRest;
    [SerializeField, Tooltip(
        "Optional authored shops in shop-occurrence order.")]
    private DungeonShopSO[] fixedShops =
        Array.Empty<DungeonShopSO>();
    [SerializeField, Tooltip(
        "Shop used when the occurrence-specific slot is empty.")]
    private DungeonShopSO defaultShop;
    [SerializeField] private EnemySO[] enemyPoolOverride =
        Array.Empty<EnemySO>();

    [Header("Presentation")]
    [SerializeField] private DungeonFieldView fieldViewPrefab;
    [SerializeField] private DungeonThemeDefinition theme;
    [SerializeField] private DungeonBgmProfile bgmProfile;
    [SerializeField] private DungeonTutorialDefinition tutorial;

    [Header("Optional Rule Modules")]
    [SerializeField] private DungeonModifier[] modifiers =
        Array.Empty<DungeonModifier>();

    public string DungeonId =>
        DungeonDefinitionCatalog.NormalizeDungeonId(dungeonId);
    public int ContentVersion => contentVersion;
    public EDungeonRunMode RunMode => runMode;
    public bool IsPractice => RunMode == EDungeonRunMode.Practice;
    public bool UsesStandardBattleCompletion => !IsPractice;
    public bool AwardsBattleRewards => !IsPractice;
    public bool PersistsDungeonProgress => !IsPractice;
    public bool IsListedInStageSelect =>
        stageSelectVisibility == EDungeonStageSelectVisibility.Listed;
    public int StageOrder => stageOrder;
    public string TitleLocalizationKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(titleLocalizationKey))
                return titleLocalizationKey.Trim();
            if (string.Equals(
                    DungeonId,
                    DungeonDefinitionCatalog.TutorialFieldId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return LocalizationKeys.UiStageSelectTutorialField;
            }
            if (string.Equals(
                    DungeonId,
                    DungeonDefinitionCatalog.PracticeBattleId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return LocalizationKeys.UiStageSelectPracticeBattle;
            }
            if (string.Equals(
                    DungeonId,
                    DungeonDefinitionCatalog.FreeBattleId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return LocalizationKeys.UiStageSelectFreeBattle;
            }
            return string.Empty;
        }
    }
    public string FallbackTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(fallbackTitle))
                return fallbackTitle.Trim();
            if (string.Equals(
                    DungeonId,
                    DungeonDefinitionCatalog.TutorialFieldId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "STAGE 0 · TUTORIAL FIELD";
            }
            if (string.Equals(
                    DungeonId,
                    DungeonDefinitionCatalog.PracticeBattleId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "PRACTICE BATTLE";
            }
            if (string.Equals(
                    DungeonId,
                    DungeonDefinitionCatalog.FreeBattleId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "FREE BATTLE";
            }
            return !string.IsNullOrWhiteSpace(DungeonId)
                ? DungeonId
                : name;
        }
    }
    public Sprite StageCoverSprite => stageCoverSprite != null
        ? stageCoverSprite
        : theme != null
            ? theme.BackgroundSprite
            : null;
    public bool SelectStartingCharacter => selectStartingCharacter;
    public bool SelectStartingItems => selectStartingItems;
    public DungeonStartingItemRule StartingItemRule =>
        startingItemRule ?? new DungeonStartingItemRule();
    public bool UseBattleCards => useBattleCards;
    public BattleCardDeckRules BattleCardDeckRules =>
        battleCardDeckRules ?? new BattleCardDeckRules();
    public bool UseIntroBattleBalance => useIntroBattleBalance;
    public bool UsesTutorialBattleSetup =>
        HasTutorial && useIntroBattleBalance;
    public EDungeonCompletionDestination CompletionDestination =>
        completionDestination;
    public int InitialRunCurrency => Mathf.Max(0, initialRunCurrency);
    public float ActiveSkillCostRecoveryDuration =>
        TimePrecision.Normalize(
            activeSkillCostRecoveryDuration,
            TimePrecision.Step);
    public int ClearedBattleHealthCost => Mathf.Max(
        AutomaticClearedBattleHealthCost,
        clearedBattleHealthCost);
    public bool HasClearedBattleHealthCostOverride =>
        ClearedBattleHealthCost >= 0;
    public float BattleArenaRadius =>
        BattleArenaSetup.NormalizeWorldRadius(battleArenaRadius);
    public int BattleShieldMaximumHealth =>
        Mathf.Max(1, battleShieldMaximumHealth);
    public DungeonShieldRecoveryRule ShieldRecoveryReward =>
        shieldRecoveryReward ?? new DungeonShieldRecoveryRule();
    public IReadOnlyList<BattleCardSO> BattleCardRewardPool =>
        battleCardRewardPool ?? Array.Empty<BattleCardSO>();
    public IReadOnlyList<BattleItemSO> ConsumableRewardPool =>
        consumableRewardPool ?? Array.Empty<BattleItemSO>();
    public DungeonFieldView FieldViewPrefab => fieldViewPrefab;
    public DungeonThemeDefinition Theme => theme;
    public DungeonBgmProfile BgmProfile => bgmProfile;
    public DungeonTutorialDefinition Tutorial => tutorial;
    public bool HasTutorial => tutorial != null;
    public IReadOnlyList<DungeonModifier> Modifiers =>
        modifiers ?? Array.Empty<DungeonModifier>();
    public IReadOnlyList<EnemySO> EnemyPoolOverride =>
        enemyPoolOverride ?? Array.Empty<EnemySO>();

    public int ResolveClearedBattleHealthCost(int difficultyScale)
    {
        if (HasClearedBattleHealthCostOverride)
            return ClearedBattleHealthCost;

        return Mathf.Clamp(difficultyScale, 0, 100) / 10;
    }

    public bool TryGetFixedBattle(int battleIndex, out BattleSO battle)
    {
        battle = null;
        if (fixedBattles == null || battleIndex < 0 ||
            battleIndex >= fixedBattles.Length)
        {
            return false;
        }

        battle = fixedBattles[battleIndex];
        return battle != null;
    }

    public bool TryGetFixedEvent(int eventIndex, out DungeonEventSO dungeonEvent)
    {
        dungeonEvent = defaultEvent;
        if (fixedEvents == null || eventIndex < 0 ||
            eventIndex >= fixedEvents.Length)
        {
            return dungeonEvent != null;
        }

        dungeonEvent = fixedEvents[eventIndex] != null
            ? fixedEvents[eventIndex]
            : defaultEvent;
        return dungeonEvent != null;
    }

    public bool TryGetFixedRest(int restIndex, out DungeonRestSO rest)
    {
        rest = defaultRest;
        if (fixedRests == null || restIndex < 0 ||
            restIndex >= fixedRests.Length)
        {
            return rest != null;
        }

        rest = fixedRests[restIndex] != null
            ? fixedRests[restIndex]
            : defaultRest;
        return rest != null;
    }

    public bool TryGetFixedShop(int shopIndex, out DungeonShopSO shop)
    {
        shop = defaultShop;
        if (fixedShops == null || shopIndex < 0 ||
            shopIndex >= fixedShops.Length)
        {
            return shop != null;
        }

        shop = fixedShops[shopIndex] != null
            ? fixedShops[shopIndex]
            : defaultShop;
        return shop != null;
    }

    public int ResolveBattleCount(int runSeed)
    {
        if (flowPolicy != null)
            return Mathf.Max(1, flowPolicy.ResolveBattleCount(runSeed));

        int minimum = Mathf.Max(1, minimumBattleCount);
        int maximum = Mathf.Max(minimum, maximumBattleCount);
        if (minimum == maximum)
            return minimum;

        System.Random random = new(runSeed);
        return random.Next(minimum, maximum + 1);
    }

    public IReadOnlyList<EDungeonPhase> BuildPhaseSequence(
        int battleCount,
        int runSeed)
    {
        battleCount = Mathf.Max(1, battleCount);
        if (flowPolicy != null)
            return flowPolicy.BuildPhaseSequence(battleCount, runSeed);

        int phaseCount = insertEventBetweenBattles
            ? battleCount * 2 - 1
            : battleCount;
        EDungeonPhase[] phases = new EDungeonPhase[phaseCount];
        for (int index = 0; index < phases.Length; index++)
        {
            phases[index] = insertEventBetweenBattles && index % 2 == 1
                ? ResolveRoomPhase(index / 2)
                : EDungeonPhase.Battle;
        }

        return Array.AsReadOnly(phases);
    }

    private EDungeonPhase ResolveRoomPhase(int roomIndex)
    {
        if (roomPattern == null || roomPattern.Length == 0)
            return EDungeonPhase.Event;

        EDungeonPhase phase = roomPattern[
            Math.Abs(roomIndex) % roomPattern.Length];
        return phase == EDungeonPhase.Battle
            ? EDungeonPhase.Event
            : phase;
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(dungeonId))
        {
            error = "Dungeon id is required.";
            return false;
        }
        if (!Enum.IsDefined(typeof(EDungeonRunMode), runMode) ||
            contentVersion < 1 || minimumBattleCount < 1 ||
            maximumBattleCount < minimumBattleCount ||
            initialRunCurrency < 0 ||
            float.IsNaN(activeSkillCostRecoveryDuration) ||
            float.IsInfinity(activeSkillCostRecoveryDuration) ||
            activeSkillCostRecoveryDuration <= 0f ||
            clearedBattleHealthCost < AutomaticClearedBattleHealthCost ||
            float.IsNaN(battleArenaRadius) ||
            float.IsInfinity(battleArenaRadius) ||
            battleArenaRadius <= 0f || battleShieldMaximumHealth < 1)
        {
            error = "Dungeon version, battle counts, or battle values are invalid.";
            return false;
        }
        if (IsListedInStageSelect &&
            string.IsNullOrWhiteSpace(TitleLocalizationKey) &&
            string.IsNullOrWhiteSpace(FallbackTitle))
        {
            error = "A listed dungeon requires a stage-select title.";
            return false;
        }
        if (IsPractice &&
            (minimumBattleCount != 1 || maximumBattleCount != 1 ||
             insertEventBetweenBattles || selectStartingCharacter ||
             selectStartingItems || initialRunCurrency != 0 ||
             clearedBattleHealthCost != 0 || tutorial != null ||
             useIntroBattleBalance))
        {
            error = "Practice mode requires one endless battle without " +
                    "starting selections, run rewards, or tutorial flow.";
            return false;
        }

        const int validationSeed = 17041;
        int battleCount = ResolveBattleCount(validationSeed);
        IReadOnlyList<EDungeonPhase> phases = BuildPhaseSequence(
            battleCount,
            validationSeed);
        if (phases == null || phases.Count == 0)
        {
            error = "Dungeon flow must contain at least one phase.";
            return false;
        }

        int phaseBattleCount = 0;
        for (int index = 0; index < phases.Count; index++)
        {
            if (phases[index] == EDungeonPhase.Battle)
                phaseBattleCount++;
        }
        if (phaseBattleCount != battleCount)
        {
            error = $"Flow resolved {battleCount} battles but contains " +
                    $"{phaseBattleCount} battle phases.";
            return false;
        }

        if (SelectStartingItems && phases[0] != EDungeonPhase.Battle)
        {
            error = "A dungeon with starting-item selection must begin " +
                    "with a Battle phase.";
            return false;
        }

        if (bgmProfile != null && !bgmProfile.TryValidate(out error))
        {
            error = $"Dungeon BGM profile is invalid: {error}";
            return false;
        }

        if (!TryValidateRooms(fixedEvents, "Event", out error) ||
            !TryValidateRooms(fixedRests, "Rest", out error) ||
            !TryValidateRooms(fixedShops, "Shop", out error))
        {
            return false;
        }

        if (defaultEvent != null && !defaultEvent.TryValidate(out error))
        {
            error = $"Default event: {error}";
            return false;
        }
        if (defaultRest != null && !defaultRest.TryValidate(out error))
        {
            error = $"Default rest room: {error}";
            return false;
        }
        if (defaultShop != null && !defaultShop.TryValidate(out error))
        {
            error = $"Default shop: {error}";
            return false;
        }

        if (tutorial != null)
        {
            if (tutorial.Steps.Count == 0)
            {
                error = "Tutorial definition requires at least one step.";
                return false;
            }

            bool hasStartBattleStep = false;
            for (int index = 0; index < tutorial.Steps.Count; index++)
            {
                DungeonTutorialStepDefinition step = tutorial.Steps[index];
                if (step == null ||
                    string.IsNullOrWhiteSpace(step.MessageLocalizationKey))
                {
                    error = $"Tutorial step {index + 1} is incomplete.";
                    return false;
                }

                if (step.Action == EDungeonTutorialAction.StartBattle)
                    hasStartBattleStep = true;
            }

            if (!hasStartBattleStep)
            {
                error = "Tutorial requires a StartBattle step.";
                return false;
            }
        }
        else if (useIntroBattleBalance)
        {
            error =
                "Intro battle balance requires a Tutorial definition.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateRooms<T>(
        T[] rooms,
        string roomType,
        out string error)
        where T : DungeonRoomSO
    {
        if (rooms != null)
        {
            for (int index = 0; index < rooms.Length; index++)
            {
                if (rooms[index] != null &&
                    !rooms[index].TryValidate(out error))
                {
                    error = $"{roomType} room {index + 1}: {error}";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    internal static DungeonDefinition CreateRuntimeFallback(
        string id,
        bool tutorialStage)
    {
        DungeonDefinition definition = CreateInstance<DungeonDefinition>();
        bool practiceStage = string.Equals(
            id,
            DungeonDefinitionCatalog.PracticeBattleId,
            StringComparison.OrdinalIgnoreCase);
        definition.name = tutorialStage
            ? "RuntimeTutorialFieldDefinition"
            : practiceStage
                ? "RuntimePracticeBattleDefinition"
                : "RuntimeFreeBattleDefinition";
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.dungeonId = id;
        definition.runMode = practiceStage
            ? EDungeonRunMode.Practice
            : EDungeonRunMode.Standard;
        definition.stageOrder = tutorialStage ? 0 : practiceStage ? 1 : 2;
        definition.titleLocalizationKey = tutorialStage
            ? LocalizationKeys.UiStageSelectTutorialField
            : practiceStage
                ? LocalizationKeys.UiStageSelectPracticeBattle
                : LocalizationKeys.UiStageSelectFreeBattle;
        definition.fallbackTitle = tutorialStage
            ? "STAGE 0 · TUTORIAL FIELD"
            : practiceStage
                ? "PRACTICE BATTLE"
                : "FREE BATTLE";
        definition.minimumBattleCount = tutorialStage || practiceStage ? 1 : 5;
        definition.maximumBattleCount = tutorialStage || practiceStage ? 1 : 8;
        definition.insertEventBetweenBattles =
            !tutorialStage && !practiceStage;
        definition.selectStartingCharacter = !practiceStage;
        definition.selectStartingItems = !practiceStage;
        definition.useIntroBattleBalance = tutorialStage;
        definition.initialRunCurrency = practiceStage ? 0 : 100;
        definition.clearedBattleHealthCost = practiceStage
            ? 0
            : AutomaticClearedBattleHealthCost;
        definition.completionDestination = tutorialStage || practiceStage
            ? EDungeonCompletionDestination.StageSelect
            : EDungeonCompletionDestination.Main;
        if (tutorialStage)
            definition.tutorial =
                DungeonTutorialDefinition.CreateRuntimeDefault();
        return definition;
    }

    private void OnValidate()
    {
    }
}
