using System.Collections.Generic;
using UnityEngine;

public abstract class DungeonFlowPolicy : ScriptableObject
{
    public abstract int ResolveBattleCount(int runSeed);

    public abstract IReadOnlyList<EDungeonPhase> BuildPhaseSequence(
        int battleCount,
        int runSeed);
}
