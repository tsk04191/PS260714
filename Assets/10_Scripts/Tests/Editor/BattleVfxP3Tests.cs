using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleVfxP3Tests
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
    public void SuccessfulMultiTargetEffect_QueuesOneCastAndPerTargetStages()
    {
        BattleVfxCueSO cast = CreateCue("Cast");
        BattleVfxCueSO projectile = CreateCue("Projectile");
        BattleVfxCueSO impact = CreateCue("Impact");
        SetField(projectile, "motionMode", BattleVfxMotionMode.Linear);
        CharacterEffectDefinition effect = new();
        SetField(effect, "castVfxCue", cast);
        SetField(effect, "projectileVfxCue", projectile);
        SetField(effect, "impactVfxCue", impact);

        CharacterRuntime source = CreateCharacter("Source");
        CharacterRuntime first = CreateCharacter("First");
        CharacterRuntime second = CreateCharacter("Second");
        BattleEffectContext context = CreateAllyContext(
            source,
            first,
            second);
        RecordingPresentationSource eventSource = new();
        RecordingSink sink = new();
        PositionResolver resolver = new(
            source,
            first,
            second);
        using BattlePresentationDispatcher dispatcher = new(sink);
        dispatcher.Bind(eventSource, resolver);

        eventSource.Raise(new BattleEffectResolvedEvent(
            context,
            effect,
            new BattleEffectResult(true, true, 9)));

        Assert.That(sink.Requests, Has.Count.EqualTo(5));
        Assert.That(
            sink.Requests.ConvertAll(request => request.Phase),
            Is.EqualTo(new[]
            {
                BattleVfxPhase.Cast,
                BattleVfxPhase.Projectile,
                BattleVfxPhase.Impact,
                BattleVfxPhase.Projectile,
                BattleVfxPhase.Impact
            }));
        Assert.That(
            sink.Requests[0].Target.BattleTarget.Ally,
            Is.SameAs(source));
        Assert.That(
            sink.Requests[1].SourceTarget.BattleTarget.Ally,
            Is.SameAs(source));
        Assert.That(
            sink.Requests[1].Target.BattleTarget.Ally,
            Is.SameAs(first));
        Assert.That(
            sink.Requests[3].Target.BattleTarget.Ally,
            Is.SameAs(second));
        Assert.That(sink.Requests[0].DelaySeconds, Is.EqualTo(0f));
        Assert.That(sink.Requests[1].DelaySeconds, Is.EqualTo(1f));
        Assert.That(sink.Requests[2].DelaySeconds, Is.EqualTo(1.25f));
        Assert.That(sink.Requests[3].DelaySeconds, Is.EqualTo(1f));
        Assert.That(sink.Requests[4].DelaySeconds, Is.EqualTo(1.25f));
    }

    [Test]
    public void FailedEffect_DoesNotQueueCastProjectileOrImpact()
    {
        CharacterEffectDefinition effect = new();
        SetField(effect, "castVfxCue", CreateCue("Cast"));
        SetField(effect, "projectileVfxCue", CreateCue("Projectile"));
        SetField(effect, "impactVfxCue", CreateCue("Impact"));
        CharacterRuntime source = CreateCharacter("Source");
        CharacterRuntime target = CreateCharacter("Target");
        RecordingPresentationSource eventSource = new();
        RecordingSink sink = new();
        using BattlePresentationDispatcher dispatcher = new(sink);
        dispatcher.Bind(
            eventSource,
            new PositionResolver(source, target));

        eventSource.Raise(new BattleEffectResolvedEvent(
            CreateAllyContext(source, target),
            effect,
            new BattleEffectResult(true, false)));

        Assert.That(sink.Requests, Is.Empty);
    }

    [Test]
    public void LinearMotion_TravelsFromSourceToTarget()
    {
        BattleVfxPlayer player = CreatePlayer();
        GameObject prefab = CreatePrefab("LinearProjectile");
        BattleVfxCueSO cue = CreateMotionCue(
            prefab,
            BattleVfxMotionMode.Linear,
            1f,
            0f);
        CharacterRuntime source = CreateCharacter("Source");
        CharacterRuntime target = CreateCharacter("Target");
        BattleVfxRequest request = CreateMotionRequest(
            cue,
            source,
            target,
            Vector3.zero,
            new Vector3(10f, 0f, 0f));

        player.Enqueue(request);

        Assert.That(
            player.TryGetActiveInstance(
                cue,
                request.Target.Handle,
                out GameObject instance),
            Is.True);
        Assert.That(instance.transform.position.x, Is.EqualTo(0f).Within(0.001f));

        player.Advance(0.5f, 0f);

        Assert.That(instance.transform.position.x, Is.EqualTo(5f).Within(0.001f));
        Assert.That(instance.transform.position.y, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void ArcMotion_ReachesConfiguredHeightAtMidpoint()
    {
        BattleVfxPlayer player = CreatePlayer();
        GameObject prefab = CreatePrefab("ArcProjectile");
        BattleVfxCueSO cue = CreateMotionCue(
            prefab,
            BattleVfxMotionMode.Arc,
            1f,
            3f);
        CharacterRuntime source = CreateCharacter("Source");
        CharacterRuntime target = CreateCharacter("Target");
        BattleVfxRequest request = CreateMotionRequest(
            cue,
            source,
            target,
            Vector3.zero,
            new Vector3(10f, 0f, 0f));
        player.Enqueue(request);
        player.TryGetActiveInstance(
            cue,
            request.Target.Handle,
            out GameObject instance);

        player.Advance(0.5f, 0f);

        Assert.That(instance, Is.Not.Null);
        Assert.That(instance.transform.position.x, Is.EqualTo(5f).Within(0.001f));
        Assert.That(instance.transform.position.y, Is.EqualTo(3f).Within(0.001f));
    }

    [Test]
    public void DelayedProjectile_WaitsBeforeStartingMotion()
    {
        BattleVfxPlayer player = CreatePlayer();
        GameObject prefab = CreatePrefab("DelayedProjectile");
        BattleVfxCueSO cue = CreateMotionCue(
            prefab,
            BattleVfxMotionMode.Linear,
            1f,
            0f);
        CharacterRuntime source = CreateCharacter("Source");
        CharacterRuntime target = CreateCharacter("Target");
        BattleVfxRequest request = CreateMotionRequest(
            cue,
            source,
            target,
            Vector3.zero,
            new Vector3(10f, 0f, 0f),
            0.5f);

        player.Enqueue(request);
        Assert.That(player.ActiveInstanceCount, Is.EqualTo(0));
        Assert.That(player.ScheduledRequestCount, Is.EqualTo(1));

        player.Advance(0.25f, 0f);
        Assert.That(player.ActiveInstanceCount, Is.EqualTo(0));

        player.Advance(0.25f, 0f);
        Assert.That(player.ActiveInstanceCount, Is.EqualTo(1));
        Assert.That(
            player.TryGetActiveInstance(
                cue,
                request.Target.Handle,
                out GameObject instance),
            Is.True);
        Assert.That(instance.transform.position.x, Is.EqualTo(0f).Within(0.001f));

        player.Advance(0.5f, 0f);
        Assert.That(instance.transform.position.x, Is.EqualTo(5f).Within(0.001f));
    }

    [Test]
    public void PersistentMotion_IsRejectedByAuthoringValidator()
    {
        BattleVfxCueSO cue = CreateCue("InvalidPersistentMotion");
        SetField(cue, "prefab", CreatePrefab("MotionPrefab"));
        SetField(cue, "motionMode", BattleVfxMotionMode.Linear);
        SetField(
            cue,
            "lifetimeMode",
            BattleVfxLifetimeMode.Persistent);

        BattleVfxCueValidationResult result =
            BattleVfxCueValidator.Validate(cue);

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(result, "vfx.motion_persistent"),
            Is.True);
    }

    private BattleVfxPlayer CreatePlayer()
    {
        GameObject gameObject = new("BattleVfxPlayer");
        _createdObjects.Add(gameObject);
        return gameObject.AddComponent<BattleVfxPlayer>();
    }

    private BattleVfxCueSO CreateCue(string cueName)
    {
        BattleVfxCueSO cue =
            ScriptableObject.CreateInstance<BattleVfxCueSO>();
        cue.name = cueName;
        cue.RegenerateCueId();
        _createdObjects.Add(cue);
        return cue;
    }

    private BattleVfxCueSO CreateMotionCue(
        GameObject prefab,
        BattleVfxMotionMode motionMode,
        float travelDuration,
        float arcHeight)
    {
        BattleVfxCueSO cue = CreateCue($"{motionMode}Cue");
        SetField(cue, "prefab", prefab);
        SetField(cue, "motionMode", motionMode);
        SetField(cue, "travelDuration", travelDuration);
        SetField(cue, "arcHeight", arcHeight);
        SetField(cue, "faceMotionDirection", false);
        SetField(cue, "lifetimeMode", BattleVfxLifetimeMode.Timed);
        SetField(cue, "duration", 2f);
        return cue;
    }

    private GameObject CreatePrefab(string objectName)
    {
        GameObject prefab = new(objectName);
        prefab.SetActive(false);
        _createdObjects.Add(prefab);
        return prefab;
    }

    private CharacterRuntime CreateCharacter(string objectName)
    {
        GameObject gameObject = new(objectName, typeof(RectTransform));
        _createdObjects.Add(gameObject);
        return gameObject.AddComponent<CharacterRuntime>();
    }

    private static BattleEffectContext CreateAllyContext(
        CharacterRuntime source,
        params CharacterRuntime[] targets)
    {
        EffectContext context = new(
            source,
            null,
            null,
            CharacterActionKind.Skill,
            CharacterTargetFaction.Ally,
            Array.Empty<EnemyRuntime>(),
            targets,
            0f);
        return BattleEffectContext.FromCharacter(context);
    }

    private static BattleVfxRequest CreateMotionRequest(
        BattleVfxCueSO cue,
        CharacterRuntime source,
        CharacterRuntime target,
        Vector3 sourcePosition,
        Vector3 targetPosition,
        float delaySeconds = 0f)
    {
        BattleStatusTarget sourceTarget =
            BattleStatusTarget.FromAlly(source);
        BattleStatusTarget targetTarget =
            BattleStatusTarget.FromAlly(target);
        return new BattleVfxRequest(
            cue,
            BattleVfxPhase.Projectile,
            BattleEffectOriginKind.CharacterSkill,
            sourceTarget,
            new BattleVfxTarget(
                new BattleVfxTargetHandle(1),
                sourceTarget,
                BattleVfxAnchorSnapshot.FromWorld(
                    sourcePosition,
                    Quaternion.identity)),
            new BattleVfxTarget(
                new BattleVfxTargetHandle(2),
                targetTarget,
                BattleVfxAnchorSnapshot.FromWorld(
                    targetPosition,
                    Quaternion.identity)),
            delaySeconds: delaySeconds);
    }

    private static bool HasDiagnostic(
        BattleVfxCueValidationResult result,
        string code)
    {
        foreach (BattleVfxCueDiagnostic diagnostic in result.Diagnostics)
        {
            if (diagnostic.Code == code)
                return true;
        }

        return false;
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
        IBattlePresentationEventSource
    {
        public event Action<BattleEffectResolvedEvent> EffectResolved;
        public event Action<StatusEffectLifecycleEvent> StatusLifecycle;
        public event Action<BattleUnitLifecycleEvent> UnitLifecycle;

        public void Raise(BattleUnitLifecycleEvent eventData)
        {
            UnitLifecycle?.Invoke(eventData);
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

    private sealed class PositionResolver : IBattleVfxTargetResolver
    {
        private readonly Dictionary<object, int> _handles = new();

        public PositionResolver(params CharacterRuntime[] characters)
        {
            for (int index = 0; index < characters.Length; index++)
            {
                if (characters[index] != null)
                    _handles[characters[index]] = index + 1;
            }
        }

        public BattleVfxTarget ResolveVfxTarget(
            BattleStatusTarget target,
            BattleVfxAnchorType anchorType)
        {
            object identity = target.Ally != null
                ? target.Ally
                : target.Enemy;
            _handles.TryGetValue(identity, out int handle);
            return new BattleVfxTarget(
                new BattleVfxTargetHandle(handle),
                target,
                BattleVfxAnchorSnapshot.FromWorld(
                    new Vector3(handle * 10f, 0f, 0f),
                    Quaternion.identity));
        }
    }
}
