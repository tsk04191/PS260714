using System;
using System.Collections.Generic;
using PS260714.Localization;
using Sirenix.OdinInspector;
using UnityEngine;

public enum EDungeonRestActionType
{
    HealSelectedCharacter = 0,
    UpgradeSelectedCharacter = 1,
    UseRestItem = 2,
    LegacyImmediate = 100,
}

public enum EDungeonRestTargetEffectType
{
    HealFlat = 0,
    HealPercent = 1,
    DungeonUpgrade = 2,
    AddRoomAction = 3,
}

[Serializable]
public sealed class DungeonRestTargetEffectDefinition
{
    [SerializeField] private EDungeonRestTargetEffectType effectType;
    [SerializeField, Min(1)] private int amount = 1;
    [SerializeField] private bool allowRevive = true;

    public EDungeonRestTargetEffectType EffectType => effectType;
    public int Amount => Mathf.Max(1, amount);
    public bool AllowRevive => allowRevive;

    internal static DungeonRestTargetEffectDefinition Create(
        EDungeonRestTargetEffectType type,
        int value,
        bool revive = true)
    {
        return new DungeonRestTargetEffectDefinition
        {
            effectType = type,
            amount = Mathf.Max(1, value),
            allowRevive = revive,
        };
    }

    internal bool TryValidate(out string error)
    {
        if (!Enum.IsDefined(typeof(EDungeonRestTargetEffectType), effectType))
        {
            error = $"Unknown Rest effect '{effectType}'.";
            return false;
        }

        if (amount <= 0)
        {
            error = $"Rest effect '{effectType}' requires a positive amount.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class DungeonRestActionDefinition : IRunAbilityDefinition
{
    [BoxGroup("Action", Order = 0)]
    [LabelWidth(120)]
    [SerializeField] private EDungeonRestActionType actionType;
    [BoxGroup("Action")]
    [LabelWidth(120)]
    [SerializeField] private DungeonRoomChoiceDefinition choice = new();
    [BoxGroup("Target Effect", Order = 1)]
    [LabelWidth(120)]
    [SerializeField, Min(1), ShowIf(nameof(ShowsAmount))]
    private int amount = 30;
    [BoxGroup("Target Effect")]
    [LabelWidth(120)]
    [SerializeField, ShowIf(nameof(IsHeal))] private bool allowRevive = true;

    public EDungeonRestActionType ActionType => actionType;
    public DungeonRoomChoiceDefinition Choice => choice;
    public string AbilityId => Choice?.AbilityId ?? string.Empty;
    public AbilityExecutionDomain ExecutionDomain =>
        AbilityExecutionDomain.Run;
    public int AbilitySchemaVersion => 1;
    public int Amount => Mathf.Max(1, amount);
    public bool AllowRevive => allowRevive;
    public bool RequiresTarget =>
        actionType == EDungeonRestActionType.HealSelectedCharacter ||
        actionType == EDungeonRestActionType.UpgradeSelectedCharacter;

    private bool IsHeal =>
        actionType == EDungeonRestActionType.HealSelectedCharacter;
    private bool ShowsAmount => IsHeal;

    internal static DungeonRestActionDefinition CreateDefault(
        EDungeonRestActionType type)
    {
        return type switch
        {
            EDungeonRestActionType.HealSelectedCharacter => new()
            {
                actionType = type,
                amount = 30,
                allowRevive = true,
                choice = new DungeonRoomChoiceDefinition().ConfigureDefaults(
                    "heal_one",
                    "1인 회복",
                    "대원을 선택해 최대 체력의 30%를 회복합니다."),
            },
            EDungeonRestActionType.UpgradeSelectedCharacter => new()
            {
                actionType = type,
                amount = 1,
                choice = new DungeonRoomChoiceDefinition().ConfigureDefaults(
                    "upgrade_one",
                    "1인 강화",
                    "대원을 선택해 던전 강화를 1회 적용합니다."),
            },
            EDungeonRestActionType.UseRestItem => new()
            {
                actionType = type,
                amount = 1,
                choice = new DungeonRoomChoiceDefinition().ConfigureDefaults(
                    "use_item",
                    "아이템 사용",
                    "휴식 중 사용할 아이템과 대원을 선택합니다."),
            },
            _ => new()
            {
                actionType = EDungeonRestActionType.LegacyImmediate,
                choice = new DungeonRoomChoiceDefinition().ConfigureDefaults(
                    "continue",
                    "계속"),
            },
        };
    }

    internal static DungeonRestActionDefinition FromLegacy(
        DungeonRoomChoiceDefinition legacyChoice)
    {
        return new DungeonRestActionDefinition
        {
            actionType = EDungeonRestActionType.LegacyImmediate,
            choice = legacyChoice ??
                     new DungeonRoomChoiceDefinition().ConfigureDefaults(
                         "continue",
                         "계속"),
            amount = 1,
        };
    }

    internal bool TryValidate(out string error)
    {
        if (!Enum.IsDefined(typeof(EDungeonRestActionType), actionType))
        {
            error = $"Unknown Rest action '{actionType}'.";
            return false;
        }

        if (choice == null)
        {
            error = "Rest action choice is required.";
            return false;
        }
        if (!choice.TryValidate(out error))
            return false;

        if (ShowsAmount && amount <= 0)
        {
            error = $"Rest action '{choice.ChoiceId}' requires a positive amount.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class CharacterRestSkillDefinition : IRunAbilityDefinition
{
    [SerializeField] private bool enabled;
    [SerializeField] private string skillId = "rest_skill";
    [SerializeField, HideInInspector] private string titleLocalizationKey;
    [SerializeField] private string fallbackTitle = "휴식 능력";
    [SerializeField, HideInInspector]
    private string descriptionLocalizationKey;
    [SerializeField, TextArea(2, 5)] private string fallbackDescription;
    [SerializeField] private Sprite icon;
    [SerializeField, Min(1)] private int usesPerRoom = 1;
    [SerializeField, Min(0), Tooltip(
        "이 대원이 파티에 있을 때 휴식방의 기본 행동 횟수에 더합니다.")]
    private int additionalRoomActions;
    [SerializeField, ListDrawerSettings(DefaultExpandedState = true)]
    private DungeonRestTargetEffectDefinition[] effects =
        Array.Empty<DungeonRestTargetEffectDefinition>();

    public bool Enabled => enabled;
    public string SkillId => string.IsNullOrWhiteSpace(skillId)
        ? "rest_skill"
        : skillId.Trim();
    public string AbilityId => SkillId;
    public AbilityExecutionDomain ExecutionDomain =>
        AbilityExecutionDomain.Run;
    public int AbilitySchemaVersion => 1;
    public string Title => ResolveText(titleLocalizationKey, fallbackTitle);
    public string Description => ResolveText(
        descriptionLocalizationKey,
        fallbackDescription);
    public Sprite Icon => icon;
    public int UsesPerRoom => Mathf.Max(1, usesPerRoom);
    public int AdditionalRoomActions => Mathf.Max(0, additionalRoomActions);
    public IReadOnlyList<DungeonRestTargetEffectDefinition> Effects =>
        effects ?? Array.Empty<DungeonRestTargetEffectDefinition>();
    public bool IsUsable => Enabled && Effects.Count > 0 &&
                            !string.IsNullOrWhiteSpace(Title);

    internal void Validate()
    {
        skillId = string.IsNullOrWhiteSpace(skillId)
            ? "rest_skill"
            : skillId.Trim();
        titleLocalizationKey =
            (titleLocalizationKey ?? string.Empty).Trim();
        descriptionLocalizationKey =
            (descriptionLocalizationKey ?? string.Empty).Trim();
        fallbackTitle = (fallbackTitle ?? string.Empty).Trim();
        fallbackDescription = (fallbackDescription ?? string.Empty).Trim();
        usesPerRoom = Mathf.Max(1, usesPerRoom);
        additionalRoomActions = Mathf.Max(0, additionalRoomActions);
        effects ??= Array.Empty<DungeonRestTargetEffectDefinition>();
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

public interface IDungeonRestActionAllowanceProvider
{
    int GetAdditionalRestActionCount(
        DungeonRuntimeContext context,
        DungeonRestSO room,
        int roomIndex);
}

public interface IDungeonRestActionProvider
{
    IReadOnlyList<DungeonRestActionDefinition> GetAdditionalRestActions(
        DungeonRuntimeContext context,
        DungeonRestSO room,
        int roomIndex);
}

public sealed partial class DungeonRestSO : DungeonRoomSO
{
    private const int CurrentRestSchemaVersion = 1;

    [SerializeField, HideInInspector] private int restSchemaVersion;
    [BoxGroup("Rest Rules", Order = 100)]
    [PropertyOrder(100)]
    [PropertySpace(6f, 6f)]
    [SerializeField, Min(1)] private int baseActionCount = 1;
    [BoxGroup("Rest Actions", Order = 110)]
    [PropertyOrder(110)]
    [PropertySpace(8f, 12f)]
    [SerializeField, ListDrawerSettings(
        DefaultExpandedState = true,
        ShowPaging = false)]
    private DungeonRestActionDefinition[] actions =
        CreateDefaultActions();

    public int BaseActionCount => Mathf.Max(1, baseActionCount);
    public IReadOnlyList<DungeonRestActionDefinition> Actions =>
        actions ?? Array.Empty<DungeonRestActionDefinition>();

    private static DungeonRestActionDefinition[] CreateDefaultActions()
    {
        return new[]
        {
            DungeonRestActionDefinition.CreateDefault(
                EDungeonRestActionType.HealSelectedCharacter),
            DungeonRestActionDefinition.CreateDefault(
                EDungeonRestActionType.UpgradeSelectedCharacter),
            DungeonRestActionDefinition.CreateDefault(
                EDungeonRestActionType.UseRestItem),
        };
    }

    private void EnsureRestSchema(
        IReadOnlyList<DungeonRoomChoiceDefinition> legacyChoices)
    {
        if (restSchemaVersion <= 0)
        {
            if (legacyChoices != null && legacyChoices.Count > 0)
            {
                actions = new DungeonRestActionDefinition[
                    legacyChoices.Count];
                for (int index = 0; index < legacyChoices.Count; index++)
                {
                    actions[index] =
                        DungeonRestActionDefinition.FromLegacy(
                            legacyChoices[index]);
                }
            }
            else if (actions == null || actions.Length == 0)
            {
                actions = CreateDefaultActions();
            }

            restSchemaVersion = CurrentRestSchemaVersion;
        }

        baseActionCount = Mathf.Max(1, baseActionCount);
        actions ??= CreateDefaultActions();
    }

    private bool TryValidateRest(
        IReadOnlyList<DungeonRoomChoiceDefinition> legacyChoices,
        out string error)
    {
        EnsureRestSchema(legacyChoices);
        if (actions == null || actions.Length == 0)
        {
            error = "Rest requires at least one action.";
            return false;
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int index = 0; index < actions.Length; index++)
        {
            DungeonRestActionDefinition action = actions[index];
            if (action == null)
            {
                error = $"Rest action {index + 1} is null.";
                return false;
            }
            if (!action.TryValidate(out error))
            {
                error = $"Rest action {index + 1}: {error}";
                return false;
            }

            if (!ids.Add(action.Choice.ChoiceId))
            {
                error = $"Rest action id '{action.Choice.ChoiceId}' is duplicated.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
