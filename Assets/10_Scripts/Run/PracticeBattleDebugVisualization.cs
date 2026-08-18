using UnityEngine;

public interface IPracticeBattleDebugVisualization
{
    bool PracticeDebugVisualizationEnabled { get; }

    void SetPracticeDebugVisualization(bool enabled);
}

public enum PracticeBattleDebugPrimitiveKind
{
    AllyClick = 0,
    EnemyClick = 1,
    AllySpacing = 2,
    EnemyFormation = 3,
    AbilityRange = 4,
    CoreReach = 5
}

public delegate bool PracticeBattleDebugGroundProjector(
    Vector2 ground,
    out Vector2 projected);

public delegate void PracticeBattleDebugLineConsumer(
    Vector2 start,
    Vector2 end);

public static class PracticeBattleDebugGeometry
{
    public static float ResolveActorHitRadius(float authoredRadius)
    {
        return Mathf.Max(1f, authoredRadius);
    }

    public static bool TryMeasureActorHit(
        Vector2 pointer,
        Vector2 actorCenter,
        float authoredRadius,
        out float distance)
    {
        distance = Vector2.Distance(pointer, actorCenter);
        return IsFinite(pointer) &&
               IsFinite(actorCenter) &&
               IsFinite(distance) &&
               distance <= ResolveActorHitRadius(authoredRadius);
    }

    public static float ResolveAllySpacingRadius(float minimumSpacing)
    {
        return IsFinite(minimumSpacing)
            ? Mathf.Max(0f, minimumSpacing) * 0.5f
            : 0f;
    }

    public static float ResolveEnemyFormationRadius(
        float formationRadius,
        float separationRatio)
    {
        if (!IsFinite(formationRadius) || !IsFinite(separationRatio))
            return 0f;

        return Mathf.Max(0f, formationRadius) *
               Mathf.Max(0f, separationRatio);
    }

    public static Vector2 ResolveEnemyCoreReachEnd(
        Vector2 enemyPosition,
        float defenseLineRadius,
        float coreAttackRange)
    {
        if (!IsFinite(enemyPosition))
            return Vector2.zero;

        float currentRadius = enemyPosition.magnitude;
        if (currentRadius <= 0.0001f)
            return Vector2.zero;

        float minimumRadius = IsFinite(defenseLineRadius)
            ? Mathf.Max(0f, defenseLineRadius)
            : 0f;
        float reach = IsFinite(coreAttackRange)
            ? Mathf.Max(0f, coreAttackRange)
            : 0f;
        float reachedRadius = Mathf.Max(
            minimumRadius,
            currentRadius - reach);
        return enemyPosition / currentRadius * reachedRadius;
    }

    public static int AppendProjectedGroundCircle(
        Vector2 center,
        float radius,
        int segmentCount,
        PracticeBattleDebugGroundProjector projector,
        PracticeBattleDebugLineConsumer addLine)
    {
        if (!IsFinite(center) || !IsFinitePositive(radius) ||
            projector == null || addLine == null)
        {
            return 0;
        }

        segmentCount = Mathf.Clamp(segmentCount, 8, 128);
        int appended = 0;
        for (int index = 0; index < segmentCount; index++)
        {
            float startRadians = index * Mathf.PI * 2f / segmentCount;
            float endRadians = (index + 1) * Mathf.PI * 2f /
                               segmentCount;
            Vector2 startGround = center + new Vector2(
                Mathf.Cos(startRadians),
                Mathf.Sin(startRadians)) * radius;
            Vector2 endGround = center + new Vector2(
                Mathf.Cos(endRadians),
                Mathf.Sin(endRadians)) * radius;
            if (!projector(startGround, out Vector2 start) ||
                !projector(endGround, out Vector2 end) ||
                !IsFinite(start) || !IsFinite(end))
            {
                continue;
            }

            addLine(start, end);
            appended++;
        }
        return appended;
    }

    public static bool IsFinitePositive(float value)
    {
        return IsFinite(value) && value > 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }
}
