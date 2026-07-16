using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CharacterRuntime : MonoBehaviour, IBattleCharacter,
    IPointerClickHandler
{
    [SerializeField] private CharacterSO original;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI cooldownText;
    [SerializeField] private Image cooldownFill;

    private bool _initialized;
    private float _remainingCooldown;
    private float _disabledTimeRemaining;
    private float _dualSkillTimeRemaining;
    private int _areaSkillAttackCount;
    private int _fireSkillAttackCount;
    private IActiveSkillResource _activeSkillResource;
    private IBattleBoard _board;
    private Image _panelImage;
    private Color _defaultPanelColor;

    public CharacterData Data { get; private set; }
    public int PartySlotIndex { get; private set; } = -1;
    public int PartySlotNumber => PartySlotIndex + 1;
    public Color EffectColor { get; private set; } = Color.white;
    public int TotalDamageDealt { get; private set; }
    public float DisabledTimeRemaining =>
        TimePrecision.FloorToTenth(_disabledTimeRemaining);

    private void Awake()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        BindBattle(null, null);
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
        _panelImage = GetComponent<Image>();
        if (_panelImage != null)
        {
            _defaultPanelColor = _panelImage.color;
            _panelImage.raycastTarget = true;
        }

        _initialized = true;
        RefreshUi();
        return true;
    }

    public void BindBattle(
        IActiveSkillResource activeSkillResource,
        IBattleBoard board)
    {
        if (_activeSkillResource != null)
            _activeSkillResource.Changed -= HandleActiveSkillResourceChanged;

        _activeSkillResource = activeSkillResource;
        _board = board;

        if (_activeSkillResource != null)
            _activeSkillResource.Changed += HandleActiveSkillResourceChanged;

        RefreshUi();
    }

    public void ConfigurePartySlot(int slotIndex, Color color)
    {
        PartySlotIndex = Mathf.Clamp(
            slotIndex,
            0,
            DungeonPage.MaximumPartySize - 1);
        color.a = 1f;
        EffectColor = color;
        RefreshUi();
    }

    public void ResetRuntime()
    {
        if (!_initialized && !Initialize())
            return;

        _remainingCooldown = Data.AttackCooldown;
        _disabledTimeRemaining = 0f;
        _dualSkillTimeRemaining = 0f;
        _areaSkillAttackCount = 0;
        _fireSkillAttackCount = 0;
        TotalDamageDealt = 0;
        RefreshUi();
    }

    public void TickBattle(float deltaTime, IBattleBoard board)
    {
        if ((!_initialized && !Initialize()) || board == null || deltaTime <= 0f)
            return;

        _board = board;
        _dualSkillTimeRemaining = Mathf.Max(
            0f,
            _dualSkillTimeRemaining - deltaTime);

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
        duration = TimePrecision.FloorToTenth(duration);
        _disabledTimeRemaining = Mathf.Max(
            _disabledTimeRemaining,
            duration);
        RefreshUi();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null ||
            eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        TryActivateActiveSkill();
    }

    public bool TryActivateActiveSkill()
    {
        if ((!_initialized && !Initialize()) ||
            _activeSkillResource == null || _board == null ||
            _board.LivingEnemyCount <= 0 || IsActiveSkillPending() ||
            !_activeSkillResource.TrySpend(Data.ActiveSkillCost))
        {
            return false;
        }

        switch (Data.AttackType)
        {
            case CharacterAttackType.RandomMultiple:
                _dualSkillTimeRemaining = Data.ActiveSkillDuration;
                break;

            case CharacterAttackType.CrossHighestHealth:
                _areaSkillAttackCount = Data.ActiveSkillAttackCount;
                break;

            case CharacterAttackType.FireRandom:
                _fireSkillAttackCount = Data.ActiveSkillAttackCount;
                break;

            default:
                int damageDealt = _board.TryAttackLowestHealthEnemy(
                    Data.AttackDamage * 2);
                RecordDamageDealt(damageDealt);
                break;
        }

        RefreshUi();
        return true;
    }

    private bool TryAttack(IBattleBoard board)
    {
        int damageDealt;
        switch (Data.AttackType)
        {
            case CharacterAttackType.RandomMultiple:
                int bonusTargetCount = _dualSkillTimeRemaining > 0f ? 2 : 0;
                damageDealt = board.TryAttackRandomEnemies(
                    Data.TargetCount + bonusTargetCount,
                    Data.AttackDamage);
                break;

            case CharacterAttackType.CrossHighestHealth:
                if (_areaSkillAttackCount > 0)
                {
                    int adjacentDamage = Mathf.Max(
                        1,
                        Mathf.FloorToInt(Data.AttackDamage * 0.5f));
                    damageDealt = board.TryAttackCrossWithAdjacentSplash(
                        Data.AttackDamage,
                        adjacentDamage);
                    if (damageDealt > 0)
                        _areaSkillAttackCount--;
                }
                else
                {
                    damageDealt = board.TryAttackCrossAroundHighestHealthEnemy(
                        Data.AttackDamage);
                }
                break;

            case CharacterAttackType.FireRandom:
                bool fireApplied = _fireSkillAttackCount > 0
                    ? board.TryApplyFireAroundRandomEnemy(
                        this,
                        Data.FireDuration,
                        Data.FireTickInterval,
                        Data.FireTickDamage)
                    : board.TryApplyFireToRandomEnemy(
                        this,
                        Data.FireDuration,
                        Data.FireTickInterval,
                        Data.FireTickDamage);
                if (fireApplied && _fireSkillAttackCount > 0)
                    _fireSkillAttackCount--;
                return fireApplied;

            default:
                damageDealt = board.TryAttackLowestHealthEnemy(Data.AttackDamage);
                break;
        }

        if (damageDealt <= 0)
            return false;

        RecordDamageDealt(damageDealt);
        return true;
    }

    private bool IsActiveSkillPending()
    {
        return Data.AttackType switch
        {
            CharacterAttackType.RandomMultiple =>
                _dualSkillTimeRemaining > 0f,
            CharacterAttackType.CrossHighestHealth =>
                _areaSkillAttackCount > 0,
            CharacterAttackType.FireRandom =>
                _fireSkillAttackCount > 0,
            _ => false,
        };
    }

    private void HandleActiveSkillResourceChanged(int _)
    {
        RefreshUi();
    }

    private void RefreshUi()
    {
        if (!_initialized)
            return;

        string slotLabel = PartySlotIndex >= 0
            ? $"[S{PartySlotNumber}] "
            : string.Empty;
        nameText.text =
            $"{slotLabel}{Data.CharacterName} [C{Data.ActiveSkillCost}]";
        nameText.color = EffectColor;
        attackText.text = Data.AttackType == CharacterAttackType.FireRandom
            ? $"FIRE {Data.FireTickDamage}/{Data.FireTickInterval:0.#}s"
            : $"ATK {Data.AttackDamage}";
        if (_disabledTimeRemaining > 0f)
        {
            float displayedTime =
                TimePrecision.FloorToTenth(_disabledTimeRemaining);
            cooldownText.text = $"STOP {displayedTime:0.0}s";
        }
        else
        {
            float displayedCooldown =
                TimePrecision.FloorToTenth(_remainingCooldown);
            if (_dualSkillTimeRemaining > 0f)
            {
                float displayedSkillTime =
                    TimePrecision.FloorToTenth(_dualSkillTimeRemaining);
                cooldownText.text = $"ACTIVE {displayedSkillTime:0.0}s";
            }
            else if (_areaSkillAttackCount > 0)
            {
                cooldownText.text = $"ACTIVE x{_areaSkillAttackCount}";
            }
            else if (_fireSkillAttackCount > 0)
            {
                cooldownText.text = $"ACTIVE x{_fireSkillAttackCount}";
            }
            else
            {
                cooldownText.text = _remainingCooldown > 0f
                    ? $"CD {displayedCooldown:0.0}s"
                    : "READY";
            }
        }
        cooldownFill.fillAmount = Data.AttackCooldown > 0f
            ? 1f - Mathf.Clamp01(_remainingCooldown / Data.AttackCooldown)
            : 1f;
        cooldownFill.color = EffectColor;

        if (_panelImage != null)
        {
            bool canAfford = _activeSkillResource != null &&
                             _activeSkillResource.Current >=
                             Data.ActiveSkillCost;
            _panelImage.color = IsActiveSkillPending()
                ? Color.Lerp(_defaultPanelColor, Color.green, 0.25f)
                : canAfford
                    ? _defaultPanelColor
                    : Color.Lerp(_defaultPanelColor, Color.black, 0.35f);
        }
    }
}
