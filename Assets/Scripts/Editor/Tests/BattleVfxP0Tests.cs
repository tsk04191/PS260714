using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleVfxP0Tests
{
    private readonly List<UnityEngine.Object> _createdObjects = new();

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
    public void SuccessfulEffect_CreatesImpactRequestForResolvedTarget()
    {
        BattleVfxCueSO cue = CreateCue("impact");
        CharacterEffectDefinition effect = new();
        SetField(effect, "impactVfxCue", cue);
        CharacterRuntime target = CreateCharacterRuntime("Target");
        BattleEffectContext context = CreateAllyContext(target);
        RecordingPresentationSource source = new();
        RecordingSink sink = new();
        using BattlePresentationDispatcher dispatcher = new(sink);
        dispatcher.Bind(source, source);

        source.Raise(new BattleEffectResolvedEvent(
            context,
            effect,
            new BattleEffectResult(true, true, 7)));

        Assert.That(sink.Requests, Has.Count.EqualTo(1));
        Assert.That(sink.Requests[0].Cue, Is.SameAs(cue));
        Assert.That(
            sink.Requests[0].Phase,
            Is.EqualTo(BattleVfxPhase.Impact));
        Assert.That(sink.Requests[0].Amount, Is.EqualTo(7));
        Assert.That(sink.Requests[0].Target.Handle.Value, Is.EqualTo(10));
        Assert.That(sink.Requests[0].Target.HasAnchor, Is.True);
    }

    [Test]
    public void FailedEffect_DoesNotCreateImpactRequest()
    {
        BattleVfxCueSO cue = CreateCue("impact");
        CharacterEffectDefinition effect = new();
        SetField(effect, "impactVfxCue", cue);
        CharacterRuntime target = CreateCharacterRuntime("Target");
        RecordingPresentationSource source = new();
        RecordingSink sink = new();
        using BattlePresentationDispatcher dispatcher = new(sink);
        dispatcher.Bind(source, source);

        source.Raise(new BattleEffectResolvedEvent(
            CreateAllyContext(target),
            effect,
            new BattleEffectResult(true, false)));

        Assert.That(sink.Requests, Is.Empty);
    }

    [Test]
    public void StatusLifecycle_MapsApplyTickAndRemoveToConfiguredCues()
    {
        BattleVfxCueSO apply = CreateCue("apply");
        BattleVfxCueSO loop = CreateCue("loop");
        BattleVfxCueSO tick = CreateCue("tick");
        BattleVfxCueSO remove = CreateCue("remove");
        StatusEffectSO status = CreateStatus(
            apply,
            loop,
            tick,
            remove);
        CharacterRuntime target = CreateCharacterRuntime("Target");
        BattleStatusTarget battleTarget =
            BattleStatusTarget.FromAlly(target);
        RecordingPresentationSource source = new();
        RecordingSink sink = new();
        using BattlePresentationDispatcher dispatcher = new(sink);
        dispatcher.Bind(source, source);

        source.Raise(new StatusEffectLifecycleEvent(
            status,
            StatusEffectLifecycleTrigger.OnApply,
            battleTarget,
            null,
            0,
            2));
        source.Raise(new StatusEffectLifecycleEvent(
            status,
            StatusEffectLifecycleTrigger.OnTick,
            battleTarget,
            null,
            2,
            2,
            3));
        source.Raise(new StatusEffectLifecycleEvent(
            status,
            StatusEffectLifecycleTrigger.OnRemove,
            battleTarget,
            null,
            2,
            0));

        Assert.That(sink.Requests, Has.Count.EqualTo(5));
        Assert.That(
            sink.Requests.ConvertAll(request => request.Phase),
            Is.EqualTo(new[]
            {
                BattleVfxPhase.StatusApply,
                BattleVfxPhase.StatusLoopStart,
                BattleVfxPhase.StatusTick,
                BattleVfxPhase.StatusLoopStop,
                BattleVfxPhase.StatusRemove
            }));
        Assert.That(sink.Requests[2].OccurrenceCount, Is.EqualTo(3));
        Assert.That(sink.Requests[2].StackCount, Is.EqualTo(2));
    }

    [Test]
    public void Unbind_StopsFurtherRequests()
    {
        BattleVfxCueSO cue = CreateCue("impact");
        CharacterEffectDefinition effect = new();
        SetField(effect, "impactVfxCue", cue);
        CharacterRuntime target = CreateCharacterRuntime("Target");
        RecordingPresentationSource source = new();
        RecordingSink sink = new();
        using BattlePresentationDispatcher dispatcher = new(sink);
        dispatcher.Bind(source, source);
        dispatcher.Unbind();

        source.Raise(new BattleEffectResolvedEvent(
            CreateAllyContext(target),
            effect,
            new BattleEffectResult(true, true, 1)));

        Assert.That(sink.Requests, Is.Empty);
    }

    private BattleVfxCueSO CreateCue(string cueName)
    {
        BattleVfxCueSO cue = ScriptableObject.CreateInstance<BattleVfxCueSO>();
        cue.name = cueName;
        _createdObjects.Add(cue);
        return cue;
    }

    private StatusEffectSO CreateStatus(
        BattleVfxCueSO apply,
        BattleVfxCueSO loop,
        BattleVfxCueSO tick,
        BattleVfxCueSO remove)
    {
        StatusEffectSO status =
            ScriptableObject.CreateInstance<StatusEffectSO>();
        status.name = "Status";
        SetField(status, "applyVfxCue", apply);
        SetField(status, "loopVfxCue", loop);
        SetField(status, "tickVfxCue", tick);
        SetField(status, "removeVfxCue", remove);
        _createdObjects.Add(status);
        return status;
    }

    private CharacterRuntime CreateCharacterRuntime(string objectName)
    {
        GameObject gameObject = new(objectName, typeof(RectTransform));
        _createdObjects.Add(gameObject);
        return gameObject.AddComponent<CharacterRuntime>();
    }

    private static BattleEffectContext CreateAllyContext(
        CharacterRuntime target)
    {
        EffectContext context = new(
            target,
            null,
            null,
            CharacterActionKind.Attack,
            CharacterTargetFaction.Ally,
            Array.Empty<EnemyRuntime>(),
            new IBattleCharacter[] { target },
            0f);
        return BattleEffectContext.FromCharacter(context);
    }

    private static void SetField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
        field.SetValue(target, value);
    }

    private sealed class RecordingSink : IBattleVfxRequestSink
    {
        public List<BattleVfxRequest> Requests { get; } = new();

        public void Enqueue(BattleVfxRequest request)
        {
            Requests.Add(request);
        }
    }

    private sealed class RecordingPresentationSource :
        IBattlePresentationEventSource,
        IBattleVfxTargetResolver
    {
        public event Action<BattleEffectResolvedEvent> EffectResolved;
        public event Action<StatusEffectLifecycleEvent> StatusLifecycle;
        public event Action<BattleUnitLifecycleEvent> UnitLifecycle;

        public void Raise(BattleUnitLifecycleEvent eventData)
        {
            UnitLifecycle?.Invoke(eventData);
        }

        public BattleVfxTarget ResolveVfxTarget(
            BattleStatusTarget target,
            BattleVfxAnchorType anchorType)
        {
            return new BattleVfxTarget(
                new BattleVfxTargetHandle(10),
                target,
                BattleVfxAnchorSnapshot.FromScreen(
                    new Vector2(100f, 200f)));
        }

        public void Raise(BattleEffectResolvedEvent eventData)
        {
            EffectResolved?.Invoke(eventData);
        }

        public void Raise(StatusEffectLifecycleEvent eventData)
        {
            StatusLifecycle?.Invoke(eventData);
        }
    }
}
