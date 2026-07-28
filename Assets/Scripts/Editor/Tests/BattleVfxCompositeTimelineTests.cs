using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleVfxCompositeTimelineTests
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
    public void AnchorSnapshot_PreservesSquareTileFrame()
    {
        BattleVfxAnchorSnapshot snapshot =
            BattleVfxAnchorSnapshot.FromWorld(
                new Vector3(3f, 4f, 5f),
                Quaternion.identity,
                new Vector3(8f, 9f, 10f),
                new Vector3(2f, 0f, 0f),
                new Vector3(0f, 2f, 0f));

        Assert.That(snapshot.IsValid, Is.True);
        Assert.That(snapshot.HasFrame, Is.True);
        Assert.That(
            snapshot.FrameCenter,
            Is.EqualTo(new Vector3(8f, 9f, 10f)));
        Assert.That(snapshot.FrameRight, Is.EqualTo(Vector3.right * 2f));
        Assert.That(snapshot.FrameUp, Is.EqualTo(Vector3.up * 2f));
    }

    [Test]
    public void CompositeCue_SchedulesClipsAndUsesTileRelativePlacement()
    {
        GameObject targetPrefab = CreatePrefab("TargetVfx");
        GameObject casterPrefab = CreatePrefab("CasterVfx");
        BattleVfxClipDefinition targetClip = CreateClip(
            "target",
            targetPrefab,
            BattleVfxPlacementArea.Target,
            new Vector2(10f, 10f),
            0f,
            1f);
        BattleVfxClipDefinition casterClip = CreateClip(
            "caster",
            casterPrefab,
            BattleVfxPlacementArea.Caster,
            Vector2.zero,
            0.5f,
            1f);
        BattleVfxCueSO cue = CreateCompositeCue(
            "Composite",
            targetClip,
            casterClip);
        Assert.That(cue.StageDuration, Is.EqualTo(1.5f).Within(0.0001f));
        BattleVfxPlayer player = CreatePlayer();
        BattleVfxRequest request = CreateRequest(
            cue,
            new BattleVfxAnchorSnapshot(
                BattleVfxCoordinateSpace.World,
                new Vector3(10f, 0f, 0f),
                Quaternion.identity,
                Vector3.right * 2f,
                Vector3.up * 2f),
            new BattleVfxAnchorSnapshot(
                BattleVfxCoordinateSpace.World,
                new Vector3(100f, 100f, 0f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.right * 2f,
                Vector3.up * 2f));

        player.Enqueue(request);

        Assert.That(player.ActiveInstanceCount, Is.EqualTo(1));
        Assert.That(player.ScheduledRequestCount, Is.EqualTo(1));
        Assert.That(
            player.TryGetActiveInstance(
                cue,
                request.Target.Handle,
                "target",
                out GameObject targetInstance),
            Is.True);
        Assert.That(
            targetInstance.transform.position,
            Is.EqualTo(new Vector3(1f, 1f, 0f)));
        Assert.That(
            targetInstance.transform.localScale,
            Is.EqualTo(Vector3.one * 2f));

        player.Advance(0.5f, 0.5f);

        Assert.That(player.ActiveInstanceCount, Is.EqualTo(2));
        Assert.That(player.ScheduledRequestCount, Is.Zero);
        Assert.That(
            player.TryGetActiveInstance(
                cue,
                request.Target.Handle,
                "caster",
                out GameObject casterInstance),
            Is.True);
        Assert.That(
            casterInstance.transform.position,
            Is.EqualTo(new Vector3(9f, -1f, 0f)));
        Assert.That(
            casterInstance.transform.localScale,
            Is.EqualTo(Vector3.one * 2f));
    }

    [Test]
    public void LegacyCue_UsesTargetTileFrameForAutomaticScale()
    {
        GameObject prefab = CreatePrefab("LegacyScaledVfx");
        BattleVfxCueSO cue =
            ScriptableObject.CreateInstance<BattleVfxCueSO>();
        cue.name = "LegacyScaled";
        cue.RegenerateCueId();
        SetField(cue, "prefab", prefab);
        SetField(cue, "lifetimeMode", BattleVfxLifetimeMode.Timed);
        SetField(cue, "duration", 1f);
        cue.ValidateDefinition();
        _createdObjects.Add(cue);
        BattleVfxPlayer player = CreatePlayer();
        BattleVfxRequest request = CreateRequest(
            cue,
            WorldFrame(Vector3.zero),
            BattleVfxAnchorSnapshot.FromWorld(
                Vector3.zero,
                Quaternion.identity,
                Vector3.right * 2f,
                Vector3.up * 2f));

        player.Enqueue(request);

        Assert.That(
            player.TryGetActiveInstance(
                cue,
                request.Target.Handle,
                out GameObject instance),
            Is.True);
        Assert.That(
            instance.transform.localScale,
            Is.EqualTo(Vector3.one * 2f));
    }

    [Test]
    public void StretchPlayback_IsRestoredWhenPooledInstanceIsReused()
    {
        GameObject prefab = CreatePrefab("ParticleVfx");
        ParticleSystem particle = prefab.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule prefabMain = particle.main;
        prefabMain.duration = 2f;

        BattleVfxClipDefinition stretched = CreateClip(
            "stretched",
            prefab,
            BattleVfxPlacementArea.Target,
            new Vector2(5f, 5f),
            0f,
            0.5f);
        SetField(
            stretched,
            "playbackFit",
            BattleVfxPlaybackFit.StretchToDuration);
        BattleVfxCueSO stretchedCue = CreateCompositeCue(
            "Stretched",
            stretched);
        BattleVfxPlayer player = CreatePlayer();
        BattleVfxRequest stretchedRequest = CreateRequest(
            stretchedCue,
            WorldFrame(Vector3.zero),
            WorldFrame(Vector3.zero));

        player.Enqueue(stretchedRequest);
        Assert.That(
            player.TryGetActiveInstance(
                stretchedCue,
                stretchedRequest.Target.Handle,
                "stretched",
                out GameObject stretchedInstance),
            Is.True);
        float stretchedSpeed =
            stretchedInstance.GetComponent<ParticleSystem>()
                .main.simulationSpeed;
        Assert.That(stretchedSpeed, Is.GreaterThan(1f));

        player.Advance(0.6f, 0.6f);
        Assert.That(player.ActiveInstanceCount, Is.Zero);

        BattleVfxClipDefinition natural = CreateClip(
            "natural",
            prefab,
            BattleVfxPlacementArea.Target,
            new Vector2(5f, 5f),
            0f,
            1f);
        BattleVfxCueSO naturalCue = CreateCompositeCue(
            "Natural",
            natural);
        BattleVfxRequest naturalRequest = CreateRequest(
            naturalCue,
            WorldFrame(Vector3.zero),
            WorldFrame(Vector3.zero));
        player.Enqueue(naturalRequest);

        Assert.That(
            player.TryGetActiveInstance(
                naturalCue,
                naturalRequest.Target.Handle,
                "natural",
                out GameObject naturalInstance),
            Is.True);
        Assert.That(
            naturalInstance.GetComponent<ParticleSystem>()
                .main.simulationSpeed,
            Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void LegacyCue_MigratesWithoutChangingConfiguredTransform()
    {
        GameObject prefab = CreatePrefab("LegacyVfx");
        BattleVfxCueSO cue =
            ScriptableObject.CreateInstance<BattleVfxCueSO>();
        _createdObjects.Add(cue);
        SetField(cue, "prefab", prefab);
        SetField(cue, "localPosition", new Vector3(1f, 2f, 3f));
        SetField(cue, "localScale", new Vector3(2f, 3f, 4f));

        bool migrated = cue.MigrateLegacyPrefabToTimeline();

        Assert.That(migrated, Is.True);
        Assert.That(cue.UsesClipTimeline, Is.True);
        Assert.That(cue.Clips, Has.Count.EqualTo(1));
        Assert.That(cue.Clips[0].Prefab, Is.SameAs(prefab));
        Assert.That(
            cue.Clips[0].ScaleMode,
            Is.EqualTo(BattleVfxScaleMode.TileRelative));
        Assert.That(
            cue.Clips[0].LocalPosition,
            Is.EqualTo(new Vector3(1f, 2f, 3f)));
        Assert.That(
            cue.Clips[0].LocalScale,
            Is.EqualTo(new Vector3(2f, 3f, 4f)));
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

    private BattleVfxCueSO CreateCompositeCue(
        string objectName,
        params BattleVfxClipDefinition[] clips)
    {
        BattleVfxCueSO cue =
            ScriptableObject.CreateInstance<BattleVfxCueSO>();
        cue.name = objectName;
        cue.RegenerateCueId();
        SetField(
            cue,
            "clips",
            new List<BattleVfxClipDefinition>(clips));
        cue.ValidateDefinition();
        _createdObjects.Add(cue);
        return cue;
    }

    private static BattleVfxClipDefinition CreateClip(
        string clipId,
        GameObject prefab,
        BattleVfxPlacementArea placement,
        Vector2 gridPosition,
        float startTime,
        float duration)
    {
        BattleVfxClipDefinition clip = new();
        SetField(clip, "clipId", clipId);
        SetField(clip, "prefab", prefab);
        SetField(clip, "placementArea", placement);
        SetField(clip, "gridPosition", gridPosition);
        SetField(clip, "startTime", startTime);
        SetField(clip, "duration", duration);
        SetField(clip, "lifetimeMode", BattleVfxLifetimeMode.Timed);
        SetField(clip, "scaleMode", BattleVfxScaleMode.TileRelative);
        return clip;
    }

    private BattleVfxRequest CreateRequest(
        BattleVfxCueSO cue,
        BattleVfxAnchorSnapshot sourceAnchor,
        BattleVfxAnchorSnapshot targetAnchor)
    {
        CharacterRuntime source = CreateCharacter("Source");
        CharacterRuntime target = CreateCharacter("Target");
        BattleStatusTarget sourceTarget =
            BattleStatusTarget.FromAlly(source);
        BattleStatusTarget targetTarget =
            BattleStatusTarget.FromAlly(target);
        return new BattleVfxRequest(
            cue,
            BattleVfxPhase.Impact,
            BattleEffectOriginKind.CharacterSkill,
            sourceTarget,
            new BattleVfxTarget(
                new BattleVfxTargetHandle(10),
                sourceTarget,
                sourceAnchor),
            new BattleVfxTarget(
                new BattleVfxTargetHandle(20),
                targetTarget,
                targetAnchor));
    }

    private CharacterRuntime CreateCharacter(string objectName)
    {
        GameObject gameObject = new(objectName);
        _createdObjects.Add(gameObject);
        return gameObject.AddComponent<CharacterRuntime>();
    }

    private static BattleVfxAnchorSnapshot WorldFrame(Vector3 center)
    {
        return BattleVfxAnchorSnapshot.FromWorld(
            center,
            Quaternion.identity,
            Vector3.right,
            Vector3.up);
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
