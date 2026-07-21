using PS260714.Localization;
using UnityEngine;

public static class CharacterLocalization
{
    public static string GetName(CharacterData data)
    {
        return data == null
            ? string.Empty
            : GetName(data.AttackType);
    }

    public static string GetName(CharacterAttackType attackType)
    {
        return LocalizationService.Get(attackType switch
        {
            CharacterAttackType.RandomMultiple =>
                LocalizationKeys.CharacterDualName,
            CharacterAttackType.CrossHighestHealth =>
                LocalizationKeys.CharacterAreaName,
            CharacterAttackType.FireRandom =>
                LocalizationKeys.CharacterFlameName,
            _ => LocalizationKeys.CharacterBasicName,
        });
    }

    public static string GetTypeName(CharacterAttackType attackType)
    {
        return LocalizationService.Get(attackType switch
        {
            CharacterAttackType.RandomMultiple =>
                LocalizationKeys.CodexCharacterTypeMultiple,
            CharacterAttackType.CrossHighestHealth =>
                LocalizationKeys.CodexCharacterTypeCross,
            CharacterAttackType.FireRandom =>
                LocalizationKeys.CodexCharacterTypeFire,
            _ => LocalizationKeys.CodexCharacterTypeLowest,
        });
    }

    public static string GetUpgradeTitle(
        CharacterData data,
        ETurretUpgradeType upgradeType)
    {
        string key = upgradeType switch
        {
            ETurretUpgradeType.PrimaryPower
                when data != null &&
                     data.AttackType == CharacterAttackType.FireRandom =>
                LocalizationKeys.UiDungeonRewardUpgradeFireDurationTitle,
            ETurretUpgradeType.PrimaryPower =>
                LocalizationKeys.UiDungeonRewardUpgradeAttackPowerTitle,
            ETurretUpgradeType.AttackSpeed =>
                LocalizationKeys.UiDungeonRewardUpgradeAttackSpeedTitle,
            ETurretUpgradeType.SkillPower
                when data != null &&
                     data.AttackType == CharacterAttackType.FireRandom =>
                LocalizationKeys.UiDungeonRewardUpgradeSkillTargetsTitle,
            ETurretUpgradeType.SkillPower =>
                LocalizationKeys.UiDungeonRewardUpgradeSkillPowerTitle,
            ETurretUpgradeType.SkillCost =>
                LocalizationKeys.UiDungeonRewardUpgradeSkillCostTitle,
            _ => LocalizationKeys.UiDungeonRewardUpgradeGenericTitle,
        };
        return LocalizationService.Get(key);
    }

    public static string GetUpgradeDescription(
        CharacterData data,
        ETurretUpgradeType upgradeType)
    {
        if (data == null)
            return string.Empty;

        switch (upgradeType)
        {
            case ETurretUpgradeType.PrimaryPower
                when data.AttackType == CharacterAttackType.FireRandom:
                return GetUpgradeChange(
                    LocalizationKeys
                        .UiDungeonRewardUpgradeFireDurationChange,
                    data.FireDuration,
                    data.FireDuration + 1f);
            case ETurretUpgradeType.PrimaryPower:
                return GetUpgradeChange(
                    LocalizationKeys.UiDungeonRewardUpgradeAttackChange,
                    data.AttackDamage,
                    Mathf.Max(
                        1,
                        Mathf.RoundToInt(
                            (data.AttackPower + 1) * data.AttackWeight)));
            case ETurretUpgradeType.AttackSpeed:
                return GetUpgradeChange(
                    LocalizationKeys.UiDungeonRewardUpgradeCooldownChange,
                    data.AttackCooldown,
                    Mathf.Max(
                        TimePrecision.Step,
                        data.AttackCooldown - TimePrecision.Step));
            case ETurretUpgradeType.SkillPower
                when data.AttackType == CharacterAttackType.FireRandom:
                return GetUpgradeChange(
                    LocalizationKeys
                        .UiDungeonRewardUpgradeSkillTargetsChange,
                    data.FireSkillTargetCount,
                    data.FireSkillTargetCount + 1);
            case ETurretUpgradeType.SkillPower:
                return GetUpgradeChange(
                    LocalizationKeys.UiDungeonRewardUpgradeSkillAttackChange,
                    data.SkillAttackDamage,
                    Mathf.Max(
                        1,
                        Mathf.RoundToInt(
                            (data.SkillAttackPower + 1) *
                            data.AttackWeight)));
            case ETurretUpgradeType.SkillCost:
                return GetUpgradeChange(
                    LocalizationKeys.UiDungeonRewardUpgradeSkillCostChange,
                    data.ActiveSkillCost,
                    Mathf.Max(1, data.ActiveSkillCost - 1));
            default:
                return string.Empty;
        }
    }

    public static string GetIdentity(
        string assetName,
        CharacterAttackType attackType)
    {
        return LocalizationService.Get(
            LocalizationKeys.CodexCharacterIdentity,
            LocalizationService.Arg("asset", assetName),
            LocalizationService.Arg("type", GetTypeName(attackType)));
    }

