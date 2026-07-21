using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(Image))]
public sealed class DungeonTileView : MonoBehaviour, IPointerClickHandler
{
    private const byte TargetEffectAlpha = 155;
    private const float AttackRangeDisplayDuration = 0.5f;
    private const byte AttackRangeMinimumAlpha = 100;
    private const byte AttackRangeAlphaStep = 50;
    private const byte AttackRangeMaximumAlpha = 250;
    private const string BasicAimStateName = "BasicAimIn";
    private const string BasicFireStateName = "BasicFire";
    private const string BasicHiddenStateName = "BasicHidden";
    private const string FireStatusLoopStateName = "FireStatusLoop";
    private const string FireStatusHiddenStateName = "FireStatusHidden";

    private static readonly float[] FireStatusAnimationOffsets =
    {
        0f,
        0.15f,
        0.3f,
    };

    [SerializeField] private Image slotSurface;
    [SerializeField] private RectTransform stackRoot;
    [SerializeField] private EnemyCard enemyCardPrefab;

    [Header("Basic Target Effect")]
    [SerializeField] private Image[] basicTargetEffectImages =
        new Image[DungeonPage.MaximumPartySize];
    [SerializeField] private Animator[] basicTargetEffectAnimators =
        new Animator[DungeonPage.MaximumPartySize];

    [Header("Fire Status Effect")]
    [SerializeField] private Sprite fireStatusSprite;
    [SerializeField] private Image[] fireStatusImages = new Image[3];
    [SerializeField] private Animator[] fireStatusAnimators = new Animator[3];

    private readonly List<EnemyRuntime> _enemies = new();
    private readonly List<EnemyCard> _cards = new();
    private readonly List<float> _attackRangeHitDurations = new();
    private Image _attackRangeOverlay;
    private int _maximumStackSize;
    private float _currentCellSize;
    private EnemyRuntime _displayedFireEnemy;

    public int Row { get; private set; }
    public int Column { get; private set; }
    public int StackCount => _enemies.Count;
    public EnemyRuntime TopEnemy =>
        _enemies.Count > 0 ? _enemies[^1] : null;
    public int TopEnemyHealth => TopEnemy != null ? TopEnemy.Health : 0;
    public bool TopEnemyHasFire =>
        TopEnemy != null && TopEnemy.HasFire;
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
        InitializeBasicTargetEffects();
        InitializeFireStatusEffects();
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
        RefreshFireStatusEffect();
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
        RefreshFireStatusEffect();
        return true;
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

    internal void PlayBasicTargetAim(IBattleCharacter source)
    {
        if (!TryGetBasicTargetEffect(
                source,
                out Image image,
                out Animator animator))
        {
            return;
        }

        image.color = GetTargetEffectColor(source.EffectColor);
        image.sprite = source.TargetEffectSprite;
        image.enabled = true;
        animator.Play(BasicAimStateName, 0, 0f);
    }

    internal void PlayBasicTargetFire(IBattleCharacter source)
    {
        if (!TryGetBasicTargetEffect(
                source,
                out Image image,
                out Animator animator))
        {
            return;
        }

        image.color = GetTargetEffectColor(source.EffectColor);
        image.sprite = source.TargetEffectSprite;
        image.enabled = true;
        animator.Play(BasicFireStateName, 0, 0f);
    }

    internal void HideBasicTargetEffect(IBattleCharacter source)
    {
        if (!TryGetBasicTargetEffect(
                source,
                out _,
                out Animator animator))
        {
            return;
        }

        if (animator.isActiveAndEnabled)
            animator.Play(BasicHiddenStateName, 0, 0f);
        else
        {
            CanvasGroup canvasGroup = animator.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
        }
    }

    internal void TickStatusEffects(
        float deltaTime,
        Func<DungeonTileView, int, int> applyDamage)
    {
        if (deltaTime <= 0f || _enemies.Count == 0 || applyDamage == null)
            return;

        EnemyRuntime burningEnemy = TopEnemy;
        bool hadFire = burningEnemy.HasFire;
        burningEnemy.TickFire(deltaTime, (damage, source) =>
        {
            if (!ReferenceEquals(TopEnemy, burningEnemy))
                return false;

            int appliedDamage = applyDamage(this, damage);
            if (appliedDamage > 0)
                source?.RecordDamageDealt(appliedDamage);
            return ReferenceEquals(TopEnemy, burningEnemy);
        });

        if (!ReferenceEquals(TopEnemy, burningEnemy))
        {
            RefreshFireStatusEffect();
            return;
        }

        if (hadFire != burningEnemy.HasFire)
            _cards[^1]?.RefreshHealth();
        RefreshFireStatusEffect();
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
        RefreshFireStatusEffect();
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
        HideAllBasicTargetEffects();
        HideFireStatusEffect();
    }

