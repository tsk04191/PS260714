using UnityEngine;
using UnityEngine.SceneManagement;

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
        popup.localScale = Vector3.one;

        float width = popup.rect.width > 0f
            ? popup.rect.width
            : popup.sizeDelta.x;
        float height = popup.rect.height > 0f
            ? popup.rect.height
            : popup.sizeDelta.y;
        Rect layerRect = layer.rect;
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

    public static void Restore(PopupLayerPlacement placement)
    {
        RectTransform popup = placement.Popup;
        if (popup == null || placement.Parent == null)
            return;

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
