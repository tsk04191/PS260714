using UnityEngine;

public enum CurrencyKind
{
    Soft = 0,
    PremiumFree = 1,
    PremiumPaid = 2,
    Event = 3,
}

[CreateAssetMenu(
    fileName = "CurrencyItem",
    menuName = "PS260714/Items/Currency")]
public sealed class CurrencyItemSO : ItemDefinitionSO
{
    [Header("Currency")]
    [SerializeField] private CurrencyKind currencyKind;
    [SerializeField] private bool purchasedWithRealMoney;

    public CurrencyKind CurrencyKind => currencyKind;
    public bool PurchasedWithRealMoney =>
        purchasedWithRealMoney;
}
