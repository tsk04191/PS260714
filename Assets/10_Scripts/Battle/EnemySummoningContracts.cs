public interface IBattleEnemySummonService
{
    int MaximumActiveSummons { get; }
    int ActiveSummonCount { get; }

    int TrySummonEnemies(
        EnemyRuntime source,
        string abilityId,
        EnemySummonDefinition definition);

    bool TryScheduleSummon(
        EnemyRuntime source,
        string abilityId,
        EnemySummonDefinition definition,
        float delaySeconds);

    bool TryAddSpawnIntervalModifier(
        string sourceId,
        float multiplier,
        float duration);
}

public interface IBattleEnemySummonServiceProvider
{
    IBattleEnemySummonService EnemySummonService { get; }
}
