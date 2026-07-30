using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum RecruitRateInputMode
{
    Weight = 0,
    Percentage = 1,
}

[Serializable]
public sealed class RecruitDummyPoolEntry
{
    [SerializeField] private string displayName = "더미 항목";
    [SerializeField] private CharacterGrade grade;
    [SerializeField, Min(0f)] private float rate = 1f;
    [SerializeField] private bool pickup;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? "DUMMY"
        : displayName.Trim();
    public CharacterGrade Grade =>
        CharacterGradePresentation.Clamp(grade);
    public float Rate => Mathf.Max(0f, rate);
    public bool Pickup => pickup;
    public Color DisplayColor =>
        CharacterGradePresentation.GetPrimaryColor(Grade);
}

[Serializable]
public sealed class RecruitPaymentRouteDefinition
{
    [SerializeField] private string routeId = "payment";
    [SerializeField] private ItemDefinitionSO item;
    [SerializeField, Min(0)] private long singleCost = 1L;
    [SerializeField] private bool singleRecruitEnabled = true;
    [SerializeField] private bool tenRecruitEnabled = true;
    [SerializeField] private bool automaticTenCost = true;
    [SerializeField, Min(0)] private long tenCostOverride = 10L;
    [SerializeField] private int priority;

    public string RouteId => string.IsNullOrWhiteSpace(routeId)
        ? "payment"
        : routeId.Trim();
    public ItemDefinitionSO Item => item;
    public long SingleCost => Math.Max(0L, singleCost);
    public bool SingleRecruitEnabled => singleRecruitEnabled;
    public bool TenRecruitEnabled => tenRecruitEnabled;
    public bool AutomaticTenCost => automaticTenCost;
    public long TenCost => automaticTenCost
        ? MultiplyWithoutOverflow(SingleCost, 10L)
        : Math.Max(0L, tenCostOverride);
    public int Priority => priority;

    public bool SupportsDrawCount(int drawCount)
    {
        return drawCount switch
        {
            1 => SingleRecruitEnabled,
            10 => TenRecruitEnabled,
            _ => false,
        };
    }

    public long GetCost(int drawCount)
    {
        return drawCount == 10 ? TenCost : SingleCost;
    }

    private static long MultiplyWithoutOverflow(long value, long multiplier)
    {
        if (value <= 0L || multiplier <= 0L)
            return 0L;
        return value > long.MaxValue / multiplier
            ? long.MaxValue
            : value * multiplier;
    }
}

public readonly struct RecruitPaymentRouteSelection
{
    public RecruitPaymentRouteDefinition Route { get; }
    public ItemDefinitionSO Item => Route?.Item;
    public long Cost { get; }
    public long OwnedAmount { get; }
    public bool IsConfigured =>
        Route != null &&
        Item != null &&
        Cost > 0L;
    public bool CanAfford =>
        IsConfigured && OwnedAmount >= Cost;

    public RecruitPaymentRouteSelection(
        RecruitPaymentRouteDefinition route,
        long cost,
        long ownedAmount)
    {
        Route = route;
        Cost = Math.Max(0L, cost);
        OwnedAmount = Math.Max(0L, ownedAmount);
    }
}

[Serializable]
public sealed class RecruitExecutionResult
{
    private readonly List<RecruitDummyPoolEntry> _entries;

    public int DrawCount => _entries.Count;
    public RecruitPaymentRouteSelection Payment { get; }
    public IReadOnlyList<RecruitDummyPoolEntry> Entries => _entries;

    public RecruitExecutionResult(
        RecruitPaymentRouteSelection payment,
        List<RecruitDummyPoolEntry> entries)
    {
        Payment = payment;
        _entries = entries ?? new List<RecruitDummyPoolEntry>();
    }
}

[Serializable]
public sealed class RecruitProbabilityTable
{
    private readonly double[] _normalizedRates;

    public int Count => _normalizedRates.Length;

    private RecruitProbabilityTable(double[] normalizedRates)
    {
        _normalizedRates = normalizedRates;
    }

    public double GetProbability(int index)
    {
        return index >= 0 && index < _normalizedRates.Length
            ? _normalizedRates[index]
            : 0d;
    }

    public int Sample(double randomValue)
    {
        if (_normalizedRates.Length == 0)
            return -1;

        double cursor = Math.Max(0d, Math.Min(0.9999999999999999d, randomValue));
        double cumulative = 0d;
        for (int index = 0; index < _normalizedRates.Length; index++)
        {
            cumulative += _normalizedRates[index];
            if (cursor < cumulative)
                return index;
        }

        return _normalizedRates.Length - 1;
    }

    public static bool TryCreate(
        IReadOnlyList<RecruitDummyPoolEntry> entries,
        RecruitRateInputMode inputMode,
        out RecruitProbabilityTable table,
        out string error)
    {
        table = null;
        error = string.Empty;
        if (entries == null || entries.Count == 0)
        {
            error = "더미 모집 풀이 비어 있습니다.";
            return false;
        }

        double total = 0d;
        double[] normalized = new double[entries.Count];
        for (int index = 0; index < entries.Count; index++)
        {
            RecruitDummyPoolEntry entry = entries[index];
            if (entry == null)
            {
                error = $"{index + 1}번 더미 항목이 비어 있습니다.";
                return false;
            }

            double rate = entry.Rate;
            if (double.IsNaN(rate) ||
                double.IsInfinity(rate) ||
                rate < 0d)
            {
                error = $"{index + 1}번 더미 항목의 확률 값이 올바르지 않습니다.";
                return false;
            }

            normalized[index] = rate;
            total += rate;
        }

        if (total <= 0d)
        {
            error = "확률 값 중 하나 이상은 0보다 커야 합니다.";
            return false;
        }

        if (inputMode == RecruitRateInputMode.Percentage &&
            Math.Abs(total - 100d) > 0.01d)
        {
            error = $"직접 확률의 합계가 100%가 아닙니다. 현재 {total:0.####}%입니다.";
            return false;
        }

        for (int index = 0; index < normalized.Length; index++)
            normalized[index] /= total;

        table = new RecruitProbabilityTable(normalized);
        return true;
    }
}

[Serializable]
public sealed class RecruitBannerPageDefinition
{
    [SerializeField] private string bannerId = "main";
    [SerializeField] private string ticketGroupId = "standard";
    [SerializeField, HideInInspector] private string koreanTitle = "상시 모집";
    [SerializeField, HideInInspector] private string englishTitle =
        "STANDARD RECRUITMENT";
    [SerializeField, HideInInspector, TextArea] private string koreanDescription =
        "새로운 대원을 모집합니다";
    [SerializeField, HideInInspector, TextArea] private string englishDescription =
        "RECRUIT NEW OPERATORS";
    [SerializeField, HideInInspector] private string koreanPeriod = "상시";
    [SerializeField, HideInInspector] private string englishPeriod = "PERMANENT";
    [Tooltip("모집 배너에 직접 표시할 이미지입니다.")]
    [SerializeField] private Sprite bannerArt;
    [SerializeField, HideInInspector] private Sprite currencyIcon;
    [SerializeField, Min(0)] private int totalRecruitCount;
    [SerializeField, Min(0)] private int currentStack;
    [SerializeField, Min(0)] private int maximumStack;
    [SerializeField, HideInInspector, Min(0)] private int singleCost;
    [SerializeField, HideInInspector, Min(0)] private int tenCost;
    [SerializeField] private RecruitRateInputMode rateInputMode;
    [SerializeField] private List<RecruitDummyPoolEntry> dummyPool = new();
    [SerializeField] private List<RecruitPaymentRouteDefinition>
        paymentRoutes = new();
    [SerializeField, Min(0)] private int defaultPaymentRouteIndex;
    [SerializeField] private bool interactionEnabled = true;

