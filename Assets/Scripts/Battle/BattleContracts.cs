public interface IBattleBoard
{
    int InitialEnemyCapacity { get; }
    int LivingEnemyCount { get; }

    bool TryAddEnemy(DungeonEnemyData enemy);
    void ClearAllEnemies();
    void TickStatusEffects(float deltaTime);

    bool TryAttackLowestHealthEnemy(int damage);
    bool TryAttackRandomEnemies(int targetCount, int damage);
    bool TryAttackCrossAroundHighestHealthEnemy(int damage);
    bool TryApplyFireToRandomEnemy(
        float duration,
        float tickInterval,
        int tickDamage);
}

public interface IBattleCharacter
{
    bool Initialize();
    void ResetRuntime();
    void TickBattle(float deltaTime, IBattleBoard board);
}
