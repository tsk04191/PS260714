using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class PracticeBattleDebugVisualizationTests
{
    private readonly List<Object> _created = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = _created.Count - 1; index >= 0; index--)
        {
            if (_created[index] != null)
                Object.DestroyImmediate(_created[index]);
        }
        _created.Clear();
    }

    [Test]
    public void ActorHitGeometry_IncludesExactBoundary_AndRejectsOutside()
    {
        Vector2 center = new(12f, -7f);

        Assert.That(
            PracticeBattleDebugGeometry.TryMeasureActorHit(
                center + Vector2.right * 46f,
                center,
                46f,
                out float boundaryDistance),
            Is.True);
        Assert.That(boundaryDistance, Is.EqualTo(46f).Within(0.0001f));
        Assert.That(
            PracticeBattleDebugGeometry.TryMeasureActorHit(
                center + Vector2.right * 46.01f,
                center,
                46f,
                out _),
            Is.False);
        Assert.That(
            PracticeBattleDebugGeometry.ResolveActorHitRadius(-10f),
            Is.EqualTo(1f));
    }

    [Test]
    public void SpatialGeometry_UsesActualSpacingContributions()
    {
        Assert.That(
            PracticeBattleDebugGeometry.ResolveAllySpacingRadius(0.55f),
            Is.EqualTo(0.275f).Within(0.0001f));
        Assert.That(
            PracticeBattleDebugGeometry.ResolveEnemyFormationRadius(
                0.4f,
                0.75f),
            Is.EqualTo(0.3f).Within(0.0001f));
    }

    [Test]
    public void CoreReach_IsRadialAndStopsAtDefenseLine()
    {
        Vector2 enemy = new(0f, 4f);

        Vector2 normalReach =
            PracticeBattleDebugGeometry.ResolveEnemyCoreReachEnd(
                enemy,
                2f,
                0.75f);
        Vector2 clippedReach =
            PracticeBattleDebugGeometry.ResolveEnemyCoreReachEnd(
                enemy,
                2f,
                8f);

        Assert.That(normalReach.x, Is.Zero.Within(0.0001f));
        Assert.That(normalReach.y, Is.EqualTo(3.25f).Within(0.0001f));
        Assert.That(clippedReach.x, Is.Zero.Within(0.0001f));
        Assert.That(clippedReach.y, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void GroundCircle_IsProjectedAsConnectedPerspectivePolyline()
    {
        List<(Vector2 Start, Vector2 End)> lines = new();

        int appended =
            PracticeBattleDebugGeometry.AppendProjectedGroundCircle(
                new Vector2(0.5f, 1f),
                2f,
                24,
                ProjectPerspective,
                (start, end) => lines.Add((start, end)));

        Assert.That(appended, Is.EqualTo(24));
        Assert.That(lines, Has.Count.EqualTo(24));
        for (int index = 0; index < lines.Count; index++)
        {
            Vector2 nextStart = lines[(index + 1) % lines.Count].Start;
            Assert.That(
                (lines[index].End - nextStart).sqrMagnitude,
                Is.LessThanOrEqualTo(0.000001f));
        }

        float upperDistance = lines[6].Start.magnitude;
        float lowerDistance = lines[18].Start.magnitude;
        Assert.That(
            Mathf.Abs(upperDistance - lowerDistance),
            Is.GreaterThan(0.05f));
    }

    [Test]
    public void Overlay_ToggleAndClear_LeaveNoRaycastOrStalePrimitives()
    {
        GameObject inputObject = CreateRect("DebugInput");
        RectTransform inputRect = inputObject.transform as RectTransform;
        inputRect.sizeDelta = new Vector2(800f, 450f);

        GameObject overlayObject = CreateRect("DebugOverlay");
        RectTransform overlayRect =
            overlayObject.transform as RectTransform;
        overlayRect.sizeDelta = inputRect.sizeDelta;
        PracticeBattleDebugOverlayGraphic graphic =
            overlayObject.AddComponent<PracticeBattleDebugOverlayGraphic>();
        PracticeBattleDebugOverlayView view =
            overlayObject.AddComponent<PracticeBattleDebugOverlayView>();
        TestReflection.SetField(view, "overlayGraphic", graphic);

        view.SetVisible(true);
        view.BeginFrame();
        view.AddInputCircle(
            inputRect,
            new Vector2(40f, -20f),
            46f,
            PracticeBattleDebugPrimitiveKind.AllyClick);
        view.AddInputLine(
            inputRect,
            Vector2.zero,
            Vector2.right * 20f,
            PracticeBattleDebugPrimitiveKind.CoreReach);
        view.EndFrame();

        Assert.That(view.IsVisible, Is.True);
        Assert.That(view.PrimitiveCount, Is.EqualTo(2));
        Assert.That(graphic.raycastTarget, Is.False);

        view.SetVisible(false);

        Assert.That(view.IsVisible, Is.False);
        Assert.That(view.PrimitiveCount, Is.Zero);
        Assert.That(graphic.enabled, Is.False);

        view.SetVisible(true);
        view.BeginFrame();
        view.AddInputCircle(
            inputRect,
            Vector2.zero,
            46f,
            PracticeBattleDebugPrimitiveKind.EnemyClick);
        view.EndFrame();
        Assert.That(view.PrimitiveCount, Is.EqualTo(1));

        overlayObject.SetActive(false);

        Assert.That(view.IsVisible, Is.False);
        Assert.That(graphic.isActiveAndEnabled, Is.False);

        // EditMode does not dispatch runtime MonoBehaviour lifecycle methods.
        // Invoke the same cleanup hook that Unity calls in PlayMode.
        TestReflection.InvokeMethod(view, "OnDisable");

        Assert.That(view.PrimitiveCount, Is.Zero);
        Assert.That(graphic.enabled, Is.False);
    }

    [Test]
    public void BoardPointerAndOverlay_ConsumeSameCenterAndRadiusHelpers()
    {
        string sourcePath = Path.Combine(
            Application.dataPath,
            "10_Scripts",
            "Run",
            "DungeonBoardView.cs");
        string source = File.ReadAllText(sourcePath)
            .Replace("\r\n", "\n");

        Assert.That(
            source,
            Does.Contain(
                "PracticeBattleDebugGeometry.TryMeasureActorHit("));
        Assert.That(
            source,
            Does.Contain(
                "PracticeBattleDebugGeometry.ResolveActorHitRadius("));
        Assert.That(
            source,
            Does.Contain("TryGetActorInputLocalPosition("));
        Assert.That(source, Does.Contain("Mathf.Lerp(bounds.xMin"));
        Assert.That(source, Does.Not.Contain("Mathf.LerpUnclamped("));
        Assert.That(source, Does.Contain("bool clampToViewport = true"));
        Assert.That(source, Does.Contain("out localPosition,\n            false"));
        Assert.That(
            source,
            Does.Contain(
                "PracticeBattleDebugGeometry.AppendProjectedGroundCircle("));
        Assert.That(
            source,
            Does.Contain("WorldActorGroundHeight,"));
    }

    private GameObject CreateRect(string name)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        _created.Add(gameObject);
        return gameObject;
    }

    private static bool ProjectPerspective(
        Vector2 ground,
        out Vector2 projected)
    {
        float depth = 5f + ground.y * 0.35f;
        projected = new Vector2(
            ground.x / depth,
            ground.y / depth);
        return true;
    }

}
