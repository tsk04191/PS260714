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
                    board.TryForcePriorityTarget(enemy, effect.Duration),
                BattleItemEffectType.ApplyFire =>
                    board.TryApplyFireToEnemy(
                        enemy,
                        effect.Duration,
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
                    turret.ApplyAttackSpeedBoost(
                        effect.Multiplier,
                        effect.Duration),
                BattleItemEffectType.PowerBoost =>
                    turret.ApplyPowerBoost(
                        effect.Multiplier,
                        effect.Duration),
                _ => false,
            };
            applied |= current;
        }

        return applied;
    }
}
