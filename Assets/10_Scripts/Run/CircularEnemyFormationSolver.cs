using System.Collections.Generic;
using UnityEngine;

internal readonly struct CircularEnemyFormationBody
{
    public Vector2 Position { get; }
    public float Radius { get; }
    public int ContactDepth { get; }

    public CircularEnemyFormationBody(
        Vector2 position,
        float radius,
        int contactDepth)
    {
        Position = position;
        Radius = Mathf.Max(0f, radius);
        ContactDepth = Mathf.Max(0, contactDepth);
    }
}

internal readonly struct CircularEnemyFormationSolution
{
    public Vector2 TargetPosition { get; }
    public int ContactDepth { get; }
    public int BlockerIndex { get; }

    public CircularEnemyFormationSolution(
        Vector2 targetPosition,
        int contactDepth,
        int blockerIndex)
    {
        TargetPosition = targetPosition;
        ContactDepth = Mathf.Max(0, contactDepth);
        BlockerIndex = blockerIndex;
    }
}

/// <summary>
/// Allocation-free geometry used by the circular battle board for seeded
/// spawn sampling, forward-space search, and swept collision checks.
/// </summary>
internal static class CircularEnemyFormationSolver
{
    public const int MaximumDirectionAttempts = 256;
    public const int ForwardSearchSamplePairs = 6;

    private const float GoldenRatioConjugate = 0.61803398875f;
    private const float GeometryTolerance = 0.0001f;

