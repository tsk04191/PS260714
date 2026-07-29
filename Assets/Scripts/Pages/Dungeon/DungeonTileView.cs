using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(Image))]
public sealed class DungeonTileView : MonoBehaviour, IPointerClickHandler
{
    private const float AttackRangeDisplayDuration = 0.5f;
    private const byte AttackRangeMinimumAlpha = 100;
    private const byte AttackRangeAlphaStep = 50;
    private const byte AttackRangeMaximumAlpha = 250;

    [SerializeField] private Image slotSurface;
    [SerializeField] private RectTransform stackRoot;
    [SerializeField] private EnemyCard enemyCardPrefab;

    private readonly List<EnemyRuntime> _enemies = new();
    private readonly List<EnemyCard> _cards = new();
    private readonly List<float> _attackRangeHitDurations = new();
    private Image _attackRangeOverlay;
    private Image _manualSelectionOverlay;
    private int _maximumStackSize;
    private float _currentCellSize;

    public int Row { get; private set; }
    public int Column { get; private set; }
    public int StackCount => _enemies.Count;
    public EnemyRuntime TopEnemy =>
        _enemies.Count > 0 ? _enemies[^1] : null;
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
            slotSurface.color = (row + column) % 2 == 0
                ? new Color(0.075f, 0.105f, 0.09f, 1f)
                : new Color(0.09f, 0.12f, 0.105f, 1f);
        }

        EnsureAttackRangeOverlay();
        ClearAttackRangeIndicator();
        SetManualSelectionState(false, false);
    }

    private void Update()
    {
        if (_attackRangeHitDurations.Count == 0)
            return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        for (int index = _attackRangeHitDurations.Count - 1;
             index >= 0;
             index--)
        {
            float remaining = _attackRangeHitDurations[index] - deltaTime;
            if (remaining <= 0f)
                _attackRangeHitDurations.RemoveAt(index);
            else
                _attackRangeHitDurations[index] = remaining;
        }

        RefreshAttackRangeIndicator();
    }

    internal bool TryAdd(EnemyRuntime enemy)
    {
        if (!CanAddEnemy || enemy == null)
            return false;

        EnemyCard card = Instantiate(enemyCardPrefab, stackRoot);
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

    internal void ShowAttackRange(int overlapCount = 1)
    {
        overlapCount = Mathf.Max(0, overlapCount);
        if (overlapCount == 0)
            return;

        EnsureAttackRangeOverlay();
        for (int index = 0; index < overlapCount; index++)
            _attackRangeHitDurations.Add(AttackRangeDisplayDuration);
        RefreshAttackRangeIndicator();
    }

    internal void SetManualSelectionState(bool candidate, bool selected)
    {
        EnsureManualSelectionOverlay();
        if (_manualSelectionOverlay == null)
            return;

        _manualSelectionOverlay.enabled = candidate;
        if (!candidate)
            return;

        _manualSelectionOverlay.color = selected
            ? new Color(1f, 0.78f, 0.12f, 0.42f)
            : new Color(0.2f, 0.9f, 0.5f, 0.28f);
        _manualSelectionOverlay.rectTransform.SetAsLastSibling();
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
            Destroy(topCard.gameObject);
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
                Destroy(card.gameObject);
            }
        }

        _enemies.Clear();
        _cards.Clear();
        ClearAttackRangeIndicator();
        SetManualSelectionState(false, false);
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
        if (enemy != null && ReferenceEquals(enemy, TopEnemy))
            NotifyEnemyClicked(enemy);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null &&
            eventData.button == PointerEventData.InputButton.Left &&
            TopEnemy != null)
        {
            NotifyEnemyClicked(TopEnemy);
        }
    }

    private void NotifyEnemyClicked(EnemyRuntime enemy)
    {
        EnemyClicked?.Invoke(enemy);
    }

    private void EnsureAttackRangeOverlay()
    {
        if (_attackRangeOverlay != null)
            return;

        Transform existing = transform.Find("imgAttackRangeOverlay");
        if (existing != null)
            _attackRangeOverlay = existing.GetComponent<Image>();
        if (_attackRangeOverlay == null)
        {
            GameObject overlayObject = new(
                "imgAttackRangeOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            overlayObject.transform.SetParent(transform, false);
            _attackRangeOverlay = overlayObject.GetComponent<Image>();
        }

        RectTransform overlayRect = _attackRangeOverlay.rectTransform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.localScale = Vector3.one;
        overlayRect.SetAsLastSibling();
        _attackRangeOverlay.raycastTarget = false;
        _attackRangeOverlay.enabled = false;
    }

    private void EnsureManualSelectionOverlay()
    {
        if (_manualSelectionOverlay != null)
            return;

        Transform existing = transform.Find("imgManualSelectionOverlay");
        if (existing != null)
            _manualSelectionOverlay = existing.GetComponent<Image>();
        if (_manualSelectionOverlay == null)
        {
            GameObject overlayObject = new(
                "imgManualSelectionOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            overlayObject.transform.SetParent(transform, false);
            _manualSelectionOverlay = overlayObject.GetComponent<Image>();
        }

        RectTransform overlayRect = _manualSelectionOverlay.rectTransform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.localScale = Vector3.one;
        overlayRect.SetAsLastSibling();
        _manualSelectionOverlay.raycastTarget = false;
        _manualSelectionOverlay.enabled = false;
    }

    private void RefreshAttackRangeIndicator()
    {
        if (_attackRangeOverlay == null)
            return;

        int overlapCount = _attackRangeHitDurations.Count;
        if (overlapCount <= 0)
        {
            _attackRangeOverlay.enabled = false;
            return;
        }

        int alpha = Mathf.Min(
            AttackRangeMaximumAlpha,
            AttackRangeMinimumAlpha +
            (Mathf.Min(overlapCount, 4) - 1) * AttackRangeAlphaStep);
        _attackRangeOverlay.color = new Color32(255, 0, 0, (byte)alpha);
        _attackRangeOverlay.enabled = true;
        _attackRangeOverlay.rectTransform.SetAsLastSibling();
    }

    private void ClearAttackRangeIndicator()
    {
        _attackRangeHitDurations.Clear();
        if (_attackRangeOverlay != null)
            _attackRangeOverlay.enabled = false;
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
