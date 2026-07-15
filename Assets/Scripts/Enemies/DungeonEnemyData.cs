using UnityEngine;

/// <summary>
/// Runtime state for one enemy. Additional combat stats can be added here later
/// without coupling them to the tile UI.
/// </summary>
public sealed class DungeonEnemyData
{
    public int Health { get; private set; }

    public DungeonEnemyData(int health)
    {
        SetHealth(health);
    }

    internal void SetHealth(int health)
    {
        Health = Mathf.Max(1, health);
    }
}
