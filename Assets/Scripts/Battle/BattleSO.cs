using System;
using System.Collections.Generic;
using UnityEngine;

public enum EEnemyCompositionMode
{
    FixedCount,
    Ratio,
}

[Serializable]
public sealed class BattleEnemyGradeRule
{
    [SerializeField, HideInInspector] private EEnemyGrade grade;
    [SerializeField, Min(0)] private int count;
    [SerializeField, Min(0f)] private float ratio;
    [SerializeField] private List<EnemySO> enemyPool = new();

    public EEnemyGrade Grade => grade;
    public int Count => count;
    public float Ratio => ratio;
    public IReadOnlyList<EnemySO> EnemyPool => enemyPool;

    public BattleEnemyGradeRule(
        EEnemyGrade grade,
        int count,
        float ratio)
    {
        this.grade = grade;
        this.count = Mathf.Max(0, count);
        this.ratio = Mathf.Max(0f, ratio);
    }

    internal void ValidateValues(EEnemyGrade expectedGrade)
    {
        grade = expectedGrade;
        count = Mathf.Max(0, count);
        ratio = Mathf.Max(0f, ratio);
        enemyPool ??= new List<EnemySO>();
    }
}

[CreateAssetMenu(fileName = "Battle", menuName = "Dungeon/Battle")]
public sealed class BattleSO : ScriptableObject
{
    public const float DefaultTimeLimit = 180f;

    [Header("Identity")]
    [SerializeField] private string battleId = "first_battle";
    [SerializeField] private string displayName = "FIRST BATTLE";

    [Header("Field")]
    [SerializeField, Range(DungeonBoardView.MinimumGridSize, DungeonBoardView.MaximumGridSize)]
    private int fieldSize = 4;
    [SerializeField, Range(1, 20)] private int maximumStackSize = 8;

    [Header("Enemy Spawn")]
    [SerializeField, Min(1)] private int totalEnemyCount = 20;
    [SerializeField, Min(1)] private int minimumEnemyHealth = 20;
    [SerializeField, Min(0)] private int randomHealthBonus = 8;
    [SerializeField, Min(0.1f)] private float spawnInterval = 4f;
    [SerializeField] private EEnemyCompositionMode compositionMode;

    [Header("Grade Composition")]
    [SerializeField] private BattleEnemyGradeRule normalEnemies =
        new(EEnemyGrade.Normal, 20, 70f);
    [SerializeField] private BattleEnemyGradeRule specialEnemies =
        new(EEnemyGrade.Special, 0, 20f);
    [SerializeField] private BattleEnemyGradeRule eliteEnemies =
        new(EEnemyGrade.Elite, 0, 8f);
    [SerializeField] private BattleEnemyGradeRule bossEnemies =
        new(EEnemyGrade.Boss, 0, 2f);

    [Header("Time Limit")]
    [SerializeField, Min(1f)] private float timeLimit = DefaultTimeLimit;

    public string BattleId => battleId;
    public string DisplayName => displayName;
    public int FieldSize => fieldSize;
    public int MaximumStackSize => maximumStackSize;
    public int TotalEnemyCount => totalEnemyCount;
    public int MinimumEnemyHealth => minimumEnemyHealth;
    public int RandomHealthBonus => randomHealthBonus;
    public float SpawnInterval => spawnInterval;
    public EEnemyCompositionMode CompositionMode => compositionMode;
    public float TimeLimit => timeLimit;

