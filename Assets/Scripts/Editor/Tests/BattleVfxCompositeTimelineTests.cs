using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

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

    [Test]
    public void CompositeCue_RoutesEmbeddedAudioAndPreservesItsSettings()
    {
        AudioMixerGroup sfxGroup = LoadSfxMixerGroup();
        GameObject prefab = CreatePrefab("EmbeddedAudioVfx");
        AudioSource authoredSource = prefab.AddComponent<AudioSource>();
        AudioClip authoredClip = AudioClip.Create(
            "EmbeddedAudio",
            64,
            1,
            44100,
            false);
        _createdObjects.Add(authoredClip);
        authoredSource.clip = authoredClip;
        authoredSource.playOnAwake = true;
        authoredSource.loop = true;
        authoredSource.volume = 0.35f;
        authoredSource.pitch = 0.8f;
        authoredSource.spatialBlend = 0.65f;
        authoredSource.dopplerLevel = 2f;
        BattleVfxClipDefinition clip = CreateClip(
            "embedded-audio",
            prefab,
            BattleVfxPlacementArea.Target,
            new Vector2(5f, 5f),
            0f,
            1f);
        BattleVfxCueSO cue = CreateCompositeCue(
            "EmbeddedAudioCue",
            clip);
        BattleVfxPlayer player = CreatePlayer();
        player.ConfigureAudioMixerGroup(sfxGroup);
        BattleVfxRequest firstRequest = CreateRequest(
            cue,
            WorldFrame(Vector3.zero),
            WorldFrame(Vector3.zero));

        player.Enqueue(firstRequest);

        Assert.That(
            player.TryGetActiveInstance(
                cue,
                firstRequest.Target.Handle,
                clip.ClipId,
                out GameObject firstInstance),
            Is.True);
        AudioSource firstSource = firstInstance.GetComponent<AudioSource>();
        Assert.That(firstSource.outputAudioMixerGroup, Is.SameAs(sfxGroup));
        Assert.That(firstSource.clip, Is.SameAs(authoredClip));
        Assert.That(firstSource.playOnAwake, Is.True);
        Assert.That(firstSource.loop, Is.True);
        Assert.That(firstSource.volume, Is.EqualTo(0.35f));
        Assert.That(firstSource.pitch, Is.EqualTo(0.8f));
        Assert.That(firstSource.spatialBlend, Is.EqualTo(0.65f));
        Assert.That(firstSource.dopplerLevel, Is.EqualTo(2f));

        player.Advance(1.1f, 1.1f);
        BattleVfxRequest secondRequest = CreateRequest(
            cue,
            WorldFrame(Vector3.zero),
            WorldFrame(Vector3.zero));
        player.Enqueue(secondRequest);

        Assert.That(
            player.TryGetActiveInstance(
                cue,
                secondRequest.Target.Handle,
                clip.ClipId,
                out GameObject reusedInstance),
            Is.True);
        Assert.That(reusedInstance, Is.SameAs(firstInstance));
        Assert.That(
            reusedInstance.GetComponent<AudioSource>()
                .outputAudioMixerGroup,
            Is.SameAs(sfxGroup));
    }

    [Test]
    public void AudioOnlyCue_RoutesThePlayerAudioSourceToSfx()
    {
        AudioMixerGroup sfxGroup = LoadSfxMixerGroup();
        AudioClip audioClip = AudioClip.Create(
            "CueAudio",
            64,
            1,
            44100,
            false);
        _createdObjects.Add(audioClip);
        BattleVfxCueSO cue =
            ScriptableObject.CreateInstance<BattleVfxCueSO>();
        cue.name = "AudioOnly";
        cue.RegenerateCueId();
        SetField(cue, "audioClip", audioClip);
        cue.ValidateDefinition();
        _createdObjects.Add(cue);
        BattleVfxPlayer player = CreatePlayer();
        player.ConfigureAudioMixerGroup(sfxGroup);

        player.Enqueue(CreateRequest(
            cue,
            WorldFrame(Vector3.zero),
            WorldFrame(Vector3.zero)));

        AudioSource source = player.GetComponent<AudioSource>();
        Assert.That(source, Is.Not.Null);
        Assert.That(source.outputAudioMixerGroup, Is.SameAs(sfxGroup));
        Assert.That(source.playOnAwake, Is.False);
        Assert.That(source.loop, Is.False);
        Assert.That(source.spatialBlend, Is.Zero);
        Assert.That(source.dopplerLevel, Is.Zero);
    }

    [Test]
    public void AudioManager_RoutesWithoutChangingAuthoredPlaybackSettings()
    {
        AudioMixerGroup sfxGroup = LoadSfxMixerGroup();
        GameObject managerObject = new("AudioManager");
        GameObject templateObject = new("SfxTemplate");
        GameObject targetObject = new("VfxAudioSource");
        _createdObjects.Add(managerObject);
        _createdObjects.Add(templateObject);
        _createdObjects.Add(targetObject);
        AudioManager manager = managerObject.AddComponent<AudioManager>();
        AudioSource template = templateObject.AddComponent<AudioSource>();
        AudioSource target = targetObject.AddComponent<AudioSource>();
        template.outputAudioMixerGroup = sfxGroup;
        manager.main_speakers = new Speakers
        {
            MainSFX = template
        };
        target.playOnAwake = true;
        target.loop = true;
        target.volume = 0.4f;
        target.pitch = 0.75f;
        target.spatialBlend = 0.8f;
        target.dopplerLevel = 2.5f;

        bool routed = manager.TryRouteToSfx(target);

        Assert.That(routed, Is.True);
        Assert.That(target.outputAudioMixerGroup, Is.SameAs(sfxGroup));
        Assert.That(target.playOnAwake, Is.True);
        Assert.That(target.loop, Is.True);
        Assert.That(target.volume, Is.EqualTo(0.4f));
        Assert.That(target.pitch, Is.EqualTo(0.75f));
        Assert.That(target.spatialBlend, Is.EqualTo(0.8f));
        Assert.That(target.dopplerLevel, Is.EqualTo(2.5f));
    }

    [TestCase(0, -80f)]
    [TestCase(1, -39.6f)]
    [TestCase(50, -20f)]
    [TestCase(100, 0f)]
    public void AudioVolumeMapping_MutesAtZeroAndUsesTheAudibleRangeAboveIt(
        int sliderValue,
        float expectedDecibels)
    {
        MethodInfo method = typeof(AudioData).GetMethod(
            "ToMixerVolume",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        float actual = (float)method.Invoke(
            new AudioData(),
            new object[] { sliderValue });

        Assert.That(actual, Is.EqualTo(expectedDecibels).Within(0.0001f));
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

    private static AudioMixerGroup LoadSfxMixerGroup()
    {
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(
            "Assets/Settings/MainAudioMixer.mixer");
        if (mixer == null)
        {
            Assert.Ignore(
                "MainAudioMixer is not available in this script-only checkout.");
            return null;
        }

        AudioMixerGroup[] groups = mixer.FindMatchingGroups("SFX");
        Assert.That(groups, Is.Not.Empty);
        return groups[0];
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

public sealed class PageBgmTests
{
    private GameObject _selectionObject;
    private GameObject _audioManagerObject;
    private GameObject _gameManagerObject;
    private GameObject _dataManagerObject;
    private GameObject _speakerObject;
    private AudioClip _clip;

    [TearDown]
    public void TearDown()
    {
        DestroyImmediate(_selectionObject);
        DestroyImmediate(_audioManagerObject);
        DestroyImmediate(_gameManagerObject);
        DestroyImmediate(_dataManagerObject);
        DestroyImmediate(_speakerObject);
        if (_clip != null)
            Object.DestroyImmediate(_clip);
    }

    [Test]
    public void PageSelection_RequestsConfiguredMusicListName()
    {
        _selectionObject = new GameObject("Page");
        PageBgmSelection selection =
            _selectionObject.AddComponent<PageBgmSelection>();
        SerializedObject serialized = new(selection);
        serialized.FindProperty("bgmClipName").stringValue =
            "  Page Track  ";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        GameEventManager events = new();
        string requestedName = null;
        events.BgmRequested += name => requestedName = name;

        bool requested = selection.RequestSelectedBgm(events);

        Assert.That(requested, Is.True);
        Assert.That(requestedName, Is.EqualTo("Page Track"));
    }

    [Test]
    public void PageSelection_KeepCurrent_DoesNotRequestBgm()
    {
        _selectionObject = new GameObject("Page");
        PageBgmSelection selection =
            _selectionObject.AddComponent<PageBgmSelection>();
        GameEventManager events = new();
        int requestCount = 0;
        events.BgmRequested += _ => requestCount++;

        bool requested = selection.RequestSelectedBgm(events);

        Assert.That(requested, Is.False);
        Assert.That(requestCount, Is.Zero);
    }

    [Test]
    public void AudioManager_Setup_DoesNotAutoPlayLegacyAudioTest()
    {
        AudioManager manager = CreateAudioManager(
            dataReady: true,
            clipName: "Audio Test");

        Assert.That(manager.main_speakers.MainMusic.clip, Is.Null);
    }

    [Test]
    public void AudioManager_PendingPageBgm_PlaysWhenDataBecomesReady()
    {
        AudioManager manager = CreateAudioManager(
            dataReady: false,
            clipName: "Page Track");

        manager.PlayBgm("Page Track");
        Assert.That(manager.main_speakers.MainMusic.clip, Is.Null);

        _gameManagerObject.GetComponent<GameManager>()
            .Data.IsSetupDone = true;
        MethodInfo playPending = typeof(AudioManager).GetMethod(
            "PlayPendingBgm",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(playPending, Is.Not.Null);
        playPending.Invoke(manager, null);

        Assert.That(
            manager.main_speakers.MainMusic.clip,
            Is.SameAs(_clip));
        Assert.That(manager.main_speakers.MainMusic.loop, Is.True);
    }

    private AudioManager CreateAudioManager(
        bool dataReady,
        string clipName)
    {
        _clip = AudioClip.Create("PageTrackClip", 64, 1, 44100, false);

        _dataManagerObject = new GameObject("DataManager");
        DataManager data = _dataManagerObject.AddComponent<DataManager>();
        data.IsSetupDone = dataReady;
        data.MusicList.Add(clipName, _clip);

        _gameManagerObject = new GameObject("GameManager");
        _gameManagerObject.SetActive(false);
        GameManager gameManager =
            _gameManagerObject.AddComponent<GameManager>();
        gameManager.Data = data;

        _speakerObject = new GameObject("MusicSpeaker");
        AudioSource speaker = _speakerObject.AddComponent<AudioSource>();
        _audioManagerObject = new GameObject("AudioManager");
        AudioManager manager =
            _audioManagerObject.AddComponent<AudioManager>();
        manager.main_speakers = new Speakers
        {
            MainMusic = speaker
        };
        manager.Setup(gameManager);
        return manager;
    }

    private static void DestroyImmediate(Object target)
    {
        if (target != null)
            Object.DestroyImmediate(target);
    }
}

public sealed class DungeonBgmProfileTests
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
    public void Profile_ResolvesPhaseOverrideAndExitVariants()
    {
        DungeonBgmProfile profile = CreateProfile();

        Assert.That(
            profile.ResolveLoopClipName(EDungeonPhase.Battle),
            Is.EqualTo("Battle Loop"));
        Assert.That(
            profile.ResolveLoopClipName(EDungeonPhase.Event),
            Is.EqualTo("Event Loop"));
        Assert.That(
            profile.ResolveLoopClipName(EDungeonPhase.Rest),
            Is.EqualTo("Battle Loop"));
        Assert.That(
            profile.ResolveExitClipName(EDungeonBgmExitReason.Clear),
            Is.EqualTo("Clear Exit"));
        Assert.That(
            profile.ResolveExitClipName(EDungeonBgmExitReason.Defeat),
            Is.EqualTo("Defeat Exit"));
        Assert.That(
            profile.ResolveExitClipName(EDungeonBgmExitReason.Aborted),
            Is.EqualTo("Abort Exit"));
    }

    [Test]
    public void AudioManager_DungeonSequence_AssignsIntroLoopPhaseAndExit()
    {
        AudioClip intro = CreateClip("IntroClip");
        AudioClip battle = CreateClip("BattleClip");
        AudioClip eventLoop = CreateClip("EventClip");
        AudioClip clearExit = CreateClip("ClearExitClip");
        DungeonBgmProfile profile = CreateProfile();

        GameObject dataObject = CreateObject("DataManager");
        DataManager data = dataObject.AddComponent<DataManager>();
        data.IsSetupDone = true;
        data.MusicList.Add("Intro", intro);
        data.MusicList.Add("Battle Loop", battle);
        data.MusicList.Add("Event Loop", eventLoop);
        data.MusicList.Add("Clear Exit", clearExit);

        GameObject gameManagerObject = CreateObject("GameManager");
        gameManagerObject.SetActive(false);
        GameManager gameManager =
            gameManagerObject.AddComponent<GameManager>();
        gameManager.Data = data;

        GameObject primaryObject = CreateObject("Primary Music");
        AudioSource primary = primaryObject.AddComponent<AudioSource>();
        GameObject audioObject = CreateObject("AudioManager");
        AudioManager audioManager = audioObject.AddComponent<AudioManager>();
        audioManager.main_speakers = new Speakers { MainMusic = primary };
        audioManager.Setup(gameManager);

        Assert.That(
            audioManager.PlayDungeonBgm(profile, EDungeonPhase.Battle),
            Is.True);
        AudioSource secondary = GetSecondaryMusicSource(audioManager);
        Assert.That(primary.clip, Is.SameAs(intro));
        Assert.That(primary.loop, Is.False);
        Assert.That(secondary.clip, Is.SameAs(battle));
        Assert.That(secondary.loop, Is.True);
        Assert.That(audioManager.IsDungeonBgmActive, Is.True);

        Assert.That(
            audioManager.SetDungeonBgmPhase(EDungeonPhase.Event),
            Is.True);
        Assert.That(secondary.clip, Is.SameAs(eventLoop));
        Assert.That(secondary.loop, Is.True);

        Assert.That(
            audioManager.RequestDungeonBgmExit(
                EDungeonBgmExitReason.Clear),
            Is.True);
        Assert.That(secondary.clip, Is.SameAs(clearExit));
        Assert.That(secondary.loop, Is.False);
    }

    private DungeonBgmProfile CreateProfile()
    {
        DungeonBgmProfile profile =
            ScriptableObject.CreateInstance<DungeonBgmProfile>();
        _createdObjects.Add(profile);
        SerializedObject serialized = new(profile);
        serialized.FindProperty("introClipName").stringValue = "Intro";
        serialized.FindProperty("defaultLoopClipName").stringValue =
            "Battle Loop";
        serialized.FindProperty("clearExitClipName").stringValue =
            "Clear Exit";
        serialized.FindProperty("defeatExitClipName").stringValue =
            "Defeat Exit";
        serialized.FindProperty("abortedExitClipName").stringValue =
            "Abort Exit";
        SerializedProperty phaseLoops = serialized.FindProperty("phaseLoops");
        phaseLoops.arraySize = 1;
        SerializedProperty entry = phaseLoops.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("phase").enumValueIndex =
            (int)EDungeonPhase.Event;
        entry.FindPropertyRelative("clipName").stringValue = "Event Loop";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return profile;
    }

    private AudioClip CreateClip(string name)
    {
        AudioClip clip = AudioClip.Create(name, 44100, 1, 44100, false);
        _createdObjects.Add(clip);
        return clip;
    }

    private GameObject CreateObject(string name)
    {
        GameObject created = new(name);
        _createdObjects.Add(created);
        return created;
    }

    private static AudioSource GetSecondaryMusicSource(AudioManager manager)
    {
        FieldInfo field = typeof(AudioManager).GetField(
            "_secondaryMusicSource",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        AudioSource source = field.GetValue(manager) as AudioSource;
        Assert.That(source, Is.Not.Null);
        return source;
    }
}
