using UnityEngine;

public abstract class DungeonModifier : ScriptableObject
{
    public virtual void OnRunStarted(DungeonRuntimeContext context) { }
    public virtual void OnPhaseEntered(
        DungeonRuntimeContext context,
        EDungeonPhase phase) { }
    public virtual void OnBattleStarted(DungeonRuntimeContext context) { }
    public virtual void OnRunTick(
        DungeonRuntimeContext context,
        float deltaTime) { }
    public virtual void OnBattleEnded(
        DungeonRuntimeContext context,
        EBattleResult result) { }
    public virtual void OnRunEnded(
        DungeonRuntimeContext context,
        EDungeonRunResult result) { }
}