    public string BannerId => string.IsNullOrWhiteSpace(bannerId)
        ? "banner"
        : bannerId.Trim();
    public string TicketGroupId => ticketGroupId?.Trim() ?? string.Empty;
    public RecruitRateInputMode RateInputMode => rateInputMode;
    public IReadOnlyList<RecruitDummyPoolEntry> DummyPool =>
        dummyPool ??= new List<RecruitDummyPoolEntry>();
    public IReadOnlyList<RecruitPaymentRouteDefinition> PaymentRoutes =>
        paymentRoutes ??= new List<RecruitPaymentRouteDefinition>();

    public static RecruitBannerPageDefinition CreateDefault()
    {
        return new RecruitBannerPageDefinition
        {
            bannerId = "main",
            bannerArt = null,
            interactionEnabled = true,
        };
    }

    public RecruitBannerPageModel CreateModel(bool korean)
    {
        return CreateModel(korean, null);
    }

    public RecruitBannerPageModel CreateModel(
        bool korean,
        InventoryData inventory)
    {
        string localizedTitle = korean
            ? koreanTitle
            : englishTitle;
        string localizedDescription = korean
            ? koreanDescription
            : englishDescription;
        string localizedPeriod = korean
            ? koreanPeriod
            : englishPeriod;

        RecruitPaymentRouteSelection singlePayment =
            ResolvePaymentRoute(1, inventory);
        RecruitPaymentRouteSelection tenPayment =
            ResolvePaymentRoute(10, inventory);
        bool singleConfigured = singlePayment.IsConfigured;
        bool tenConfigured = tenPayment.IsConfigured;
        bool validDummyRates = RecruitProbabilityTable.TryCreate(
            DummyPool,
            rateInputMode,
            out _,
            out _);

        return new RecruitBannerPageModel(
            BannerId,
            Fallback(
                localizedTitle,
                korean ? "모집" : "RECRUITMENT"),
            Fallback(
                localizedDescription,
                korean
                    ? "새로운 대원을 모집합니다"
                    : "RECRUIT NEW OPERATORS"),
            Fallback(
                localizedPeriod,
                korean ? "상시" : "PERMANENT"),
            bannerArt,
            singlePayment.Item != null
                ? singlePayment.Item.Icon
                : currencyIcon,
            tenPayment.Item != null
                ? tenPayment.Item.Icon
                : currencyIcon,
            Mathf.Max(0, totalRecruitCount),
            Mathf.Max(0, currentStack),
            Mathf.Max(0, maximumStack),
            singleConfigured
                ? singlePayment.Cost
                : Mathf.Max(0, singleCost),
            tenConfigured
                ? tenPayment.Cost
                : Mathf.Max(0, tenCost),
            singlePayment.OwnedAmount,
            tenPayment.OwnedAmount,
            singlePayment.Item != null
                ? singlePayment.Item.GetDisplayName(korean)
                : string.Empty,
            tenPayment.Item != null
                ? tenPayment.Item.GetDisplayName(korean)
                : string.Empty,
            validDummyRates &&
            singleConfigured &&
            (inventory == null || singlePayment.CanAfford),
            validDummyRates &&
            tenConfigured &&
            (inventory == null || tenPayment.CanAfford),
            DummyPool.Count,
            validDummyRates,
            PaymentRoutes.Count,
            interactionEnabled,
            korean);
    }

    public RecruitPaymentRouteSelection ResolvePaymentRoute(
        int drawCount,
        InventoryData inventory)
    {
        if (paymentRoutes == null || paymentRoutes.Count == 0)
            return default;

        int defaultIndex = Mathf.Clamp(
            defaultPaymentRouteIndex,
            0,
            paymentRoutes.Count - 1);
        RecruitPaymentRouteDefinition best = null;
        long bestOwnedAmount = 0L;
        int bestAvailability = int.MaxValue;
        int bestPriority = int.MaxValue;
        int bestDefaultRank = int.MaxValue;
        for (int index = 0; index < paymentRoutes.Count; index++)
        {
            RecruitPaymentRouteDefinition route = paymentRoutes[index];
            if (!IsRouteConfigured(route, drawCount))
                continue;

            long cost = route.GetCost(drawCount);
            long ownedAmount = inventory != null
                ? inventory.GetAmount(route.Item)
                : 0L;
            int availability = inventory == null
                ? 2
                : ownedAmount >= cost
                    ? 0
                    : ownedAmount > 0L
                        ? 1
                        : 2;
            int defaultRank = index == defaultIndex ? 0 : 1;

            if (best == null ||
                availability < bestAvailability ||
                availability == bestAvailability &&
                route.Priority < bestPriority ||
                availability == bestAvailability &&
                route.Priority == bestPriority &&
                defaultRank < bestDefaultRank)
            {
                best = route;
                bestOwnedAmount = ownedAmount;
                bestAvailability = availability;
                bestPriority = route.Priority;
                bestDefaultRank = defaultRank;
            }
        }

        return best != null
            ? new RecruitPaymentRouteSelection(
                best,
                best.GetCost(drawCount),
                bestOwnedAmount)
            : default;
    }

    public bool TryRecruit(
        InventoryData inventory,
        int drawCount,
        bool korean,
        out RecruitExecutionResult result,
        out string error)
    {
        result = null;
        error = string.Empty;

        if (!interactionEnabled)
        {
            error = korean
                ? "현재 모집을 이용할 수 없습니다."
                : "RECRUITMENT IS CURRENTLY UNAVAILABLE.";
            return false;
        }

        if (drawCount != 1 && drawCount != 10)
        {
            error = korean
                ? "지원하지 않는 모집 횟수입니다."
                : "UNSUPPORTED RECRUIT COUNT.";
            return false;
        }

        if (inventory == null)
        {
            error = korean
                ? "인벤토리 데이터를 불러오지 못했습니다."
                : "INVENTORY DATA IS UNAVAILABLE.";
            return false;
        }

        if (!RecruitProbabilityTable.TryCreate(
                DummyPool,
                rateInputMode,
                out RecruitProbabilityTable table,
                out string probabilityError))
        {
            error = korean
                ? probabilityError
                : "CHECK THE DUMMY POOL RATES.";
            return false;
        }

        RecruitPaymentRouteSelection payment =
            ResolvePaymentRoute(drawCount, inventory);
        if (!payment.IsConfigured)
        {
            error = korean
                ? "사용 가능한 모집 재화가 설정되지 않았습니다."
                : "NO RECRUIT PAYMENT ROUTE IS CONFIGURED.";
            return false;
        }

        if (!payment.CanAfford)
        {
            string itemName =
                payment.Item.GetDisplayName(korean);
            error = korean
                ? $"{itemName}이 부족합니다. 보유 {payment.OwnedAmount:N0} / 필요 {payment.Cost:N0}"
                : $"NOT ENOUGH {itemName}. OWNED {payment.OwnedAmount:N0} / REQUIRED {payment.Cost:N0}";
            return false;
        }

        if (!inventory.TrySpend(
                payment.Item.ItemId,
                payment.Cost,
                true))
        {
            error = korean
                ? "모집 재화 차감에 실패했습니다."
                : "FAILED TO SPEND RECRUIT CURRENCY.";
            return false;
        }

        List<RecruitDummyPoolEntry> entries = new(drawCount);
        for (int draw = 0; draw < drawCount; draw++)
        {
            int selectedIndex = table.Sample(UnityEngine.Random.value);
            if (selectedIndex >= 0 &&
                selectedIndex < DummyPool.Count)
            {
                entries.Add(DummyPool[selectedIndex]);
            }
        }

        totalRecruitCount = AddWithoutOverflow(
            totalRecruitCount,
            drawCount);
        result = new RecruitExecutionResult(payment, entries);
        return true;
    }

