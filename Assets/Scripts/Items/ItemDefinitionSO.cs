using System;
using UnityEngine;

public enum ItemCategory
{
    Currency = 0,
    RecruitTicket = 1,
    UpgradeMaterial = 2,
    Consumable = 3,
    EventCurrency = 4,
}

public enum ItemRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4,
}

public static class CoreItemIds
{
    public const string SoftCredit = "currency.soft";
    public const string PaidCredit = "currency.premium.paid";
    public const string FreeCredit = "currency.premium.free";
    public const string StandardRecruitTicket =
        "ticket.recruit.standard";
    public const string BasicUpgradeMaterial =
        "material.upgrade.basic";
}

public abstract class ItemDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string itemId;
    [SerializeField] private ItemCategory category;
    [SerializeField] private ItemRarity rarity;
    [SerializeField] private int sortOrder;

    [Header("Localization")]
    [SerializeField] private string koreanName;
    [SerializeField] private string englishName;
    [SerializeField, TextArea] private string koreanDescription;
    [SerializeField, TextArea] private string englishDescription;

    [Header("Presentation")]
    [SerializeField] private Sprite icon;

    [Header("Inventory")]
    [SerializeField, Min(0)] private long maximumStack;
    [SerializeField, Min(0)] private long initialAmount;
    [SerializeField] private bool hiddenInStorage;

    public string ItemId => itemId ?? string.Empty;
    public ItemCategory Category => category;
    public ItemRarity Rarity => rarity;
    public int SortOrder => sortOrder;
    public Sprite Icon => icon;
    public long MaximumStack => Math.Max(0L, maximumStack);
    public bool HasUnlimitedStack => MaximumStack == 0L;
    public long InitialAmount => ClampAmount(
        Math.Max(0L, initialAmount));
    public bool HiddenInStorage => hiddenInStorage;

    public string GetDisplayName(bool korean)
    {
        string localized = korean ? koreanName : englishName;
        if (!string.IsNullOrWhiteSpace(localized))
            return localized.Trim();
        return string.IsNullOrWhiteSpace(ItemId)
            ? name
            : ItemId;
    }

    public string GetDescription(bool korean)
    {
        string localized = korean
            ? koreanDescription
            : englishDescription;
        return localized?.Trim() ?? string.Empty;
    }

    public long ClampAmount(long amount)
    {
        amount = Math.Max(0L, amount);
        return HasUnlimitedStack
            ? amount
            : Math.Min(amount, MaximumStack);
    }

    protected virtual void OnValidate()
    {
        itemId = itemId?.Trim() ?? string.Empty;
        koreanName = koreanName?.Trim() ?? string.Empty;
        englishName = englishName?.Trim() ?? string.Empty;
        maximumStack = Math.Max(0L, maximumStack);
        initialAmount = ClampAmount(
            Math.Max(0L, initialAmount));
    }
}
