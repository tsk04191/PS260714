using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class CharacterRuntime : MonoBehaviour, IBattleCharacter,
    IPointerClickHandler
{
    private const float TargetAttackRecoveryDuration = 0.5f;

    [SerializeField] private CharacterSO original;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI cooldownText;
    [SerializeField] private Image cooldownFill;
    [SerializeField] private AudioSource attackSfxSpeaker;

    private bool _initialized;
    private float _remainingCooldown;
    private float _disabledTimeRemaining;
    private float _attackRecoveryRemaining;
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
    public Sprite TargetEffectSprite => Data?.TargetEffectSprite;
    public RuntimeAnimatorController TargetEffectController =>
        Data?.TargetEffectController;
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
        InitializeAttackSfxSpeaker();
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
        _board?.ClearPreparedAttack(this);
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
        _attackRecoveryRemaining = 0f;
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
        if (_attackRecoveryRemaining > 0f)
        {
            float recoveryDeltaTime = Mathf.Min(
                activeDeltaTime,
                _attackRecoveryRemaining);
            _attackRecoveryRemaining = Mathf.Max(
                0f,
                _attackRecoveryRemaining - recoveryDeltaTime);
            activeDeltaTime -= recoveryDeltaTime;

            if (_attackRecoveryRemaining <= 0f)
                _remainingCooldown = Data.AttackCooldown;
        }

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

        bool targetChanged = false;
        if (Data.AttackType == CharacterAttackType.LowestHealth)
        {
            board.TryPrepareLowestHealthAttack(this, out targetChanged);
        }
        else if (Data.AttackType == CharacterAttackType.RandomMultiple)
        {
            board.TryPrepareRandomAttack(
                this,
                GetRandomTargetCount(),
                out targetChanged);
        }

        if (targetChanged)
        {
            _remainingCooldown = Mathf.Max(_remainingCooldown, 1f);
        }

        _remainingCooldown = Mathf.Max(0f, _remainingCooldown - activeDeltaTime);
        if (_remainingCooldown <= 0f && TryAttack(board))
        {
            if (UsesAttackRecovery())
                BeginTargetAttackRecovery();
            else
                _remainingCooldown = Data.AttackCooldown;
        }

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
                _board.ClearPreparedAttack(this);
                break;

            case CharacterAttackType.CrossHighestHealth:
                _areaSkillAttackCount = Data.ActiveSkillAttackCount;
                break;

            case CharacterAttackType.FireRandom:
                _fireSkillAttackCount = Data.ActiveSkillAttackCount;
                break;

            default:
                _board.ClearPreparedAttack(this);
                int damageDealt = _board.TryAttackLowestHealthEnemy(
                    this,
                    Data.AttackDamage * 2);
                RecordDamageDealt(damageDealt);
                if (damageDealt > 0)
                {
                    PlayAttackSfx();
                    BeginTargetAttackRecovery();
                }
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
                damageDealt = board.TryResolveRandomAttack(
                    this,
                    Data.AttackDamage);
                break;

            case CharacterAttackType.CrossHighestHealth:
                if (_areaSkillAttackCount > 0)
                {
                    int adjacentDamage = Mathf.Max(
                        1,
                        Mathf.FloorToInt(Data.AttackDamage * 0.5f));
                    damageDealt = board.TryAttackCrossWithAdjacentSplash(
                        this,
                        Data.AttackDamage,
                        adjacentDamage);
                    if (damageDealt > 0)
                        _areaSkillAttackCount--;
                }
                else
                {
                    damageDealt = board.TryAttackCrossAroundHighestHealthEnemy(
                        this,
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
                if (fireApplied)
                    PlayAttackSfx();
                return fireApplied;

            default:
                damageDealt = board.TryResolveLowestHealthAttack(
                    this,
                    Data.AttackDamage);
                break;
        }

        if (damageDealt <= 0)
            return false;

        RecordDamageDealt(damageDealt);
        PlayAttackSfx();
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

    private int GetRandomTargetCount()
    {
        int bonusTargetCount = _dualSkillTimeRemaining > 0f ? 2 : 0;
        return Data.TargetCount + bonusTargetCount;
    }

    private bool UsesAttackRecovery()
    {
        return Data.AttackType == CharacterAttackType.LowestHealth ||
               Data.AttackType == CharacterAttackType.RandomMultiple ||
               Data.AttackType == CharacterAttackType.CrossHighestHealth;
    }

    private void BeginTargetAttackRecovery()
    {
        _remainingCooldown = 0f;
        _attackRecoveryRemaining = TargetAttackRecoveryDuration;
    }

    private void PlayAttackSfx()
    {
        if (Data?.AttackSfx == null)
            return;

        InitializeAttackSfxSpeaker();
        GameManager manager = GameManager.Instance;
        if (manager?.Audio != null)
        {
            manager.Audio.PlaySfx(attackSfxSpeaker, Data.AttackSfx);
            return;
        }

        attackSfxSpeaker?.PlayOneShot(Data.AttackSfx);
    }

    private void InitializeAttackSfxSpeaker()
    {
        if (attackSfxSpeaker == null)
            attackSfxSpeaker = GetComponent<AudioSource>();
        if (attackSfxSpeaker == null)
            attackSfxSpeaker = gameObject.AddComponent<AudioSource>();

        attackSfxSpeaker.playOnAwake = false;
        attackSfxSpeaker.loop = false;
        attackSfxSpeaker.spatialBlend = 0f;
        attackSfxSpeaker.dopplerLevel = 0f;
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
        else if (_attackRecoveryRemaining > 0f)
        {
            float displayedRecovery =
                TimePrecision.FloorToTenth(_attackRecoveryRemaining);
            cooldownText.text = $"RECOVERY {displayedRecovery:0.0}s";
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
        cooldownFill.fillAmount = _attackRecoveryRemaining > 0f
            ? 0f
            : Data.AttackCooldown > 0f
                ? 1f - Mathf.Clamp01(
                    _remainingCooldown / Data.AttackCooldown)
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
