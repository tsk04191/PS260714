using UnityEngine;

public enum UpgradeMaterialTarget
{
    Any = 0,
    Character = 1,
    Equipment = 2,
    Skill = 3,
}

[CreateAssetMenu(
    fileName = "UpgradeMaterialItem",
    menuName = "PS260714/Items/Upgrade Material")]
public sealed class UpgradeMaterialItemSO : ItemDefinitionSO
{
    [Header("Upgrade Material")]
    [SerializeField] private UpgradeMaterialTarget target;
    [SerializeField, Min(1)] private int grade = 1;
    [SerializeField, Min(0)] private int upgradeValue;

    public UpgradeMaterialTarget Target => target;
    public int Grade => Mathf.Max(1, grade);
    public int UpgradeValue => Mathf.Max(0, upgradeValue);

    protected override void OnValidate()
    {
        base.OnValidate();
    }
}
