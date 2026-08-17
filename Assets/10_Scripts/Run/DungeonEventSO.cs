using System;
using System.Collections.Generic;
using PS260714.Localization;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

public enum EDungeonRoomEffectType
{
    RunCurrency,
    HealPartyFlat,
    HealPartyPercent,
    MaximumEnergy,
    RechargeSpeed,
    BattleItem,
}

public enum EDungeonRoomConditionType
{
    MinimumRunCurrency,
    PartyHasInjuredMember,
    OwnsBattleItem,
    DoesNotOwnBattleItem,
}

[Serializable]
public sealed class DungeonRoomConditionDefinition
{
    [SerializeField] private EDungeonRoomConditionType conditionType;
    [SerializeField, Min(0)] private int amount;
    [SerializeField] private BattleItemSO battleItem;

    public EDungeonRoomConditionType ConditionType => conditionType;
    public int Amount => Mathf.Max(0, amount);
    public BattleItemSO BattleItem => battleItem;

    public bool TryValidate(out string error)
    {
        if (!Enum.IsDefined(typeof(EDungeonRoomConditionType), conditionType) ||
            amount < 0)
        {
            error = "Room condition type or amount is invalid.";
            return false;
        }
        if ((conditionType == EDungeonRoomConditionType.OwnsBattleItem ||
             conditionType == EDungeonRoomConditionType.DoesNotOwnBattleItem) &&
            battleItem == null)
        {
            error = $"{conditionType} requires a battle item.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class DungeonRoomEffectDefinition
{
    [SerializeField] private EDungeonRoomEffectType effectType;
    [SerializeField, Tooltip(
        "Signed currency delta, heal amount/percent, or upgrade count.")]
    private int amount = 1;
    [SerializeField] private BattleItemSO battleItem;

    public EDungeonRoomEffectType EffectType => effectType;
    public int Amount => amount;
    public BattleItemSO BattleItem => battleItem;

    public bool TryValidate(out string error)
    {
        if (!Enum.IsDefined(typeof(EDungeonRoomEffectType), effectType))
        {
            error = "Room effect type is invalid.";
            return false;
        }
        if (effectType == EDungeonRoomEffectType.BattleItem &&
            battleItem == null)
        {
            error = "A BattleItem effect requires an item.";
            return false;
        }

        if (effectType != EDungeonRoomEffectType.RunCurrency && amount <= 0)
        {
            error = $"{effectType} requires a positive amount.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public class DungeonRoomChoiceDefinition : IRunAbilityDefinition
{
    [BoxGroup("Choice")]
    [SerializeField] private string choiceId = "choice";
    [BoxGroup("Choice")]
    [SerializeField, HideInInspector] private string titleLocalizationKey;
    [BoxGroup("Choice")]
    [SerializeField] private string fallbackTitle = "CONTINUE";
    [BoxGroup("Choice")]
    [SerializeField, HideInInspector] private string descriptionLocalizationKey;
    [BoxGroup("Choice")]
    [SerializeField, TextArea(2, 5)] private string fallbackDescription;
    [BoxGroup("Cost")]
    [SerializeField, Min(0)] private int runCurrencyCost;
    [BoxGroup("Conditions")]
    [ListDrawerSettings(DefaultExpandedState = true)]
    [SerializeField] private DungeonRoomConditionDefinition[] conditions =
        Array.Empty<DungeonRoomConditionDefinition>();
    [BoxGroup("Shop")]
    [SerializeField, Tooltip(
        "When enabled, this product can only be purchased once per shop.")]
    private bool singlePurchase = true;
    [BoxGroup("Rewards")]
    [ListDrawerSettings(DefaultExpandedState = true)]
    [SerializeField] private DungeonRoomEffectDefinition[] effects =
        Array.Empty<DungeonRoomEffectDefinition>();

    public string ChoiceId => choiceId;
    public string AbilityId => ChoiceId ?? string.Empty;
    public AbilityExecutionDomain ExecutionDomain =>
        AbilityExecutionDomain.Run;
    public int AbilitySchemaVersion => 1;
    public int RunCurrencyCost => Mathf.Max(0, runCurrencyCost);
    public bool SinglePurchase => singlePurchase;
    public IReadOnlyList<DungeonRoomConditionDefinition> Conditions =>
        conditions ?? Array.Empty<DungeonRoomConditionDefinition>();
    public IReadOnlyList<DungeonRoomEffectDefinition> Effects =>
        effects ?? Array.Empty<DungeonRoomEffectDefinition>();
    public string Title => ResolveText(titleLocalizationKey, fallbackTitle);
    public string Description => ResolveText(
        descriptionLocalizationKey,
        fallbackDescription);

    internal DungeonRoomChoiceDefinition ConfigureDefaults(
        string id,
        string title,
        string description = "")
    {
        choiceId = string.IsNullOrWhiteSpace(id) ? "choice" : id.Trim();
        fallbackTitle = string.IsNullOrWhiteSpace(title)
            ? "CONTINUE"
            : title.Trim();
        fallbackDescription = (description ?? string.Empty).Trim();
        conditions ??= Array.Empty<DungeonRoomConditionDefinition>();
        effects ??= Array.Empty<DungeonRoomEffectDefinition>();
        return this;
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(choiceId))
        {
            error = "Choice id is required.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(Title))
        {
            error = $"Choice '{choiceId}' requires a title.";
            return false;
        }

        IReadOnlyList<DungeonRoomConditionDefinition> authoredConditions =
            conditions ?? Array.Empty<DungeonRoomConditionDefinition>();
        IReadOnlyList<DungeonRoomEffectDefinition> authoredEffects =
            effects ?? Array.Empty<DungeonRoomEffectDefinition>();
        for (int index = 0; index < authoredConditions.Count; index++)
        {
            if (authoredConditions[index] == null)
            {
                error = $"Choice '{choiceId}' condition {index + 1} is null.";
                return false;
            }
            if (!authoredConditions[index].TryValidate(out error))
            {
                error = $"Choice '{choiceId}' condition {index + 1}: {error}";
                return false;
            }
        }

        for (int index = 0; index < authoredEffects.Count; index++)
        {
            if (authoredEffects[index] == null)
            {
                error = $"Choice '{choiceId}' effect {index + 1} is null.";
                return false;
            }
            if (!authoredEffects[index].TryValidate(out error))
            {
                error = $"Choice '{choiceId}' effect {index + 1}: {error}";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static string ResolveText(string key, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(key) &&
            LocalizationService.TryGet(key, out string localized))
        {
            return localized;
        }

        return (fallback ?? string.Empty).Trim();
    }
}

[Serializable]
public sealed class DungeonEventChoiceNodeDefinition :
    DungeonRoomChoiceDefinition
{
    [BoxGroup("Node")]
    [ReadOnly]
    [SerializeField] private string nodeId;
    [BoxGroup("Result")]
    [SerializeField, HideInInspector]
    private string resultDescriptionLocalizationKey;
    [BoxGroup("Result")]
    [SerializeField, TextArea(2, 6)] private string fallbackResultDescription;
    [BoxGroup("Flow")]
    [SerializeField] private bool endsEvent = true;
    [BoxGroup("Flow")]
    [ReadOnly]
    [SerializeField, Tooltip(
        "Connect choices in Event Editor. IDs are read-only here to keep " +
        "graph links consistent.")]
    private string[] nextChoiceNodeIds =
        Array.Empty<string>();
    [SerializeField, HideInInspector] private Vector2 editorPosition;

    public string NodeId => !string.IsNullOrWhiteSpace(nodeId)
        ? nodeId.Trim()
        : ChoiceId;
    public string ResultDescription => ResolveNodeText(
        resultDescriptionLocalizationKey,
        fallbackResultDescription);
    public bool EndsEvent => endsEvent;
    public IReadOnlyList<string> NextChoiceNodeIds =>
        nextChoiceNodeIds ?? Array.Empty<string>();
    public Vector2 EditorPosition => editorPosition;

    internal bool TryValidateNode(out string error)
    {
        if (!TryValidate(out error))
            return false;
        if (string.IsNullOrWhiteSpace(NodeId))
        {
            error = "Node id is required.";
            return false;
        }

        IReadOnlyList<string> nextIdsAuthored =
            nextChoiceNodeIds ?? Array.Empty<string>();
        if (endsEvent && nextIdsAuthored.Count > 0)
        {
            error = $"Node '{NodeId}' cannot end the event and have next " +
                    "choices at the same time.";
            return false;
        }
        if (!endsEvent && nextIdsAuthored.Count == 0)
        {
            error = $"Node '{NodeId}' requires a next choice or must end " +
                    "the event.";
            return false;
        }

        HashSet<string> nextIds = new(StringComparer.Ordinal);
        for (int index = 0; index < nextIdsAuthored.Count; index++)
        {
            string nextId = (nextIdsAuthored[index] ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(nextId))
            {
                error = $"Node '{NodeId}' has an empty next choice id.";
                return false;
            }
            if (!nextIds.Add(nextId))
            {
                error = $"Node '{NodeId}' links to '{nextId}' more than " +
                        "once.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static string ResolveNodeText(string key, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(key) &&
            LocalizationService.TryGet(key, out string localized))
        {
            return localized;
        }

        return (fallback ?? string.Empty).Trim();
    }
}

public abstract class DungeonRoomSO : ScriptableObject
{
    [Header("Identity")]
    [FormerlySerializedAs("eventId")]
    [SerializeField] private string roomId = "dungeon_room";
    [FormerlySerializedAs("displayName")]
    [SerializeField] private string displayName = "DUNGEON ROOM";

    [Header("Room Copy")]
    [SerializeField, HideInInspector] private string titleLocalizationKey;
    [SerializeField, HideInInspector] private string descriptionLocalizationKey;
    [SerializeField, TextArea(3, 8)] private string fallbackDescription;

    [Header("Presentation Override")]
    [SerializeField] private Sprite banner;
    [SerializeField, Tooltip(
        "Optional music used while this room is active. When empty, the " +
        "dungeon's default Rest Clip is used.")]
    private AudioClip bgmOverride;

    public string RoomId => roomId;
    public string DisplayName => ResolveText(
        titleLocalizationKey,
        displayName);
    public string Description => ResolveText(
        descriptionLocalizationKey,
        fallbackDescription);
    public Sprite Banner => banner;
    public AudioClip BgmOverride => bgmOverride;

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            error = "Room id is required.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            error = $"Room '{roomId}' requires a display name.";
            return false;
        }

        return TryValidateRoom(out error);
    }

    protected abstract bool TryValidateRoom(out string error);

    private static string ResolveText(string key, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(key) &&
            LocalizationService.TryGet(key, out string localized))
        {
            return localized;
        }

        return (fallback ?? string.Empty).Trim();
    }
}

[CreateAssetMenu(
    fileName = "DungeonEvent",
    menuName = "Dungeon/Room/Event")]
public sealed class DungeonEventSO : DungeonRoomSO
{
    public const int CurrentChoiceGraphVersion = 1;

    [SerializeField, HideInInspector] private int choiceGraphVersion;
    [SerializeField, HideInInspector] private string[] entryChoiceNodeIds =
        Array.Empty<string>();
    [SerializeField, HideInInspector]
    private DungeonEventChoiceNodeDefinition[] choices =
        Array.Empty<DungeonEventChoiceNodeDefinition>();

    public string EventId => RoomId;
    public bool UsesChoiceGraph => choiceGraphVersion > 0;
    public IReadOnlyList<DungeonEventChoiceNodeDefinition> Choices => choices;
    public IReadOnlyList<string> EntryChoiceNodeIds =>
        entryChoiceNodeIds ?? Array.Empty<string>();

    public bool IsEntryChoice(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return false;
        if (entryChoiceNodeIds == null || entryChoiceNodeIds.Length == 0)
            return !UsesChoiceGraph && FindChoiceIndex(nodeId) >= 0;

        for (int index = 0; index < entryChoiceNodeIds.Length; index++)
        {
            if (string.Equals(
                    entryChoiceNodeIds[index],
                    nodeId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetChoiceNode(
        string nodeId,
        out DungeonEventChoiceNodeDefinition node)
    {
        int index = FindChoiceIndex(nodeId);
        node = index >= 0 ? choices[index] : null;
        return node != null;
    }

    public int FindChoiceIndex(string nodeId)
    {
        if (choices == null || string.IsNullOrWhiteSpace(nodeId))
            return -1;

        for (int index = 0; index < choices.Length; index++)
        {
            DungeonEventChoiceNodeDefinition node = choices[index];
            if (node != null && string.Equals(
                    node.NodeId,
                    nodeId,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    public void GetEntryChoices(
        List<DungeonEventChoiceNodeDefinition> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        results.Clear();
        if (choices == null)
            return;
        if (entryChoiceNodeIds == null || entryChoiceNodeIds.Length == 0)
        {
            if (UsesChoiceGraph)
                return;
            for (int index = 0; index < choices.Length; index++)
            {
                if (choices[index] != null)
                    results.Add(choices[index]);
            }
            return;
        }

        for (int index = 0; index < entryChoiceNodeIds.Length; index++)
        {
            if (TryGetChoiceNode(entryChoiceNodeIds[index], out var node))
                results.Add(node);
        }
    }

    protected override bool TryValidateRoom(out string error)
    {
        if (!ValidateChoices(choices, "Event", out error))
            return false;
        if (!UsesChoiceGraph)
            return true;

        return TryValidateGraph(out error);
    }

    private void OnValidate()
    {
    }

    internal static bool ValidateChoices(
        IReadOnlyList<DungeonRoomChoiceDefinition> definitions,
        string roomType,
        out string error)
    {
        if (definitions == null || definitions.Count == 0)
        {
            error = $"{roomType} requires at least one choice.";
            return false;
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int index = 0; index < definitions.Count; index++)
        {
            DungeonRoomChoiceDefinition choice = definitions[index];
            if (choice == null)
            {
                error = $"{roomType} choice {index + 1} is null.";
                return false;
            }
            if (!choice.TryValidate(out error))
            {
                error = $"{roomType} choice {index + 1}: {error}";
                return false;
            }
            if (!ids.Add(choice.ChoiceId))
            {
                error = $"{roomType} choice id '{choice.ChoiceId}' is duplicated.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private bool TryValidateGraph(out string error)
    {
        Dictionary<string, DungeonEventChoiceNodeDefinition> nodes =
            new(StringComparer.Ordinal);
        for (int index = 0; index < choices.Length; index++)
        {
            DungeonEventChoiceNodeDefinition node = choices[index];
            if (!node.TryValidateNode(out error))
            {
                error = $"Event node {index + 1}: {error}";
                return false;
            }
            if (!nodes.TryAdd(node.NodeId, node))
            {
                error = $"Event node id '{node.NodeId}' is duplicated.";
                return false;
            }
        }

        List<string> entries = new();
        if (entryChoiceNodeIds == null || entryChoiceNodeIds.Length == 0)
        {
            error = "Event graph requires at least one entry choice.";
            return false;
        }
        else
        {
            HashSet<string> uniqueEntries = new(StringComparer.Ordinal);
            for (int index = 0; index < entryChoiceNodeIds.Length; index++)
            {
                string entryId = (entryChoiceNodeIds[index] ?? string.Empty)
                    .Trim();
                if (!nodes.ContainsKey(entryId))
                {
                    error = $"Entry choice '{entryId}' does not exist.";
                    return false;
                }
                if (!uniqueEntries.Add(entryId))
                {
                    error = $"Entry choice '{entryId}' is duplicated.";
                    return false;
                }
                entries.Add(entryId);
            }
        }

        if (!HasUnconditionalChoice(entries, nodes))
        {
            error = "The entry choice set requires at least one choice " +
                    "without conditions.";
            return false;
        }

        foreach (DungeonEventChoiceNodeDefinition node in choices)
        {
            if (node.EndsEvent)
                continue;

            foreach (string nextId in node.NextChoiceNodeIds)
            {
                if (!nodes.ContainsKey(nextId))
                {
                    error = $"Node '{node.NodeId}' links to missing node " +
                            $"'{nextId}'.";
                    return false;
                }
            }
            if (!HasUnconditionalChoice(node.NextChoiceNodeIds, nodes))
            {
                error = $"Choices after node '{node.NodeId}' require at " +
                        "least one option without conditions.";
                return false;
            }
        }

        HashSet<string> reachable = new(StringComparer.Ordinal);
        HashSet<string> visiting = new(StringComparer.Ordinal);
        foreach (string entry in entries)
        {
            if (!VisitNode(entry, nodes, reachable, visiting, out error))
                return false;
        }
        if (reachable.Count != nodes.Count)
        {
            foreach (string nodeId in nodes.Keys)
            {
                if (!reachable.Contains(nodeId))
                {
                    error = $"Node '{nodeId}' cannot be reached from an " +
                            "entry choice.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool HasUnconditionalChoice(
        IEnumerable<string> nodeIds,
        IReadOnlyDictionary<string, DungeonEventChoiceNodeDefinition> nodes)
    {
        foreach (string nodeId in nodeIds)
        {
            if (nodes.TryGetValue(nodeId, out var node) &&
                node.Conditions.Count == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool VisitNode(
        string nodeId,
        IReadOnlyDictionary<string, DungeonEventChoiceNodeDefinition> nodes,
        ISet<string> visited,
        ISet<string> visiting,
        out string error)
    {
        if (visited.Contains(nodeId))
        {
            error = string.Empty;
            return true;
        }
        if (!visiting.Add(nodeId))
        {
            error = $"Choice graph contains a cycle at node '{nodeId}'.";
            return false;
        }

        DungeonEventChoiceNodeDefinition node = nodes[nodeId];
        if (!node.EndsEvent)
        {
            foreach (string nextId in node.NextChoiceNodeIds)
            {
                if (!VisitNode(nextId, nodes, visited, visiting, out error))
                    return false;
            }
        }

        visiting.Remove(nodeId);
        visited.Add(nodeId);
        error = string.Empty;
        return true;
    }
}

[CreateAssetMenu(
    fileName = "DungeonRest",
    menuName = "Dungeon/Room/Rest")]
public sealed partial class DungeonRestSO : DungeonRoomSO
{
    [SerializeField, HideInInspector]
    private DungeonRoomChoiceDefinition[] choices =
        Array.Empty<DungeonRoomChoiceDefinition>();

    public IReadOnlyList<DungeonRoomChoiceDefinition> Choices => choices;

    protected override bool TryValidateRoom(out string error)
    {
        return TryValidateRest(choices, out error);
    }

    private void OnValidate()
    {
    }
}

[CreateAssetMenu(
    fileName = "DungeonShop",
    menuName = "Dungeon/Room/Shop")]
public sealed partial class DungeonShopSO : DungeonRoomSO
{
    [SerializeField] private DungeonRoomChoiceDefinition[] products =
        Array.Empty<DungeonRoomChoiceDefinition>();

    public IReadOnlyList<DungeonRoomChoiceDefinition> Products => products;

    protected override bool TryValidateRoom(out string error)
    {
        return DungeonEventSO.ValidateChoices(products, "Shop", out error);
    }

    private void OnValidate()
    {
    }
}
