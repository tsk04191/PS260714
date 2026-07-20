using UnityEngine;

public enum ETurretUpgradeType
{
    PrimaryPower,
    AttackSpeed,
    SkillPower,
    SkillCost,
}

public sealed class CharacterData
{
    public string CharacterName { get; private set; }
    public CharacterAttackType AttackType { get; private set; }
    public int AttackPower { get; private set; }
    public float AttackWeight { get; private set; }
    public float AttackCooldown { get; private set; }
    public int SkillAttackPower { get; private set; }
    public int TargetCount { get; private set; }
    public float FireDuration { get; private set; }
    public float FireTickInterval { get; private set; }
    public int FireTickDamage { get; private set; }
    public int FireSkillTargetCount { get; private set; }
    public Sprite TargetEffectSprite { get; private set; }
    public RuntimeAnimatorController TargetEffectController { get; private set; }
    public AudioClip AttackSfx { get; private set; }
    public int ActiveSkillCost { get; private set; }
    public float ActiveSkillDuration { get; private set; }
    public int ActiveSkillAttackCount { get; private set; }
    public int AttackDamage => Mathf.Max(
        1,
        Mathf.RoundToInt(AttackPower * AttackWeight));
    public int SkillAttackDamage => Mathf.Max(
        1,
        Mathf.RoundToInt(SkillAttackPower * AttackWeight));

    public CharacterData(CharacterSO original)
    {
        CharacterName = original != null ? original.CharacterName : string.Empty;
        AttackType = original != null
            ? original.AttackType
            : CharacterAttackType.LowestHealth;
        AttackPower = original != null ? original.AttackPower : 1;
        AttackWeight = original != null ? original.AttackWeight : 1f;
        AttackCooldown = original != null ? original.AttackCooldown : 1f;
        SkillAttackPower = original != null ? original.SkillAttackPower : 2;
        TargetCount = original != null ? original.TargetCount : 1;
        FireDuration = original != null ? original.FireDuration : 6f;
        FireTickInterval = original != null ? original.FireTickInterval : 2f;
        FireTickDamage = original != null ? original.FireTickDamage : 1;
        FireSkillTargetCount = original != null
            ? original.FireSkillTargetCount
            : 1;
        TargetEffectSprite = original != null
            ? original.TargetEffectSprite
            : null;
        TargetEffectController = original != null
            ? original.TargetEffectController
            : null;
        AttackSfx = original != null ? original.AttackSfx : null;
        ActiveSkillCost = original != null ? original.ActiveSkillCost : 1;
        ActiveSkillDuration = original != null
            ? original.ActiveSkillDuration
            : 10f;
        ActiveSkillAttackCount = original != null
            ? original.ActiveSkillAttackCount
            : 1;
    }

    public bool CanApplyUpgrade(ETurretUpgradeType upgradeType)
    {
        return upgradeType switch
        {
            ETurretUpgradeType.AttackSpeed => AttackCooldown > TimePrecision.Step,
            ETurretUpgradeType.SkillCost => ActiveSkillCost > 1,
            _ => true,
        };
    }

    public bool ApplyUpgrade(ETurretUpgradeType upgradeType)
    {
        if (!CanApplyUpgrade(upgradeType))
            return false;

        switch (upgradeType)
        {
            case ETurretUpgradeType.PrimaryPower:
                if (AttackType == CharacterAttackType.FireRandom)
                    FireDuration = TimePrecision.Normalize(FireDuration + 1f, 0.1f);
                else
                    AttackPower++;
                break;

            case ETurretUpgradeType.AttackSpeed:
                AttackCooldown = TimePrecision.Normalize(
                    AttackCooldown - TimePrecision.Step,
                    TimePrecision.Step);
                break;

            case ETurretUpgradeType.SkillPower:
                if (AttackType == CharacterAttackType.FireRandom)
                    FireSkillTargetCount++;
                else
                    SkillAttackPower++;
                break;

            case ETurretUpgradeType.SkillCost:
                ActiveSkillCost = Mathf.Max(1, ActiveSkillCost - 1);
                break;

            default:
                return false;
        }

        return true;
    }

    public string GetUpgradeLabel(ETurretUpgradeType upgradeType)
    {
        return upgradeType switch
        {
            ETurretUpgradeType.PrimaryPower
                when AttackType == CharacterAttackType.FireRandom =>
                $"FIRE DURATION {FireDuration:0.#}s > {FireDuration + 1f:0.#}s",
            ETurretUpgradeType.PrimaryPower =>
                $"ATTACK {AttackDamage} > " +
                $"{Mathf.Max(1, Mathf.RoundToInt((AttackPower + 1) * AttackWeight))}",
            ETurretUpgradeType.AttackSpeed =>
                $"COOLDOWN {AttackCooldown:0.#}s > " +
                $"{Mathf.Max(TimePrecision.Step, AttackCooldown - TimePrecision.Step):0.#}s",
            ETurretUpgradeType.SkillPower
                when AttackType == CharacterAttackType.FireRandom =>
                $"SKILL TARGETS {FireSkillTargetCount} > {FireSkillTargetCount + 1}",
            ETurretUpgradeType.SkillPower =>
                $"SKILL ATTACK {SkillAttackDamage} > " +
                $"{Mathf.Max(1, Mathf.RoundToInt((SkillAttackPower + 1) * AttackWeight))}",
            ETurretUpgradeType.SkillCost =>
                $"SKILL COST {ActiveSkillCost} > {Mathf.Max(1, ActiveSkillCost - 1)}",
            _ => upgradeType.ToString(),
        };
    }
}
