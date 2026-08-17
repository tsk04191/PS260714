using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using static TestReflection;

public sealed class BattleVfxP1Tests
{
    private readonly List<UnityEngine.Object> _createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = _createdObjects.Count - 1; index >= 0; index--)
        {
            if (_createdObjects[index] != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    _createdObjects[index]);
            }
        }

        _createdObjects.Clear();
    }

    [Test]
    public void TimedCue_SpawnsAtWorldAnchorAndReturnsToPool()
    {
        GameObject prefab = CreatePrefab("TimedPrefab");
        prefab.transform.localScale = new Vector3(2f, 3f, 4f);
        BattleVfxCueSO cue = CreateCue(
            prefab,
            BattleVfxLifetimeMode.Timed,
            0.25f,
            BattleVfxStopMode.Immediate,
            1,
            4);
        SetField(cue, "localPosition", new Vector3(1f, 2f, 3f));
        SetField(cue, "localScale", new Vector3(0.5f, 2f, 1f));
        BattleVfxPlayer player = CreatePlayer();
        CharacterRuntime target = CreateTarget();
        BattleVfxTargetHandle handle = new(11);

        player.Enqueue(CreateRequest(
            cue,
            BattleVfxPhase.Impact,
            target,
            handle,
            new Vector3(10f, 20f, 30f)));

        Assert.That(player.ActiveInstanceCount, Is.EqualTo(1));
        Assert.That(
            player.TryGetActiveInstance(cue, handle, out GameObject instance),
            Is.True);
        Assert.That(
            instance.transform.position,
            Is.EqualTo(new Vector3(11f, 22f, 33f)));
        Assert.That(
            instance.transform.localScale,
            Is.EqualTo(new Vector3(1f, 6f, 4f)));

        player.Advance(0.3f, 0.3f);

        Assert.That(player.ActiveInstanceCount, Is.Zero);
        Assert.That(player.PooledInstanceCount, Is.EqualTo(1));
        Assert.That(instance.activeSelf, Is.False);
    }

    [Test]
    public void PersistentCue_ReapplyDoesNotDuplicateAndStopRecycles()
    {
        GameObject prefab = CreatePrefab("PersistentPrefab");
        BattleVfxCueSO cue = CreateCue(
            prefab,
            BattleVfxLifetimeMode.Persistent,
            1f,
            BattleVfxStopMode.Immediate,
            0,
            4);
        BattleVfxPlayer player = CreatePlayer();
        CharacterRuntime target = CreateTarget();
        BattleVfxTargetHandle handle = new(22);
        BattleVfxRequest start = CreateRequest(
            cue,
            BattleVfxPhase.StatusLoopStart,
            target,
            handle,
            Vector3.one);

        player.Enqueue(start);
        Assert.That(
            player.TryGetActiveInstance(cue, handle, out GameObject first),
            Is.True);

        player.Enqueue(start);

        Assert.That(player.ActiveInstanceCount, Is.EqualTo(1));
        Assert.That(
            player.TryGetActiveInstance(cue, handle, out GameObject second),
            Is.True);
        Assert.That(second, Is.SameAs(first));

        player.Enqueue(CreateRequest(
            cue,
            BattleVfxPhase.StatusLoopStop,
            target,
            handle,
            Vector3.one));

        Assert.That(player.ActiveInstanceCount, Is.Zero);
        Assert.That(player.PooledInstanceCount, Is.EqualTo(1));
    }

    [Test]
    public void FollowTarget_RefreshesAnchorThroughResolver()
    {
        GameObject prefab = CreatePrefab("FollowPrefab");
        BattleVfxCueSO cue = CreateCue(
            prefab,
            BattleVfxLifetimeMode.Timed,
            2f,
            BattleVfxStopMode.Immediate,
            0,
            4);
        SetField(cue, "attachMode", BattleVfxAttachMode.FollowTarget);
        BattleVfxPlayer player = CreatePlayer();
        CharacterRuntime target = CreateTarget();
        BattleStatusTarget battleTarget =
            BattleStatusTarget.FromAlly(target);
        BattleVfxTargetHandle handle = new(33);
        MovingResolver resolver = new(
            battleTarget,
            handle,
            new Vector3(2f, 3f, 4f));
        player.BindTargetResolver(resolver);

        player.Enqueue(new BattleVfxRequest(
            cue,
            BattleVfxPhase.StatusLoopStart,
            BattleEffectOriginKind.StatusEffect,
            default,
            new BattleVfxTarget(
                handle,
                battleTarget,
                BattleVfxAnchorSnapshot.FromWorld(
                    Vector3.zero,
                    Quaternion.identity))));
        Assert.That(
            player.TryGetActiveInstance(cue, handle, out GameObject instance),
            Is.True);
        Assert.That(
            instance.transform.position,
            Is.EqualTo(new Vector3(2f, 3f, 4f)));

        resolver.Position = new Vector3(8f, 9f, 10f);
        player.Advance(0f, 0f);

        Assert.That(
            instance.transform.position,
            Is.EqualTo(new Vector3(8f, 9f, 10f)));
    }

    [Test]
    public void ConcurrentLimit_RecyclesOldestCueInstance()
    {
        GameObject prefab = CreatePrefab("LimitedPrefab");
        BattleVfxCueSO cue = CreateCue(
            prefab,
            BattleVfxLifetimeMode.Timed,
            2f,
            BattleVfxStopMode.Immediate,
            0,
            1);
        BattleVfxPlayer player = CreatePlayer();
        CharacterRuntime target = CreateTarget();
        BattleVfxTargetHandle firstHandle = new(41);
        BattleVfxTargetHandle secondHandle = new(42);

        player.Enqueue(CreateRequest(
            cue,
            BattleVfxPhase.Impact,
            target,
            firstHandle,
            Vector3.zero));
        player.Enqueue(CreateRequest(
            cue,
            BattleVfxPhase.Impact,
            target,
            secondHandle,
            Vector3.one));

        Assert.That(player.ActiveInstanceCount, Is.EqualTo(1));
        Assert.That(
            player.TryGetActiveInstance(cue, firstHandle, out _),
            Is.False);
        Assert.That(
            player.TryGetActiveInstance(
                cue,
                secondHandle,
                out GameObject active),
            Is.True);
        Assert.That(active.transform.position, Is.EqualTo(Vector3.one));
    }

    private BattleVfxPlayer CreatePlayer()
    {
        GameObject gameObject = new("BattleVfxPlayer");
        _createdObjects.Add(gameObject);
        return gameObject.AddComponent<BattleVfxPlayer>();
    }

    private GameObject CreatePrefab(string objectName)
    {
        GameObject prefab = new(objectName);
        prefab.SetActive(false);
        _createdObjects.Add(prefab);
        return prefab;
    }

    private CharacterRuntime CreateTarget()
    {
        GameObject gameObject = new("VfxTarget");
        _createdObjects.Add(gameObject);
        return gameObject.AddComponent<CharacterRuntime>();
    }

    private BattleVfxCueSO CreateCue(
        GameObject prefab,
        BattleVfxLifetimeMode lifetimeMode,
        float duration,
        BattleVfxStopMode stopMode,
        int prewarmCount,
        int maximumConcurrent)
    {
        BattleVfxCueSO cue =
            ScriptableObject.CreateInstance<BattleVfxCueSO>();
        cue.name = $"Cue_{prefab.name}";
        _createdObjects.Add(cue);
        SetField(cue, "prefab", prefab);
        SetField(cue, "lifetimeMode", lifetimeMode);
        SetField(cue, "duration", duration);
        SetField(cue, "stopMode", stopMode);
        SetField(cue, "prewarmCount", prewarmCount);
        SetField(cue, "maximumConcurrent", maximumConcurrent);
        return cue;
    }

    private static BattleVfxRequest CreateRequest(
        BattleVfxCueSO cue,
        BattleVfxPhase phase,
        CharacterRuntime target,
        BattleVfxTargetHandle handle,
        Vector3 position)
    {
        BattleStatusTarget battleTarget =
            BattleStatusTarget.FromAlly(target);
        return new BattleVfxRequest(
            cue,
            phase,
            BattleEffectOriginKind.CharacterAttack,
            default,
            new BattleVfxTarget(
                handle,
                battleTarget,
                BattleVfxAnchorSnapshot.FromWorld(
                    position,
                    Quaternion.identity)));
    }

    private sealed class MovingResolver : IBattleVfxTargetResolver
    {
        private readonly BattleStatusTarget _target;
        private readonly BattleVfxTargetHandle _handle;

        public Vector3 Position { get; set; }

        public MovingResolver(
            BattleStatusTarget target,
            BattleVfxTargetHandle handle,
            Vector3 position)
        {
            _target = target;
            _handle = handle;
            Position = position;
        }

        public BattleVfxTarget ResolveVfxTarget(
            BattleStatusTarget target,
            BattleVfxAnchorType anchorType)
        {
            return new BattleVfxTarget(
                _handle,
                _target,
                BattleVfxAnchorSnapshot.FromWorld(
                    Position,
                    Quaternion.identity));
        }
    }
}
