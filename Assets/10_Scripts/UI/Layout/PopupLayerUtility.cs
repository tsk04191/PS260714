using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public struct PopupLayerPlacement
{
    internal RectTransform Popup;
    internal Transform Parent;
    internal int SiblingIndex;
    internal Vector2 AnchorMin;
    internal Vector2 AnchorMax;
    internal Vector2 Pivot;
    internal Vector2 AnchoredPosition;
    internal Vector2 SizeDelta;
    internal Vector3 LocalScale;

    public bool IsActive => Popup != null && Parent != null;
}

public static class ResponsiveCanvasUtility
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public static void Configure(CanvasScaler scaler)
    {
        if (scaler == null ||
            scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            return;
        }

        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.Expand;
    }

    private static void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            CanvasScaler[] scalers = roots[rootIndex]
                .GetComponentsInChildren<CanvasScaler>(true);
            for (int index = 0; index < scalers.Length; index++)
                Configure(scalers[index]);
        }
    }
}

public static class PopupLayerUtility
{
    public const string PopupLayerName = "layPopup";

    public static RectTransform FindPopupLayer(Transform context)
    {
        if (context == null)
            return null;

        Scene scene = context.gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            RectTransform[] candidates = roots[rootIndex]
                .GetComponentsInChildren<RectTransform>(true);
            for (int index = 0; index < candidates.Length; index++)
            {
                if (candidates[index] != null &&
                    candidates[index].name == PopupLayerName)
                {
                    return candidates[index];
                }
            }
        }

        return null;
    }

    public static PopupLayerPlacement MoveToPopupLayer(
        RectTransform popup,
        RectTransform owner,
        float gap = 12f,
        float edgePadding = 12f)
    {
        PopupLayerPlacement placement = Capture(popup);
        RectTransform layer = FindPopupLayer(owner);
        if (popup == null || owner == null || layer == null)
            return placement;

        Vector3[] ownerCorners = new Vector3[4];
        owner.GetWorldCorners(ownerCorners);
        Vector3 rightCenter = (ownerCorners[2] + ownerCorners[3]) * 0.5f;
        Vector3 leftCenter = (ownerCorners[0] + ownerCorners[1]) * 0.5f;
        Vector3 rightLocal = layer.InverseTransformPoint(rightCenter);
        Vector3 leftLocal = layer.InverseTransformPoint(leftCenter);

        popup.SetParent(layer, false);
        popup.anchorMin = new Vector2(0.5f, 0.5f);
        popup.anchorMax = new Vector2(0.5f, 0.5f);
        Vector3 authoredScale = placement.LocalScale;
        popup.localScale = authoredScale;

        float width = popup.rect.width > 0f
            ? popup.rect.width
            : popup.sizeDelta.x;
        float height = popup.rect.height > 0f
            ? popup.rect.height
            : popup.sizeDelta.y;
        Rect layerRect = layer.rect;
        Vector2 availableSize = new(
            Mathf.Max(0f, layerRect.width - edgePadding * 2f),
            Mathf.Max(0f, layerRect.height - edgePadding * 2f));
        float fitScale = ResponsivePanelFitter.CalculateFitScale(
            availableSize,
            new Vector2(
                width * Mathf.Abs(authoredScale.x),
                height * Mathf.Abs(authoredScale.y)),
            false);
        popup.localScale = new Vector3(
            authoredScale.x * fitScale,
            authoredScale.y * fitScale,
            authoredScale.z);
        width *= Mathf.Abs(popup.localScale.x);
        height *= Mathf.Abs(popup.localScale.y);
        bool openLeft = rightLocal.x + gap + width >
                        layerRect.xMax - edgePadding;
        popup.pivot = new Vector2(openLeft ? 1f : 0f, 0.5f);
        float x = openLeft
            ? leftLocal.x - gap
            : rightLocal.x + gap;
        float y = Mathf.Clamp(
            rightLocal.y,
            layerRect.yMin + height * 0.5f + edgePadding,
            layerRect.yMax - height * 0.5f - edgePadding);
        popup.localPosition = new Vector3(x, y, 0f);
        popup.SetAsLastSibling();
        return placement;
    }

    public static bool Restore(PopupLayerPlacement placement)
    {
        RectTransform popup = placement.Popup;
        if (popup == null || placement.Parent == null)
            return false;

        Transform currentParent = popup.parent;
        if (currentParent == null ||
            !currentParent.gameObject.activeInHierarchy ||
            !placement.Parent.gameObject.activeInHierarchy)
        {
            return false;
        }

        popup.SetParent(placement.Parent, false);
        popup.anchorMin = placement.AnchorMin;
        popup.anchorMax = placement.AnchorMax;
        popup.pivot = placement.Pivot;
        popup.anchoredPosition = placement.AnchoredPosition;
        popup.sizeDelta = placement.SizeDelta;
        popup.localScale = placement.LocalScale;
        popup.SetSiblingIndex(Mathf.Clamp(
            placement.SiblingIndex,
            0,
            placement.Parent.childCount - 1));
        return true;
    }

    private static PopupLayerPlacement Capture(RectTransform popup)
    {
        return popup == null
            ? default
            : new PopupLayerPlacement
            {
                Popup = popup,
                Parent = popup.parent,
                SiblingIndex = popup.GetSiblingIndex(),
                AnchorMin = popup.anchorMin,
                AnchorMax = popup.anchorMax,
                Pivot = popup.pivot,
                AnchoredPosition = popup.anchoredPosition,
                SizeDelta = popup.sizeDelta,
                LocalScale = popup.localScale,
            };
    }
}

