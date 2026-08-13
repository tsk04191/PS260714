using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleArenaMode
{
    LegacyGrid = 0,
    CircularDefense = 1,
}

public sealed class BattleArenaSetup
{
    public const int DefaultCoreMaximumHealth = 100;
    public const int DefaultLaneCount = 12;
    public const float DefaultWallRadiusNormalized = 0.24f;
    public const float DefaultSpawnRadiusNormalized = 0.45f;
    public const float DefaultWorldRadius = 2.2666667f;
    public const float MinimumWorldRadius = 0.5f;
    public const float MaximumWorldRadius = 10f;

    public static BattleArenaSetup Legacy { get; } = new(
        BattleArenaMode.LegacyGrid,
        0,
        0,
        0f,
        0f,
        0f);

    public BattleArenaMode Mode { get; }
    public int CoreMaximumHealth { get; }
    public int LaneCount { get; }
    public float WallRadiusNormalized { get; }
    public float SpawnRadiusNormalized { get; }
    public float WorldRadius { get; }
    public bool UsesBattleCore => Mode == BattleArenaMode.CircularDefense;

    public BattleArenaSetup(
        BattleArenaMode mode,
        int coreMaximumHealth,
        int laneCount,
        float wallRadiusNormalized,
        float spawnRadiusNormalized,
        float worldRadius = DefaultWorldRadius)
    {
        Mode = mode;
        if (mode != BattleArenaMode.CircularDefense)
        {
            CoreMaximumHealth = 0;
            LaneCount = 0;
            WallRadiusNormalized = 0f;
            SpawnRadiusNormalized = 0f;
            WorldRadius = 0f;
            return;
        }

        CoreMaximumHealth = Mathf.Max(1, coreMaximumHealth);
        LaneCount = Mathf.Clamp(laneCount, 4, 64);
        WallRadiusNormalized = Mathf.Clamp(
            wallRadiusNormalized,
            0.12f,
            0.4f);
        SpawnRadiusNormalized = Mathf.Clamp(
            spawnRadiusNormalized,
            WallRadiusNormalized + 0.05f,
            0.5f);
        WorldRadius = NormalizeWorldRadius(worldRadius);
    }

    public static BattleArenaSetup CreateCircular(
        int coreMaximumHealth = DefaultCoreMaximumHealth,
        int laneCount = DefaultLaneCount,
        float wallRadiusNormalized = DefaultWallRadiusNormalized,
        float spawnRadiusNormalized = DefaultSpawnRadiusNormalized,
        float worldRadius = DefaultWorldRadius)
    {
        return new BattleArenaSetup(
            BattleArenaMode.CircularDefense,
            coreMaximumHealth,
            laneCount,
            wallRadiusNormalized,
            spawnRadiusNormalized,
            worldRadius);
    }

    public BattleArenaSetup WithWorldRadius(float worldRadius)
    {
        return UsesBattleCore
            ? new BattleArenaSetup(
                Mode,
                CoreMaximumHealth,
                LaneCount,
                WallRadiusNormalized,
                SpawnRadiusNormalized,
                worldRadius)
            : this;
    }

    public BattleArenaSetup WithCoreMaximumHealth(int maximumHealth)
    {
        return UsesBattleCore
            ? new BattleArenaSetup(
                Mode,
                maximumHealth,
                LaneCount,
                WallRadiusNormalized,
                SpawnRadiusNormalized,
                WorldRadius)
            : this;
    }

    public static float NormalizeWorldRadius(float worldRadius)
    {
        if (float.IsNaN(worldRadius) || float.IsInfinity(worldRadius))
            return DefaultWorldRadius;
        return Mathf.Clamp(
            worldRadius,
            MinimumWorldRadius,
            MaximumWorldRadius);
    }
}

public sealed class BattleEnvironmentSetup
{
    public const float DefaultCameraFieldOfView = 40f;

    public static BattleEnvironmentSetup Default { get; } = new(
        null,
        Color.white,
        new Color(0.018f, 0.014f, 0.01f, 1f),
        DefaultCameraFieldOfView);

    public Sprite Backdrop { get; }
    public Color BackdropTint { get; }
    public Color ClearColor { get; }
    public float CameraFieldOfView { get; }

    public BattleEnvironmentSetup(
        Sprite backdrop,
        Color backdropTint,
        Color clearColor,
        float cameraFieldOfView)
    {
        Backdrop = backdrop;
        BackdropTint = backdropTint;
        ClearColor = clearColor;
        CameraFieldOfView = Mathf.Clamp(cameraFieldOfView, 25f, 65f);
    }
}

