using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DungeonBattleAreaPreviewPrefabView : MonoBehaviour
{
    private const int SegmentCount = 64;
    private const float PreviewHeight = 0.095f;

    [SerializeField] private MeshFilter fillFilter;
    [SerializeField] private MeshRenderer fillRenderer;
    [SerializeField] private DungeonWorldPolylineRenderer outline;
    [SerializeField] private Color fillColor =
        new(0.898f, 0.224f, 0.208f, 0.3f);
    [SerializeField] private Color outlineColor =
        new(1f, 0.31f, 0.28f, 0.94f);
    [SerializeField, Min(0.001f)] private float outlineWidth = 0.045f;

    private Mesh _mesh;
    private MaterialPropertyBlock _properties;

    public bool HasRequiredReferences =>
        fillFilter != null && fillRenderer != null && outline != null;

    public void Show(
        Vector2 origin,
        Vector2 direction,
        BattleAreaDefinition definition)
    {
        if (!HasRequiredReferences || definition == null ||
            !definition.UsesWorldArea)
        {
            Hide();
            return;
        }

        float sectorAngle = definition.ShapeType switch
        {
            CharacterAreaShapeType.Circle => 360f,
            CharacterAreaShapeType.Semicircle => 180f,
            CharacterAreaShapeType.Cone => definition.ConeAngle,
            _ => 0f,
        };
        if (sectorAngle <= 0f)
        {
            Hide();
            return;
        }

        EnsureMesh();
        int segments = Mathf.Max(
            8,
            Mathf.CeilToInt(SegmentCount * sectorAngle / 360f));
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;
        float startAngle = -sectorAngle * 0.5f;
        for (int index = 0; index <= segments; index++)
        {
            float angle = (startAngle + sectorAngle * index / segments) *
                          Mathf.Deg2Rad;
            vertices[index + 1] = new Vector3(
                Mathf.Sin(angle) * definition.Radius,
                0f,
                Mathf.Cos(angle) * definition.Radius);
            if (index >= segments)
                continue;

            int triangle = index * 3;
            triangles[triangle] = 0;
            triangles[triangle + 1] = index + 1;
            triangles[triangle + 2] = index + 2;
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();
        _mesh.RecalculateNormals();
        SetFillColor();
        RefreshOutline(vertices, segments, sectorAngle);

        Vector2 forward = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.up;
        transform.localPosition = new Vector3(
            origin.x,
            PreviewHeight,
            origin.y);
        transform.localRotation = Quaternion.Euler(
            0f,
            Mathf.Atan2(forward.x, forward.y) * Mathf.Rad2Deg,
            0f);
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void EnsureMesh()
    {
        if (_mesh != null)
            return;
        _mesh = new Mesh
        {
            name = "Manual Area Preview Runtime Mesh",
            hideFlags = HideFlags.DontSave,
        };
        fillFilter.sharedMesh = _mesh;
    }

    private void SetFillColor()
    {
        _properties ??= new MaterialPropertyBlock();
        fillRenderer.GetPropertyBlock(_properties);
        _properties.SetColor("_Color", fillColor);
        _properties.SetColor("_BaseColor", fillColor);
        fillRenderer.SetPropertyBlock(_properties);
    }

    private void RefreshOutline(
        IReadOnlyList<Vector3> vertices,
        int segments,
        float sectorAngle)
    {
        bool circle = sectorAngle >= 359.9f;
        List<Vector3> points = new(segments + (circle ? 0 : 3));
        if (!circle)
            points.Add(Vector3.zero);
        for (int index = 0; index <= segments; index++)
        {
            Vector3 point = vertices[index + 1];
            point.y += 0.012f;
            points.Add(point);
        }
        if (!circle)
            points.Add(Vector3.zero);
        outline.SetPolyline(points, outlineWidth, outlineColor, circle);
    }

    private void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
        _mesh = null;
    }
}
