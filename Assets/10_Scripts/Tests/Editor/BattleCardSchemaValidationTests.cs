using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BattleCardSchemaValidationTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new();
    private int _nextCardId;

    [TearDown]
    public void TearDown()
    {
        for (int index = _createdObjects.Count - 1; index >= 0; index--)
        {
            if (_createdObjects[index] != null)
                UnityEngine.Object.DestroyImmediate(_createdObjects[index]);
        }
        _createdObjects.Clear();
    }

    [Test]
    public void ObjectiveTargetMode_PreservesSerializedProjection()
    {
        CharacterEffectDefinition effect = Effect(
            CharacterEffectType.Heal,
            CharacterEffectTargetMode.Objective);

        effect.Validate();

        Assert.That(
            (int)CharacterEffectTargetMode.Objective,
            Is.EqualTo((int)BattleEffectTargetMode.Objective));
        Assert.That(
            effect.TargetMode,
            Is.EqualTo(CharacterEffectTargetMode.Objective));
        Assert.That(
            effect.BattleTargetMode,
            Is.EqualTo(BattleEffectTargetMode.Objective));
        Assert.That(
            BattleEffectRules.TryValidate(effect, out string error),
            Is.True,
            error);
    }

    [TestCase(BattleCardTargetScope.AllEnemies)]
    [TestCase(BattleCardTargetScope.AllAllies)]
    [TestCase(BattleCardTargetScope.RandomEnemies)]
    [TestCase(BattleCardTargetScope.LowestHealthAlly)]
    [TestCase(BattleCardTargetScope.SpecificCharacter)]
    public void OperationOnly_GlobalScope_DoesNotRequireActionTargets(
        BattleCardTargetScope scope)
    {
        BattleCardOperationDefinition operation = Operation(
            BattleCardOperationType.SharedEffect,
            scope);
        SetField(operation, "sharedEffect", Effect(
            CharacterEffectType.Damage,
            CharacterEffectTargetMode.InheritAction));
        if (scope == BattleCardTargetScope.SpecificCharacter)
        {
            SetField(
                operation,
                "requiredCharacter",
                Create<CharacterSO>());
        }
        BattleCardSO card = Card(operation);

        Assert.That(card.Subject, Is.EqualTo(CharacterAttackSubject.None));
        Assert.That(card.AbilitySchemaVersion, Is.EqualTo(2));
        Assert.That(card.RequiresActionTargets, Is.False);
        Assert.That(
            AbilityDefinitionValidator.TryValidate(card, out string error),
            Is.True,
            error);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                card,
                out string cardError),
            Is.True,
            cardError);
    }

    [TestCase(BattleCardTargetScope.Primary)]
    [TestCase(BattleCardTargetScope.NearbyPrimaryEnemies)]
    [TestCase(BattleCardTargetScope.BehindPrimaryEnemy)]
    public void PrimaryDependentOperation_RequiresPrimarySelection(
        BattleCardTargetScope scope)
    {
        BattleCardSO card = Card(Operation(
            BattleCardOperationType.SharedEffect,
            scope));

        Assert.That(card.RequiresActionTargets, Is.True);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(card, out string error),
            Is.False);
        Assert.That(error, Does.Contain("primary target"));
    }

    [Test]
    public void SecondaryOperation_UsesSecondarySelectionOnly()
    {
        BattleCardSO card = Card(Operation(
            BattleCardOperationType.SharedEffect,
            BattleCardTargetScope.Secondary));
        BattleCardSecondaryTargetDefinition secondary = card.SecondaryTarget;
        SetField(secondary, "enabled", true);
        SetField(secondary, "worldPoint", false);
        SetField(
            secondary,
            "targetFaction",
            CharacterTargetFaction.Enemy);
        SetField(
            secondary,
            "subject",
            CharacterAttackSubject.Manual);

        Assert.That(card.Subject, Is.EqualTo(CharacterAttackSubject.None));
        Assert.That(card.RequiresActionTargets, Is.True);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(card, out string error),
            Is.True,
            error);
    }

    [TestCase(0, 1, true)]
    [TestCase(1, 1, true)]
    [TestCase(2, 1, true)]
    [TestCase(3, 1, true)]
    [TestCase(4, 1, false)]
    [TestCase(4, 4, true)]
    [TestCase(5, 4, false)]
    [TestCase(5, 5, true)]
    public void MinimumMaximumEnergy_EnforcesHighCostPolicy(
        int cost,
        int minimumMaximumEnergy,
        bool expected)
    {
        BattleCardSO card = Card(Operation(BattleCardOperationType.Draw));
        SetField(card, "energyCost", cost);
        SetField(card, "minimumMaximumEnergy", minimumMaximumEnergy);

        bool valid = BattleCardDefinitionValidator.TryValidate(
            card,
            out string error);

        Assert.That(valid, Is.EqualTo(expected), error);
    }

    [Test]
    public void SelectionRangeValidation_ReadsUnclampedSerializedValues()
    {
        BattleCardOperationDefinition operation = Operation(
            BattleCardOperationType.DiscardSelected);
        SetField(operation, "minimumSelectionCount", 2);
        SetField(operation, "maximumSelectionCount", 1);
        BattleCardSO card = Card(operation);

        Assert.That(operation.HasValidSelectionRange, Is.False);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(card, out string error),
            Is.False);
        Assert.That(error, Does.Contain("selection range"));
    }

    [Test]
    public void OperationValidation_RejectsNullUndefinedAndMissingReferences()
    {
        BattleCardSO nullCollection = CardWithEffect(Effect(
            CharacterEffectType.Heal,
            CharacterEffectTargetMode.Objective));
        SetField<List<BattleCardOperationDefinition>>(
            nullCollection,
            "operations",
            null);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                nullCollection,
                out string collectionError),
            Is.False);
        Assert.That(collectionError, Does.Contain("collections"));

        BattleCardSO nullOperation = Card((BattleCardOperationDefinition)null);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                nullOperation,
                out string nullError),
            Is.False);
        Assert.That(nullError, Does.Contain("null"));

        BattleCardOperationDefinition undefined = Operation(
            (BattleCardOperationType)999);
        BattleCardSO undefinedCard = Card(undefined);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                undefinedCard,
                out string enumError),
            Is.False);
        Assert.That(enumError, Does.Contain("undefined"));

        BattleCardOperationDefinition missingCondition = Operation(
            BattleCardOperationType.Draw);
        SetField<BattleCardConditionDefinition>(
            missingCondition,
            "condition",
            null);
        BattleCardSO missingConditionCard = Card(missingCondition);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                missingConditionCard,
                out string conditionError),
            Is.False);
        Assert.That(conditionError, Does.Contain("condition"));

        BattleCardOperationDefinition missingSharedEffect = Operation(
            BattleCardOperationType.SharedEffect,
            BattleCardTargetScope.AllEnemies);
        SetField<CharacterEffectDefinition>(
            missingSharedEffect,
            "sharedEffect",
            null);
        BattleCardSO missingSharedEffectCard = Card(missingSharedEffect);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                missingSharedEffectCard,
                out string sharedEffectError),
            Is.False);
        Assert.That(sharedEffectError, Does.Contain("shared effect"));

        BattleCardOperationDefinition extension = Operation(
            BattleCardOperationType.ExtendStatusDuration,
            BattleCardTargetScope.AllEnemies);
        SetField(extension, "duration", 1f);
        BattleCardSO extensionCard = Card(extension);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                extensionCard,
                out string referenceError),
            Is.False);
        Assert.That(referenceError, Does.Contain("status"));
    }

    [Test]
    public void FilterValidation_RejectsEnemyCharacterFilters()
    {
        BattleCardSO card = Card(Operation(BattleCardOperationType.Draw));
        SetField(
            card.PrimaryTargetFilter,
            "requiredRole",
            Create<CharacterRoleSO>());

        Assert.That(
            BattleCardDefinitionValidator.TryValidate(card, out string error),
            Is.False);
        Assert.That(error, Does.Contain("allied target faction"));
    }

    [Test]
    public void SecondaryWorldPoint_RequiresDesignatedWorldArea()
    {
        BattleCardSO card = Card(Operation(
            BattleCardOperationType.SharedEffect,
            BattleCardTargetScope.Secondary));
        BattleCardSecondaryTargetDefinition secondary = card.SecondaryTarget;
        SetField(secondary, "enabled", true);
        SetField(secondary, "worldPoint", true);
        SetField(
            secondary,
            "subject",
            CharacterAttackSubject.Manual);

        Assert.That(
            BattleCardDefinitionValidator.TryValidate(card, out string error),
            Is.False);
        Assert.That(error, Does.Contain("world point"));
    }

    [Test]
    public void ObjectiveEffects_AllowOnlyHealAndShield()
    {
        BattleCardSO healCard = CardWithEffect(Effect(
            CharacterEffectType.Heal,
            CharacterEffectTargetMode.Objective));
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                healCard,
                out string healError),
            Is.True,
            healError);

        BattleCardSO damageCard = CardWithEffect(Effect(
            CharacterEffectType.Damage,
            CharacterEffectTargetMode.Objective));
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                damageCard,
                out string damageError),
            Is.False);
        Assert.That(damageError, Does.Contain("objective").IgnoreCase);
    }

    [Test]
    public void ObjectiveOperations_RequireDurationAndAlliedRedirectTarget()
    {
        BattleCardOperationDefinition immunity = Operation(
            BattleCardOperationType.ObjectiveInvulnerability);
        BattleCardSO immunityCard = Card(immunity);
        Assert.That(immunity.Duration, Is.Zero);
        Assert.That(immunity.DelaySeconds, Is.Zero);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                immunityCard,
                out string durationError),
            Is.False);
        Assert.That(durationError, Does.Contain("duration"));

        SetField(immunity, "duration", 3f);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                immunityCard,
                out string immunityError),
            Is.True,
            immunityError);

        BattleCardOperationDefinition redirect = Operation(
            BattleCardOperationType.ObjectiveDamageRedirect,
            BattleCardTargetScope.Primary);
        SetField(redirect, "ratio", 0.3f);
        BattleCardSO redirectCard = Card(redirect);
        SetField(redirectCard, "subject", CharacterAttackSubject.Manual);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                redirectCard,
                out string factionError),
            Is.False);
        Assert.That(factionError, Does.Contain("allied target"));

        SetField(
            redirectCard,
            "targetFaction",
            CharacterTargetFaction.Ally);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                redirectCard,
                out string redirectError),
            Is.True,
            redirectError);
    }

    [Test]
    public void ReadyBasicAttack_RequiresAlliedTargets()
    {
        BattleCardOperationDefinition ready = Operation(
            BattleCardOperationType.ReadyBasicAttack,
            BattleCardTargetScope.Primary);
        BattleCardSO card = Card(ready);
        SetField(card, "subject", CharacterAttackSubject.Manual);

        Assert.That(
            BattleCardDefinitionValidator.TryValidate(card, out string error),
            Is.False);
        Assert.That(error, Does.Contain("allied target"));

        SetField(card, "targetFaction", CharacterTargetFaction.Ally);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                card,
                out string configuredError),
            Is.True,
            configuredError);
    }

    [Test]
    public void SpatialOperation_RequiresDesignatedPoint()
    {
        BattleCardOperationDefinition zone = Operation(
            BattleCardOperationType.CreateZone,
            BattleCardTargetScope.EnemiesAtDesignatedPoint);
        BattleCardSO card = Card(zone);

        Assert.That(
            BattleCardDefinitionValidator.TryValidate(card, out string error),
            Is.False);
        Assert.That(error, Does.Contain("designated"));

        ConfigureDesignatedWorldArea(card);
        Assert.That(card.RequiresActionTargets, Is.True);
        Assert.That(
            BattleCardDefinitionValidator.TryValidate(
                card,
                out string configuredError),
            Is.True,
            configuredError);
    }

    [Test]
    public void OperationOnlyWorldCards_RequestPrimaryPointSelection()
    {
        BattleCardOperationDefinition move = Operation(
            BattleCardOperationType.Move,
            BattleCardTargetScope.AllAllies);
        SetField(
            move,
            "movementMode",
            BattleCardMovementMode.ToWorldPoint);
        BattleCardOperationDefinition[] operations =
        {
            Operation(
                BattleCardOperationType.SharedEffect,
                BattleCardTargetScope.EnemiesAtDesignatedPoint),
            Operation(
                BattleCardOperationType.CreateZone,
                BattleCardTargetScope.EnemiesAtDesignatedPoint),
            Operation(
                BattleCardOperationType.PullEnemies,
                BattleCardTargetScope.EnemiesAtDesignatedPoint),
            move,
        };

        foreach (BattleCardOperationDefinition operation in operations)
        {
            BattleCardSO card = Card(operation);
            ConfigureDesignatedWorldArea(card);

            Assert.That(operation.UsesDesignatedPoint, Is.True);
            Assert.That(
                card.RequiresActionTargets,
                Is.True,
                operation.Type.ToString());
            Assert.That(
                BattleCardDefinitionValidator.TryValidate(
                    card,
                    out string error),
                Is.True,
                $"{operation.Type}: {error}");
        }
    }

    [Test]
    public void OperationCharacterRestrictions_FormPrimaryTargetWhitelist()
    {
        CharacterSO first = Create<CharacterSO>();
        CharacterSO second = Create<CharacterSO>();
        CharacterSO unrelated = Create<CharacterSO>();
        BattleCardOperationDefinition firstOperation = Operation(
            BattleCardOperationType.SharedEffect,
            BattleCardTargetScope.Primary);
        BattleCardOperationDefinition secondOperation = Operation(
            BattleCardOperationType.SharedEffect,
            BattleCardTargetScope.Primary);
        SetField(firstOperation, "requiredCharacter", first);
        SetField(secondOperation, "requiredCharacter", second);
        BattleCardSO card = Card(firstOperation, secondOperation);

        Assert.That(card.AllowsOperationPrimaryTarget(first), Is.True);
        Assert.That(card.AllowsOperationPrimaryTarget(second), Is.True);
        Assert.That(card.AllowsOperationPrimaryTarget(unrelated), Is.False);

        SetField<CharacterSO>(secondOperation, "requiredCharacter", null);
        Assert.That(card.AllowsOperationPrimaryTarget(unrelated), Is.True);
    }

    [Test]
    public void Validation_DoesNotRepairNullSerializedDefinitions()
    {
        BattleCardSO card = Card(Operation(BattleCardOperationType.Draw));
        SetField<BattleAreaDefinition>(card, "areaDefinition", null);
        string before = EditorJsonUtility.ToJson(card);

        Assert.That(
            BattleCardDefinitionValidator.TryValidate(card, out _),
            Is.False);

        Assert.That(EditorJsonUtility.ToJson(card), Is.EqualTo(before));
    }

    private BattleCardSO Card(
        params BattleCardOperationDefinition[] operations)
    {
        BattleCardSO card = Create<BattleCardSO>();
        SetField(card, "cardId", $"test.card.schema.{_nextCardId++}");
        SetField(card, "subject", CharacterAttackSubject.None);
        SetField(
            card,
            "operations",
            new List<BattleCardOperationDefinition>(operations));
        return card;
    }

    private BattleCardSO CardWithEffect(CharacterEffectDefinition effect)
    {
        BattleCardSO card = Card();
        SetField(
            card,
            "abilityEffects",
            new List<CharacterEffectDefinition> { effect });
        return card;
    }

    private static BattleCardOperationDefinition Operation(
        BattleCardOperationType type,
        BattleCardTargetScope scope = BattleCardTargetScope.None)
    {
        BattleCardOperationDefinition operation = new();
        SetField(operation, "operationId", $"operation_{Guid.NewGuid():N}");
        SetField(operation, "type", type);
        SetField(operation, "targetScope", scope);
        if (type == BattleCardOperationType.SharedEffect ||
            type == BattleCardOperationType.CreateZone)
        {
            SetField(
                operation,
                "sharedEffect",
                Effect(
                    CharacterEffectType.Damage,
                    CharacterEffectTargetMode.InheritAction));
        }
        return operation;
    }

    private static CharacterEffectDefinition Effect(
        CharacterEffectType type,
        CharacterEffectTargetMode targetMode)
    {
        CharacterEffectDefinition effect = new();
        SetField(effect, "type", type);
        SetField(effect, "targetMode", targetMode);
        SetField(
            effect,
            "damageAmountMode",
            CharacterDamageAmountMode.Fixed);
        SetField(effect, "damageAmount", 10f);
        return effect;
    }

    private static void ConfigureDesignatedWorldArea(BattleCardSO card)
    {
        SetField(card, "subject", CharacterAttackSubject.Manual);
        SetField(card, "targetCount", 0);
        SetField(
            card.AreaDefinition,
            "shapeType",
            CharacterAreaShapeType.CircleSector);
        SetField(
            card.AreaDefinition,
            "originMode",
            CharacterAreaOriginMode.DesignatedPoint);
        SetField(card.AreaDefinition, "radius", 1f);
        SetField(card.AreaDefinition, "angle", 360f);
        SetField(card.AreaDefinition, "maxCastDistance", 4f);
    }

    private T Create<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        value.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(value);
        return value;
    }

    private static void SetField<T>(object target, string name, T value)
    {
        Assert.That(target, Is.Not.Null);
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(
            field,
            Is.Not.Null,
            $"Missing test field '{target.GetType().Name}.{name}'.");
        field.SetValue(target, value);
    }
}
