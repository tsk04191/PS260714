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

    public CharacterData Data { get; private set; }

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
        RefreshUi();
    }

    public void TickBattle(float deltaTime, IBattleBoard board)
    {
        if ((!_initialized && !Initialize()) || board == null || deltaTime <= 0f)
            return;

        _remainingCooldown = Mathf.Max(0f, _remainingCooldown - deltaTime);
        if (_remainingCooldown <= 0f && TryAttack(board))
            _remainingCooldown = Data.AttackCooldown;

        RefreshUi();
    }

    private bool TryAttack(IBattleBoard board)
    {
        switch (Data.AttackType)
        {
            case CharacterAttackType.RandomMultiple:
                return board.TryAttackRandomEnemies(
                    Data.TargetCount,
                    Data.AttackDamage);

            case CharacterAttackType.CrossHighestHealth:
                return board.TryAttackCrossAroundHighestHealthEnemy(
                    Data.AttackDamage);

            case CharacterAttackType.FireRandom:
                return board.TryApplyFireToRandomEnemy(
                    Data.FireDuration,
                    Data.FireTickInterval,
                    Data.FireTickDamage);

            default:
                return board.TryAttackLowestHealthEnemy(Data.AttackDamage);
        }
    }

    private void RefreshUi()
    {
        if (!_initialized)
            return;

        nameText.text = Data.CharacterName;
        attackText.text = Data.AttackType == CharacterAttackType.FireRandom
            ? $"FIRE {Data.FireTickDamage}/{Data.FireTickInterval:0.#}s"
            : $"ATK {Data.AttackDamage}";
        cooldownText.text = _remainingCooldown > 0f
            ? $"CD {_remainingCooldown:0.0}s"
            : "READY";
        cooldownFill.fillAmount = Data.AttackCooldown > 0f
            ? 1f - Mathf.Clamp01(_remainingCooldown / Data.AttackCooldown)
            : 1f;
    }
}
