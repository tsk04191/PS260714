using System;
using PS260714.Localization;
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
    [SerializeField] private string nameLocalizationKey;
    [SerializeField] private string descriptionLocalizationKey;
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
    public string NameLocalizationKey =>
        nameLocalizationKey ?? string.Empty;
    public string DescriptionLocalizationKey =>
        descriptionLocalizationKey ?? string.Empty;
    public Sprite Icon => icon;
    public long MaximumStack => Math.Max(0L, maximumStack);
    public bool HasUnlimitedStack => MaximumStack == 0L;
    public long InitialAmount => ClampAmount(
        Math.Max(0L, initialAmount));
    public bool HiddenInStorage => hiddenInStorage;

    public string GetDisplayName(bool korean)
    {
        string localized = ResolveForLocale(
            nameLocalizationKey,
            korean);
        if (string.IsNullOrWhiteSpace(localized))
            localized = korean ? koreanName : englishName;
        if (!string.IsNullOrWhiteSpace(localized))
            return localized.Trim();
        return string.IsNullOrWhiteSpace(ItemId)
            ? name
            : ItemId;
    }

    public string GetDescription(bool korean)
    {
        string localized = ResolveForLocale(
            descriptionLocalizationKey,
            korean);
        if (string.IsNullOrWhiteSpace(localized))
        {
            localized = korean
                ? koreanDescription
                : englishDescription;
        }
        return localized?.Trim() ?? string.Empty;
    }

    public string GetLocalizedDisplayName()
    {
        if (TryResolveCurrentLocale(
                nameLocalizationKey,
                out string localized))
        {
            return localized;
        }

        return GetDisplayName(IsCurrentLocaleKorean());
    }

    public virtual string GetLocalizedDescription()
    {
        if (TryResolveCurrentLocale(
                descriptionLocalizationKey,
                out string localized))
        {
            return localized;
        }

        return GetDescription(IsCurrentLocaleKorean());
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
        nameLocalizationKey =
            nameLocalizationKey?.Trim() ?? string.Empty;
        descriptionLocalizationKey =
            descriptionLocalizationKey?.Trim() ?? string.Empty;
        koreanName = koreanName?.Trim() ?? string.Empty;
        englishName = englishName?.Trim() ?? string.Empty;
        maximumStack = Math.Max(0L, maximumStack);
        initialAmount = ClampAmount(
            Math.Max(0L, initialAmount));
    }

    protected static bool TryResolveCurrentLocale(
        string localizationKey,
        out string localized,
        params LocalizationArgument[] arguments)
    {
        localizationKey = localizationKey?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(localizationKey) ||
            !LocalizationService.TryGet(
                localizationKey,
                out _,
                arguments ?? Array.Empty<LocalizationArgument>()))
        {
            localized = string.Empty;
            return false;
        }

        localized = LocalizationService.Get(
            localizationKey,
            arguments ?? Array.Empty<LocalizationArgument>());
        return !string.IsNullOrWhiteSpace(localized);
    }

    protected static bool IsCurrentLocaleKorean()
    {
        return LocalizationService.CurrentLocale?.StartsWith(
            "ko",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string ResolveForLocale(
        string localizationKey,
        bool korean)
    {
        localizationKey = localizationKey?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(localizationKey))
            return string.Empty;

        string locale = korean ? "ko-KR" : "en-US";
        return GeneratedLocalizationTables.TryGet(
            locale,
            localizationKey,
            out LocalizationEntry entry)
            ? entry.Text
            : string.Empty;
    }
}
