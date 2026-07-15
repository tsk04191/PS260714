using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CharacterRuntime : MonoBehaviour, IBattleCharacter
{
    [SerializeField] private CharacterSO original;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI cooldownText;
    [SerializeField] private Image cooldownFill;

    private bool _initialized;
    private float _remainingCooldown;
    private float _disabledTimeRemaining;

    public CharacterData Data { get; private set; }
    public int TotalDamageDealt { get; private set; }
    public float DisabledTimeRemaining => _disabledTimeRemaining;

    private void Awake()
    {
        Initialize();
    }

    public bool Initialize()
    {
        if (_initialized)
            return true;

        if (original == null || nameText == null ||
            attackText == null || cooldownText == null || cooldownFill == null)
        {
            Debug.LogError("CharacterRuntime references are incomplete.", this);
            return false;
        }

        Data = original.CreateData();
        _remainingCooldown = Data.AttackCooldown;
        _initialized = true;
        RefreshUi();
        return true;
    }

    public void ResetRuntime()
    {
        if (!_initialized && !Initialize())
            return;

        _remainingCooldown = Data.AttackCooldown;
        _disabledTimeRemaining = 0f;
        TotalDamageDealt = 0;
        RefreshUi();
    }

    public void TickBattle(float deltaTime, IBattleBoard board)
    {
        if ((!_initialized && !Initialize()) || board == null || deltaTime <= 0f)
            return;

        float activeDeltaTime = deltaTime;
        if (_disabledTimeRemaining > 0f)
        {
            float disabledDeltaTime = Mathf.Min(
                activeDeltaTime,
                _disabledTimeRemaining);
            _disabledTimeRemaining = Mathf.Max(
                0f,
                _disabledTimeRemaining - disabledDeltaTime);
            activeDeltaTime -= disabledDeltaTime;
        }

        if (activeDeltaTime <= 0f)
        {
            RefreshUi();
            return;
        }

        _remainingCooldown = Mathf.Max(0f, _remainingCooldown - activeDeltaTime);
        if (_remainingCooldown <= 0f && TryAttack(board))
            _remainingCooldown = Data.AttackCooldown;

        RefreshUi();
    }

    public void RecordDamageDealt(int damage)
    {
        TotalDamageDealt += Mathf.Max(0, damage);
    }

    public void DisableFor(float duration)
    {
        _disabledTimeRemaining = Mathf.Max(
            _disabledTimeRemaining,
            Mathf.Max(0f, duration));
        RefreshUi();
    }

    private bool TryAttack(IBattleBoard board)
    {
        int damageDealt;
        switch (Data.AttackType)
        {
            case CharacterAttackType.RandomMultiple:
                damageDealt = board.TryAttackRandomEnemies(
                    Data.TargetCount,
                    Data.AttackDamage);
                break;

            case CharacterAttackType.CrossHighestHealth:
                damageDealt = board.TryAttackCrossAroundHighestHealthEnemy(
                    Data.AttackDamage);
                break;

            case CharacterAttackType.FireRandom:
                return board.TryApplyFireToRandomEnemy(
                    this,
                    Data.FireDuration,
                    Data.FireTickInterval,
                    Data.FireTickDamage);

            default:
                damageDealt = board.TryAttackLowestHealthEnemy(Data.AttackDamage);
                break;
        }

        if (damageDealt <= 0)
            return false;

        RecordDamageDealt(damageDealt);
        return true;
    }

    private void RefreshUi()
    {
        if (!_initialized)
            return;

        nameText.text = Data.CharacterName;
        attackText.text = Data.AttackType == CharacterAttackType.FireRandom
            ? $"FIRE {Data.FireTickDamage}/{Data.FireTickInterval:0.#}s"
            : $"ATK {Data.AttackDamage}";
        if (_disabledTimeRemaining > 0f)
            cooldownText.text = $"STOP {_disabledTimeRemaining:0.0}s";
        else
            cooldownText.text = _remainingCooldown > 0f
                ? $"CD {_remainingCooldown:0.0}s"
                : "READY";
        cooldownFill.fillAmount = Data.AttackCooldown > 0f
            ? 1f - Mathf.Clamp01(_remainingCooldown / Data.AttackCooldown)
            : 1f;
    }
}
