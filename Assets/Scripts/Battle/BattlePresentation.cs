using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleVfxCoordinateSpace
{
    Screen = 0,
    World = 1
}

public enum BattleVfxPhase
{
    Cast = 0,
    Projectile = 1,
    Impact = 2,
    StatusApply = 3,
    StatusLoopStart = 4,
    StatusTick = 5,
    StatusLoopStop = 6,
    StatusRemove = 7,
    Death = 8,
    Spawn = 9
}

public readonly struct BattleVfxTargetHandle :
    IEquatable<BattleVfxTargetHandle>
{
    public int Value { get; }
    public bool IsValid => Value > 0;

    public BattleVfxTargetHandle(int value)
    {
        Value = Mathf.Max(0, value);
    }

    public bool Equals(BattleVfxTargetHandle other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object obj)
    {
        return obj is BattleVfxTargetHandle other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value;
    }

    public static bool operator ==(
        BattleVfxTargetHandle left,
        BattleVfxTargetHandle right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(
        BattleVfxTargetHandle left,
        BattleVfxTargetHandle right)
    {
        return !left.Equals(right);
    }
}

public readonly struct BattleVfxAnchorSnapshot
{
    public BattleVfxCoordinateSpace CoordinateSpace { get; }
    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public bool IsValid { get; }

    public BattleVfxAnchorSnapshot(
        BattleVfxCoordinateSpace coordinateSpace,
        Vector3 position,
        Quaternion rotation)
    {
        CoordinateSpace = coordinateSpace;
        Position = position;
        Rotation = rotation;
        IsValid = IsFinite(position) && IsFinite(rotation);
    }

    public static BattleVfxAnchorSnapshot FromScreen(Vector2 position)
    {
        return new BattleVfxAnchorSnapshot(
            BattleVfxCoordinateSpace.Screen,
            new Vector3(position.x, position.y, 0f),
            Quaternion.identity);
    }

    public static BattleVfxAnchorSnapshot FromWorld(
        Vector3 position,
        Quaternion rotation)
    {
        return new BattleVfxAnchorSnapshot(
            BattleVfxCoordinateSpace.World,
            position,
            rotation);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z) &&
               IsFinite(value.w);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

public readonly struct BattleVfxTarget
{
    public BattleVfxTargetHandle Handle { get; }
    public BattleStatusTarget BattleTarget { get; }
    public BattleVfxAnchorSnapshot Anchor { get; }
    public bool IsValid => Handle.IsValid && BattleTarget.IsValid;
    public bool HasAnchor => Anchor.IsValid;

    public BattleVfxTarget(
        BattleVfxTargetHandle handle,
        BattleStatusTarget battleTarget,
        BattleVfxAnchorSnapshot anchor)
    {
        Handle = handle;
        BattleTarget = battleTarget;
        Anchor = anchor;
    }
}

public interface IBattleVfxAnchorProvider
{
    bool TryGetVfxAnchor(
        BattleVfxAnchorType anchorType,
        out BattleVfxAnchorSnapshot snapshot);
}

public static class BattleVfxUiAnchorUtility
{
    public static bool TryCreateScreenAnchor(
        RectTransform rectTransform,
        BattleVfxAnchorType anchorType,
        out BattleVfxAnchorSnapshot snapshot)
    {
        snapshot = default;
        if (rectTransform == null || !rectTransform.gameObject.activeInHierarchy)
            return false;

        Vector2 normalizedPoint = anchorType switch
        {
            BattleVfxAnchorType.Ground => new Vector2(0.5f, 0.08f),
            BattleVfxAnchorType.Head => new Vector2(0.5f, 0.9f),
            BattleVfxAnchorType.Muzzle => new Vector2(0.78f, 0.56f),
            BattleVfxAnchorType.Status => new Vector2(0.5f, 0.78f),
            _ => new Vector2(0.5f, 0.5f)
        };
        Rect rect = rectTransform.rect;
        Vector3 localPoint = new(
            Mathf.Lerp(rect.xMin, rect.xMax, normalizedPoint.x),
            Mathf.Lerp(rect.yMin, rect.yMax, normalizedPoint.y),
            0f);
        Vector3 worldPoint = rectTransform.TransformPoint(localPoint);
        Canvas rootCanvas =
            rectTransform.GetComponentInParent<Canvas>()?.rootCanvas;
        Camera canvasCamera =
            rootCanvas != null &&
            rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            canvasCamera,
            worldPoint);
        snapshot = BattleVfxAnchorSnapshot.FromScreen(screenPoint);
        return snapshot.IsValid;
    }
}

public interface IBattleVfxTargetResolver
{
    BattleVfxTarget ResolveVfxTarget(
        BattleStatusTarget target,
        BattleVfxAnchorType anchorType);
}

public interface IBattlePresentationEffectDefinition
{
    BattleVfxCueSO CastVfxCue { get; }
    BattleVfxCueSO ProjectileVfxCue { get; }
    BattleVfxCueSO ImpactVfxCue { get; }
}

public interface IBattlePresentationUnitDefinition
{
    BattleVfxCueSO SpawnVfxCue { get; }
    BattleVfxCueSO DeathVfxCue { get; }
}

public enum BattleUnitLifecycleType
{
    Spawned = 0,
    Defeated = 1
}

public readonly struct BattleUnitLifecycleEvent
{
    public BattleUnitLifecycleType Type { get; }
    public BattleStatusTarget Target { get; }
    public IBattlePresentationUnitDefinition Definition { get; }
    public float DelaySeconds { get; }
    public bool IsValid =>
        Target.IsValid &&
        Definition != null;

    public BattleUnitLifecycleEvent(
        BattleUnitLifecycleType type,
        BattleStatusTarget target,
        IBattlePresentationUnitDefinition definition,
        float delaySeconds = 0f)
    {
        Type = type;
        Target = target;
        Definition = definition;
        DelaySeconds =
            float.IsNaN(delaySeconds) || float.IsInfinity(delaySeconds)
                ? 0f
                : Mathf.Max(0f, delaySeconds);
    }
}

public readonly struct BattleEffectResolvedEvent
{
    private readonly BattleStatusTarget[] _targets;

    public BattleEffectOriginKind OriginKind { get; }
    public BattleStatusTarget Source { get; }
    public CharacterTargetFaction TargetFaction { get; }
    public IBattleEffectDefinition Effect { get; }
    public BattleEffectResult Result { get; }
    public IReadOnlyList<BattleStatusTarget> Targets =>
        _targets ?? Array.Empty<BattleStatusTarget>();
    public bool IsValid =>
        Effect != null &&
        Result.Attempted;

    public BattleEffectResolvedEvent(
        BattleEffectContext context,
        IBattleEffectDefinition effect,
        BattleEffectResult result)
    {
        OriginKind = context.OriginKind;
        Source = context.SourceTarget;
        TargetFaction = context.TargetFaction;
        Effect = effect;
        Result = result;
        _targets = SnapshotTargets(context);
    }

    private static BattleStatusTarget[] SnapshotTargets(
        BattleEffectContext context)
    {
        List<BattleStatusTarget> targets = new();
        if (context.TargetFaction == CharacterTargetFaction.Ally)
        {
            HashSet<IBattleCharacter> unique = new();
            foreach (IBattleCharacter ally in context.AllyTargets)
            {
                if (ally != null && unique.Add(ally))
                    targets.Add(BattleStatusTarget.FromAlly(ally));
            }
        }
        else
        {
            HashSet<EnemyRuntime> unique = new();
            foreach (EnemyRuntime enemy in context.EnemyTargets)
            {
                if (enemy != null && unique.Add(enemy))
                    targets.Add(BattleStatusTarget.FromEnemy(enemy));
            }
        }

        if (targets.Count == 0 && context.SourceTarget.IsValid)
            targets.Add(context.SourceTarget);
        return targets.ToArray();
    }
}

public interface IBattlePresentationEventSource
{
    event Action<BattleEffectResolvedEvent> EffectResolved;
    event Action<StatusEffectLifecycleEvent> StatusLifecycle;
    event Action<BattleUnitLifecycleEvent> UnitLifecycle;
}

public interface IBattlePresentationEventPublisher :
    IBattlePresentationEventSource
{
    void PublishEffectResolved(BattleEffectResolvedEvent eventData);
    void PublishStatusLifecycle(StatusEffectLifecycleEvent eventData);
    void PublishUnitLifecycle(BattleUnitLifecycleEvent eventData);
}

public readonly struct BattleVfxRequest
{
    public BattleVfxCueSO Cue { get; }
    public BattleVfxPhase Phase { get; }
    public BattleEffectOriginKind OriginKind { get; }
    public BattleStatusTarget Source { get; }
    public BattleVfxTarget SourceTarget { get; }
    public BattleVfxTarget Target { get; }
    public StatusEffectSO StatusEffect { get; }
    public int Amount { get; }
    public int StackCount { get; }
    public int OccurrenceCount { get; }
    public float DelaySeconds { get; }
    public bool DelayUsesBattleTime { get; }
    public bool IsValid => Cue != null;

    public BattleVfxRequest(
        BattleVfxCueSO cue,
        BattleVfxPhase phase,
        BattleEffectOriginKind originKind,
        BattleStatusTarget source,
        BattleVfxTarget target,
        StatusEffectSO statusEffect = null,
        int amount = 0,
        int stackCount = 0,
        int occurrenceCount = 1,
        float delaySeconds = 0f,
        bool delayUsesBattleTime = true)
        : this(
            cue,
            phase,
            originKind,
            source,
            default,
            target,
            statusEffect,
            amount,
            stackCount,
            occurrenceCount,
            delaySeconds,
            delayUsesBattleTime)
    {
    }

    public BattleVfxRequest(
        BattleVfxCueSO cue,
        BattleVfxPhase phase,
        BattleEffectOriginKind originKind,
        BattleStatusTarget source,
        BattleVfxTarget sourceTarget,
        BattleVfxTarget target,
        StatusEffectSO statusEffect = null,
        int amount = 0,
        int stackCount = 0,
        int occurrenceCount = 1,
        float delaySeconds = 0f,
        bool delayUsesBattleTime = true)
    {
        Cue = cue;
        Phase = phase;
        OriginKind = originKind;
        Source = source;
        SourceTarget = sourceTarget;
        Target = target;
        StatusEffect = statusEffect;
        Amount = Mathf.Max(0, amount);
        StackCount = Mathf.Max(0, stackCount);
        OccurrenceCount = Mathf.Max(1, occurrenceCount);
        DelaySeconds =
            float.IsNaN(delaySeconds) || float.IsInfinity(delaySeconds)
                ? 0f
                : Mathf.Max(0f, delaySeconds);
        DelayUsesBattleTime = delayUsesBattleTime;
    }
}

public interface IBattleVfxRequestSink
{
    void Enqueue(BattleVfxRequest request);
}

public sealed class BattlePresentationDispatcher : IDisposable
{
    private IBattlePresentationEventSource _source;
    private IBattleVfxTargetResolver _targetResolver;
    private readonly IBattleVfxRequestSink _sink;

    public BattlePresentationDispatcher(IBattleVfxRequestSink sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public void Bind(
        IBattlePresentationEventSource source,
        IBattleVfxTargetResolver targetResolver = null)
    {
        if (ReferenceEquals(_source, source) &&
            ReferenceEquals(_targetResolver, targetResolver))
        {
            return;
        }

        Unbind();
        _source = source;
        _targetResolver = targetResolver;
        if (_source == null)
            return;

        _source.EffectResolved += HandleEffectResolved;
        _source.StatusLifecycle += HandleStatusLifecycle;
        _source.UnitLifecycle += HandleUnitLifecycle;
    }

    public void Unbind()
    {
        if (_source != null)
        {
            _source.EffectResolved -= HandleEffectResolved;
            _source.StatusLifecycle -= HandleStatusLifecycle;
            _source.UnitLifecycle -= HandleUnitLifecycle;
        }

        _source = null;
        _targetResolver = null;
    }

    public void Dispose()
    {
        Unbind();
    }

    private void HandleEffectResolved(BattleEffectResolvedEvent eventData)
    {
        if (!eventData.IsValid || !eventData.Result.Succeeded ||
            eventData.Effect is not IBattlePresentationEffectDefinition
                presentation)
        {
            return;
        }

        BattleVfxCueSO castCue = presentation.CastVfxCue;
        BattleVfxCueSO projectileCue = presentation.ProjectileVfxCue;
        float projectileDelay = castCue != null
            ? castCue.StageDuration
            : 0f;
        float impactDelay = projectileDelay +
                            (projectileCue != null
                                ? projectileCue.StageDuration
                                : 0f);

        EnqueueCast(eventData, castCue);
        foreach (BattleStatusTarget target in eventData.Targets)
        {
            EnqueueProjectile(
                eventData,
                projectileCue,
                target,
                projectileDelay);
            EnqueueImpact(
                eventData,
                presentation.ImpactVfxCue,
                target,
                impactDelay);
        }
    }

    private void EnqueueCast(
        BattleEffectResolvedEvent eventData,
        BattleVfxCueSO cue)
    {
        if (cue == null)
            return;

        BattleStatusTarget target = eventData.Source.IsValid
            ? eventData.Source
            : GetFirstTarget(eventData.Targets);
        if (!target.IsValid)
            return;

        Enqueue(new BattleVfxRequest(
            cue,
            BattleVfxPhase.Cast,
            eventData.OriginKind,
            eventData.Source,
            Resolve(target, cue.AnchorType),
            eventData.Effect.StatusEffect,
            eventData.Result.DamageDealt));
    }

    private void EnqueueProjectile(
        BattleEffectResolvedEvent eventData,
        BattleVfxCueSO cue,
        BattleStatusTarget target,
        float delaySeconds)
    {
        if (cue == null)
            return;

        BattleStatusTarget source = eventData.Source.IsValid
            ? eventData.Source
            : target;
        Enqueue(new BattleVfxRequest(
            cue,
            BattleVfxPhase.Projectile,
            eventData.OriginKind,
            eventData.Source,
            Resolve(source, cue.MotionSourceAnchorType),
            Resolve(target, cue.AnchorType),
            eventData.Effect.StatusEffect,
            eventData.Result.DamageDealt,
            delaySeconds: delaySeconds,
            delayUsesBattleTime: cue.UseBattleTime));
    }

    private void EnqueueImpact(
        BattleEffectResolvedEvent eventData,
        BattleVfxCueSO cue,
        BattleStatusTarget target,
        float delaySeconds)
    {
        if (cue == null)
            return;

        Enqueue(new BattleVfxRequest(
            cue,
            BattleVfxPhase.Impact,
            eventData.OriginKind,
            eventData.Source,
            Resolve(target, cue.AnchorType),
            eventData.Effect.StatusEffect,
            eventData.Result.DamageDealt,
            delaySeconds: delaySeconds,
            delayUsesBattleTime: cue.UseBattleTime));
    }

    private static BattleStatusTarget GetFirstTarget(
        IReadOnlyList<BattleStatusTarget> targets)
    {
        if (targets == null)
            return default;
        for (int index = 0; index < targets.Count; index++)
        {
            if (targets[index].IsValid)
                return targets[index];
        }

        return default;
    }

    private void HandleUnitLifecycle(BattleUnitLifecycleEvent eventData)
    {
        if (!eventData.IsValid)
            return;

        BattleVfxCueSO cue;
        BattleVfxPhase phase;
        switch (eventData.Type)
        {
            case BattleUnitLifecycleType.Spawned:
                cue = eventData.Definition.SpawnVfxCue;
                phase = BattleVfxPhase.Spawn;
                break;

            case BattleUnitLifecycleType.Defeated:
                cue = eventData.Definition.DeathVfxCue;
                phase = BattleVfxPhase.Death;
                break;

            default:
                return;
        }

        if (cue == null)
            return;

        Enqueue(new BattleVfxRequest(
            cue,
            phase,
            BattleEffectOriginKind.BattleLifecycle,
            eventData.Target,
            Resolve(eventData.Target, cue.AnchorType),
            delaySeconds: eventData.DelaySeconds,
            delayUsesBattleTime: cue.UseBattleTime));
    }

    private void HandleStatusLifecycle(
        StatusEffectLifecycleEvent eventData)
    {
        if (!eventData.IsValid)
            return;

        BattleStatusTarget source = eventData.Source != null
            ? BattleStatusTarget.FromAlly(eventData.Source)
            : default;
        switch (eventData.Trigger)
        {
            case StatusEffectLifecycleTrigger.OnApply:
            case StatusEffectLifecycleTrigger.OnReapply:
                EnqueueStatus(
                    eventData.Definition.ApplyVfxCue,
                    BattleVfxPhase.StatusApply,
                    eventData,
                    source);
                EnqueueStatus(
                    eventData.Definition.LoopVfxCue,
                    BattleVfxPhase.StatusLoopStart,
                    eventData,
                    source);
                break;

            case StatusEffectLifecycleTrigger.OnTick:
                EnqueueStatus(
                    eventData.Definition.TickVfxCue,
                    BattleVfxPhase.StatusTick,
                    eventData,
                    source);
                break;

            case StatusEffectLifecycleTrigger.OnExpire:
            case StatusEffectLifecycleTrigger.OnRemove:
                EnqueueStatus(
                    eventData.Definition.LoopVfxCue,
                    BattleVfxPhase.StatusLoopStop,
                    eventData,
                    source);
                EnqueueStatus(
                    eventData.Definition.RemoveVfxCue,
                    BattleVfxPhase.StatusRemove,
                    eventData,
                    source);
                break;
        }
    }

    private void EnqueueStatus(
        BattleVfxCueSO cue,
        BattleVfxPhase phase,
        StatusEffectLifecycleEvent eventData,
        BattleStatusTarget source)
    {
        if (cue == null)
            return;

        Enqueue(new BattleVfxRequest(
            cue,
            phase,
            BattleEffectOriginKind.StatusEffect,
            source,
            Resolve(eventData.Target, cue.AnchorType),
            eventData.Definition,
            0,
            eventData.CurrentStacks,
            eventData.OccurrenceCount));
    }

    private BattleVfxTarget Resolve(
        BattleStatusTarget target,
        BattleVfxAnchorType anchorType)
    {
        return _targetResolver != null
            ? _targetResolver.ResolveVfxTarget(target, anchorType)
            : new BattleVfxTarget(default, target, default);
    }

    private void Enqueue(BattleVfxRequest request)
    {
        if (request.IsValid)
            _sink.Enqueue(request);
    }
}
