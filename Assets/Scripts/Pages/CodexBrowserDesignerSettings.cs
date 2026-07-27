using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class CodexBrowserDesignerSettings : MonoBehaviour
{
    [Header("Designer-owned references")]
    [SerializeField] private RectTransform listPanel;
    [SerializeField] private RectTransform toolbar;
    [SerializeField] private RectTransform cardContent;
    [SerializeField] private RectTransform detailRoot;
    [SerializeField] private GridLayoutGroup cardGrid;
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Button searchButton;
    [SerializeField] private Button filterButton;
    [SerializeField] private Button sortButton;
    [SerializeField] private GameObject cardTemplate;

    [Header("Card presentation")]
    [SerializeField] private Color ownedCardColor =
        new(0.16f, 0.235f, 0.19f, 1f);
    [SerializeField] private Color ownedTextColor =
        new(0.94f, 0.91f, 0.78f, 1f);
    [SerializeField, Range(0f, 1f)] private float unownedDarken = 0.58f;
    [SerializeField] private Color unownedIconColor =
        new(0.34f, 0.34f, 0.34f, 1f);
    [SerializeField] private Color unownedTextColor =
        new(0.48f, 0.48f, 0.43f, 1f);

    [SerializeField, HideInInspector] private int designerLayoutVersion;

    public RectTransform ListPanel => listPanel;
    public RectTransform Toolbar => toolbar;
    public RectTransform CardContent => cardContent;
    public RectTransform DetailRoot => detailRoot;
    public GridLayoutGroup CardGrid => cardGrid;
    public TMP_InputField SearchInput => searchInput;
    public Button SearchButton => searchButton;
    public Button FilterButton => filterButton;
    public Button SortButton => sortButton;
    public GameObject CardTemplate => cardTemplate;
    public Color OwnedCardColor => ownedCardColor;
    public Color OwnedTextColor => ownedTextColor;
    public float UnownedDarken => unownedDarken;
    public Color UnownedIconColor => unownedIconColor;
    public Color UnownedTextColor => unownedTextColor;
    public bool HasDesignerLayout => designerLayoutVersion > 0;

    public void CaptureReferencesFromHierarchy()
    {
        Transform list = transform.Find("grpCodexList");
        Transform toolbarTransform = list != null
            ? list.Find("grpCodexListToolbar")
            : null;
        Transform content = list != null
            ? list.Find(
                "scrCodexList/vptCodexList/grpCodexCardContent")
            : null;
        Transform detail = transform.Find("grpCodexDetailHost");

        listPanel = list as RectTransform;
        toolbar = toolbarTransform as RectTransform;
        cardContent = content as RectTransform;
        detailRoot = detail as RectTransform;
        cardGrid = content != null
            ? content.GetComponent<GridLayoutGroup>()
            : null;
        searchInput = toolbarTransform != null
            ? toolbarTransform.Find("inpCodexSearch")
                ?.GetComponent<TMP_InputField>()
            : null;
        searchButton = toolbarTransform != null
            ? toolbarTransform.Find("btnCodexSearch")
                ?.GetComponent<Button>()
            : null;
        filterButton = toolbarTransform != null
            ? toolbarTransform.Find("btnCodexFilter")
                ?.GetComponent<Button>()
            : null;
        sortButton = toolbarTransform != null
            ? toolbarTransform.Find("btnCodexSort")
                ?.GetComponent<Button>()
            : null;

        if (cardTemplate == null && content != null)
        {
            Transform firstCard = content.Find("btnCodexCard_0");
            if (firstCard != null)
                cardTemplate = firstCard.gameObject;
        }
    }

    public void MarkDesignerLayoutCurrent()
    {
        designerLayoutVersion = 1;
    }
}
