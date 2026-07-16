using System;
using System.Collections.Generic;

public interface IActiveSkillResource
{
    int Current { get; }
    event Action<int> Changed;

    bool CanSpend(int amount);
    bool TrySpend(int amount);
}

public interface IBattleBoard
{
    int InitialEnemyCapacity { get; }
    int LivingEnemyCount { get; }
    bool HasEmptyEnemyTile { get; }
    event Action<EnemyRuntime> EnemyDefeated;

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
    int TryAttackCrossWithAdjacentSplash(
        int damage,
        int adjacentDamage);
    bool TryApplyFireToRandomEnemy(
        IBattleCharacter source,
        float duration,
        float tickInterval,
        int tickDamage);
    bool TryApplyFireAroundRandomEnemy(
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
    void BindBattle(
        IActiveSkillResource activeSkillResource,
        IBattleBoard board);
    void ResetRuntime();
    void TickBattle(float deltaTime, IBattleBoard board);
    void RecordDamageDealt(int damage);
    void DisableFor(float duration);
}