    private void OnValidate()
    {
        battleId = string.IsNullOrWhiteSpace(battleId)
            ? name.ToLowerInvariant().Replace(' ', '_')
            : battleId.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName)
            ? name.ToUpperInvariant()
            : displayName.Trim();
        fieldSize = Mathf.Clamp(
            fieldSize,
            DungeonBoardView.MinimumGridSize,
            DungeonBoardView.MaximumGridSize);
        maximumStackSize = Mathf.Clamp(maximumStackSize, 1, 20);
        totalEnemyCount = Mathf.Max(1, totalEnemyCount);
        minimumEnemyHealth = Mathf.Max(1, minimumEnemyHealth);
        randomHealthBonus = Mathf.Max(0, randomHealthBonus);
        spawnInterval = Mathf.Max(0.1f, spawnInterval);
        timeLimit = Mathf.Max(1f, timeLimit);
        EnsureRules();
    }

    public bool TryGetGradeCounts(
        out BattleEnemyGradeCounts counts,
        out string error)
    {
        EnsureRules();
        BattleEnemyGradeRule[] rules = GetRules();
        int[] resolvedCounts = new int[rules.Length];

        if (compositionMode == EEnemyCompositionMode.FixedCount)
        {
            int total = 0;
            for (int index = 0; index < rules.Length; index++)
            {
                resolvedCounts[index] = Mathf.Max(0, rules[index].Count);
                total += resolvedCounts[index];
            }

            if (total != totalEnemyCount)
            {
                counts = default;
                error = $"Fixed grade counts must total {totalEnemyCount}, but total {total}.";
                return false;
            }
        }
        else if (!TryResolveRatioCounts(rules, resolvedCounts, out error))
        {
            counts = default;
            return false;
        }

        counts = new BattleEnemyGradeCounts(
            resolvedCounts[0],
            resolvedCounts[1],
            resolvedCounts[2],
            resolvedCounts[3]);
        error = string.Empty;
        return true;
    }

    public bool TryValidate(out string error)
    {
        if (!TryGetGradeCounts(out BattleEnemyGradeCounts counts, out error))
            return false;

        HashSet<EnemySO> usedDefinitions = new();
        foreach (BattleEnemyGradeRule rule in GetRules())
        {
            IReadOnlyList<EnemySO> pool = rule.EnemyPool;
            if (counts.Get(rule.Grade) > 0 && (pool == null || pool.Count == 0))
            {
                error = $"{rule.Grade} enemies require at least one EnemySO.";
                return false;
            }

            if (pool == null)
                continue;

            foreach (EnemySO definition in pool)
            {
                if (definition == null)
                {
                    error = $"{rule.Grade} enemy pool contains an empty entry.";
                    return false;
                }

                if (definition.Grade != rule.Grade)
                {
                    error = $"{definition.name} is {definition.Grade}, not {rule.Grade}.";
                    return false;
                }

                if (!usedDefinitions.Add(definition))
                {
                    error = $"{definition.name} is assigned more than once.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    public bool TryCreateSetup(
        int randomSeed,
        out BattleSetup setup,
        out string error)
    {
        setup = null;
        if (!TryValidate(out error) ||
            !TryGetGradeCounts(out BattleEnemyGradeCounts counts, out error))
        {
            return false;
        }

        System.Random random = new(randomSeed);
        List<EnemyRuntime> enemies = new(totalEnemyCount);
        foreach (BattleEnemyGradeRule rule in GetRules())
        {
            int count = counts.Get(rule.Grade);
            for (int index = 0; index < count; index++)
            {
                EnemySO definition = rule.EnemyPool[
                    random.Next(0, rule.EnemyPool.Count)];
                int maximumHealth = Math.Max(
                    minimumEnemyHealth,
                    definition.BaseHealth) + random.Next(0, randomHealthBonus + 1);
                enemies.Add(definition.CreateRuntime(maximumHealth));
            }
        }

        for (int index = enemies.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(0, index + 1);
            (enemies[index], enemies[swapIndex]) =
                (enemies[swapIndex], enemies[index]);
        }

        setup = new BattleSetup(
            fieldSize,
            maximumStackSize,
            spawnInterval,
            timeLimit,
            counts,
            enemies);
        return true;
    }

    private bool TryResolveRatioCounts(
        BattleEnemyGradeRule[] rules,
        int[] resolvedCounts,
        out string error)
    {
        double totalRatio = 0d;
        foreach (BattleEnemyGradeRule rule in rules)
            totalRatio += Math.Max(0d, rule.Ratio);

        if (totalRatio <= 0d)
        {
            error = "At least one grade ratio must be greater than zero.";
            return false;
        }

        double[] remainders = new double[rules.Length];
        int assignedCount = 0;
        for (int index = 0; index < rules.Length; index++)
        {
            double exactCount = totalEnemyCount *
                                Math.Max(0d, rules[index].Ratio) /
                                totalRatio;
            resolvedCounts[index] = (int)Math.Floor(exactCount);
            remainders[index] = exactCount - resolvedCounts[index];
            assignedCount += resolvedCounts[index];
        }

        int remainingCount = totalEnemyCount - assignedCount;
        while (remainingCount > 0)
        {
            int selectedIndex = 0;
            for (int index = 1; index < remainders.Length; index++)
            {
                if (remainders[index] > remainders[selectedIndex])
                    selectedIndex = index;
            }

            resolvedCounts[selectedIndex]++;
            remainders[selectedIndex] = -1d;
            remainingCount--;
        }

        error = string.Empty;
        return true;
    }

    private void EnsureRules()
    {
        normalEnemies ??= new BattleEnemyGradeRule(EEnemyGrade.Normal, 20, 70f);
        specialEnemies ??= new BattleEnemyGradeRule(EEnemyGrade.Special, 0, 20f);
        eliteEnemies ??= new BattleEnemyGradeRule(EEnemyGrade.Elite, 0, 8f);
        bossEnemies ??= new BattleEnemyGradeRule(EEnemyGrade.Boss, 0, 2f);
        normalEnemies.ValidateValues(EEnemyGrade.Normal);
        specialEnemies.ValidateValues(EEnemyGrade.Special);
        eliteEnemies.ValidateValues(EEnemyGrade.Elite);
        bossEnemies.ValidateValues(EEnemyGrade.Boss);
    }

    private BattleEnemyGradeRule[] GetRules()
    {
        return new[]
        {
            normalEnemies,
            specialEnemies,
            eliteEnemies,
            bossEnemies,
        };
    }
}
