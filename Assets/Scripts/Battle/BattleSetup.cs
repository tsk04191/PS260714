using System.Collections.Generic;

public enum EBattleResult
{
    None,
    Victory,
    Timeout,
    Aborted,
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
        List<EnemyRuntime> enemies)
    {
        FieldSize = fieldSize;
        MaximumStackSize = maximumStackSize;
        SpawnInterval = spawnInterval;
        TimeLimit = timeLimit;
        GradeCounts = gradeCounts;
        Enemies = enemies != null
            ? enemies.AsReadOnly()
            : new List<EnemyRuntime>().AsReadOnly();
    }
}