    private static bool IsRouteConfigured(
        RecruitPaymentRouteDefinition route,
        int drawCount)
    {
        return route != null &&
               route.Item != null &&
               route.SupportsDrawCount(drawCount) &&
               route.GetCost(drawCount) > 0L;
    }

    private static int AddWithoutOverflow(int value, int addition)
    {
        if (value < 0)
            value = 0;
        if (addition <= 0)
            return value;
        return value > int.MaxValue - addition
            ? int.MaxValue
            : value + addition;
    }

    private static string Fallback(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }
}

public readonly struct RecruitBannerPageModel
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string Period { get; }
    public Sprite BannerArt { get; }
    public Sprite SingleCurrencyIcon { get; }
    public Sprite TenCurrencyIcon { get; }
    public int TotalRecruitCount { get; }
    public int CurrentStack { get; }
    public int MaximumStack { get; }
    public long SingleCost { get; }
    public long TenCost { get; }
    public long SingleOwnedAmount { get; }
    public long TenOwnedAmount { get; }
    public string SinglePaymentName { get; }
    public string TenPaymentName { get; }
    public bool SingleRecruitEnabled { get; }
    public bool TenRecruitEnabled { get; }
    public int DummyPoolCount { get; }
    public bool HasValidDummyRates { get; }
    public int PaymentRouteCount { get; }
    public bool InteractionEnabled { get; }
    public bool IsKorean { get; }

    public RecruitBannerPageModel(
        string id,
        string title,
        string description,
        string period,
        Sprite bannerArt,
        Sprite singleCurrencyIcon,
        Sprite tenCurrencyIcon,
        int totalRecruitCount,
        int currentStack,
        int maximumStack,
        long singleCost,
        long tenCost,
        long singleOwnedAmount,
        long tenOwnedAmount,
        string singlePaymentName,
        string tenPaymentName,
        bool singleRecruitEnabled,
        bool tenRecruitEnabled,
        int dummyPoolCount,
        bool hasValidDummyRates,
        int paymentRouteCount,
        bool interactionEnabled,
        bool isKorean)
    {
        Id = id ?? string.Empty;
        Title = title ?? string.Empty;
        Description = description ?? string.Empty;
        Period = period ?? string.Empty;
        BannerArt = bannerArt;
        SingleCurrencyIcon = singleCurrencyIcon;
        TenCurrencyIcon = tenCurrencyIcon;
        TotalRecruitCount = Mathf.Max(0, totalRecruitCount);
        CurrentStack = Mathf.Max(0, currentStack);
        MaximumStack = Mathf.Max(0, maximumStack);
        SingleCost = Math.Max(0L, singleCost);
        TenCost = Math.Max(0L, tenCost);
        SingleOwnedAmount = Math.Max(0L, singleOwnedAmount);
        TenOwnedAmount = Math.Max(0L, tenOwnedAmount);
        SinglePaymentName = singlePaymentName ?? string.Empty;
        TenPaymentName = tenPaymentName ?? string.Empty;
        SingleRecruitEnabled = singleRecruitEnabled;
        TenRecruitEnabled = tenRecruitEnabled;
        DummyPoolCount = Mathf.Max(0, dummyPoolCount);
        HasValidDummyRates = hasValidDummyRates;
        PaymentRouteCount = Mathf.Max(0, paymentRouteCount);
        InteractionEnabled = interactionEnabled;
        IsKorean = isKorean;
    }
}

public sealed class RecruitBannerView
{
    private const string RootName = "grpRecruitBannerView";
    public const float BannerArtAspectRatio = 1.41421356f;

    private static readonly Color BackdropColor =
        new(0.025f, 0.035f, 0.034f, 1f);
    private static readonly Color HeaderColor =
        new(0.055f, 0.075f, 0.071f, 0.99f);
    private static readonly Color BannerColor =
        new(0.055f, 0.085f, 0.078f, 1f);
    private static readonly Color BottomStripColor =
        new(0.022f, 0.032f, 0.031f, 0.98f);
    private static readonly Color CardColor =
        new(0.075f, 0.105f, 0.098f, 1f);
    private static readonly Color AccentColor =
        new(0.25f, 0.76f, 0.68f, 1f);
    private static readonly Color PaidAccentColor =
        new(0.90f, 0.68f, 0.30f, 1f);
    private static readonly Color PrimaryButtonColor =
        new(0.20f, 0.58f, 0.51f, 1f);
    private static readonly Color SecondaryButtonColor =
        new(0.12f, 0.28f, 0.25f, 1f);
    private static readonly Color TextColor =
        new(0.92f, 0.95f, 0.91f, 1f);
    private static readonly Color MutedTextColor =
        new(0.56f, 0.64f, 0.60f, 1f);

    private readonly Transform _host;
    private readonly List<RecruitBannerPageModel> _pages = new();

    private RecruitBannerDesignerBindings _designerBindings;
    private RectTransform _root;
    private TextMeshProUGUI _headerTitle;
    private TextMeshProUGUI _freeCurrencyLabel;
    private TextMeshProUGUI _paidCurrencyLabel;
    private TextMeshProUGUI _freeCurrencyValue;
    private TextMeshProUGUI _paidCurrencyValue;
    private RectTransform _bannerArtViewport;
    private Image _bannerArt;
    private TextMeshProUGUI _bannerArtFallback;
    private TextMeshProUGUI _pagePosition;
    private TextMeshProUGUI _totalRecruitCount;
    private TextMeshProUGUI _stackCount;
    private TextMeshProUGUI _singleRecruitLabel;
    private TextMeshProUGUI _singleRecruitCost;
    private TextMeshProUGUI _tenRecruitLabel;
    private TextMeshProUGUI _tenRecruitCost;
    private TextMeshProUGUI _statusMessage;
    private Image _singleCurrencyIcon;
    private Image _tenCurrencyIcon;
    private Button _previousButton;
    private Button _nextButton;
    private Button _singleRecruitButton;
    private Button _tenRecruitButton;
    private int _currentPageIndex;
    private Action<int, string> _recruitRequested;
    private bool _interactionLocked;

