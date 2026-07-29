using System;
using System.Collections.Generic;
using System.Text;
using PS260714.Localization;
using UnityEngine;

public static class CharacterLocalization
{
    public static string GetName(CharacterData data)
    {
        if (data == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(data.NameLocalizationKey))
            return LocalizationService.Get(data.NameLocalizationKey);

        return !string.IsNullOrWhiteSpace(data.CharacterName)
            ? data.CharacterName
            : LocalizationService.Get(LocalizationKeys.CharacterUnnamedName);
    }

    public static string GetDescription(CharacterData data)
    {
        if (data == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(
            data.DescriptionLocalizationKey)
            ? LocalizationService.Get(data.DescriptionLocalizationKey)
            : data.CharacterDescription ?? string.Empty;
    }

    public static string GetDungeonUpgradeTitle(
        CharacterDungeonUpgradeType upgradeType)
    {
        string key = upgradeType switch
        {
            CharacterDungeonUpgradeType.AttackPower =>
                LocalizationKeys.UiDungeonRewardUpgradeAttackPowerTitle,
            CharacterDungeonUpgradeType.Speed =>
                LocalizationKeys.UiDungeonRewardUpgradeAttackSpeedTitle,
            CharacterDungeonUpgradeType.AttackDamage =>
                LocalizationKeys.UiDungeonRewardUpgradeAttackPowerTitle,
            CharacterDungeonUpgradeType.SkillDamage =>
                LocalizationKeys.UiDungeonRewardUpgradeSkillPowerTitle,
            CharacterDungeonUpgradeType.SkillCostReduction =>
                LocalizationKeys.UiDungeonRewardUpgradeSkillCostTitle,
            _ => LocalizationKeys.UiDungeonRewardUpgradeGenericTitle,
        };
        return LocalizationService.Get(key);
    }

    public static string GetDungeonUpgradeDescription(
        CharacterData data,
        CharacterDungeonUpgradeType upgradeType)
    {
        return data?.GetDungeonUpgradeLabel(upgradeType) ?? string.Empty;
    }

    public static string GetIdentity(string assetName, CharacterData data)
    {
        if (data == null)
            return string.Empty;

        return LocalizationService.Get(
            LocalizationKeys.CodexCharacterIdentity,
            LocalizationService.Arg("asset", assetName));
    }

    public static string GetStats(CharacterData data)
    {
        if (data == null)
            return string.Empty;

        return UsesKoreanLocale
            ? $"공격력 {data.AttackPower:0.##}  |  " +
              $"공격 간격 {data.AttackCooldown:0.##}초  |  " +
              $"패시브 {data.ConfiguredPassiveDefinitionCount} / " +
              $"공격 {data.AttackDefinitions.Count} / " +
              $"기술 {data.SkillDefinitions.Count}"
            : $"ATK {data.AttackPower:0.##}  |  " +
              $"INTERVAL {data.AttackCooldown:0.##}s  |  " +
              $"PASSIVE {data.ConfiguredPassiveDefinitionCount} / " +
              $"ATTACK {data.AttackDefinitions.Count} / " +
              $"SKILL {data.SkillDefinitions.Count}";
    }

    public static string GetOwnership(CharacterData data)
    {
        if (data == null)
            return string.Empty;

        return data.IsOwned
            ? (UsesKoreanLocale ? "보유" : "OWNED")
            : (UsesKoreanLocale ? "미보유" : "NOT OWNED");
    }

    public static string GetPassiveDescription(CharacterData data)
    {
        if (data == null || !data.HasCustomPassiveDefinitions)
            return string.Empty;

        return GetCustomPassiveDescription(data);
    }

    public static string GetCumulativeUpgradeDescription(CharacterData data)
    {
        if (data == null)
        {
            return UsesKoreanLocale
                ? "적용된 누적 업그레이드 없음"
                : "No cumulative upgrades applied";
        }

        StringBuilder builder = new();
        HashSet<string> configuredIds = new(StringComparer.Ordinal);
        foreach (CharacterCumulativeUpgradeDefinition definition in
                 data.CumulativeUpgradeDefinitions)
        {
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.UpgradeId) ||
                !configuredIds.Add(definition.UpgradeId))
            {
                continue;
            }

            int level = data.GetCumulativeUpgradeLevel(
                definition.UpgradeId);
            if (level <= 0)
                continue;

            string maximum = definition.HasUnlimitedMaxLevel
                ? string.Empty
                : $"/{definition.MaxLevel}";
            AppendCodexLine(
                builder,
                $"{definition.UpgradeId}  Lv.{level}{maximum}");
        }

        foreach (CharacterCumulativeUpgradeProgress progress in
                 data.CumulativeUpgrades)
        {
            if (progress == null ||
                progress.Level <= 0 ||
                string.IsNullOrWhiteSpace(progress.UpgradeId) ||
                configuredIds.Contains(progress.UpgradeId))
            {
                continue;
            }

            AppendCodexLine(
                builder,
                $"{progress.UpgradeId}  Lv.{progress.Level}");
        }

