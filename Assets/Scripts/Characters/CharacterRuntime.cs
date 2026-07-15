using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CharacterRuntime : MonoBehaviour
{
    [SerializeField] private CharacterSO original;
    [SerializeField] private DungeonBoardView board;
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

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    public bool Initialize()
    {
        if (_initialized)
            return true;

        if (original == null || board == null || nameText == null ||
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

    private void Tick(float deltaTime)
    {
        if ((!_initialized && !Initialize()) || deltaTime <= 0f)
            return;

        _remainingCooldown = Mathf.Max(0f, _remainingCooldown - deltaTime);
        if (_remainingCooldown <= 0f && board.TryAttackLowestHealthEnemy(Data.AttackDamage))
            _remainingCooldown = Data.AttackCooldown;

        RefreshUi();
    }

    private void RefreshUi()
    {
        if (!_initialized)
            return;

        nameText.text = Data.CharacterName;
        attackText.text = $"ATK {Data.AttackDamage}";
        cooldownText.text = _remainingCooldown > 0f
            ? $"CD {_remainingCooldown:0.0}s"
            : "READY";
        cooldownFill.fillAmount = Data.AttackCooldown > 0f
            ? 1f - Mathf.Clamp01(_remainingCooldown / Data.AttackCooldown)
            : 1f;
    }
}
