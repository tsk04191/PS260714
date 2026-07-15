using UnityEngine;

/// <summary>
/// Runtime state for one enemy. Additional combat stats can be added here later
/// without coupling them to the tile UI.
/// </summary>
public sealed class DungeonEnemyData
{
    public const int HeavyGuardHitCount = 3;
    public const float MedicAbilityCooldown = 4f;
    public const float MechanicAbilityCooldown = 10f;
    public const float MechanicDisableDuration = 5f;

    private float _fireRemainingDuration;
    private float _fireTickElapsed;
    private float _fireTickInterval;
    private int _fireTickDamage;
    private IBattleCharacter _fireSource;
    private float _abilityCooldownRemaining;

    public EEnemyGrade Grade { get; }
    public EEnemyType Type { get; }
    public int MaxHealth { get; private set; }
    public int Health { get; private set; }
    public int RemainingGuardedHits { get; private set; }
    public bool HasFire => _fireRemainingDuration > 0f;
    public float SpawnIntervalMultiplier => Type == EEnemyType.Assault ? 0.5f : 1f;
    public float AbilityCooldownRemaining => _abilityCooldownRemaining;

    public DungeonEnemyData(int health)
        : this(health, EEnemyGrade.Normal, EEnemyType.Basic)
    {
    }

    public DungeonEnemyData(int health, EEnemyGrade grade)
        : this(health, grade, EEnemyType.Basic)
    {
    }

    public DungeonEnemyData(int health, EEnemyGrade grade, EEnemyType type)
    {
        Grade = NormalizeGrade(grade);
        Type = NormalizeType(type);
        MaxHealth = Mathf.Max(1, health);
        Health = MaxHealth;
        RemainingGuardedHits = Type == EEnemyType.Heavy
            ? HeavyGuardHitCount
            : 0;
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

        if (Type == EEnemyType.Heavy && RemainingGuardedHits > 0)
        {
            RemainingGuardedHits--;
            damage = 1;
        }

        int appliedDamage = Mathf.Min(Health, damage);
        Health -= appliedDamage;
        return appliedDamage;
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
        _fireRemainingDuration = Mathf.Max(0.1f, duration);
        _fireTickElapsed = 0f;
        _fireTickInterval = Mathf.Max(0.1f, tickInterval);
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
        if ((Type != EEnemyType.Medic && Type != EEnemyType.Mechanic) ||
            deltaTime <= 0f)
        {
            return false;
        }

        _abilityCooldownRemaining = Mathf.Max(
            0f,
            _abilityCooldownRemaining - deltaTime);
        return _abilityCooldownRemaining <= 0f;
    }

    internal void ResetAbilityCooldown()
    {
        switch (Type)
        {
            case EEnemyType.Medic:
                _abilityCooldownRemaining = MedicAbilityCooldown;
                break;
            case EEnemyType.Mechanic:
                _abilityCooldownRemaining = MechanicAbilityCooldown;
                break;
            default:
                _abilityCooldownRemaining = 0f;
                break;
        }
    }

    private static EEnemyGrade NormalizeGrade(EEnemyGrade grade)
    {
        switch (grade)
        {
            case EEnemyGrade.Special:
            case EEnemyGrade.Elite:
            case EEnemyGrade.Boss:
                return grade;
            default:
                return EEnemyGrade.Normal;
        }
    }

    private static EEnemyType NormalizeType(EEnemyType type)
    {
        switch (type)
        {
            case EEnemyType.Assault:
            case EEnemyType.Heavy:
            case EEnemyType.Medic:
            case EEnemyType.Mechanic:
                return type;
            default:
                return EEnemyType.Basic;
        }
    }
}
