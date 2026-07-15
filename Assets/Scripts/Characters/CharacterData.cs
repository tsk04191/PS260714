using UnityEngine;

public sealed class CharacterData
{
    public string CharacterName { get; private set; }
    public int AttackPower { get; private set; }
    public float AttackWeight { get; private set; }
    public float AttackCooldown { get; private set; }
    public int AttackDamage => Mathf.Max(
        1,
        Mathf.RoundToInt(AttackPower * AttackWeight));

    public CharacterData(CharacterSO original)
    {
        CharacterName = original != null ? original.CharacterName : string.Empty;
        AttackPower = original != null ? original.AttackPower : 1;
        AttackWeight = original != null ? original.AttackWeight : 1f;
        AttackCooldown = original != null ? original.AttackCooldown : 1f;
    }
}
