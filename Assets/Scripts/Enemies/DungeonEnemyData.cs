using UnityEngine;

/// <summary>
/// Runtime state for one enemy. Additional combat stats can be added here later
/// without coupling them to the tile UI.
/// </summary>
public sealed class DungeonEnemyData
{
    private float _fireRemainingDuration;
    private float _fireTickElapsed;
    private float _fireTickInterval;
    private int _fireTickDamage;

    public int Health { get; private set; }
    public bool HasFire => _fireRemainingDuration > 0f;

    public DungeonEnemyData(int health)
    {
        SetHealth(health);
    }

    internal void SetHealth(int health)
    {
        Health = Mathf.Max(1, health);
    }

    internal void ApplyFire(float duration, float tickInterval, int tickDamage)
    {
        _fireRemainingDuration = Mathf.Max(0.1f, duration);
        _fireTickElapsed = 0f;
        _fireTickInterval = Mathf.Max(0.1f, tickInterval);
        _fireTickDamage = Mathf.Max(1, tickDamage);
    }

    internal int TickFire(float deltaTime)
    {
        if (!HasFire || deltaTime <= 0f)
            return 0;

        float activeDelta = Mathf.Min(deltaTime, _fireRemainingDuration);
        _fireRemainingDuration = Mathf.Max(0f, _fireRemainingDuration - activeDelta);
        _fireTickElapsed += activeDelta;

        int tickCount = Mathf.FloorToInt(
            (_fireTickElapsed + 0.0001f) / _fireTickInterval);
        if (tickCount <= 0)
            return 0;

        _fireTickElapsed -= tickCount * _fireTickInterval;
        return tickCount * _fireTickDamage;
    }
}