    public static Vector2 ResolveDeterministicDirection(
        int formationSeed,
        int stableOrder,
        int attempt)
    {
        float initialSample = ResolveDeterministicSample(
            formationSeed,
            stableOrder,
            0xA511E9B3u);
        float sample = Mathf.Repeat(
            initialSample + Mathf.Max(0, attempt) * GoldenRatioConjugate,
            1f);
        float angle = sample * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    public static float ResolveDeterministicSample(
        int formationSeed,
        int stableOrder,
        uint salt)
    {
        uint value = unchecked((uint)formationSeed);
        value ^= unchecked((uint)stableOrder) * 0x9E3779B9u;
        value ^= salt;
        value = Mix(value);
        return (value & 0x00FFFFFFu) / 16777216f;
    }

    public static float ResolveMinimumSeparation(
        float firstRadius,
        float secondRadius,
        float layerSpacing,
        float separationRatio)
    {
        float radiusSeparation =
            (Mathf.Max(0f, firstRadius) + Mathf.Max(0f, secondRadius)) *
            Mathf.Max(0f, separationRatio);
        return Mathf.Max(Mathf.Max(0f, layerSpacing), radiusSeparation);
    }

    public static bool TryResolveTarget(
        Vector2 direction,
        float radius,
        float defenseLineRadius,
        float layerSpacing,
        float separationRatio,
        IReadOnlyList<CircularEnemyFormationBody> settledBodies,
        IReadOnlyList<int> depthCounts,
        int maximumEnemiesPerLayer,
        int maximumLayerCount,
        out CircularEnemyFormationSolution solution)
    {
        solution = default;
        direction = NormalizeDirection(direction);
        defenseLineRadius = Mathf.Max(0f, defenseLineRadius);

        float targetRadius = defenseLineRadius;
        int blockerIndex = -1;
        int contactDepth = 0;
        if (settledBodies != null)
        {
            for (int index = 0; index < settledBodies.Count; index++)
            {
                CircularEnemyFormationBody blocker = settledBodies[index];
                float separation = ResolveMinimumSeparation(
                    radius,
                    blocker.Radius,
                    layerSpacing,
                    separationRatio);
                if (!TryResolveOuterContactRadius(
                        direction,
                        blocker.Position,
                        separation,
                        out float blockedRadius) ||
                    blockedRadius <= targetRadius + GeometryTolerance)
                {
                    continue;
                }

                targetRadius = blockedRadius;
                blockerIndex = index;
                contactDepth = blocker.ContactDepth + 1;
            }
        }

        maximumEnemiesPerLayer = Mathf.Max(1, maximumEnemiesPerLayer);
        maximumLayerCount = Mathf.Max(1, maximumLayerCount);
        if (contactDepth >= maximumLayerCount ||
            depthCounts != null &&
            contactDepth < depthCounts.Count &&
            depthCounts[contactDepth] >= maximumEnemiesPerLayer)
        {
            return false;
        }

        solution = new CircularEnemyFormationSolution(
            direction * targetRadius,
            contactDepth,
            blockerIndex);
        return true;
    }

    public static float ResolveSafeRadialTarget(
        Vector2 direction,
        float minimumRadius,
        float radius,
        float layerSpacing,
        float separationRatio,
        IReadOnlyList<CircularEnemyFormationBody> blockers)
    {
        direction = NormalizeDirection(direction);
        float result = Mathf.Max(0f, minimumRadius);
        if (blockers == null)
            return result;

        for (int index = 0; index < blockers.Count; index++)
        {
            CircularEnemyFormationBody blocker = blockers[index];
            float separation = ResolveMinimumSeparation(
                radius,
                blocker.Radius,
                layerSpacing,
                separationRatio);
            if (TryResolveOuterContactRadius(
                    direction,
                    blocker.Position,
                    separation,
                    out float blockedRadius))
            {
                result = Mathf.Max(result, blockedRadius);
            }
        }

        return result;
    }

    public static Vector2 ResolveSafeSegmentEnd(
        Vector2 start,
        Vector2 requestedEnd,
        float radius,
        float layerSpacing,
        float separationRatio,
        IReadOnlyList<CircularEnemyFormationBody> blockers)
    {
        Vector2 movement = requestedEnd - start;
        float movementLengthSquared = movement.sqrMagnitude;
        if (movementLengthSquared <=
            GeometryTolerance * GeometryTolerance || blockers == null)
        {
            return requestedEnd;
        }

        float allowedFraction = 1f;
        for (int index = 0; index < blockers.Count; index++)
        {
            CircularEnemyFormationBody blocker = blockers[index];
            float separation = ResolveMinimumSeparation(
                radius,
                blocker.Radius,
                layerSpacing,
                separationRatio);
            Vector2 offset = start - blocker.Position;
            float separationSquared = separation * separation;
            float startDistanceSquared = offset.sqrMagnitude;

            float closestFraction = Mathf.Clamp01(
                -Vector2.Dot(offset, movement) /
                movementLengthSquared);
            float closestDistanceSquared =
                (offset + movement * closestFraction).sqrMagnitude;
            if (closestDistanceSquared >=
                separationSquared - GeometryTolerance)
            {
                // Capsule-path followers commonly sit on a tangent to the
                // blocker path. Tiny floating-point closing components must
                // not turn that safe tangency into a permanent hard stop.
                continue;
            }

            // A touching body may move away or tangentially without being
            // pinned by the contact it is already resolving.
            if (startDistanceSquared <=
                separationSquared + GeometryTolerance)
            {
                if (Vector2.Dot(movement, offset) >= 0f)
                    continue;
                allowedFraction = 0f;
                break;
            }

            float linear = 2f * Vector2.Dot(offset, movement);
            float constant = startDistanceSquared - separationSquared;
            float discriminant = linear * linear -
                                 4f * movementLengthSquared * constant;
            if (discriminant < 0f)
                continue;

            float root = (-linear - Mathf.Sqrt(discriminant)) /
                         (2f * movementLengthSquared);
            if (root >= 0f && root <= allowedFraction)
                allowedFraction = Mathf.Clamp01(root);
        }

        return start + movement * allowedFraction;
    }

    public static Vector2 ResolveForwardSearchEnd(
        Vector2 start,
        Vector2 requestedEnd,
        float forwardSearchAngleDegrees,
        int stableOrder,
        float radius,
        float layerSpacing,
        float separationRatio,
        IReadOnlyList<CircularEnemyFormationBody> blockers)
    {
        Vector2 requestedMovement = requestedEnd - start;
        float requestedDistance = requestedMovement.magnitude;
        if (requestedDistance <= GeometryTolerance)
            return requestedEnd;

        Vector2 forward = requestedMovement / requestedDistance;
        float lookAheadDistance = requestedDistance;
        if (blockers != null)
        {
            for (int index = 0; index < blockers.Count; index++)
            {
                float separation = ResolveMinimumSeparation(
                    radius,
                    blockers[index].Radius,
                    layerSpacing,
                    separationRatio);
                lookAheadDistance = Mathf.Max(
                    lookAheadDistance,
                    separation * 2f + GeometryTolerance);
            }
        }

        Vector2 directLookAheadEnd = start +
                                     forward * lookAheadDistance;
        Vector2 safeDirectLookAhead = ResolveSafeSegmentEnd(
            start,
            directLookAheadEnd,
            radius,
            layerSpacing,
            separationRatio,
            blockers);
        Vector2 bestLookAheadMovement = safeDirectLookAhead - start;
        Vector2 bestEnd = ResolveSafeSegmentEnd(
            start,
            requestedEnd,
            radius,
            layerSpacing,
            separationRatio,
            blockers);
        Vector2 bestMovement = bestEnd - start;
        float bestForwardProgress = Vector2.Dot(bestMovement, forward);
        float bestDistanceSquared = bestMovement.sqrMagnitude;
        if (bestLookAheadMovement.magnitude >= lookAheadDistance -
            GeometryTolerance)
        {
            return bestEnd;
        }

        float halfAngle = Mathf.Clamp(
            forwardSearchAngleDegrees,
            0f,
            180f) * 0.5f;
        if (halfAngle <= GeometryTolerance)
            return bestEnd;

        bool searchesLeftFirst = (stableOrder & 1) == 0;
        for (int sample = 1;
             sample <= ForwardSearchSamplePairs;
             sample++)
        {
            float angle = halfAngle * sample /
                          ForwardSearchSamplePairs;
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0
                    ? searchesLeftFirst
                    : !searchesLeftFirst;
                float signedAngle = left ? angle : -angle;
                Vector2 candidateDirection = RotateDegrees(
                    forward,
                    signedAngle);
                Vector2 candidateLookAheadEnd = start +
                    candidateDirection * lookAheadDistance;
                Vector2 safeLookAheadEnd = ResolveSafeSegmentEnd(
                    start,
                    candidateLookAheadEnd,
                    radius,
                    layerSpacing,
                    separationRatio,
                    blockers);
                Vector2 safeLookAheadMovement =
                    safeLookAheadEnd - start;
                Vector2 candidateEnd = start +
                                       candidateDirection *
                                       requestedDistance;
                Vector2 safeEnd = ResolveSafeSegmentEnd(
                    start,
                    candidateEnd,
                    radius,
                    layerSpacing,
                    separationRatio,
                    blockers);
                Vector2 safeMovement = safeEnd - start;
                float forwardProgress = Vector2.Dot(
                    safeMovement,
                    forward);
                if (safeLookAheadMovement.magnitude >=
                    lookAheadDistance - GeometryTolerance &&
                    safeMovement.magnitude >= requestedDistance -
                    GeometryTolerance &&
                    forwardProgress > GeometryTolerance)
                {
                    // Samples are ordered from the smallest steering angle,
                    // so the first fully open path is the most natural one.
                    return safeEnd;
                }

                float safeDistanceSquared = safeMovement.sqrMagnitude;
                if (forwardProgress > bestForwardProgress +
                    GeometryTolerance ||
                    Mathf.Abs(
                        forwardProgress - bestForwardProgress) <=
                    GeometryTolerance &&
                    safeDistanceSquared > bestDistanceSquared)
                {
                    bestEnd = safeEnd;
                    bestForwardProgress = forwardProgress;
                    bestDistanceSquared = safeDistanceSquared;
                }
            }
        }

        return bestForwardProgress > GeometryTolerance
            ? bestEnd
            : start;
    }

