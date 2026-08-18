using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PracticeBattleDebugOverlayGraphic : MaskableGraphic
{
    private const int CircleSegmentCount = 48;

    private readonly struct CirclePrimitive
    {
        public Vector2 Center { get; }
        public Vector2 AxisX { get; }
        public Vector2 AxisY { get; }
        public float Width { get; }
        public Color32 Color { get; }

        public CirclePrimitive(
            Vector2 center,
            Vector2 axisX,
            Vector2 axisY,
            float width,
            Color color)
        {
            Center = center;
            AxisX = axisX;
            AxisY = axisY;
            Width = Mathf.Max(0.5f, width);
            Color = color;
        }
    }

    private readonly struct LinePrimitive
    {
        public Vector2 Start { get; }
        public Vector2 End { get; }
        public float Width { get; }
        public Color32 Color { get; }

        public LinePrimitive(
            Vector2 start,
            Vector2 end,
            float width,
            Color color)
        {
            Start = start;
            End = end;
            Width = Mathf.Max(0.5f, width);
            Color = color;
        }
    }

    private readonly List<CirclePrimitive> _circles = new();
    private readonly List<LinePrimitive> _lines = new();

    public int CircleCount => _circles.Count;
    public int LineCount => _lines.Count;
    public int PrimitiveCount => CircleCount + LineCount;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void BeginFrame()
    {
        _circles.Clear();
        _lines.Clear();
    }

    public void AddCircle(
        Vector2 center,
        Vector2 axisX,
        Vector2 axisY,
        float width,
        Color color)
    {
        if (!IsFinite(center) || !IsFinite(axisX) || !IsFinite(axisY) ||
            axisX.sqrMagnitude <= 0.0001f ||
            axisY.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _circles.Add(new CirclePrimitive(
            center,
            axisX,
            axisY,
            width,
            color));
    }

    public void AddLine(
        Vector2 start,
        Vector2 end,
        float width,
        Color color)
    {
        if (!IsFinite(start) || !IsFinite(end) ||
            (end - start).sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _lines.Add(new LinePrimitive(start, end, width, color));
    }

    public void EndFrame()
    {
        SetVerticesDirty();
    }

    public void Clear()
    {
        if (_circles.Count == 0 && _lines.Count == 0)
            return;

        _circles.Clear();
        _lines.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        foreach (CirclePrimitive circle in _circles)
        {
            Vector2 previous = ResolveCirclePoint(circle, 0);
            for (int index = 1; index <= CircleSegmentCount; index++)
            {
                Vector2 current = ResolveCirclePoint(circle, index);
                AddLineQuad(
                    vertexHelper,
                    previous,
                    current,
                    circle.Width,
                    circle.Color);
                previous = current;
            }
        }

        foreach (LinePrimitive line in _lines)
        {
            AddLineQuad(
                vertexHelper,
                line.Start,
                line.End,
                line.Width,
                line.Color);
        }
    }

    private static Vector2 ResolveCirclePoint(
        CirclePrimitive circle,
        int segmentIndex)
    {
        float radians = segmentIndex * Mathf.PI * 2f /
                        CircleSegmentCount;
        return circle.Center +
               circle.AxisX * Mathf.Cos(radians) +
               circle.AxisY * Mathf.Sin(radians);
    }

    private static void AddLineQuad(
        VertexHelper vertexHelper,
        Vector2 start,
        Vector2 end,
        float width,
        Color32 color)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        direction.Normalize();
        Vector2 normal = new Vector2(-direction.y, direction.x) *
                         (Mathf.Max(0.5f, width) * 0.5f);
        int firstVertex = vertexHelper.currentVertCount;
        AddVertex(vertexHelper, start - normal, color);
        AddVertex(vertexHelper, start + normal, color);
        AddVertex(vertexHelper, end + normal, color);
        AddVertex(vertexHelper, end - normal, color);
        vertexHelper.AddTriangle(
            firstVertex,
            firstVertex + 1,
            firstVertex + 2);
        vertexHelper.AddTriangle(
            firstVertex,
            firstVertex + 2,
            firstVertex + 3);
    }

    private static void AddVertex(
        VertexHelper vertexHelper,
        Vector2 position,
        Color32 color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vertexHelper.AddVert(vertex);
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) &&
               !float.IsInfinity(value.y);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        raycastTarget = false;
        SetVerticesDirty();
    }
#endif
}
