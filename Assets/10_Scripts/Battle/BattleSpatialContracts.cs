using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared defaults for battle-space card and ability operations. Distances
/// are expressed in the circular battle world's ground-plane units.
/// </summary>
public static class BattleSpatialDefaults
{
    public const float NearbyRadius = 1.5f;
    public const float MovementStep = 1f;
    public const float InnerZoneRadiusRatio = 0.5f;
    public const float RecentCoreAttackWindow = 5f;
    public const float BehindHalfAngle = 35f;
}

public enum BattleSpatialZone
{
    Unknown = 0,
    Inner = 1,
    Outer = 2,
    DefenseLine = 3,
}

public interface IBattleSpatialServiceProvider
{
    IBattleSpatialService SpatialService { get; }
}

/// <summary>
/// Owner-neutral spatial boundary used by cards, items, characters, enemies,
/// and status-triggered effects. Implementations own collision, arena-boundary,
/// and presentation synchronization rules.
/// </summary>
public interface IBattleSpatialService
{
    bool IsAvailable { get; }
    float ArenaRadius { get; }
    float InnerZoneBoundaryRadius { get; }

    bool TryGetUnitPosition(
        BattleStatusTarget target,
        out Vector2 position);

    BattleSpatialZone GetUnitZone(BattleStatusTarget target);

    IReadOnlyList<EnemyRuntime> SelectNearbyEnemies(
        BattleStatusTarget anchor,
        float radius = BattleSpatialDefaults.NearbyRadius,
        int maximumCount = 0,
        bool includeAnchor = false);

    IReadOnlyList<EnemyRuntime> SelectEnemiesBehind(
        EnemyRuntime anchor,
        float maximumDistance = BattleSpatialDefaults.NearbyRadius,
        int maximumCount = 1,
        float halfAngle = BattleSpatialDefaults.BehindHalfAngle);

    IReadOnlyList<EnemyRuntime> SelectDefenseLineEnemies();

    IReadOnlyList<EnemyRuntime> SelectRecentCoreAttackers(
        float lookbackSeconds =
            BattleSpatialDefaults.RecentCoreAttackWindow);

    int MoveAlliesCoreward(
        IReadOnlyList<IBattleCharacter> targets,
        float distance = BattleSpatialDefaults.MovementStep);

    int MoveAlliesOutward(
        IReadOnlyList<IBattleCharacter> targets,
        float distance = BattleSpatialDefaults.MovementStep);

    int MoveAlliesToOuterZone(
        IReadOnlyList<IBattleCharacter> targets);

    int MoveAlliesToPoint(
        IReadOnlyList<IBattleCharacter> targets,
        Vector2 point,
        bool instant = false);

    int MoveAlliesToEnemyFlank(
        IReadOnlyList<IBattleCharacter> targets,
        EnemyRuntime enemy,
        float flankDistance = BattleSpatialDefaults.MovementStep,
        bool instant = false);

    bool TrySwapAllies(
        IBattleCharacter first,
        IBattleCharacter second);

    int PullEnemiesTowardPoint(
        IReadOnlyList<EnemyRuntime> targets,
        Vector2 point,
        float distance = BattleSpatialDefaults.MovementStep);
}
