using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleVfxP5Tests
{
    private readonly List<UnityEngine.Object> _createdObjects = new();
    private int _nextHandle;

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
    public void QualityProfile_NormalizesInvalidBudgetValues()
    {
        BattleVfxQualityProfileSO profile = CreateProfile();
        SetField(
            profile,
            "qualityTier",
            (BattleVfxQualityTier)99);
        SetField(
            profile,
            "minimumImportance",
            (BattleVfxImportance)99);
        SetField(profile, "maximumActiveInstances", 0);
        SetField(profile, "maximumScheduledRequests", -10);
        SetField(profile, "prewarmScale", float.NaN);

        profile.ValidateDefinition();

        Assert.That(
            profile.QualityTier,
            Is.EqualTo(BattleVfxQualityTier.High));
        Assert.That(
            profile.MinimumImportance,
            Is.EqualTo(BattleVfxImportance.Low));
        Assert.That(profile.MaximumActiveInstances, Is.EqualTo(1));
        Assert.That(profile.MaximumScheduledRequests, Is.EqualTo(1));
        Assert.That(profile.PrewarmScale, Is.EqualTo(1f));
    }

    [Test]
    public void MinimumImportance_SkipsLowerPriorityCue()
    {
        BattleVfxPlayer player = CreatePlayer();
        BattleVfxQualityProfileSO profile = CreateProfile(
            BattleVfxImportance.High);
        player.ConfigureQuality(profile);
        BattleVfxCueSO low = CreateCue(
            "Low",
            BattleVfxImportance.Low);
        BattleVfxCueSO high = CreateCue(
            "High",
            BattleVfxImportance.High);

        player.Enqueue(CreateRequest(low));
        player.Enqueue(CreateRequest(high));

        Assert.That(player.ActiveInstanceCount, Is.EqualTo(1));
        Assert.That(player.SkippedByQualityCount, Is.EqualTo(1));
        Assert.That(
            player.TryGetActiveInstance(
                high,
                new BattleVfxTargetHandle(_nextHandle),
                out _),
            Is.True);
    }

    [Test]
    public void HigherImportance_EvictsLowerWhenActiveBudgetIsFull()
    {
        BattleVfxPlayer player = CreatePlayer();
        BattleVfxQualityProfileSO profile = CreateProfile(
            maximumActive: 1);
        player.ConfigureQuality(profile);
        BattleVfxCueSO low = CreateCue(
            "Low",
            BattleVfxImportance.Low);
        BattleVfxCueSO high = CreateCue(
            "High",
            BattleVfxImportance.High);
        BattleVfxRequest lowRequest = CreateRequest(low);
        BattleVfxRequest highRequest = CreateRequest(high);

        player.Enqueue(lowRequest);
        player.Enqueue(highRequest);

        Assert.That(player.ActiveInstanceCount, Is.EqualTo(1));
        Assert.That(
            player.TryGetActiveInstance(
                low,
                lowRequest.Target.Handle,
                out _),
            Is.False);
        Assert.That(
            player.TryGetActiveInstance(
                high,
                highRequest.Target.Handle,
                out _),
            Is.True);
        Assert.That(player.SkippedByActiveBudgetCount, Is.Zero);
    }

    [Test]
    public void LowerImportance_CannotEvictHigherActiveCue()
    {
        BattleVfxPlayer player = CreatePlayer();
        BattleVfxQualityProfileSO profile = CreateProfile(
            maximumActive: 1);
        player.ConfigureQuality(profile);
        BattleVfxCueSO high = CreateCue(
            "High",
            BattleVfxImportance.High);
        BattleVfxCueSO low = CreateCue(
            "Low",
            BattleVfxImportance.Low);
        BattleVfxRequest highRequest = CreateRequest(high);
        BattleVfxRequest lowRequest = CreateRequest(low);

        player.Enqueue(highRequest);
        player.Enqueue(lowRequest);

        Assert.That(player.ActiveInstanceCount, Is.EqualTo(1));
        Assert.That(
            player.TryGetActiveInstance(
                high,
                highRequest.Target.Handle,
                out _),
            Is.True);
        Assert.That(
            player.TryGetActiveInstance(
                low,
                lowRequest.Target.Handle,
                out _),
            Is.False);
        Assert.That(player.SkippedByActiveBudgetCount, Is.EqualTo(1));
    }

    [Test]
    public void ScheduledBudget_ReplacesLowerPriorityRequest()
    {
        BattleVfxPlayer player = CreatePlayer();
        BattleVfxQualityProfileSO profile = CreateProfile(
            maximumScheduled: 1);
        player.ConfigureQuality(profile);
        BattleVfxCueSO low = CreateCue(
            "Low",
            BattleVfxImportance.Low);
        BattleVfxCueSO critical = CreateCue(
            "Critical",
            BattleVfxImportance.Critical);
        BattleVfxRequest lowRequest = CreateRequest(low, 0.5f);
        BattleVfxRequest criticalRequest =
            CreateRequest(critical, 0.5f);

        player.Enqueue(lowRequest);
        player.Enqueue(criticalRequest);

        Assert.That(player.ScheduledRequestCount, Is.EqualTo(1));
        Assert.That(
            player.SkippedByScheduledBudgetCount,
            Is.EqualTo(1));

        player.Advance(0.5f, 0f);

        Assert.That(player.ScheduledRequestCount, Is.Zero);
        Assert.That(
            player.TryGetActiveInstance(
                low,
                lowRequest.Target.Handle,
                out _),
            Is.False);
        Assert.That(
            player.TryGetActiveInstance(
                critical,
                criticalRequest.Target.Handle,
                out _),
            Is.True);
    }

    [Test]
    public void PrewarmScale_ReducesCreatedPoolSize()
    {
        BattleVfxPlayer player = CreatePlayer();
        BattleVfxQualityProfileSO profile = CreateProfile();
        SetField(profile, "prewarmScale", 0.5f);
        profile.ValidateDefinition();
        player.ConfigureQuality(profile);
        BattleVfxCueSO cue = CreateCue(
            "Prewarm",
            BattleVfxImportance.Normal,
            prewarmCount: 4);

        player.Enqueue(CreateRequest(cue));

        Assert.That(player.ActiveInstanceCount, Is.EqualTo(1));
        Assert.That(player.PooledInstanceCount, Is.EqualTo(1));
    }

    [Test]
    public void PersistentStop_BypassesScheduledRequestBudget()
    {
        BattleVfxPlayer player = CreatePlayer();
        BattleVfxQualityProfileSO profile = CreateProfile(
            maximumScheduled: 1);
        player.ConfigureQuality(profile);
        BattleVfxCueSO persistent = CreateCue(
            "Persistent",
            BattleVfxImportance.Low);
        SetField(
            persistent,
            "lifetimeMode",
            BattleVfxLifetimeMode.Persistent);
        SetField(
            persistent,
            "stopMode",
            BattleVfxStopMode.Immediate);
        persistent.ValidateDefinition();
        BattleVfxCueSO blocker = CreateCue(
            "Blocker",
            BattleVfxImportance.Critical);
        BattleVfxRequest start = CreateRequest(
            persistent,
            phase: BattleVfxPhase.StatusLoopStart);
        BattleVfxRequest stop = new(
            persistent,
            BattleVfxPhase.StatusLoopStop,
            start.OriginKind,
            start.Source,
            start.Target,
            delaySeconds: 0.5f);

        player.Enqueue(start);
        player.Enqueue(CreateRequest(blocker, 0.5f));
        player.Enqueue(stop);

        Assert.That(player.ActiveInstanceCount, Is.EqualTo(1));
        Assert.That(player.ScheduledRequestCount, Is.EqualTo(2));

        player.Advance(0.5f, 0f);

        Assert.That(
            player.TryGetActiveInstance(
                persistent,
                start.Target.Handle,
                out _),
            Is.False);
    }

    private BattleVfxPlayer CreatePlayer()
    {
        GameObject gameObject = new("BattleVfxPlayer");
        _createdObjects.Add(gameObject);
        return gameObject.AddComponent<BattleVfxPlayer>();
    }

    private BattleVfxQualityProfileSO CreateProfile(
        BattleVfxImportance minimumImportance =
            BattleVfxImportance.Low,
        int maximumActive = 64,
        int maximumScheduled = 64)
    {
        BattleVfxQualityProfileSO profile =
            ScriptableObject.CreateInstance<BattleVfxQualityProfileSO>();
        profile.name = "TestQualityProfile";
        _createdObjects.Add(profile);
        SetField(profile, "minimumImportance", minimumImportance);
        SetField(profile, "maximumActiveInstances", maximumActive);
        SetField(
            profile,
            "maximumScheduledRequests",
            maximumScheduled);
        profile.ValidateDefinition();
        return profile;
    }

    private BattleVfxCueSO CreateCue(
        string cueName,
        BattleVfxImportance importance,
        int prewarmCount = 0)
    {
        GameObject prefab = new($"{cueName}Prefab");
        prefab.SetActive(false);
        _createdObjects.Add(prefab);

        BattleVfxCueSO cue =
            ScriptableObject.CreateInstance<BattleVfxCueSO>();
        cue.name = $"{cueName}Cue";
        cue.RegenerateCueId();
        _createdObjects.Add(cue);
        SetField(cue, "prefab", prefab);
        SetField(cue, "importance", importance);
        SetField(cue, "prewarmCount", prewarmCount);
        SetField(cue, "maximumConcurrent", 16);
        SetField(cue, "lifetimeMode", BattleVfxLifetimeMode.Timed);
        SetField(cue, "duration", 10f);
        cue.ValidateDefinition();
        return cue;
    }

    private BattleVfxRequest CreateRequest(
        BattleVfxCueSO cue,
        float delaySeconds = 0f,
        BattleVfxPhase phase = BattleVfxPhase.Impact)
    {
        GameObject targetObject =
            new($"VfxTarget{_nextHandle + 1}");
        _createdObjects.Add(targetObject);
        CharacterRuntime target =
            targetObject.AddComponent<CharacterRuntime>();
        BattleStatusTarget battleTarget =
            BattleStatusTarget.FromAlly(target);
        BattleVfxTargetHandle handle = new(++_nextHandle);
        return new BattleVfxRequest(
            cue,
            phase,
            BattleEffectOriginKind.CharacterSkill,
            default,
            new BattleVfxTarget(
                handle,
                battleTarget,
                BattleVfxAnchorSnapshot.FromWorld(
                    Vector3.zero,
                    Quaternion.identity)),
            delaySeconds: delaySeconds);
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
}
