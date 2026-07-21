using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AlternatingDungeonFlow",
    menuName = "Dungeon/Flow/Alternating Battle And Event")]
public sealed class AlternatingDungeonFlowPolicy : DungeonFlowPolicy
{
    [SerializeField, Min(1)] private int minimumBattleCount = 5;
    [SerializeField, Min(1)] private int maximumBattleCount = 8;
    [SerializeField] private EDungeonPhase phaseBetweenBattles =
        EDungeonPhase.Event;

    public override int ResolveBattleCount(int runSeed)
    {
        int minimum = Mathf.Max(1, minimumBattleCount);
        int maximum = Mathf.Max(minimum, maximumBattleCount);
        return minimum == maximum
            ? minimum
            : new System.Random(runSeed).Next(minimum, maximum + 1);
    }

    public override IReadOnlyList<EDungeonPhase> BuildPhaseSequence(
        int battleCount,
        int runSeed)
    {
        battleCount = Mathf.Max(1, battleCount);
        EDungeonPhase[] phases = new EDungeonPhase[battleCount * 2 - 1];
        for (int index = 0; index < phases.Length; index++)
        {
            phases[index] = index % 2 == 0
                ? EDungeonPhase.Battle
                : phaseBetweenBattles;
        }

        return Array.AsReadOnly(phases);
    }

    private void OnValidate()
    {
        minimumBattleCount = Mathf.Max(1, minimumBattleCount);
        maximumBattleCount = Mathf.Max(
            minimumBattleCount,
            maximumBattleCount);
    }
}
