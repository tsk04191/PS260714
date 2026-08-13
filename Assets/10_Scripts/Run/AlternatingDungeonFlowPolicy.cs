using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AlternatingDungeonFlow",
    menuName = "Dungeon/Flow/Alternating Battle And Rooms")]
public sealed class AlternatingDungeonFlowPolicy : DungeonFlowPolicy
{
    [SerializeField, Min(1)] private int minimumBattleCount = 5;
    [SerializeField, Min(1)] private int maximumBattleCount = 8;
    [SerializeField] private EDungeonPhase phaseBetweenBattles =
        EDungeonPhase.Event;
    [SerializeField, Tooltip(
        "Optional round-robin room pattern between battles. Empty uses " +
        "the legacy Phase Between Battles value.")]
    private EDungeonPhase[] roomPattern =
    {
        EDungeonPhase.Event,
        EDungeonPhase.Rest,
        EDungeonPhase.Shop,
    };

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
                : ResolveRoomPhase(index / 2);
        }

        return Array.AsReadOnly(phases);
    }

    private EDungeonPhase ResolveRoomPhase(int roomIndex)
    {
        if (roomPattern == null || roomPattern.Length == 0)
            return NormalizeRoomPhase(phaseBetweenBattles);

        return NormalizeRoomPhase(
            roomPattern[Math.Abs(roomIndex) % roomPattern.Length]);
    }

    private static EDungeonPhase NormalizeRoomPhase(EDungeonPhase phase)
    {
        return phase == EDungeonPhase.Battle
            ? EDungeonPhase.Event
            : phase;
    }

    private void OnValidate()
    {
        minimumBattleCount = Mathf.Max(1, minimumBattleCount);
        maximumBattleCount = Mathf.Max(
            minimumBattleCount,
            maximumBattleCount);
        roomPattern ??= Array.Empty<EDungeonPhase>();
    }
}
