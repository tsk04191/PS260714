using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-only enemy slot retained behind the battle-board contract.
/// The legacy tile UI was removed; all presentation is handled by the
/// authored 2.5D world actor prefabs.
/// </summary>
public sealed class DungeonBoardSlot
{
    private readonly List<EnemyRuntime> _enemies = new();
    private int _maximumStackSize;
    private EnemyRuntime _exclusiveFootprintOccupant;

    public int Row { get; private set; }
    public int Column { get; private set; }
    public int StackCount => _enemies.Count;
    public EnemyRuntime TopEnemy =>
        _enemies.Count > 0 ? _enemies[^1] : null;
    internal EnemyRuntime InteractionEnemy =>
        _exclusiveFootprintOccupant != null
            ? _exclusiveFootprintOccupant
            : TopEnemy;
    public int TopEnemyHealth => TopEnemy != null ? TopEnemy.Health : 0;
    public bool IsFull => _enemies.Count >= _maximumStackSize;
    internal bool CanAddEnemy => !IsFull;

    public void Initialize(int row, int column, int stackSize)
    {
        Row = row;
        Column = column;
        _maximumStackSize = Mathf.Max(1, stackSize);
    }

    internal void SetExclusiveFootprintOccupant(
        EnemyRuntime enemy,
        bool isAnchor)
    {
        _exclusiveFootprintOccupant = enemy;
    }

    internal bool TryAdd(EnemyRuntime enemy)
    {
        if (!CanAddEnemy || enemy == null)
            return false;

        _enemies.Add(enemy);
        return true;
    }

    public bool TrySetTopEnemyHealth(int health)
    {
        if (TopEnemy == null)
            return false;

        TopEnemy.SetHealth(health);
        return true;
    }

    internal int TryDamageTop(int damage)
    {
        return TryDamageTop(damage, CharacterAttackDamageType.Physical);
    }

    internal int TryDamageTop(
        int damage,
        CharacterAttackDamageType damageType)
    {
        if (TopEnemy == null || damage <= 0)
            return 0;

        EnemyRuntime target = TopEnemy;
        int appliedDamage = target.TakeDamage(damage, damageType);
        if (appliedDamage > 0 && target.Health <= 0)
        {
            target.ClearStatusEffectsOnDefeat();
            TryRemoveTop();
        }
        return appliedDamage;
    }

    internal int TryHealTop(int amount)
    {
        return TopEnemy != null && amount > 0
            ? TopEnemy.Heal(amount)
            : 0;
    }

    internal int TryGrantShieldTop(int amount)
    {
        return TopEnemy != null && amount > 0
            ? TopEnemy.GainShield(amount)
            : 0;
    }

    internal void RefreshTopEnemyCard()
    {
    }

    internal bool ShowEnemyHitFeedback(
        EnemyRuntime enemy,
        Color color,
        float duration)
    {
        return false;
    }

    internal bool TryApplyFireToTop(
        IBattleCharacter source,
        float duration,
        float tickInterval,
        int tickDamage)
    {
        return TryApplyFireToTop(
            source,
            duration,
            tickInterval,
            tickDamage,
            null);
    }

    internal bool TryApplyFireToTop(
        IBattleCharacter source,
        float duration,
        float tickInterval,
        int tickDamage,
        Func<DungeonBoardSlot, int, IBattleCharacter, int> applyDamage)
    {
        StatusEffectSO fire =
            StatusEffectDefinitionCatalog.FindById(StatusEffectIds.Fire);
        return fire != null && TryApplyStatusToTop(
            fire,
            duration,
            tickDamage,
            source,
            tickInterval,
            applyDamage);
    }

    internal bool TryApplyStatusToTop(
        StatusEffectSO statusEffect,
        float duration,
        int stacks,
        IBattleCharacter source = null,
        float tickInterval = 0f)
    {
        return TryApplyStatusToTop(
            statusEffect,
            duration,
            stacks,
            source,
            tickInterval,
            null);
    }