    public static float ResolveMinimumRadialPathDistanceSquared(
        Vector2 firstDirection,
        float firstMinimumRadius,
        float firstMaximumRadius,
        Vector2 secondDirection,
        float secondMinimumRadius,
        float secondMaximumRadius)
    {
        firstDirection = NormalizeDirection(firstDirection);
        secondDirection = NormalizeDirection(secondDirection);
        NormalizeInterval(
            ref firstMinimumRadius,
            ref firstMaximumRadius);
        NormalizeInterval(
            ref secondMinimumRadius,
            ref secondMaximumRadius);

        float cosine = Mathf.Clamp(
            Vector2.Dot(firstDirection, secondDirection),
            -1f,
            1f);
        float result = float.PositiveInfinity;
        EvaluateRadialPair(
            firstMinimumRadius,
            secondMinimumRadius,
            cosine,
            ref result);
        EvaluateRadialPair(
            firstMinimumRadius,
            secondMaximumRadius,
            cosine,
            ref result);
        EvaluateRadialPair(
            firstMaximumRadius,
            secondMinimumRadius,
            cosine,
            ref result);
        EvaluateRadialPair(
            firstMaximumRadius,
            secondMaximumRadius,
            cosine,
            ref result);

        float projectedFirstAtSecondMinimum = Mathf.Clamp(
            secondMinimumRadius * cosine,
            firstMinimumRadius,
            firstMaximumRadius);
        float projectedFirstAtSecondMaximum = Mathf.Clamp(
            secondMaximumRadius * cosine,
            firstMinimumRadius,
            firstMaximumRadius);
        EvaluateRadialPair(
            projectedFirstAtSecondMinimum,
            secondMinimumRadius,
            cosine,
            ref result);
        EvaluateRadialPair(
            projectedFirstAtSecondMaximum,
            secondMaximumRadius,
            cosine,
            ref result);

        float projectedSecondAtFirstMinimum = Mathf.Clamp(
            firstMinimumRadius * cosine,
            secondMinimumRadius,
            secondMaximumRadius);
        float projectedSecondAtFirstMaximum = Mathf.Clamp(
            firstMaximumRadius * cosine,
            secondMinimumRadius,
            secondMaximumRadius);
        EvaluateRadialPair(
            firstMinimumRadius,
            projectedSecondAtFirstMinimum,
            cosine,
            ref result);
        EvaluateRadialPair(
            firstMaximumRadius,
            projectedSecondAtFirstMaximum,
            cosine,
            ref result);
        return Mathf.Max(0f, result);
    }

