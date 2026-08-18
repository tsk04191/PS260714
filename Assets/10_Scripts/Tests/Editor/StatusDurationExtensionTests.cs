using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class StatusDurationExtensionTests
{
    private readonly List<Object> _createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = _createdObjects.Count - 1; index >= 0; index--)
        {
            if (_createdObjects[index] != null)
                Object.DestroyImmediate(_createdObjects[index]);
        }
        _createdObjects.Clear();
    }

    [Test]
    public void RuntimeState_IndependentBatches_ExtendsAggregateExactlyOnce()
    {
        StatusEffectSO status = CreateStatus(
            "test_extend_independent",
            StatusEffectStackMode.IndependentDuration,
            StatusEffectDurationMode.Timed);
        StatusEffectRuntimeState state = new(status);

        Assert.That(
            state.Apply(1, 2f, 1f, default).Succeeded,
            Is.True);
        Assert.That(
            state.Apply(1, 3f, 1f, default).Succeeded,
            Is.True);
        Assert.That(state.StackCount, Is.EqualTo(2));
        Assert.That(state.RemainingDuration, Is.EqualTo(5f));

        Assert.That(state.TryExtendDuration(1.5f), Is.True);

        Assert.That(state.StackCount, Is.EqualTo(2));
        Assert.That(state.RemainingDuration, Is.EqualTo(6.5f));
        Assert.That(state.TotalDuration, Is.EqualTo(6.5f));
        Assert.That(
            state.ActiveBatch.RemainingDuration,
            Is.EqualTo(3.5f));
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void RuntimeState_InvalidExtension_DoesNotMutate(float seconds)
    {
        StatusEffectSO status = CreateStatus(
            "test_extend_invalid",
            StatusEffectStackMode.AddAndRefreshDuration,
            StatusEffectDurationMode.Timed);
        StatusEffectRuntimeState state = new(status);
        Assert.That(
            state.Apply(2, 2f, 1f, default).Succeeded,
            Is.True);

        Assert.That(state.TryExtendDuration(seconds), Is.False);

        Assert.That(state.StackCount, Is.EqualTo(2));
        Assert.That(state.RemainingDuration, Is.EqualTo(2f));
    }

    [Test]
    public void RuntimeState_PermanentStatus_IsNotChanged()
    {
        StatusEffectSO status = CreateStatus(
            "test_extend_permanent",
            StatusEffectStackMode.AddAndRefreshDuration,
            StatusEffectDurationMode.Permanent);
        StatusEffectRuntimeState state = new(status);
        Assert.That(
            state.Apply(
                1,
                float.PositiveInfinity,
                1f,
                default).Succeeded,
            Is.True);

        Assert.That(state.TryExtendDuration(5f), Is.False);
        Assert.That(state.StackCount, Is.EqualTo(1));
        Assert.That(
            state.RemainingDuration,
            Is.EqualTo(float.PositiveInfinity));
    }

    [Test]
    public void EnemyExtension_RaisesReappliedEventWithoutChangingStacks()
    {
        StatusEffectSO status = CreateStatus(
            "test_extend_enemy",
            StatusEffectStackMode.IndependentDuration,
            StatusEffectDurationMode.Timed);
        EnemySO definition = ScriptableObject.CreateInstance<EnemySO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(definition);
        EnemyRuntime enemy = new(definition);
        List<BattleStatusChangedEvent> events = new();
        enemy.StatusChanged += events.Add;

        Assert.That(enemy.ApplyStatusEffect(status, 2f, 1), Is.True);
        Assert.That(enemy.ApplyStatusEffect(status, 3f, 1), Is.True);
        int eventCountBeforeExtension = events.Count;

        Assert.That(enemy.TryExtendStatusDuration(status, 2f), Is.True);

        Assert.That(events, Has.Count.EqualTo(eventCountBeforeExtension + 1));
        BattleStatusChangedEvent change = events[^1];
        Assert.That(
            change.ChangeType,
            Is.EqualTo(BattleStatusChangeType.Reapplied));
        Assert.That(change.PreviousStacks, Is.EqualTo(2));
        Assert.That(change.CurrentStacks, Is.EqualTo(2));
        Assert.That(change.Previous.RemainingDuration, Is.EqualTo(5f));
        Assert.That(change.Current.RemainingDuration, Is.EqualTo(7f));
        Assert.That(
            enemy.GetActiveStatusEffects()[0].RemainingDuration,
            Is.EqualTo(7f));
    }

    [Test]
    public void CharacterExtension_RaisesReappliedEventAndRefreshesSnapshot()
    {
        StatusEffectSO status = CreateStatus(
            "test_extend_character",
            StatusEffectStackMode.AddAndRefreshDuration,
            StatusEffectDurationMode.Timed);
        GameObject gameObject = new(
            "StatusDurationExtensionCharacter",
            typeof(RectTransform));
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(gameObject);
        CharacterRuntime character =
            gameObject.AddComponent<CharacterRuntime>();
        List<BattleStatusChangedEvent> events = new();
        character.StatusChanged += events.Add;

        Assert.That(character.ApplyStatusEffect(status, 2f, 3), Is.True);
        int eventCountBeforeExtension = events.Count;

        Assert.That(
            character.TryExtendStatusDuration(status, 3f),
            Is.True);

        Assert.That(events, Has.Count.EqualTo(eventCountBeforeExtension + 1));
        BattleStatusChangedEvent change = events[^1];
        Assert.That(
            change.ChangeType,
            Is.EqualTo(BattleStatusChangeType.Reapplied));
        Assert.That(change.PreviousStacks, Is.EqualTo(3));
        Assert.That(change.CurrentStacks, Is.EqualTo(3));
        Assert.That(change.Previous.RemainingDuration, Is.EqualTo(2f));
        Assert.That(change.Current.RemainingDuration, Is.EqualTo(5f));
        Assert.That(
            character.GetActiveStatusEffects()[0].RemainingDuration,
            Is.EqualTo(5f));
    }

    private StatusEffectSO CreateStatus(
        string statusId,
        StatusEffectStackMode stackMode,
        StatusEffectDurationMode durationMode)
    {
        StatusEffectSO status =
            ScriptableObject.CreateInstance<StatusEffectSO>();
        status.hideFlags = HideFlags.HideAndDontSave;
        status.name = statusId;
        _createdObjects.Add(status);

        SerializedObject serialized = new(status);
        serialized.FindProperty("statusId").stringValue = statusId;
        serialized.FindProperty("canTargetEnemy").boolValue = true;
        serialized.FindProperty("canTargetAlly").boolValue = true;
        serialized.FindProperty("durationMode").enumValueIndex =
            (int)durationMode;
        serialized.FindProperty("defaultDuration").floatValue = 1f;
        serialized.FindProperty("refreshDurationOnReapply").boolValue = true;
        serialized.FindProperty("tickInterval").floatValue = 1f;
        serialized.FindProperty("stackMode").enumValueIndex = (int)stackMode;
        serialized.FindProperty("maximumStacks").intValue = 0;
        serialized.FindProperty("defaultAppliedStacks").intValue = 1;
        serialized.FindProperty("stackRemovalOrder").enumValueIndex =
            (int)StatusEffectStackRemovalOrder.Oldest;
        serialized.FindProperty("removable").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        status.ValidateDefinition();
        return status;
    }
}