    internal bool TryApplyStatusToTop(
        StatusEffectSO statusEffect,
        float duration,
        int stacks,
        IBattleCharacter source,
        float tickInterval,
        Func<DungeonBoardSlot, int, IBattleCharacter, int> applyDamage)
    {
        if (TopEnemy == null || statusEffect == null)
            return false;

        EnemyRuntime target = TopEnemy;
        return target.ApplyStatusEffect(
            statusEffect,
            duration,
            stacks,
            source,
            tickInterval,
            CreateStatusDamageCallback(target, applyDamage));
    }

    internal int TryRemoveStatusFromTop(
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        int removalCount)
    {
        if (removalCount < 0)
            return 0;
        return TryRemoveStatusFromTop(
            new CharacterStatusRemovalSelection(removalTarget, statusEffect),
            CharacterStatusRemovalAmount.Fixed(removalCount),
            null);
    }

    internal int TryRemoveStatusFromTop(
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        int removalCount,
        Func<DungeonBoardSlot, int, IBattleCharacter, int> applyDamage)
    {
        if (removalCount < 0)
            return 0;
        return TryRemoveStatusFromTop(
            new CharacterStatusRemovalSelection(removalTarget, statusEffect),
            CharacterStatusRemovalAmount.Fixed(removalCount),
            applyDamage);
    }

    internal int TryRemoveStatusFromTop(
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        CharacterStatusRemovalAmount removalAmount,
        Func<DungeonBoardSlot, int, IBattleCharacter, int> applyDamage)
    {
        return TryRemoveStatusFromTop(
            new CharacterStatusRemovalSelection(removalTarget, statusEffect),
            removalAmount,
            applyDamage);
    }

    internal int TryRemoveStatusFromTop(
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount,
        Func<DungeonBoardSlot, int, IBattleCharacter, int> applyDamage)
    {
        if (TopEnemy == null)
            return 0;

        EnemyRuntime target = TopEnemy;
        return target.RemoveStatusEffects(
            removalSelection,
            removalAmount,
            CreateStatusDamageCallback(target, applyDamage));
    }

    internal void ShowTargetArea()
    {
    }

    internal void SetManualSelectionState(bool candidate, bool selected)
    {
    }

    internal void TickStatusEffects(
        float deltaTime,
        Func<DungeonBoardSlot, int, IBattleCharacter, int> applyDamage)
    {
        if (deltaTime <= 0f || TopEnemy == null || applyDamage == null)
            return;

        EnemyRuntime target = TopEnemy;
        target.TickStatusEffects(
            deltaTime,
            CreateStatusDamageCallback(target, applyDamage));
    }

    private Func<int, IBattleCharacter, bool> CreateStatusDamageCallback(
        EnemyRuntime target,
        Func<DungeonBoardSlot, int, IBattleCharacter, int> applyDamage)
    {
        if (target == null || applyDamage == null)
            return null;

        return (damage, source) =>
        {
            if (!ReferenceEquals(TopEnemy, target))
                return false;

            int appliedDamage = applyDamage(this, damage, source);
            if (appliedDamage > 0)
                source?.RecordDamageDealt(appliedDamage);
            return ReferenceEquals(TopEnemy, target);
        };
    }

    public bool TryRemoveTop()
    {
        if (_enemies.Count == 0)
            return false;

        _enemies.RemoveAt(_enemies.Count - 1);
        return true;
    }

    public void ClearStack()
    {
        _enemies.Clear();
        _exclusiveFootprintOccupant = null;
    }

    internal List<EnemyRuntime> CopyEnemyRuntimes()
    {
        return new List<EnemyRuntime>(_enemies);
    }

    internal bool TryGetEnemyVfxAnchor(
        EnemyRuntime enemy,
        BattleVfxAnchorType anchorType,
        out BattleVfxAnchorSnapshot snapshot)
    {
        snapshot = default;
        return false;
    }
}
