using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class DungeonCardFanLayout : LayoutGroup
{
    [SerializeField, Min(20f)] private float cardSpacing = 112f;
    [SerializeField, Min(0f)] private float arcHeight = 34f;
    [SerializeField, Range(0f, 30f)] private float maximumAngle = 12f;
    [SerializeField] private float bottomOffset = -38f;

    private readonly List<RectTransform> _ordered = new();

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        SetLayoutInputForAxis(0f, rectTransform.rect.width, -1f, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        SetLayoutInputForAxis(0f, rectTransform.rect.height, -1f, 1);
    }

    public override void SetLayoutHorizontal()
    {
        ApplyFan();
    }

    public override void SetLayoutVertical()
    {
        ApplyFan();
    }

    private void ApplyFan()
    {
        _ordered.Clear();
        for (int index = 0; index < rectChildren.Count; index++)
        {
            RectTransform child = rectChildren[index];
            if (child != null && child.gameObject.activeSelf)
                _ordered.Add(child);
        }
        _ordered.Sort((left, right) =>
        {
            DungeonItemCardView leftCard =
                left.GetComponent<DungeonItemCardView>();
            DungeonItemCardView rightCard =
                right.GetComponent<DungeonItemCardView>();
            return (leftCard?.LayoutOrder ?? left.GetSiblingIndex())
                .CompareTo(rightCard?.LayoutOrder ?? right.GetSiblingIndex());
        });

        int count = _ordered.Count;
        if (count == 0)
            return;

        float cardWidth = _ordered[0].rect.width;
        float available = Mathf.Max(0f, rectTransform.rect.width - cardWidth);
        float spacing = count > 1
            ? Mathf.Min(cardSpacing, available / (count - 1))
            : 0f;
        float center = (count - 1) * 0.5f;
        for (int index = 0; index < count; index++)
        {
            RectTransform child = _ordered[index];
            float offset = index - center;
            float normalized = center > 0f ? offset / center : 0f;
            Vector2 position = new(
                offset * spacing,
                child.rect.height * 0.5f + bottomOffset +
                arcHeight * (1f - normalized * normalized));
            float angle = -normalized * maximumAngle;

            child.anchorMin = new Vector2(0.5f, 0f);
            child.anchorMax = new Vector2(0.5f, 0f);
            child.pivot = new Vector2(0.5f, 0.5f);
            DungeonItemCardView card =
                child.GetComponent<DungeonItemCardView>();
            if (card != null)
                card.ApplyLayoutPose(position, angle);
            else
            {
                child.anchoredPosition = position;
                child.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }
}