    private RecruitBannerView(Transform host)
    {
        _host = host;
    }

    public static RecruitBannerView Build(Transform host)
    {
        RecruitBannerView view = new(host);
        if (!view.TryBindLayout())
        {
            RecruitBannerDesignerBindings existingBindings =
                host != null
                    ? host.Find(RootName)
                        ?.GetComponent<RecruitBannerDesignerBindings>()
                    : null;
            if (existingBindings != null &&
                existingBindings.HasDesignerLayout)
            {
                throw new InvalidOperationException(
                    "The designer-owned recruit banner has missing UI " +
                    "references. Repair its bindings instead of rebuilding " +
                    "the scene layout.");
            }

            view.BuildLayout();
            if (!view.TryBindLayout())
            {
                throw new InvalidOperationException(
                    "Failed to build the recruit banner layout.");
            }
        }

        view.WireButtons();
        bool designerOwned =
            view._designerBindings != null &&
            view._designerBindings.HasDesignerLayout;
        if (!designerOwned)
            view.ApplyStaticLayout();
        view._root.gameObject.SetActive(true);
        if (!designerOwned)
            view._root.SetAsLastSibling();
        return view;
    }

    public void SetRecruitRequested(
        Action<int, string> recruitRequested)
    {
        _recruitRequested = recruitRequested;
    }

    public void SetInteractionLocked(bool locked)
    {
        if (_interactionLocked == locked)
            return;
        _interactionLocked = locked;
        RefreshCurrentPage();
    }

    public void SetHeader(
        string title,
        string freeCurrencyLabel,
        string paidCurrencyLabel)
    {
        _headerTitle.text = title ?? string.Empty;
        _freeCurrencyLabel.text =
            freeCurrencyLabel ?? string.Empty;
        _paidCurrencyLabel.text =
            paidCurrencyLabel ?? string.Empty;
        _freeCurrencyValue.text = "--";
        _paidCurrencyValue.text = "--";
    }

    public void SetCurrencyAmounts(
        long freeCurrencyAmount,
        long paidCurrencyAmount)
    {
        _freeCurrencyValue.text =
            Math.Max(0L, freeCurrencyAmount).ToString("N0");
        _paidCurrencyValue.text =
            Math.Max(0L, paidCurrencyAmount).ToString("N0");
    }

    public void SetPages(
        IReadOnlyList<RecruitBannerPageModel> pages)
    {
        string currentId =
            _currentPageIndex >= 0 &&
            _currentPageIndex < _pages.Count
                ? _pages[_currentPageIndex].Id
                : string.Empty;

        _pages.Clear();
        if (pages != null)
        {
            for (int index = 0; index < pages.Count; index++)
                _pages.Add(pages[index]);
        }

        int preservedIndex = -1;
        if (!string.IsNullOrWhiteSpace(currentId))
        {
            preservedIndex = _pages.FindIndex(page =>
                string.Equals(
                    page.Id,
                    currentId,
                    StringComparison.Ordinal));
        }

        _currentPageIndex = preservedIndex >= 0
            ? preservedIndex
            : 0;
        RefreshCurrentPage();
    }

    public void SetPreviewPageIndex(int pageIndex)
    {
        if (_pages.Count == 0)
            return;
        _currentPageIndex = Mathf.Clamp(
            pageIndex,
            0,
            _pages.Count - 1);
        RefreshCurrentPage();
    }

#if UNITY_EDITOR
    public bool CaptureDesignerLayout()
    {
        if (_root == null)
            return false;
        _designerBindings ??=
            _root.GetComponent<RecruitBannerDesignerBindings>();
        if (_designerBindings == null)
        {
            _designerBindings =
                UnityEditor.Undo.AddComponent<
                    RecruitBannerDesignerBindings>(
                    _root.gameObject);
        }

        if (!_designerBindings.HasRequiredReferences &&
            !_designerBindings.CaptureReferencesFromHierarchy())
        {
            return false;
        }
        _designerBindings.MarkDesignerLayoutCurrent();
        UnityEditor.EditorUtility.SetDirty(_root.gameObject);
        return true;
    }
#endif

    public void ShowStatusMessage(string message)
    {
        _statusMessage.text = message ?? string.Empty;
    }

    private void CyclePage(int direction)
    {
        if (_pages.Count <= 1 || direction == 0)
            return;

        _currentPageIndex =
            (_currentPageIndex + direction) % _pages.Count;
        if (_currentPageIndex < 0)
            _currentPageIndex += _pages.Count;
        RefreshCurrentPage();
    }

    private void RequestRecruit(int count)
    {
        if (_pages.Count == 0 ||
            _currentPageIndex < 0 ||
            _currentPageIndex >= _pages.Count)
        {
            return;
        }

        RecruitBannerPageModel page =
            _pages[_currentPageIndex];
        bool drawEnabled = count switch
        {
            1 => page.SingleRecruitEnabled,
            10 => page.TenRecruitEnabled,
            _ => false,
        };
        if (_interactionLocked ||
            !page.InteractionEnabled ||
            !drawEnabled)
            return;
        _recruitRequested?.Invoke(count, page.Id);
    }

    private void RefreshCurrentPage()
    {
        if (_pages.Count == 0)
        {
            ShowEmpty();
            return;
        }

        _currentPageIndex = Mathf.Clamp(
            _currentPageIndex,
            0,
            _pages.Count - 1);
        RecruitBannerPageModel page =
            _pages[_currentPageIndex];

        _bannerArt.sprite = page.BannerArt;
        _bannerArt.enabled = page.BannerArt != null;
        _bannerArtFallback.gameObject.SetActive(
            page.BannerArt == null);
        _pagePosition.text =
            $"{_currentPageIndex + 1:00} / {_pages.Count:00}";
        _totalRecruitCount.text = page.IsKorean
            ? $"누적 모집\n{page.TotalRecruitCount:N0}회"
            : $"TOTAL RECRUITS\n{page.TotalRecruitCount:N0}";
        _stackCount.text = page.IsKorean
            ? $"스택\n{FormatStack(page)}"
            : $"STACK\n{FormatStack(page)}";
        _singleRecruitLabel.text = page.IsKorean
            ? "1회 모집"
            : "RECRUIT x1";
        _tenRecruitLabel.text = page.IsKorean
            ? "10회 모집"
            : "RECRUIT x10";
        _singleRecruitCost.text = FormatCost(
            page.SinglePaymentName,
            page.SingleCost,
            page.SingleOwnedAmount,
            page.IsKorean);
        _tenRecruitCost.text = FormatCost(
            page.TenPaymentName,
            page.TenCost,
            page.TenOwnedAmount,
            page.IsKorean);
        BindCurrencyIcon(
            _singleCurrencyIcon,
            page.SingleCurrencyIcon);
        BindCurrencyIcon(
            _tenCurrencyIcon,
            page.TenCurrencyIcon);
        _singleRecruitButton.interactable =
            !_interactionLocked &&
            page.InteractionEnabled &&
            page.SingleRecruitEnabled;
        _tenRecruitButton.interactable =
            !_interactionLocked &&
            page.InteractionEnabled &&
            page.TenRecruitEnabled;
        _statusMessage.text = GetConfigurationStatus(page);

        bool multiplePages = _pages.Count > 1;
        _previousButton.gameObject.SetActive(multiplePages);
        _nextButton.gameObject.SetActive(multiplePages);
        _previousButton.interactable =
            multiplePages && !_interactionLocked;
        _nextButton.interactable =
            multiplePages && !_interactionLocked;
    }

