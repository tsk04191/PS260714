using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RecruitBannerDesignerBindings : MonoBehaviour
{
    [Header("Fixed Scene UI")]
    [SerializeField] private RectTransform root;
    [SerializeField] private TextMeshProUGUI headerTitle;
    [SerializeField] private TextMeshProUGUI freeCurrencyLabel;
    [SerializeField] private TextMeshProUGUI paidCurrencyLabel;
    [SerializeField] private TextMeshProUGUI freeCurrencyValue;
    [SerializeField] private TextMeshProUGUI paidCurrencyValue;
    [SerializeField] private RectTransform bannerArtViewport;
    [SerializeField] private Image bannerArt;
    [SerializeField] private TextMeshProUGUI bannerArtFallback;
    [SerializeField] private TextMeshProUGUI pagePosition;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI totalRecruitCount;
    [SerializeField] private TextMeshProUGUI stackCount;
    [SerializeField] private Button singleRecruitButton;
    [SerializeField] private TextMeshProUGUI singleRecruitLabel;
    [SerializeField] private TextMeshProUGUI singleRecruitCost;
    [SerializeField] private Image singleCurrencyIcon;
    [SerializeField] private Button tenRecruitButton;
    [SerializeField] private TextMeshProUGUI tenRecruitLabel;
    [SerializeField] private TextMeshProUGUI tenRecruitCost;
    [SerializeField] private Image tenCurrencyIcon;
    [SerializeField] private TextMeshProUGUI statusMessage;
    [SerializeField, HideInInspector] private int designerLayoutVersion;

    public RectTransform Root => root;
    public TextMeshProUGUI HeaderTitle => headerTitle;
    public TextMeshProUGUI FreeCurrencyLabel => freeCurrencyLabel;
    public TextMeshProUGUI PaidCurrencyLabel => paidCurrencyLabel;
    public TextMeshProUGUI FreeCurrencyValue => freeCurrencyValue;
    public TextMeshProUGUI PaidCurrencyValue => paidCurrencyValue;
    public RectTransform BannerArtViewport => bannerArtViewport;
    public Image BannerArt => bannerArt;
    public TextMeshProUGUI BannerArtFallback => bannerArtFallback;
    public TextMeshProUGUI PagePosition => pagePosition;
    public Button PreviousButton => previousButton;
    public Button NextButton => nextButton;
    public TextMeshProUGUI TotalRecruitCount => totalRecruitCount;
    public TextMeshProUGUI StackCount => stackCount;
    public Button SingleRecruitButton => singleRecruitButton;
    public TextMeshProUGUI SingleRecruitLabel => singleRecruitLabel;
    public TextMeshProUGUI SingleRecruitCost => singleRecruitCost;
    public Image SingleCurrencyIcon => singleCurrencyIcon;
    public Button TenRecruitButton => tenRecruitButton;
    public TextMeshProUGUI TenRecruitLabel => tenRecruitLabel;
    public TextMeshProUGUI TenRecruitCost => tenRecruitCost;
    public Image TenCurrencyIcon => tenCurrencyIcon;
    public TextMeshProUGUI StatusMessage => statusMessage;
    public bool HasDesignerLayout => designerLayoutVersion > 0;

    public bool HasRequiredReferences =>
        root != null &&
        headerTitle != null &&
        freeCurrencyLabel != null &&
        paidCurrencyLabel != null &&
        freeCurrencyValue != null &&
        paidCurrencyValue != null &&
        bannerArtViewport != null &&
        bannerArt != null &&
        bannerArtFallback != null &&
        pagePosition != null &&
        previousButton != null &&
        nextButton != null &&
        totalRecruitCount != null &&
        stackCount != null &&
        singleRecruitButton != null &&
        singleRecruitLabel != null &&
        singleRecruitCost != null &&
        singleCurrencyIcon != null &&
        tenRecruitButton != null &&
        tenRecruitLabel != null &&
        tenRecruitCost != null &&
        tenCurrencyIcon != null &&
        statusMessage != null;

    public bool CaptureReferencesFromHierarchy()
    {
        root = transform as RectTransform;
        Transform header = transform.Find("grpRecruitHeader");
        Transform banner = transform.Find("grpRecruitMainBanner");
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
        Transform free = header?.Find("grpRecruitFreeCurrency");
        Transform paid = header?.Find("grpRecruitPaidCurrency");

        headerTitle = header?.Find("txtRecruitHeaderTitle")
            ?.GetComponent<TextMeshProUGUI>();
        freeCurrencyLabel = free?.Find("txtCurrencyLabel")
            ?.GetComponent<TextMeshProUGUI>();
        paidCurrencyLabel = paid?.Find("txtCurrencyLabel")
            ?.GetComponent<TextMeshProUGUI>();
        freeCurrencyValue = free?.Find("txtCurrencyValue")
            ?.GetComponent<TextMeshProUGUI>();
        paidCurrencyValue = paid?.Find("txtCurrencyValue")
            ?.GetComponent<TextMeshProUGUI>();
        bannerArtViewport = artViewport as RectTransform;
        bannerArt = art?.GetComponent<Image>();
        bannerArtFallback = artFallback
            ?.GetComponent<TextMeshProUGUI>();
        pagePosition = banner?.Find("txtRecruitPagePosition")
            ?.GetComponent<TextMeshProUGUI>();
        previousButton = banner?.Find("btnRecruitPrevious")
            ?.GetComponent<Button>();
        nextButton = banner?.Find("btnRecruitNext")
            ?.GetComponent<Button>();
        totalRecruitCount = total?.Find("txtValue")
            ?.GetComponent<TextMeshProUGUI>();
        stackCount = stack?.Find("txtValue")
            ?.GetComponent<TextMeshProUGUI>();
        singleRecruitButton = single?.GetComponent<Button>();
        singleRecruitLabel = single?.Find("txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        singleRecruitCost = single?.Find("txtCost")
            ?.GetComponent<TextMeshProUGUI>();
        singleCurrencyIcon = single?.Find("imgCostIcon")
            ?.GetComponent<Image>();
        tenRecruitButton = ten?.GetComponent<Button>();
        tenRecruitLabel = ten?.Find("txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        tenRecruitCost = ten?.Find("txtCost")
            ?.GetComponent<TextMeshProUGUI>();
        tenCurrencyIcon = ten?.Find("imgCostIcon")
            ?.GetComponent<Image>();
        statusMessage = bottom?.Find("txtRecruitStatus")
            ?.GetComponent<TextMeshProUGUI>();
        return HasRequiredReferences;
    }

#if UNITY_EDITOR
    public void MarkDesignerLayoutCurrent()
    {
        designerLayoutVersion = 1;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
