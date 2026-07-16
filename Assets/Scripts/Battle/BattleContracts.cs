using System;
using System.Collections.Generic;
using UnityEngine;

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

    bool TryPrepareLowestHealthAttack(
        IBattleCharacter source,
        out bool targetChanged);
    int TryResolveLowestHealthAttack(
        IBattleCharacter source,
        int damage);
    int TryAttackLowestHealthEnemy(
        IBattleCharacter source,
        int damage);
    bool TryPrepareRandomAttack(
        IBattleCharacter source,
        int targetCount,
        out bool targetChanged);
    int TryResolveRandomAttack(
        IBattleCharacter source,
        int damage);
    void ClearPreparedAttack(IBattleCharacter source);
    int TryAttackCrossAroundHighestHealthEnemy(
        IBattleCharacter source,
        int damage);
    int TryAttackCrossWithAdjacentSplash(
        IBattleCharacter source,
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
    int PartySlotIndex { get; }
    Color EffectColor { get; }
    Sprite TargetEffectSprite { get; }
    RuntimeAnimatorController TargetEffectController { get; }
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
