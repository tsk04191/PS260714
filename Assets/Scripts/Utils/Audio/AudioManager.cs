using System;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    private const float MutedVolume = -80f;
    private const float UnmutedVolume = 0f;

    public AudioMixer mixer;
    [SerializeField] public Speakers main_speakers;

    public AudioMixerGroup SfxMixerGroup
    {
        get
        {
            AudioSource speaker = main_speakers != null
                ? main_speakers.MainSFX
                : null;
            return speaker != null
                ? speaker.outputAudioMixerGroup
                : null;
        }
    }

    private GameManager _manager;
    private GameEventManager _events;
    private bool _hasFocus = true;
    private bool _isPaused;
    private string _pendingBgmClipName;
    private DungeonBgmProfile _pendingDungeonBgmProfile;
    private EDungeonPhase _pendingDungeonBgmPhase;

    private AudioSource _secondaryMusicSource;
    private DungeonBgmProfile _activeDungeonBgmProfile;
    private bool _dungeonBgmActive;
    private AudioSource _currentDungeonLoopSource;
    private AudioClip _currentDungeonLoopClip;
    private double _currentDungeonLoopStartDspTime;
    private AudioSource _pendingDungeonLoopSource;
    private AudioClip _pendingDungeonLoopClip;
    private double _pendingDungeonLoopStartDspTime;
    private bool _hasPendingDungeonLoop;
    private bool _dungeonExitScheduled;
    private double _dungeonExitEndDspTime;
    private string _queuedBgmNameAfterExit;
    private AudioClip _queuedBgmClipAfterExit;

    public bool IsDungeonBgmActive => _dungeonBgmActive;

    public void Setup(GameManager manager)
    {
        if (_manager == manager && _events == (manager != null ? manager.Events : null))
            return;

        Teardown();

        _manager = manager;
        SetEventManager(manager != null ? manager.Events : null);
        ApplyMuteInBackground();

        if (_manager != null && _manager.Data != null &&
            _manager.Data.IsSetupDone)
            PlayPendingBgm();
    }

    public void Teardown()
    {
        SetEventManager(null);
        _manager = null;
    }

    private void OnDestroy()
    {
        Teardown();
    }

    private void Update()
    {
        FinalizeScheduledDungeonLoop();
        FinalizeDungeonExit();
    }

    private void OnApplicationFocus(bool focus)
    {
        _hasFocus = focus;
        ApplyMuteInBackground();
    }

    private void OnApplicationPause(bool pause)
    {
        _isPaused = pause;
        ApplyMuteInBackground();
    }

    private void SetEventManager(GameEventManager events)
    {
        if (_events == events)
            return;

        if (_events != null)
            UnsubscribeEventManager(_events);

        _events = events;

        if (_events != null)
            SubscribeEventManager(_events);
    }

    private void SubscribeEventManager(GameEventManager events)
    {
        events.DataReady += PlayPendingBgm;
        events.BgmRequested += PlayBgm;
        events.SfxRequested += PlaySfx;
        events.SfxClipRequested += PlaySfx;
        events.UiSoundRequested += PlayUiSound;
        events.MuteInBackgroundChanged += OnMuteInBackgroundChanged;
    }

    private void UnsubscribeEventManager(GameEventManager events)
    {
        events.DataReady -= PlayPendingBgm;
        events.BgmRequested -= PlayBgm;
        events.SfxRequested -= PlaySfx;
        events.SfxClipRequested -= PlaySfx;
        events.UiSoundRequested -= PlayUiSound;
        events.MuteInBackgroundChanged -= OnMuteInBackgroundChanged;
    }

    public AudioClip FindBgmClip(string clipName)
    {
        DataManager data = GetDataManager();

        if (data == null || data.MusicList == null)
            return null;

        return data.MusicList.Find(clipName);
    }

    public AudioClip FindSfxClip(string clipName)
    {
        DataManager data = GetDataManager();

        if (data == null)
            return null;

        AudioClip clip = data.SFXList != null ? data.SFXList.Find(clipName) : null;

        if (clip != null)
            return clip;

        return data.UIList != null ? data.UIList.Find(clipName) : null;
    }

    public AudioClip FindUiClip(string clipName)
    {
        DataManager data = GetDataManager();

        if (data == null || data.UIList == null)
            return null;

        return data.UIList.Find(clipName);
    }

    public void PlayBgm(string clipName)
    {
        string normalizedName = NormalizeClipName(clipName);
        if (string.IsNullOrEmpty(normalizedName))
            return;

        if (_dungeonExitScheduled)
        {
            _queuedBgmNameAfterExit = normalizedName;
            _queuedBgmClipAfterExit = null;
            return;
        }

        _pendingDungeonBgmProfile = null;

        DataManager data = GetDataManager();
        if (data == null || !data.IsSetupDone)
        {
            _pendingBgmClipName = normalizedName;
            return;
        }

        AudioClip clip = FindBgmClip(normalizedName);
        if (clip == null)
        {
            Debug.LogWarning(
                $"BGM '{normalizedName}' was not found in Music List.",
                this);
            return;
        }

        _pendingBgmClipName = null;
        PlayBgm(clip);
    }

    public void PlayBgm(AudioClip clip)
    {
        Speakers speakers = main_speakers;
        
        if (speakers == null || speakers.MainMusic == null || clip == null)
            return;

        if (_dungeonExitScheduled)
        {
            _queuedBgmClipAfterExit = clip;
            _queuedBgmNameAfterExit = null;
            return;
        }

        AudioSource musicSpeaker = speakers.MainMusic;
        if (!_dungeonBgmActive && ReferenceEquals(musicSpeaker.clip, clip) &&
            musicSpeaker.isPlaying)
        {
            return;
        }

        StopDungeonBgmImmediately();
        musicSpeaker.loop = true;
        musicSpeaker.clip = clip;
        musicSpeaker.Play();
    }

    public bool PlayDungeonBgm(
        DungeonBgmProfile profile,
        EDungeonPhase initialPhase)
    {
        if (profile == null)
            return false;

        DataManager data = GetDataManager();
        if (data == null || !data.IsSetupDone)
        {
            _pendingDungeonBgmProfile = profile;
            _pendingDungeonBgmPhase = initialPhase;
            _pendingBgmClipName = null;
            return true;
        }

        if (_dungeonBgmActive && !_dungeonExitScheduled &&
            ReferenceEquals(_activeDungeonBgmProfile, profile))
        {
            SetDungeonBgmPhase(initialPhase);
            return true;
        }

        string loopName = profile.ResolveLoopClipName(initialPhase);
        AudioClip loopClip = FindRequiredDungeonClip(
            loopName,
            $"{initialPhase} loop",
            profile);
        if (loopClip == null)
            return false;

        AudioSource primary = GetPrimaryMusicSource();
        AudioSource secondary = EnsureSecondaryMusicSource();
        if (primary == null || secondary == null)
        {
            Debug.LogWarning(
                "Dungeon BGM requires a Main Music AudioSource.",
                this);
            return false;
        }

        AudioClip introClip = null;
        if (!string.IsNullOrEmpty(profile.IntroClipName))
        {
            introClip = FindRequiredDungeonClip(
                profile.IntroClipName,
                "intro",
                profile);
        }

        StopDungeonBgmImmediately();
        CopyMusicSourceSettings(primary, secondary);

        double startDspTime = AudioSettings.dspTime +
                              profile.ScheduleLeadTime;
        if (introClip != null)
        {
            ConfigureScheduledMusic(primary, introClip, false);
            primary.PlayScheduled(startDspTime);

            double loopStart = startDspTime + GetClipDuration(introClip);
            ConfigureScheduledMusic(secondary, loopClip, true);
            secondary.PlayScheduled(loopStart);
            _currentDungeonLoopSource = secondary;
            _currentDungeonLoopStartDspTime = loopStart;
        }
        else
        {
            ConfigureScheduledMusic(primary, loopClip, true);
            primary.PlayScheduled(startDspTime);
            _currentDungeonLoopSource = primary;
            _currentDungeonLoopStartDspTime = startDspTime;
        }

        _currentDungeonLoopClip = loopClip;
        _activeDungeonBgmProfile = profile;
        _dungeonBgmActive = true;
        _pendingDungeonBgmProfile = null;
        _pendingBgmClipName = null;
        return true;
    }

    public bool SetDungeonBgmPhase(EDungeonPhase phase)
    {
        FinalizeScheduledDungeonLoop();
        if (!_dungeonBgmActive || _dungeonExitScheduled ||
            _activeDungeonBgmProfile == null ||
            _currentDungeonLoopSource == null)
        {
            return false;
        }

        string clipName = _activeDungeonBgmProfile.ResolveLoopClipName(phase);
        AudioClip nextClip = FindRequiredDungeonClip(
            clipName,
            $"{phase} loop",
            _activeDungeonBgmProfile);
        if (nextClip == null)
            return false;

        if (!_hasPendingDungeonLoop &&
            ReferenceEquals(nextClip, _currentDungeonLoopClip))
        {
            return true;
        }

        double now = AudioSettings.dspTime;
        double earliestStart = now +
                               _activeDungeonBgmProfile.ScheduleLeadTime;
        if (_currentDungeonLoopStartDspTime >= earliestStart)
        {
            _currentDungeonLoopSource.Stop();
            ConfigureScheduledMusic(
                _currentDungeonLoopSource,
                nextClip,
                true);
            _currentDungeonLoopSource.PlayScheduled(
                _currentDungeonLoopStartDspTime);
            _currentDungeonLoopClip = nextClip;
            CancelPendingDungeonLoop();
            return true;
        }

        double transitionDspTime = CalculateTransitionDspTime(
            earliestStart);
        AudioSource nextSource = GetOtherMusicSource(
            _currentDungeonLoopSource);
        if (nextSource == null)
            return false;

        CancelPendingDungeonLoop();
        _currentDungeonLoopSource.SetScheduledEndTime(transitionDspTime);
        ConfigureScheduledMusic(nextSource, nextClip, true);
        nextSource.PlayScheduled(transitionDspTime);
        _pendingDungeonLoopSource = nextSource;
        _pendingDungeonLoopClip = nextClip;
        _pendingDungeonLoopStartDspTime = transitionDspTime;
        _hasPendingDungeonLoop = true;
        return true;
    }

    public bool RequestDungeonBgmExit(EDungeonBgmExitReason reason)
    {
        if (_pendingDungeonBgmProfile != null && !_dungeonBgmActive)
        {
            _pendingDungeonBgmProfile = null;
            return true;
        }

        FinalizeScheduledDungeonLoop();
        if (!_dungeonBgmActive || _activeDungeonBgmProfile == null ||
            _currentDungeonLoopSource == null)
            return false;
        if (_dungeonExitScheduled)
            return true;

        string exitName = _activeDungeonBgmProfile.ResolveExitClipName(reason);
        AudioClip exitClip = string.IsNullOrEmpty(exitName)
            ? null
            : FindRequiredDungeonClip(
                exitName,
                $"{reason} exit",
                _activeDungeonBgmProfile);

        double earliestStart = AudioSettings.dspTime +
                               _activeDungeonBgmProfile.ScheduleLeadTime;
        double transitionDspTime;
        AudioSource exitSource;
        if (_currentDungeonLoopStartDspTime >= earliestStart)
        {
            transitionDspTime = _currentDungeonLoopStartDspTime;
            exitSource = _currentDungeonLoopSource;
            CancelPendingDungeonLoop();
            exitSource.Stop();
        }
        else
        {
            transitionDspTime = CalculateTransitionDspTime(earliestStart);
            CancelPendingDungeonLoop();
            _currentDungeonLoopSource.SetScheduledEndTime(transitionDspTime);
            exitSource = GetOtherMusicSource(_currentDungeonLoopSource);
        }

        if (exitClip != null && exitSource == null)
            return false;

        if (exitClip != null)
        {
            ConfigureScheduledMusic(exitSource, exitClip, false);
            exitSource.PlayScheduled(transitionDspTime);
        }
        _dungeonExitScheduled = true;
        _dungeonExitEndDspTime = transitionDspTime +
                                 GetClipDuration(exitClip);
        return true;
    }

    private void PlayPendingBgm()
    {
        DungeonBgmProfile pendingProfile = _pendingDungeonBgmProfile;
        if (pendingProfile != null)
        {
            EDungeonPhase pendingPhase = _pendingDungeonBgmPhase;
            _pendingDungeonBgmProfile = null;
            PlayDungeonBgm(pendingProfile, pendingPhase);
            return;
        }

        string pendingName = _pendingBgmClipName;
        if (string.IsNullOrEmpty(pendingName))
            return;

        PlayBgm(pendingName);
    }

    private static string NormalizeClipName(string clipName)
    {
        return string.IsNullOrWhiteSpace(clipName)
            ? string.Empty
            : clipName.Trim();
    }

    private AudioClip FindRequiredDungeonClip(
        string clipName,
        string role,
        DungeonBgmProfile profile)
    {
        string normalizedName = NormalizeClipName(clipName);
        if (string.IsNullOrEmpty(normalizedName))
        {
            Debug.LogWarning(
                $"Dungeon BGM profile '{profile.name}' has no {role} clip.",
                profile);
            return null;
        }

        AudioClip clip = FindBgmClip(normalizedName);
        if (clip == null)
        {
            Debug.LogWarning(
                $"Dungeon BGM {role} '{normalizedName}' from profile " +
                $"'{profile.name}' was not found in Music List.",
                profile);
        }

        return clip;
    }

    private AudioSource GetPrimaryMusicSource()
    {
        return main_speakers != null ? main_speakers.MainMusic : null;
    }

    private AudioSource EnsureSecondaryMusicSource()
    {
        AudioSource primary = GetPrimaryMusicSource();
        if (primary == null)
            return null;
        if (_secondaryMusicSource == null)
        {
            GameObject speakerObject = new("Dungeon BGM Secondary");
            speakerObject.transform.SetParent(transform, false);
            _secondaryMusicSource = speakerObject.AddComponent<AudioSource>();
        }

        CopyMusicSourceSettings(primary, _secondaryMusicSource);
        return _secondaryMusicSource;
    }

    private static void CopyMusicSourceSettings(
        AudioSource source,
        AudioSource destination)
    {
        if (source == null || destination == null)
            return;

        destination.outputAudioMixerGroup = source.outputAudioMixerGroup;
        destination.mute = source.mute;
        destination.bypassEffects = source.bypassEffects;
        destination.bypassListenerEffects = source.bypassListenerEffects;
        destination.bypassReverbZones = source.bypassReverbZones;
        destination.priority = source.priority;
        destination.volume = source.volume;
        destination.pitch = source.pitch;
        destination.panStereo = source.panStereo;
        destination.spatialBlend = source.spatialBlend;
        destination.reverbZoneMix = source.reverbZoneMix;
        destination.dopplerLevel = source.dopplerLevel;
        destination.playOnAwake = false;
    }

    private static void ConfigureScheduledMusic(
        AudioSource source,
        AudioClip clip,
        bool loop)
    {
        source.Stop();
        source.playOnAwake = false;
        source.clip = clip;
        source.loop = loop;
    }

    private AudioSource GetOtherMusicSource(AudioSource source)
    {
        AudioSource primary = GetPrimaryMusicSource();
        AudioSource secondary = EnsureSecondaryMusicSource();
        return ReferenceEquals(source, primary) ? secondary : primary;
    }

    private double CalculateTransitionDspTime(double earliestStart)
    {
        double unitDuration;
        if (_activeDungeonBgmProfile.TransitionMode ==
            EDungeonBgmTransitionMode.LoopBoundary)
        {
            unitDuration = GetClipDuration(_currentDungeonLoopClip);
        }
        else
        {
            unitDuration = 60d / _activeDungeonBgmProfile.Bpm *
                           _activeDungeonBgmProfile.BeatsPerBar;
        }

        unitDuration = Math.Max(0.001d, unitDuration);
        double elapsed = Math.Max(
            0d,
            earliestStart - _currentDungeonLoopStartDspTime);
        double unitCount = Math.Ceiling(elapsed / unitDuration);
        return _currentDungeonLoopStartDspTime +
               Math.Max(1d, unitCount) * unitDuration;
    }

    private void FinalizeScheduledDungeonLoop()
    {
        if (!_hasPendingDungeonLoop ||
            AudioSettings.dspTime < _pendingDungeonLoopStartDspTime)
        {
            return;
        }

        AudioSource previousSource = _currentDungeonLoopSource;
        _currentDungeonLoopSource = _pendingDungeonLoopSource;
        _currentDungeonLoopClip = _pendingDungeonLoopClip;
        _currentDungeonLoopStartDspTime =
            _pendingDungeonLoopStartDspTime;
        _pendingDungeonLoopSource = null;
        _pendingDungeonLoopClip = null;
        _pendingDungeonLoopStartDspTime = 0d;
        _hasPendingDungeonLoop = false;
        if (previousSource != null &&
            !ReferenceEquals(previousSource, _currentDungeonLoopSource))
        {
            previousSource.Stop();
        }
    }

    private void FinalizeDungeonExit()
    {
        if (!_dungeonExitScheduled ||
            AudioSettings.dspTime < _dungeonExitEndDspTime)
        {
            return;
        }

        string queuedName = _queuedBgmNameAfterExit;
        AudioClip queuedClip = _queuedBgmClipAfterExit;
        StopDungeonBgmImmediately();
        if (queuedClip != null)
            PlayBgm(queuedClip);
        else if (!string.IsNullOrEmpty(queuedName))
            PlayBgm(queuedName);
    }

    private void CancelPendingDungeonLoop()
    {
        if (_pendingDungeonLoopSource != null)
            _pendingDungeonLoopSource.Stop();
        _pendingDungeonLoopSource = null;
        _pendingDungeonLoopClip = null;
        _pendingDungeonLoopStartDspTime = 0d;
        _hasPendingDungeonLoop = false;
    }

    private void StopDungeonBgmImmediately()
    {
        AudioSource primary = GetPrimaryMusicSource();
        if (primary != null)
            primary.Stop();
        if (_secondaryMusicSource != null)
            _secondaryMusicSource.Stop();

        _activeDungeonBgmProfile = null;
        _dungeonBgmActive = false;
        _currentDungeonLoopSource = null;
        _currentDungeonLoopClip = null;
        _currentDungeonLoopStartDspTime = 0d;
        _pendingDungeonLoopSource = null;
        _pendingDungeonLoopClip = null;
        _pendingDungeonLoopStartDspTime = 0d;
        _hasPendingDungeonLoop = false;
        _dungeonExitScheduled = false;
        _dungeonExitEndDspTime = 0d;
        _queuedBgmNameAfterExit = null;
        _queuedBgmClipAfterExit = null;
    }

    private static double GetClipDuration(AudioClip clip)
    {
        if (clip == null)
            return 0d;
        return clip.frequency > 0
            ? (double)clip.samples / clip.frequency
            : clip.length;
    }

    public void PlaySfx(string clipName)
    {
        PlaySfx(FindSfxClip(clipName));
    }

    public void PlaySfx(AudioClip clip)
    {
        Speakers speakers = main_speakers;

        PlayOneShot(speakers != null ? speakers.MainSFX : null, clip);
    }

    public void PlaySfx(AudioSource speaker, AudioClip clip)
    {
        Speakers speakers = main_speakers;
        AudioSource fallbackSpeaker = speakers != null
            ? speakers.MainSFX
            : null;
        AudioSource targetSpeaker = speaker != null
            ? speaker
            : fallbackSpeaker;

        ConfigureSfxSpeaker(targetSpeaker, fallbackSpeaker);
        PlayOneShot(targetSpeaker, clip);
    }

    public bool TryRouteToSfx(AudioSource speaker)
    {
        if (speaker == null)
            return false;

        AudioMixerGroup group = SfxMixerGroup;
        if (group == null)
            return false;

        speaker.outputAudioMixerGroup = group;
        return true;
    }

    public void PlayUiSound(string clipName)
    {
        PlayUiSound(FindUiClip(clipName));
    }

    public void PlayUiSound(AudioClip clip)
    {
        Speakers speakers = main_speakers;

        PlayOneShot(speakers != null ? speakers.MainUI : null, clip);
    }

    private void PlayOneShot(AudioSource speaker, AudioClip clip)
    {
        if (speaker == null || clip == null)
            return;

        speaker.PlayOneShot(clip);
    }

    private void ConfigureSfxSpeaker(
        AudioSource speaker,
        AudioSource template)
    {
        if (speaker == null)
            return;

        speaker.playOnAwake = false;
        speaker.loop = false;
        speaker.spatialBlend = 0f;
        speaker.dopplerLevel = 0f;

        if (template == null || ReferenceEquals(speaker, template))
            return;

        TryRouteToSfx(speaker);
        speaker.priority = template.priority;
    }

    private void OnMuteInBackgroundChanged(bool _)
    {
        ApplyMuteInBackground();
    }

    private void ApplyMuteInBackground()
    {
        DataManager data = GetDataManager();
        AudioMixer audioMixer = mixer;

        if (data == null || data.AudioDatas == null || audioMixer == null)
            return;

        bool shouldMute = data.AudioDatas.mute_in_bg && (!_hasFocus || _isPaused);
        audioMixer.SetFloat("MiB", shouldMute ? MutedVolume : UnmutedVolume);
    }

    private DataManager GetDataManager()
    {
        if (_manager != null && _manager.Data != null)
            return _manager.Data;

        return GameManager.Instance != null ? GameManager.Instance.Data : null;
    }
}
