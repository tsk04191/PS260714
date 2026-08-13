using System;
using System.Collections.Generic;
using UnityEngine;

public enum EDungeonTutorialTarget
{
    StartingChoice,
    Field,
    Queue,
    Character,
    Item,
    Timer,
}

public enum EDungeonTutorialAction
{
    SelectStartingCharacter,
    Continue,
    StartBattle,
}

[Serializable]
public sealed class DungeonTutorialStepDefinition
{
    [SerializeField] private EDungeonTutorialTarget target;
    [SerializeField] private string messageLocalizationKey;
    [SerializeField] private EDungeonTutorialAction action =
        EDungeonTutorialAction.Continue;

    public EDungeonTutorialTarget Target => target;
    public string MessageLocalizationKey => messageLocalizationKey;
    public EDungeonTutorialAction Action => action;

    public DungeonTutorialStepDefinition() { }

    internal DungeonTutorialStepDefinition(
        EDungeonTutorialTarget target,
        string messageLocalizationKey,
        EDungeonTutorialAction action)
    {
        this.target = target;
        this.messageLocalizationKey = messageLocalizationKey;
        this.action = action;
    }
}

[CreateAssetMenu(
    fileName = "DungeonTutorial",
    menuName = "Dungeon/Tutorial Definition")]
public sealed class DungeonTutorialDefinition : ScriptableObject
{
    [SerializeField] private List<DungeonTutorialStepDefinition> steps = new();
    [SerializeField] private string nextButtonLocalizationKey =
        "ui.tutorial.next";
    [SerializeField] private string startBattleButtonLocalizationKey =
        "ui.tutorial.start_battle";
    [SerializeField] private string completionLocalizationKey =
        "ui.tutorial.complete";
    [SerializeField] private string returnButtonLocalizationKey =
        "ui.tutorial.return";

    public IReadOnlyList<DungeonTutorialStepDefinition> Steps => steps;
    public string NextButtonLocalizationKey => nextButtonLocalizationKey;
    public string StartBattleButtonLocalizationKey =>
        startBattleButtonLocalizationKey;
    public string CompletionLocalizationKey => completionLocalizationKey;
    public string ReturnButtonLocalizationKey => returnButtonLocalizationKey;

    internal static DungeonTutorialDefinition CreateRuntimeDefault()
    {
        DungeonTutorialDefinition definition =
            CreateInstance<DungeonTutorialDefinition>();
        definition.name = "RuntimeTestFieldTutorial";
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.steps = new List<DungeonTutorialStepDefinition>
        {
            new(
                EDungeonTutorialTarget.StartingChoice,
                "ui.tutorial.choice",
                EDungeonTutorialAction.SelectStartingCharacter),
            new(
                EDungeonTutorialTarget.Field,
                "ui.tutorial.field",
                EDungeonTutorialAction.Continue),
            new(
                EDungeonTutorialTarget.Queue,
                "ui.tutorial.queue",
                EDungeonTutorialAction.Continue),
            new(
                EDungeonTutorialTarget.Character,
                "ui.tutorial.character",
                EDungeonTutorialAction.Continue),
            new(
                EDungeonTutorialTarget.Item,
                "ui.tutorial.item",
                EDungeonTutorialAction.Continue),
            new(
                EDungeonTutorialTarget.Timer,
                "ui.tutorial.timer",
                EDungeonTutorialAction.StartBattle),
        };
        return definition;
    }

    private void OnValidate()
    {
        steps ??= new List<DungeonTutorialStepDefinition>();
    }
}
