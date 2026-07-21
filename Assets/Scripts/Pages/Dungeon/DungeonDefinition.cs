using System;
using System.Collections.Generic;
using UnityEngine;

public enum EDungeonCompletionDestination
{
    Main,
    StageSelect,
}

[CreateAssetMenu(
    fileName = "DungeonDefinition",
    menuName = "Dungeon/Definition")]
public sealed class DungeonDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string dungeonId = "free_battle";
    [SerializeField, Min(1)] private int contentVersion = 1;

    [Header("Flow")]
    [SerializeField, Min(1)] private int minimumBattleCount = 5;
    [SerializeField, Min(1)] private int maximumBattleCount = 8;
    [SerializeField] private bool insertEventBetweenBattles = true;
    [SerializeField] private DungeonFlowPolicy flowPolicy;

    [Header("Run Rules")]
    [SerializeField] private bool selectStartingCharacter = true;
    [SerializeField] private bool includeStartingConsumable = true;
    [SerializeField] private bool useIntroBattleBalance = true;
    [SerializeField] private EDungeonCompletionDestination completionDestination =
        EDungeonCompletionDestination.Main;

    [Header("Encounters")]
    [SerializeField] private BattleSO[] fixedBattles =
        Array.Empty<BattleSO>();
    [SerializeField] private EnemySO[] enemyPoolOverride =
        Array.Empty<EnemySO>();

    [Header("Presentation")]
    [SerializeField] private DungeonFieldView fieldViewPrefab;
    [SerializeField] private DungeonThemeDefinition theme;
    [SerializeField] private DungeonTutorialDefinition tutorial;

    [Header("Optional Rule Modules")]
    [SerializeField] private DungeonModifier[] modifiers =
        Array.Empty<DungeonModifier>();

    public string DungeonId => dungeonId;
    public int ContentVersion => contentVersion;
    public bool SelectStartingCharacter => selectStartingCharacter;
    public bool IncludeStartingConsumable => includeStartingConsumable;
    public bool UseIntroBattleBalance => useIntroBattleBalance;
    public EDungeonCompletionDestination CompletionDestination =>
        completionDestination;
    public DungeonFieldView FieldViewPrefab => fieldViewPrefab;
    public DungeonThemeDefinition Theme => theme;
    public DungeonTutorialDefinition Tutorial => tutorial;
    public bool HasTutorial => tutorial != null;
    public IReadOnlyList<DungeonModifier> Modifiers => modifiers;
    public IReadOnlyList<EnemySO> EnemyPoolOverride => enemyPoolOverride;

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
                ? EDungeonPhase.Event
                : EDungeonPhase.Battle;
        }

        return Array.AsReadOnly(phases);
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(dungeonId))
        {
            error = "Dungeon id is required.";
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

        error = string.Empty;
        return true;
    }

    internal static DungeonDefinition CreateRuntimeFallback(
        string id,
        bool tutorialStage)
    {
        DungeonDefinition definition = CreateInstance<DungeonDefinition>();
        definition.name = tutorialStage
            ? "RuntimeTestFieldDefinition"
            : "RuntimeFreeBattleDefinition";
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.dungeonId = id;
        definition.minimumBattleCount = tutorialStage ? 1 : 5;
        definition.maximumBattleCount = tutorialStage ? 1 : 8;
        definition.insertEventBetweenBattles = !tutorialStage;
        definition.completionDestination = tutorialStage
            ? EDungeonCompletionDestination.StageSelect
            : EDungeonCompletionDestination.Main;
        if (tutorialStage)
            definition.tutorial =
                DungeonTutorialDefinition.CreateRuntimeDefault();
        return definition;
    }

    private void OnValidate()
    {
        dungeonId = string.IsNullOrWhiteSpace(dungeonId)
            ? name.Trim().ToLowerInvariant().Replace(' ', '_')
            : dungeonId.Trim();
        contentVersion = Mathf.Max(1, contentVersion);
        minimumBattleCount = Mathf.Max(1, minimumBattleCount);
        maximumBattleCount = Mathf.Max(
            minimumBattleCount,
            maximumBattleCount);
        fixedBattles ??= Array.Empty<BattleSO>();
        enemyPoolOverride ??= Array.Empty<EnemySO>();
        modifiers ??= Array.Empty<DungeonModifier>();
    }
}
