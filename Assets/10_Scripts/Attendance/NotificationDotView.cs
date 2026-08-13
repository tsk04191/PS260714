using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NotificationDotView : MaskableGraphic
{
    private const int SegmentCount = 32;

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
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
        SetVerticesDirty();
    }
#endif
}