        return builder.Length > 0
            ? builder.ToString()
            : (UsesKoreanLocale
                ? "적용된 누적 업그레이드 없음"
                : "No cumulative upgrades applied");
    }

    public static string GetDungeonUpgradeDescription(CharacterData data)
    {
        if (data == null || !data.HasCustomDungeonUpgrades)
        {
            return UsesKoreanLocale
                ? "설정된 던전 업그레이드 없음"
                : "No dungeon upgrades configured";
        }

        StringBuilder builder = new();
        int groupIndex = 1;
        foreach (CharacterDungeonUpgradeDefinition definition in
                 data.DungeonUpgradeDefinitions)
        {
            if (definition == null)
                continue;

            AppendCodexLine(
                builder,
                UsesKoreanLocale
                    ? $"목록 {groupIndex++} (합계 {definition.TotalProbability:0.##}%)"
                    : $"POOL {groupIndex++} (TOTAL {definition.TotalProbability:0.##}%)");
            foreach (CharacterDungeonUpgradeEntry entry in definition.Entries)
            {
                if (entry == null)
                    continue;

                string limit = entry.HasUnlimitedLimit
                    ? (UsesKoreanLocale ? "무제한" : "Unlimited")
                    : (UsesKoreanLocale
                        ? $"최대 {entry.Limit}회"
                        : $"Limit {entry.Limit}");
                AppendCodexLine(
                    builder,
                    $"  {GetDungeonUpgradeName(entry.Type)}  " +
                    $"{entry.Probability:0.##}% / {limit}");
            }
        }

        return builder.ToString();
    }

    public static string GetNormalAttackDescription(CharacterData data)
    {
        if (data == null)
            return string.Empty;

        return GetCustomAttackDescription(data);
    }

    public static string GetActiveSkillDescription(
        CharacterData data,
        float? fireDurationOverride = null)
    {
        if (data == null)
            return string.Empty;

        return GetCustomSkillDescription(data);
    }

    public static string GetActiveSkillTitle(int cost)
    {
        return LocalizationService.Get(
            LocalizationKeys.CodexCharacterActiveSkillCost,
            LocalizationService.Arg("cost", cost));
    }

    public static string GetNormalAttackTitle(CharacterData data)
    {
        return LocalizationService.Get(
            LocalizationKeys.CodexCharacterNormalAttack);
    }

    public static string GetTurretStatus(
        bool active,
        bool hasEnoughEnergy)
    {
        return LocalizationService.Get(active
            ? LocalizationKeys.UiTurretStatusActive
            : hasEnoughEnergy
                ? LocalizationKeys.UiTurretStatusReady
                : LocalizationKeys.UiTurretStatusNotEnoughEnergy);
    }

    public static string GetTurretSkillHeader(int cost, string status)
    {
        return LocalizationService.Get(
            LocalizationKeys.UiTurretSkillHeader,
            LocalizationService.Arg("cost", cost),
            LocalizationService.Arg("status", status));
    }

    public static string GetTurretClickActivate()
    {
        return LocalizationService.Get(
            LocalizationKeys.UiTurretClickActivate);
    }

    public static string GetTurretName(CharacterData data)
    {
        if (data == null)
            return string.Empty;

        return LocalizationService.Get(
            LocalizationKeys.UiTurretName,
            LocalizationService.Arg("name", GetName(data)),
            LocalizationService.Arg("cost", data.ActiveSkillCost));
    }

    public static string GetTurretAttack(CharacterData data)
    {
        return GetCompactSummary(data);
    }

    public static string GetCompactSummary(CharacterData data)
    {
        if (data == null)
            return string.Empty;

        return LocalizationService.Get(
            LocalizationKeys.UiCharacterCompactSummary,
            LocalizationService.Arg("attack", data.AttackPower),
            LocalizationService.Arg("cost", data.ActiveSkillCost),
            LocalizationService.Arg("count", data.SkillDefinitions.Count));
    }

    public static string GetCooldownStop(float seconds)
    {
        return GetSecondsText(LocalizationKeys.UiTurretCooldownStop, seconds);
    }

    public static string GetCooldownRecovery(float seconds)
    {
        return GetSecondsText(
            LocalizationKeys.UiTurretCooldownRecovery,
            seconds);
    }

    public static string GetCooldownWait(float seconds)
    {
        return GetSecondsText(LocalizationKeys.UiTurretCooldownWait, seconds);
    }

    public static string GetReadyStatus()
    {
        return LocalizationService.Get(LocalizationKeys.UiTurretStatusReady);
    }

    private static string GetSecondsText(string key, float seconds)
    {
        return LocalizationService.Get(
            key,
            LocalizationService.Arg("seconds", seconds));
    }

    private static string GetCustomPassiveDescription(
        CharacterData data)
    {
        StringBuilder builder = new();
        int index = 1;
        foreach (CharacterPassiveDefinition definition in
                 data.PassiveDefinitions)
        {
            if (definition == null || definition.IsEmptyPlaceholder)
                continue;

            if (definition.HasStatusContributionSection &&
                !definition.HasSection(
                    CharacterPassiveSectionType.Ability))
            {
                AppendCodexLine(
                    builder,
                    $"{(UsesKoreanLocale ? "패시브" : "PASSIVE")} " +
                    $"{index++}: " +
                    FormatStatusContributionMultipliers(
                        definition.StatusContributionMultipliers));
                continue;
            }

            CharacterAttackSubject subject = definition.HasSection(
                CharacterPassiveSectionType.Subject)
                ? definition.Subject
                : CharacterAttackSubject.Random;
            string subjectDescription;
            if (subject == CharacterAttackSubject.None &&
                definition.Trigger ==
                CharacterPassiveTrigger.OnStatusAcquired)
            {
                subjectDescription = UsesKoreanLocale
                    ? "상태가 적용된 대상"
                    : "Target of the status event";
            }
            else if (subject == CharacterAttackSubject.None &&
                     definition.Trigger ==
                     CharacterPassiveTrigger.OnAttackTargetSelected)
            {
                subjectDescription = UsesKoreanLocale
                    ? "이번에 선택된 공격 대상"
                    : "The selected attack target";
            }
            else
            {
                subjectDescription = FormatSubject(
                    definition.TargetFaction,
                    subject,
                    definition.SubjectMetric,
                    definition.SubjectCount);
            }
            string abilityDescription = definition.HasExplicitEffects
                ? FormatEffects(
                    definition.Effects,
                    effect => data.CalculatePassiveDamage(effect))
                : FormatAbility(
                    definition.DamageType,
                    definition.DamageAmountMode,
                    definition.DamageAmount,
                    definition.AppliedStatusEffect,
                    definition.StatusDuration,
                    definition.StatusStacks,
                    definition.StatusRemovalEffect,
                    definition.StatusRemovalTarget,
                    definition.StatusRemovalAmountMode,
                    definition.StatusRemovalCount,
                    definition.StatusRemovalRatio,
                    data.CalculatePassiveDamage(definition),
                    statusRemovalPickMode:
                        definition.StatusRemovalPickMode,
                    statusRemovalPickCount:
                        definition.StatusRemovalPickCount);
            AppendCodexLine(
                builder,
                $"{(UsesKoreanLocale ? "패시브" : "PASSIVE")} {index++}: " +
                FormatPassiveTrigger(definition) +
                (definition.Trigger == CharacterPassiveTrigger.OnAttack
                    ? FormatLinkage(
                        definition.HasSection(
                            CharacterPassiveSectionType.Linkage),
                        definition.Linkage)
                    : string.Empty) +
                FormatPassiveAttackTargetRelation(definition) +
                FormatNumericConditions(
                    definition.HasSection(
                        CharacterPassiveSectionType.Condition),
                    definition.ConditionMatchMode,
                    definition.NumericConditions) +
                FormatPassiveStatusCost(definition) +
                subjectDescription +
                FormatArea(definition.AreaOffsets) + " → " +
                abilityDescription);
        }

        return builder.Length > 0
            ? builder.ToString()
            : (UsesKoreanLocale
                ? "설정된 패시브 없음"
                : "No configured passives");
    }

    private static string GetCustomAttackDescription(CharacterData data)
    {
        StringBuilder builder = new();
        int index = 1;
        foreach (CharacterAttackDefinition definition in
                 data.AttackDefinitions)
        {
            if (definition == null)
                continue;

            string abilityDescription = definition.HasExplicitEffects
                ? FormatEffects(
                    definition.Effects,
                    effect => data.CalculateAttackDamage(effect))
                : FormatAbility(
                    definition.DamageType,
                    definition.DamageAmountMode,
                    definition.DamageAmount,
                    definition.AppliedStatusEffect,
                    definition.StatusDuration,
                    definition.StatusStacks,
                    definition.StatusRemovalEffect,
                    definition.StatusRemovalTarget,
                    definition.StatusRemovalAmountMode,
                    definition.StatusRemovalCount,
                    definition.StatusRemovalRatio,
                    data.CalculateAttackDamage(definition),
                    statusRemovalPickMode:
                        definition.StatusRemovalPickMode,
                    statusRemovalPickCount:
                        definition.StatusRemovalPickCount);
            AppendCodexLine(
                builder,
                $"{(UsesKoreanLocale ? "공격" : "ATTACK")} {index++}: " +
                FormatLinkage(
                    definition.HasSection(
                        CharacterAttackSectionType.Linkage),
                    definition.Linkage) +
                FormatNumericConditions(
                    definition.HasSection(
                        CharacterAttackSectionType.Condition),
                    definition.ConditionMatchMode,
                    definition.NumericConditions) +
                FormatSubject(
                    definition.TargetFaction,
                    definition.Subject,
                    definition.SubjectMetric,
                    definition.SubjectCount) +
                FormatTargetRetention(definition.TargetRetentionMode) +
                FormatArea(definition.AreaOffsets) + " → " +
                abilityDescription);
        }

        return builder.Length > 0
            ? builder.ToString()
            : (UsesKoreanLocale ? "설정된 공격 없음" : "No configured attacks");
    }

    private static string GetCustomSkillDescription(CharacterData data)
    {
        StringBuilder builder = new();
        int index = 1;
        foreach (CharacterSkillDefinition definition in data.SkillDefinitions)
        {
            if (definition == null)
                continue;

            CharacterAttackSubject subject = definition.HasSection(
                CharacterSkillSectionType.Subject)
                ? definition.Subject
                : CharacterAttackSubject.Random;
            bool hasNoRequiredActionTarget =
                subject == CharacterAttackSubject.None &&
                CanExecuteEffectsWithoutActionTargets(definition.Effects);
            string subjectDescription = hasNoRequiredActionTarget
                ? (UsesKoreanLocale
                    ? "행동 대상 불필요"
                    : "No action target required")
                : FormatSubject(
                    definition.TargetFaction,
                    subject,
                    definition.SubjectMetric,
                    definition.SubjectCount);
            string cost = definition.HasSection(CharacterSkillSectionType.Cost)
                ? (UsesKoreanLocale
                    ? $"코스트 {data.GetSkillCost(definition)}, "
                    : $"Cost {data.GetSkillCost(definition)}, ")
                : string.Empty;
            string abilityDescription = definition.HasExplicitEffects
                ? FormatEffects(
                    definition.Effects,
                    effect => data.CalculateSkillDamage(effect))
                : FormatAbility(
                    definition.DamageType,
                    definition.DamageAmountMode,
                    definition.DamageAmount,
                    definition.AppliedStatusEffect,
                    definition.StatusDuration,
                    definition.StatusStacks,
                    definition.StatusRemovalEffect,
                    definition.StatusRemovalTarget,
                    definition.StatusRemovalAmountMode,
                    definition.StatusRemovalCount,
                    definition.StatusRemovalRatio,
                    data.CalculateSkillDamage(definition),
                    statusRemovalPickMode:
                        definition.StatusRemovalPickMode,
                    statusRemovalPickCount:
                        definition.StatusRemovalPickCount);
            AppendCodexLine(
                builder,
                $"{(UsesKoreanLocale ? "기술" : "SKILL")} {index++}: " +
                cost +
                FormatLinkage(
                    definition.HasSection(
                        CharacterSkillSectionType.Linkage),
                    definition.Linkage) +
                FormatNumericConditions(
                    definition.HasSection(
                        CharacterSkillSectionType.Condition),
                    definition.ConditionMatchMode,
                    definition.NumericConditions) +
                subjectDescription +
                FormatArea(definition.AreaOffsets) + " → " +
                abilityDescription);
        }

        return builder.Length > 0
            ? builder.ToString()
            : (UsesKoreanLocale ? "설정된 기술 없음" : "No configured skills");
    }

    private static string FormatPassiveTrigger(
        CharacterPassiveDefinition definition)
    {
        if (definition == null)
            return string.Empty;

        switch (definition.Trigger)
        {
            case CharacterPassiveTrigger.OnCooldown:
                return UsesKoreanLocale
                    ? $"매 {definition.Cooldown:0.##}초마다, "
                    : $"Every {definition.Cooldown:0.##}s, ";
            case CharacterPassiveTrigger.OnKill:
            {
                string killerName = definition.KillSource switch
                {
                    CharacterPassiveKillSource.Self =>
                        UsesKoreanLocale ? "자신" : "self",
                    CharacterPassiveKillSource.Other =>
                        UsesKoreanLocale ? "자신 외 아군" : "another ally",
                    CharacterPassiveKillSource.SpecificCharacter =>
                        GetCharacterDefinitionName(
                            definition.SpecifiedKillerCharacter),
                    _ => UsesKoreanLocale ? "아군" : "any ally"
                };
                return UsesKoreanLocale
                    ? $"{killerName}이(가) 적 처치 시, "
                    : $"Whenever {killerName} defeats an enemy, ";
            }
            case CharacterPassiveTrigger.OnStatusAcquired:
            {
                string targetName = definition.StatusTarget switch
                {
                    CharacterPassiveStatusTarget.Ally =>
                        UsesKoreanLocale ? "아군" : "an ally",
                    CharacterPassiveStatusTarget.Enemy =>
                        UsesKoreanLocale ? "적" : "an enemy",
                    _ => UsesKoreanLocale ? "누군가" : "any combatant"
                };
                CharacterStatusSelection triggerStatuses =
                    definition.TriggerStatusSelection;
                string statusName = definition.TriggerStatusScope switch
                {
                    CharacterStatusSelectionScope.AllBuffs =>
                        UsesKoreanLocale ? "버프" : "a buff",
                    CharacterStatusSelectionScope.AllDebuffs =>
                        UsesKoreanLocale ? "디버프" : "a debuff",
                    _ => triggerStatuses.Count > 0
                        ? FormatStatusSelectionNames(triggerStatuses)
                        : (UsesKoreanLocale ? "상태" : "a status")
                };
                if (definition.TriggerStatusScope ==
                        CharacterStatusSelectionScope.SelectedStatuses &&
                    triggerStatuses.Count > 1)
                {
                    statusName = UsesKoreanLocale
                        ? $"{statusName} 중 하나"
                        : $"any of {statusName}";
                }
                return UsesKoreanLocale
                    ? $"{targetName}에게 {statusName} 적용 시, "
                    : $"When {targetName} gains {statusName}, ";
            }
            case CharacterPassiveTrigger.OnAttackTargetSelected:
                return UsesKoreanLocale
                    ? "공격 대상 선택 시, "
                    : "When an attack target is selected, ";
            default:
                return UsesKoreanLocale ? "공격 시, " : "On attack, ";
        }
    }

    private static string FormatPassiveAttackTargetRelation(
        CharacterPassiveDefinition definition)
    {
        if (definition == null ||
            (definition.Trigger != CharacterPassiveTrigger.OnAttack &&
             definition.Trigger !=
             CharacterPassiveTrigger.OnAttackTargetSelected) ||
            !definition.HasAttackTargetRelationCondition)
        {
            return string.Empty;
        }

        return definition.AttackTargetRelation switch
        {
            CharacterPassiveAttackTargetRelation.SameAsPreviousAttack =>
                UsesKoreanLocale
                    ? "[직전 공격과 동일 대상] "
                    : "[same target as previous attack] ",
            CharacterPassiveAttackTargetRelation
                    .DifferentFromPreviousAttack =>
                UsesKoreanLocale
                    ? "[직전 공격과 다른 대상] "
                    : "[different target from previous attack] ",
            _ => string.Empty
        };
    }

    private static string FormatPassiveStatusCost(
        CharacterPassiveDefinition definition)
    {
        if (definition == null || !definition.HasSelfStatusCost)
            return string.Empty;

        CharacterStatusStackCostDefinition cost =
            definition.SelfStatusCost;
        string statusName = GetStatusEffectName(cost.StatusEffect);
        return UsesKoreanLocale
            ? $"비용: 자신의 {statusName} {cost.RequiredStacks}스택 필요, " +
              $"성공 시 {cost.ConsumedStacks}스택 소비, "
            : $"Cost: requires {cost.RequiredStacks} self {statusName} " +
              $"stack(s), consumes {cost.ConsumedStacks} on success, ";
    }

    private static string FormatLinkage(
        bool hasLinkage,
        CharacterActionLinkage linkage)
    {
        if (!hasLinkage || linkage == CharacterActionLinkage.None)
            return string.Empty;

        return linkage switch
        {
            CharacterActionLinkage.PreviousAttackSucceeded =>
                UsesKoreanLocale
                    ? "연동: 앞선 공격 성공 시, "
                    : "After previous success, ",
            CharacterActionLinkage.SimultaneousWithPreviousAttack =>
                UsesKoreanLocale
                    ? "연동: 앞선 공격과 동시에, "
                    : "Alongside previous attack, ",
            _ => string.Empty,
        };
    }

    private static string FormatNumericConditions(
        bool hasCondition,
        CharacterConditionMatchMode matchMode,
        IReadOnlyList<CharacterNumericCondition> conditions)
    {
        if (!hasCondition || conditions == null || conditions.Count == 0)
            return string.Empty;

        StringBuilder builder = new();
        string separator = matchMode == CharacterConditionMatchMode.All
            ? (UsesKoreanLocale ? " 그리고 " : " AND ")
            : (UsesKoreanLocale ? " 또는 " : " OR ");
        foreach (CharacterNumericCondition condition in conditions)
        {
            if (condition == null)
                continue;
            if (builder.Length > 0)
                builder.Append(separator);

            string metric = condition.Metric switch
            {
                CharacterNumericConditionMetric.HealthPercentage =>
                    UsesKoreanLocale ? "체력 비율" : "health percentage",
                CharacterNumericConditionMetric.StackCount =>
                    UsesKoreanLocale ? "적 타일 스택" : "enemy tile stack",
                CharacterNumericConditionMetric.AttackPower =>
                    UsesKoreanLocale ? "공격력" : "attack power",
                CharacterNumericConditionMetric.AttackSpeed =>
                    UsesKoreanLocale ? "속도" : "speed",
                CharacterNumericConditionMetric.Shield =>
                    UsesKoreanLocale ? "보호막" : "shield",
                CharacterNumericConditionMetric.StatusStackCount =>
                    FormatStatusConditionMetric(condition),
                _ => UsesKoreanLocale ? "체력" : "health"
            };
            if (condition.Target == CharacterConditionTarget.Source)
            {
                metric = UsesKoreanLocale
                    ? $"자신의 {metric}"
                    : $"source {metric}";
            }

            string comparison = condition.Comparison switch
            {
                CharacterNumericComparison.GreaterThanOrEqual =>
                    UsesKoreanLocale ? "이상" : "or more",
                CharacterNumericComparison.LessThanOrEqual =>
                    UsesKoreanLocale ? "이하" : "or less",
                CharacterNumericComparison.GreaterThan =>
                    UsesKoreanLocale ? "초과" : "greater than",
                CharacterNumericComparison.LessThan =>
                    UsesKoreanLocale ? "미만" : "less than",
                CharacterNumericComparison.Equal =>
                    UsesKoreanLocale ? "같음" : "equal to",
                CharacterNumericComparison.NotEqual =>
                    UsesKoreanLocale ? "다름" : "not equal to",
                _ => string.Empty
            };
            string value = condition.Metric ==
                           CharacterNumericConditionMetric.HealthPercentage
                ? $"{condition.Threshold:0.##}%"
                : $"{condition.Threshold:0.##}";
            builder.Append(UsesKoreanLocale
                ? $"{metric} {value} {comparison}"
                : $"{metric} {comparison} {value}");
        }

        if (builder.Length == 0)
            return string.Empty;
        return UsesKoreanLocale
            ? $"조건: {builder}, "
            : $"Condition: {builder}, ";
    }

    private static string FormatSubject(
        CharacterTargetFaction faction,
        CharacterAttackSubject subject,
        CharacterAttackSubjectMetric metric,
        int count)
    {
        count = Mathf.Max(1, count);
        if (subject == CharacterAttackSubject.None)
        {
            return UsesKoreanLocale
                ? "앞선 공격과 동일한 대상"
                : "Same target(s) as the previous attack";
        }
        if (subject == CharacterAttackSubject.Manual)
        {
            string manualFaction =
                faction == CharacterTargetFaction.Ally
                    ? (UsesKoreanLocale ? "아군" : "ally")
                    : (UsesKoreanLocale ? "적" : "enemy");
            return UsesKoreanLocale
                ? $"플레이어가 선택한 {manualFaction} {count}명"
                : $"{count} manually selected {manualFaction} target(s)";
        }

        if (faction == CharacterTargetFaction.Ally)
        {
            if (subject == CharacterAttackSubject.Self)
                return UsesKoreanLocale ? "자신" : "Self";
            if (subject == CharacterAttackSubject.RandomExceptSelf)
            {
                return UsesKoreanLocale
                    ? $"자신을 제외한 무작위 아군 {count}명"
                    : $"{count} random ally target(s) except self";
            }
            if (subject == CharacterAttackSubject.AllExceptSelf)
            {
                return UsesKoreanLocale
                    ? "자신을 제외한 아군 전체"
                    : "All allies except self";
            }
            if (subject == CharacterAttackSubject.All)
            {
                return UsesKoreanLocale
                    ? "자신을 포함한 아군 전체"
                    : "All allies including self";
            }
        }

        string factionName = faction == CharacterTargetFaction.Ally
            ? (UsesKoreanLocale ? "아군" : "ally")
            : (UsesKoreanLocale ? "적" : "enemy");
        if (subject == CharacterAttackSubject.All)
        {
            return UsesKoreanLocale
                ? $"{factionName} 전체"
                : $"All {factionName} targets";
        }

        string metricName = metric switch
        {
            CharacterAttackSubjectMetric.StackCount =>
                UsesKoreanLocale ? "스택" : "stack",
            CharacterAttackSubjectMetric.AttackPower =>
                UsesKoreanLocale ? "공격력" : "attack power",
            CharacterAttackSubjectMetric.AttackSpeed =>
                UsesKoreanLocale ? "속도" : "speed",
            CharacterAttackSubjectMetric.Shield =>
                UsesKoreanLocale ? "보호막" : "shield",
            _ => UsesKoreanLocale ? "체력" : "health"
        };
        return subject switch
        {
            CharacterAttackSubject.HighestValue => UsesKoreanLocale
                ? $"{metricName}이 가장 높은 {factionName} {count}명"
                : $"{count} {factionName} target(s) with highest {metricName}",
            CharacterAttackSubject.LowestValue => UsesKoreanLocale
                ? $"{metricName}이 가장 낮은 {factionName} {count}명"
                : $"{count} {factionName} target(s) with lowest {metricName}",
            _ => UsesKoreanLocale
                ? $"무작위 {factionName} {count}명"
                : $"{count} random {factionName} target(s)",
        };
    }

    private static string FormatTargetRetention(
        CharacterAttackTargetRetentionMode retentionMode)
    {
        if (retentionMode !=
            CharacterAttackTargetRetentionMode.LockUntilInvalid)
        {
            return string.Empty;
        }

        return UsesKoreanLocale
            ? " (대상이 유효한 동안 고정)"
            : " (locked while target remains valid)";
    }

    private static string GetDungeonUpgradeName(
        CharacterDungeonUpgradeType upgradeType)
    {
        return upgradeType switch
        {
            CharacterDungeonUpgradeType.AttackPower =>
                UsesKoreanLocale ? "공격력 +0.5" : "Attack Power +0.5",
            CharacterDungeonUpgradeType.Speed =>
                UsesKoreanLocale ? "공격 간격 -0.1초" : "Interval -0.1s",
            CharacterDungeonUpgradeType.PassiveDamage =>
                UsesKoreanLocale ? "패시브 피해량 +0.5" : "Passive Damage +0.5",
            CharacterDungeonUpgradeType.AttackDamage =>
                UsesKoreanLocale ? "공격 피해량 +0.5" : "Attack Damage +0.5",
            CharacterDungeonUpgradeType.SkillDamage =>
                UsesKoreanLocale ? "기술 피해량 +1" : "Skill Damage +1",
            CharacterDungeonUpgradeType.SkillCostReduction =>
                UsesKoreanLocale ? "기술 코스트 -1" : "Skill Cost -1",
            _ => upgradeType.ToString(),
        };
    }

    private static string FormatAbility(
        CharacterAttackDamageType damageType,
        CharacterDamageAmountMode amountMode,
        float amount,
        StatusEffectSO appliedStatusEffect,
        float statusDuration,
        float statusStacks,
        StatusEffectSO statusRemovalEffect,
        CharacterStatusRemovalTarget statusRemovalTarget,
        CharacterStatusRemovalAmountMode statusRemovalAmountMode,
        int statusRemovalCount,
        float statusRemovalRatio,
        int finalDamage,
        bool includeFinalDamage = true,
        IReadOnlyList<StatusEffectSO> statusRemovalEffects = null,
        CharacterStatusRemovalPickMode statusRemovalPickMode =
            CharacterStatusRemovalPickMode.AllMatches,
        int statusRemovalPickCount = 1)
    {
        string typeName = damageType switch
        {
            CharacterAttackDamageType.Magical =>
                UsesKoreanLocale ? "마법" : "Magical",
            CharacterAttackDamageType.Fixed =>
                UsesKoreanLocale ? "고정" : "Fixed",
            CharacterAttackDamageType.StatusEffect =>
                UsesKoreanLocale ? "상태 부여" : "Status",
            _ => UsesKoreanLocale ? "물리" : "Physical",
        };
        if (damageType == CharacterAttackDamageType.StatusEffect)
        {
            string statusName = GetStatusEffectName(appliedStatusEffect);
            string durationText = appliedStatusEffect != null &&
                                  appliedStatusEffect.DurationMode ==
                                  StatusEffectDurationMode.Permanent
                ? (UsesKoreanLocale ? "영구" : "Permanent")
                : (UsesKoreanLocale
                    ? $"{statusDuration:0.##}초"
                    : $"{statusDuration:0.##}s");
            return UsesKoreanLocale
                ? $"{typeName}: {statusName} / " +
                  $"{durationText} / {statusStacks:0.##}스택"
                : $"{typeName}: {statusName} / " +
                  $"{durationText} / {statusStacks:0.##} stacks";
        }

        if (damageType == CharacterAttackDamageType.StatusRemoval)
        {
            bool usesRandomCount =
                statusRemovalTarget ==
                    CharacterStatusRemovalTarget.Random ||
                statusRemovalPickMode ==
                    CharacterStatusRemovalPickMode.RandomCount;
            string targetName = statusRemovalTarget switch
            {
                CharacterStatusRemovalTarget.Random =>
                    UsesKoreanLocale ? "제거 가능 상태" : "removable statuses",
                CharacterStatusRemovalTarget.Buff =>
                    UsesKoreanLocale ? "버프" : "buffs",
                CharacterStatusRemovalTarget.Debuff =>
                    UsesKoreanLocale ? "디버프" : "debuffs",
                CharacterStatusRemovalTarget.All =>
                    UsesKoreanLocale ? "전체 상태" : "statuses",
                _ => FormatStatusRemovalNames(
                    statusRemovalEffect,
                    statusRemovalEffects)
            };
            targetName = usesRandomCount
                ? (UsesKoreanLocale
                    ? $"{targetName} 중 {Mathf.Max(1, statusRemovalPickCount)}개"
                    : $"{Mathf.Max(1, statusRemovalPickCount)} of " +
                      $"{targetName}")
                : (UsesKoreanLocale
                    ? $"{targetName} 모두"
                    : $"all {targetName}");
            string countName;
            if (statusRemovalAmountMode ==
                CharacterStatusRemovalAmountMode.CurrentStacksRatio)
            {
                float percentage = Mathf.Clamp01(statusRemovalRatio) * 100f;
                countName = UsesKoreanLocale
                    ? $"현재 스택의 {percentage:0.##}%"
                    : $"{percentage:0.##}% of current stacks";
            }
            else
            {
                countName = statusRemovalCount == 0
                    ? (UsesKoreanLocale ? "전부" : "all stacks")
                    : (UsesKoreanLocale
                        ? $"{statusRemovalCount}스택"
                        : $"{statusRemovalCount} stack(s)");
            }
            return UsesKoreanLocale
                ? $"상태 제거: {targetName} / {countName}"
                : $"Remove: {targetName} / {countName}";
        }

        string amountText = amountMode == CharacterDamageAmountMode.Ratio
            ? (UsesKoreanLocale
                ? $"공격력 × {amount:0.##}"
                : $"ATK × {amount:0.##}")
            : (UsesKoreanLocale
                ? $"고정 {amount:0.##}"
                : $"Fixed {amount:0.##}");
        if (!includeFinalDamage)
            return $"{typeName} / {amountText}";

        return UsesKoreanLocale
            ? $"{typeName} / {amountText} (피해 {finalDamage})"
            : $"{typeName} / {amountText} (Damage {finalDamage})";
    }

    private static string FormatStatusRemovalNames(
        StatusEffectSO legacyStatus,
        IReadOnlyList<StatusEffectSO> statuses)
    {
        if (statuses == null || statuses.Count == 0)
            return GetStatusEffectName(legacyStatus);

        List<string> names = new();
        HashSet<StatusEffectSO> visited = new();
        foreach (StatusEffectSO status in statuses)
        {
            if (status != null && visited.Add(status))
                names.Add(GetStatusEffectName(status));
        }

        return names.Count > 0
            ? string.Join(", ", names)
            : GetStatusEffectName(legacyStatus);
    }

    private static string FormatStatusConditionMetric(
        CharacterNumericCondition condition)
    {
        if (condition == null)
            return UsesKoreanLocale ? "상태 스택" : "status stacks";

        if (condition.StatusSelectionScope !=
            CharacterStatusSelectionScope.SelectedStatuses)
        {
            string category = condition.StatusSelectionScope ==
                              CharacterStatusSelectionScope.AllBuffs
                ? (UsesKoreanLocale ? "보유 버프" : "active buffs")
                : (UsesKoreanLocale ? "보유 디버프" : "active debuffs");
            string categoryMatch = condition.StatusMatchMode switch
            {
                CharacterStatusConditionMatchMode.All =>
                    UsesKoreanLocale
                        ? $"{category} 모두"
                        : $"all {category}",
                CharacterStatusConditionMatchMode.AtLeastCount =>
                    UsesKoreanLocale
                        ? $"{category} 중 " +
                          $"{condition.RequiredStatusMatchCount}개 이상"
                        : $"at least " +
                          $"{condition.RequiredStatusMatchCount} " +
                          category,
                _ => UsesKoreanLocale
                    ? $"{category} 중 하나 이상"
                    : $"any {category}"
            };
            return UsesKoreanLocale
                ? $"{categoryMatch}의 상태 스택"
                : $"{categoryMatch} status stacks";
        }

        CharacterStatusSelection selection = condition.StatusSelection;
        string names = FormatStatusSelectionNames(selection);
        string match = condition.StatusMatchMode switch
        {
            CharacterStatusConditionMatchMode.All =>
                UsesKoreanLocale ? $"{names} 모두" : $"all of {names}",
            CharacterStatusConditionMatchMode.AtLeastCount =>
                UsesKoreanLocale
                    ? $"{names} 중 {condition.RequiredStatusMatchCount}개 이상"
                    : $"at least {condition.RequiredStatusMatchCount} of " +
                      names,
            _ => UsesKoreanLocale
                ? $"{names} 중 하나 이상"
                : $"any of {names}"
        };
        return UsesKoreanLocale
            ? $"{match}의 상태 스택"
            : $"{match} status stacks";
    }

    private static string FormatStatusSelectionNames(
        CharacterStatusSelection selection)
    {
        List<string> names = new();
        for (int index = 0; index < selection.Count; index++)
        {
            StatusEffectSO status = selection.GetStatus(index);
            if (status == null)
                continue;

            bool duplicate = false;
            for (int previous = 0; previous < index; previous++)
            {
                if (CharacterStatusSelection.IsSameStatus(
                        selection.GetStatus(previous),
                        status))
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
                names.Add(GetStatusEffectName(status));
        }

        return names.Count > 0
            ? string.Join(", ", names)
            : (UsesKoreanLocale ? "상태" : "status");
    }

    private static string FormatEffects(
        IReadOnlyList<CharacterEffectDefinition> effects,
        Func<CharacterEffectDefinition, int> calculateDamage)
    {
        if (effects == null || effects.Count == 0)
            return string.Empty;

        StringBuilder builder = new();
        foreach (CharacterEffectDefinition effect in effects)
        {
            if (effect == null)
                continue;

            if (builder.Length > 0)
                builder.Append(" + ");
            if (effect.PreconditionFailurePolicy ==
                CharacterEffectPreconditionFailurePolicy.SkipEffect)
            {
                builder.Append(
                    UsesKoreanLocale
                        ? "선택 효과: "
                        : "Optional: ");
            }
            if (effect.TargetMode == CharacterEffectTargetMode.Source)
            {
                builder.Append(
                    UsesKoreanLocale
                        ? "자신에게 "
                        : "Self: ");
            }
            else if (effect.TargetMode ==
                     CharacterEffectTargetMode.FreshSelection)
            {
                builder.Append(FormatFreshEffectSelector(
                    effect.TargetSelector));
            }

            int finalDamage = calculateDamage?.Invoke(effect) ?? 0;
            switch (effect.Type)
            {
                case CharacterEffectType.ApplyStatus:
                    builder.Append(FormatAbility(
                        CharacterAttackDamageType.StatusEffect,
                        effect.DamageAmountMode,
                        effect.DamageAmount,
                        effect.StatusEffect,
                        effect.StatusDuration,
                        effect.StatusStacks,
                        null,
                        effect.StatusRemovalTarget,
                        effect.StatusRemovalAmountMode,
                        effect.StatusRemovalCount,
                        effect.StatusRemovalRatio,
                        finalDamage));
                    break;
                case CharacterEffectType.RemoveStatus:
                    builder.Append(FormatAbility(
                        CharacterAttackDamageType.StatusRemoval,
                        effect.DamageAmountMode,
                        effect.DamageAmount,
                        null,
                        effect.StatusDuration,
                        effect.StatusStacks,
                        effect.StatusEffect,
                        effect.StatusRemovalTarget,
                        effect.StatusRemovalAmountMode,
                        effect.StatusRemovalCount,
                        effect.StatusRemovalRatio,
                        finalDamage,
                        statusRemovalEffects:
                            effect.StatusRemovalEffects,
                        statusRemovalPickMode:
                            effect.StatusRemovalPickMode,
                        statusRemovalPickCount:
                            effect.StatusRemovalPickCount));
                    break;
                case CharacterEffectType.GainResource:
                    builder.Append(FormatResourceGain(effect));
                    break;
                case CharacterEffectType.SpendResource:
                    builder.Append(FormatResourceSpend(effect));
                    break;
                case CharacterEffectType.Heal:
                    builder.Append(FormatHeal(effect));
                    break;
                case CharacterEffectType.Shield:
                    builder.Append(FormatShield(effect));
                    break;
                case CharacterEffectType.SpendHealth:
                    builder.Append(FormatHealthSpend(effect));
                    break;
                default:
                    bool hasRuntimeScaling =
                        effect.SourceResourceScale != 0f ||
                        effect.TargetCurrentHealthScale != 0f ||
                        effect.TargetMaxHealthScale != 0f ||
                        effect.SourceStatusStacksScale != 0f ||
                        effect.TargetStatusStacksScale != 0f ||
                        effect.StatusContributionMultipliers.Count > 0;
                    string damageText = FormatAbility(
                        effect.DamageType,
                        effect.DamageAmountMode,
                        effect.DamageAmount,
                        null,
                        effect.StatusDuration,
                        effect.StatusStacks,
                        null,
                        effect.StatusRemovalTarget,
                        effect.StatusRemovalAmountMode,
                        effect.StatusRemovalCount,
                        effect.StatusRemovalRatio,
                        finalDamage,
                        !hasRuntimeScaling);
                    builder.Append(AppendScalingTerms(
                        damageText,
                        effect,
                        true));
                    break;
            }

            if (effect.FailurePolicy ==
                CharacterEffectFailurePolicy.StopRemainingEffects)
            {
                builder.Append(
                    UsesKoreanLocale
                        ? " (실패 시 후속 효과 중단)"
                        : " (stop remaining effects on failure)");
            }
        }

        return builder.ToString();
    }

    private static string FormatFreshEffectSelector(
        CharacterEffectTargetSelector selector)
    {
        if (selector == null)
        {
            return UsesKoreanLocale
                ? "별도 대상 미지정 → "
                : "Fresh target unassigned → ";
        }

        string subject = FormatSubject(
            selector.TargetFaction,
            selector.Subject,
            selector.SubjectMetric,
            selector.SubjectCount);
        string conditions = FormatNumericConditions(
                selector.HasNumericConditions,
                selector.ConditionMatchMode,
                selector.NumericConditions)
            .TrimEnd(' ', ',');
        string details = subject + FormatArea(selector.AreaOffsets);
        if (!string.IsNullOrWhiteSpace(conditions))
            details += $", {conditions}";

        return UsesKoreanLocale
            ? $"별도 선택({details}) → "
            : $"Fresh selection ({details}) → ";
    }

    private static bool CanExecuteEffectsWithoutActionTargets(
        IReadOnlyList<CharacterEffectDefinition> effects)
    {
        if (effects == null)
            return false;

        bool hasEffect = false;
        foreach (CharacterEffectDefinition effect in effects)
        {
            if (effect == null)
                continue;
            if (effect.RequiresActionTargets &&
                effect.PreconditionFailurePolicy !=
                CharacterEffectPreconditionFailurePolicy.SkipEffect)
            {
                return false;
            }
            hasEffect = true;
        }

        return hasEffect;
    }

    private static string FormatResourceGain(
        CharacterEffectDefinition effect)
    {
        string baseAmount = effect.AmountMode ==
                            CharacterDamageAmountMode.Ratio
            ? (UsesKoreanLocale
                ? $"공격력 × {effect.Amount:0.##}"
                : $"ATK × {effect.Amount:0.##}")
            : $"{effect.Amount:0.##}";
        string formula = AppendScalingTerms(
            baseAmount,
            effect,
            false);
        return UsesKoreanLocale
            ? $"자원 획득: {formula}"
            : $"Gain Resource: {formula}";
    }

    private static string FormatResourceSpend(
        CharacterEffectDefinition effect)
    {
        return UsesKoreanLocale
            ? $"자원 소비: {Mathf.Max(0f, effect.Amount):0.##}"
            : $"Spend Resource: {Mathf.Max(0f, effect.Amount):0.##}";
    }

    private static string FormatHeal(
        CharacterEffectDefinition effect)
    {
        string baseAmount = effect.AmountMode ==
                            CharacterDamageAmountMode.Ratio
            ? (UsesKoreanLocale
                ? $"공격력 × {effect.Amount:0.##}"
                : $"ATK × {effect.Amount:0.##}")
            : $"{effect.Amount:0.##}";
        string formula = AppendScalingTerms(
            baseAmount,
            effect,
            true);
        return UsesKoreanLocale
            ? $"체력 회복: {formula}"
            : $"Heal: {formula}";
    }

    private static string FormatHealthSpend(
        CharacterEffectDefinition effect)
    {
        return UsesKoreanLocale
            ? $"체력 소비: {Mathf.Max(0f, effect.Amount):0.##}"
            : $"Spend Health: {Mathf.Max(0f, effect.Amount):0.##}";
    }

    private static string FormatShield(
        CharacterEffectDefinition effect)
    {
        string baseAmount = effect.AmountMode ==
                            CharacterDamageAmountMode.Ratio
            ? (UsesKoreanLocale
                ? $"공격력 × {effect.Amount:0.##}"
                : $"ATK × {effect.Amount:0.##}")
            : $"{effect.Amount:0.##}";
        string formula = AppendScalingTerms(
            baseAmount,
            effect,
            true);
        return UsesKoreanLocale
            ? $"보호막 부여: {formula}"
            : $"Grant Shield: {formula}";
    }

    private static string AppendScalingTerms(
        string text,
        CharacterEffectDefinition effect,
        bool includeTargetTerms)
    {
        if (effect == null)
            return text;

        StringBuilder builder = new(text ?? string.Empty);
        AppendScalingTerm(
            builder,
            effect.SourceResourceScale,
            UsesKoreanLocale ? "현재 자원" : "Current Resource");
        string sourceStatusName = GetStatusEffectName(
            effect.SourceStatusScalingEffect);
        AppendScalingTerm(
            builder,
            effect.SourceStatusStacksScale,
            UsesKoreanLocale
                ? $"시전자 {sourceStatusName} 스택"
                : $"Source {sourceStatusName} Stacks");
        AppendStatusContributionMultipliers(
            builder,
            effect.StatusContributionMultipliers);
        if (!includeTargetTerms)
            return builder.ToString();

        AppendScalingTerm(
            builder,
            effect.TargetCurrentHealthScale,
            UsesKoreanLocale ? "대상 현재 체력" : "Target Current HP");
        AppendScalingTerm(
            builder,
            effect.TargetMaxHealthScale,
            UsesKoreanLocale ? "대상 최대 체력" : "Target Maximum HP");
        string targetStatusName = GetStatusEffectName(
            effect.TargetStatusScalingEffect);
        AppendScalingTerm(
            builder,
            effect.TargetStatusStacksScale,
            UsesKoreanLocale
                ? $"대상 {targetStatusName} 스택"
                : $"Target {targetStatusName} Stacks");
        return builder.ToString();
    }

    private static string FormatStatusContributionMultipliers(
        IReadOnlyList<CharacterStatusStatContributionMultiplier> modifiers)
    {
        if (modifiers == null || modifiers.Count == 0)
        {
            return UsesKoreanLocale
                ? "설정된 상태 기여 배율 없음"
                : "No status contribution multipliers";
        }

        StringBuilder builder = new();
        AppendStatusContributionMultipliers(builder, modifiers);
        return builder.ToString().TrimStart(' ', '/', '+');
    }

    private static void AppendStatusContributionMultipliers(
        StringBuilder builder,
        IReadOnlyList<CharacterStatusStatContributionMultiplier> modifiers)
    {
        if (builder == null || modifiers == null)
            return;

        foreach (CharacterStatusStatContributionMultiplier modifier in
                 modifiers)
        {
            if (modifier?.StatusEffect == null)
                continue;

            if (builder.Length > 0)
                builder.Append(" / ");
            string statusName = GetStatusEffectName(
                modifier.StatusEffect);
            string statName = FormatStatusStatType(modifier.StatType);
            builder.Append(
                UsesKoreanLocale
                    ? $"{statusName}의 {statName} 기여 ×" +
                      $"{modifier.Multiplier:0.##}"
                    : $"{statusName} {statName} contribution ×" +
                      $"{modifier.Multiplier:0.##}");
        }
    }

    private static string FormatStatusStatType(
        StatusEffectStatType statType)
    {
        return statType switch
        {
            StatusEffectStatType.AttackPower =>
                UsesKoreanLocale ? "공격력" : "Attack Power",
            StatusEffectStatType.AttackSpeed =>
                UsesKoreanLocale ? "공격 속도" : "Attack Speed",
            StatusEffectStatType.IncomingDamage =>
                UsesKoreanLocale ? "받는 피해" : "Incoming Damage",
            StatusEffectStatType.TargetPriority =>
                UsesKoreanLocale ? "대상 우선순위" : "Target Priority",
            _ => statType.ToString()
        };
    }

    private static void AppendScalingTerm(
        StringBuilder builder,
        float scale,
        string label)
    {
        if (float.IsNaN(scale) || float.IsInfinity(scale) ||
            scale == 0f)
        {
            return;
        }

        if (builder.Length > 0)
            builder.Append(scale < 0f ? " - " : " + ");
        else if (scale < 0f)
            builder.Append('-');

        builder.Append(
            $"{label} × {Mathf.Abs(scale):0.##}");
    }

    private static string GetStatusEffectName(StatusEffectSO definition)
    {
        if (definition != null)
        {
            if (!string.IsNullOrWhiteSpace(definition.NameLocalizationKey))
                return LocalizationService.Get(definition.NameLocalizationKey);
            return definition.name;
        }

        return UsesKoreanLocale ? "미지정 상태" : "Unassigned status";
    }

    private static string GetCharacterDefinitionName(CharacterSO definition)
    {
        if (definition == null)
            return UsesKoreanLocale
                ? "미지정 캐릭터"
                : "an unassigned character";
        if (!string.IsNullOrWhiteSpace(definition.NameLocalizationKey))
            return LocalizationService.Get(definition.NameLocalizationKey);
        if (!string.IsNullOrWhiteSpace(definition.CharacterName))
            return definition.CharacterName;
        return definition.name;
    }

    private static string FormatArea(
        IReadOnlyList<CharacterTargetAreaOffset> offsets)
    {
        if (offsets == null || offsets.Count == 0)
            return string.Empty;

        int cellCount = 1;
        foreach (CharacterTargetAreaOffset offset in offsets)
        {
            if (offset != null && !offset.IsCenter)
                cellCount++;
        }

        return UsesKoreanLocale
            ? $" / 범위 {cellCount}칸"
            : $" / Area {cellCount} cells";
    }

    private static void AppendCodexLine(StringBuilder builder, string line)
    {
        if (builder.Length > 0)
            builder.Append('\n');
        builder.Append(line);
    }

    private static bool UsesKoreanLocale =>
        LocalizationService.CurrentLocale?.StartsWith(
            "ko",
            StringComparison.OrdinalIgnoreCase) == true;
}