public abstract class ResponsivePanelFitterBase : MonoBehaviour
{
    [SerializeField] private RectTransform viewport;
    [SerializeField] private bool allowUpscale;

    private RectTransform _panel;
    private Vector2 _authoredSize;
    private Vector3 _authoredScale;
    private bool _captured;

    public static ResponsivePanelFitter Bind(
        RectTransform panel,
        RectTransform viewport = null,
        bool allowUpscale = false)
    {
        if (panel == null)
            return null;

        ResponsivePanelFitter fitter =
            panel.GetComponent<ResponsivePanelFitter>();
        if (fitter == null)
        {
            Debug.LogError(
                "ResponsivePanelFitter must be authored on the panel.",
                panel);
            return null;
        }
        fitter.CaptureAuthoredLayout();
        fitter.RefreshLayout();
        return fitter;
    }

    public static float CalculateFitScale(
        Vector2 viewportSize,
        Vector2 contentSize,
        bool allowUpscale)
    {
        if (viewportSize.x <= 0f || viewportSize.y <= 0f ||
            contentSize.x <= 0f || contentSize.y <= 0f)
        {
            return 1f;
        }

        float scale = Mathf.Min(
            viewportSize.x / contentSize.x,
            viewportSize.y / contentSize.y);
        return allowUpscale
            ? Mathf.Max(0f, scale)
            : Mathf.Clamp01(scale);
    }

    public void RefreshLayout()
    {
        EnsureReferences();
        if (!_captured || _panel == null || viewport == null)
            return;

        float fitScale = CalculateFitScale(
            viewport.rect.size,
            _authoredSize,
            allowUpscale);
        _panel.localScale = new Vector3(
            _authoredScale.x * fitScale,
            _authoredScale.y * fitScale,
            _authoredScale.z);
    }

    protected virtual void Awake()
    {
        EnsureReferences();
        CaptureAuthoredLayout();
    }

    protected virtual void OnEnable()
    {
        RefreshLayout();
    }

    protected virtual void OnRectTransformDimensionsChange()
    {
        RefreshLayout();
    }

    private void EnsureReferences()
    {
        _panel ??= transform as RectTransform;
        viewport ??= _panel != null
            ? _panel.parent as RectTransform
            : null;
    }

    private void CaptureAuthoredLayout()
    {
        EnsureReferences();
        if (_captured || _panel == null)
            return;

        _authoredSize = _panel.rect.size;
        if (_authoredSize.x <= 0f || _authoredSize.y <= 0f)
            _authoredSize = _panel.sizeDelta;
        _authoredScale = _panel.localScale;
        _captured = _authoredSize.x > 0f && _authoredSize.y > 0f;
    }
}

public abstract class ResponsiveGridConstraintBase : MonoBehaviour
{
    [SerializeField] private RectTransform viewport;
    [SerializeField, Min(1)] private int maximumColumns = int.MaxValue;

    private GridLayoutGroup _grid;
    private bool _bound;
    private bool _refreshing;

    public static ResponsiveGridConstraint Bind(
        GridLayoutGroup grid,
        RectTransform viewport = null,
        int maximumColumns = int.MaxValue)
    {
        if (grid == null)
            return null;

        ResponsiveGridConstraint constraint =
            grid.GetComponent<ResponsiveGridConstraint>();
        if (constraint == null)
        {
            Debug.LogError(
                "ResponsiveGridConstraint must be authored on the grid.",
                grid);
            return null;
        }
        constraint._bound = true;
        if (constraint.viewport != null &&
            constraint.viewport.GetComponentInParent<Canvas>() != null)
        {
            constraint.RefreshLayout();
        }
        return constraint;
    }

    public static int CalculateColumnCount(
        float viewportWidth,
        float cellWidth,
        float spacing,
        int leftPadding,
        int rightPadding)
    {
        float usableWidth = Mathf.Max(
            0f,
            viewportWidth - leftPadding - rightPadding);
        if (cellWidth <= 0f)
            return 1;

        float occupiedWidth = cellWidth + Mathf.Max(0f, spacing);
        if (occupiedWidth <= 0f)
            return 1;

        return Mathf.Max(
            1,
            Mathf.FloorToInt(
                (usableWidth + Mathf.Max(0f, spacing)) /
                occupiedWidth));
    }

    public void RefreshLayout()
    {
        if (!_bound || _refreshing)
            return;

        _grid ??= GetComponent<GridLayoutGroup>();
        viewport ??= transform.parent as RectTransform;
        if (_grid == null || viewport == null ||
            viewport.rect.width <= 0f)
        {
            return;
        }

        _refreshing = true;
        int columnCount = Mathf.Min(
            maximumColumns,
            CalculateColumnCount(
                viewport.rect.width,
                _grid.cellSize.x,
                _grid.spacing.x,
                _grid.padding.left,
                _grid.padding.right));
        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = columnCount;
        _refreshing = false;
    }

    protected virtual void Awake()
    {
        _grid = GetComponent<GridLayoutGroup>();
        _bound = true;
    }

    protected virtual void OnEnable()
    {
        RefreshLayout();
    }

    protected virtual void Start()
    {
        RefreshLayout();
    }

    protected virtual void OnRectTransformDimensionsChange()
    {
        RefreshLayout();
    }
}
