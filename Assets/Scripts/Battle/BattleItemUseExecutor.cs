using System;
using System.Collections.Generic;
using UnityEngine;

public static class BattleItemUseExecutor
{
    public static bool TryApplyToEnemy(
        BattleItemSO item,
        DungeonBoardView board,
        EnemyRuntime enemy)
    {
        if (item == null || !item.HasCompatibleEffects ||
            board == null || enemy == null ||
            item.TargetType != BattleItemTargetType.Enemy ||
            !board.ContainsTargetableEnemy(enemy))
        {
            return false;
        }

        bool applied = false;
        foreach (BattleItemEffectDefinition effect in item.Effects)
        {
            if (effect == null)
                continue;

            bool current = effect.EffectType switch
            {
                BattleItemEffectType.ForcePriorityTarget =>
                    board.TryForcePriorityTarget(
                        enemy,
                        effect.RuntimeDuration),
                BattleItemEffectType.ApplyFire =>
                    board.TryApplyFireToEnemy(
                        enemy,
                        effect.RuntimeDuration,
                        effect.Interval,
                        Mathf.Max(1, effect.Amount)),
                BattleItemEffectType.FixedDamage =>
                    board.TryDamageEnemy(enemy, effect.Amount) > 0,
                _ => false,
            };
            applied |= current;
        }

        return applied;
    }

    public static bool TryApplyToTurret(
        BattleItemSO item,
        CharacterRuntime turret)
    {
        if (item == null || !item.HasCompatibleEffects || turret == null ||
            item.TargetType != BattleItemTargetType.Turret)
        {
            return false;
        }

        bool applied = false;
        foreach (BattleItemEffectDefinition effect in item.Effects)
        {
            if (effect == null)
                continue;

            bool current = effect.EffectType switch
            {
                BattleItemEffectType.AttackSpeedBoost =>
                    TryApplyLegacyModifier(
                        item,
                        effect,
                        turret,
                        CharacterModifierStat.AttackCooldown,
                        effect.Multiplier > 0f
                            ? 1f / effect.Multiplier
                            : 0f),
                BattleItemEffectType.PowerBoost =>
                    TryApplyLegacyModifier(
                        item,
                        effect,
                        turret,
                        CharacterModifierStat.AttackPower,
                        effect.Multiplier),
                BattleItemEffectType.CharacterModifier =>
                    TryApplyModifierModules(item, effect, turret),
                _ => false,
            };
            applied |= current;
        }

        return applied;
    }

    private static bool TryApplyLegacyModifier(
        BattleItemSO item,
        BattleItemEffectDefinition effect,
        CharacterRuntime turret,
        CharacterModifierStat stat,
        float multiplier)
    {
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
            return false;

        CharacterModifierModule module = new(
            $"legacy.{effect.EffectType}",
            new CharacterModifierTarget(
                CharacterModifierTargetScope.Character),
            stat,
            CharacterModifierOperation.Multiply,
            multiplier);
        return ApplyModifierSource(
            item,
            effect,
            turret,
            new[] { module });
    }

    private static bool TryApplyModifierModules(
        BattleItemSO item,
        BattleItemEffectDefinition effect,
        CharacterRuntime turret)
    {
        return effect.ModifierModules.Count > 0 &&
               ApplyModifierSource(
                   item,
                   effect,
                   turret,
                   effect.ModifierModules);
    }

    private static bool ApplyModifierSource(
        BattleItemSO item,
        BattleItemEffectDefinition effect,
        CharacterRuntime turret,
        IReadOnlyList<CharacterModifierModule> modules)
    {
        if (effect.DurationMode == BattleItemEffectDurationMode.Instant)
            return false;
        if (effect.DurationMode == BattleItemEffectDurationMode.Timed &&
            effect.RuntimeDuration <= 0f)
        {
            return false;
        }

        string sourceId = $"item:{item.ItemId}:{Guid.NewGuid():N}";
        return turret.ApplyModifierSource(
            sourceId,
            modules,
            effect.ModifierLifetimeScope,
            effect.RuntimeDuration);
    }
}
