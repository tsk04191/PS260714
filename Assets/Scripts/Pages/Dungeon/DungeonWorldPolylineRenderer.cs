using System.Collections.Generic;
using UnityEngine;

/// <summary>Draws authored dungeon-world lines and rings with a runtime mesh.</summary>
[DisallowMultipleComponent]
public sealed class DungeonWorldPolylineRenderer : MonoBehaviour
{
    private const int DefaultRingSegments = 48;

    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;

    private Mesh _mesh;
    private MaterialPropertyBlock _properties;

    public void SetSortingOrder(int order)
    {
        if (meshRenderer != null)
            meshRenderer.sortingOrder = order;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void SetSegment(
        Vector3 start,
        Vector3 end,
        float width,
        Color color)
    {
        if (!EnsureMesh())
            return;

        Vector3 direction = end - start;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.000001f)
        {
            SetVisible(false);
            return;
        }

        Vector3 side = Vector3.Cross(Vector3.up, direction.normalized) *
                       Mathf.Max(0.001f, width) * 0.5f;
        Vector3[] vertices =
        {
            start - side,
            start + side,
            end - side,
            end + side,
        };
        int[] triangles = { 0, 1, 2, 2, 1, 3 };
        ApplyMesh(vertices, triangles);
        SetColor(color);
        SetVisible(true);
    }

    public void SetRing(
        float radius,
        float progress,
        float width,
        Color color,
        Vector3 center,
        int segments = DefaultRingSegments,
        float startAngle = 90f,
        float sweepAngle = 360f,
        bool clockwise = true)
    {
        if (!EnsureMesh())
            return;

        progress = Mathf.Clamp01(progress);
        if (progress <= 0.0001f)
        {
            SetVisible(false);
            return;
        }

        radius = Mathf.Max(0.001f, radius);
        width = Mathf.Clamp(width, 0.001f, radius * 1.9f);
        segments = Mathf.Max(8, segments);
        sweepAngle = Mathf.Clamp(sweepAngle, 1f, 360f);
        float sweepRatio = sweepAngle / 360f;
        int sectionCount = progress >= 0.999f
            ? Mathf.Max(2, Mathf.CeilToInt(segments * sweepRatio))
            : Mathf.Max(
                2,
                Mathf.CeilToInt(segments * sweepRatio * progress));
        int sampleCount = sectionCount + 1;
        Vector3[] vertices = new Vector3[sampleCount * 2];
        int[] triangles = new int[sectionCount * 6];
        float inner = Mathf.Max(0.001f, radius - width * 0.5f);
        float outer = radius + width * 0.5f;
        for (int index = 0; index < sampleCount; index++)
        {
            float normalized = index / (float)sectionCount * progress;
            float direction = clockwise ? -1f : 1f;
            float angle = (startAngle +
                           direction * normalized * sweepAngle) *
                          Mathf.Deg2Rad;
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[index * 2] = center + radial * inner;
            vertices[index * 2 + 1] = center + radial * outer;

            if (index >= sectionCount)
                continue;
            int triangle = index * 6;
            int vertex = index * 2;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 2;
            triangles[triangle + 3] = vertex + 2;
            triangles[triangle + 4] = vertex + 1;
            triangles[triangle + 5] = vertex + 3;
        }

        ApplyMesh(vertices, triangles);
        SetColor(color);
        SetVisible(true);
    }

    public void SetPolyline(
        IReadOnlyList<Vector3> points,
        float width,
        Color color,
        bool close)
    {
        if (!EnsureMesh() || points == null || points.Count < 2)
        {
            SetVisible(false);
            return;
        }

        int segmentCount = points.Count - 1 + (close ? 1 : 0);
        Vector3[] vertices = new Vector3[segmentCount * 4];
        int[] triangles = new int[segmentCount * 6];
        float halfWidth = Mathf.Max(0.001f, width) * 0.5f;
        for (int index = 0; index < segmentCount; index++)
        {
            Vector3 start = points[index % points.Count];
            Vector3 end = points[(index + 1) % points.Count];
            Vector3 direction = end - start;
            direction.y = 0f;
            Vector3 side = direction.sqrMagnitude > 0.000001f
                ? Vector3.Cross(Vector3.up, direction.normalized) * halfWidth
                : Vector3.right * halfWidth;
            int vertex = index * 4;
            vertices[vertex] = start - side;
            vertices[vertex + 1] = start + side;
            vertices[vertex + 2] = end - side;
            vertices[vertex + 3] = end + side;
            int triangle = index * 6;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 2;
            triangles[triangle + 3] = vertex + 2;
            triangles[triangle + 4] = vertex + 1;
            triangles[triangle + 5] = vertex + 3;
        }

        ApplyMesh(vertices, triangles);
        SetColor(color);
        SetVisible(true);
    }

    private bool EnsureMesh()
    {
        if (meshFilter == null || meshRenderer == null)
            return false;
        if (_mesh != null)
            return true;

        _mesh = new Mesh
        {
            name = $"{name} Runtime Mesh",
            hideFlags = HideFlags.DontSave,
        };
        meshFilter.sharedMesh = _mesh;
        return true;
    }

    private void ApplyMesh(Vector3[] vertices, int[] triangles)
    {
        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();
        _mesh.RecalculateNormals();
    }

    private void SetColor(Color color)
    {
        _properties ??= new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(_properties);
        _properties.SetColor("_Color", color);
        _properties.SetColor("_BaseColor", color);
        meshRenderer.SetPropertyBlock(_properties);
    }

    private void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
        _mesh = null;
    }
}