    public static bool TryResolveOuterContactRadius(
        Vector2 direction,
        Vector2 blockerPosition,
        float separation,
        out float outerRadius)
    {
        outerRadius = 0f;
        direction = NormalizeDirection(direction);
        separation = Mathf.Max(0f, separation);
        if (separation <= GeometryTolerance)
            return false;

        float projected = Vector2.Dot(direction, blockerPosition);
        float perpendicularSquared = Mathf.Max(
            0f,
            blockerPosition.sqrMagnitude - projected * projected);
        float separationSquared = separation * separation;
        if (perpendicularSquared > separationSquared + GeometryTolerance)
            return false;

        float radialOffset = Mathf.Sqrt(Mathf.Max(
            0f,
            separationSquared - perpendicularSquared));
        outerRadius = projected + radialOffset;
        return outerRadius > GeometryTolerance;
    }

    public static bool TryResolveOuterCapsuleContactRadius(
        Vector2 direction,
        Vector2 segmentStart,
        Vector2 segmentEnd,
        float separation,
        out float outerRadius)
    {
        outerRadius = 0f;
        direction = NormalizeDirection(direction);
        separation = Mathf.Max(0f, separation);
        if (separation <= GeometryTolerance)
            return false;

        Vector2 segment = segmentEnd - segmentStart;
        float longitudinalStart = Vector2.Dot(
            direction,
            segmentStart);
        float lateralStart = Cross(direction, segmentStart);
        float longitudinalDelta = Vector2.Dot(direction, segment);
        float lateralDelta = Cross(direction, segment);
        bool found = false;

        EvaluateCapsuleContactCandidate(
            0f,
            longitudinalStart,
            lateralStart,
            longitudinalDelta,
            lateralDelta,
            separation,
            ref found,
            ref outerRadius);
        EvaluateCapsuleContactCandidate(
            1f,
            longitudinalStart,
            lateralStart,
            longitudinalDelta,
            lateralDelta,
            separation,
            ref found,
            ref outerRadius);

        if (Mathf.Abs(lateralDelta) <= GeometryTolerance)
            return found;

        EvaluateCapsuleContactCandidate(
            (separation - lateralStart) / lateralDelta,
            longitudinalStart,
            lateralStart,
            longitudinalDelta,
            lateralDelta,
            separation,
            ref found,
            ref outerRadius);
        EvaluateCapsuleContactCandidate(
            (-separation - lateralStart) / lateralDelta,
            longitudinalStart,
            lateralStart,
            longitudinalDelta,
            lateralDelta,
            separation,
            ref found,
            ref outerRadius);

        float segmentLength = segment.magnitude;
        if (segmentLength > GeometryTolerance)
        {
            float stationaryLateral = separation * longitudinalDelta *
                                      Mathf.Sign(lateralDelta) /
                                      segmentLength;
            EvaluateCapsuleContactCandidate(
                (stationaryLateral - lateralStart) / lateralDelta,
                longitudinalStart,
                lateralStart,
                longitudinalDelta,
                lateralDelta,
                separation,
                ref found,
                ref outerRadius);
        }

        return found;
    }

