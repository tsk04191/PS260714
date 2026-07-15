using UnityEngine;

[CreateAssetMenu(fileName = "Character", menuName = "Dungeon/Character")]
public sealed class CharacterSO : ScriptableObject
{
    [SerializeField] private string characterName = "BASIC TURRET";
    [SerializeField, Min(1)] private int attackPower = 1;
    [SerializeField, Min(0.01f)] private float attackWeight = 1f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 1f;

    public string CharacterName => characterName;
    public int AttackPower => attackPower;
    public float AttackWeight => attackWeight;
    public float AttackCooldown => attackCooldown;

    private void OnValidate()
    {
        attackPower = Mathf.Max(1, attackPower);
        attackWeight = Mathf.Max(0.01f, attackWeight);
        attackCooldown = Mathf.Max(0.1f, attackCooldown);
    }

    public CharacterData CreateData()
    {
        return new CharacterData(this);
    }
}