    internal List<EnemyRuntime> CopyEnemyRuntimes()
    {
        return new List<EnemyRuntime>(_enemies);
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

    private void InitializeBasicTargetEffects()
    {
        int effectCount = Mathf.Min(
            basicTargetEffectImages?.Length ?? 0,
            basicTargetEffectAnimators?.Length ?? 0);
        for (int index = 0; index < effectCount; index++)
        {
            Image image = basicTargetEffectImages[index];
            Animator animator = basicTargetEffectAnimators[index];
            if (image != null)
            {
                image.raycastTarget = false;
            }

            if (animator != null && animator.runtimeAnimatorController != null &&
                animator.isActiveAndEnabled)
                animator.Play(BasicHiddenStateName, 0, 0f);
        }
    }

    private void InitializeFireStatusEffects()
    {
        int effectCount = Mathf.Min(
            fireStatusImages?.Length ?? 0,
            fireStatusAnimators?.Length ?? 0);
        for (int index = 0; index < effectCount; index++)
        {
            Image image = fireStatusImages[index];
            Animator animator = fireStatusAnimators[index];
            if (image != null)
            {
                image.raycastTarget = false;
                if (fireStatusSprite != null)
                    image.sprite = fireStatusSprite;
                image.color = BattleStatusColors.Fire;
            }

            if (animator == null || animator.runtimeAnimatorController == null)
                continue;

            if (animator.isActiveAndEnabled)
                animator.Play(FireStatusHiddenStateName, 0, 0f);
            else
                SetAnimatorCanvasAlpha(animator, 0f);
        }

        _displayedFireEnemy = null;
        RefreshFireStatusEffect();
    }

    private void RefreshFireStatusEffect()
    {
        EnemyRuntime fireEnemy = TopEnemy != null && TopEnemy.HasFire
            ? TopEnemy
            : null;
        if (fireEnemy == null)
        {
            if (_displayedFireEnemy != null)
                HideFireStatusEffect();
            return;
        }

        bool shouldRestart = !ReferenceEquals(_displayedFireEnemy, fireEnemy);
        _displayedFireEnemy = fireEnemy;
        int effectCount = Mathf.Min(
            fireStatusImages?.Length ?? 0,
            fireStatusAnimators?.Length ?? 0);
        for (int index = 0; index < effectCount; index++)
        {
            Image image = fireStatusImages[index];
            Animator animator = fireStatusAnimators[index];
            if (image != null)
            {
                Sprite displaySprite = fireStatusSprite != null
                    ? fireStatusSprite
                    : fireEnemy.FireStatusSprite;
                if (displaySprite != null)
                    image.sprite = displaySprite;
                image.color = BattleStatusColors.Fire;
                image.enabled = true;
            }

            if (!shouldRestart || animator == null ||
                animator.runtimeAnimatorController == null ||
                !animator.isActiveAndEnabled)
            {
                continue;
            }

            float offset = index < FireStatusAnimationOffsets.Length
                ? FireStatusAnimationOffsets[index]
                : 0f;
            animator.Play(FireStatusLoopStateName, 0, offset);
        }
    }

    private void HideFireStatusEffect()
    {
        _displayedFireEnemy = null;
        if (fireStatusAnimators == null)
            return;

        foreach (Animator animator in fireStatusAnimators)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                continue;

            if (animator.isActiveAndEnabled)
                animator.Play(FireStatusHiddenStateName, 0, 0f);
            else
                SetAnimatorCanvasAlpha(animator, 0f);
        }
    }

    private static void SetAnimatorCanvasAlpha(Animator animator, float alpha)
    {
        CanvasGroup canvasGroup = animator != null
            ? animator.GetComponent<CanvasGroup>()
            : null;
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;
    }

    private static Color32 GetTargetEffectColor(Color sourceColor)
    {
        Color32 color = sourceColor;
        color.a = TargetEffectAlpha;
        return color;
    }

    private bool TryGetBasicTargetEffect(
        IBattleCharacter source,
        out Image image,
        out Animator animator)
    {
        image = null;
        animator = null;
        if (source == null || source.PartySlotIndex < 0 ||
            basicTargetEffectImages == null ||
            basicTargetEffectAnimators == null ||
            source.PartySlotIndex >= basicTargetEffectImages.Length ||
            source.PartySlotIndex >= basicTargetEffectAnimators.Length)
        {
            return false;
        }

        image = basicTargetEffectImages[source.PartySlotIndex];
        animator = basicTargetEffectAnimators[source.PartySlotIndex];
        return image != null && animator != null &&
               animator.runtimeAnimatorController != null;
    }

    private void HideAllBasicTargetEffects()
    {
        if (basicTargetEffectAnimators == null)
            return;

        foreach (Animator animator in basicTargetEffectAnimators)
        {
            if (animator != null && animator.runtimeAnimatorController != null &&
                animator.isActiveAndEnabled)
                animator.Play(BasicHiddenStateName, 0, 0f);
        }
    }
}
