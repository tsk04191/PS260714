using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using static TestReflection;

public sealed class BattleCardControlServiceTests
{
    private readonly List<Object> created = new();
    private int nextCardId;

    [TearDown]
    public void TearDown()
    {
        for (int index = 0; index < created.Count; index++)
        {
            if (created[index] != null)
                Object.DestroyImmediate(created[index]);
        }
        created.Clear();
    }

    [Test]
    public void CardLock_BlocksPlayUntilDurationExpires()
    {
        BattleCardDeckRuntime deck = Deck(Card(1), Card(2));
        BattleCardInstance locked = deck.Hand[0];

        Assert.That(deck.TryLockCard(locked, 4f), Is.True);
        Assert.That(deck.IsCardLocked(locked), Is.True);
        Assert.That(deck.CanPlay(locked), Is.False);
        Assert.That(deck.ActiveLockedCardCount, Is.EqualTo(1));

        deck.Tick(3.9f);
        Assert.That(deck.IsCardLocked(locked), Is.True);
        deck.Tick(0.1f);

        Assert.That(deck.IsCardLocked(locked), Is.False);
        Assert.That(deck.CanPlay(locked), Is.True);
        Assert.That(deck.ActiveLockedCardCount, Is.Zero);
    }

    [Test]
    public void RandomHandLock_SelectsOnlyAnUnlockedCard()
    {
        BattleCardDeckRuntime deck = Deck(Card(1), Card(1));

        Assert.That(deck.TryLockRandomHandCard(2f), Is.True);
        Assert.That(deck.ActiveLockedCardCount, Is.EqualTo(1));
        Assert.That(deck.TryLockRandomHandCard(2f), Is.True);
        Assert.That(deck.ActiveLockedCardCount, Is.EqualTo(2));
        Assert.That(deck.TryLockRandomHandCard(2f), Is.False);
    }

    [Test]
    public void TimedCostTax_IsNotConsumedByPlayAndExpiresByTime()
    {
        BattleCardDeckRuntime deck = Deck(Card(2), Card(3));
        BattleCardInstance first = deck.Hand[0];
        BattleCardInstance second = deck.Hand[1];

        Assert.That(
            deck.TryAddTimedCostModifier(
                BattleCardCostModifierMode.Add,
                1,
                5f),
            Is.True);
        Assert.That(deck.GetEffectiveCost(first), Is.EqualTo(3));
        Assert.That(deck.GetEffectiveCost(second), Is.EqualTo(4));
        Assert.That(deck.CompleteSuccessfulPlay(first), Is.True);
        Assert.That(deck.ActiveTimedCostModifierCount, Is.EqualTo(1));
        Assert.That(deck.GetEffectiveCost(second), Is.EqualTo(4));

        deck.Tick(5f);

        Assert.That(deck.ActiveTimedCostModifierCount, Is.Zero);
        Assert.That(deck.GetEffectiveCost(second), Is.EqualTo(3));
    }

    [Test]
    public void InvalidControlDurations_DoNotChangeDeck()
    {
        BattleCardDeckRuntime deck = Deck(Card(1));
        BattleCardInstance card = deck.Hand[0];

        Assert.That(deck.TryLockCard(card, 0f), Is.False);
        Assert.That(deck.TryLockCard(card, float.NaN), Is.False);
        Assert.That(
            deck.TryAddTimedCostModifier(
                BattleCardCostModifierMode.Add,
                1,
                float.PositiveInfinity),
            Is.False);
        Assert.That(deck.ActiveLockedCardCount, Is.Zero);
        Assert.That(deck.ActiveTimedCostModifierCount, Is.Zero);
    }

    private BattleCardDeckRuntime Deck(params BattleCardSO[] cards)
    {
        BattleCardDeckRuntime deck = new();
        Assert.That(
            deck.ConfigureResolvedDeck(
                new BattleCardDeckRules(),
                cards,
                260714,
                cards.Length,
                100f,
                100f),
            Is.True);
        Assert.That(deck.BeginBattle(), Is.True);
        Assert.That(deck.Hand.Count, Is.EqualTo(cards.Length));
        return deck;
    }

    private BattleCardSO Card(int cost)
    {
        BattleCardSO card = ScriptableObject.CreateInstance<BattleCardSO>();
        created.Add(card);
        SetField(card, "cardId", $"test.enemy.control.{nextCardId++}");
        SetField(card, "energyCost", cost);
        return card;
    }
}
