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
    [SerializeField] private List<BattleEnemyDetailRule> detailedEnemies = new();

    public EEnemyGrade Grade => grade;
    public int Count => count;
    public float Ratio => ratio;
    public IReadOnlyList<EnemySO> EnemyPool => enemyPool;
    public IReadOnlyList<BattleEnemyDetailRule> DetailedEnemies => detailedEnemies;

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
        detailedEnemies ??= new List<BattleEnemyDetailRule>();
    }
}

[Serializable]
public sealed class BattleEnemyDetailRule
{
    [SerializeField] private EnemySO enemy;
    [SerializeField, Min(0)] private int count;

    public EnemySO Enemy => enemy;
    public int Count => count;
}

[CreateAssetMenu(fileName = "Battle", menuName = "Dungeon/Battle")]
public sealed class BattleSO : ScriptableObject
{
    public const float DefaultTimeLimit = 180f;

    [Header("Identity")]
    [SerializeField] private string battleId = "first_battle";
    [SerializeField] private string displayName = "FIRST BATTLE";

    [Header("Progress Balance")]
    [SerializeField, Range(0, 100)] private int difficultyPercent;
    [SerializeField] private int balanceSeed = 1000;

    [Header("Field")]
    [SerializeField, Range(DungeonBoardView.MinimumGridSize, DungeonBoardView.MaximumGridSize)]
    private int fieldSize = 4;
    [SerializeField, Range(1, 20)] private int maximumStackSize = 8;

    [Header("Arena")]
    [SerializeField] private BattleArenaMode arenaMode =
        BattleArenaMode.CircularDefense;
    [SerializeField, Min(1)] private int coreMaximumHealth =
        BattleArenaSetup.DefaultCoreMaximumHealth;
    [SerializeField, Range(4, 64)] private int circularLaneCount =
        BattleArenaSetup.DefaultLaneCount;
    [SerializeField, Range(0.12f, 0.4f)]
    private float wallRadiusNormalized =
        BattleArenaSetup.DefaultWallRadiusNormalized;
    [SerializeField, Range(0.17f, 0.5f)]
    private float spawnRadiusNormalized =
        BattleArenaSetup.DefaultSpawnRadiusNormalized;

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

    [Header("Presentation Override")]
    [SerializeField, Tooltip(
        "Optional music used during this battle. When empty, the dungeon's " +
        "default Battle Clip is used.")]
    private AudioClip bgmOverride;

    [Header("2.5D Environment")]
    [SerializeField, Tooltip(
        "Optional 1920x1080 background art rendered behind the authored " +
        "3D arena. Leave empty to use only the Scene environment.")]
    private Sprite environmentBackdrop;
    [SerializeField] private Color environmentBackdropTint = Color.white;
    [SerializeField] private Color environmentClearColor =
        new(0.018f, 0.014f, 0.01f, 1f);
    [SerializeField, Range(25f, 65f)] private float environmentCameraFov =
        BattleEnvironmentSetup.DefaultCameraFieldOfView;

    public string BattleId => battleId;
    public string DisplayName => displayName;
    public int DifficultyPercent => difficultyPercent;
    public int BalanceSeed => balanceSeed;
    public int FieldSize => fieldSize;
    public int MaximumStackSize => maximumStackSize;
    public BattleArenaMode ArenaMode => arenaMode;
    public int CoreMaximumHealth => Mathf.Max(1, coreMaximumHealth);
    public int CircularLaneCount => Mathf.Clamp(circularLaneCount, 4, 64);
    public float WallRadiusNormalized => Mathf.Clamp(
        wallRadiusNormalized,
        0.12f,
        0.4f);
    public float SpawnRadiusNormalized => Mathf.Clamp(
        spawnRadiusNormalized,
        WallRadiusNormalized + 0.05f,
        0.5f);
    public int TotalEnemyCount => totalEnemyCount;
    public int MinimumEnemyHealth => minimumEnemyHealth;
    public int RandomHealthBonus => randomHealthBonus;
    public float SpawnInterval => TimePrecision.Normalize(spawnInterval, 0.1f);
    public EEnemyCompositionMode CompositionMode => compositionMode;
    public float TimeLimit => TimePrecision.Normalize(timeLimit, 1f);
    public AudioClip BgmOverride => bgmOverride;
    public Sprite EnvironmentBackdrop => environmentBackdrop;
    public Color EnvironmentBackdropTint => environmentBackdropTint;
    public Color EnvironmentClearColor => environmentClearColor;
    public float EnvironmentCameraFov => Mathf.Clamp(
        environmentCameraFov,
        25f,
        65f);

    public IReadOnlyList<EnemySO> GetAllEnemyDefinitions()
    {
        EnsureRules();
        List<EnemySO> definitions = new();
        HashSet<EnemySO> uniqueDefinitions = new();
        foreach (BattleEnemyGradeRule rule in GetRules())
        {
            foreach (EnemySO definition in rule.EnemyPool)
            {
                if (definition != null && uniqueDefinitions.Add(definition))
                    definitions.Add(definition);
            }
        }

        return definitions.AsReadOnly();
    }

