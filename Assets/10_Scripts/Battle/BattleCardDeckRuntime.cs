using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleCardDeckPhase
{
    Inactive = 0,
    Ready = 1,
    RedrawCooldown = 2,
    Empty = 3,
}

public enum BattleCardZone
{
    DrawPile = 0,
    Hand = 1,
    DiscardPile = 2,
    ExhaustPile = 3,
}

public sealed class BattleCardZoneSelectionState
{
    private readonly BattleCardInstance[] candidates;
    private readonly List<BattleCardInstance> selected = new();

    public BattleCardZone Zone { get; }
    public int MinimumCount { get; }
    public int MaximumCount { get; }
    public IReadOnlyList<BattleCardInstance> Candidates => candidates;
    public IReadOnlyList<BattleCardInstance> Selected => selected;
    public bool CanConfirm => selected.Count >= MinimumCount &&
                              selected.Count <= MaximumCount;

    internal BattleCardZoneSelectionState(
        BattleCardZone zone,
        int minimumCount,
        int maximumCount,
        IReadOnlyList<BattleCardInstance> candidates)
    {
        Zone = zone;
        MinimumCount = Mathf.Max(0, minimumCount);
        MaximumCount = Mathf.Max(MinimumCount, maximumCount);
        this.candidates = candidates != null
            ? CopyCandidates(candidates)
            : Array.Empty<BattleCardInstance>();
    }

    internal bool TryToggle(BattleCardInstance card)
    {
        if (card == null || Array.IndexOf(candidates, card) < 0)
            return false;
        if (selected.Remove(card))
            return true;
        if (selected.Count >= MaximumCount)
            return false;

        selected.Add(card);
        return true;
    }

    internal IReadOnlyList<BattleCardInstance> SnapshotSelected()
    {
        return selected.ToArray();
    }

    private static BattleCardInstance[] CopyCandidates(
        IReadOnlyList<BattleCardInstance> source)
    {
        BattleCardInstance[] result =
            new BattleCardInstance[source.Count];
        for (int index = 0; index < source.Count; index++)
            result[index] = source[index];
        return result;
    }
}

public sealed class BattleCardInstance
{
    public long InstanceId { get; }
    public BattleCardSO Definition { get; }

    internal BattleCardInstance(long instanceId, BattleCardSO definition)
    {
        InstanceId = instanceId;
        Definition = definition;
    }
}

