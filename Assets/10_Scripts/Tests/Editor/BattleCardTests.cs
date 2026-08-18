using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PS260714.Localization;
using UnityEditor;
using UnityEngine;
using static TestReflection;

public sealed class BattleCardTests
{
    private readonly List<Object> created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (Object instance in created)
        {
            if (instance != null)
                Object.DestroyImmediate(instance);
        }
        created.Clear();
        BattleCardCatalog.Invalidate();
    }

    [Test]
    public void DeckRules_DefaultAutomaticRedrawCooldownIsTenSeconds()
    {
        BattleCardDeckRules rules = new();

        Assert.That(rules.RedrawCooldown, Is.EqualTo(10f));
    }

    [Test]
    public void AutomaticCooldown_StartsFull_AllowsPlay_ThenReplacesHand()
    {
        BattleCardDeckRules rules = CreateRules(
            baseDrawCount: 5,
            redrawCooldown: 2f);
        List<BattleCardSO> definitions = CreateCards(10);
        BattleCardDeckRuntime deck = new();

        Assert.That(
            deck.ConfigureResolvedDeck(rules, definitions, 1234),
            Is.True);
        Assert.That(deck.BeginBattle(), Is.True);
        Assert.That(deck.Hand, Has.Count.EqualTo(5));
        Assert.That(deck.Phase, Is.EqualTo(BattleCardDeckPhase.Ready));
        Assert.That(deck.CooldownRemaining, Is.EqualTo(2f).Within(0.001f));
        List<BattleCardInstance> openingHand = new(deck.Hand);

        BattleCardInstance played = deck.Hand[0];
        Assert.That(deck.CompleteSuccessfulPlay(played), Is.True);
        Assert.That(deck.Phase, Is.EqualTo(BattleCardDeckPhase.Ready));
        Assert.That(deck.Hand, Has.Count.EqualTo(4));
        Assert.That(deck.DiscardPile, Has.Count.EqualTo(1));
        Assert.That(deck.CooldownRemaining, Is.EqualTo(2f).Within(0.001f));

        deck.Tick(1f);
        Assert.That(deck.Phase, Is.EqualTo(BattleCardDeckPhase.Ready));
        Assert.That(deck.CooldownRemaining, Is.EqualTo(1f).Within(0.001f));
        Assert.That(deck.Hand, Has.Count.EqualTo(4));

        deck.Tick(1f);
        Assert.That(deck.Phase, Is.EqualTo(BattleCardDeckPhase.Ready));
        Assert.That(deck.Hand, Has.Count.EqualTo(5));
        Assert.That(deck.CooldownRemaining, Is.EqualTo(2f).Within(0.001f));
        Assert.That(deck.ExhaustPile, Is.Empty);
        foreach (BattleCardInstance previous in openingHand)
        {
            foreach (BattleCardInstance current in deck.Hand)
                Assert.That(current, Is.Not.SameAs(previous));
        }
    }

    [Test]
    public void ExhaustCard_DoesNotReturnToFutureHands()
    {
        BattleCardDeckRules rules = CreateRules(
            baseDrawCount: 5,
            redrawCooldown: 0.25f);
        List<BattleCardSO> definitions = CreateCards(5);
        SetEnum(
            definitions[0],
            "recyclePolicy",
            (int)BattleCardRecyclePolicy.Exhaust);
        BattleCardDeckRuntime deck = new();
        deck.ConfigureResolvedDeck(rules, definitions, 44);
        deck.BeginBattle();
        BattleCardInstance exhausted = null;
        foreach (BattleCardInstance instance in deck.Hand)
        {
            if (ReferenceEquals(instance.Definition, definitions[0]))
            {
                exhausted = instance;
                break;
            }
        }

        Assert.That(exhausted, Is.Not.Null);
        Assert.That(deck.CompleteSuccessfulPlay(exhausted), Is.True);
        deck.Tick(0.25f);

        Assert.That(deck.ExhaustPile, Has.Count.EqualTo(1));
        Assert.That(deck.Hand, Has.Count.EqualTo(4));
        foreach (BattleCardInstance instance in deck.Hand)
            Assert.That(instance, Is.Not.SameAs(exhausted));
    }

    [Test]
    public void CardDrawEffect_AddsRequestedCardsToCurrentHand()
    {
        BattleCardDeckRules rules = CreateRules(
            baseDrawCount: 2,
            redrawCooldown: 2f);
        List<BattleCardSO> definitions = CreateCards(6);
        BattleCardSO drawCard = definitions[0];
        SerializedObject data = new(drawCard);
        SerializedProperty effect = data.FindProperty("abilityEffects")
            .GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.CardDraw;
        effect.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        effect.FindPropertyRelative("damageAmount").floatValue = 2f;
        data.ApplyModifiedPropertiesWithoutUndo();

        BattleCardDeckRuntime deck = new();
        Assert.That(
            deck.ConfigureResolvedDeck(rules, definitions, 44),
            Is.True);
        Assert.That(deck.BeginBattle(), Is.True);
        Assert.That(deck.Hand, Has.Count.EqualTo(2));

        BattleEffectContext context = BattleEffectContext.ForBattleCard(
            null,
            null,
            null,
            CharacterTargetFaction.Enemy,
            null,
            null,
            0f,
            deck);
        BattleEffectResult result = BattleEffectExecutor.ExecuteAbility(
            context,
            drawCard);

        Assert.That(result.Attempted, Is.True);
        Assert.That(result.Succeeded, Is.True);
        Assert.That(deck.Hand, Has.Count.EqualTo(4));
        Assert.That(
            drawCard.AbilityEffects[0].RequiresActionTargets,
            Is.False);
    }

    [Test]
    public void SharedExecutor_MultipleResourceCostsAbortWithoutPartialSpend()
    {
        TestActiveSkillResource resource = new(5);
        CharacterEffectDefinition first = CreateEffect(
            CharacterEffectType.SpendResource,
            4f);
        CharacterEffectDefinition second = CreateEffect(
            CharacterEffectType.SpendResource,
            4f);
        BattleEffectContext context = BattleEffectContext.ForBattleCard(
            null,
            null,
            resource,
            CharacterTargetFaction.Enemy,
            null,
            null);

        BattleEffectResult result = BattleEffectExecutor.ExecuteSequence(
            context,
            new IBattleEffectDefinition[] { first, second });

        Assert.That(result.Attempted, Is.False);
        Assert.That(resource.Current, Is.EqualTo(5));
    }

    [Test]
    public void Mulligan_DiscardsWholeHand_AndUsesOwnCooldown()
    {
        BattleCardDeckRules rules = CreateRules(
            baseDrawCount: 3,
            redrawCooldown: 2f,
            mulliganCooldown: 0.5f);
        BattleCardDeckRuntime deck = new();
        deck.ConfigureResolvedDeck(rules, CreateCards(6), 17);
        deck.BeginBattle();

        Assert.That(deck.TryMulligan(), Is.True);
        Assert.That(deck.Hand, Is.Empty);
        Assert.That(deck.DiscardPile, Has.Count.EqualTo(3));
        Assert.That(deck.CooldownRemaining, Is.EqualTo(0.5f).Within(0.001f));

        deck.Tick(0.5f);
        Assert.That(deck.Phase, Is.EqualTo(BattleCardDeckPhase.Ready));
        Assert.That(deck.Hand, Has.Count.EqualTo(3));
        Assert.That(deck.CooldownRemaining, Is.EqualTo(2f).Within(0.001f));
    }

    [Test]
    public void Judgment_AddsToDungeonBaseDrawCountEveryTurn()
    {
        BattleCardDeckRules rules = CreateRules(
            baseDrawCount: 3,
            redrawCooldown: 0.25f);
        CharacterSO first = Create<CharacterSO>();
        CharacterSO second = Create<CharacterSO>();
        SetSerializedInt(first, "judgment", 2);
        SetSerializedInt(second, "judgment", 1);
        CharacterData firstData = first.CreateData();
        CharacterData secondData = second.CreateData();
        int partyJudgment = firstData.Judgment + secondData.Judgment;
        int drawCount = rules.ResolveCardsDrawnPerTurn(partyJudgment);
        BattleCardDeckRuntime deck = new();

        Assert.That(drawCount, Is.EqualTo(6));
        Assert.That(
            deck.ConfigureResolvedDeck(
                rules,
                CreateCards(10),
                260714,
                drawCount),
            Is.True);
        Assert.That(deck.BeginBattle(), Is.True);
        Assert.That(deck.CardsDrawnPerTurn, Is.EqualTo(6));
        Assert.That(deck.Hand, Has.Count.EqualTo(6));

        BattleCardInstance played = deck.Hand[0];
        Assert.That(deck.CompleteSuccessfulPlay(played), Is.True);
        deck.Tick(0.25f);
        Assert.That(deck.Hand, Has.Count.EqualTo(6));
    }

    [Test]
    public void Knowledge_AcceleratesAutomaticAndMulliganDrawCooldowns()
    {
        BattleCardDeckRules rules = CreateRules(
            baseDrawCount: 3,
            redrawCooldown: 2f,
            mulliganCooldown: 1.2f);
        CharacterSO character = Create<CharacterSO>();
        SetSerializedInt(character, "knowledge", 3);
        CharacterData data = character.CreateData();
        float automaticCooldown =
            rules.ResolveRedrawCooldown(data.Knowledge);
        float mulliganCooldown =
            rules.ResolveMulliganCooldown(data.Knowledge);
        BattleCardDeckRuntime deck = new();

        Assert.That(automaticCooldown, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(mulliganCooldown, Is.EqualTo(0.3f).Within(0.001f));
        deck.ConfigureResolvedDeck(
            rules,
            CreateCards(6),
            84,
            3,
            automaticCooldown,
            mulliganCooldown);
        deck.BeginBattle();
        Assert.That(deck.CooldownRemaining, Is.EqualTo(0.5f).Within(0.001f));

        deck.Tick(0.4f);
        Assert.That(deck.Hand, Has.Count.EqualTo(3));
        Assert.That(deck.CooldownRemaining, Is.EqualTo(0.1f).Within(0.001f));
        Assert.That(deck.TryMulligan(), Is.True);
        Assert.That(deck.CooldownRemaining, Is.EqualTo(0.3f).Within(0.001f));
        deck.Tick(0.3f);
        Assert.That(deck.Hand, Has.Count.EqualTo(3));
        Assert.That(deck.CooldownRemaining, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void AutomaticDraw_RefillsDeckByShufflingDiscardPile()
    {
        BattleCardDeckRules rules = CreateRules(
            baseDrawCount: 3,
            redrawCooldown: 0.2f);
        BattleCardDeckRuntime deck = new();
        deck.ConfigureResolvedDeck(rules, CreateCards(3), 71);
        deck.BeginBattle();

        Assert.That(deck.Hand, Has.Count.EqualTo(3));
        Assert.That(deck.DrawPile, Is.Empty);
        deck.Tick(0.2f);

        Assert.That(deck.Hand, Has.Count.EqualTo(3));
        Assert.That(deck.DrawPile, Is.Empty);
        Assert.That(deck.DiscardPile, Is.Empty);
        Assert.That(deck.CooldownRemaining, Is.EqualTo(0.2f).Within(0.001f));
    }

    [Test]
    public void ZoneSelection_ExcludesResolvingCard_AndPausesDeck()
    {
        BattleCardDeckRuntime deck = new();
        deck.ConfigureResolvedDeck(
            CreateRules(3, 2f),
            CreateCards(6),
            101);
        deck.BeginBattle();
        BattleCardInstance resolving = deck.Hand[0];
        float cooldown = deck.CooldownRemaining;

        Assert.That(
            deck.TryBeginZoneSelection(
                BattleCardZone.Hand,
                1,
                2,
                resolving),
            Is.True);
        Assert.That(deck.IsZoneSelectionPending, Is.True);
        Assert.That(deck.CurrentSelection.Candidates.Count, Is.EqualTo(2));
        foreach (BattleCardInstance candidate in
                 deck.CurrentSelection.Candidates)
        {
            Assert.That(candidate, Is.Not.SameAs(resolving));
        }
        Assert.That(deck.CanPlay(resolving), Is.False);

        deck.Tick(1f);

        Assert.That(deck.CooldownRemaining, Is.EqualTo(cooldown));
        Assert.That(deck.TryDrawCards(1), Is.Zero);

        BattleCardInstance selected = deck.CurrentSelection.Candidates[0];
        Assert.That(deck.TryToggleZoneSelection(selected), Is.True);
        Assert.That(deck.CurrentSelection.CanConfirm, Is.True);
        Assert.That(
            deck.TryConfirmZoneSelection(out
                IReadOnlyList<BattleCardInstance> selection),
            Is.True);
        Assert.That(selection.Count, Is.EqualTo(1));
        Assert.That(selection[0], Is.SameAs(selected));
        Assert.That(deck.IsZoneSelectionPending, Is.False);
        Assert.That(deck.CanPlay(resolving), Is.True);
    }

    [Test]
    public void SelectedHandMoves_IgnoreDuplicatesAndResolvingCard()
    {
        BattleCardDeckRuntime deck = new();
        deck.ConfigureResolvedDeck(
            CreateRules(5, 2f),
            CreateCards(8),
            102);
        deck.BeginBattle();
        BattleCardInstance resolving = deck.Hand[0];
        BattleCardInstance discarded = deck.Hand[1];
        BattleCardInstance exhausted = deck.Hand[2];

        Assert.That(
            deck.TryDiscardSelectedHandCards(
                new[] { discarded, discarded, resolving },
                resolving),
            Is.EqualTo(1));
        Assert.That(deck.DiscardPile, Has.Member(discarded));
        Assert.That(deck.Hand, Has.Member(resolving));
        Assert.That(
            deck.TryExhaustSelectedHandCards(
                new[] { exhausted, resolving },
                resolving),
            Is.EqualTo(1));
        Assert.That(deck.ExhaustPile, Has.Member(exhausted));

        Assert.That(deck.TryMoveDiscardCardToHand(discarded), Is.True);
        Assert.That(deck.Hand, Has.Member(discarded));
        Assert.That(deck.DiscardPile, Has.No.Member(discarded));

        int expectedDiscarded = deck.Hand.Count - 1;
        Assert.That(
            deck.DiscardEntireHand(resolving),
            Is.EqualTo(expectedDiscarded));
        Assert.That(deck.Hand, Has.Count.EqualTo(1));
        Assert.That(deck.Hand[0], Is.SameAs(resolving));
        Assert.That(deck.ExhaustPile, Has.Member(exhausted));
    }

    [Test]
    public void ForcedShuffle_MergesDiscardIntoExistingDrawPile()
    {
        BattleCardDeckRuntime deck = new();
        deck.ConfigureResolvedDeck(
            CreateRules(3, 2f),
            CreateCards(8),
            103);
        deck.BeginBattle();
        BattleCardInstance discarded = deck.Hand[0];
        int drawCount = deck.DrawPile.Count;

        Assert.That(
            deck.TryDiscardSelectedHandCards(new[] { discarded }),
            Is.EqualTo(1));
        Assert.That(deck.DiscardPile, Has.Count.EqualTo(1));
        Assert.That(deck.TryShuffleDiscardIntoDrawPile(), Is.True);
        Assert.That(deck.DiscardPile, Is.Empty);
        Assert.That(deck.DrawPile, Has.Count.EqualTo(drawCount + 1));
        Assert.That(deck.DrawPile, Has.Member(discarded));
        Assert.That(deck.TryShuffleDrawPile(), Is.True);
    }

    [Test]
    public void CostModifiers_ApplyInOrder_AndConsumeOnSuccessOnly()
    {
        List<BattleCardSO> definitions = CreateCards(6);
        foreach (BattleCardSO definition in definitions)
            SetSerializedInt(definition, "energyCost", 3);
        BattleCardDeckRuntime deck = new();
        deck.ConfigureResolvedDeck(
            CreateRules(6, 2f),
            definitions,
            104);
        deck.BeginBattle();
        BattleCardInstance source = deck.Hand[0];

        Assert.That(
            deck.TryAddCostModifier(
                BattleCardCostModifierMode.Add,
                -1,
                3,
                source),
            Is.True);
        Assert.That(deck.GetEffectiveCost(source), Is.EqualTo(3));
        Assert.That(deck.CompleteSuccessfulPlay(source), Is.True);
        Assert.That(deck.ActiveCostModifierCount, Is.EqualTo(1));

        BattleCardInstance next = deck.Hand[0];
        Assert.That(deck.GetEffectiveCost(next), Is.EqualTo(2));
        Assert.That(
            deck.TryAddCostModifier(
                BattleCardCostModifierMode.Set,
                0,
                1),
            Is.True);
        Assert.That(deck.GetEffectiveCost(next), Is.Zero);
        Assert.That(deck.CompleteSuccessfulPlay(source), Is.False);
        Assert.That(deck.ActiveCostModifierCount, Is.EqualTo(2));

        Assert.That(deck.CompleteSuccessfulPlay(next), Is.True);
        Assert.That(deck.ActiveCostModifierCount, Is.EqualTo(1));
        Assert.That(deck.GetEffectiveCost(deck.Hand[0]), Is.EqualTo(2));
        Assert.That(deck.CompleteSuccessfulPlay(deck.Hand[0]), Is.True);
        Assert.That(deck.GetEffectiveCost(deck.Hand[0]), Is.EqualTo(2));
        Assert.That(deck.CompleteSuccessfulPlay(deck.Hand[0]), Is.True);
        Assert.That(deck.ActiveCostModifierCount, Is.Zero);
        Assert.That(deck.GetEffectiveCost(deck.Hand[0]), Is.EqualTo(3));
    }

    [Test]
    public void AutomaticRedrawSkip_IsOneShot_AndDoesNotSkipMulligan()
    {
        BattleCardDeckRuntime deck = new();
        deck.ConfigureResolvedDeck(
            CreateRules(3, 0.5f, 0.2f),
            CreateCards(9),
            105);
        deck.BeginBattle();
        List<BattleCardInstance> openingHand = new(deck.Hand);

        Assert.That(deck.TrySkipNextAutomaticRedraw(), Is.True);
        Assert.That(deck.TrySkipNextAutomaticRedraw(), Is.False);
        deck.Tick(0.5f);

        Assert.That(deck.AutomaticRedrawSkipPending, Is.False);
        Assert.That(deck.Hand, Is.EqualTo(openingHand));
        Assert.That(deck.DiscardPile, Is.Empty);

        deck.Tick(0.5f);

        Assert.That(deck.Hand, Is.Not.EqualTo(openingHand));
        Assert.That(deck.DiscardPile, Has.Count.EqualTo(3));

        Assert.That(deck.TrySkipNextAutomaticRedraw(), Is.True);
        Assert.That(deck.TryMulligan(), Is.True);
        deck.Tick(0.2f);
        Assert.That(deck.AutomaticRedrawSkipPending, Is.True);
        List<BattleCardInstance> postMulligan = new(deck.Hand);

        deck.Tick(0.5f);

        Assert.That(deck.AutomaticRedrawSkipPending, Is.False);
        Assert.That(deck.Hand, Is.EqualTo(postMulligan));
    }

    [Test]
    public void Affiliation_RestrictsCardsToConfiguredParty()
    {
        CharacterSO first = Create<CharacterSO>();
        CharacterSO second = Create<CharacterSO>();
        BattleCardSO exclusive = CreateCard("card.exclusive");
        SerializedObject exclusiveData = new(exclusive);
        exclusiveData.FindProperty("affiliation").enumValueIndex =
            (int)BattleCardAffiliation.CharacterExclusive;
        exclusiveData.FindProperty("ownerCharacter").objectReferenceValue =
            first;
        exclusiveData.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(exclusive.IsEligible(new[] { first }), Is.True);
        Assert.That(exclusive.IsEligible(new[] { second }), Is.False);

        BattleCardSO dependent = CreateCard("card.dependent");
        SerializedObject dependentData = new(dependent);
        dependentData.FindProperty("affiliation").enumValueIndex =
            (int)BattleCardAffiliation.CharacterDependent;
        dependentData.FindProperty("requirementMode").enumValueIndex =
            (int)BattleCardRequirementMatchMode.All;
        SerializedProperty requirements =
            dependentData.FindProperty("requiredCharacters");
        requirements.arraySize = 2;
        requirements.GetArrayElementAtIndex(0).objectReferenceValue = first;
        requirements.GetArrayElementAtIndex(1).objectReferenceValue = second;
        dependentData.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(dependent.IsEligible(new[] { first }), Is.False);
        Assert.That(dependent.IsEligible(new[] { first, second }), Is.True);
    }

    [Test]
    public void DirectCard_UsesCurrentSharedAbilitySchema()
    {
        BattleCardSO card = CreateCard("card.direct");

        Assert.That(card.AbilitySchemaVersion, Is.EqualTo(1));
        Assert.That(card.UsesLegacyEffectStorage, Is.False);
        Assert.That(card.Targeting.IsValid, Is.True);
        Assert.That(card.HasExecutableContent, Is.True);
        Assert.That(
            AbilityDefinitionValidator.TryValidate(card, out string error),
            Is.True,
            error);
    }

    [TestCase(CharacterEffectType.GainResource)]
    [TestCase(CharacterEffectType.SpendResource)]
    [TestCase(CharacterEffectType.SpendHealth)]
    [TestCase(CharacterEffectType.CardDraw)]
    public void SharedTargetlessEffects_DoNotRequireActionTarget(
        CharacterEffectType effectType)
    {
        BattleCardSO card = CreateCard($"card.targetless.{effectType}");
        SerializedObject data = new(card);
        SerializedProperty effect = data.FindProperty("abilityEffects")
            .GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)effectType;
        effect.FindPropertyRelative("targetMode").enumValueIndex =
            (int)CharacterEffectTargetMode.InheritAction;
        data.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(
            BattleEffectRules.RequiresTargets(
                (BattleEffectType)(int)effectType),
            Is.False);
        Assert.That(card.RequiresActionTargets, Is.False);
    }

    [Test]
    public void SharedLocalizationArguments_ExposeEffectAndAreaValues()
    {
        BattleCardSO card = CreateCard("card.localization.arguments");
        SerializedObject data = new(card);
        SerializedProperty area = data.FindProperty("areaDefinition");
        area.FindPropertyRelative("shapeType").enumValueIndex =
            (int)CharacterAreaShapeType.CircleSector;
        area.FindPropertyRelative("radius").floatValue = 2.75f;
        SerializedProperty effect = data.FindProperty("abilityEffects")
            .GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.CardDraw;
        effect.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        effect.FindPropertyRelative("damageAmount").floatValue = 3f;
        data.ApplyModifiedPropertiesWithoutUndo();

        Dictionary<string, object> values = new();
        foreach (LocalizationArgument argument in
                 BattleAbilityLocalizationArguments.Build(card))
        {
            values[argument.Name] = argument.Value;
        }

        Assert.That(values["radius"], Is.EqualTo(2.75f));
        Assert.That(values["drawCount"], Is.EqualTo(3f));
        Assert.That(values["amount"], Is.EqualTo(3f));
    }

    [Test]
    public void CircularCardArea_UsesClickedWorldPointWithoutRecentering()
    {
        BattleAreaDefinition area = new();
        SetField(
            area,
            "shapeType",
            CharacterAreaShapeType.CircleSector);
        SetField(area, "maxCastDistance", 1f);
        Vector2 clicked = new(7f, -3f);

        Vector2 resolved = BattleAreaGeometry.ResolveManualOrigin(
            clicked,
            Vector2.zero,
            area,
            2f,
            BattleManualAreaPlacementMode.FreePointer);
        Vector2 abilityConstrained =
            BattleAreaGeometry.ResolveManualOrigin(
                clicked,
                Vector2.zero,
                area,
                2f,
                BattleManualAreaPlacementMode.AbilityConstrained);

        Assert.That(resolved, Is.EqualTo(clicked));
        Assert.That(abilityConstrained.magnitude, Is.EqualTo(1f)
            .Within(0.0001f));
    }

    [Test]
    public void TargetArea_IgnoresUnusedWorldGeometryValues()
    {
        BattleAreaDefinition area = new();
        SetField(area, "radius", 0f);
        SetField(area, "angle", float.NaN);
        SetField(area, "maxCastDistance", 0f);

        Assert.That(area.ShapeType,
            Is.EqualTo(CharacterAreaShapeType.Target));
        Assert.That(area.IsValid, Is.True);
    }

    [Test]
    public void DesignatedWorldArea_RequiresValidWorldGeometryValues()
    {
        BattleAreaDefinition area = new();
        SetField(
            area,
            "shapeType",
            CharacterAreaShapeType.CircleSector);
        SetField(
            area,
            "originMode",
            CharacterAreaOriginMode.DesignatedPoint);
        SetField(area, "radius", 0f);

        Assert.That(area.IsValid, Is.False);

        SetField(area, "radius", 1f);
        SetField(area, "angle", 360f);
        SetField(area, "maxCastDistance", 0f);

        Assert.That(area.IsValid, Is.False);
    }

    [Test]
    public void FixedSourceCard_IsIneligibleWithoutConfiguredOwner()
    {
        BattleCardSO card = CreateCard("card.fixed-source");
        SerializedObject data = new(card);
        data.FindProperty("affiliation").enumValueIndex =
            (int)BattleCardAffiliation.Neutral;
        data.FindProperty("sourcePolicy").enumValueIndex =
            (int)BattleCardSourcePolicy.FixedCharacter;
        data.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(card.IsEligible(System.Array.Empty<CharacterSO>()),
            Is.False);
    }

    [Test]
    public void MissingLocalization_UsesSingleFallback()
    {
        BattleCardSO card = CreateCard("card.single-fallback");
        SerializedObject data = new(card);
        string missingKey = $"test.missing.{System.Guid.NewGuid():N}";
        data.FindProperty("nameLocalizationKey").stringValue =
            missingKey + ".name";
        data.FindProperty("descriptionLocalizationKey").stringValue =
            missingKey + ".description";
        data.FindProperty("fallbackName").stringValue =
            "CARD_IDENTIFIER";
        data.FindProperty("fallbackDescription").stringValue =
            "CARD_DESCRIPTION_IDENTIFIER";
        data.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(data.FindProperty("koreanName"), Is.Null);
        Assert.That(data.FindProperty("englishName"), Is.Null);
        Assert.That(data.FindProperty("koreanDescription"), Is.Null);
        Assert.That(data.FindProperty("englishDescription"), Is.Null);
        Assert.That(
            card.GetLocalizedDisplayName(),
            Is.EqualTo("CARD_IDENTIFIER"));
        Assert.That(
            card.GetLocalizedDescription(),
            Is.EqualTo("CARD_DESCRIPTION_IDENTIFIER"));
    }

    [Test]
    public void SelectingDifferentCard_ClearsEditingFocus()
    {
        BattleCardSO first = CreateCard("card.focus.first");
        BattleCardSO second = CreateCard("card.focus.second");
        BattleCardEditorWindow window =
            ScriptableObject.CreateInstance<BattleCardEditorWindow>();
        window.hideFlags = HideFlags.HideAndDontSave;
        created.Add(window);

        FieldInfo selectedField = typeof(BattleCardEditorWindow).GetField(
            "selected",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo serializedField = typeof(BattleCardEditorWindow).GetField(
            "serialized",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo select = typeof(BattleCardEditorWindow).GetMethod(
            "Select",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(selectedField, Is.Not.Null);
        Assert.That(serializedField, Is.Not.Null);
        Assert.That(select, Is.Not.Null);
        selectedField.SetValue(window, first);
        serializedField.SetValue(window, new SerializedObject(first));

        try
        {
            EditorGUIUtility.editingTextField = true;
            select.Invoke(window, new object[] { second });

            Assert.That(EditorGUIUtility.editingTextField, Is.False);
            Assert.That(selectedField.GetValue(window), Is.SameAs(second));
        }
        finally
        {
            EditorGUIUtility.editingTextField = false;
        }
    }

    [Test]
    public void CardEditorFilters_CombineRarityAndCharacterDependency()
    {
        BattleCardSO neutralCommon = CreateCard("card.filter.neutral");
        BattleCardSO dependentRare = CreateCard("card.filter.dependent");
        SerializedObject neutralData = new(neutralCommon);
        neutralData.FindProperty("rarity").enumValueIndex =
            (int)ItemRarity.Common;
        neutralData.FindProperty("affiliation").enumValueIndex =
            (int)BattleCardAffiliation.Neutral;
        neutralData.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject dependentData = new(dependentRare);
        dependentData.FindProperty("rarity").enumValueIndex =
            (int)ItemRarity.Rare;
        dependentData.FindProperty("affiliation").enumValueIndex =
            (int)BattleCardAffiliation.CharacterDependent;
        dependentData.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(
            BattleCardEditorWindow.MatchesFilters(neutralCommon, 1, 1),
            Is.True);
        Assert.That(
            BattleCardEditorWindow.MatchesFilters(dependentRare, 1, 1),
            Is.False);
        Assert.That(
            BattleCardEditorWindow.MatchesFilters(dependentRare, 3, 2),
            Is.True);
        Assert.That(
            BattleCardEditorWindow.MatchesFilters(neutralCommon, 3, 2),
            Is.False);
    }

    private BattleCardDeckRules CreateRules(
        int baseDrawCount,
        float redrawCooldown,
        float mulliganCooldown = 3f)
    {
        BattleCardDeckRules rules = new();
        SetField(rules, "baseDrawCount", baseDrawCount);
        SetField(rules, "redrawCooldown", redrawCooldown);
        SetField(rules, "mulliganCooldown", mulliganCooldown);
        return rules;
    }

    private List<BattleCardSO> CreateCards(int count)
    {
        List<BattleCardSO> cards = new(count);
        for (int index = 0; index < count; index++)
            cards.Add(CreateCard($"card.test.{index}"));
        return cards;
    }

    private BattleCardSO CreateCard(string id)
    {
        BattleCardSO card = Create<BattleCardSO>();
        SerializedObject data = new(card);
        data.FindProperty("cardId").stringValue = id;
        SerializedProperty effects = data.FindProperty("abilityEffects");
        BattleAbilityEditorGUI.AddDefaultEffect(effects);
        data.ApplyModifiedPropertiesWithoutUndo();
        return card;
    }

    private T Create<T>() where T : ScriptableObject
    {
        T instance = ScriptableObject.CreateInstance<T>();
        created.Add(instance);
        return instance;
    }

    private static CharacterEffectDefinition CreateEffect(
        CharacterEffectType type,
        float amount)
    {
        CharacterEffectDefinition effect = new();
        SetField(effect, "type", type);
        SetField(
            effect,
            "damageAmountMode",
            CharacterDamageAmountMode.Fixed);
        SetField(effect, "damageAmount", amount);
        return effect;
    }

    private sealed class TestActiveSkillResource : IActiveSkillResource
    {
        public int Current { get; private set; }
        public int Maximum { get; }
        public event System.Action<int> Changed;

        public TestActiveSkillResource(int current)
        {
            Current = Mathf.Max(0, current);
            Maximum = Current;
        }

        public bool CanSpend(int amount)
        {
            return amount > 0 && Current >= amount;
        }

        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount))
                return false;
            Current -= amount;
            Changed?.Invoke(Current);
            return true;
        }

        public bool TryGain(int amount)
        {
            return false;
        }
    }

    private static void SetEnum(
        Object target,
        string propertyName,
        int value)
    {
        SerializedObject serialized = new(target);
        serialized.FindProperty(propertyName).enumValueIndex = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerializedInt(
        Object target,
        string propertyName,
        int value)
    {
        SerializedObject serialized = new(target);
        serialized.FindProperty(propertyName).intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

}
