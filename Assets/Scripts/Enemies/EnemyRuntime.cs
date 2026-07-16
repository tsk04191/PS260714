using System;
using UnityEngine;

/// <summary>
/// Independent combat state for one enemy created from an EnemySO definition.
/// </summary>
public sealed class EnemyRuntime
{
    private float _fireRemainingDuration;
    private float _fireTickElapsed;
    private float _fireTickInterval;
    private int _fireTickDamage;
    private IBattleCharacter _fireSource;
    private float _abilityCooldownRemaining;

    public EnemySO Definition { get; }
    public EEnemyGrade Grade => Definition.Grade;
    public EEnemyType Type => Definition.Type;
    public int MaxHealth { get; private set; }
    public int Health { get; private set; }
    public int Armor { get; private set; }
    public int RemainingGuardedHits { get; private set; }
    public bool HasFire => _fireRemainingDuration > 0f;
    public bool IsTargetPriorityExcluded => Definition.TargetPriorityExcluded;
    public float SpawnIntervalMultiplier => Definition.SpawnIntervalMultiplier;
    public float AbilityCooldownRemaining =>
        TimePrecision.FloorToTenth(_abilityCooldownRemaining);

    public EnemyRuntime(EnemySO definition, int maximumHealthOverride = 0)
    {
        Definition = definition != null
            ? definition
            : throw new ArgumentNullException(nameof(definition));
        MaxHealth = maximumHealthOverride > 0
            ? maximumHealthOverride
            : Definition.BaseHealth;
        MaxHealth = Mathf.Max(1, MaxHealth);
        Health = MaxHealth;
        Armor = Mathf.Max(
            0,
            Mathf.RoundToInt(MaxHealth * Definition.InitialArmorMultiplier));
        RemainingGuardedHits = Mathf.Max(0, Definition.GuardedHitCount);
        ResetAbilityCooldown();
    }

    internal void SetHealth(int health)
    {
        Health = Mathf.Max(1, health);
        MaxHealth = Mathf.Max(MaxHealth, Health);
    }

    internal int TakeDamage(int damage)
    {
        damage = Mathf.Max(0, damage);
        if (damage <= 0 || Health <= 0)
            return 0;

        if (RemainingGuardedHits > 0)
        {
            RemainingGuardedHits--;
            damage = 1;
        }

        int appliedDamage = 0;
        if (Armor > 0)
        {
            int armorDamage = Mathf.Min(Armor, damage);
            Armor -= armorDamage;
            damage -= armorDamage;
            appliedDamage += armorDamage;
        }

        if (damage <= 0)
            return appliedDamage;

        int healthDamage = Mathf.Min(Health, damage);
        Health -= healthDamage;
        return appliedDamage + healthDamage;
    }

    internal int Heal(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount <= 0 || Health <= 0 || Health >= MaxHealth)
            return 0;

        int previousHealth = Health;
        Health = Mathf.Min(MaxHealth, Health + amount);
        return Health - previousHealth;
    }

    internal void ApplyFire(
        float duration,
        float tickInterval,
        int tickDamage,
        IBattleCharacter source)
    {
        _fireRemainingDuration = TimePrecision.Normalize(duration, 0.1f);
        _fireTickElapsed = 0f;
        _fireTickInterval = TimePrecision.Normalize(tickInterval, 0.1f);
        _fireTickDamage = Mathf.Max(1, tickDamage);
        _fireSource = source;
    }

    internal int TickFire(float deltaTime, out IBattleCharacter source)
    {
        source = _fireSource;
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

    internal bool TickAbilityCooldown(float deltaTime)
    {
        if (Definition.AbilityCooldown <= 0f || deltaTime <= 0f)
            return false;

        _abilityCooldownRemaining = Mathf.Max(
            0f,
            _abilityCooldownRemaining - deltaTime);
        return _abilityCooldownRemaining <= 0f;
    }

    internal void ResetAbilityCooldown()
    {
        _abilityCooldownRemaining = Mathf.Max(0f, Definition.AbilityCooldown);
    }
}
