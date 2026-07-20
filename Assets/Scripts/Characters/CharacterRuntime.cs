using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class CharacterRuntime : MonoBehaviour, IBattleCharacter,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    public const float TargetAttackRecoveryDuration = 0.5f;

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
    private float _attackSpeedBoostRemaining;
    private float _attackSpeedMultiplier = 1f;
    private float _powerBoostRemaining;
    private float _powerMultiplier = 1f;
    private int _areaSkillAttackCount;
    private int _fireSkillAttackCount;
    private IActiveSkillResource _activeSkillResource;
    private IBattleBoard _board;
    private Image _panelImage;
    private Color _defaultPanelColor;
    private System.Func<CharacterRuntime, bool> _itemTargetHandler;
    private GameObject _skillTooltip;
    private TextMeshProUGUI _skillTooltipText;

    public CharacterSO Definition => original;
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
        _itemTargetHandler = null;
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
        _remainingCooldown = GetEffectiveAttackCooldown();
        _panelImage = GetComponent<Image>();
        if (_panelImage != null)
        {
            _defaultPanelColor = _panelImage.color;
            _panelImage.raycastTarget = true;
        }

        _initialized = true;
        EnsureSkillTooltip();
        RefreshUi();
        return true;
    }

    private void OnDisable()
    {
        _skillTooltip?.SetActive(false);
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

    public bool ConfigureDefinition(CharacterSO definition)
    {
        if (definition == null)
            return false;

        BindBattle(null, null);
        original = definition;
        if (!_initialized)
            return Initialize();

        Data = original.CreateData();
        ResetRuntime();
        return true;
    }

    public bool ApplyUpgrade(ETurretUpgradeType upgradeType)
    {
        if ((!_initialized && !Initialize()) || Data == null)
            return false;

        float previousCooldown = Data.AttackCooldown;
        if (!Data.ApplyUpgrade(upgradeType))
            return false;

        if (Data.AttackCooldown < previousCooldown)
        {
            _remainingCooldown = Mathf.Min(
                _remainingCooldown,
                GetEffectiveAttackCooldown());
        }
        RefreshUi();
        return true;
    }

    public void ResetRuntime()
    {
        if (!_initialized && !Initialize())
            return;

        _attackSpeedBoostRemaining = 0f;
        _attackSpeedMultiplier = 1f;
        _powerBoostRemaining = 0f;
        _powerMultiplier = 1f;
        _remainingCooldown = GetEffectiveAttackCooldown();
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
        TickTemporaryBoosts(deltaTime);
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
                _remainingCooldown = GetEffectiveAttackCooldown();
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
            _remainingCooldown = Mathf.Max(
                _remainingCooldown,
                1f / _attackSpeedMultiplier);
        }

        _remainingCooldown = Mathf.Max(0f, _remainingCooldown - activeDeltaTime);
        if (_remainingCooldown <= 0f && TryAttack(board))
        {
            if (UsesAttackRecovery())
                BeginTargetAttackRecovery();
            else
                _remainingCooldown = GetEffectiveAttackCooldown();
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

    public void BindItemTargetHandler(
        System.Func<CharacterRuntime, bool> itemTargetHandler)
    {
        _itemTargetHandler = itemTargetHandler;
    }

    public bool ApplyAttackSpeedBoost(float multiplier, float duration)
    {
        multiplier = Mathf.Max(1f, multiplier);
        duration = TimePrecision.Normalize(duration, 0.1f);
        if (multiplier <= 1f || duration <= 0f)
            return false;

        if (_attackSpeedMultiplier < multiplier)
        {
            float ratio = multiplier / _attackSpeedMultiplier;
            _remainingCooldown /= ratio;
            _attackRecoveryRemaining /= ratio;
            _attackSpeedMultiplier = multiplier;
        }

        _attackSpeedBoostRemaining = Mathf.Max(
            _attackSpeedBoostRemaining,
            duration);
        RefreshUi();
        return true;
    }

    public bool ApplyPowerBoost(float multiplier, float duration)
    {
        multiplier = Mathf.Max(1f, multiplier);
        duration = TimePrecision.Normalize(duration, 0.1f);
        if (multiplier <= 1f || duration <= 0f)
            return false;

        _powerMultiplier = Mathf.Max(_powerMultiplier, multiplier);
        _powerBoostRemaining = Mathf.Max(_powerBoostRemaining, duration);
        RefreshUi();
        return true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null ||
            eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (_itemTargetHandler != null && _itemTargetHandler(this))
            return;

        TryActivateActiveSkill();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if ((!_initialized && !Initialize()) || Data == null)
            return;

        EnsureSkillTooltip();
        PositionSkillTooltip();
        RefreshSkillTooltip();
        _skillTooltip?.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _skillTooltip?.SetActive(false);
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
                    Data.SkillAttackDamage);
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
                    _dualSkillTimeRemaining > 0f
                        ? Data.SkillAttackDamage
                        : GetNormalAttackDamage());
                break;

            case CharacterAttackType.CrossHighestHealth:
                if (_areaSkillAttackCount > 0)
                {
                    int adjacentDamage = Mathf.Max(
                        1,
                        Mathf.FloorToInt(Data.SkillAttackDamage * 0.5f));
                    damageDealt = board.TryAttackCrossWithAdjacentSplash(
                        this,
                        Data.SkillAttackDamage,
                        adjacentDamage);
                    if (damageDealt > 0)
                        _areaSkillAttackCount--;
                }
                else
                {
                    damageDealt = board.TryAttackCrossAroundHighestHealthEnemy(
                        this,
                        GetNormalAttackDamage());
                }
                break;

            case CharacterAttackType.FireRandom:
                bool fireApplied = _fireSkillAttackCount > 0
                    ? board.TryApplyFireAroundRandomEnemies(
                        this,
                        Data.FireSkillTargetCount,
                        GetEffectiveFireDuration(),
                        Data.FireTickInterval,
                        Data.FireTickDamage)
                    : board.TryApplyFireToRandomEnemy(
                        this,
                        GetEffectiveFireDuration(),
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
                    GetNormalAttackDamage());
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
        _attackRecoveryRemaining =
            TargetAttackRecoveryDuration / _attackSpeedMultiplier;
    }

    private void TickTemporaryBoosts(float deltaTime)
    {
        if (_attackSpeedBoostRemaining > 0f)
        {
            _attackSpeedBoostRemaining = Mathf.Max(
                0f,
                _attackSpeedBoostRemaining - deltaTime);
            if (_attackSpeedBoostRemaining <= 0f &&
                _attackSpeedMultiplier > 1f)
            {
                _remainingCooldown *= _attackSpeedMultiplier;
                _attackRecoveryRemaining *= _attackSpeedMultiplier;
                _attackSpeedMultiplier = 1f;
            }
        }

        if (_powerBoostRemaining > 0f)
        {
            _powerBoostRemaining = Mathf.Max(
                0f,
                _powerBoostRemaining - deltaTime);
            if (_powerBoostRemaining <= 0f)
                _powerMultiplier = 1f;
        }
    }

    private float GetEffectiveAttackCooldown()
    {
        return Data != null
            ? Data.AttackCooldown / Mathf.Max(1f, _attackSpeedMultiplier)
            : 0f;
    }

    private int GetNormalAttackDamage()
    {
        return Data != null
            ? Mathf.Max(
                1,
                Mathf.RoundToInt(Data.AttackDamage * _powerMultiplier))
            : 0;
    }

    private float GetEffectiveFireDuration()
    {
        return Data != null
            ? TimePrecision.Normalize(
                Data.FireDuration * _powerMultiplier,
                0.1f)
            : 0f;
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

    private void EnsureSkillTooltip()
    {
        if (_skillTooltip != null && _skillTooltipText != null)
            return;

        Transform existing = transform.Find("grpSkillTooltip");
        if (existing != null)
            _skillTooltip = existing.gameObject;
        if (_skillTooltip == null)
        {
            _skillTooltip = new GameObject(
                "grpSkillTooltip",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasRenderer),
                typeof(CanvasGroup),
                typeof(Image));
            _skillTooltip.transform.SetParent(transform, false);
        }

        RectTransform tooltipRect =
            (RectTransform)_skillTooltip.transform;
        tooltipRect.sizeDelta = new Vector2(410f, 172f);
        tooltipRect.localScale = Vector3.one;

        Canvas tooltipCanvas = _skillTooltip.GetComponent<Canvas>();
        tooltipCanvas.overrideSorting = true;
        tooltipCanvas.sortingOrder = 200;

        CanvasGroup tooltipGroup = _skillTooltip.GetComponent<CanvasGroup>();
        tooltipGroup.interactable = false;
        tooltipGroup.blocksRaycasts = false;

        Image tooltipBackground = _skillTooltip.GetComponent<Image>();
        tooltipBackground.color = new Color(0.045f, 0.06f, 0.052f, 0.98f);
        tooltipBackground.raycastTarget = false;

        Transform textTransform = tooltipRect.Find("txtSkillTooltip");
        if (textTransform != null)
            _skillTooltipText = textTransform.GetComponent<TextMeshProUGUI>();
        if (_skillTooltipText == null)
        {
            GameObject textObject = new(
                "txtSkillTooltip",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(tooltipRect, false);
            _skillTooltipText = textObject.GetComponent<TextMeshProUGUI>();
        }

        RectTransform textRect = _skillTooltipText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 12f);
        textRect.offsetMax = new Vector2(-16f, -12f);
        _skillTooltipText.fontSize = 17f;
        _skillTooltipText.fontStyle = FontStyles.Bold;
        _skillTooltipText.color = new Color(0.94f, 0.91f, 0.78f, 1f);
        _skillTooltipText.alignment = TextAlignmentOptions.MidlineLeft;
        _skillTooltipText.textWrappingMode = TextWrappingModes.Normal;
        _skillTooltipText.raycastTarget = false;
        _skillTooltip.SetActive(false);
    }

    private void PositionSkillTooltip()
    {
        if (_skillTooltip == null)
            return;

        bool openToLeft = true;
        Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        RectTransform canvasRect = rootCanvas != null
            ? rootCanvas.transform as RectTransform
            : null;
        RectTransform turretRect = transform as RectTransform;
        if (canvasRect != null && turretRect != null)
        {
            Vector3 worldCenter = turretRect.TransformPoint(
                turretRect.rect.center);
            Vector3 canvasLocalCenter = canvasRect.InverseTransformPoint(
                worldCenter);
            openToLeft = canvasLocalCenter.x > canvasRect.rect.center.x;
        }

        RectTransform tooltipRect =
            (RectTransform)_skillTooltip.transform;
        float anchorX = openToLeft ? 0f : 1f;
        tooltipRect.anchorMin = new Vector2(anchorX, 0.5f);
        tooltipRect.anchorMax = new Vector2(anchorX, 0.5f);
        tooltipRect.pivot = new Vector2(openToLeft ? 1f : 0f, 0.5f);
        tooltipRect.anchoredPosition = new Vector2(
            openToLeft ? -12f : 12f,
            0f);
        tooltipRect.SetAsLastSibling();
    }

    private void RefreshSkillTooltip()
    {
        if (_skillTooltipText == null || Data == null)
            return;

        string effect = Data.AttackType switch
        {
            CharacterAttackType.RandomMultiple =>
                $"For {Data.ActiveSkillDuration:0.#}s, attacks target " +
                $"{Data.TargetCount + 2} enemies and deal " +
                $"{Data.SkillAttackDamage} damage each.",
            CharacterAttackType.CrossHighestHealth =>
                $"Next {Data.ActiveSkillAttackCount} attacks deal " +
                $"{Data.SkillAttackDamage} damage in the inner cross and " +
                $"{Mathf.Max(1, Mathf.FloorToInt(Data.SkillAttackDamage * 0.5f))} " +
                "damage to the outer and diagonal tiles.",
            CharacterAttackType.FireRandom =>
                $"Next {Data.ActiveSkillAttackCount} attacks choose " +
                $"{Data.FireSkillTargetCount} centers and apply fire for " +
                $"{GetEffectiveFireDuration():0.#}s in each 3x3 area. " +
                "Overlapping areas stack duration.",
            _ =>
                $"Deal {Data.SkillAttackDamage} damage to the " +
                "lowest-health enemy.",
        };
        string status = IsActiveSkillPending()
            ? "ACTIVE"
            : _activeSkillResource != null &&
              _activeSkillResource.Current >= Data.ActiveSkillCost
                ? "READY"
                : "NOT ENOUGH ENERGY";
        _skillTooltipText.text =
            $"ACTIVE SKILL  [C{Data.ActiveSkillCost}]  {status}\n" +
            effect + "\nCLICK TURRET TO ACTIVATE";
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
            ? $"FIRE {Data.FireDuration:0.#}s | SK x{Data.FireSkillTargetCount}"
            : $"ATK {Data.AttackDamage} | SK {Data.SkillAttackDamage}";
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
        float effectiveAttackCooldown = GetEffectiveAttackCooldown();
        cooldownFill.fillAmount = _attackRecoveryRemaining > 0f
            ? 0f
            : effectiveAttackCooldown > 0f
                ? 1f - Mathf.Clamp01(
                    _remainingCooldown / effectiveAttackCooldown)
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

        if (_skillTooltip != null && _skillTooltip.activeSelf)
            RefreshSkillTooltip();
    }
}