    private void ShowEmpty()
    {
        _bannerArt.sprite = null;
        _bannerArt.enabled = false;
        _bannerArtFallback.gameObject.SetActive(true);
        _pagePosition.text = "00 / 00";
        _totalRecruitCount.text = "--";
        _stackCount.text = "--";
        _singleRecruitLabel.text = "RECRUIT x1";
        _tenRecruitLabel.text = "RECRUIT x10";
        _singleRecruitCost.text = "--";
        _tenRecruitCost.text = "--";
        _statusMessage.text = string.Empty;
        _singleRecruitButton.interactable = false;
        _tenRecruitButton.interactable = false;
        _previousButton.gameObject.SetActive(false);
        _nextButton.gameObject.SetActive(false);
    }

    private static string FormatStack(
        RecruitBannerPageModel page)
    {
        return page.MaximumStack > 0
            ? $"{page.CurrentStack:N0} / {page.MaximumStack:N0}"
            : $"{page.CurrentStack:N0} / --";
    }

    private static string FormatCost(
        string paymentName,
        long cost,
        long ownedAmount,
        bool korean)
    {
        if (cost <= 0L)
            return "COST  --";

        string name = string.IsNullOrWhiteSpace(paymentName)
            ? (korean ? "재화" : "CURRENCY")
            : paymentName.Trim();
        return korean
            ? $"{name}  {cost:N0} · 보유 {ownedAmount:N0}"
            : $"{name}  {cost:N0} · OWN {ownedAmount:N0}";
    }

    private static string GetConfigurationStatus(
        RecruitBannerPageModel page)
    {
        if (page.DummyPoolCount == 0)
        {
            return page.IsKorean
                ? "더미 모집 풀을 에디터에서 설정해 주세요"
                : "CONFIGURE THE DUMMY POOL IN THE EDITOR";
        }

        if (!page.HasValidDummyRates)
        {
            return page.IsKorean
                ? "더미 모집 풀의 확률 설정을 확인해 주세요"
                : "CHECK THE DUMMY POOL RATES";
        }

        if (page.PaymentRouteCount == 0)
        {
            return page.IsKorean
                ? "모집 재화 결제 경로를 설정해 주세요"
                : "CONFIGURE A RECRUIT PAYMENT ROUTE";
        }

        if (page.SingleCost <= 0L &&
            page.TenCost <= 0L)
        {
            return page.IsKorean
                ? "모집 재화 설정을 확인해 주세요"
                : "CHECK THE RECRUIT PAYMENT SETTINGS";
        }

        if (!page.SingleRecruitEnabled &&
            !page.TenRecruitEnabled)
        {
            return page.IsKorean
                ? "모집에 필요한 재화가 부족합니다"
                : "NOT ENOUGH CURRENCY TO RECRUIT";
        }

        return page.IsKorean
            ? $"더미 {page.DummyPoolCount}종 · 재화 차감 및 추첨 가능"
            : $"{page.DummyPoolCount} DUMMY ENTRIES · DRAW READY";
    }

    private static void BindCurrencyIcon(
        Image target,
        Sprite sprite)
    {
        target.sprite = sprite;
        target.enabled = sprite != null;
    }

    private void WireButtons()
    {
        _previousButton.onClick.RemoveAllListeners();
        _previousButton.onClick.AddListener(
            () => CyclePage(-1));
        _nextButton.onClick.RemoveAllListeners();
        _nextButton.onClick.AddListener(
            () => CyclePage(1));
        _singleRecruitButton.onClick.RemoveAllListeners();
        _singleRecruitButton.onClick.AddListener(
            () => RequestRecruit(1));
        _tenRecruitButton.onClick.RemoveAllListeners();
        _tenRecruitButton.onClick.AddListener(
            () => RequestRecruit(10));
    }

