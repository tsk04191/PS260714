using UnityEngine;

public enum CharacterAttackType
{
    LowestHealth,
    RandomMultiple,
    CrossHighestHealth,
    FireRandom
}

[CreateAssetMenu(fileName = "Character", menuName = "Dungeon/Character")]
public sealed class CharacterSO : ScriptableObject
{
    [SerializeField] private string characterName = "BASIC TURRET";
    [SerializeField] private CharacterAttackType attackType;
    [SerializeField, Min(1)] private int attackPower = 1;
    [SerializeField, Min(0.01f)] private float attackWeight = 1f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 1f;
    [SerializeField, Min(1)] private int targetCount = 1;
    [SerializeField, Min(0.1f)] private float fireDuration = 6f;
    [SerializeField, Min(0.1f)] private float fireTickInterval = 2f;
    [SerializeField, Min(1)] private int fireTickDamage = 1;

    [Header("Active Skill")]
    [SerializeField, Min(1)] private int activeSkillCost = 1;
    [SerializeField, Min(0.1f)] private float activeSkillDuration = 10f;
    [SerializeField, Min(1)] private int activeSkillAttackCount = 1;

    public string CharacterName => characterName;
    public CharacterAttackType AttackType => attackType;
    public int AttackPower => attackPower;
    public float AttackWeight => attackWeight;
    public float AttackCooldown => TimePrecision.Normalize(attackCooldown, 0.1f);
    public int TargetCount => targetCount;
    public float FireDuration => TimePrecision.Normalize(fireDuration, 0.1f);
    public float FireTickInterval =>
        TimePrecision.Normalize(fireTickInterval, 0.1f);
    public int FireTickDamage => fireTickDamage;
    public int ActiveSkillCost => activeSkillCost;
    public float ActiveSkillDuration =>
        TimePrecision.Normalize(activeSkillDuration, 0.1f);
    public int ActiveSkillAttackCount => activeSkillAttackCount;

    private void OnValidate()
    {
        attackPower = Mathf.Max(1, attackPower);
        attackWeight = Mathf.Max(0.01f, attackWeight);
        attackCooldown = TimePrecision.Normalize(attackCooldown, 0.1f);
        targetCount = Mathf.Max(1, targetCount);
        fireDuration = TimePrecision.Normalize(fireDuration, 0.1f);
        fireTickInterval = TimePrecision.Normalize(fireTickInterval, 0.1f);
        fireTickDamage = Mathf.Max(1, fireTickDamage);
        activeSkillCost = Mathf.Max(1, activeSkillCost);
        activeSkillDuration = TimePrecision.Normalize(
            activeSkillDuration,
            0.1f);
        activeSkillAttackCount = Mathf.Max(1, activeSkillAttackCount);
    }

    public CharacterData CreateData()
    {
        return new CharacterData(this);
    }
}
