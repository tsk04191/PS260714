using UnityEngine;

[CreateAssetMenu(
    fileName = "RecruitTicketItem",
    menuName = "PS260714/Items/Recruit Ticket")]
public sealed class RecruitTicketItemSO : ItemDefinitionSO
{
    [Header("Recruit Ticket")]
    [SerializeField] private string bannerGroupId = "standard";
    [SerializeField, Min(1)] private int recruitsPerItem = 1;

    public string BannerGroupId =>
        bannerGroupId ?? string.Empty;
    public int RecruitsPerItem => Mathf.Max(1, recruitsPerItem);

    protected override void OnValidate()
    {
        base.OnValidate();
    }
}
