using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(Image))]
public sealed class DungeonTileView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image slotSurface;
    [SerializeField] private RectTransform stackRoot;
    [SerializeField] private EnemyCard enemyCardPrefab;
    [Header("Target Area Feedback")]
    [SerializeField] private Image attackRangeOverlay;
    [SerializeField, Min(0.01f)]
    private float targetAreaDisplayDuration = 0.5f;
    [SerializeField]
    private Color targetAreaColor = new(1f, 0.58f, 0.18f, 0.22f);
    [SerializeField] private Image manualSelectionOverlay;

    private readonly List<EnemyRuntime> _enemies = new();
    private readonly List<EnemyCard> _cards = new();
    private readonly List<EnemyCard> _cardPool = new();
    private float _targetAreaDisplayRemaining;
    private int _maximumStackSize;
    private float _currentCellSize;
    private Color _baseSlotColor;
    private EnemyRuntime _exclusiveFootprintOccupant;
    private bool _cardPoolPrepared;

    public int Row { get; private set; }
    public int Column { get; private set; }
    public int StackCount => _enemies.Count;
    public EnemyRuntime TopEnemy =>
        _enemies.Count > 0 ? _enemies[^1] : null;
    internal EnemyRuntime InteractionEnemy =>
        _exclusiveFootprintOccupant != null
            ? _exclusiveFootprintOccupant
            : TopEnemy;
    public int TopEnemyHealth => TopEnemy != null ? TopEnemy.Health : 0;
    public bool IsFull => _enemies.Count >= _maximumStackSize;
    public event Action<EnemyRuntime> EnemyClicked;
    internal bool CanAddEnemy =>
        !IsFull && stackRoot != null && enemyCardPrefab != null;

    public void Initialize(int row, int column, int stackSize)
    {
        Row = row;
        Column = column;
        _maximumStackSize = Mathf.Max(1, stackSize);

        Image pointerSurface = GetComponent<Image>();
        if (pointerSurface != null)
            pointerSurface.raycastTarget = true;

        if (slotSurface != null)
        {
            slotSurface.raycastTarget = false;
            _baseSlotColor = (row + column) % 2 == 0
                ? new Color(0.075f, 0.105f, 0.09f, 1f)
                : new Color(0.09f, 0.12f, 0.105f, 1f);
            slotSurface.color = _baseSlotColor;
        }

        PrepareEnemyCardPool();
        EnsureTargetAreaOverlay();
        ClearTargetAreaIndicator();
        SetManualSelectionState(false, false);
    }

    internal void SetExclusiveFootprintOccupant(
        EnemyRuntime enemy,
        bool isAnchor)
    {
        _exclusiveFootprintOccupant = enemy;
        if (slotSurface == null)
            return;

        slotSurface.color = enemy == null
            ? _baseSlotColor
            : Color.Lerp(
                _baseSlotColor,
                isAnchor
                    ? new Color(0.42f, 0.26f, 0.12f, 1f)
                    : new Color(0.24f, 0.2f, 0.1f, 1f),
                isAnchor ? 0.55f : 0.38f);
    }

    private void Update()
    {
        if (_targetAreaDisplayRemaining <= 0f)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        if (deltaTime <= 0f)
            return;

        _targetAreaDisplayRemaining = Mathf.Max(
            0f,
            _targetAreaDisplayRemaining - deltaTime);
        RefreshTargetAreaIndicator();
    }

    internal bool TryAdd(EnemyRuntime enemy)
    {
        if (!CanAddEnemy || enemy == null)
            return false;

        EnemyCard card = AcquireEnemyCard();
        card.ApplyGameDefaultFont();
        card.name = $"grpEnemyCard_{_cards.Count + 1}";
        card.Bind(enemy);
        card.Clicked += HandleEnemyCardClicked;
        _enemies.Add(enemy);
        _cards.Add(card);

        RefreshCardPositions();
        return true;
    }

    public bool TrySetTopEnemyHealth(int health)
    {
        if (_enemies.Count == 0)
            return false;

        TopEnemy.SetHealth(health);
        _cards[^1]?.RefreshHealth();
        return true;
    }

    internal int TryDamageTop(int damage)
    {
        return TryDamageTop(damage, CharacterAttackDamageType.Physical);
    }

    internal int TryDamageTop(
        int damage,
        CharacterAttackDamageType damageType)
    {
        if (_enemies.Count == 0 || damage <= 0)
            return 0;

        EnemyRuntime topEnemy = TopEnemy;
        int appliedDamage = topEnemy.TakeDamage(damage, damageType);
        if (appliedDamage <= 0)
            return 0;

        if (topEnemy.Health <= 0)
        {
            topEnemy.ClearStatusEffectsOnDefeat();
            TryRemoveTop();
        }
        else
            _cards[^1]?.RefreshHealth();

        return appliedDamage;
    }

    internal int TryHealTop(int amount)
    {
        if (_enemies.Count == 0 || amount <= 0)
            return 0;

        int healedAmount = TopEnemy.Heal(amount);
        if (healedAmount > 0)
            _cards[^1]?.RefreshHealth();

        return healedAmount;
    }

    internal int TryGrantShieldTop(int amount)
    {
        if (_enemies.Count == 0 || amount <= 0)
            return 0;

        int grantedAmount = TopEnemy.GainShield(amount);
        if (grantedAmount > 0)
            _cards[^1]?.RefreshHealth();

        return grantedAmount;
    }

    internal void RefreshTopEnemyCard()
    {
        if (_cards.Count > 0)
            _cards[^1]?.RefreshHealth();
    }

    internal bool TryApplyFireToTop(
        IBattleCharacter source,
        float duration,
        float tickInterval,
        int tickDamage)
    {
        return TryApplyFireToTop(
            source,
            duration,
            tickInterval,
            tickDamage,
            null);
    }

    internal bool TryApplyFireToTop(
        IBattleCharacter source,
        float duration,
        float tickInterval,
        int tickDamage,
        Func<DungeonTileView, int, IBattleCharacter, int> applyDamage)
    {
        StatusEffectSO fire =
            StatusEffectDefinitionCatalog.FindById(StatusEffectIds.Fire);
        if (fire == null)
            return false;

        return TryApplyStatusToTop(
            fire,
            duration,
            tickDamage,
            source,
            tickInterval,
            applyDamage);
    }

    internal bool TryApplyStatusToTop(
        StatusEffectSO statusEffect,
        float duration,
        int stacks,
        IBattleCharacter source = null,
        float tickInterval = 0f)
    {
        return TryApplyStatusToTop(
            statusEffect,
            duration,
            stacks,
            source,
            tickInterval,
            null);
    }

    internal bool TryApplyStatusToTop(
        StatusEffectSO statusEffect,
        float duration,
        int stacks,
        IBattleCharacter source,
        float tickInterval,
        Func<DungeonTileView, int, IBattleCharacter, int> applyDamage)
    {
        if (_enemies.Count == 0 || statusEffect == null)
            return false;

        EnemyRuntime target = TopEnemy;
        bool applied = target.ApplyStatusEffect(
            statusEffect,
            duration,
            stacks,
            source,
            tickInterval,
            CreateStatusDamageCallback(target, applyDamage));
        if (applied && ReferenceEquals(TopEnemy, target))
            _cards[^1]?.RefreshStatus();
        return applied;
    }

    internal int TryRemoveStatusFromTop(
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        int removalCount)
    {
        if (removalCount < 0)
            return 0;

        return TryRemoveStatusFromTop(
            new CharacterStatusRemovalSelection(
                removalTarget,
                statusEffect),
            CharacterStatusRemovalAmount.Fixed(removalCount),
            null);
    }

    internal int TryRemoveStatusFromTop(
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        int removalCount,
        Func<DungeonTileView, int, IBattleCharacter, int> applyDamage)
    {
        if (removalCount < 0)
            return 0;

        return TryRemoveStatusFromTop(
            new CharacterStatusRemovalSelection(
                removalTarget,
                statusEffect),
            CharacterStatusRemovalAmount.Fixed(removalCount),
            applyDamage);
    }

    internal int TryRemoveStatusFromTop(
        CharacterStatusRemovalTarget removalTarget,
        StatusEffectSO statusEffect,
        CharacterStatusRemovalAmount removalAmount,
        Func<DungeonTileView, int, IBattleCharacter, int> applyDamage)
    {
        return TryRemoveStatusFromTop(
            new CharacterStatusRemovalSelection(
                removalTarget,
                statusEffect),
            removalAmount,
            applyDamage);
    }

    internal int TryRemoveStatusFromTop(
        CharacterStatusRemovalSelection removalSelection,
        CharacterStatusRemovalAmount removalAmount,
        Func<DungeonTileView, int, IBattleCharacter, int> applyDamage)
    {
        if (_enemies.Count == 0)
            return 0;

        EnemyRuntime target = TopEnemy;
        int removed = target.RemoveStatusEffects(
            removalSelection,
            removalAmount,
            CreateStatusDamageCallback(target, applyDamage));
        if (removed <= 0)
            return 0;

        if (ReferenceEquals(TopEnemy, target))
            _cards[^1]?.RefreshStatus();
        return removed;
    }

    internal void ShowTargetArea()
    {
        EnsureTargetAreaOverlay();
        _targetAreaDisplayRemaining = Mathf.Max(
            _targetAreaDisplayRemaining,
            Mathf.Max(0.01f, targetAreaDisplayDuration));
        RefreshTargetAreaIndicator();
    }

    internal void SetManualSelectionState(bool candidate, bool selected)
    {
        EnsureManualSelectionOverlay();
        if (manualSelectionOverlay == null)
            return;

        manualSelectionOverlay.enabled = candidate;
        if (!candidate)
            return;

        manualSelectionOverlay.color = selected
            ? new Color(1f, 0.78f, 0.12f, 0.42f)
            : new Color(0.2f, 0.9f, 0.5f, 0.28f);
    }

    internal void TickStatusEffects(
        float deltaTime,
        Func<DungeonTileView, int, IBattleCharacter, int> applyDamage)
    {
        if (deltaTime <= 0f || _enemies.Count == 0 || applyDamage == null)
            return;

        EnemyRuntime statusEnemy = TopEnemy;
        bool hadFire = statusEnemy.HasFire;
        bool statusChanged = statusEnemy.TickStatusEffects(
            deltaTime,
            CreateStatusDamageCallback(statusEnemy, applyDamage));

        if (!ReferenceEquals(TopEnemy, statusEnemy))
            return;

        if (statusChanged)
            _cards[^1]?.RefreshStatus();
        if (hadFire != statusEnemy.HasFire)
            _cards[^1]?.RefreshHealth();
    }

    private Func<int, IBattleCharacter, bool> CreateStatusDamageCallback(
        EnemyRuntime target,
        Func<DungeonTileView, int, IBattleCharacter, int> applyDamage)
    {
        if (target == null || applyDamage == null)
            return null;

        return (damage, source) =>
        {
            if (!ReferenceEquals(TopEnemy, target))
                return false;

            int appliedDamage = applyDamage(this, damage, source);
            if (appliedDamage > 0)
                source?.RecordDamageDealt(appliedDamage);
            return ReferenceEquals(TopEnemy, target);
        };
    }

    public bool TryRemoveTop()
    {
        if (_enemies.Count == 0)
            return false;

        int topIndex = _enemies.Count - 1;
        EnemyCard topCard = _cards[topIndex];
        _enemies.RemoveAt(topIndex);
        _cards.RemoveAt(topIndex);

        if (topCard != null)
        {
            topCard.Clicked -= HandleEnemyCardClicked;
            topCard.gameObject.SetActive(false);
        }

        RefreshCardPositions();
        return true;
    }

    public void ClearStack()
    {
        foreach (EnemyCard card in _cards)
        {
            if (card != null)
            {
                card.Clicked -= HandleEnemyCardClicked;
                card.gameObject.SetActive(false);
            }
        }

        _enemies.Clear();
        _cards.Clear();
        ClearTargetAreaIndicator();
        SetManualSelectionState(false, false);
    }

    private void PrepareEnemyCardPool()
    {
        if (_cardPoolPrepared || stackRoot == null)
            return;

        _cardPoolPrepared = true;
        for (int index = 0; index < stackRoot.childCount; index++)
        {
            EnemyCard card = stackRoot.GetChild(index)
                .GetComponent<EnemyCard>();
            if (card == null || _cardPool.Contains(card))
                continue;

            card.gameObject.SetActive(false);
            _cardPool.Add(card);
        }
    }

    private EnemyCard AcquireEnemyCard()
    {
        PrepareEnemyCardPool();
        for (int index = 0; index < _cardPool.Count; index++)
        {
            EnemyCard candidate = _cardPool[index];
            if (candidate != null && !_cards.Contains(candidate))
            {
                candidate.gameObject.SetActive(true);
                return candidate;
            }
        }

        EnemyCard instance = Instantiate(enemyCardPrefab, stackRoot);
        _cardPool.Add(instance);
        return instance;
    }

    internal List<EnemyRuntime> CopyEnemyRuntimes()
    {
        return new List<EnemyRuntime>(_enemies);
    }

    internal bool TryGetEnemyVfxAnchor(
        EnemyRuntime enemy,
        BattleVfxAnchorType anchorType,
        out BattleVfxAnchorSnapshot snapshot)
    {
        snapshot = default;
        int index = enemy != null ? _enemies.IndexOf(enemy) : -1;
        if (index < 0 || index >= _cards.Count || _cards[index] == null)
            return false;

        return BattleVfxUiAnchorUtility.TryCreateScreenAnchor(
            _cards[index].transform as RectTransform,
            transform as RectTransform,
            anchorType,
            out snapshot);
    }

    private void HandleEnemyCardClicked(EnemyRuntime enemy)
    {
        if (enemy != null && ReferenceEquals(enemy, InteractionEnemy))
            NotifyEnemyClicked(enemy);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null &&
            eventData.button == PointerEventData.InputButton.Left &&
            InteractionEnemy != null)
        {
            NotifyEnemyClicked(InteractionEnemy);
        }
    }

    private void NotifyEnemyClicked(EnemyRuntime enemy)
    {
        EnemyClicked?.Invoke(enemy);
    }

    private void EnsureTargetAreaOverlay()
    {
        if (attackRangeOverlay != null)
            return;

        Transform existing = transform.Find("imgAttackRangeOverlay");
        if (existing != null)
            attackRangeOverlay = existing.GetComponent<Image>();
    }

    private void EnsureManualSelectionOverlay()
    {
        if (manualSelectionOverlay != null)
            return;

        Transform existing = transform.Find("imgManualSelectionOverlay");
        if (existing != null)
            manualSelectionOverlay = existing.GetComponent<Image>();
    }

    private void RefreshTargetAreaIndicator()
    {
        if (attackRangeOverlay == null)
            return;

        if (_targetAreaDisplayRemaining <= 0f)
        {
            attackRangeOverlay.enabled = false;
            return;
        }

        attackRangeOverlay.color = new Color(
            Mathf.Clamp01(targetAreaColor.r),
            Mathf.Clamp01(targetAreaColor.g),
            Mathf.Clamp01(targetAreaColor.b),
            Mathf.Clamp01(targetAreaColor.a));
        attackRangeOverlay.enabled = true;
    }

    private void ClearTargetAreaIndicator()
    {
        _targetAreaDisplayRemaining = 0f;
        if (attackRangeOverlay != null)
            attackRangeOverlay.enabled = false;
    }

    private void OnDisable()
    {
        ClearTargetAreaIndicator();
    }

    public void RefreshLayout(float cellSize)
    {
        _currentCellSize = Mathf.Max(1f, cellSize);
        RefreshCardPositions();
    }

    private void RefreshCardPositions()
    {
        if (_currentCellSize <= 0f)
            return;

        float cardWidth = _currentCellSize * 0.61f;
        float cardHeight = _currentCellSize * 0.66f;
        float baseHeight = _currentCellSize * 0.105f;
        float stackStep = Mathf.Max(4f, _currentCellSize * 0.052f);
        float sideDepth = Mathf.Max(3f, _currentCellSize * 0.07f);
        float edge = Mathf.Max(1.5f, _currentCellSize * 0.014f);

        for (int index = 0; index < _cards.Count; index++)
        {
            EnemyCard card = _cards[index];
            if (card == null)
                continue;

            RectTransform root = card.RectTransform;
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(cardWidth, cardHeight);

            root.anchoredPosition = new Vector2(0f, baseHeight + stackStep * index);
            card.ApplyLayout(edge, sideDepth);
        }
    }

}
