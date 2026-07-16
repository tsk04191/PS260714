using UnityEngine;

public sealed class CharacterData
{
    public string CharacterName { get; private set; }
    public CharacterAttackType AttackType { get; private set; }
    public int AttackPower { get; private set; }
    public float AttackWeight { get; private set; }
    public float AttackCooldown { get; private set; }
    public int TargetCount { get; private set; }
    public float FireDuration { get; private set; }
    public float FireTickInterval { get; private set; }
    public int FireTickDamage { get; private set; }
    public int ActiveSkillCost { get; private set; }
    public float ActiveSkillDuration { get; private set; }
    public int ActiveSkillAttackCount { get; private set; }
    public int AttackDamage => Mathf.Max(
        1,
        Mathf.RoundToInt(AttackPower * AttackWeight));

    public CharacterData(CharacterSO original)
    {
        CharacterName = original != null ? original.CharacterName : string.Empty;
        AttackType = original != null
            ? original.AttackType
            : CharacterAttackType.LowestHealth;
        AttackPower = original != null ? original.AttackPower : 1;
        AttackWeight = original != null ? original.AttackWeight : 1f;
        AttackCooldown = original != null ? original.AttackCooldown : 1f;
        TargetCount = original != null ? original.TargetCount : 1;
        FireDuration = original != null ? original.FireDuration : 6f;
        FireTickInterval = original != null ? original.FireTickInterval : 2f;
        FireTickDamage = original != null ? original.FireTickDamage : 1;
        ActiveSkillCost = original != null ? original.ActiveSkillCost : 1;
        ActiveSkillDuration = original != null
            ? original.ActiveSkillDuration
            : 10f;
        ActiveSkillAttackCount = original != null
            ? original.ActiveSkillAttackCount
            : 1;
    }
}
