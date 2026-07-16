using System.Collections.Generic;

public interface IBattleBoard
{
    int InitialEnemyCapacity { get; }
    int LivingEnemyCount { get; }
    bool HasEmptyEnemyTile { get; }

    bool TryAddEnemy(EnemyRuntime enemy);
    bool TryAddEnemiesToDistinctTiles(
        IReadOnlyList<EnemyRuntime> enemies);
    void ClearAllEnemies();
    void TickStatusEffects(float deltaTime);
    void TickEnemyAbilities(
        float deltaTime,
        IReadOnlyList<IBattleCharacter> characters);

    int TryAttackLowestHealthEnemy(int damage);
    int TryAttackRandomEnemies(int targetCount, int damage);
    int TryAttackCrossAroundHighestHealthEnemy(int damage);
    bool TryApplyFireToRandomEnemy(
        IBattleCharacter source,
        float duration,
        float tickInterval,
        int tickDamage);
}

public interface IBattleCharacter
{
    int TotalDamageDealt { get; }
    float DisabledTimeRemaining { get; }

    bool Initialize();
    void ResetRuntime();
    void TickBattle(float deltaTime, IBattleBoard board);
    void RecordDamageDealt(int damage);
    void DisableFor(float duration);
}
