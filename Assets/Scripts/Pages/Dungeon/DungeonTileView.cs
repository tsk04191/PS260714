using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(Image))]
public sealed class DungeonTileView : MonoBehaviour
{
    [SerializeField] private Image slotSurface;
    [SerializeField] private RectTransform stackRoot;
    [SerializeField] private EnemyCard enemyCardPrefab;

    private readonly List<EnemyRuntime> _enemies = new();
    private readonly List<EnemyCard> _cards = new();
    private int _maximumStackSize;
    private float _currentCellSize;

    public int Row { get; private set; }
    public int Column { get; private set; }
    public int StackCount => _enemies.Count;
    public EnemyRuntime TopEnemy =>
        _enemies.Count > 0 ? _enemies[^1] : null;
    public int TopEnemyHealth => TopEnemy != null ? TopEnemy.Health : 0;
    public bool TopEnemyHasFire =>
        TopEnemy != null && TopEnemy.HasFire;
    public bool IsFull => _enemies.Count >= _maximumStackSize;
    internal bool CanAddEnemy =>
        !IsFull && stackRoot != null && enemyCardPrefab != null;

    public void Initialize(int row, int column, int stackSize)
    {
        Row = row;
        Column = column;
        _maximumStackSize = Mathf.Max(1, stackSize);

        if (slotSurface != null)
        {
            slotSurface.color = (row + column) % 2 == 0
                ? new Color(0.075f, 0.105f, 0.09f, 1f)
                : new Color(0.09f, 0.12f, 0.105f, 1f);
        }
    }

    internal bool TryAdd(EnemyRuntime enemy)
    {
        if (!CanAddEnemy || enemy == null)
            return false;

        EnemyCard card = Instantiate(enemyCardPrefab, stackRoot);
        card.name = $"grpEnemyCard_{_cards.Count + 1}";
        card.Bind(enemy);
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
        if (_enemies.Count == 0 || damage <= 0)
            return 0;

        EnemyRuntime topEnemy = TopEnemy;
        int appliedDamage = topEnemy.TakeDamage(damage);
        if (appliedDamage <= 0)
            return 0;

        if (topEnemy.Health <= 0)
            TryRemoveTop();
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

    internal bool TryApplyFireToTop(
        IBattleCharacter source,
        float duration,
        float tickInterval,
        int tickDamage)
    {
        if (_enemies.Count == 0)
            return false;

        TopEnemy.ApplyFire(duration, tickInterval, tickDamage, source);
        _cards[^1]?.RefreshStatus();
        return true;
    }

    internal void TickStatusEffects(
        float deltaTime,
        Func<DungeonTileView, int, int> applyDamage)
    {
        if (deltaTime <= 0f || _enemies.Count == 0 || applyDamage == null)
            return;

        EnemyRuntime burningEnemy = TopEnemy;
        bool hadFire = burningEnemy.HasFire;
        int damage = burningEnemy.TickFire(
            deltaTime,
            out IBattleCharacter source);
        int appliedDamage = applyDamage(this, damage);
        if (appliedDamage > 0)
            source?.RecordDamageDealt(appliedDamage);

        if (!ReferenceEquals(TopEnemy, burningEnemy))
            return;

        if (hadFire != burningEnemy.HasFire)
            _cards[^1]?.RefreshHealth();
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
            Destroy(topCard.gameObject);

        RefreshCardPositions();
        return true;
    }

    public void ClearStack()
    {
        foreach (EnemyCard card in _cards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }

        _enemies.Clear();
        _cards.Clear();
    }

    internal List<EnemyRuntime> CopyEnemyRuntimes()
    {
        return new List<EnemyRuntime>(_enemies);
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