    private void OnValidate()
    {
        battleId = string.IsNullOrWhiteSpace(battleId)
            ? name.ToLowerInvariant().Replace(' ', '_')
            : battleId.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName)
            ? name.ToUpperInvariant()
            : displayName.Trim();
        difficultyPercent = Mathf.Clamp(difficultyPercent, 0, 100);
        fieldSize = Mathf.Clamp(
            fieldSize,
            DungeonBoardView.MinimumGridSize,
            DungeonBoardView.MaximumGridSize);
        maximumStackSize = Mathf.Clamp(maximumStackSize, 1, 20);
        coreMaximumHealth = Mathf.Max(1, coreMaximumHealth);
        circularLaneCount = Mathf.Clamp(circularLaneCount, 4, 64);
        wallRadiusNormalized = Mathf.Clamp(
            wallRadiusNormalized,
            0.12f,
            0.4f);
        spawnRadiusNormalized = Mathf.Clamp(
            spawnRadiusNormalized,
            wallRadiusNormalized + 0.05f,
            0.5f);
        environmentCameraFov = Mathf.Clamp(
            environmentCameraFov,
            25f,
            65f);
        totalEnemyCount = Mathf.Max(1, totalEnemyCount);
        minimumEnemyHealth = Mathf.Max(1, minimumEnemyHealth);
        randomHealthBonus = Mathf.Max(0, randomHealthBonus);
        spawnInterval = TimePrecision.Normalize(spawnInterval, 0.1f);
        timeLimit = TimePrecision.Normalize(timeLimit, 1f);
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

    public BattleArenaSetup CreateArenaSetup()
    {
        return arenaMode == BattleArenaMode.CircularDefense
            ? BattleArenaSetup.CreateCircular(
                coreMaximumHealth,
                circularLaneCount,
                wallRadiusNormalized,
                spawnRadiusNormalized)
            : BattleArenaSetup.Legacy;
    }

    public BattleEnvironmentSetup CreateEnvironmentSetup()
    {
        return new BattleEnvironmentSetup(
            environmentBackdrop,
            environmentBackdropTint,
            environmentClearColor,
            environmentCameraFov);
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

            HashSet<EnemySO> poolDefinitions = new();
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

                poolDefinitions.Add(definition);
            }

            int gradeCount = counts.Get(rule.Grade);
            int detailedCount = 0;
            HashSet<EnemySO> detailedDefinitions = new();
            foreach (BattleEnemyDetailRule detail in rule.DetailedEnemies)
            {
                if (detail == null || detail.Enemy == null)
                {
                    error = $"{rule.Grade} detailed enemies contain an empty entry.";
                    return false;
                }

                if (!poolDefinitions.Contains(detail.Enemy))
                {
                    error = $"{detail.Enemy.name} must also be in the {rule.Grade} enemy pool.";
                    return false;
                }

                if (!detailedDefinitions.Add(detail.Enemy))
                {
                    error = $"{detail.Enemy.name} has more than one detailed count.";
                    return false;
                }

                detailedCount += Mathf.Max(0, detail.Count);
            }

            if (detailedCount > gradeCount)
            {
                error = $"{rule.Grade} detailed counts total {detailedCount}, " +
                        $"but the grade count is {gradeCount}.";
                return false;
            }

            if (detailedCount < gradeCount &&
                poolDefinitions.Count <= detailedDefinitions.Count)
            {
                error = $"{rule.Grade} needs at least one non-detailed enemy " +
                        "for its remaining random count.";
                return false;
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
            HashSet<EnemySO> detailedDefinitions = new();
            int detailedCount = 0;
            foreach (BattleEnemyDetailRule detail in rule.DetailedEnemies)
            {
                detailedDefinitions.Add(detail.Enemy);
                int exactCount = Mathf.Max(0, detail.Count);
                detailedCount += exactCount;
                for (int index = 0; index < exactCount; index++)
                    AddEnemyRuntime(enemies, detail.Enemy, random);
            }

            List<EnemySO> randomPool = new();
            foreach (EnemySO definition in rule.EnemyPool)
            {
                if (!detailedDefinitions.Contains(definition))
                    randomPool.Add(definition);
            }

            int randomCount = count - detailedCount;
            for (int index = 0; index < randomCount; index++)
            {
                EnemySO definition = randomPool[
                    random.Next(0, randomPool.Count)];
                AddEnemyRuntime(enemies, definition, random);
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
            enemies,
            0,
            CreateArenaSetup(),
            CreateEnvironmentSetup());
        return true;
    }

    private void AddEnemyRuntime(
        ICollection<EnemyRuntime> enemies,
        EnemySO definition,
        System.Random random)
    {
        int maximumHealth = Math.Max(
            minimumEnemyHealth,
            definition.BaseHealth) + random.Next(0, randomHealthBonus + 1);
        enemies.Add(definition.CreateRuntime(maximumHealth));
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
