using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NotificationDotView : MaskableGraphic
{
    private const string ResourcePath = "Presentation/NotificationDot";
    private const int SegmentCount = 32;

    [SerializeField, Min(1f)] private float diameter = 18f;
    [SerializeField, Range(0f, 1f)] private float overlapRatio = 0.25f;

    public float Diameter => Mathf.Max(1f, diameter);
    public float OverlapRatio => Mathf.Clamp01(overlapRatio);

    public static NotificationDotView BuildOrBind(RectTransform button)
    {
        if (button == null)
            return null;

        NotificationDotView existing =
            button.GetComponentInChildren<NotificationDotView>(true);
        if (existing != null)
        {
            existing.ApplyLayout(button);
            return existing;
        }

        NotificationDotView prefab =
            Resources.Load<NotificationDotView>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogError(
                $"Notification dot prefab is missing at Resources/" +
                $"{ResourcePath}.",
                button);
            return null;
        }

        NotificationDotView instance = Instantiate(prefab, button, false);
        instance.name = "imgNotificationDot";
        instance.ApplyLayout(button);
        return instance;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void ApplyLayout(RectTransform button)
    {
        RectTransform rect = rectTransform;
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.one * Diameter;
        float outwardOffset = Diameter * (0.5f - OverlapRatio);
        rect.anchoredPosition = new Vector2(
            outwardOffset,
            outwardOffset);
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        float radius = Mathf.Min(rectTransform.rect.width,
            rectTransform.rect.height) * 0.5f;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = Vector3.zero;
        vertexHelper.AddVert(vertex);

        for (int index = 0; index <= SegmentCount; index++)
        {
            float radians = index * Mathf.PI * 2f / SegmentCount;
            vertex.position = new Vector3(
                Mathf.Cos(radians) * radius,
                Mathf.Sin(radians) * radius,
                0f);
            vertexHelper.AddVert(vertex);
        }

        for (int index = 1; index <= SegmentCount; index++)
            vertexHelper.AddTriangle(0, index, index + 1);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        diameter = Mathf.Max(1f, diameter);
        overlapRatio = Mathf.Clamp01(overlapRatio);
        if (transform.parent is RectTransform button)
            ApplyLayout(button);
    }
#endif
}