    public static string GetStats(CharacterData data)
    {
        if (data == null)
            return string.Empty;

        if (data.AttackType == CharacterAttackType.FireRandom)
        {
            return LocalizationService.Get(
                LocalizationKeys.CodexCharacterStatsFire,
                LocalizationService.Arg("cooldown", data.AttackCooldown),
                LocalizationService.Arg("duration", data.FireDuration),
                LocalizationService.Arg("damage", data.FireTickDamage),
                LocalizationService.Arg("interval", data.FireTickInterval),
                LocalizationService.Arg("weight", data.AttackWeight));
        }

        return LocalizationService.Get(
            LocalizationKeys.CodexCharacterStatsAttack,
            LocalizationService.Arg("attack", data.AttackDamage),
            LocalizationService.Arg("cooldown", data.AttackCooldown),
            LocalizationService.Arg("skill", data.SkillAttackDamage),
            LocalizationService.Arg("weight", data.AttackWeight));
    }

    public static string GetNormalAttackDescription(CharacterData data)
    {
        if (data == null)
            return string.Empty;

        return data.AttackType switch
        {
            CharacterAttackType.RandomMultiple => LocalizationService.Get(
                LocalizationKeys.CodexCharacterAttackMultiple,
                LocalizationService.Arg("count", data.TargetCount),
                LocalizationService.Arg("damage", data.AttackDamage)),
            CharacterAttackType.CrossHighestHealth => LocalizationService.Get(
                LocalizationKeys.CodexCharacterAttackCross,
                LocalizationService.Arg("damage", data.AttackDamage)),
            CharacterAttackType.FireRandom => LocalizationService.Get(
                LocalizationKeys.CodexCharacterAttackFire,
                LocalizationService.Arg("duration", data.FireDuration),
                LocalizationService.Arg("damage", data.FireTickDamage),
                LocalizationService.Arg("interval", data.FireTickInterval)),
            _ => LocalizationService.Get(
                LocalizationKeys.CodexCharacterAttackLowest,
                LocalizationService.Arg("damage", data.AttackDamage)),
        };
    }

    public static string GetActiveSkillDescription(
        CharacterData data,
        float? fireDurationOverride = null)
    {
        if (data == null)
            return string.Empty;

        return data.AttackType switch
        {
            CharacterAttackType.RandomMultiple => LocalizationService.Get(
                LocalizationKeys.CodexCharacterSkillMultiple,
                LocalizationService.Arg("duration", data.ActiveSkillDuration),
                LocalizationService.Arg("count", data.TargetCount + 2),
                LocalizationService.Arg("damage", data.SkillAttackDamage)),
            CharacterAttackType.CrossHighestHealth => LocalizationService.Get(
                LocalizationKeys.CodexCharacterSkillCross,
                LocalizationService.Arg("count", data.ActiveSkillAttackCount),
                LocalizationService.Arg("inner", data.SkillAttackDamage),
                LocalizationService.Arg(
                    "outer",
                    Mathf.Max(
                        1,
                        Mathf.FloorToInt(data.SkillAttackDamage * 0.5f)))),
            CharacterAttackType.FireRandom => LocalizationService.Get(
                LocalizationKeys.CodexCharacterSkillFire,
                LocalizationService.Arg("count", data.ActiveSkillAttackCount),
                LocalizationService.Arg("centers", data.FireSkillTargetCount),
                LocalizationService.Arg(
                    "duration",
                    fireDurationOverride ?? data.FireDuration)),
            _ => LocalizationService.Get(
                LocalizationKeys.CodexCharacterSkillLowest,
                LocalizationService.Arg("damage", data.SkillAttackDamage)),
        };
    }

    public static string GetActiveSkillTitle(int cost)
    {
        return LocalizationService.Get(
            LocalizationKeys.CodexCharacterActiveSkillCost,
            LocalizationService.Arg("cost", cost));
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
        if (data == null)
            return string.Empty;

        return data.AttackType == CharacterAttackType.FireRandom
            ? LocalizationService.Get(
                LocalizationKeys.UiTurretAttackFire,
                LocalizationService.Arg("duration", data.FireDuration),
                LocalizationService.Arg("targets", data.FireSkillTargetCount))
            : LocalizationService.Get(
                LocalizationKeys.UiTurretAttackDamage,
                LocalizationService.Arg("attack", data.AttackDamage),
                LocalizationService.Arg("skill", data.SkillAttackDamage));
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

    public static string GetCooldownActiveTime(float seconds)
    {
        return GetSecondsText(
            LocalizationKeys.UiTurretCooldownActiveTime,
            seconds);
    }

    public static string GetCooldownActiveCount(int count)
    {
        return LocalizationService.Get(
            LocalizationKeys.UiTurretCooldownActiveCount,
            LocalizationService.Arg("count", count));
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

    private static string GetUpgradeChange(
        string key,
        object before,
        object after)
    {
        return LocalizationService.Get(
            key,
            LocalizationService.Arg("before", before),
            LocalizationService.Arg("after", after));
    }
}
