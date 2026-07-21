using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "FixedDungeonFlow",
    menuName = "Dungeon/Flow/Fixed Sequence")]
public sealed class FixedDungeonFlowPolicy : DungeonFlowPolicy
{
    [SerializeField] private EDungeonPhase[] phaseSequence =
    {
        EDungeonPhase.Battle,
    };

    public override int ResolveBattleCount(int runSeed)
    {
        int count = 0;
        if (phaseSequence != null)
        {
            for (int index = 0; index < phaseSequence.Length; index++)
            {
                if (phaseSequence[index] == EDungeonPhase.Battle)
                    count++;
            }
        }

        return Mathf.Max(1, count);
    }

    public override IReadOnlyList<EDungeonPhase> BuildPhaseSequence(
        int battleCount,
        int runSeed)
    {
        if (phaseSequence == null || phaseSequence.Length == 0)
            return Array.AsReadOnly(new[] { EDungeonPhase.Battle });

        EDungeonPhase[] copy = new EDungeonPhase[phaseSequence.Length];
        Array.Copy(phaseSequence, copy, phaseSequence.Length);
        return Array.AsReadOnly(copy);
    }

    private void OnValidate()
    {
        if (phaseSequence == null || phaseSequence.Length == 0)
            phaseSequence = new[] { EDungeonPhase.Battle };
    }
}