    private bool TryBindLayout()
    {
        if (_host == null)
            return false;

        Transform root = _host.Find(RootName);
        _designerBindings =
            root?.GetComponent<RecruitBannerDesignerBindings>();
        if (_designerBindings != null)
        {
            if (!_designerBindings.HasRequiredReferences)
                _designerBindings.CaptureReferencesFromHierarchy();
            if (_designerBindings.HasRequiredReferences)
            {
                BindFromDesignerBindings(_designerBindings);
                return true;
            }
        }

        Transform header = root?.Find("grpRecruitHeader");
        Transform banner = root?.Find("grpRecruitMainBanner");
        Transform artViewport =
            banner?.Find("grpRecruitBannerArtViewport");
        Transform art = artViewport?.Find("imgRecruitBannerArt") ??
                        banner?.Find("imgRecruitBannerArt");
        Transform artFallback =
            artViewport?.Find("txtRecruitBannerArtFallback") ??
            banner?.Find("txtRecruitBannerArtFallback");
        Transform bottom = banner?.Find("grpRecruitBottomStrip");
        Transform total = bottom?.Find("grpRecruitTotalCount");
        Transform stack = bottom?.Find("grpRecruitStack");
        Transform single = bottom?.Find("btnRecruitSingle");
        Transform ten = bottom?.Find("btnRecruitTen");
        Transform free =
            header?.Find("grpRecruitFreeCurrency");
        Transform paid =
            header?.Find("grpRecruitPaidCurrency");

        _root = root as RectTransform;
        _headerTitle = header?.Find("txtRecruitHeaderTitle")
            ?.GetComponent<TextMeshProUGUI>();
        _freeCurrencyLabel = free?.Find("txtCurrencyLabel")
            ?.GetComponent<TextMeshProUGUI>();
        _paidCurrencyLabel = paid?.Find("txtCurrencyLabel")
            ?.GetComponent<TextMeshProUGUI>();
        _freeCurrencyValue = free?.Find("txtCurrencyValue")
            ?.GetComponent<TextMeshProUGUI>();
        _paidCurrencyValue = paid?.Find("txtCurrencyValue")
            ?.GetComponent<TextMeshProUGUI>();
        _bannerArtViewport = artViewport as RectTransform;
        _bannerArt = art?.GetComponent<Image>();
        _bannerArtFallback = artFallback
            ?.GetComponent<TextMeshProUGUI>();
        _pagePosition = banner?.Find("txtRecruitPagePosition")
            ?.GetComponent<TextMeshProUGUI>();
        _previousButton = banner?.Find("btnRecruitPrevious")
            ?.GetComponent<Button>();
        _nextButton = banner?.Find("btnRecruitNext")
            ?.GetComponent<Button>();
        _totalRecruitCount = total?.Find("txtValue")
            ?.GetComponent<TextMeshProUGUI>();
        _stackCount = stack?.Find("txtValue")
            ?.GetComponent<TextMeshProUGUI>();
        _singleRecruitButton = single?.GetComponent<Button>();
        _singleRecruitLabel = single?.Find("txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        _singleRecruitCost = single?.Find("txtCost")
            ?.GetComponent<TextMeshProUGUI>();
        _singleCurrencyIcon = single?.Find("imgCostIcon")
            ?.GetComponent<Image>();
        _tenRecruitButton = ten?.GetComponent<Button>();
        _tenRecruitLabel = ten?.Find("txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        _tenRecruitCost = ten?.Find("txtCost")
            ?.GetComponent<TextMeshProUGUI>();
        _tenCurrencyIcon = ten?.Find("imgCostIcon")
            ?.GetComponent<Image>();
        _statusMessage = bottom?.Find("txtRecruitStatus")
            ?.GetComponent<TextMeshProUGUI>();

        return _root != null &&
               _headerTitle != null &&
               _freeCurrencyLabel != null &&
               _paidCurrencyLabel != null &&
               _freeCurrencyValue != null &&
               _paidCurrencyValue != null &&
               _bannerArt != null &&
               _bannerArtFallback != null &&
               _pagePosition != null &&
               _previousButton != null &&
               _nextButton != null &&
               _totalRecruitCount != null &&
               _stackCount != null &&
               _singleRecruitButton != null &&
               _singleRecruitLabel != null &&
               _singleRecruitCost != null &&
               _singleCurrencyIcon != null &&
               _tenRecruitButton != null &&
               _tenRecruitLabel != null &&
               _tenRecruitCost != null &&
               _tenCurrencyIcon != null &&
               _statusMessage != null;
    }

    private void BindFromDesignerBindings(
        RecruitBannerDesignerBindings bindings)
    {
        _root = bindings.Root;
        _headerTitle = bindings.HeaderTitle;
        _freeCurrencyLabel = bindings.FreeCurrencyLabel;
        _paidCurrencyLabel = bindings.PaidCurrencyLabel;
        _freeCurrencyValue = bindings.FreeCurrencyValue;
        _paidCurrencyValue = bindings.PaidCurrencyValue;
        _bannerArtViewport = bindings.BannerArtViewport;
        _bannerArt = bindings.BannerArt;
        _bannerArtFallback = bindings.BannerArtFallback;
        _pagePosition = bindings.PagePosition;
        _previousButton = bindings.PreviousButton;
        _nextButton = bindings.NextButton;
        _totalRecruitCount = bindings.TotalRecruitCount;
        _stackCount = bindings.StackCount;
        _singleRecruitButton = bindings.SingleRecruitButton;
        _singleRecruitLabel = bindings.SingleRecruitLabel;
        _singleRecruitCost = bindings.SingleRecruitCost;
        _singleCurrencyIcon = bindings.SingleCurrencyIcon;
        _tenRecruitButton = bindings.TenRecruitButton;
        _tenRecruitLabel = bindings.TenRecruitLabel;
        _tenRecruitCost = bindings.TenRecruitCost;
        _tenCurrencyIcon = bindings.TenCurrencyIcon;
        _statusMessage = bindings.StatusMessage;
    }

    private void BuildLayout()
    {
        GameObject rootObject = GetOrCreateUiObject(
            _host,
            RootName,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(RecruitBannerDesignerBindings));
        RectTransform root = (RectTransform)rootObject.transform;
        Stretch(root);
        Image rootImage = rootObject.GetComponent<Image>();
        rootImage.color = BackdropColor;
        rootImage.raycastTarget = true;

        BuildHeader(root);
        BuildMainBanner(root);
        _designerBindings =
            rootObject.GetComponent<RecruitBannerDesignerBindings>();
        _designerBindings.CaptureReferencesFromHierarchy();
    }

    private void BuildHeader(Transform parent)
    {
        GameObject headerObject = GetOrCreateUiObject(
            parent,
            "grpRecruitHeader",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform header = (RectTransform)headerObject.transform;
        ConfigureTopStretch(header, 112f);
        Image headerImage = headerObject.GetComponent<Image>();
        headerImage.color = HeaderColor;
        headerImage.raycastTarget = false;

        GameObject accentObject = GetOrCreateUiObject(
            header,
            "imgRecruitHeaderAccent",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform accent = (RectTransform)accentObject.transform;
        ConfigureTopLeft(
            accent,
            new Vector2(176f, -24f),
            new Vector2(6f, 64f));
        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.color = AccentColor;
        accentImage.raycastTarget = false;

        TextMeshProUGUI title = CreateText(
            header,
            "txtRecruitHeaderTitle",
            38f,
            TextAlignmentOptions.MidlineLeft,
            TextColor);
        ConfigureTopLeft(
            title.rectTransform,
            new Vector2(200f, -22f),
            new Vector2(520f, 64f));
        title.fontStyle = FontStyles.Bold;

        BuildCurrencySlot(
            header,
            "grpRecruitFreeCurrency",
            -440f,
            AccentColor);
        BuildCurrencySlot(
            header,
            "grpRecruitPaidCurrency",
            -224f,
            PaidAccentColor);
    }

    private void BuildCurrencySlot(
        Transform parent,
        string objectName,
        float right,
        Color accentColor)
    {
        GameObject slotObject = GetOrCreateUiObject(
            parent,
            objectName,
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform slot = (RectTransform)slotObject.transform;
        slot.anchorMin = Vector2.one;
        slot.anchorMax = Vector2.one;
        slot.pivot = Vector2.one;
        slot.anchoredPosition = new Vector2(right, -24f);
        slot.sizeDelta = new Vector2(200f, 64f);
        Image image = slotObject.GetComponent<Image>();
        image.color = CardColor;
        image.raycastTarget = false;

        GameObject iconObject = GetOrCreateUiObject(
            slot,
            "imgCurrencyPlaceholder",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform icon = (RectTransform)iconObject.transform;
        ConfigureTopLeft(
            icon,
            new Vector2(14f, -15f),
            new Vector2(34f, 34f));
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.color = accentColor;
        iconImage.raycastTarget = false;

        TextMeshProUGUI label = CreateText(
            slot,
            "txtCurrencyLabel",
            13f,
            TextAlignmentOptions.TopLeft,
            MutedTextColor);
        ConfigureTopLeft(
            label.rectTransform,
            new Vector2(58f, -8f),
            new Vector2(130f, 24f));

        TextMeshProUGUI value = CreateText(
            slot,
            "txtCurrencyValue",
            22f,
            TextAlignmentOptions.BottomRight,
            TextColor);
        ConfigureTopLeft(
            value.rectTransform,
            new Vector2(58f, -30f),
            new Vector2(130f, 28f));
        value.fontStyle = FontStyles.Bold;
    }

    private void BuildMainBanner(Transform parent)
    {
        GameObject bannerObject = GetOrCreateUiObject(
            parent,
            "grpRecruitMainBanner",
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline));
        RectTransform banner = (RectTransform)bannerObject.transform;
        Stretch(banner);
        banner.offsetMin = new Vector2(28f, 28f);
        banner.offsetMax = new Vector2(-28f, -128f);
        Image bannerImage = bannerObject.GetComponent<Image>();
        bannerImage.color = BannerColor;
        bannerImage.raycastTarget = false;
        Outline outline = bannerObject.GetComponent<Outline>();
        outline.effectColor =
            new Color(0.18f, 0.42f, 0.37f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject viewportObject = GetOrCreateUiObject(
            banner,
            "grpRecruitBannerArtViewport",
            typeof(RectMask2D));
        RectTransform viewport =
            (RectTransform)viewportObject.transform;
        Stretch(viewport);
        viewport.SetAsFirstSibling();

        Transform legacyArt = banner.Find("imgRecruitBannerArt");
        if (legacyArt != null)
            legacyArt.SetParent(viewport, false);
        Transform legacyFallback =
            banner.Find("txtRecruitBannerArtFallback");
        if (legacyFallback != null)
            legacyFallback.SetParent(viewport, false);

        GameObject artObject = GetOrCreateUiObject(
            viewport,
            "imgRecruitBannerArt",
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(AspectRatioFitter));
        RectTransform art = (RectTransform)artObject.transform;
        ConfigureBannerBackgroundArt(artObject);
        Image artImage = artObject.GetComponent<Image>();
        artImage.preserveAspect = true;
        artImage.raycastTarget = false;

        TextMeshProUGUI artFallback = CreateText(
            viewport,
            "txtRecruitBannerArtFallback",
            34f,
            TextAlignmentOptions.Center,
            MutedTextColor);
        Stretch(artFallback.rectTransform);
        artFallback.text = string.Empty;

        BuildPageControls(banner);
        BuildBottomStrip(banner);
    }

    private void BuildPageControls(Transform parent)
    {
        TextMeshProUGUI position = CreateText(
            parent,
            "txtRecruitPagePosition",
            17f,
            TextAlignmentOptions.Center,
            AccentColor);
        position.rectTransform.anchorMin = Vector2.one;
        position.rectTransform.anchorMax = Vector2.one;
        position.rectTransform.pivot = Vector2.one;
        position.rectTransform.anchoredPosition =
            new Vector2(-140f, -18f);
        position.rectTransform.sizeDelta =
            new Vector2(108f, 42f);
        position.fontStyle = FontStyles.Bold;

        BuildNavigationButton(
            parent,
            "btnRecruitPrevious",
            "<",
            new Vector2(-252f, -18f));
        BuildNavigationButton(
            parent,
            "btnRecruitNext",
            ">",
            new Vector2(-28f, -18f));
    }

    private void BuildBottomStrip(Transform parent)
    {
        GameObject stripObject = GetOrCreateUiObject(
            parent,
            "grpRecruitBottomStrip",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform strip = (RectTransform)stripObject.transform;
        strip.anchorMin = Vector2.zero;
        strip.anchorMax = new Vector2(1f, 0f);
        strip.pivot = Vector2.zero;
        strip.anchoredPosition = Vector2.zero;
        strip.sizeDelta = new Vector2(0f, 180f);
        Image stripImage = stripObject.GetComponent<Image>();
        stripImage.color = BottomStripColor;
        stripImage.raycastTarget = false;

        BuildStatusCard(
            strip,
            "grpRecruitTotalCount",
            new Vector2(24f, 26f),
            "#",
            AccentColor);
        BuildStatusCard(
            strip,
            "grpRecruitStack",
            new Vector2(286f, 26f),
            "S",
            PaidAccentColor);

        TextMeshProUGUI status = CreateText(
            strip,
            "txtRecruitStatus",
            15f,
            TextAlignmentOptions.Center,
            MutedTextColor);
        status.rectTransform.anchorMin =
            new Vector2(0.30f, 0f);
        status.rectTransform.anchorMax =
            new Vector2(0.62f, 1f);
        status.rectTransform.offsetMin =
            new Vector2(12f, 24f);
        status.rectTransform.offsetMax =
            new Vector2(-12f, -24f);

        BuildRecruitButton(
            strip,
            "btnRecruitSingle",
            new Vector2(-390f, 28f),
            new Vector2(250f, 118f),
            SecondaryButtonColor);
        BuildRecruitButton(
            strip,
            "btnRecruitTen",
            new Vector2(-24f, 28f),
            new Vector2(350f, 118f),
            PrimaryButtonColor);
    }

    private void BuildStatusCard(
        Transform parent,
        string objectName,
        Vector2 bottomLeftPosition,
        string iconLabel,
        Color accentColor)
    {
        GameObject cardObject = GetOrCreateUiObject(
            parent,
            objectName,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline));
        RectTransform card = (RectTransform)cardObject.transform;
        card.anchorMin = Vector2.zero;
        card.anchorMax = Vector2.zero;
        card.pivot = Vector2.zero;
        card.anchoredPosition = bottomLeftPosition;
        card.sizeDelta = new Vector2(250f, 128f);
        Image cardImage = cardObject.GetComponent<Image>();
        cardImage.color = CardColor;
        cardImage.raycastTarget = false;
        Outline outline = cardObject.GetComponent<Outline>();
        outline.effectColor = new Color(
            accentColor.r,
            accentColor.g,
            accentColor.b,
            0.55f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject iconObject = GetOrCreateUiObject(
            card,
            "grpStatusIcon",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform icon = (RectTransform)iconObject.transform;
        icon.anchorMin = new Vector2(0f, 0.5f);
        icon.anchorMax = new Vector2(0f, 0.5f);
        icon.pivot = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(16f, 0f);
        icon.sizeDelta = new Vector2(62f, 62f);
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.color = accentColor;
        iconImage.raycastTarget = false;

        TextMeshProUGUI iconText = CreateText(
            icon,
            "txtIcon",
            26f,
            TextAlignmentOptions.Center,
            BackdropColor);
        Stretch(iconText.rectTransform);
        iconText.text = iconLabel;
        iconText.fontStyle = FontStyles.Bold;

        TextMeshProUGUI value = CreateText(
            card,
            "txtValue",
            20f,
            TextAlignmentOptions.MidlineLeft,
            TextColor);
        Stretch(value.rectTransform);
        value.rectTransform.offsetMin =
            new Vector2(94f, 14f);
        value.rectTransform.offsetMax =
            new Vector2(-12f, -14f);
        value.fontStyle = FontStyles.Bold;
    }

    private void BuildRecruitButton(
        Transform parent,
        string objectName,
        Vector2 bottomRightPosition,
        Vector2 size,
        Color color)
    {
        GameObject buttonObject = GetOrCreateUiObject(
            parent,
            objectName,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(Outline));
        RectTransform button = (RectTransform)buttonObject.transform;
        button.anchorMin = new Vector2(1f, 0f);
        button.anchorMax = new Vector2(1f, 0f);
        button.pivot = new Vector2(1f, 0f);
        button.anchoredPosition = bottomRightPosition;
        button.sizeDelta = size;
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button buttonComponent = buttonObject.GetComponent<Button>();
        buttonComponent.targetGraphic = image;
        ApplyButtonColors(buttonComponent, color);
        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor =
            new Color(0.55f, 0.91f, 0.82f, 0.7f);
        outline.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI label = CreateText(
            button,
            "txtLabel",
            28f,
            TextAlignmentOptions.MidlineLeft,
            TextColor);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin =
            new Vector2(22f, 44f);
        label.rectTransform.offsetMax =
            new Vector2(-18f, -10f);
        label.fontStyle = FontStyles.Bold;

        GameObject costIconObject = GetOrCreateUiObject(
            button,
            "imgCostIcon",
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform costIcon =
            (RectTransform)costIconObject.transform;
        costIcon.anchorMin = Vector2.zero;
        costIcon.anchorMax = Vector2.zero;
        costIcon.pivot = Vector2.zero;
        costIcon.anchoredPosition = new Vector2(22f, 16f);
        costIcon.sizeDelta = new Vector2(24f, 24f);
        Image costIconImage =
            costIconObject.GetComponent<Image>();
        costIconImage.preserveAspect = true;
        costIconImage.raycastTarget = false;

        TextMeshProUGUI cost = CreateText(
            button,
            "txtCost",
            15f,
            TextAlignmentOptions.BottomLeft,
            TextColor);
        cost.rectTransform.anchorMin = Vector2.zero;
        cost.rectTransform.anchorMax = new Vector2(1f, 0f);
        cost.rectTransform.pivot = Vector2.zero;
        cost.rectTransform.anchoredPosition =
            new Vector2(54f, 14f);
        cost.rectTransform.sizeDelta =
            new Vector2(-72f, 28f);
    }

    private Button BuildNavigationButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 topRightPosition)
    {
        GameObject buttonObject = GetOrCreateUiObject(
            parent,
            objectName,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        RectTransform button = (RectTransform)buttonObject.transform;
        button.anchorMin = Vector2.one;
        button.anchorMax = Vector2.one;
        button.pivot = Vector2.one;
        button.anchoredPosition = topRightPosition;
        button.sizeDelta = new Vector2(84f, 44f);
        Image image = buttonObject.GetComponent<Image>();
        image.color = CardColor;
        Button buttonComponent = buttonObject.GetComponent<Button>();
        buttonComponent.targetGraphic = image;
        ApplyButtonColors(buttonComponent, CardColor);

        TextMeshProUGUI text = CreateText(
            button,
            "txtLabel",
            27f,
            TextAlignmentOptions.Center,
            TextColor);
        Stretch(text.rectTransform);
        text.text = label;
        return buttonComponent;
    }

    private void ApplyStaticLayout()
    {
        Transform header = _root.Find("grpRecruitHeader");
        RectTransform accent = header
            ?.Find("imgRecruitHeaderAccent")
            as RectTransform;
        if (accent != null)
        {
            ConfigureTopLeft(
                accent,
                new Vector2(176f, -24f),
                new Vector2(6f, 64f));
        }

        if (_headerTitle != null)
        {
            ConfigureTopLeft(
                _headerTitle.rectTransform,
                new Vector2(200f, -22f),
                new Vector2(520f, 64f));
        }

        ApplyBannerBackgroundLayout();

        SetButtonLabel(_previousButton, "<");
        SetButtonLabel(_nextButton, ">");
    }

    private void ApplyBannerBackgroundLayout()
    {
        Transform banner = _root?.Find("grpRecruitMainBanner");
        if (banner == null)
            return;

        GameObject viewportObject = GetOrCreateUiObject(
            banner,
            "grpRecruitBannerArtViewport",
            typeof(RectMask2D));
        _bannerArtViewport =
            (RectTransform)viewportObject.transform;
        Stretch(_bannerArtViewport);
        _bannerArtViewport.SetAsFirstSibling();

        if (_bannerArt != null)
        {
            if (_bannerArt.transform.parent != _bannerArtViewport)
                _bannerArt.transform.SetParent(_bannerArtViewport, false);
            ConfigureBannerBackgroundArt(_bannerArt.gameObject);
        }

        if (_bannerArtFallback != null)
        {
            if (_bannerArtFallback.transform.parent != _bannerArtViewport)
            {
                _bannerArtFallback.transform.SetParent(
                    _bannerArtViewport,
                    false);
            }
            Stretch(_bannerArtFallback.rectTransform);
            _bannerArtFallback.transform.SetAsLastSibling();
        }

        Transform obsoleteTextPanel =
            banner.Find("grpRecruitBannerText");
        if (obsoleteTextPanel != null)
            obsoleteTextPanel.gameObject.SetActive(false);
    }

    private static void SetButtonLabel(Button button, string label)
    {
        TextMeshProUGUI text = button != null
            ? button.transform.Find("txtLabel")
                ?.GetComponent<TextMeshProUGUI>()
            : null;
        if (text != null)
            text.text = label ?? string.Empty;
    }

    private static void ApplyButtonColors(
        Button button,
        Color normalColor)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor =
            Color.Lerp(normalColor, Color.white, 0.18f);
        colors.pressedColor =
            Color.Lerp(normalColor, Color.black, 0.22f);
        colors.selectedColor = normalColor;
        colors.disabledColor =
            Color.Lerp(normalColor, Color.black, 0.48f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static GameObject GetOrCreateUiObject(
        Transform parent,
        string objectName,
        params Type[] componentTypes)
    {
        Transform existing = parent != null
            ? parent.Find(objectName)
            : null;
        GameObject target;
        if (existing != null)
        {
            target = existing.gameObject;
        }
        else
        {
            target = new GameObject(
                objectName,
                typeof(RectTransform));
            target.layer = parent != null
                ? parent.gameObject.layer
                : 0;
            target.transform.SetParent(parent, false);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(
                    target,
                    "Create Recruit Designer UI");
            }
#endif
        }

        if (componentTypes != null)
        {
            foreach (Type componentType in componentTypes)
            {
                if (componentType != null &&
                    target.GetComponent(componentType) == null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        UnityEditor.Undo.AddComponent(
                            target,
                            componentType);
                    }
                    else
                    {
                        target.AddComponent(componentType);
                    }
#else
                    target.AddComponent(componentType);
#endif
                }
            }
        }

        return target;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = GetOrCreateUiObject(
            parent,
            objectName,
            typeof(TextMeshProUGUI));
        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();
        LocalizationFontResolver.ApplyGameDefault(text);
        text.fontSize = fontSize;
        text.fontSizeMin = Mathf.Max(11f, fontSize - 10f);
        text.fontSizeMax = fontSize;
        text.enableAutoSizing = true;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void ConfigureTopStretch(
        RectTransform rect,
        float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, height);
    }

    private static void ConfigureTopLeft(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void ConfigureBannerBackgroundArt(GameObject artObject)
    {
        if (artObject == null)
            return;

        RectTransform rect = artObject.transform as RectTransform;
        if (rect == null)
            return;
        Stretch(rect);

        AspectRatioFitter fitter =
            artObject.GetComponent<AspectRatioFitter>();
        if (fitter == null)
            fitter = artObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode =
            AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = BannerArtAspectRatio;
    }
}