public interface IBattleObjective
{
    bool IsActive { get; }
    int CurrentHealth { get; }
    int MaximumHealth { get; }
    bool IsDestroyed { get; }
    event Action<int, int> HealthChanged;
    event Action Destroyed;
}

public interface IBattleObjectiveProvider
{
    IBattleObjective Objective { get; }
}

public sealed class BattleCoreRuntime : IBattleObjective
{
    public bool IsActive { get; private set; }
    public int CurrentHealth { get; private set; }
    public int MaximumHealth { get; private set; }
    public bool IsDestroyed => IsActive && CurrentHealth <= 0;

    public event Action<int, int> HealthChanged;
    public event Action Destroyed;

    public void Configure(
        int maximumHealth,
        bool active,
        int currentHealth = -1)
    {
        IsActive = active;
        MaximumHealth = active ? Mathf.Max(1, maximumHealth) : 0;
        CurrentHealth = active
            ? currentHealth < 0
                ? MaximumHealth
                : Mathf.Clamp(currentHealth, 0, MaximumHealth)
            : 0;
        HealthChanged?.Invoke(CurrentHealth, MaximumHealth);
    }

    public int TakeDamage(int amount)
    {
        if (!IsActive || IsDestroyed || amount <= 0)
            return 0;

        int applied = Mathf.Min(CurrentHealth, amount);
        CurrentHealth -= applied;
        HealthChanged?.Invoke(CurrentHealth, MaximumHealth);
        if (CurrentHealth <= 0)
            Destroyed?.Invoke();
        return applied;
    }

    public int Heal(int amount)
    {
        if (!IsActive || IsDestroyed || amount <= 0 ||
            CurrentHealth >= MaximumHealth)
        {
            return 0;
        }

        int previous = CurrentHealth;
        CurrentHealth = Mathf.Min(MaximumHealth, CurrentHealth + amount);
        HealthChanged?.Invoke(CurrentHealth, MaximumHealth);
        return CurrentHealth - previous;
    }
}

public enum EBattleResult
{
    None,
    Victory,
    Timeout,
    Aborted,
    Defeat,
}

public readonly struct BattleEnemyGradeCounts
{
    public int Normal { get; }
    public int Special { get; }
    public int Elite { get; }
    public int Boss { get; }
    public int Total => Normal + Special + Elite + Boss;

    public BattleEnemyGradeCounts(
        int normal,
        int special,
        int elite,
        int boss)
    {
        Normal = normal;
        Special = special;
        Elite = elite;
        Boss = boss;
    }

    public int Get(EEnemyGrade grade)
    {
        return grade switch
        {
            EEnemyGrade.Special => Special,
            EEnemyGrade.Elite => Elite,
            EEnemyGrade.Boss => Boss,
            _ => Normal,
        };
    }
}

public sealed class BattleSetup
{
    public int FieldSize { get; }
    public int MaximumStackSize { get; }
    public int InitialEnemyCount { get; }
    public float SpawnInterval { get; }
    public float TimeLimit { get; }
    public BattleEnemyGradeCounts GradeCounts { get; }
    public IReadOnlyList<EnemyRuntime> Enemies { get; }
    public BattleArenaSetup Arena { get; }
    public BattleEnvironmentSetup Environment { get; }

    public BattleSetup(
        int fieldSize,
        int maximumStackSize,
        float spawnInterval,
        float timeLimit,
        BattleEnemyGradeCounts gradeCounts,
        List<EnemyRuntime> enemies,
        int initialEnemyCount = 0,
        BattleArenaSetup arena = null,
        BattleEnvironmentSetup environment = null)
    {
        FieldSize = fieldSize;
        MaximumStackSize = maximumStackSize;
        SpawnInterval = TimePrecision.Normalize(spawnInterval, 0.1f);
        TimeLimit = TimePrecision.FloorToTenth(timeLimit);
        GradeCounts = gradeCounts;
        Enemies = enemies != null
            ? enemies.AsReadOnly()
            : new List<EnemyRuntime>().AsReadOnly();
        Arena = arena ?? BattleArenaSetup.Legacy;
        Environment = environment ?? BattleEnvironmentSetup.Default;
        int defaultInitialCount = Arena.UsesBattleCore
            ? Arena.LaneCount
            : fieldSize * fieldSize;
        InitialEnemyCount = System.Math.Min(
            Enemies.Count,
            System.Math.Max(
                0,
                initialEnemyCount > 0
                    ? initialEnemyCount
                    : defaultInitialCount));
    }
}
