using System.Collections.Generic;

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

    public BattleSetup(
        int fieldSize,
        int maximumStackSize,
        float spawnInterval,
        float timeLimit,
        BattleEnemyGradeCounts gradeCounts,
        List<EnemyRuntime> enemies,
        int initialEnemyCount = 0)
    {
        FieldSize = fieldSize;
        MaximumStackSize = maximumStackSize;
        SpawnInterval = TimePrecision.Normalize(spawnInterval, 0.1f);
        TimeLimit = TimePrecision.FloorToTenth(timeLimit);
        GradeCounts = gradeCounts;
        Enemies = enemies != null
            ? enemies.AsReadOnly()
            : new List<EnemyRuntime>().AsReadOnly();
        int defaultInitialCount = fieldSize * fieldSize;
        InitialEnemyCount = System.Math.Min(
            Enemies.Count,
            System.Math.Max(
                0,
                initialEnemyCount > 0
                    ? initialEnemyCount
                    : defaultInitialCount));
    }
}
