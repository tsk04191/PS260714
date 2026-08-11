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

public sealed class BattleCardDeckRuntime : IBattleCardDrawService
{
    private readonly List<BattleCardInstance> allCards = new();
    private readonly List<BattleCardInstance> drawPile = new();
    private readonly List<BattleCardInstance> hand = new();
    private readonly List<BattleCardInstance> discardPile = new();
    private readonly List<BattleCardInstance> exhaustPile = new();
    private BattleCardDeckRules rules = new();
    private System.Random random;
    private long nextInstanceId = 1;
    private int cardsDrawnPerTurn;
    private float automaticDrawCooldown;
    private float mulliganDrawCooldown;
    private bool mulliganPending;

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
        CooldownRemaining = 0f;
        CooldownDuration = 0f;
        mulliganPending = false;
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
        return IsReady && ContainsInHand(card) &&
               card.Definition != null;
    }

    public bool CompleteSuccessfulPlay(BattleCardInstance card)
    {
        if (!CanPlay(card) || !hand.Remove(card))
            return false;

        if (card.Definition.RecyclePolicy == BattleCardRecyclePolicy.Exhaust)
            exhaustPile.Add(card);
        else
            discardPile.Add(card);
        Changed?.Invoke();
        return true;
    }

    public int TryDrawCards(int count)
    {
        if (!IsReady || count <= 0)
            return 0;

        int drawn = DrawCards(Mathf.Max(0, count));
        if (drawn > 0)
            Changed?.Invoke();
        return drawn;
    }

    public bool TryMulligan()
    {
        if (!IsReady || hand.Count == 0)
            return false;
        DiscardCurrentHand();
        mulliganPending = true;
        Phase = BattleCardDeckPhase.RedrawCooldown;
        StartCooldown(mulliganDrawCooldown);
        Changed?.Invoke();
        return true;
    }

    public void Tick(float deltaTime)
    {
        if ((Phase != BattleCardDeckPhase.Ready &&
             Phase != BattleCardDeckPhase.RedrawCooldown) ||
            deltaTime <= 0f || CooldownRemaining <= 0f)
        {
            return;
        }
        CooldownRemaining = Mathf.Max(0f, CooldownRemaining - deltaTime);
        if (CooldownRemaining > 0f)
            return;

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
        random = null;
        CooldownRemaining = 0f;
        CooldownDuration = 0f;
        cardsDrawnPerTurn = 0;
        automaticDrawCooldown = 0f;
        mulliganDrawCooldown = 0f;
        mulliganPending = false;
        Phase = BattleCardDeckPhase.Inactive;
        if (notify)
            Changed?.Invoke();
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
