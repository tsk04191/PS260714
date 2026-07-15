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

    public string CharacterName => characterName;
    public CharacterAttackType AttackType => attackType;
    public int AttackPower => attackPower;
    public float AttackWeight => attackWeight;
    public float AttackCooldown => attackCooldown;
    public int TargetCount => targetCount;
    public float FireDuration => fireDuration;
    public float FireTickInterval => fireTickInterval;
    public int FireTickDamage => fireTickDamage;

    private void OnValidate()
    {
        attackPower = Mathf.Max(1, attackPower);
        attackWeight = Mathf.Max(0.01f, attackWeight);
        attackCooldown = Mathf.Max(0.1f, attackCooldown);
        targetCount = Mathf.Max(1, targetCount);
        fireDuration = Mathf.Max(0.1f, fireDuration);
        fireTickInterval = Mathf.Max(0.1f, fireTickInterval);
        fireTickDamage = Mathf.Max(1, fireTickDamage);
    }

    public CharacterData CreateData()
    {
        return new CharacterData(this);
    }
}
