using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

internal enum CharacterAbilityIconKind
{
    Details,
    Passive,
    Active,
}

[DisallowMultipleComponent]
internal sealed class CharacterAbilityIconView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    private CharacterRuntime _owner;
    private CharacterAbilityIconKind _kind;

    public void Configure(
        CharacterRuntime owner,
        CharacterAbilityIconKind kind)
    {
        _owner = owner;
        _kind = kind;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _owner?.ShowAbilityTooltip(_kind);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _owner?.HideAbilityTooltip(_kind);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_kind != CharacterAbilityIconKind.Active ||
            eventData == null ||
            eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        _owner?.HandleAbilityIconClick(_kind);
    }
}

[DisallowMultipleComponent]
internal sealed class CharacterInfoHoverView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private CharacterRuntime _owner;

    public void Configure(CharacterRuntime owner)
    {
        _owner = owner;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _owner?.ShowAbilityTooltip(
            CharacterAbilityIconKind.Details);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _owner?.HideAbilityTooltip(
            CharacterAbilityIconKind.Details);
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class CharacterRuntime : MonoBehaviour, IBattleCharacter,
    IBattleVfxAnchorProvider,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private const float AttackSdDisplayDuration = 0.45f;
    private const float PassiveSdDisplayDuration = 0.7f;
    private const float SkillSdDisplayDuration = 0.9f;
    private const int MaximumStatusChangesPerDispatch = 128;
    private const float AbilityIconSize = 48f;
    private static readonly Color AbilityIconFrameColor =
        new(0.055f, 0.07f, 0.062f, 0.98f);
    private static readonly Color UnavailableAbilityIconColor =
        new(0.28f, 0.28f, 0.28f, 1f);

    private struct RectLayout
    {
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 Pivot;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;

        public static RectLayout Capture(RectTransform rect)
        {
            return new RectLayout
            {
                AnchorMin = rect.anchorMin,
                AnchorMax = rect.anchorMax,
                Pivot = rect.pivot,
                AnchoredPosition = rect.anchoredPosition,
                SizeDelta = rect.sizeDelta,
            };
        }

        public void Apply(RectTransform rect)
        {
            rect.anchorMin = AnchorMin;
            rect.anchorMax = AnchorMax;
            rect.pivot = Pivot;
            rect.anchoredPosition = AnchoredPosition;
            rect.sizeDelta = SizeDelta;
        }
    }

    private readonly struct AbilityTargetSelection
    {
        public CharacterTargetFaction Faction { get; }
        public IReadOnlyList<EnemyRuntime> EnemyTargets { get; }
        public IReadOnlyList<IBattleCharacter> AllyTargets { get; }
        public bool RangeAlreadyShown { get; }
        public int Count => Faction == CharacterTargetFaction.Ally
            ? AllyTargets?.Count ?? 0
            : EnemyTargets?.Count ?? 0;

        private AbilityTargetSelection(
            CharacterTargetFaction faction,
            IReadOnlyList<EnemyRuntime> enemyTargets,
            IReadOnlyList<IBattleCharacter> allyTargets,
            bool rangeAlreadyShown)
        {
            Faction = faction;
            EnemyTargets = enemyTargets ?? System.Array.Empty<EnemyRuntime>();
            AllyTargets = allyTargets ??
                System.Array.Empty<IBattleCharacter>();
            RangeAlreadyShown = rangeAlreadyShown;
        }

        public static AbilityTargetSelection Enemies(
            IReadOnlyList<EnemyRuntime> targets,
            bool rangeAlreadyShown = false)
        {
            return new AbilityTargetSelection(
                CharacterTargetFaction.Enemy,
                targets,
                null,
                rangeAlreadyShown);
        }

        public static AbilityTargetSelection Allies(
            IReadOnlyList<IBattleCharacter> targets)
        {
            return new AbilityTargetSelection(
                CharacterTargetFaction.Ally,
                null,
                targets,
                false);
        }
    }

    private readonly struct PreparedSkillAction
    {
        public CharacterSkillDefinition Definition { get; }
        public AbilityTargetSelection Targets { get; }
        public AbilityTargetSelection SelectedTargets { get; }
        public IReadOnlyList<PreparedEffectExecution> Effects { get; }
        public int Damage { get; }

        public PreparedSkillAction(
            CharacterSkillDefinition definition,
            AbilityTargetSelection targets,
            AbilityTargetSelection selectedTargets,
            IReadOnlyList<PreparedEffectExecution> effects,
            int damage)
        {
            Definition = definition;
            Targets = targets;
            SelectedTargets = selectedTargets;
            Effects = effects ??
                System.Array.Empty<PreparedEffectExecution>();
            Damage = damage;
        }
    }

    private readonly struct PreparedEffectExecution
    {
        public bool IsPrepared { get; }
        public AbilityTargetSelection Targets { get; }
        public int ResourceSpendAmount { get; }
        public int HealthSpendAmount { get; }

        public PreparedEffectExecution(
            AbilityTargetSelection targets,
            int resourceSpendAmount = 0,
            int healthSpendAmount = 0)
        {
            IsPrepared = true;
            Targets = targets;
            ResourceSpendAmount = Mathf.Max(
                0,
                resourceSpendAmount);
            HealthSpendAmount = Mathf.Max(
                0,
                healthSpendAmount);
        }
    }

    private sealed class PendingManualPassiveAction
    {
        public IBattleBoard Board { get; }
        public CharacterPassiveDefinition Definition { get; }
        public CharacterActionConditionData Condition { get; }
        public AbilityTargetSelection InheritedTargets { get; }

        public PendingManualPassiveAction(
            IBattleBoard board,
            CharacterPassiveDefinition definition,
            CharacterActionConditionData condition,
            AbilityTargetSelection inheritedTargets)
        {
            Board = board;
            Definition = definition;
            Condition = condition;
            InheritedTargets = inheritedTargets;
        }
    }

    private sealed class EffectCostReservation
    {
        private readonly IActiveSkillResource _resource;
        private readonly IBattleCharacter _source;
        private readonly int _baseCost;
        private int _reservedAmount;
        private int _reservedHealth;

        private EffectCostReservation(
            IActiveSkillResource resource,
            IBattleCharacter source,
            int baseCost)
        {
            _resource = resource;
            _source = source;
            _baseCost = Mathf.Max(0, baseCost);
            _reservedAmount = _baseCost;
        }

        public static bool TryCreate(
            IActiveSkillResource resource,
            IBattleCharacter source,
            int baseCost,
            out EffectCostReservation reservation)
        {
            reservation = null;
            baseCost = Mathf.Max(0, baseCost);
            if (source == null ||
                (baseCost > 0 && resource == null) ||
                (baseCost > 0 && !resource.CanSpend(baseCost)))
            {
                return false;
            }

            reservation = new EffectCostReservation(
                resource,
                source,
                baseCost);
            return true;
        }

        public bool TryReserveEffectSpend(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (_resource == null || amount == 0 ||
                _resource.Current - _reservedAmount < amount)
            {
                return false;
            }

            _reservedAmount += amount;
            return true;
        }

        public bool TryReserveHealthSpend(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (amount == 0 ||
                _source.CurrentHealth - _reservedHealth - amount < 1)
            {
                return false;
            }

            _reservedHealth += amount;
            return true;
        }

        public bool TryCommitBaseCost()
        {
            return _baseCost == 0 ||
                   _resource.TrySpend(_baseCost);
        }
    }

    private readonly struct TargetDamageSnapshot
    {
        public EnemyRuntime Target { get; }
        public int Damage { get; }

        public TargetDamageSnapshot(
            EnemyRuntime target,
            int damage)
        {
            Target = target;
            Damage = Mathf.Max(0, damage);
        }
    }

    [SerializeField] private CharacterSO original;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI cooldownText;
    [SerializeField] private Image cooldownFill;
    [SerializeField] private AudioSource attackSfxSpeaker;
    [SerializeField] private Image sdImage;

    private bool _initialized;
    private int _currentHealth;
    private int _currentShield;
    private float _remainingCooldown;
    private readonly Dictionary<string, StatusEffectRuntimeState>
        _statusEffects =
        new(System.StringComparer.Ordinal);
    private readonly Queue<BattleStatusChangedEvent> _statusChangeQueue =
        new();
    private bool _dispatchingStatusChanges;
    private bool _suppressStatusTriggers;
    private float _attackRecoveryRemaining;
    private float _attackSpeedBoostRemaining;
    private float _attackSpeedMultiplier = 1f;
    private float _powerBoostRemaining;
    private float _powerMultiplier = 1f;
    private float _attackSdTimeRemaining;
    private float _passiveSdTimeRemaining;
    private float _skillSdTimeRemaining;
    private IActiveSkillResource _activeSkillResource;
    private IBattleBoard _board;
    private Image _panelImage;
    private Color _defaultPanelColor;
    private bool _manualTargetCandidate;
    private bool _manualTargetSelected;
    private System.Func<CharacterRuntime, bool> _manualTargetHandler;
    private System.Func<CharacterRuntime, bool> _itemTargetHandler;
    private GameObject _skillTooltip;
    private TextMeshProUGUI _skillTooltipText;
    private CharacterAbilityIconKind _skillTooltipKind =
        CharacterAbilityIconKind.Active;
    private Image _passiveIconFrame;
    private Image _passiveIconImage;
    private Image _activeSkillIconFrame;
    private Image _activeSkillIconImage;
    private bool _infoLayoutCached;
    private bool _sdLayoutEnabled;
    private RectTransform _cooldownTrack;
    private RectLayout _nameLayout;
    private RectLayout _attackLayout;
    private RectLayout _cooldownLayout;
    private RectLayout _cooldownTrackLayout;
    private readonly Dictionary<CharacterPassiveDefinition, float>
        _passiveCooldowns = new();
    private bool _lastAttackAttempted;
    private bool _lastAttackSucceeded;
    private AbilityTargetSelection _lastAttackTargets;
    private AbilityTargetSelection _previousAttackAttemptTargets;
    private readonly Dictionary<int, AbilityTargetSelection>
        _retainedAttackTargets = new();
    private bool _manualTargetRequestPending;
    private bool _hasCompletedManualTargetSelection;
    private bool _manualTargetSelectionCancelled;
    private AbilityTargetSelection _completedManualTargetSelection;
    private bool _resumeActiveSkillAfterManualSelection;
    private readonly List<PendingManualPassiveAction>
        _pendingManualPassiveActions = new();
    private bool _replayingManualPassiveAction;

    public CharacterSO Definition => original;
    public CharacterData Data { get; private set; }
    internal IActiveSkillResource ActiveSkillResource =>
        _activeSkillResource;
    internal IBattleBoard BoundBattleBoard => _board;
    public int PartySlotIndex { get; private set; } = -1;
    public int PartySlotNumber => PartySlotIndex + 1;
    public Color EffectColor { get; private set; } = Color.white;
    public int TotalDamageDealt { get; private set; }
    public int CurrentHealth => Mathf.Clamp(
        _currentHealth,
        0,
        MaximumHealth);
    public int MaximumHealth => Mathf.Max(1, Data?.MaximumHealth ?? 1);
    public int CurrentShield => Mathf.Max(0, _currentShield);
    public float DisabledTimeRemaining
    {
        get
        {
            float duration = GetDisabledDuration();
            return float.IsPositiveInfinity(duration)
                ? duration
                : TimePrecision.FloorToTenth(duration);
        }
    }
    public StatusEffectSO DisabledStatusEffect => GetDisabledStatusEffect();
    public float CurrentAttackPower => GetEffectiveAttackPower();
    public float CurrentAttackSpeed => GetEffectiveAttackSpeed();
    public bool AreAllActionsDisabled => IsActionDisabled();
    public bool IsBasicAttackBlocked =>
        AreAllActionsDisabled ||
        HasStatusControl(StatusEffectControlType.DisableBasicAttack);
    public bool IsActiveSkillBlocked =>
        AreAllActionsDisabled ||
        HasStatusControl(StatusEffectControlType.DisableActiveSkill);
    public bool ArePassiveCooldownsPaused =>
        AreAllActionsDisabled ||
        HasStatusControl(StatusEffectControlType.PausePassiveCooldowns);
    public event System.Action<BattleStatusChangedEvent> StatusChanged;
    public event System.Action<StatusEffectLifecycleEvent> StatusLifecycle;

    public bool TryGetVfxAnchor(
        BattleVfxAnchorType anchorType,
        out BattleVfxAnchorSnapshot snapshot)
    {
        RectTransform anchorRect = sdImage != null
            ? sdImage.rectTransform
            : transform as RectTransform;
        return BattleVfxUiAnchorUtility.TryCreateScreenAnchor(
            anchorRect,
            anchorType,
            out snapshot);
    }

    public IReadOnlyList<BattleStatusSnapshot> GetActiveStatusEffects()
    {
        if (_statusEffects.Count == 0)
            return System.Array.Empty<BattleStatusSnapshot>();

        List<BattleStatusSnapshot> snapshots =
            new(_statusEffects.Count);
        foreach (StatusEffectRuntimeState state in _statusEffects.Values)
        {
            if (state == null || !state.HasStacks ||
                state.Definition == null)
            {
                continue;
            }

            snapshots.Add(CreateStatusSnapshot(state));
        }

        snapshots.Sort((left, right) => string.Compare(
            left.Definition?.StatusId,
            right.Definition?.StatusId,
            System.StringComparison.Ordinal));
        return snapshots.Count > 0
            ? snapshots.ToArray()
            : System.Array.Empty<BattleStatusSnapshot>();
    }

    public bool HasStatusEffect(StatusEffectSO definition)
    {
        if (definition == null)
            return false;

        return !string.IsNullOrWhiteSpace(definition.StatusId) &&
               _statusEffects.TryGetValue(
                   definition.StatusId,
                   out StatusEffectRuntimeState state) &&
               state != null && state.HasStacks;
    }

    public int GetStatusStackCount(StatusEffectSO definition)
    {
        if (definition == null)
            return 0;

        return _statusEffects.TryGetValue(
            definition.StatusId,
            out StatusEffectRuntimeState state)
                ? Mathf.Max(0, state.StackCount)
                : 0;
    }

    public bool TryConsumeStatusStacks(
        StatusEffectSO definition,
        int stackCount)
    {
        stackCount = Mathf.Max(1, stackCount);
        if (GetStatusStackCount(definition) < stackCount)
            return false;

        return RemoveStatusEffects(
            CharacterStatusRemovalTarget.Single,
            definition,
            stackCount) == stackCount;
    }

    public int Heal(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount <= 0 || _currentHealth <= 0 ||
            _currentHealth >= MaximumHealth)
        {
            return 0;
        }

        int previous = _currentHealth;
        _currentHealth = Mathf.Min(
            MaximumHealth,
            _currentHealth + amount);
        RefreshUi();
        return _currentHealth - previous;
    }

    public int GainShield(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount <= 0 || _currentHealth <= 0 ||
            _currentShield == int.MaxValue)
        {
            return 0;
        }

        int previous = _currentShield;
        long total = (long)_currentShield + amount;
        _currentShield = total >= int.MaxValue
            ? int.MaxValue
            : (int)total;
        RefreshUi();
        return _currentShield - previous;
    }

    public int TakeDamage(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount <= 0 || _currentHealth <= 0)
            return 0;

        amount = ResolveIncomingDamage(amount);
        if (amount <= 0)
            return 0;

        int appliedDamage = 0;
        if (_currentShield > 0)
        {
            int shieldDamage = Mathf.Min(_currentShield, amount);
            _currentShield -= shieldDamage;
            amount -= shieldDamage;
            appliedDamage += shieldDamage;
        }

        if (amount > 0)
        {
            int healthDamage = Mathf.Min(_currentHealth, amount);
            _currentHealth -= healthDamage;
            appliedDamage += healthDamage;
        }

        if (appliedDamage > 0)
            RefreshUi();
        return appliedDamage;
    }

    public bool CanSpendHealth(int amount)
    {
        return amount > 0 && _currentHealth - amount >= 1;
    }

    public bool TrySpendHealth(int amount)
    {
        if (!CanSpendHealth(amount))
            return false;

        _currentHealth -= amount;
        RefreshUi();
        return true;
    }

    private void Awake()
    {
        // Party slots may intentionally start without a definition. They are
        // initialized later when DungeonPage assigns a CharacterSO through
        // ConfigureDefinition().
        if (original != null)
            Initialize();
    }

    private void OnEnable()
    {
        LocalizationService.LocaleChanged += HandleLocaleChanged;
        if (_initialized)
            RefreshUi();
    }

    private void OnDestroy()
    {
        LocalizationService.LocaleChanged -= HandleLocaleChanged;
        _manualTargetHandler = null;
        _itemTargetHandler = null;
        BindBattle(null, null);
        SetCharacterData(null);
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

        LocalizationFontResolver.ApplyGameDefault(nameText);
        LocalizationFontResolver.ApplyGameDefault(attackText);
        LocalizationFontResolver.ApplyGameDefault(cooldownText);

        SetCharacterData(CreateCharacterData(original));
        _currentHealth = Data.MaximumHealth;
        InitializeAttackSfxSpeaker();
        _remainingCooldown = GetEffectiveAttackCooldown();
        _panelImage = GetComponent<Image>();
        if (_panelImage != null)
        {
            _defaultPanelColor = _panelImage.color;
            _panelImage.raycastTarget = true;
        }

        _initialized = true;
        EnsureSdInfoView();
        EnsureAbilityIconView();
        EnsureSkillTooltip();
        RefreshUi();
        return true;
    }

    private void OnDisable()
    {
        LocalizationService.LocaleChanged -= HandleLocaleChanged;
        _skillTooltip?.SetActive(false);
    }

    public void BindBattle(
        IActiveSkillResource activeSkillResource,
        IBattleBoard board)
    {
        if (!ReferenceEquals(_board, board))
        {
            _retainedAttackTargets.Clear();
            _previousAttackAttemptTargets = default;
        }

        if (_board != null)
        {
            _board.StatusApplied -= HandleStatusApplied;
            _board.EnemyDefeated -= HandleEnemyDefeated;
        }
        if (_activeSkillResource != null)
            _activeSkillResource.Changed -= HandleActiveSkillResourceChanged;

        _activeSkillResource = activeSkillResource;
        _board = board;

        if (_activeSkillResource != null)
            _activeSkillResource.Changed += HandleActiveSkillResourceChanged;
        if (_board != null)
        {
            _board.StatusApplied += HandleStatusApplied;
            _board.EnemyDefeated += HandleEnemyDefeated;
        }

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

        SetCharacterData(CreateCharacterData(original));
        ResetRuntime();
        return true;
    }

    private void SetCharacterData(CharacterData data)
    {
        if (Data != null)
            Data.StatsChanged -= HandleCharacterStatsChanged;

        Data = data;
        if (Data != null)
            Data.StatsChanged += HandleCharacterStatsChanged;
    }

    private void HandleCharacterStatsChanged()
    {
        _currentHealth = Mathf.Clamp(
            _currentHealth,
            0,
            MaximumHealth);
        RefreshUi();
    }

    private static CharacterData CreateCharacterData(CharacterSO definition)
    {
        if (definition == null)
            return null;

        CharacterCollectionData collection =
            DataManager.Current?.CharacterDatas;
        return collection != null
            ? collection.CreateRuntimeData(definition)
            : definition.CreateData(new CharacterProgressData(
                definition.CharacterId,
                definition.InitiallyOwned));
    }

    public bool ApplyDungeonUpgrade(
        int definitionIndex,
        CharacterDungeonUpgradeType upgradeType)
    {
        if ((!_initialized && !Initialize()) || Data == null)
            return false;

        float previousCooldown = Data.AttackCooldown;
        if (!Data.ApplyDungeonUpgrade(definitionIndex, upgradeType))
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
        _currentHealth = MaximumHealth;
        _currentShield = 0;
        IReadOnlyList<BattleStatusSnapshot> removedStatuses =
            GetActiveStatusEffects();
        _statusEffects.Clear();
        _suppressStatusTriggers = true;
        try
        {
            foreach (BattleStatusSnapshot removedStatus in removedStatuses)
            {
                NotifyStatusChanged(
                    BattleStatusChangeType.Removed,
                    removedStatus,
                    new BattleStatusSnapshot(
                        removedStatus.Definition,
                        0,
                        0f));
            }
        }
        finally
        {
            _suppressStatusTriggers = false;
        }
        _remainingCooldown = GetEffectiveAttackCooldown();
        _attackRecoveryRemaining = 0f;
        _attackSdTimeRemaining = 0f;
        _passiveSdTimeRemaining = 0f;
        _skillSdTimeRemaining = 0f;
        _lastAttackAttempted = false;
        _lastAttackSucceeded = false;
        _lastAttackTargets = default;
        _previousAttackAttemptTargets = default;
        _retainedAttackTargets.Clear();
        _manualTargetRequestPending = false;
        _hasCompletedManualTargetSelection = false;
        _manualTargetSelectionCancelled = false;
        _completedManualTargetSelection = default;
        _resumeActiveSkillAfterManualSelection = false;
        _pendingManualPassiveActions.Clear();
        ResetPassiveCooldowns();
        TotalDamageDealt = 0;
        EnsureSdInfoView();
        RefreshUi();
    }

    public void TickBattle(float deltaTime, IBattleBoard board)
    {
        if ((!_initialized && !Initialize()) || board == null || deltaTime <= 0f)
            return;

        _board = board;
        if (ProcessPendingManualActions())
        {
            RefreshUi();
            return;
        }

        TickSdActionTimers(deltaTime);
        TickTemporaryBoosts(deltaTime);

        bool passiveCooldownPaused = ArePassiveCooldownsPaused;
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

        float disabledDuration = GetDisabledDuration();
        TickGenericStatusEffects(deltaTime, activeDeltaTime);
        if (_manualTargetRequestPending)
        {
            RefreshUi();
            return;
        }
        activeDeltaTime -= Mathf.Min(activeDeltaTime, disabledDuration);

        if (!passiveCooldownPaused)
            TickCooldownPassives(deltaTime, board);
        if (_manualTargetRequestPending)
        {
            RefreshUi();
            return;
        }

        if (activeDeltaTime <= 0f)
        {
            RefreshUi();
            return;
        }

        _remainingCooldown = Mathf.Max(0f, _remainingCooldown - activeDeltaTime);
        if (_remainingCooldown <= 0f &&
            !IsBasicAttackBlocked &&
            TryAttack(board))
        {
            if (Data.AttackRecoveryDuration > 0f)
                BeginAttackRecovery(Data.AttackRecoveryDuration);
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
        StatusEffectSO stun =
            StatusEffectDefinitionCatalog.FindById(StatusEffectIds.Stun);
        if (!ApplyStatusEffect(stun, duration, 1))
        {
            Debug.LogWarning(
                "Unable to apply the configured Stun status effect.",
                this);
        }
    }

    public bool ApplyStatusEffect(
        StatusEffectSO definition,
        float duration,
        int stacks)
    {
        return ApplyStatusEffect(
            definition,
            duration,
            stacks,
            null);
    }

    public bool ApplyStatusEffect(
        StatusEffectSO definition,
        float duration,
        int stacks,
        IBattleCharacter source)
    {
        if (definition == null || !definition.CanTargetAlly || stacks <= 0 ||
            string.IsNullOrWhiteSpace(definition.StatusId))
        {
            return false;
        }

        float remainingDuration = ResolveStatusDuration(definition, duration);
        if (remainingDuration <= 0f)
            return false;

        float previousAttackSpeed = GetEffectiveAttackSpeed();
        int previousStacks = GetStatusStackCount(definition);
        BattleStatusSnapshot previousSnapshot = default;
        bool wasActive = false;
        if (!_statusEffects.TryGetValue(
                definition.StatusId,
                out StatusEffectRuntimeState state))
        {
            state = new StatusEffectRuntimeState(definition);
            _statusEffects.Add(definition.StatusId, state);
        }
        else if (state != null && state.HasStacks)
        {
            wasActive = true;
            previousSnapshot = CreateStatusSnapshot(state);
        }

        StatusEffectRuntimeMutation mutation = state.Apply(
            stacks,
            remainingDuration,
            definition.TickInterval,
            source);
        if (!mutation.Succeeded)
        {
            if (!state.HasStacks)
                _statusEffects.Remove(definition.StatusId);
            return false;
        }

        int currentStacks = GetStatusStackCount(definition);
        BattleStatusSnapshot currentSnapshot =
            CreateStatusSnapshot(state);
        NotifyStatusChanged(
            wasActive
                ? BattleStatusChangeType.Reapplied
                : BattleStatusChangeType.Applied,
            previousSnapshot,
            currentSnapshot);
        AdjustCooldownForAttackSpeedChange(
            previousAttackSpeed,
            GetEffectiveAttackSpeed());
        RefreshUi();
        if (MatchesActiveStatusSnapshot(currentSnapshot))
        {
            NotifyStatusApplied(
                definition,
                previousStacks,
                currentStacks,
                source);
        }
        return true;
    }

    public int RemoveStatusEffects(
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        int removalCount)
    {
        if (removalCount < 0)
            return 0;

        return RemoveStatusEffects(
            new CharacterStatusRemovalSelection(
                removalTarget,
                statusEffect),
            CharacterStatusRemovalAmount.Fixed(removalCount));
    }

    public int RemoveStatusEffects(
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        CharacterStatusRemovalAmount removalAmount)
    {
        return RemoveStatusEffects(
            new CharacterStatusRemovalSelection(
                removalTarget,
                statusEffect),
            removalAmount);
    }

    public int RemoveStatusEffects(
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount)
    {
        float previousAttackSpeed = GetEffectiveAttackSpeed();
        int removed = RemoveMatchingStatusEffects(
            removalSelection,
            removalAmount);
        if (removed > 0)
        {
            AdjustCooldownForAttackSpeedChange(
                previousAttackSpeed,
                GetEffectiveAttackSpeed());
            RefreshUi();
        }
        return removed;
    }

    private int RemoveSingleStatusEffect(
        StatusEffectSO definition,
        CharacterStatusRemovalAmount removalAmount)
    {
        if (definition == null || !definition.Removable)
            return 0;

        int removalCount = removalAmount.Resolve(
            GetStatusStackCount(definition));
        return removalCount > 0
            ? RemoveStatusStacks(definition.StatusId, removalCount)
            : 0;
    }

    private int RemoveMatchingStatusEffects(
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount)
    {
        List<StatusEffectSO> candidates =
            CollectStatusRemovalCandidates(removalSelection);
        int selectedCount = CharacterStatusRemovalPick.SelectInPlace(
            candidates,
            removalSelection);
        int removed = 0;
        for (int index = 0; index < selectedCount; index++)
        {
            removed += RemoveSingleStatusEffect(
                candidates[index],
                removalAmount);
        }

        return removed;
    }

    private List<StatusEffectSO> CollectStatusRemovalCandidates(
        CharacterStatusRemovalSelection removalSelection)
    {
        List<StatusEffectSO> candidates = new();
        HashSet<string> visitedIds = new(StringComparer.Ordinal);
        if (removalSelection.Target ==
            CharacterStatusRemovalTarget.Single)
        {
            for (int index = 0;
                 index < removalSelection.ExplicitStatusCount;
                 index++)
            {
                AddStatusRemovalCandidate(
                    candidates,
                    visitedIds,
                    removalSelection.GetExplicitStatus(index),
                    removalSelection);
            }
        }
        else
        {
            foreach (StatusEffectRuntimeState state in
                     _statusEffects.Values)
            {
                AddStatusRemovalCandidate(
                    candidates,
                    visitedIds,
                    state?.Definition,
                    removalSelection);
            }
        }

        candidates.Sort((left, right) => string.Compare(
            left?.StatusId,
            right?.StatusId,
            StringComparison.Ordinal));
        return candidates;
    }

    private void AddStatusRemovalCandidate(
        List<StatusEffectSO> candidates,
        HashSet<string> visitedIds,
        StatusEffectSO definition,
        CharacterStatusRemovalSelection removalSelection)
    {
        if (definition == null || !definition.Removable ||
            string.IsNullOrWhiteSpace(definition.StatusId) ||
            !visitedIds.Add(definition.StatusId) ||
            !_statusEffects.ContainsKey(definition.StatusId) ||
            !removalSelection.MatchesStatus(definition))
        {
            return;
        }

        candidates.Add(definition);
    }

    private int RemoveStatusStacks(
        string statusId,
        int removalCount)
    {
        if (string.IsNullOrWhiteSpace(statusId) ||
            !_statusEffects.TryGetValue(
                statusId,
                out StatusEffectRuntimeState state) ||
            state.Definition == null || !state.Definition.Removable)
        {
            return 0;
        }

        BattleStatusSnapshot previousSnapshot =
            CreateStatusSnapshot(state);
        StatusEffectRuntimeMutation mutation =
            state.RemoveStacks(removalCount);
        if (!mutation.Succeeded)
            return 0;

        BattleStatusSnapshot currentSnapshot =
            CreateStatusSnapshot(state);
        if (!state.HasStacks)
            _statusEffects.Remove(statusId);
        NotifyStatusChanged(
            state.HasStacks
                ? BattleStatusChangeType.StackChanged
                : BattleStatusChangeType.Removed,
            previousSnapshot,
            currentSnapshot);
        return mutation.RemovedStacks;
    }

    private void TickGenericStatusEffects(
        float deltaTime,
        float disableDeltaTime)
    {
        if (deltaTime <= 0f || _statusEffects.Count == 0)
            return;

        float previousAttackSpeed = GetEffectiveAttackSpeed();
        bool changed = false;
        List<string> statusIds = new(_statusEffects.Keys);
        foreach (string statusId in statusIds)
        {
            if (!_statusEffects.TryGetValue(
                    statusId,
                    out StatusEffectRuntimeState state) ||
                state == null)
            {
                continue;
            }

            float stateDeltaTime = HasContinuousStatusOperation(
                state.Definition,
                StatusEffectOperationType.DisableAction)
                    ? disableDeltaTime
                    : deltaTime;
            changed |= AdvanceStatusState(state, stateDeltaTime);
            if (!state.HasStacks)
                _statusEffects.Remove(statusId);
        }

        if (changed)
        {
            AdjustCooldownForAttackSpeedChange(
                previousAttackSpeed,
                GetEffectiveAttackSpeed());
            RefreshUi();
        }
    }

    private bool AdvanceStatusState(
        StatusEffectRuntimeState state,
        float deltaTime)
    {
        if (state == null || deltaTime <= 0f)
            return false;

        bool changed = false;
        float remainingDelta = deltaTime;
        while (remainingDelta > 0f && state.HasStacks)
        {
            if (RemoveExpiredStatusBatch(state))
            {
                changed = true;
                continue;
            }

            StatusEffectRuntimeBatch activeBatch = state.ActiveBatch;
            float activeDelta = state.AdvanceActiveDuration(remainingDelta);
            if (activeDelta <= 0f)
                break;

            remainingDelta -= activeDelta;
            int tickCount = state.ConsumePendingTickCount();
            if (tickCount > 0 && activeBatch != null)
            {
                StatusEffectLifecycleEvent tick =
                    StatusEffectLifecycleResolver.ResolveTick(
                        BattleStatusTarget.FromAlly(this),
                        new BattleStatusSnapshot(
                            state.Definition,
                            activeBatch.Stacks,
                            activeBatch.RemainingDuration,
                            activeBatch.Source),
                        tickCount);
                StatusLifecycle?.Invoke(tick);
                StatusEffectTriggerExecutor.Execute(tick, _board);
            }
            if (RemoveExpiredStatusBatch(state))
                changed = true;
        }

        return changed;
    }

    private bool RemoveExpiredStatusBatch(StatusEffectRuntimeState state)
    {
        if (state == null)
            return false;

        BattleStatusSnapshot previousSnapshot =
            CreateStatusSnapshot(state);
        StatusEffectRuntimeMutation mutation =
            state.RemoveExpiredActiveBatch();
        if (!mutation.Succeeded)
            return false;

        BattleStatusSnapshot currentSnapshot =
            CreateStatusSnapshot(state);
        NotifyStatusChanged(
            state.HasStacks
                ? BattleStatusChangeType.StackChanged
                : BattleStatusChangeType.Expired,
            previousSnapshot,
            currentSnapshot);
        return true;
    }

    private static BattleStatusSnapshot CreateStatusSnapshot(
        StatusEffectRuntimeState state)
    {
        return state?.Definition != null
            ? new BattleStatusSnapshot(
                state.Definition,
                state.StackCount,
                state.RemainingDuration,
                state.ActiveBatch?.Source)
            : default;
    }

    private bool MatchesActiveStatusSnapshot(
        BattleStatusSnapshot expected)
    {
        if (!expected.IsValid ||
            !_statusEffects.TryGetValue(
                expected.Definition.StatusId,
                out StatusEffectRuntimeState state) ||
            state == null || !state.HasStacks)
        {
            return false;
        }

        BattleStatusSnapshot current = CreateStatusSnapshot(state);
        return ReferenceEquals(current.Definition, expected.Definition) &&
               current.StackCount == expected.StackCount &&
               (current.RemainingDuration.Equals(
                    expected.RemainingDuration) ||
                Mathf.Approximately(
                    current.RemainingDuration,
                    expected.RemainingDuration));
    }

    private bool IsActionDisabled()
    {
        return HasStatusControl(
            StatusEffectControlType.DisableAllActions);
    }

    private float GetDisabledDuration()
    {
        float longestDuration = 0f;
        foreach (StatusEffectRuntimeState state in _statusEffects.Values)
        {
            if (state == null || !state.HasStacks ||
                !DefinitionHasControl(
                    state.Definition,
                    StatusEffectControlType.DisableAllActions))
            {
                continue;
            }

            longestDuration = Mathf.Max(
                longestDuration,
                state.RemainingDuration);
        }

        return longestDuration;
    }

    private StatusEffectSO GetDisabledStatusEffect()
    {
        StatusEffectSO result = null;
        float longestDuration = 0f;
        foreach (StatusEffectRuntimeState state in _statusEffects.Values)
        {
            if (state == null || !state.HasStacks ||
                !DefinitionHasControl(
                    state.Definition,
                    StatusEffectControlType.DisableAllActions))
            {
                continue;
            }

            if (result == null ||
                state.RemainingDuration > longestDuration)
            {
                result = state.Definition;
                longestDuration = state.RemainingDuration;
            }
        }

        return result;
    }

    private static bool HasContinuousStatusOperation(
        StatusEffectSO definition,
        StatusEffectOperationType operationType)
    {
        if (operationType == StatusEffectOperationType.DisableAction &&
            DefinitionHasControl(
                definition,
                StatusEffectControlType.DisableAllActions))
        {
            return true;
        }

        if (definition?.Operations == null)
            return false;

        foreach (StatusEffectOperationDefinition operation in
                 definition.Operations)
        {
            if (operation != null &&
                operation.Trigger == StatusEffectOperationTrigger.OnApply &&
                operation.OperationType == operationType)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasStatusControl(StatusEffectControlType controlType)
    {
        foreach (StatusEffectRuntimeState state in _statusEffects.Values)
        {
            if (state != null && state.HasStacks &&
                DefinitionHasControl(state.Definition, controlType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DefinitionHasControl(
        StatusEffectSO definition,
        StatusEffectControlType controlType)
    {
        if (definition == null)
            return false;
        if (definition.HasControl(controlType))
            return true;
        if (controlType != StatusEffectControlType.DisableAllActions ||
            definition.Operations == null)
        {
            return false;
        }

        foreach (StatusEffectOperationDefinition operation in
                 definition.Operations)
        {
            if (operation != null &&
                operation.Trigger ==
                    StatusEffectOperationTrigger.OnApply &&
                operation.OperationType ==
                    StatusEffectOperationType.DisableAction)
            {
                return true;
            }
        }

        return false;
    }

    private static float ResolveStatusDuration(
        StatusEffectSO definition,
        float duration)
    {
        if (definition.DurationMode == StatusEffectDurationMode.Permanent)
            return float.PositiveInfinity;

        return TimePrecision.Normalize(
            duration > 0f ? duration : definition.DefaultDuration,
            0.1f);
    }

    private void NotifyStatusApplied(
        StatusEffectSO definition,
        int previousStacks,
        int currentStacks,
        IBattleCharacter source)
    {
        _board?.NotifyStatusApplied(new BattleStatusAppliedEvent(
            BattleStatusTarget.FromAlly(this),
            definition,
            previousStacks,
            currentStacks,
            source));
    }

    private void NotifyStatusChanged(
        BattleStatusChangeType changeType,
        BattleStatusSnapshot previous,
        BattleStatusSnapshot current)
    {
        BattleStatusChangedEvent eventData = new(
            BattleStatusTarget.FromAlly(this),
            changeType,
            previous,
            current);
        if (eventData.IsValid)
            _statusChangeQueue.Enqueue(eventData);
        DispatchStatusChanges();
    }

    private void DispatchStatusChanges()
    {
        if (_dispatchingStatusChanges)
            return;

        _dispatchingStatusChanges = true;
        try
        {
            int dispatchedCount = 0;
            while (_statusChangeQueue.Count > 0)
            {
                if (dispatchedCount >= MaximumStatusChangesPerDispatch)
                {
                    int discardedCount = _statusChangeQueue.Count;
                    _statusChangeQueue.Clear();
                    Debug.LogError(
                        $"Status change dispatch exceeded " +
                        $"{MaximumStatusChangesPerDispatch} events. " +
                        $"Discarded {discardedCount} queued events to stop " +
                        $"a re-entrant lifecycle loop.",
                        this);
                    break;
                }

                BattleStatusChangedEvent eventData =
                    _statusChangeQueue.Dequeue();
                dispatchedCount++;
                StatusChanged?.Invoke(eventData);
                if (!_suppressStatusTriggers)
                {
                    foreach (StatusEffectLifecycleEvent lifecycleEvent in
                             StatusEffectLifecycleResolver.Resolve(eventData))
                    {
                        StatusLifecycle?.Invoke(lifecycleEvent);
                        StatusEffectTriggerExecutor.Execute(
                            lifecycleEvent,
                            _board);
                    }
                }
            }
        }
        finally
        {
            _dispatchingStatusChanges = false;
        }
    }

    public void NotifyPassiveActivated()
    {
        NotifyPassiveActivated(PassiveSdDisplayDuration);
    }

    public void NotifyPassiveActivated(float duration)
    {
        if ((!_initialized && !Initialize()) || duration <= 0f)
            return;

        _passiveSdTimeRemaining = Mathf.Max(
            _passiveSdTimeRemaining,
            duration);
        RefreshSdImage();
    }

    public void BindItemTargetHandler(
        System.Func<CharacterRuntime, bool> itemTargetHandler)
    {
        _itemTargetHandler = itemTargetHandler;
    }

    public void BindManualTargetHandler(
        System.Func<CharacterRuntime, bool> manualTargetHandler)
    {
        _manualTargetHandler = manualTargetHandler;
    }

    public void SetManualTargetSelectionState(
        bool candidate,
        bool selected)
    {
        _manualTargetCandidate = candidate;
        _manualTargetSelected = candidate && selected;
        RefreshManualTargetHighlight();
    }

    private void RefreshManualTargetHighlight()
    {
        if (_panelImage == null)
            return;

        _panelImage.color = !_manualTargetCandidate
            ? _defaultPanelColor
            : _manualTargetSelected
                ? new Color(1f, 0.78f, 0.12f, 0.72f)
                : new Color(0.2f, 0.9f, 0.5f, 0.5f);
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

        if (_manualTargetHandler != null &&
            _manualTargetHandler(this))
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

        ShowAbilityTooltip(CharacterAbilityIconKind.Details);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _skillTooltip?.SetActive(false);
    }

    internal void ShowAbilityTooltip(CharacterAbilityIconKind kind)
    {
        if ((!_initialized && !Initialize()) || Data == null)
            return;

        _skillTooltipKind = kind;
        EnsureSkillTooltip();
        RefreshSkillTooltip();
        PositionSkillTooltip();
        _skillTooltip?.SetActive(true);
    }

    internal void HideAbilityTooltip(CharacterAbilityIconKind kind)
    {
        if (_skillTooltipKind == kind)
            _skillTooltip?.SetActive(false);
    }

    internal void HandleAbilityIconClick(CharacterAbilityIconKind kind)
    {
        if (kind != CharacterAbilityIconKind.Active)
            return;

        if (_manualTargetHandler != null &&
            _manualTargetHandler(this))
        {
            return;
        }

        if (_itemTargetHandler != null && _itemTargetHandler(this))
            return;

        TryActivateActiveSkill();
    }

    public bool CanActivateActiveSkill()
    {
        return (_initialized || Initialize()) &&
               _activeSkillResource != null &&
               _board != null &&
               Data != null &&
               Data.HasCustomSkillDefinitions &&
               !IsActiveSkillBlocked &&
               _activeSkillResource.CanSpend(Data.ActiveSkillCost);
    }

    public bool TryActivateActiveSkill()
    {
        if ((!_initialized && !Initialize()) ||
            _activeSkillResource == null || _board == null ||
            Data == null || !Data.HasCustomSkillDefinitions ||
            IsActiveSkillBlocked)
        {
            return false;
        }

        bool activated = TryActivateCustomSkill();
        if (!activated && _manualTargetRequestPending)
            _resumeActiveSkillAfterManualSelection = true;
        return activated || _manualTargetRequestPending;
    }

    private bool TryActivateCustomSkill()
    {
        return Data.SkillExecutionPolicy ==
               CharacterSkillExecutionPolicy.SequenceAll
            ? TryActivateSkillSequence()
            : TryActivateFirstSuccessfulSkill();
    }

    private bool TryActivateFirstSuccessfulSkill()
    {
        CharacterSkillDefinition selected = null;
        AbilityTargetSelection selectedTargets = default;
        IReadOnlyList<PreparedEffectExecution> selectedEffects =
            System.Array.Empty<PreparedEffectExecution>();
        EffectCostReservation selectedCostReservation = null;
        int selectedDamage = 0;
        foreach (CharacterSkillDefinition definition in Data.SkillDefinitions)
        {
            if (!EffectCostReservation.TryCreate(
                    _activeSkillResource,
                    this,
                    Data.GetSkillCost(definition),
                    out EffectCostReservation costReservation))
            {
                continue;
            }

            if (!TryPrepareSkillAction(
                    definition,
                    _lastAttackAttempted,
                    _lastAttackSucceeded,
                    _lastAttackTargets,
                    out AbilityTargetSelection targets,
                    out _,
                    out IReadOnlyList<PreparedEffectExecution> effects,
                    costReservation,
                    out int damage))
            {
                if (_manualTargetRequestPending)
                    break;
                continue;
            }

            selected = definition;
            selectedTargets = targets;
            selectedEffects = effects;
            selectedCostReservation = costReservation;
            selectedDamage = damage;
            break;
        }

        if (selected == null ||
            !selectedCostReservation.TryCommitBaseCost())
        {
            return false;
        }

        BattleEffectResult effectResult = selected.HasExplicitEffects
            ? ExecuteExplicitEffectsOnTargets(
                _board,
                selectedTargets,
                selected.Effects,
                CharacterActionKind.Skill,
                selectedEffects)
            : ExecuteLegacyAbilityOnTargets(
                _board,
                selectedTargets,
                selected.DamageType,
                selectedDamage,
                selected.AppliedStatusEffect,
                selected.StatusDuration,
                selected.StatusStacks,
                selected.StatusRemovalSelection,
                selected.StatusRemovalAmount);
        RecordDamageDealt(effectResult.DamageDealt);
        if (effectResult.Succeeded)
            PlayActionSfx(selected.AudioClip);

        FinishCustomSkillActivation(effectResult.Succeeded);
        return true;
    }

    private bool TryActivateSkillSequence()
    {
        int skillCost = Data.ActiveSkillCost;
        if (!EffectCostReservation.TryCreate(
                _activeSkillResource,
                this,
                skillCost,
                out _))
        {
            return false;
        }

        bool previousAttempted = _lastAttackAttempted;
        bool previousSucceeded = _lastAttackSucceeded;
        bool anyAttempted = false;
        bool anySucceeded = false;
        bool costPaid = false;
        int totalDamage = 0;
        AbilityTargetSelection previousTargets = _lastAttackTargets;
        List<PreparedSkillAction> simultaneousGroup = new();

        int definitionIndex = 0;
        while (definitionIndex < Data.SkillDefinitions.Count)
        {
            CharacterSkillDefinition definition =
                Data.SkillDefinitions[definitionIndex];
            EffectCostReservation groupCostReservation;
            if (costPaid)
            {
                groupCostReservation =
                    CreateEffectCostReservation();
            }
            else if (!EffectCostReservation.TryCreate(
                         _activeSkillResource,
                         this,
                         skillCost,
                         out groupCostReservation))
            {
                return false;
            }
            if (!TryPrepareSkillAction(
                    definition,
                    previousAttempted,
                    previousSucceeded,
                    previousTargets,
                    out AbilityTargetSelection targets,
                    out AbilityTargetSelection selectedTargets,
                    out IReadOnlyList<PreparedEffectExecution> effects,
                    groupCostReservation,
                    out int damage))
            {
                if (_manualTargetRequestPending)
                    return false;
                previousAttempted = false;
                previousSucceeded = false;
                previousTargets = default;
                definitionIndex++;
                continue;
            }

            simultaneousGroup.Clear();
            simultaneousGroup.Add(new PreparedSkillAction(
                definition,
                targets,
                selectedTargets,
                effects,
                damage));

            // Simultaneous steps must resolve from one board snapshot.
            // Otherwise an earlier hit can remove its center enemy and make a
            // later area step lose that tile or hit a newly exposed enemy.
            int nextDefinitionIndex = definitionIndex + 1;
            bool skippedInvalidSimultaneousStep = false;
            AbilityTargetSelection plannedPreviousTargets = selectedTargets;
            while (nextDefinitionIndex < Data.SkillDefinitions.Count)
            {
                CharacterSkillDefinition nextDefinition =
                    Data.SkillDefinitions[nextDefinitionIndex];
                if (!IsSimultaneousSkillStep(nextDefinition))
                    break;

                if (!TryPrepareSkillAction(
                        nextDefinition,
                        true,
                        false,
                        plannedPreviousTargets,
                        out AbilityTargetSelection nextTargets,
                        out AbilityTargetSelection nextSelectedTargets,
                        out IReadOnlyList<PreparedEffectExecution>
                            nextEffects,
                        groupCostReservation,
                        out int nextDamage))
                {
                    skippedInvalidSimultaneousStep = true;
                    nextDefinitionIndex++;
                    break;
                }

                simultaneousGroup.Add(new PreparedSkillAction(
                    nextDefinition,
                    nextTargets,
                    nextSelectedTargets,
                    nextEffects,
                    nextDamage));
                plannedPreviousTargets = nextSelectedTargets;
                nextDefinitionIndex++;
            }

            if (!costPaid)
            {
                if (!groupCostReservation.TryCommitBaseCost())
                    return false;
                costPaid = true;
            }

            foreach (PreparedSkillAction action in simultaneousGroup)
            {
                CharacterSkillDefinition actionDefinition =
                    action.Definition;
                BattleEffectResult effectResult =
                    actionDefinition.HasExplicitEffects
                        ? ExecuteExplicitEffectsOnTargets(
                            _board,
                            action.Targets,
                            actionDefinition.Effects,
                            CharacterActionKind.Skill,
                            action.Effects)
                        : ExecuteLegacyAbilityOnTargets(
                            _board,
                            action.Targets,
                            actionDefinition.DamageType,
                            action.Damage,
                            actionDefinition.AppliedStatusEffect,
                            actionDefinition.StatusDuration,
                            actionDefinition.StatusStacks,
                            actionDefinition.StatusRemovalSelection,
                            actionDefinition.StatusRemovalAmount);
                totalDamage += effectResult.DamageDealt;
                previousAttempted = effectResult.Attempted;
                previousSucceeded = effectResult.Succeeded;
                previousTargets = action.SelectedTargets;
                anyAttempted |= effectResult.Attempted;
                anySucceeded |= effectResult.Succeeded;
                if (effectResult.Succeeded)
                    PlayActionSfx(actionDefinition.AudioClip);
            }

            if (skippedInvalidSimultaneousStep)
            {
                previousAttempted = false;
                previousSucceeded = false;
                previousTargets = default;
            }

            definitionIndex = nextDefinitionIndex;
        }

        if (!anyAttempted)
            return false;

        RecordDamageDealt(totalDamage);
        FinishCustomSkillActivation(anySucceeded);
        return true;
    }

    private static bool IsSimultaneousSkillStep(
        CharacterSkillDefinition definition)
    {
        return definition != null &&
               definition.HasSection(CharacterSkillSectionType.Linkage) &&
               definition.Linkage ==
               CharacterActionLinkage.SimultaneousWithPreviousAttack;
    }

    private bool TryPrepareSkillAction(
        CharacterSkillDefinition definition,
        bool previousAttempted,
        bool previousSucceeded,
        AbilityTargetSelection inheritedTargets,
        out AbilityTargetSelection targets,
        out AbilityTargetSelection selectedTargets,
        out IReadOnlyList<PreparedEffectExecution> effects,
        EffectCostReservation costReservation,
        out int damage)
    {
        targets = default;
        selectedTargets = default;
        effects = System.Array.Empty<PreparedEffectExecution>();
        damage = 0;
        if (definition == null ||
            !definition.HasSection(CharacterSkillSectionType.Ability))
        {
            return false;
        }

        CharacterActionConditionData actionCondition =
            Data.GetActionConditionData(definition);
        if (!PassesLinkage(
                actionCondition.Linkage,
                previousAttempted,
                previousSucceeded))
        {
            return false;
        }

        CharacterAttackSubject subject = definition.HasSection(
            CharacterSkillSectionType.Subject)
            ? definition.Subject
            : CharacterAttackSubject.Random;
        damage = Data.CalculateSkillDamage(
            definition,
            GetEffectivePowerMultiplier());
        selectedTargets = SelectSkillActionTargets(
            definition,
            subject,
            actionCondition,
            inheritedTargets);
        if (_manualTargetRequestPending)
            return false;

        if (!CharacterConditionEvaluator.AllowsAction(
                this,
                actionCondition.MatchMode,
                actionCondition.NumericConditions,
                selectedTargets.Count > 0))
        {
            return false;
        }

        targets = ExpandCustomAbilityArea(
            _board,
            selectedTargets,
            definition.AreaOffsets);
        if (definition.HasExplicitEffects)
        {
            effects = PrepareExplicitEffects(
                _board,
                targets,
                definition.Effects,
                CharacterActionKind.Skill,
                costReservation);
            return CanExecutePreparedExplicitEffects(
                definition.Effects,
                effects,
                CharacterActionKind.Skill);
        }

        return targets.Count > 0 &&
               HasUsableAbilityValue(definition.DamageType, damage);
    }

    private AbilityTargetSelection SelectSkillActionTargets(
        CharacterSkillDefinition definition,
        CharacterAttackSubject subject,
        CharacterActionConditionData actionCondition,
        AbilityTargetSelection inheritedTargets)
    {
        if (subject != CharacterAttackSubject.None ||
            !UsesActionTargets(definition))
        {
            return SelectCustomAbilityTargets(
                _board,
                definition.TargetFaction,
                subject,
                definition.SubjectMetric,
                definition.SubjectCount,
                actionCondition.HasNumericConditions,
                actionCondition.MatchMode,
                actionCondition.NumericConditions,
                inheritedTargets);
        }

        AbilityTargetSelection validInheritedTargets =
            FilterInheritedAbilityTargets(
                _board,
                inheritedTargets,
                actionCondition.HasNumericConditions,
                actionCondition.MatchMode,
                actionCondition.NumericConditions);
        if (validInheritedTargets.Count > 0)
            return validInheritedTargets;

        CharacterAttackDefinition fallbackAttack =
            GetLinkedSkillFallbackAttack();
        if (fallbackAttack == null ||
            (fallbackAttack.Subject == CharacterAttackSubject.Manual &&
             Data.SkillDefinitions.Count > 1))
        {
            return default;
        }

        return SelectCustomAbilityTargets(
            _board,
            fallbackAttack.TargetFaction,
            fallbackAttack.Subject,
            fallbackAttack.SubjectMetric,
            fallbackAttack.SubjectCount,
            actionCondition.HasNumericConditions,
            actionCondition.MatchMode,
            actionCondition.NumericConditions,
            default);
    }

    private static bool UsesActionTargets(
        CharacterSkillDefinition definition)
    {
        if (definition == null || !definition.HasExplicitEffects)
            return true;

        foreach (CharacterEffectDefinition effect in definition.Effects)
        {
            if (effect?.RequiresActionTargets == true)
                return true;
        }

        return false;
    }

    private CharacterAttackDefinition GetLinkedSkillFallbackAttack()
    {
        foreach (CharacterAttackDefinition definition in
                 Data.AttackDefinitions)
        {
            if (definition == null ||
                !definition.HasSection(CharacterAttackSectionType.Subject) ||
                !definition.HasSection(CharacterAttackSectionType.Ability) ||
                definition.Subject == CharacterAttackSubject.None ||
                (definition.HasLinkageSection &&
                 definition.Linkage != CharacterActionLinkage.None))
            {
                continue;
            }

            return definition;
        }

        return null;
    }

    private AbilityTargetSelection FilterInheritedAbilityTargets(
        IBattleBoard board,
        AbilityTargetSelection inheritedTargets,
        bool hasNumericConditions,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions)
    {
        if (board == null || inheritedTargets.Count == 0)
            return default;

        IReadOnlyList<CharacterNumericCondition> conditions =
            hasNumericConditions
                ? numericConditions
                : System.Array.Empty<CharacterNumericCondition>();
        return inheritedTargets.Faction == CharacterTargetFaction.Ally
            ? AbilityTargetSelection.Allies(
                board.FilterAlliedCharacters(
                    this,
                    inheritedTargets.AllyTargets,
                    conditionMatchMode,
                    conditions))
            : AbilityTargetSelection.Enemies(
                board.FilterCharacterTargets(
                    this,
                    inheritedTargets.EnemyTargets,
                    conditionMatchMode,
                    conditions));
    }

    private EffectCostReservation CreateEffectCostReservation()
    {
        EffectCostReservation.TryCreate(
            _activeSkillResource,
            this,
            0,
            out EffectCostReservation reservation);
        return reservation;
    }

    private void FinishCustomSkillActivation(bool succeeded)
    {
        if (succeeded && Data.ActiveSkillRecoveryDuration > 0f)
            BeginAttackRecovery(Data.ActiveSkillRecoveryDuration);
        _skillSdTimeRemaining = Mathf.Max(
            _skillSdTimeRemaining,
            SkillSdDisplayDuration);
        RefreshUi();
    }

    private bool TryAttack(IBattleBoard board)
    {
        return Data.HasCustomAttackDefinitions && TryCustomAttack(board);
    }

    private bool TryCustomAttack(IBattleBoard board)
    {
        bool previousAttempted = false;
        bool previousSucceeded = false;
        bool anyAttempted = false;
        bool anySucceeded = false;
        int totalDamage = 0;
        AbilityTargetSelection previousTargets = default;
        AbilityTargetSelection lastAttemptedTargets = default;
        AbilityTargetSelection lastAttemptedSelectedTargets = default;
        for (int definitionIndex = 0;
             definitionIndex < Data.AttackDefinitions.Count;
             definitionIndex++)
        {
            CharacterAttackDefinition definition =
                Data.AttackDefinitions[definitionIndex];
            if (definition == null ||
                !definition.HasSection(CharacterAttackSectionType.Subject) ||
                !definition.HasSection(CharacterAttackSectionType.Ability))
            {
                previousAttempted = false;
                previousSucceeded = false;
                previousTargets = default;
                continue;
            }

            CharacterActionConditionData actionCondition =
                Data.GetActionConditionData(definition);
            if (!PassesLinkage(
                    actionCondition.Linkage,
                    previousAttempted,
                    previousSucceeded))
            {
                previousAttempted = false;
                previousSucceeded = false;
                previousTargets = default;
                continue;
            }

            AbilityTargetSelection selectedTargets =
                SelectAttackDefinitionTargets(
                    board,
                    definition,
                    definitionIndex,
                    actionCondition,
                    previousTargets);
            if (_manualTargetRequestPending)
                return false;

            AbilityTargetSelection targets = selectedTargets;
            BattleEffectResult effectResult;
            if (!CharacterConditionEvaluator.AllowsAction(
                    this,
                    actionCondition.MatchMode,
                    actionCondition.NumericConditions,
                    targets.Count > 0))
            {
                effectResult = default;
            }
            else
            {
                if (selectedTargets.Count > 0)
                {
                    CharacterPassiveAttackTargetRelation
                        attackTargetRelation = ResolveAttackTargetRelation(
                            _previousAttackAttemptTargets,
                            selectedTargets);
                    ExecuteAttackTargetSelectedPassives(
                        board,
                        selectedTargets,
                        attackTargetRelation);
                }

                if (definition.HasExplicitEffects)
                {
                    targets = ExpandCustomAbilityArea(
                        board,
                        targets,
                        definition.AreaOffsets);
                    IReadOnlyList<PreparedEffectExecution> effects =
                        PrepareExplicitEffects(
                            board,
                            targets,
                            definition.Effects,
                            CharacterActionKind.Attack,
                            CreateEffectCostReservation());
                    effectResult = HasUsableExplicitEffects(
                        definition.Effects,
                        CharacterActionKind.Attack)
                        ? ExecuteExplicitEffectsOnTargets(
                            board,
                            targets,
                            definition.Effects,
                            CharacterActionKind.Attack,
                            effects)
                        : default;
                }
                else
                {
                    int abilityDamage = Data.CalculateAttackDamage(
                        definition,
                        GetEffectivePowerMultiplier());
                    targets = ExpandCustomAbilityArea(
                        board,
                        targets,
                        definition.AreaOffsets);
                    bool succeeded = ExecuteCustomAbilityOnTargets(
                        board,
                        targets,
                        definition.DamageType,
                        abilityDamage,
                        definition.AppliedStatusEffect,
                        definition.StatusDuration,
                        definition.StatusStacks,
                        definition.StatusRemovalSelection,
                        definition.StatusRemovalAmount,
                        out int damageDealt);
                    bool attempted = targets.Count > 0 &&
                        HasUsableAbilityValue(
                            definition.DamageType,
                            abilityDamage);
                    effectResult = new BattleEffectResult(
                        attempted,
                        succeeded,
                        damageDealt);
                }
            }

            totalDamage += effectResult.DamageDealt;
            previousAttempted = effectResult.Attempted;
            previousSucceeded = effectResult.Succeeded;
            previousTargets = effectResult.Attempted ? targets : default;
            if (effectResult.Attempted)
            {
                lastAttemptedTargets = targets;
                if (selectedTargets.Count > 0)
                    lastAttemptedSelectedTargets = selectedTargets;
            }
            anyAttempted |= effectResult.Attempted;
            anySucceeded |= effectResult.Succeeded;
            if (effectResult.Succeeded)
                PlayActionSfx(definition.AudioClip);
        }

        _lastAttackAttempted = anyAttempted;
        _lastAttackSucceeded = anySucceeded;
        _lastAttackTargets = anyAttempted
            ? lastAttemptedTargets
            : default;
        RecordDamageDealt(totalDamage);
        if (anyAttempted)
        {
            CharacterPassiveAttackTargetRelation attackTargetRelation =
                ResolveAttackTargetRelation(
                    _previousAttackAttemptTargets,
                    lastAttemptedSelectedTargets);
            ShowAttackSd();
            ExecuteCustomPassives(
                board,
                anyAttempted,
                anySucceeded,
                attackTargetRelation);
            if (lastAttemptedSelectedTargets.Count > 0)
            {
                _previousAttackAttemptTargets =
                    ReuseAbilityTargets(lastAttemptedSelectedTargets);
            }
        }

        return anyAttempted || Data.AttackDefinitions.Count > 0;
    }

    private AbilityTargetSelection SelectAttackDefinitionTargets(
        IBattleBoard board,
        CharacterAttackDefinition definition,
        int definitionIndex,
        CharacterActionConditionData actionCondition,
        AbilityTargetSelection inheritedTargets)
    {
        bool retainsTarget = definition.TargetRetentionMode ==
            CharacterAttackTargetRetentionMode.LockUntilInvalid &&
            CharacterAttackDefinition.SupportsTargetRetention(
                definition.Subject,
                definition.SubjectCount);
        if (!retainsTarget)
        {
            _retainedAttackTargets.Remove(definitionIndex);
            return SelectCustomAbilityTargets(
                board,
                definition.TargetFaction,
                definition.Subject,
                definition.SubjectMetric,
                definition.SubjectCount,
                actionCondition.HasNumericConditions,
                actionCondition.MatchMode,
                actionCondition.NumericConditions,
                inheritedTargets);
        }

        IReadOnlyList<CharacterNumericCondition> conditions =
            actionCondition.HasNumericConditions
                ? actionCondition.NumericConditions
                : System.Array.Empty<CharacterNumericCondition>();
        if (_retainedAttackTargets.TryGetValue(
                definitionIndex,
                out AbilityTargetSelection retainedTargets))
        {
            AbilityTargetSelection validTargets =
                retainedTargets.Faction == CharacterTargetFaction.Ally
                    ? AbilityTargetSelection.Allies(
                        board.FilterAlliedCharacters(
                            this,
                            retainedTargets.AllyTargets,
                            actionCondition.MatchMode,
                            conditions))
                    : AbilityTargetSelection.Enemies(
                        board.FilterCharacterTargets(
                            this,
                            retainedTargets.EnemyTargets,
                            actionCondition.MatchMode,
                            conditions));
            if (validTargets.Count == 1)
                return validTargets;

            _retainedAttackTargets.Remove(definitionIndex);
        }

        AbilityTargetSelection selectedTargets =
            SelectCustomAbilityTargets(
                board,
                definition.TargetFaction,
                definition.Subject,
                definition.SubjectMetric,
                definition.SubjectCount,
                actionCondition.HasNumericConditions,
                actionCondition.MatchMode,
                actionCondition.NumericConditions,
                inheritedTargets);
        if (selectedTargets.Count == 1)
        {
            _retainedAttackTargets[definitionIndex] =
                ReuseAbilityTargets(selectedTargets);
        }

        return selectedTargets;
    }

    private void ExecuteAttackTargetSelectedPassives(
        IBattleBoard board,
        AbilityTargetSelection selectedTargets,
        CharacterPassiveAttackTargetRelation attackTargetRelation)
    {
        if (!Data.HasCustomPassiveDefinitions ||
            selectedTargets.Count == 0)
        {
            return;
        }

        bool anyPassiveSucceeded = false;
        int totalDamage = 0;
        foreach (CharacterPassiveDefinition definition in
                 Data.PassiveDefinitions)
        {
            if (definition == null ||
                definition.IsEmptyPlaceholder ||
                definition.Trigger !=
                CharacterPassiveTrigger.OnAttackTargetSelected ||
                !definition.HasSection(
                    CharacterPassiveSectionType.Ability) ||
                !MatchesAttackTargetRelation(
                    definition,
                    attackTargetRelation))
            {
                continue;
            }

            CharacterActionConditionData actionCondition =
                Data.GetActionConditionData(definition);
            bool succeeded = TryExecutePassiveAbility(
                board,
                definition,
                actionCondition,
                selectedTargets,
                out int damageDealt);
            totalDamage += damageDealt;
            anyPassiveSucceeded |= succeeded;
        }

        RecordDamageDealt(totalDamage);
        if (anyPassiveSucceeded)
            NotifyPassiveActivated();
    }

    private void ExecuteCustomPassives(
        IBattleBoard board,
        bool attackAttempted,
        bool attackSucceeded,
        CharacterPassiveAttackTargetRelation attackTargetRelation)
    {
        if (!Data.HasCustomPassiveDefinitions)
            return;

        bool anyPassiveSucceeded = false;
        int totalDamage = 0;
        foreach (CharacterPassiveDefinition definition in
                 Data.PassiveDefinitions)
        {
            if (definition == null ||
                definition.IsEmptyPlaceholder ||
                definition.Trigger != CharacterPassiveTrigger.OnAttack ||
                !definition.HasSection(
                    CharacterPassiveSectionType.Ability))
            {
                continue;
            }

            CharacterActionConditionData actionCondition =
                Data.GetActionConditionData(definition);
            if (!PassesLinkage(
                    actionCondition.Linkage,
                    attackAttempted,
                    attackSucceeded))
            {
                continue;
            }
            if (!MatchesAttackTargetRelation(
                    definition,
                    attackTargetRelation))
            {
                continue;
            }

            bool succeeded = TryExecutePassiveAbility(
                board,
                definition,
                actionCondition,
                _lastAttackTargets,
                out int damageDealt);
            totalDamage += damageDealt;
            anyPassiveSucceeded |= succeeded;
        }

        RecordDamageDealt(totalDamage);
        if (anyPassiveSucceeded)
            NotifyPassiveActivated();
    }

    private static CharacterPassiveAttackTargetRelation
        ResolveAttackTargetRelation(
            AbilityTargetSelection previousTargets,
            AbilityTargetSelection currentTargets)
    {
        if (previousTargets.Count == 0 || currentTargets.Count == 0)
            return CharacterPassiveAttackTargetRelation.Any;
        if (previousTargets.Faction != currentTargets.Faction)
        {
            return CharacterPassiveAttackTargetRelation
                .DifferentFromPreviousAttack;
        }

        bool sameTarget = currentTargets.Faction ==
                          CharacterTargetFaction.Ally
            ? ReferenceEquals(
                previousTargets.AllyTargets[0],
                currentTargets.AllyTargets[0])
            : ReferenceEquals(
                previousTargets.EnemyTargets[0],
                currentTargets.EnemyTargets[0]);
        return sameTarget
            ? CharacterPassiveAttackTargetRelation.SameAsPreviousAttack
            : CharacterPassiveAttackTargetRelation
                .DifferentFromPreviousAttack;
    }

    private static bool MatchesAttackTargetRelation(
        CharacterPassiveDefinition definition,
        CharacterPassiveAttackTargetRelation actualRelation)
    {
        if (definition == null ||
            !definition.HasAttackTargetRelationCondition)
        {
            return true;
        }

        return definition.AttackTargetRelation == actualRelation;
    }

    private void TickCooldownPassives(float deltaTime, IBattleBoard board)
    {
        if (deltaTime <= 0f || board == null || Data == null ||
            !Data.HasCustomPassiveDefinitions)
        {
            return;
        }

        bool anyPassiveSucceeded = false;
        int totalDamage = 0;
        foreach (CharacterPassiveDefinition definition in
                 Data.PassiveDefinitions)
        {
            if (definition == null ||
                definition.IsEmptyPlaceholder ||
                definition.Trigger != CharacterPassiveTrigger.OnCooldown ||
                !definition.HasSection(CharacterPassiveSectionType.Ability))
            {
                continue;
            }

            if (!_passiveCooldowns.TryGetValue(
                    definition,
                    out float remainingCooldown))
            {
                remainingCooldown = definition.Cooldown;
            }

            remainingCooldown -= deltaTime;
            if (remainingCooldown > 0f)
            {
                _passiveCooldowns[definition] = remainingCooldown;
                continue;
            }

            // Reset before execution so an unsuccessful attempt cannot retry
            // every frame and status-triggered callbacks remain re-entrancy safe.
            _passiveCooldowns[definition] = definition.Cooldown;
            CharacterActionConditionData actionCondition =
                Data.GetActionConditionData(definition);
            bool succeeded = TryExecutePassiveAbility(
                board,
                definition,
                actionCondition,
                default,
                out int damageDealt);
            totalDamage += damageDealt;
            anyPassiveSucceeded |= succeeded;
        }

        RecordDamageDealt(totalDamage);
        if (anyPassiveSucceeded)
            NotifyPassiveActivated();
    }

    private void ResetPassiveCooldowns()
    {
        _passiveCooldowns.Clear();
        if (Data == null || !Data.HasCustomPassiveDefinitions)
            return;

        foreach (CharacterPassiveDefinition definition in
                 Data.PassiveDefinitions)
        {
            if (definition != null &&
                !definition.IsEmptyPlaceholder &&
                definition.HasSection(
                    CharacterPassiveSectionType.Ability) &&
                definition.Trigger == CharacterPassiveTrigger.OnCooldown)
            {
                _passiveCooldowns[definition] = definition.Cooldown;
            }
        }
    }

    private void HandleStatusApplied(BattleStatusAppliedEvent eventData)
    {
        if (!eventData.IsValid || !_initialized || Data == null ||
            _board == null ||
            !Data.HasCustomPassiveDefinitions)
        {
            return;
        }

        bool anyPassiveSucceeded = false;
        int totalDamage = 0;
        foreach (CharacterPassiveDefinition definition in
                 Data.PassiveDefinitions)
        {
            if (definition == null ||
                definition.IsEmptyPlaceholder ||
                definition.Trigger !=
                CharacterPassiveTrigger.OnStatusAcquired ||
                !MatchesStatusTarget(
                    definition.StatusTarget,
                    eventData.Target.Faction) ||
                !MatchesTriggerStatus(
                    definition.TriggerStatusScope,
                    definition.TriggerStatusSelection,
                    eventData.StatusEffect) ||
                !definition.HasSection(CharacterPassiveSectionType.Ability))
            {
                continue;
            }

            CharacterActionConditionData actionCondition =
                Data.GetActionConditionData(definition);
            bool succeeded = TryExecutePassiveAbility(
                _board,
                definition,
                actionCondition,
                CreateStatusEventTargets(eventData.Target),
                out int damageDealt);
            totalDamage += damageDealt;
            anyPassiveSucceeded |= succeeded;
        }

        RecordDamageDealt(totalDamage);
        if (anyPassiveSucceeded)
            NotifyPassiveActivated();
    }

    private void HandleEnemyDefeated(BattleEnemyDefeatedEvent eventData)
    {
        if (!eventData.IsValid || !eventData.HasCharacterKiller ||
            !_initialized || Data == null || _board == null ||
            !Data.HasCustomPassiveDefinitions)
        {
            return;
        }

        bool anyPassiveSucceeded = false;
        int totalDamage = 0;
        foreach (CharacterPassiveDefinition definition in
                 Data.PassiveDefinitions)
        {
            if (definition == null ||
                definition.IsEmptyPlaceholder ||
                definition.Trigger != CharacterPassiveTrigger.OnKill ||
                !MatchesKillSource(definition, eventData.Killer) ||
                !definition.HasSection(CharacterPassiveSectionType.Ability))
            {
                continue;
            }

            CharacterActionConditionData actionCondition =
                Data.GetActionConditionData(definition);
            bool succeeded = TryExecutePassiveAbility(
                _board,
                definition,
                actionCondition,
                default,
                out int damageDealt);
            totalDamage += damageDealt;
            anyPassiveSucceeded |= succeeded;
        }

        RecordDamageDealt(totalDamage);
        if (anyPassiveSucceeded)
            NotifyPassiveActivated();
    }

    private bool MatchesKillSource(
        CharacterPassiveDefinition definition,
        IBattleCharacter killer)
    {
        if (definition == null || killer == null)
            return false;

        return definition.KillSource switch
        {
            CharacterPassiveKillSource.Self =>
                ReferenceEquals(killer, this),
            CharacterPassiveKillSource.Other =>
                !ReferenceEquals(killer, this),
            CharacterPassiveKillSource.SpecificCharacter =>
                MatchesCharacterDefinition(
                    killer,
                    definition.SpecifiedKillerCharacter),
            CharacterPassiveKillSource.All => true,
            _ => false
        };
    }

    private static bool MatchesCharacterDefinition(
        IBattleCharacter character,
        CharacterSO expectedDefinition)
    {
        if (character is not CharacterRuntime runtime ||
            runtime.Definition == null ||
            expectedDefinition == null)
        {
            return false;
        }

        return ReferenceEquals(runtime.Definition, expectedDefinition) ||
               string.Equals(
                   runtime.Definition.CharacterId,
                   expectedDefinition.CharacterId,
                   System.StringComparison.Ordinal);
    }

    private static bool MatchesStatusTarget(
        CharacterPassiveStatusTarget configuredTarget,
        CharacterTargetFaction acquiredFaction)
    {
        return configuredTarget == CharacterPassiveStatusTarget.All ||
               (configuredTarget == CharacterPassiveStatusTarget.Ally &&
                acquiredFaction == CharacterTargetFaction.Ally) ||
               (configuredTarget == CharacterPassiveStatusTarget.Enemy &&
                acquiredFaction == CharacterTargetFaction.Enemy);
    }

    private static bool MatchesTriggerStatus(
        CharacterStatusSelectionScope scope,
        CharacterStatusSelection configuredStatuses,
        StatusEffectSO acquiredStatus)
    {
        if (acquiredStatus == null)
            return false;
        if (scope == CharacterStatusSelectionScope.AllBuffs)
        {
            return acquiredStatus.Alignment ==
                   StatusEffectAlignment.Buff;
        }
        if (scope == CharacterStatusSelectionScope.AllDebuffs)
        {
            return acquiredStatus.Alignment ==
                   StatusEffectAlignment.Debuff;
        }
        if (configuredStatuses.Count == 0)
            return true;

        return configuredStatuses.Contains(acquiredStatus);
    }

    private static AbilityTargetSelection CreateStatusEventTargets(
        BattleStatusTarget target)
    {
        if (!target.IsValid)
            return default;

        return target.Faction == CharacterTargetFaction.Ally
            ? AbilityTargetSelection.Allies(new[] { target.Ally })
            : AbilityTargetSelection.Enemies(new[] { target.Enemy });
    }

    private bool TryExecutePassiveAbility(
        IBattleBoard board,
        CharacterPassiveDefinition definition,
        CharacterActionConditionData actionCondition,
        AbilityTargetSelection inheritedTargets,
        out int damageDealt)
    {
        damageDealt = 0;
        CharacterStatusStackCostDefinition cost =
            definition?.HasSelfStatusCost == true
                ? definition.SelfStatusCost
                : null;
        if (cost != null && GetStatusStackCount(cost.StatusEffect) <
            cost.RequiredStacks)
        {
            return false;
        }

        bool succeeded = ExecutePassiveAbility(
            board,
            definition,
            actionCondition,
            inheritedTargets,
            out damageDealt);
        bool usesManualTarget =
            definition?.HasSection(CharacterPassiveSectionType.Subject) ==
                true &&
            definition.Subject == CharacterAttackSubject.Manual;
        if (!succeeded && usesManualTarget &&
            _manualTargetRequestPending &&
            !_replayingManualPassiveAction)
        {
            _pendingManualPassiveActions.Add(
                new PendingManualPassiveAction(
                    board,
                    definition,
                    actionCondition,
                    inheritedTargets));
        }
        if (!succeeded || cost == null)
            return succeeded;

        if (TryConsumeStatusStacks(
                cost.StatusEffect,
                cost.ConsumedStacks))
        {
            return true;
        }

        Debug.LogError(
            $"Failed to consume passive status cost for " +
            $"'{Definition?.name ?? name}'.",
            this);
        return true;
    }

    private bool ProcessPendingManualActions()
    {
        if (_manualTargetRequestPending)
            return true;

        if (_resumeActiveSkillAfterManualSelection &&
            (_hasCompletedManualTargetSelection ||
             _manualTargetSelectionCancelled))
        {
            _resumeActiveSkillAfterManualSelection = false;
            TryActivateCustomSkill();
            return true;
        }

        if (_pendingManualPassiveActions.Count == 0)
            return false;

        PendingManualPassiveAction pending =
            _pendingManualPassiveActions[0];
        _replayingManualPassiveAction = true;
        bool succeeded;
        int damageDealt;
        try
        {
            succeeded = TryExecutePassiveAbility(
                pending.Board,
                pending.Definition,
                pending.Condition,
                pending.InheritedTargets,
                out damageDealt);
        }
        finally
        {
            _replayingManualPassiveAction = false;
        }

        if (_manualTargetRequestPending)
            return true;

        _pendingManualPassiveActions.RemoveAt(0);
        RecordDamageDealt(damageDealt);
        if (succeeded)
            NotifyPassiveActivated();
        return true;
    }

    private bool ExecutePassiveAbility(
        IBattleBoard board,
        CharacterPassiveDefinition definition,
        CharacterActionConditionData actionCondition,
        AbilityTargetSelection inheritedTargets,
        out int damageDealt)
    {
        damageDealt = 0;
        if (board == null || definition == null ||
            !definition.HasSection(CharacterPassiveSectionType.Ability))
        {
            return false;
        }

        CharacterAttackSubject subject = definition.HasSection(
            CharacterPassiveSectionType.Subject)
            ? definition.Subject
            : CharacterAttackSubject.Random;
        BattleEffectResult effectResult;
        if (definition.HasExplicitEffects)
        {
            AbilityTargetSelection targets = SelectCustomAbilityTargets(
                board,
                definition.TargetFaction,
                subject,
                definition.SubjectMetric,
                definition.SubjectCount,
                actionCondition.HasNumericConditions,
                actionCondition.MatchMode,
                actionCondition.NumericConditions,
                inheritedTargets);
            if (_manualTargetRequestPending)
                return false;

            if (!CharacterConditionEvaluator.AllowsAction(
                    this,
                    actionCondition.MatchMode,
                    actionCondition.NumericConditions,
                    targets.Count > 0))
            {
                return false;
            }

            targets = ExpandCustomAbilityArea(
                board,
                targets,
                definition.AreaOffsets);
            IReadOnlyList<PreparedEffectExecution> effects =
                PrepareExplicitEffects(
                    board,
                    targets,
                    definition.Effects,
                    CharacterActionKind.Passive,
                    CreateEffectCostReservation());
            effectResult = HasUsableExplicitEffects(
                definition.Effects,
                CharacterActionKind.Passive)
                ? ExecuteExplicitEffectsOnTargets(
                    board,
                    targets,
                    definition.Effects,
                    CharacterActionKind.Passive,
                    effects)
                : default;
        }
        else
        {
            int legacyDamage =
                Data.CalculatePassiveDamage(
                    definition,
                    GetEffectivePowerMultiplier());
            bool succeeded = ExecuteCustomAbility(
                board,
                definition.TargetFaction,
                subject,
                definition.SubjectMetric,
                definition.SubjectCount,
                actionCondition.HasNumericConditions,
                actionCondition.MatchMode,
                actionCondition.NumericConditions,
                definition.DamageType,
                legacyDamage,
                definition.AppliedStatusEffect,
                definition.StatusDuration,
                definition.StatusStacks,
                definition.StatusRemovalSelection,
                definition.StatusRemovalAmount,
                definition.AreaOffsets,
                inheritedTargets,
                out AbilityTargetSelection targets,
                out int legacyDamageDealt);
            bool attempted = targets.Count > 0 && HasUsableAbilityValue(
                definition.DamageType,
                legacyDamage);
            effectResult = new BattleEffectResult(
                attempted,
                succeeded,
                legacyDamageDealt);
        }

        damageDealt = effectResult.DamageDealt;
        if (effectResult.Succeeded)
            PlayActionSfx(definition.AudioClip);
        return effectResult.Succeeded;
    }

    private bool ExecuteCustomAbility(
        IBattleBoard board,
        CharacterTargetFaction targetFaction,
        CharacterAttackSubject subject,
        CharacterAttackSubjectMetric metric,
        int targetCount,
        bool hasNumericConditions,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions,
        CharacterAttackDamageType damageType,
        int damage,
        StatusEffectSO appliedStatusEffect,
        float statusDuration,
        float statusStacks,
        CharacterStatusRemovalSelection statusRemovalSelection,
        CharacterStatusRemovalAmount statusRemovalAmount,
        IReadOnlyList<CharacterTargetAreaOffset> areaOffsets,
        AbilityTargetSelection inheritedTargets,
        out AbilityTargetSelection targets,
        out int damageDealt)
    {
        targets = SelectCustomAbilityTargets(
            board,
            targetFaction,
            subject,
            metric,
            targetCount,
            hasNumericConditions,
            conditionMatchMode,
            numericConditions,
            inheritedTargets);
        if (_manualTargetRequestPending)
        {
            damageDealt = 0;
            return false;
        }

        if (!CharacterConditionEvaluator.AllowsAction(
                this,
                conditionMatchMode,
                numericConditions,
                targets.Count > 0))
        {
            damageDealt = 0;
            return false;
        }

        targets = ExpandCustomAbilityArea(board, targets, areaOffsets);

        return ExecuteCustomAbilityOnTargets(
            board,
            targets,
            damageType,
            damage,
            appliedStatusEffect,
            statusDuration,
            statusStacks,
            statusRemovalSelection,
            statusRemovalAmount,
            out damageDealt);
    }

    private static AbilityTargetSelection ExpandCustomAbilityArea(
        IBattleBoard board,
        AbilityTargetSelection targets,
        IReadOnlyList<CharacterTargetAreaOffset> areaOffsets)
    {
        if (board == null ||
            targets.Faction != CharacterTargetFaction.Enemy ||
            targets.Count == 0 || areaOffsets == null ||
            areaOffsets.Count == 0)
        {
            return targets;
        }

        return AbilityTargetSelection.Enemies(
            board.ExpandCharacterAreaTargets(
                targets.EnemyTargets,
                areaOffsets),
            true);
    }

    private AbilityTargetSelection SelectCustomAbilityTargets(
        IBattleBoard board,
        CharacterTargetFaction targetFaction,
        CharacterAttackSubject subject,
        CharacterAttackSubjectMetric metric,
        int targetCount,
        bool hasNumericConditions,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> numericConditions,
        AbilityTargetSelection inheritedTargets)
    {
        IReadOnlyList<CharacterNumericCondition> conditions =
            hasNumericConditions
                ? numericConditions
                : System.Array.Empty<CharacterNumericCondition>();
        if (subject == CharacterAttackSubject.Manual)
        {
            return ResolveManualAbilityTargets(
                board,
                targetFaction,
                targetCount,
                conditionMatchMode,
                conditions);
        }
        if (subject == CharacterAttackSubject.None)
        {
            AbilityTargetSelection reusedTargets =
                ReuseAbilityTargets(inheritedTargets);
            if (conditions.Count == 0 || board == null)
                return reusedTargets;

            return reusedTargets.Faction == CharacterTargetFaction.Ally
                ? AbilityTargetSelection.Allies(
                    board.FilterAlliedCharacters(
                        this,
                        reusedTargets.AllyTargets,
                        conditionMatchMode,
                        conditions))
                : AbilityTargetSelection.Enemies(
                    board.FilterCharacterTargets(
                        this,
                        reusedTargets.EnemyTargets,
                        conditionMatchMode,
                        conditions));
        }

        if (targetFaction == CharacterTargetFaction.Ally)
        {
            return AbilityTargetSelection.Allies(
                board.SelectAlliedCharacters(
                    this,
                    subject,
                    metric,
                    targetCount,
                    conditionMatchMode,
                    conditions));
        }

        return AbilityTargetSelection.Enemies(
            board.SelectCharacterTargets(
                this,
                subject,
                metric,
                targetCount,
                conditionMatchMode,
                conditions));
    }

    private AbilityTargetSelection ResolveManualAbilityTargets(
        IBattleBoard board,
        CharacterTargetFaction targetFaction,
        int targetCount,
        CharacterConditionMatchMode conditionMatchMode,
        IReadOnlyList<CharacterNumericCondition> conditions)
    {
        if (_manualTargetSelectionCancelled)
        {
            _manualTargetSelectionCancelled = false;
            return default;
        }

        if (_hasCompletedManualTargetSelection)
        {
            if (_completedManualTargetSelection.Faction != targetFaction)
                return default;

            AbilityTargetSelection completed =
                _completedManualTargetSelection;
            _completedManualTargetSelection = default;
            _hasCompletedManualTargetSelection = false;
            return completed;
        }

        if (_manualTargetRequestPending || board == null ||
            board is not IBattleManualTargetSelectionService service)
        {
            return default;
        }

        IReadOnlyList<IBattleCharacter> allyCandidates =
            System.Array.Empty<IBattleCharacter>();
        IReadOnlyList<EnemyRuntime> enemyCandidates =
            System.Array.Empty<EnemyRuntime>();
        if (targetFaction == CharacterTargetFaction.Ally)
        {
            allyCandidates = board.SelectAlliedCharacters(
                this,
                CharacterAttackSubject.All,
                CharacterAttackSubjectMetric.Health,
                int.MaxValue,
                conditionMatchMode,
                conditions);
        }
        else
        {
            enemyCandidates = board.SelectCharacterTargets(
                this,
                CharacterAttackSubject.All,
                CharacterAttackSubjectMetric.Health,
                int.MaxValue,
                conditionMatchMode,
                conditions);
        }

        int candidateCount =
            targetFaction == CharacterTargetFaction.Ally
                ? allyCandidates?.Count ?? 0
                : enemyCandidates?.Count ?? 0;
        if (candidateCount == 0)
            return default;

        BattleManualTargetSelectionRequest request = new(
            this,
            targetFaction,
            Mathf.Max(1, targetCount),
            enemyCandidates,
            allyCandidates,
            false,
            HandleManualTargetSelectionCompleted);
        if (!service.TryBeginManualTargetSelection(request))
            return default;

        _manualTargetRequestPending = true;
        return default;
    }

    private void HandleManualTargetSelectionCompleted(
        BattleManualTargetSelectionResult result)
    {
        _manualTargetRequestPending = false;
        if (result.Cancelled || !result.HasTargets)
        {
            _manualTargetSelectionCancelled = true;
            return;
        }

        _completedManualTargetSelection =
            result.Faction == CharacterTargetFaction.Ally
                ? AbilityTargetSelection.Allies(result.AllyTargets)
                : AbilityTargetSelection.Enemies(result.EnemyTargets);
        _hasCompletedManualTargetSelection = true;
    }

    private IReadOnlyList<PreparedEffectExecution>
        PrepareExplicitEffects(
            IBattleBoard board,
            AbilityTargetSelection actionTargets,
            IReadOnlyList<CharacterEffectDefinition> effects,
            CharacterActionKind actionKind,
            EffectCostReservation costReservation)
    {
        if (effects == null || effects.Count == 0)
            return System.Array.Empty<PreparedEffectExecution>();

        PreparedEffectExecution[] preparedEffects =
            new PreparedEffectExecution[effects.Count];
        if (board == null)
            return preparedEffects;

        for (int index = 0; index < effects.Count; index++)
        {
            CharacterEffectDefinition effect = effects[index];
            if (!IsUsableExplicitEffect(effect))
            {
                continue;
            }

            AbilityTargetSelection effectTargets = default;
            if (effect.Type != CharacterEffectType.GainResource &&
                effect.Type != CharacterEffectType.SpendResource &&
                effect.Type != CharacterEffectType.SpendHealth &&
                effect.TargetMode ==
                CharacterEffectTargetMode.FreshSelection)
            {
                CharacterEffectTargetSelector selector =
                    effect.TargetSelector;
                if (selector != null &&
                    selector.Subject != CharacterAttackSubject.None)
                {
                    AbilityTargetSelection selected =
                        SelectCustomAbilityTargets(
                            board,
                            selector.TargetFaction,
                            selector.Subject,
                            selector.SubjectMetric,
                            selector.SubjectCount,
                            selector.HasNumericConditions,
                            selector.ConditionMatchMode,
                            selector.NumericConditions,
                            default);
                    effectTargets = ExpandCustomAbilityArea(
                        board,
                        selected,
                        selector.AreaOffsets);
                }
            }

            if (!MeetsEffectTargetPreconditions(
                    actionTargets,
                    effect,
                    effectTargets))
            {
                continue;
            }

            int resourceSpendAmount = 0;
            int healthSpendAmount = 0;
            if (effect.Type == CharacterEffectType.SpendResource ||
                effect.Type == CharacterEffectType.SpendHealth)
            {
                EffectContext reservationContext = new(
                    this,
                    board,
                    _activeSkillResource,
                    actionKind,
                    actionTargets.Faction,
                    actionTargets.EnemyTargets,
                    actionTargets.AllyTargets,
                    GetScalingAttackPower(
                        effect.StatusContributionMultipliers));
                int spendAmount = Data.CalculateEffectAmount(
                    effect,
                    reservationContext);
                bool reserved = effect.Type ==
                                CharacterEffectType.SpendResource
                    ? costReservation?.TryReserveEffectSpend(
                        spendAmount) == true
                    : costReservation?.TryReserveHealthSpend(
                        spendAmount) == true;
                if (spendAmount <= 0 || !reserved)
                {
                    continue;
                }

                if (effect.Type == CharacterEffectType.SpendResource)
                    resourceSpendAmount = spendAmount;
                else
                    healthSpendAmount = spendAmount;
            }

            preparedEffects[index] = new PreparedEffectExecution(
                effectTargets,
                resourceSpendAmount,
                healthSpendAmount);
        }

        return preparedEffects;
    }

    private static AbilityTargetSelection ReuseAbilityTargets(
        AbilityTargetSelection targets)
    {
        if (targets.Count == 0)
            return default;

        return targets.Faction == CharacterTargetFaction.Ally
            ? AbilityTargetSelection.Allies(targets.AllyTargets)
            : AbilityTargetSelection.Enemies(targets.EnemyTargets);
    }

    private bool ExecuteCustomAbilityOnTargets(
        IBattleBoard board,
        AbilityTargetSelection targets,
        CharacterAttackDamageType damageType,
        int damage,
        StatusEffectSO appliedStatusEffect,
        float statusDuration,
        float statusStacks,
        CharacterStatusRemovalSelection statusRemovalSelection,
        CharacterStatusRemovalAmount statusRemovalAmount,
        out int damageDealt)
    {
        damageDealt = 0;
        if (targets.Count == 0)
            return false;

        if (damageType == CharacterAttackDamageType.StatusRemoval)
        {
            return targets.Faction == CharacterTargetFaction.Ally
                ? board.TryRemoveAlliedCharacterStatus(
                    this,
                    targets.AllyTargets,
                    statusRemovalSelection,
                    statusRemovalAmount)
                : board.TryRemoveCharacterStatus(
                    this,
                    targets.EnemyTargets,
                    statusRemovalSelection,
                    statusRemovalAmount,
                    !targets.RangeAlreadyShown);
        }

        if (damageType == CharacterAttackDamageType.StatusEffect)
        {
            return targets.Faction == CharacterTargetFaction.Ally
                ? board.TryApplyAlliedCharacterStatus(
                    this,
                    targets.AllyTargets,
                    appliedStatusEffect,
                    statusDuration,
                    statusStacks)
                : board.TryApplyCharacterStatus(
                    this,
                    targets.EnemyTargets,
                    appliedStatusEffect,
                    statusDuration,
                    statusStacks,
                    appliedStatusEffect?.TickInterval ?? 0f,
                    !targets.RangeAlreadyShown);
        }

        // CharacterRuntime currently has no health or generic ally-effect
        // receiver. Keep the selected allies intact in the target pipeline so
        // upcoming buff/heal ability types can consume them without changing
        // target selection again.
        if (targets.Faction == CharacterTargetFaction.Ally)
            return false;

        IReadOnlyList<EnemyRuntime> enemyTargets = targets.EnemyTargets;

        if (damage <= 0)
            return false;

        damageDealt = board.TryDamageCharacterTargets(
            this,
            enemyTargets,
            damage,
            damageType,
            !targets.RangeAlreadyShown);
        return damageDealt > 0;
    }

    private BattleEffectResult ExecuteLegacyAbilityOnTargets(
        IBattleBoard board,
        AbilityTargetSelection targets,
        CharacterAttackDamageType damageType,
        int damage,
        StatusEffectSO appliedStatusEffect,
        float statusDuration,
        float statusStacks,
        CharacterStatusRemovalSelection statusRemovalSelection,
        CharacterStatusRemovalAmount statusRemovalAmount)
    {
        List<EnemyRuntime> livingTargets = new();
        if (targets.Faction == CharacterTargetFaction.Enemy)
        {
            HashSet<EnemyRuntime> uniqueTargets = new();
            foreach (EnemyRuntime target in targets.EnemyTargets)
            {
                if (target != null &&
                    target.Health > 0 &&
                    uniqueTargets.Add(target))
                {
                    livingTargets.Add(target);
                }
            }
        }

        bool succeeded = ExecuteCustomAbilityOnTargets(
            board,
            targets,
            damageType,
            damage,
            appliedStatusEffect,
            statusDuration,
            statusStacks,
            statusRemovalSelection,
            statusRemovalAmount,
            out int damageDealt);
        if (succeeded &&
            board is IBattlePresentationEventPublisher publisher)
        {
            foreach (EnemyRuntime target in livingTargets)
            {
                if (target.Health > 0 || target.Definition == null)
                    continue;

                publisher.PublishUnitLifecycle(
                    new BattleUnitLifecycleEvent(
                        BattleUnitLifecycleType.Defeated,
                        BattleStatusTarget.FromEnemy(target),
                        target.Definition));
            }
        }
        return new BattleEffectResult(
            targets.Count > 0 &&
            HasUsableAbilityValue(damageType, damage),
            succeeded,
            damageDealt);
    }

    private BattleEffectResult ExecuteExplicitEffectsOnTargets(
        IBattleBoard board,
        AbilityTargetSelection targets,
        IReadOnlyList<CharacterEffectDefinition> effects,
        CharacterActionKind actionKind,
        IReadOnlyList<PreparedEffectExecution> preparedEffects)
    {
        if (board == null || effects == null || effects.Count == 0 ||
            !CanExecutePreparedExplicitEffects(
                effects,
                preparedEffects,
                actionKind))
        {
            return default;
        }

        BattleEffectResult combined = default;
        bool showAttackRange = !targets.RangeAlreadyShown;
        EffectContext context = new(
            this,
            board,
            _activeSkillResource,
            actionKind,
            targets.Faction,
            targets.EnemyTargets,
            targets.AllyTargets,
            GetScalingAttackPower());
        float[] effectAttackPowers = new float[effects.Count];
        for (int index = 0; index < effects.Count; index++)
        {
            CharacterEffectDefinition effect = effects[index];
            effectAttackPowers[index] = GetScalingAttackPower(
                effect?.StatusContributionMultipliers);
        }
        for (int index = 0; index < effects.Count; index++)
        {
            CharacterEffectDefinition effect = effects[index];
            if (!IsUsableExplicitEffect(effect) ||
                !MeetsExplicitEffectPreconditions(
                    preparedEffects,
                    index))
            {
                continue;
            }

            PreparedEffectExecution preparedEffect =
                GetPreparedEffect(preparedEffects, index);
            AbilityTargetSelection preparedTargets =
                preparedEffect.Targets;
            EffectContext effectActionContext =
                context.WithSourceAttackPower(effectAttackPowers[index]);
            if (!TryResolveEffectContext(
                    effectActionContext,
                    effect,
                    preparedTargets,
                    out EffectContext effectContext))
            {
                continue;
            }

            bool effectShowAttackRange =
                effect.TargetMode ==
                CharacterEffectTargetMode.FreshSelection
                    ? !preparedTargets.RangeAlreadyShown
                    : showAttackRange;
            BattleEffectResult current = ExecuteExplicitEffectOnTargets(
                effectContext,
                effect,
                effectShowAttackRange,
                preparedEffect.ResourceSpendAmount,
                preparedEffect.HealthSpendAmount);
            combined = combined.Combine(current);
            if (current.Attempted &&
                effect.TargetMode ==
                CharacterEffectTargetMode.InheritAction &&
                effectContext.TargetFaction ==
                CharacterTargetFaction.Enemy)
            {
                showAttackRange = false;
            }

            if (!current.Succeeded &&
                effect.FailurePolicy ==
                CharacterEffectFailurePolicy.StopRemainingEffects)
            {
                break;
            }
        }

        return combined;
    }

    private static bool TryResolveEffectContext(
        EffectContext actionContext,
        CharacterEffectDefinition effect,
        AbilityTargetSelection preparedTargets,
        out EffectContext effectContext)
    {
        effectContext = default;
        if (effect == null)
            return false;
        if (effect.Type == CharacterEffectType.GainResource ||
            effect.Type == CharacterEffectType.SpendResource ||
            effect.Type == CharacterEffectType.SpendHealth)
        {
            effectContext = actionContext;
            return true;
        }

        switch (effect.TargetMode)
        {
            case CharacterEffectTargetMode.InheritAction:
                effectContext = actionContext;
                return true;
            case CharacterEffectTargetMode.Source:
                effectContext = actionContext.RetargetToSource();
                return effectContext.HasTargets;
            case CharacterEffectTargetMode.FreshSelection:
                if (preparedTargets.Count == 0)
                    return false;
                effectContext = actionContext.RetargetTo(
                    preparedTargets.Faction,
                    preparedTargets.EnemyTargets,
                    preparedTargets.AllyTargets);
                return effectContext.HasTargets;
            default:
                return false;
        }
    }

    private BattleEffectResult ExecuteExplicitEffectOnTargets(
        EffectContext context,
        CharacterEffectDefinition effect,
        bool showAttackRange,
        int preparedResourceSpendAmount,
        int preparedHealthSpendAmount)
    {
        return BattleEffectExecutor.ExecuteEffect(
            BattleEffectContext.FromCharacter(context),
            effect,
            Data,
            1,
            showAttackRange,
            preparedResourceSpendAmount,
            preparedHealthSpendAmount);
    }

    private BattleEffectResult ExecuteHeal(
        EffectContext context,
        CharacterEffectDefinition effect,
        bool showAttackRange)
    {
        if (!effect.AmountScaling.HasTargetDependentTerm)
        {
            int amount = Data.CalculateEffectAmount(effect, context);
            if (amount <= 0)
                return default;

            int healed = context.TargetFaction ==
                         CharacterTargetFaction.Ally
                ? context.Board.TryHealAlliedCharacters(
                    context.Source,
                    context.AllyTargets,
                    amount)
                : context.Board.TryHealCharacterTargets(
                    context.Source,
                    context.EnemyTargets,
                    amount,
                    showAttackRange);
            return new BattleEffectResult(true, healed > 0);
        }

        bool attempted = false;
        int totalHealed = 0;
        if (context.TargetFaction == CharacterTargetFaction.Ally)
        {
            HashSet<IBattleCharacter> uniqueTargets = new();
            foreach (IBattleCharacter target in context.AllyTargets)
            {
                if (target == null || !uniqueTargets.Add(target))
                    continue;

                EffectContext targetContext = context.BindAllyTarget(
                    target,
                    effect.TargetStatusScalingEffect);
                int amount = Data.CalculateEffectAmount(
                    effect,
                    targetContext);
                if (amount <= 0)
                    continue;

                attempted = true;
                totalHealed += context.Board.TryHealAlliedCharacters(
                    context.Source,
                    new[] { target },
                    amount);
            }
        }
        else
        {
            HashSet<EnemyRuntime> uniqueTargets = new();
            foreach (EnemyRuntime target in context.EnemyTargets)
            {
                if (target == null || !uniqueTargets.Add(target))
                    continue;

                EffectContext targetContext = context.BindEnemyTarget(
                    target,
                    effect.TargetStatusScalingEffect);
                int amount = Data.CalculateEffectAmount(
                    effect,
                    targetContext);
                if (amount <= 0)
                    continue;

                attempted = true;
                totalHealed += context.Board.TryHealCharacterTargets(
                    context.Source,
                    new[] { target },
                    amount,
                    showAttackRange);
            }
        }

        return new BattleEffectResult(
            attempted,
            totalHealed > 0);
    }

    private BattleEffectResult ExecuteShield(
        EffectContext context,
        CharacterEffectDefinition effect,
        bool showAttackRange)
    {
        if (!effect.AmountScaling.HasTargetDependentTerm)
        {
            int amount = Data.CalculateEffectAmount(effect, context);
            if (amount <= 0)
                return default;

            int granted = context.TargetFaction ==
                          CharacterTargetFaction.Ally
                ? context.Board.TryGrantShieldToAlliedCharacters(
                    context.Source,
                    context.AllyTargets,
                    amount)
                : context.Board.TryGrantShieldToCharacterTargets(
                    context.Source,
                    context.EnemyTargets,
                    amount,
                    showAttackRange);
            return new BattleEffectResult(true, granted > 0);
        }

        bool attempted = false;
        int totalGranted = 0;
        if (context.TargetFaction == CharacterTargetFaction.Ally)
        {
            HashSet<IBattleCharacter> uniqueTargets = new();
            foreach (IBattleCharacter target in context.AllyTargets)
            {
                if (target == null || !uniqueTargets.Add(target))
                    continue;

                EffectContext targetContext = context.BindAllyTarget(
                    target,
                    effect.TargetStatusScalingEffect);
                int amount = Data.CalculateEffectAmount(
                    effect,
                    targetContext);
                if (amount <= 0)
                    continue;

                attempted = true;
                totalGranted +=
                    context.Board.TryGrantShieldToAlliedCharacters(
                        context.Source,
                        new[] { target },
                        amount);
            }
        }
        else
        {
            HashSet<EnemyRuntime> uniqueTargets = new();
            foreach (EnemyRuntime target in context.EnemyTargets)
            {
                if (target == null || !uniqueTargets.Add(target))
                    continue;

                EffectContext targetContext = context.BindEnemyTarget(
                    target,
                    effect.TargetStatusScalingEffect);
                int amount = Data.CalculateEffectAmount(
                    effect,
                    targetContext);
                if (amount <= 0)
                    continue;

                attempted = true;
                totalGranted +=
                    context.Board.TryGrantShieldToCharacterTargets(
                        context.Source,
                        new[] { target },
                        amount,
                        showAttackRange);
            }
        }

        return new BattleEffectResult(
            attempted,
            totalGranted > 0);
    }

    private BattleEffectResult ExecuteTargetScaledDamage(
        EffectContext context,
        CharacterEffectDefinition effect,
        bool showAttackRange)
    {
        List<TargetDamageSnapshot> snapshots = new(
            context.EnemyTargets.Count);
        HashSet<EnemyRuntime> uniqueTargets = new();
        foreach (EnemyRuntime target in context.EnemyTargets)
        {
            if (target == null || !uniqueTargets.Add(target))
                continue;

            EffectContext targetContext = context.BindEnemyTarget(
                target,
                effect.TargetStatusScalingEffect);
            snapshots.Add(new TargetDamageSnapshot(
                target,
                Data.CalculateEffectDamage(effect, targetContext)));
        }

        if (snapshots.Count == 0)
            return default;

        int totalDamageDealt = 0;
        int groupedDamage = 0;
        List<EnemyRuntime> groupedTargets = new();
        foreach (TargetDamageSnapshot snapshot in snapshots)
        {
            if (snapshot.Damage <= 0)
                continue;

            if (groupedTargets.Count > 0 &&
                snapshot.Damage != groupedDamage)
            {
                totalDamageDealt +=
                    context.Board.TryDamageCharacterTargets(
                        context.Source,
                        groupedTargets,
                        groupedDamage,
                        effect.DamageType,
                        showAttackRange);
                groupedTargets.Clear();
            }

            groupedDamage = snapshot.Damage;
            groupedTargets.Add(snapshot.Target);
        }

        if (groupedTargets.Count > 0)
        {
            totalDamageDealt +=
                context.Board.TryDamageCharacterTargets(
                    context.Source,
                    groupedTargets,
                    groupedDamage,
                    effect.DamageType,
                    showAttackRange);
        }

        return new BattleEffectResult(
            true,
            totalDamageDealt > 0,
            totalDamageDealt);
    }

    private bool HasUsableExplicitEffects(
        IReadOnlyList<CharacterEffectDefinition> effects,
        CharacterActionKind actionKind)
    {
        if (effects == null)
            return false;

        _ = actionKind;
        foreach (CharacterEffectDefinition effect in effects)
        {
            if (IsUsableExplicitEffect(effect))
                return true;
        }

        return false;
    }

    private bool CanExecutePreparedExplicitEffects(
        IReadOnlyList<CharacterEffectDefinition> effects,
        IReadOnlyList<PreparedEffectExecution> preparedEffects,
        CharacterActionKind actionKind)
    {
        if (effects == null)
            return false;

        _ = actionKind;
        bool hasPreparedEffect = false;
        for (int index = 0; index < effects.Count; index++)
        {
            CharacterEffectDefinition effect = effects[index];
            if (!IsUsableExplicitEffect(effect))
                continue;

            if (MeetsExplicitEffectPreconditions(
                    preparedEffects,
                    index))
            {
                hasPreparedEffect = true;
                continue;
            }

            if (effect.PreconditionFailurePolicy !=
                CharacterEffectPreconditionFailurePolicy.SkipEffect)
            {
                return false;
            }
        }

        return hasPreparedEffect;
    }

    private static bool MeetsExplicitEffectPreconditions(
        IReadOnlyList<PreparedEffectExecution> preparedEffects,
        int effectIndex)
    {
        return GetPreparedEffect(
            preparedEffects,
            effectIndex).IsPrepared;
    }

    private static PreparedEffectExecution GetPreparedEffect(
        IReadOnlyList<PreparedEffectExecution> preparedEffects,
        int effectIndex)
    {
        if (preparedEffects == null ||
            effectIndex < 0 ||
            effectIndex >= preparedEffects.Count)
        {
            return default;
        }

        return preparedEffects[effectIndex];
    }

    private static bool MeetsEffectTargetPreconditions(
        AbilityTargetSelection actionTargets,
        CharacterEffectDefinition effect,
        AbilityTargetSelection preparedTargets)
    {
        if (effect == null)
            return false;
        if (effect.Type == CharacterEffectType.GainResource ||
            effect.Type == CharacterEffectType.SpendResource ||
            effect.Type == CharacterEffectType.SpendHealth ||
            effect.TargetMode == CharacterEffectTargetMode.Source)
        {
            return true;
        }

        return effect.TargetMode switch
        {
            CharacterEffectTargetMode.InheritAction =>
                actionTargets.Count > 0,
            CharacterEffectTargetMode.FreshSelection =>
                preparedTargets.Count > 0,
            _ => false,
        };
    }

    private static bool IsUsableExplicitEffect(
        CharacterEffectDefinition effect)
    {
        if (effect == null ||
            !System.Enum.IsDefined(
                typeof(CharacterEffectTargetMode),
                effect.TargetMode) ||
            !System.Enum.IsDefined(
                typeof(CharacterEffectPreconditionFailurePolicy),
                effect.PreconditionFailurePolicy) ||
            !System.Enum.IsDefined(
                typeof(CharacterEffectFailurePolicy),
                effect.FailurePolicy))
        {
            return false;
        }

        switch (effect.Type)
        {
            case CharacterEffectType.Damage:
                return effect.TargetMode !=
                           CharacterEffectTargetMode.Source &&
                       IsDirectDamageType(effect.DamageType) &&
                       effect.DamageScaling.IsFinite &&
                       effect.DamageScaling.HasNonZeroTerm;
            case CharacterEffectType.ApplyStatus:
                return effect.StatusEffect != null &&
                       (effect.TargetMode !=
                            CharacterEffectTargetMode.Source ||
                        effect.StatusEffect.CanTargetAlly);
            case CharacterEffectType.RemoveStatus:
                if (effect.StatusRemovalTarget !=
                    CharacterStatusRemovalTarget.Single)
                {
                    return true;
                }

                CharacterStatusRemovalSelection removalSelection =
                    effect.StatusRemovalSelection;
                if (!removalSelection.HasExplicitStatus)
                    return false;

                if (effect.TargetMode != CharacterEffectTargetMode.Source)
                    return true;

                for (int index = 0;
                     index < removalSelection.ExplicitStatusCount;
                     index++)
                {
                    StatusEffectSO status =
                        removalSelection.GetExplicitStatus(index);
                    if (status != null && !status.CanTargetAlly)
                        return false;
                }

                return true;
            case CharacterEffectType.GainResource:
                return effect.AmountScaling.IsFinite &&
                       effect.AmountScaling.HasNonZeroTerm;
            case CharacterEffectType.SpendResource:
                return effect.AmountMode ==
                           CharacterDamageAmountMode.Fixed &&
                       !float.IsNaN(effect.Amount) &&
                       !float.IsInfinity(effect.Amount) &&
                       effect.Amount >= 1f &&
                       effect.SourceResourceScale == 0f &&
                       effect.TargetCurrentHealthScale == 0f &&
                       effect.TargetMaxHealthScale == 0f &&
                       effect.SourceStatusStacksScale == 0f &&
                       effect.TargetStatusStacksScale == 0f;
            case CharacterEffectType.Heal:
                return effect.AmountScaling.IsFinite &&
                       effect.AmountScaling.HasNonZeroTerm;
            case CharacterEffectType.Shield:
                return effect.AmountScaling.IsFinite &&
                       effect.AmountScaling.HasNonZeroTerm;
            case CharacterEffectType.SpendHealth:
                return effect.AmountMode ==
                           CharacterDamageAmountMode.Fixed &&
                       !float.IsNaN(effect.Amount) &&
                       !float.IsInfinity(effect.Amount) &&
                       effect.Amount >= 1f &&
                       effect.SourceResourceScale == 0f &&
                       effect.TargetCurrentHealthScale == 0f &&
                       effect.TargetMaxHealthScale == 0f &&
                       effect.SourceStatusStacksScale == 0f &&
                       effect.TargetStatusStacksScale == 0f;
            default:
                return false;
        }
    }

    private static bool IsDirectDamageType(
        CharacterAttackDamageType damageType)
    {
        return damageType == CharacterAttackDamageType.Physical ||
               damageType == CharacterAttackDamageType.Magical ||
               damageType == CharacterAttackDamageType.Fixed;
    }

    private static bool HasUsableAbilityValue(
        CharacterAttackDamageType damageType,
        int damage)
    {
        return damageType == CharacterAttackDamageType.StatusEffect ||
               damageType == CharacterAttackDamageType.StatusRemoval ||
               damage > 0;
    }

    private static bool PassesLinkage(
        CharacterActionLinkage linkage,
        bool previousAttempted,
        bool previousSucceeded)
    {
        if (linkage == CharacterActionLinkage.None)
            return true;

        return linkage switch
        {
            CharacterActionLinkage.PreviousAttackSucceeded =>
                previousSucceeded,
            CharacterActionLinkage.SimultaneousWithPreviousAttack =>
                previousAttempted,
            CharacterActionLinkage.PreviousAttackFailed =>
                !previousSucceeded,
            _ => true,
        };
    }

    private void ShowAttackSd()
    {
        _attackSdTimeRemaining = Mathf.Max(
            _attackSdTimeRemaining,
            AttackSdDisplayDuration);
        RefreshSdImage();
    }

    private void TickSdActionTimers(float deltaTime)
    {
        _attackSdTimeRemaining = Mathf.Max(
            0f,
            _attackSdTimeRemaining - deltaTime);
        _passiveSdTimeRemaining = Mathf.Max(
            0f,
            _passiveSdTimeRemaining - deltaTime);
        _skillSdTimeRemaining = Mathf.Max(
            0f,
            _skillSdTimeRemaining - deltaTime);
    }

    private void BeginAttackRecovery(float duration)
    {
        duration = TimePrecision.Normalize(duration);
        if (duration <= 0f)
            return;

        _remainingCooldown = 0f;
        _attackRecoveryRemaining = duration /
            Mathf.Max(TimePrecision.Step, GetEffectiveAttackSpeedRatio());
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
        float attackSpeed = GetEffectiveAttackSpeed();
        return attackSpeed > 0f ? 1f / attackSpeed : 0f;
    }

    private int ResolveIncomingDamage(int amount)
    {
        float modifiedDamage = GetStatusModifiedStat(
            amount,
            StatusEffectStatType.IncomingDamage,
            null);
        if (float.IsNaN(modifiedDamage) || modifiedDamage <= 0f)
            return 0;
        if (float.IsInfinity(modifiedDamage) ||
            modifiedDamage >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return Mathf.Max(0, Mathf.RoundToInt(modifiedDamage));
    }

    private float GetEffectiveAttackPower()
    {
        if (Data == null)
            return 0f;

        float modifiedPower = GetStatusModifiedStat(
            Data.AttackPower,
            StatusEffectStatType.AttackPower,
            StatusEffectOperationType.AttackPowerModifier);
        return Mathf.Max(0f, modifiedPower * _powerMultiplier);
    }

    private float GetEffectivePowerMultiplier()
    {
        if (Data == null || Data.AttackPower <= 0f)
            return Mathf.Max(0f, _powerMultiplier);

        return GetEffectiveAttackPower() / Data.AttackPower;
    }

    private float GetScalingAttackPower(
        IReadOnlyList<CharacterStatusStatContributionMultiplier>
            localContributionMultipliers = null)
    {
        if (Data == null)
            return 0f;

        float modifiedPower = GetStatusModifiedStat(
            Data.AttackPower,
            StatusEffectStatType.AttackPower,
            StatusEffectOperationType.AttackPowerModifier,
            localContributionMultipliers);
        return Mathf.Max(0f, modifiedPower * _powerMultiplier);
    }

    private float GetEffectiveAttackSpeed()
    {
        float baseAttackSpeed = GetBaseAttackSpeed();
        if (baseAttackSpeed <= 0f)
            return 0f;

        float modifiedSpeed = GetStatusModifiedStat(
            baseAttackSpeed,
            StatusEffectStatType.AttackSpeed,
            StatusEffectOperationType.AttackSpeedModifier);
        return Mathf.Max(
            TimePrecision.Step,
            modifiedSpeed * _attackSpeedMultiplier);
    }

    private float GetBaseAttackSpeed()
    {
        return Data != null && Data.AttackCooldown > 0f
            ? 1f / Data.AttackCooldown
            : 0f;
    }

    private float GetEffectiveAttackSpeedRatio()
    {
        float baseAttackSpeed = GetBaseAttackSpeed();
        return baseAttackSpeed > 0f
            ? GetEffectiveAttackSpeed() / baseAttackSpeed
            : 1f;
    }

    private float GetStatusModifiedStat(
        float baseValue,
        StatusEffectStatType statType,
        StatusEffectOperationType? operationType,
        IReadOnlyList<CharacterStatusStatContributionMultiplier>
            localContributionMultipliers = null)
    {
        StatusEffectStatAccumulator accumulator = default;
        foreach (StatusEffectRuntimeState state in _statusEffects.Values)
        {
            if (state == null || !state.HasStacks ||
                state.Definition == null)
            {
                continue;
            }

            int stacks = Mathf.Max(1, state.StackCount);
            float contributionMultiplier =
                GetStatusContributionMultiplier(
                    state.Definition,
                    statType,
                    localContributionMultipliers);
            IReadOnlyList<StatusEffectStatModifierDefinition> modifiers =
                state.Definition.StatModifiers;
            if (modifiers != null)
            {
                foreach (StatusEffectStatModifierDefinition modifier in
                         modifiers)
                {
                    if (modifier != null &&
                        modifier.StatType == statType)
                    {
                        accumulator.Add(
                            modifier,
                            stacks,
                            contributionMultiplier);
                    }
                }
            }

            IReadOnlyList<StatusEffectOperationDefinition> operations =
                state.Definition.Operations;
            if (!operationType.HasValue || operations == null)
                continue;
            foreach (StatusEffectOperationDefinition operation in operations)
            {
                if (operation == null ||
                    operation.Trigger !=
                        StatusEffectOperationTrigger.OnApply ||
                    operation.OperationType != operationType.Value ||
                    float.IsNaN(operation.Value) ||
                    float.IsInfinity(operation.Value))
                {
                    continue;
                }

                float value = operation.Value *
                    (operation.ScaleWithStacks ? stacks : 1) *
                    contributionMultiplier;
                if (operation.ValueMode == StatusEffectValueMode.Fixed)
                    accumulator.AddFlat(value);
                else if (operation.ValueMode == StatusEffectValueMode.Ratio)
                    accumulator.AddAdditiveRatio(value);
            }
        }

        return accumulator.Evaluate(baseValue);
    }

    private float GetStatusContributionMultiplier(
        StatusEffectSO statusEffect,
        StatusEffectStatType statType,
        IReadOnlyList<CharacterStatusStatContributionMultiplier>
            localContributionMultipliers)
    {
        float multiplier = 1f;
        if (Data?.PassiveDefinitions != null)
        {
            foreach (CharacterPassiveDefinition passive in
                     Data.PassiveDefinitions)
            {
                if (passive == null ||
                    !passive.HasStatusContributionSection)
                {
                    continue;
                }

                multiplier *= ResolveStatusContributionMultiplier(
                    passive.StatusContributionMultipliers,
                    statusEffect,
                    statType);
            }
        }

        multiplier *= ResolveStatusContributionMultiplier(
            localContributionMultipliers,
            statusEffect,
            statType);
        return float.IsNaN(multiplier) ||
               float.IsInfinity(multiplier)
            ? 1f
            : Mathf.Max(0f, multiplier);
    }

    private static float ResolveStatusContributionMultiplier(
        IReadOnlyList<CharacterStatusStatContributionMultiplier> modifiers,
        StatusEffectSO statusEffect,
        StatusEffectStatType statType)
    {
        if (modifiers == null || statusEffect == null)
            return 1f;

        float multiplier = 1f;
        foreach (CharacterStatusStatContributionMultiplier modifier in
                 modifiers)
        {
            if (modifier == null ||
                modifier.StatType != statType ||
                !IsSameStatus(
                    modifier.StatusEffect,
                    statusEffect))
            {
                continue;
            }

            multiplier *= Mathf.Max(0f, modifier.Multiplier);
        }

        return multiplier;
    }

    private static bool IsSameStatus(
        StatusEffectSO left,
        StatusEffectSO right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left == null || right == null ||
            string.IsNullOrWhiteSpace(left.StatusId))
        {
            return false;
        }

        return string.Equals(
            left.StatusId,
            right.StatusId,
            StringComparison.Ordinal);
    }

    private void AdjustCooldownForAttackSpeedChange(
        float previousAttackSpeed,
        float currentAttackSpeed)
    {
        if (previousAttackSpeed <= 0f || currentAttackSpeed <= 0f ||
            Mathf.Approximately(previousAttackSpeed, currentAttackSpeed))
        {
            return;
        }

        float timeScale = previousAttackSpeed / currentAttackSpeed;
        _remainingCooldown *= timeScale;
        _attackRecoveryRemaining *= timeScale;
    }

    private void PlayActionSfx(AudioClip clip)
    {
        if (clip == null)
            return;

        InitializeAttackSfxSpeaker();
        GameManager manager = GameManager.Instance;
        if (manager?.Audio != null)
        {
            manager.Audio.PlaySfx(attackSfxSpeaker, clip);
            return;
        }

        attackSfxSpeaker?.PlayOneShot(clip);
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

    private void HandleLocaleChanged(string unusedLocale)
    {
        RefreshUi();
        RefreshSkillTooltip();
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
        tooltipRect.sizeDelta = new Vector2(420f, 220f);
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

        LocalizationFontResolver.ApplyGameDefault(_skillTooltipText);
        RectTransform textRect = _skillTooltipText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 12f);
        textRect.offsetMax = new Vector2(-16f, -12f);
        _skillTooltipText.fontSize = 17f;
        _skillTooltipText.enableAutoSizing = true;
        _skillTooltipText.fontSizeMin = 12f;
        _skillTooltipText.fontSizeMax = 17f;
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
        float verticalOffset = 0f;
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

            RectTransform measuredTooltipRect =
                (RectTransform)_skillTooltip.transform;
            float halfHeight =
                measuredTooltipRect.rect.height * 0.5f;
            float minimumCenter = canvasRect.rect.yMin + halfHeight + 12f;
            float maximumCenter = canvasRect.rect.yMax - halfHeight - 12f;
            if (minimumCenter <= maximumCenter)
            {
                float clampedCenter = Mathf.Clamp(
                    canvasLocalCenter.y,
                    minimumCenter,
                    maximumCenter);
                verticalOffset =
                    clampedCenter - canvasLocalCenter.y;
            }
        }

        RectTransform tooltipRect =
            (RectTransform)_skillTooltip.transform;
        float anchorX = openToLeft ? 0f : 1f;
        tooltipRect.anchorMin = new Vector2(anchorX, 0.5f);
        tooltipRect.anchorMax = new Vector2(anchorX, 0.5f);
        tooltipRect.pivot = new Vector2(openToLeft ? 1f : 0f, 0.5f);
        tooltipRect.anchoredPosition = new Vector2(
            openToLeft ? -12f : 12f,
            verticalOffset);
        tooltipRect.SetAsLastSibling();
    }

    private void RefreshSkillTooltip()
    {
        if (_skillTooltipText == null || Data == null)
            return;

        if (_skillTooltipKind == CharacterAbilityIconKind.Details)
        {
            ResizeSkillTooltip(new Vector2(520f, 430f));
            _skillTooltipText.text = BuildCharacterDetailTooltip();
            return;
        }

        ResizeSkillTooltip(new Vector2(420f, 220f));
        if (_skillTooltipKind == CharacterAbilityIconKind.Passive)
        {
            string passiveTitle = LocalizationService.Get(
                LocalizationKeys.CodexCharacterPassive);
            _skillTooltipText.text =
                $"<b>{passiveTitle}</b>\n" +
                CharacterLocalization.GetPassiveDescription(Data);
            return;
        }

        bool hasEnoughEnergy = _activeSkillResource != null &&
                               _activeSkillResource.Current >=
                               Data.ActiveSkillCost;
        string status = CharacterLocalization.GetTurretStatus(
            false,
            hasEnoughEnergy);
        _skillTooltipText.text =
            CharacterLocalization.GetTurretSkillHeader(
                Data.ActiveSkillCost,
                status) + "\n" +
            CharacterLocalization.GetActiveSkillDescription(Data) + "\n" +
            CharacterLocalization.GetTurretClickActivate();
    }

    private void ResizeSkillTooltip(Vector2 size)
    {
        if (_skillTooltip != null)
            ((RectTransform)_skillTooltip.transform).sizeDelta = size;
    }

    private string BuildCharacterDetailTooltip()
    {
        System.Text.StringBuilder builder = new();
        builder.Append("<size=22><b>");
        builder.Append(CharacterLocalization.GetName(Data));
        builder.Append("</b></size>\n");
        builder.Append(CharacterLocalization.GetCompactSummary(Data));
        builder.Append("  |  ");
        builder.Append(GetCurrentCooldownStatusText());

        string characterDescription =
            CharacterLocalization.GetDescription(Data);
        if (!string.IsNullOrWhiteSpace(characterDescription))
        {
            builder.Append("\n\n");
            builder.Append(characterDescription);
        }

        if (Data.HasCustomAttackDefinitions)
        {
            builder.Append("\n\n<b>");
            builder.Append(LocalizationService.Get(
                LocalizationKeys.CodexCharacterNormalAttack));
            builder.Append("</b>\n");
            builder.Append(
                CharacterLocalization.GetNormalAttackDescription(Data));
        }

        if (Data.HasCustomPassiveDefinitions)
        {
            builder.Append("\n\n<b>");
            builder.Append(LocalizationService.Get(
                LocalizationKeys.CodexCharacterPassive));
            builder.Append("</b>\n");
            builder.Append(
                CharacterLocalization.GetPassiveDescription(Data));
        }

        if (Data.HasCustomSkillDefinitions)
        {
            builder.Append("\n\n<b>");
            builder.Append(CharacterLocalization.GetActiveSkillTitle(
                Data.ActiveSkillCost));
            builder.Append("</b>\n");
            builder.Append(
                CharacterLocalization.GetActiveSkillDescription(Data));
        }

        return builder.ToString();
    }

    private void EnsureSdInfoView()
    {
        CacheInfoLayout();

        bool hasSdSprite = Data != null &&
            (Data.WaitingSdSprite != null ||
             Data.AttackSdSprite != null ||
             Data.DamagedSdSprite != null ||
             Data.SkillSdSprite != null ||
             Data.PassiveSdSprite != null ||
             Data.IconSprite != null);

        if (!hasSdSprite)
        {
            if (sdImage != null)
                sdImage.gameObject.SetActive(false);
            ApplySdInfoLayout(true);
            return;
        }

        if (sdImage == null)
        {
            Transform existing = transform.Find("imgCharacterSd");
            if (existing != null)
                sdImage = existing.GetComponent<Image>();
        }

        if (sdImage == null)
        {
            GameObject imageObject = new(
                "imgCharacterSd",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(transform, false);
            sdImage = imageObject.GetComponent<Image>();
        }

        RectTransform imageRect = sdImage.rectTransform;
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.zero;
        imageRect.pivot = Vector2.zero;
        imageRect.anchoredPosition = new Vector2(-10f, 18f);
        imageRect.sizeDelta = new Vector2(168f, 168f);
        imageRect.localScale = Vector3.one;
        sdImage.preserveAspect = true;
        sdImage.raycastTarget = true;
        sdImage.type = Image.Type.Simple;
        CharacterInfoHoverView interaction =
            sdImage.GetComponent<CharacterInfoHoverView>() ??
            sdImage.gameObject.AddComponent<CharacterInfoHoverView>();
        interaction.Configure(this);
        sdImage.gameObject.SetActive(true);
        imageRect.SetAsFirstSibling();

        ApplySdInfoLayout(true);
        RefreshSdImage();
    }

    private void CacheInfoLayout()
    {
        if (_infoLayoutCached)
            return;

        _cooldownTrack = cooldownFill != null
            ? cooldownFill.rectTransform.parent as RectTransform
            : null;
        if (nameText == null || attackText == null ||
            cooldownText == null || _cooldownTrack == null)
        {
            return;
        }

        _nameLayout = RectLayout.Capture(nameText.rectTransform);
        _attackLayout = RectLayout.Capture(attackText.rectTransform);
        _cooldownLayout = RectLayout.Capture(cooldownText.rectTransform);
        _cooldownTrackLayout = RectLayout.Capture(_cooldownTrack);
        _infoLayoutCached = true;
    }

    private void ApplySdInfoLayout(bool enabled)
    {
        if (!_infoLayoutCached || _sdLayoutEnabled == enabled)
            return;

        if (!enabled)
        {
            nameText.gameObject.SetActive(true);
            attackText.gameObject.SetActive(true);
            cooldownText.gameObject.SetActive(true);
            _nameLayout.Apply(nameText.rectTransform);
            _attackLayout.Apply(attackText.rectTransform);
            _cooldownLayout.Apply(cooldownText.rectTransform);
            _cooldownTrackLayout.Apply(_cooldownTrack);
            _sdLayoutEnabled = false;
            return;
        }

        nameText.gameObject.SetActive(false);
        attackText.gameObject.SetActive(false);
        cooldownText.gameObject.SetActive(false);

        _cooldownTrack.anchorMin = Vector2.zero;
        _cooldownTrack.anchorMax = new Vector2(1f, 0f);
        _cooldownTrack.pivot = new Vector2(0.5f, 0.5f);
        _cooldownTrack.offsetMin = new Vector2(6f, 4f);
        _cooldownTrack.offsetMax = new Vector2(-6f, 14f);
        _sdLayoutEnabled = true;
    }

    private void EnsureAbilityIconView()
    {
        _passiveIconImage = EnsureAbilityIcon(
            "grpPassiveAbilityIcon",
            "imgPassiveAbilityIcon",
            new Vector2(-62f, -6f),
            CharacterAbilityIconKind.Passive,
            out _passiveIconFrame);
        _activeSkillIconImage = EnsureAbilityIcon(
            "grpActiveAbilityIcon",
            "imgActiveAbilityIcon",
            new Vector2(-8f, -6f),
            CharacterAbilityIconKind.Active,
            out _activeSkillIconFrame);
        RefreshAbilityIcons();
    }

    private Image EnsureAbilityIcon(
        string frameName,
        string imageName,
        Vector2 anchoredPosition,
        CharacterAbilityIconKind kind,
        out Image frameImage)
    {
        Transform existingFrame = transform.Find(frameName);
        GameObject frameObject = existingFrame != null
            ? existingFrame.gameObject
            : new GameObject(
                frameName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(CharacterAbilityIconView));
        if (existingFrame == null)
            frameObject.transform.SetParent(transform, false);
        frameObject.layer = gameObject.layer;

        RectTransform frameRect =
            (RectTransform)frameObject.transform;
        frameRect.anchorMin = Vector2.one;
        frameRect.anchorMax = Vector2.one;
        frameRect.pivot = Vector2.one;
        frameRect.anchoredPosition = anchoredPosition;
        frameRect.sizeDelta = new Vector2(
            AbilityIconSize,
            AbilityIconSize);
        frameRect.localScale = Vector3.one;

        frameImage = frameObject.GetComponent<Image>() ??
                     frameObject.AddComponent<Image>();
        frameImage.color = AbilityIconFrameColor;
        frameImage.raycastTarget = true;

        Outline outline = frameObject.GetComponent<Outline>() ??
                          frameObject.AddComponent<Outline>();
        outline.effectColor = new Color(
            EffectColor.r,
            EffectColor.g,
            EffectColor.b,
            0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        CharacterAbilityIconView interaction =
            frameObject.GetComponent<CharacterAbilityIconView>() ??
            frameObject.AddComponent<CharacterAbilityIconView>();
        interaction.Configure(this, kind);

        Transform existingImage = frameRect.Find(imageName);
        GameObject imageObject = existingImage != null
            ? existingImage.gameObject
            : new GameObject(
                imageName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
        if (existingImage == null)
            imageObject.transform.SetParent(frameRect, false);
        imageObject.layer = gameObject.layer;

        Image iconImage = imageObject.GetComponent<Image>() ??
                          imageObject.AddComponent<Image>();
        RectTransform iconRect = iconImage.rectTransform;
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(4f, 4f);
        iconRect.offsetMax = new Vector2(-4f, -4f);
        iconRect.localScale = Vector3.one;
        iconImage.type = Image.Type.Simple;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        frameRect.SetAsLastSibling();
        return iconImage;
    }

    private void RefreshAbilityIcons()
    {
        if (Data == null)
            return;

        bool hasPassive = Data.HasCustomPassiveDefinitions;
        if (_passiveIconFrame != null)
            _passiveIconFrame.gameObject.SetActive(hasPassive);
        if (_passiveIconImage != null)
        {
            _passiveIconImage.sprite =
                Data.PassiveAbilityIconSprite;
            _passiveIconImage.enabled =
                _passiveIconImage.sprite != null;
            _passiveIconImage.color = Color.white;
        }

        bool hasActiveSkill = Data.HasCustomSkillDefinitions;
        if (_activeSkillIconFrame != null)
            _activeSkillIconFrame.gameObject.SetActive(hasActiveSkill);
        if (_activeSkillIconImage != null)
        {
            _activeSkillIconImage.sprite =
                Data.ActiveAbilityIconSprite;
            _activeSkillIconImage.enabled =
                _activeSkillIconImage.sprite != null;
            _activeSkillIconImage.color = CanActivateActiveSkill()
                ? Color.white
                : UnavailableAbilityIconColor;
        }

        if (_passiveIconFrame != null)
        {
            Outline passiveOutline =
                _passiveIconFrame.GetComponent<Outline>();
            if (passiveOutline != null)
                passiveOutline.effectColor = EffectColor;
        }
        if (_activeSkillIconFrame != null)
        {
            bool available = CanActivateActiveSkill();
            _activeSkillIconFrame.color = available
                ? Color.Lerp(AbilityIconFrameColor, EffectColor, 0.2f)
                : AbilityIconFrameColor;
            Outline activeOutline =
                _activeSkillIconFrame.GetComponent<Outline>();
            if (activeOutline != null)
            {
                activeOutline.effectColor = available
                    ? EffectColor
                    : new Color(0.18f, 0.18f, 0.18f, 1f);
            }
        }
    }

    private static void SetAnchoredRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private void RefreshSdImage()
    {
        if (sdImage == null || Data == null ||
            !sdImage.gameObject.activeSelf)
        {
            return;
        }

        Sprite sprite = null;
        if (_skillSdTimeRemaining > 0f)
            sprite = Data.SkillSdSprite;
        if (sprite == null && _passiveSdTimeRemaining > 0f)
            sprite = Data.PassiveSdSprite;
        if (sprite == null && _attackSdTimeRemaining > 0f)
            sprite = Data.AttackSdSprite;
        if (sprite == null && IsActionDisabled())
            sprite = Data.DamagedSdSprite;
        if (sprite == null)
            sprite = Data.WaitingSdSprite;
        if (sprite == null)
            sprite = Data.IconSprite;

        sdImage.sprite = sprite;
        sdImage.enabled = sprite != null;
    }

    private void RefreshUi()
    {
        if (!_initialized)
            return;

        RefreshSdImage();

        string slotLabel = PartySlotIndex >= 0
            ? $"[S{PartySlotNumber}] "
            : string.Empty;
        nameText.text = slotLabel + CharacterLocalization.GetTurretName(Data);
        nameText.color = EffectColor;
        attackText.text = CharacterLocalization.GetTurretAttack(Data);
        cooldownText.text = GetCurrentCooldownStatusText();
        float effectiveAttackCooldown = GetEffectiveAttackCooldown();
        cooldownFill.fillAmount = _attackRecoveryRemaining > 0f
            ? 0f
            : effectiveAttackCooldown > 0f
                ? 1f - Mathf.Clamp01(
                    _remainingCooldown / effectiveAttackCooldown)
                : 1f;
        cooldownFill.color = EffectColor;

        RefreshManualTargetHighlight();

        RefreshAbilityIcons();

        if (_skillTooltip != null && _skillTooltip.activeSelf)
            RefreshSkillTooltip();
    }

    private string GetCurrentCooldownStatusText()
    {
        float disabledTimeRemaining = GetDisabledDuration();
        if (disabledTimeRemaining > 0f)
        {
            return CharacterLocalization.GetCooldownStop(
                DisabledTimeRemaining);
        }

        if (_attackRecoveryRemaining > 0f)
        {
            float displayedRecovery =
                TimePrecision.FloorToTenth(_attackRecoveryRemaining);
            return CharacterLocalization.GetCooldownRecovery(
                displayedRecovery);
        }

        float displayedCooldown =
            TimePrecision.FloorToTenth(_remainingCooldown);
        return _remainingCooldown > 0f
            ? CharacterLocalization.GetCooldownWait(displayedCooldown)
            : CharacterLocalization.GetReadyStatus();
    }
}