public sealed class BattleCardDeckRuntime :
    IBattleCardDrawService,
    IBattleCardControlService
{
    private sealed class CostModifierRuntime
    {
        public BattleCardCostModifierMode Mode { get; }
        public int Value { get; }
        public int RemainingSuccessfulPlays { get; set; }
        public float RemainingDuration { get; set; }
        public long ExcludedInstanceId { get; set; }
        public bool IsTimed { get; }

        public CostModifierRuntime(
            BattleCardCostModifierMode mode,
            int value,
            int remainingSuccessfulPlays,
            BattleCardInstance excludedCard)
        {
            Mode = mode;
            Value = value;
            RemainingSuccessfulPlays = Mathf.Max(
                1,
                remainingSuccessfulPlays);
            RemainingDuration = 0f;
            ExcludedInstanceId = excludedCard?.InstanceId ?? 0L;
            IsTimed = false;
        }

        public CostModifierRuntime(
            BattleCardCostModifierMode mode,
            int value,
            float duration)
        {
            Mode = mode;
            Value = value;
            RemainingSuccessfulPlays = 0;
            RemainingDuration = TimePrecision.Normalize(duration);
            ExcludedInstanceId = 0L;
            IsTimed = true;
        }

        public bool IsExcluded(BattleCardInstance card)
        {
            return card != null && ExcludedInstanceId != 0L &&
                   card.InstanceId == ExcludedInstanceId;
        }
    }

    private readonly List<BattleCardInstance> allCards = new();
    private readonly List<BattleCardInstance> drawPile = new();
    private readonly List<BattleCardInstance> hand = new();
    private readonly List<BattleCardInstance> discardPile = new();
    private readonly List<BattleCardInstance> exhaustPile = new();
    private readonly List<CostModifierRuntime> costModifiers = new();
    private readonly Dictionary<long, float> cardLocks = new();
    private BattleCardDeckRules rules = new();
    private System.Random random;
    private long nextInstanceId = 1;
    private int cardsDrawnPerTurn;
    private float automaticDrawCooldown;
    private float mulliganDrawCooldown;
    private bool mulliganPending;
    private bool automaticRedrawSkipPending;
    private BattleCardZoneSelectionState currentSelection;

    public event Action Changed;

    public BattleCardDeckPhase Phase { get; private set; } =
        BattleCardDeckPhase.Inactive;
    public float CooldownRemaining { get; private set; }
    public float CooldownDuration { get; private set; }
    public IReadOnlyList<BattleCardInstance> Hand => hand;
    public IReadOnlyList<BattleCardInstance> DrawPile => drawPile;
    public IReadOnlyList<BattleCardInstance> DiscardPile => discardPile;
    public IReadOnlyList<BattleCardInstance> ExhaustPile => exhaustPile;
    public int CardsDrawnPerTurn => cardsDrawnPerTurn;
    public float AutomaticDrawCooldown => automaticDrawCooldown;
    public bool IsReady => Phase == BattleCardDeckPhase.Ready;
    public bool IsZoneSelectionPending => currentSelection != null;
    public BattleCardZoneSelectionState CurrentSelection => currentSelection;
    public int ActiveCostModifierCount => costModifiers.Count;
    public int ActiveLockedCardCount => cardLocks.Count;
    public int ActiveTimedCostModifierCount
    {
        get
        {
            int count = 0;
            for (int index = 0; index < costModifiers.Count; index++)
            {
                if (costModifiers[index]?.IsTimed == true)
                    count++;
            }
            return count;
        }
    }
    public bool AutomaticRedrawSkipPending => automaticRedrawSkipPending;

    public bool Configure(
        BattleCardDeckRules deckRules,
        IReadOnlyList<CharacterSO> party,
        int seed)
    {
        BattleCardDeckRules resolvedRules =
            deckRules ?? new BattleCardDeckRules();
        List<BattleCardSO> definitions = new();
        resolvedRules.BuildDeck(party, definitions);
        int partyJudgment = 0;
        int partyKnowledge = 0;
        if (party != null)
        {
            for (int index = 0; index < party.Count; index++)
            {
                CharacterSO character = party[index];
                if (character == null)
                    continue;

                long next = (long)partyJudgment + character.Judgment;
                partyJudgment = next >= int.MaxValue
                    ? int.MaxValue
                    : (int)next;

                next = (long)partyKnowledge + character.Knowledge;
                partyKnowledge = next >= int.MaxValue
                    ? int.MaxValue
                    : (int)next;
            }
        }
        return ConfigureResolvedDeck(
            resolvedRules,
            definitions,
            seed,
            resolvedRules.ResolveCardsDrawnPerTurn(partyJudgment),
            resolvedRules.ResolveRedrawCooldown(partyKnowledge),
            resolvedRules.ResolveMulliganCooldown(partyKnowledge));
    }

    public bool ConfigureResolvedDeck(
        BattleCardDeckRules deckRules,
        IReadOnlyList<BattleCardSO> definitions,
        int seed,
        int resolvedCardsDrawnPerTurn = -1,
        float resolvedRedrawCooldown = -1f,
        float resolvedMulliganCooldown = -1f)
    {
        Clear(false);
        rules = deckRules ?? new BattleCardDeckRules();
        cardsDrawnPerTurn = resolvedCardsDrawnPerTurn > 0
            ? resolvedCardsDrawnPerTurn
            : rules.ResolveCardsDrawnPerTurn(0);
        automaticDrawCooldown = resolvedRedrawCooldown > 0f
            ? TimePrecision.Normalize(
                resolvedRedrawCooldown,
                TimePrecision.Step)
            : rules.ResolveRedrawCooldown(0);
        mulliganDrawCooldown = resolvedMulliganCooldown > 0f
            ? TimePrecision.Normalize(
                resolvedMulliganCooldown,
                TimePrecision.Step)
            : rules.ResolveMulliganCooldown(0);
        definitions ??= Array.Empty<BattleCardSO>();
        if (definitions.Count == 0)
        {
            Phase = BattleCardDeckPhase.Empty;
            Changed?.Invoke();
            return false;
        }

        random = new System.Random(seed);
        foreach (BattleCardSO definition in definitions)
        {
            if (definition != null)
            {
                allCards.Add(new BattleCardInstance(
                    nextInstanceId++,
                    definition));
            }
        }
        Phase = allCards.Count > 0
            ? BattleCardDeckPhase.Inactive
            : BattleCardDeckPhase.Empty;
        Changed?.Invoke();
        return allCards.Count > 0;
    }

    public bool BeginBattle()
    {
        drawPile.Clear();
        hand.Clear();
        discardPile.Clear();
        exhaustPile.Clear();
        costModifiers.Clear();
        cardLocks.Clear();
        CooldownRemaining = 0f;
        CooldownDuration = 0f;
        mulliganPending = false;
        automaticRedrawSkipPending = false;
        currentSelection = null;
        if (allCards.Count == 0)
        {
            Phase = BattleCardDeckPhase.Empty;
            Changed?.Invoke();
            return false;
        }

        drawPile.AddRange(allCards);
        Shuffle(drawPile);
        DrawCards();
        if (hand.Count > 0)
        {
            Phase = BattleCardDeckPhase.Ready;
            StartCooldown(automaticDrawCooldown);
        }
        else
        {
            Phase = BattleCardDeckPhase.Empty;
        }
        Changed?.Invoke();
        return hand.Count > 0;
    }

    public bool ContainsInHand(BattleCardInstance card)
    {
        return card != null && hand.Contains(card);
    }

    public bool CanPlay(BattleCardInstance card)
    {
        return IsReady && !IsZoneSelectionPending &&
               ContainsInHand(card) &&
               card.Definition != null &&
               !IsCardLocked(card);
    }

    public bool IsCardLocked(BattleCardInstance card)
    {
        return card != null && cardLocks.TryGetValue(
            card.InstanceId,
            out float remaining) && remaining > 0f;
    }

    public bool TryLockCard(BattleCardInstance card, float duration)
    {
        duration = TimePrecision.Normalize(duration);
        if (!IsReady || IsZoneSelectionPending ||
            !ContainsInHand(card) || duration <= 0f ||
            float.IsNaN(duration) || float.IsInfinity(duration))
        {
            return false;
        }

        if (cardLocks.TryGetValue(card.InstanceId, out float remaining))
            duration = Mathf.Max(duration, remaining);
        cardLocks[card.InstanceId] = duration;
        Changed?.Invoke();
        return true;
    }

    public bool TryLockRandomHandCard(float duration)
    {
        duration = TimePrecision.Normalize(duration);
        if (!IsReady || IsZoneSelectionPending || hand.Count == 0 ||
            duration <= 0f || float.IsNaN(duration) ||
            float.IsInfinity(duration))
        {
            return false;
        }

        List<BattleCardInstance> candidates = new();
        for (int index = 0; index < hand.Count; index++)
        {
            BattleCardInstance card = hand[index];
            if (card != null && !IsCardLocked(card))
                candidates.Add(card);
        }
        if (candidates.Count == 0)
            return false;

        random ??= new System.Random(0);
        BattleCardInstance selected = candidates[
            random.Next(candidates.Count)];
        cardLocks[selected.InstanceId] = duration;
        Changed?.Invoke();
        return true;
    }

    public int GetEffectiveCost(BattleCardInstance card)
    {
        if (card?.Definition == null)
            return 0;

        long cost = card.Definition.EnergyCost;
        foreach (CostModifierRuntime modifier in costModifiers)
        {
            if (modifier == null ||
                (!modifier.IsTimed &&
                 modifier.RemainingSuccessfulPlays <= 0) ||
                (modifier.IsTimed && modifier.RemainingDuration <= 0f) ||
                modifier.IsExcluded(card))
            {
                continue;
            }

            cost = modifier.Mode == BattleCardCostModifierMode.Set
                ? Math.Max(0L, modifier.Value)
                : Math.Max(0L, cost + modifier.Value);
            if (cost >= int.MaxValue)
                return int.MaxValue;
        }
        return (int)Math.Max(0L, cost);
    }

    public bool TryAddCostModifier(
        BattleCardCostModifierMode mode,
        int value,
        int successfulPlayCount,
        BattleCardInstance excludedCard = null)
    {
        if (!IsReady || IsZoneSelectionPending ||
            !Enum.IsDefined(typeof(BattleCardCostModifierMode), mode) ||
            successfulPlayCount <= 0 ||
            (excludedCard != null && !ContainsInHand(excludedCard)))
        {
            return false;
        }

        costModifiers.Add(new CostModifierRuntime(
            mode,
            value,
            successfulPlayCount,
            excludedCard));
        Changed?.Invoke();
        return true;
    }

    public bool TryAddTimedCostModifier(
        BattleCardCostModifierMode mode,
        int value,
        float duration)
    {
        duration = TimePrecision.Normalize(duration);
        if (!IsReady || IsZoneSelectionPending ||
            !Enum.IsDefined(typeof(BattleCardCostModifierMode), mode) ||
            duration <= 0f || float.IsNaN(duration) ||
            float.IsInfinity(duration))
        {
            return false;
        }

        costModifiers.Add(new CostModifierRuntime(
            mode,
            value,
            duration));
        Changed?.Invoke();
        return true;
    }

    public bool CompleteSuccessfulPlay(BattleCardInstance card)
    {
        if (!CanPlay(card) || !hand.Remove(card))
            return false;

        if (card.Definition.RecyclePolicy == BattleCardRecyclePolicy.Exhaust)
            exhaustPile.Add(card);
        else
            discardPile.Add(card);
        ConsumeCostModifiers(card);
        Changed?.Invoke();
        return true;
    }

    public int TryDrawCards(int count)
    {
        if (!IsReady || IsZoneSelectionPending || count <= 0)
            return 0;

        int drawn = DrawCards(Mathf.Max(0, count));
        if (drawn > 0)
            Changed?.Invoke();
        return drawn;
    }

    public bool TryMulligan()
    {
        if (!IsReady || IsZoneSelectionPending || hand.Count == 0)
            return false;
        DiscardCurrentHand();
        mulliganPending = true;
        Phase = BattleCardDeckPhase.RedrawCooldown;
        StartCooldown(mulliganDrawCooldown);
        Changed?.Invoke();
        return true;
    }

    public IReadOnlyList<BattleCardInstance> GetZoneCards(
        BattleCardZone zone)
    {
        return zone switch
        {
            BattleCardZone.DrawPile => drawPile,
            BattleCardZone.Hand => hand,
            BattleCardZone.DiscardPile => discardPile,
            BattleCardZone.ExhaustPile => exhaustPile,
            _ => Array.Empty<BattleCardInstance>(),
        };
    }

    public bool TryCreateCardInZone(
        BattleCardSO definition,
        BattleCardZone zone,
        out BattleCardInstance instance)
    {
        instance = null;
        if (definition == null || IsZoneSelectionPending ||
            Phase == BattleCardDeckPhase.Inactive ||
            !Enum.IsDefined(typeof(BattleCardZone), zone))
        {
            return false;
        }

        BattleCardInstance created = new(
            nextInstanceId++,
            definition);
        allCards.Add(created);
        switch (zone)
        {
            case BattleCardZone.DrawPile:
                drawPile.Add(created);
                break;
            case BattleCardZone.Hand:
                hand.Add(created);
                break;
            case BattleCardZone.DiscardPile:
                discardPile.Add(created);
                break;
            case BattleCardZone.ExhaustPile:
                exhaustPile.Add(created);
                break;
        }

        if (Phase == BattleCardDeckPhase.Empty &&
            zone != BattleCardZone.ExhaustPile)
        {
            Phase = BattleCardDeckPhase.Ready;
            StartCooldown(Mathf.Max(
                TimePrecision.Step,
                automaticDrawCooldown));
        }

        instance = created;
        Changed?.Invoke();
        return true;
    }

    public bool TryBeginZoneSelection(
        BattleCardZone zone,
        int minimumCount,
        int maximumCount,
        BattleCardInstance excludedCard = null,
        IReadOnlyList<BattleCardInstance> candidates = null)
    {
        if (!IsReady || IsZoneSelectionPending ||
            !Enum.IsDefined(typeof(BattleCardZone), zone) ||
            minimumCount < 0 || maximumCount < minimumCount)
        {
            return false;
        }

        IReadOnlyList<BattleCardInstance> source = GetZoneCards(zone);
        List<BattleCardInstance> resolvedCandidates = new();
        HashSet<BattleCardInstance> unique = new();
        if (candidates == null)
        {
            for (int index = 0; index < source.Count; index++)
            {
                BattleCardInstance candidate = source[index];
                if (candidate != null &&
                    !ReferenceEquals(candidate, excludedCard) &&
                    unique.Add(candidate))
                {
                    resolvedCandidates.Add(candidate);
                }
            }
        }
        else
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                BattleCardInstance candidate = candidates[index];
                if (candidate != null && ContainsReference(source, candidate) &&
                    !ReferenceEquals(candidate, excludedCard) &&
                    unique.Add(candidate))
                {
                    resolvedCandidates.Add(candidate);
                }
            }
        }

        if (resolvedCandidates.Count < minimumCount)
            return false;

        currentSelection = new BattleCardZoneSelectionState(
            zone,
            minimumCount,
            Mathf.Min(maximumCount, resolvedCandidates.Count),
            resolvedCandidates);
        Changed?.Invoke();
        return true;
    }

    public bool TryToggleZoneSelection(BattleCardInstance card)
    {
        if (currentSelection?.TryToggle(card) != true)
            return false;
        Changed?.Invoke();
        return true;
    }

    public bool TryConfirmZoneSelection(
        out IReadOnlyList<BattleCardInstance> selectedCards)
    {
        selectedCards = Array.Empty<BattleCardInstance>();
        if (currentSelection?.CanConfirm != true)
            return false;

        selectedCards = currentSelection.SnapshotSelected();
        currentSelection = null;
        Changed?.Invoke();
        return true;
    }

    public bool CancelZoneSelection()
    {
        if (currentSelection == null)
            return false;
        currentSelection = null;
        Changed?.Invoke();
        return true;
    }

    public int TryDiscardSelectedHandCards(
        IReadOnlyList<BattleCardInstance> cards,
        BattleCardInstance resolvingCard = null)
    {
        return MoveSelectedHandCards(
            cards,
            resolvingCard,
            discardPile);
    }

    public int TryExhaustSelectedHandCards(
        IReadOnlyList<BattleCardInstance> cards,
        BattleCardInstance resolvingCard = null)
    {
        return MoveSelectedHandCards(
            cards,
            resolvingCard,
            exhaustPile);
    }

    public bool TryMoveDiscardCardToHand(BattleCardInstance card)
    {
        if (!IsReady || IsZoneSelectionPending || card == null ||
            !discardPile.Remove(card))
        {
            return false;
        }

        hand.Add(card);
        Changed?.Invoke();
        return true;
    }

    public int DiscardEntireHand(BattleCardInstance resolvingCard = null)
    {
        if (!IsReady || IsZoneSelectionPending || hand.Count == 0)
            return 0;

        int discarded = 0;
        for (int index = hand.Count - 1; index >= 0; index--)
        {
            BattleCardInstance card = hand[index];
            if (ReferenceEquals(card, resolvingCard))
                continue;

            hand.RemoveAt(index);
            discardPile.Add(card);
            discarded++;
        }
        if (discarded > 0)
            Changed?.Invoke();
        return discarded;
    }

    public bool TryShuffleDrawPile()
    {
        if (!IsReady || IsZoneSelectionPending || drawPile.Count < 2)
            return false;
        Shuffle(drawPile);
        Changed?.Invoke();
        return true;
    }

    public bool TryShuffleDiscardIntoDrawPile()
    {
        if (!IsReady || IsZoneSelectionPending || discardPile.Count == 0)
            return false;

        drawPile.AddRange(discardPile);
        discardPile.Clear();
        Shuffle(drawPile);
        Changed?.Invoke();
        return true;
    }

    public bool TryCombineAndShuffleDrawAndDiscardPiles()
    {
        if (!IsReady || IsZoneSelectionPending ||
            (drawPile.Count == 0 && discardPile.Count == 0))
        {
            return false;
        }

        if (discardPile.Count > 0)
        {
            drawPile.AddRange(discardPile);
            discardPile.Clear();
        }
        Shuffle(drawPile);
        Changed?.Invoke();
        return true;
    }

    public bool TrySkipNextAutomaticRedraw()
    {
        if (!IsReady || IsZoneSelectionPending ||
            automaticRedrawSkipPending)
        {
            return false;
        }

        automaticRedrawSkipPending = true;
        Changed?.Invoke();
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f || float.IsNaN(deltaTime) ||
            float.IsInfinity(deltaTime))
        {
            return;
        }

        bool controlStateChanged = TickCardControlState(deltaTime);
        if (IsZoneSelectionPending ||
            (Phase != BattleCardDeckPhase.Ready &&
             Phase != BattleCardDeckPhase.RedrawCooldown) ||
            CooldownRemaining <= 0f)
        {
            if (controlStateChanged)
                Changed?.Invoke();
            return;
        }
        CooldownRemaining = Mathf.Max(0f, CooldownRemaining - deltaTime);
        if (CooldownRemaining > 0f)
        {
            if (controlStateChanged)
                Changed?.Invoke();
            return;
        }

        if (!mulliganPending && automaticRedrawSkipPending)
        {
            automaticRedrawSkipPending = false;
            StartCooldown(automaticDrawCooldown);
            Changed?.Invoke();
            return;
        }

        if (!mulliganPending)
            DiscardCurrentHand();
        mulliganPending = false;
        DrawCards();
        if (hand.Count > 0)
        {
            Phase = BattleCardDeckPhase.Ready;
            StartCooldown(automaticDrawCooldown);
        }
        else
        {
            Phase = BattleCardDeckPhase.Empty;
            CooldownRemaining = 0f;
            CooldownDuration = 0f;
        }
        Changed?.Invoke();
    }

    public void Clear(bool notify = true)
    {
        allCards.Clear();
        drawPile.Clear();
        hand.Clear();
        discardPile.Clear();
        exhaustPile.Clear();
        costModifiers.Clear();
        cardLocks.Clear();
        random = null;
        CooldownRemaining = 0f;
        CooldownDuration = 0f;
        cardsDrawnPerTurn = 0;
        automaticDrawCooldown = 0f;
        mulliganDrawCooldown = 0f;
        mulliganPending = false;
        automaticRedrawSkipPending = false;
        currentSelection = null;
        Phase = BattleCardDeckPhase.Inactive;
        if (notify)
            Changed?.Invoke();
    }

    private int MoveSelectedHandCards(
        IReadOnlyList<BattleCardInstance> cards,
        BattleCardInstance resolvingCard,
        List<BattleCardInstance> destination)
    {
        if (!IsReady || IsZoneSelectionPending || cards == null ||
            cards.Count == 0 || destination == null)
        {
            return 0;
        }

        int moved = 0;
        HashSet<BattleCardInstance> unique = new();
        for (int index = 0; index < cards.Count; index++)
        {
            BattleCardInstance card = cards[index];
            if (card == null || ReferenceEquals(card, resolvingCard) ||
                !unique.Add(card) || !hand.Remove(card))
            {
                continue;
            }

            destination.Add(card);
            moved++;
        }
        if (moved > 0)
            Changed?.Invoke();
        return moved;
    }

    private void ConsumeCostModifiers(BattleCardInstance card)
    {
        for (int index = costModifiers.Count - 1; index >= 0; index--)
        {
            CostModifierRuntime modifier = costModifiers[index];
            if (modifier == null)
            {
                costModifiers.RemoveAt(index);
                continue;
            }

            if (modifier.IsTimed)
                continue;

            if (modifier.IsExcluded(card))
            {
                modifier.ExcludedInstanceId = 0L;
                continue;
            }

            modifier.RemainingSuccessfulPlays--;
            if (modifier.RemainingSuccessfulPlays <= 0)
                costModifiers.RemoveAt(index);
        }
    }

    private bool TickCardControlState(float deltaTime)
    {
        bool changed = false;
        if (cardLocks.Count > 0)
        {
            List<long> expired = null;
            List<long> keys = new(cardLocks.Keys);
            for (int index = 0; index < keys.Count; index++)
            {
                long instanceId = keys[index];
                float remaining = Mathf.Max(
                    0f,
                    cardLocks[instanceId] - deltaTime);
                if (remaining > 0f)
                {
                    cardLocks[instanceId] = remaining;
                    continue;
                }

                expired ??= new List<long>();
                expired.Add(instanceId);
            }

            if (expired != null)
            {
                for (int index = 0; index < expired.Count; index++)
                    cardLocks.Remove(expired[index]);
                changed = true;
            }
        }

        for (int index = costModifiers.Count - 1; index >= 0; index--)
        {
            CostModifierRuntime modifier = costModifiers[index];
            if (modifier == null)
            {
                costModifiers.RemoveAt(index);
                changed = true;
                continue;
            }
            if (!modifier.IsTimed)
                continue;

            modifier.RemainingDuration = Mathf.Max(
                0f,
                modifier.RemainingDuration - deltaTime);
            if (modifier.RemainingDuration > 0f)
                continue;

            costModifiers.RemoveAt(index);
            changed = true;
        }
        return changed;
    }

    private static bool ContainsReference(
        IReadOnlyList<BattleCardInstance> cards,
        BattleCardInstance target)
    {
        if (cards == null || target == null)
            return false;
        for (int index = 0; index < cards.Count; index++)
        {
            if (ReferenceEquals(cards[index], target))
                return true;
        }
        return false;
    }

    private void StartCooldown(float duration)
    {
        CooldownDuration = TimePrecision.Normalize(
            duration,
            TimePrecision.Step);
        CooldownRemaining = CooldownDuration;
    }

    private void DiscardCurrentHand()
    {
        discardPile.AddRange(hand);
        hand.Clear();
    }

    private void DrawCards()
    {
        DrawCards(Mathf.Max(0, cardsDrawnPerTurn - hand.Count));
    }

    private int DrawCards(int count)
    {
        int drawn = 0;
        while (drawn < count)
        {
            if (drawPile.Count == 0)
            {
                if (discardPile.Count == 0)
                    break;
                drawPile.AddRange(discardPile);
                discardPile.Clear();
                Shuffle(drawPile);
            }

            int last = drawPile.Count - 1;
            BattleCardInstance card = drawPile[last];
            drawPile.RemoveAt(last);
            hand.Add(card);
            drawn++;
        }
        return drawn;
    }

    private void Shuffle(List<BattleCardInstance> cards)
    {
        random ??= new System.Random(Environment.TickCount);
        for (int index = cards.Count - 1; index > 0; index--)
        {
            int swap = random.Next(index + 1);
            (cards[index], cards[swap]) = (cards[swap], cards[index]);
        }
    }
}