    private static Vector2 NormalizeDirection(Vector2 direction)
    {
        return direction.sqrMagnitude > GeometryTolerance * GeometryTolerance
            ? direction.normalized
            : Vector2.up;
    }

    private static Vector2 RotateDegrees(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(
            direction.x * cosine - direction.y * sine,
            direction.x * sine + direction.y * cosine);
    }

    private static void NormalizeInterval(
        ref float minimum,
        ref float maximum)
    {
        minimum = Mathf.Max(0f, minimum);
        maximum = Mathf.Max(0f, maximum);
        if (minimum <= maximum)
            return;
        (minimum, maximum) = (maximum, minimum);
    }

    private static void EvaluateRadialPair(
        float firstRadius,
        float secondRadius,
        float cosine,
        ref float minimumDistanceSquared)
    {
        float distanceSquared = Mathf.Max(
            0f,
            firstRadius * firstRadius + secondRadius * secondRadius -
            2f * firstRadius * secondRadius * cosine);
        minimumDistanceSquared = Mathf.Min(
            minimumDistanceSquared,
            distanceSquared);
    }

    private static void EvaluateCapsuleContactCandidate(
        float segmentFraction,
        float longitudinalStart,
        float lateralStart,
        float longitudinalDelta,
        float lateralDelta,
        float separation,
        ref bool found,
        ref float outerRadius)
    {
        if (segmentFraction < -GeometryTolerance ||
            segmentFraction > 1f + GeometryTolerance)
        {
            return;
        }

        float fraction = Mathf.Clamp01(segmentFraction);
        float lateral = lateralStart + lateralDelta * fraction;
        float radialSquared = separation * separation -
                              lateral * lateral;
        if (radialSquared < -GeometryTolerance)
            return;

        float longitudinal = longitudinalStart +
                             longitudinalDelta * fraction;
        float candidate = longitudinal +
                          Mathf.Sqrt(Mathf.Max(0f, radialSquared));
        if (candidate <= GeometryTolerance)
            return;

        found = true;
        outerRadius = Mathf.Max(outerRadius, candidate);
    }

    private static float Cross(Vector2 left, Vector2 right)
    {
        return left.x * right.y - left.y * right.x;
    }

    private static uint Mix(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value;
    }
}
