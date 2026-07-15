using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(Image))]
public sealed class DungeonTileView : MonoBehaviour
{
    [SerializeField] private Image slotSurface;
    [SerializeField] private RectTransform stackRoot;
    [SerializeField] private DungeonEnemyCard enemyCardPrefab;

    private readonly List<DungeonEnemyCard> _cards = new();
    private int _maximumStackSize;
    private float _currentCellSize;

    public int Row { get; private set; }
    public int Column { get; private set; }
    public int StackCount => _cards.Count;
    public DungeonEnemyData TopEnemy =>
        _cards.Count > 0 ? _cards[^1].Enemy : null;
    public int TopEnemyHealth => TopEnemy != null ? TopEnemy.Health : 0;
    public bool TopEnemyHasFire =>
        TopEnemy != null && TopEnemy.HasFire;
    public bool IsFull => _cards.Count >= _maximumStackSize;

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

    public bool TryAdd(int health)
    {
        return TryAdd(new DungeonEnemyData(health));
    }

    internal bool TryAdd(DungeonEnemyData enemy)
    {
        if (IsFull || stackRoot == null || enemyCardPrefab == null || enemy == null)
            return false;

        DungeonEnemyCard card = Instantiate(enemyCardPrefab, stackRoot);
        card.name = $"grpEnemyCard_{_cards.Count + 1}";
        card.Setup(enemy);
        _cards.Add(card);

        RefreshCardPositions();
        return true;
    }

    public bool TrySetTopEnemyHealth(int health)
    {
        if (_cards.Count == 0)
            return false;

        DungeonEnemyCard topCard = _cards[^1];
        topCard.Enemy.SetHealth(health);
        topCard.RefreshHealth();
        return true;
    }

    internal int TryDamageTop(int damage)
    {
        if (_cards.Count == 0 || damage <= 0)
            return 0;

        DungeonEnemyCard topCard = _cards[^1];
        int appliedDamage = topCard.Enemy.TakeDamage(damage);
        if (appliedDamage <= 0)
            return 0;

        if (topCard.Enemy.Health <= 0)
            TryRemoveTop();
        else
            topCard.RefreshHealth();

        return appliedDamage;
    }

    internal int TryHealTop(int amount)
    {
        if (_cards.Count == 0 || amount <= 0)
            return 0;

        DungeonEnemyCard topCard = _cards[^1];
        int healedAmount = topCard.Enemy.Heal(amount);
        if (healedAmount > 0)
            topCard.RefreshHealth();

        return healedAmount;
    }

    internal bool TryApplyFireToTop(
        IBattleCharacter source,
        float duration,
        float tickInterval,
        int tickDamage)
    {
        if (_cards.Count == 0)
            return false;

        DungeonEnemyCard topCard = _cards[^1];
        topCard.Enemy.ApplyFire(duration, tickInterval, tickDamage, source);
        topCard.RefreshStatus();
        return true;
    }

    internal void TickStatusEffects(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        bool removedCard = false;
        for (int index = _cards.Count - 1; index >= 0; index--)
        {
            DungeonEnemyCard card = _cards[index];
            if (card == null || card.Enemy == null)
                continue;

            bool hadFire = card.Enemy.HasFire;
            int damage = card.Enemy.TickFire(
                deltaTime,
                out IBattleCharacter source);
            int appliedDamage = card.Enemy.TakeDamage(damage);
            if (appliedDamage > 0)
                source?.RecordDamageDealt(appliedDamage);

            if (card.Enemy.Health <= 0)
            {
                _cards.RemoveAt(index);
                Destroy(card.gameObject);
                removedCard = true;
                continue;
            }

            if (appliedDamage > 0 || hadFire != card.Enemy.HasFire)
                card.RefreshHealth();
        }

        if (removedCard)
            RefreshCardPositions();
    }

    public bool TryRemoveTop()
    {
        if (_cards.Count == 0)
            return false;

        int topIndex = _cards.Count - 1;
        DungeonEnemyCard topCard = _cards[topIndex];
        _cards.RemoveAt(topIndex);

        if (topCard != null)
            Destroy(topCard.gameObject);

        RefreshCardPositions();
        return true;
    }

    public void ClearStack()
    {
        foreach (DungeonEnemyCard card in _cards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }

        _cards.Clear();
    }

    internal List<DungeonEnemyData> CopyEnemyData()
    {
        List<DungeonEnemyData> result = new(_cards.Count);
        foreach (DungeonEnemyCard card in _cards)
        {
            if (card != null && card.Enemy != null)
                result.Add(card.Enemy);
        }

        return result;
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
            DungeonEnemyCard card = _cards[index];
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
