using System;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    private enum EMusicFadeState
    {
        None,
        FadingOut,
        FadingIn,
    }

    private const float MutedVolume = -80f;
    private const float UnmutedVolume = 0f;

    public AudioMixer mixer;
    [SerializeField] public Speakers main_speakers;

    [Header("Page BGM Sequential Fade")]
    [SerializeField, Min(0f)] private float pageBgmFadeOutDuration = 0.5f;
    [SerializeField, Min(0f)] private float pageBgmFadeInDuration = 0.5f;

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

    public bool IsDungeonBgmActive => _activeDungeonBgmProfile != null;
    public bool IsMusicTransitioning =>
        _musicFadeState != EMusicFadeState.None;
    public AudioClip CurrentMusicClip => _currentMusicClip;

    private GameManager _manager;
    private GameEventManager _events;
    private bool _hasFocus = true;
    private bool _isPaused;
    private string _pendingBgmClipName;
    private DungeonBgmProfile _pendingDungeonBgmProfile;
    private EDungeonBgmState _pendingDungeonBgmState;
    private AudioClip _pendingDungeonBgmOverride;
    private DungeonBgmProfile _activeDungeonBgmProfile;

    private AudioSource _musicSource;
    private AudioClip _currentMusicClip;
    private AudioClip _pendingMusicClip;
    private EMusicFadeState _musicFadeState;
    private float _musicBaseVolume = 1f;
    private float _fadeStartVolume;
    private float _fadeDuration;
    private float _fadeElapsed;
    private float _pendingFadeInDuration;
    private float _musicTargetVolume = 1f;
    private float _pendingMusicTargetVolume = 1f;

    public void Setup(GameManager manager)
    {
        if (_manager == manager &&
            _events == (manager != null ? manager.Events : null))
        {
            return;
        }

        Teardown();
        _manager = manager;
        SetEventManager(manager != null ? manager.Events : null);
        ResolveMusicSource();
        ApplyMuteInBackground();

        if (_manager != null && _manager.Data != null &&
            _manager.Data.IsSetupDone)
        {
            PlayPendingBgm();
        }
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
        TickMusicTransition(Time.unscaledDeltaTime);
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
        return data?.MusicList?.Find(clipName);
    }

    public AudioClip FindSfxClip(string clipName)
    {
        DataManager data = GetDataManager();
        if (data == null)
            return null;

        AudioClip clip = data.SFXList?.Find(clipName);
        return clip != null ? clip : data.UIList?.Find(clipName);
    }

    public AudioClip FindUiClip(string clipName)
    {
        return GetDataManager()?.UIList?.Find(clipName);
    }

    public void PlayBgm(string clipName)
    {
        string normalizedName = NormalizeClipName(clipName);
        if (string.IsNullOrEmpty(normalizedName))
            return;

        _pendingDungeonBgmProfile = null;
        _pendingDungeonBgmOverride = null;
        _activeDungeonBgmProfile = null;
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
        if (clip == null)
            return;

        _pendingDungeonBgmProfile = null;
        _pendingDungeonBgmOverride = null;
        _activeDungeonBgmProfile = null;
        RequestMusicTransition(
            clip,
            pageBgmFadeOutDuration,
            pageBgmFadeInDuration,
            1f);
    }

    public bool PlayDungeonBgm(
        DungeonBgmProfile profile,
        EDungeonBgmState state,
        AudioClip overrideClip = null)
    {
        if (profile == null)
            return false;

        DataManager data = GetDataManager();
        if (data == null || !data.IsSetupDone)
        {
            _pendingDungeonBgmProfile = profile;
            _pendingDungeonBgmState = state;
            _pendingDungeonBgmOverride = overrideClip;
            _pendingBgmClipName = null;
            return true;
        }

        AudioClip clip = profile.ResolveClip(state, overrideClip);
        if (clip == null)
        {
            Debug.LogWarning(
                $"Dungeon BGM profile '{profile.name}' has no {state} clip.",
                profile);
            return false;
        }

        _activeDungeonBgmProfile = profile;
        _pendingDungeonBgmProfile = null;
        _pendingDungeonBgmOverride = null;
        _pendingBgmClipName = null;
        return RequestMusicTransition(
            clip,
            profile.FadeOutDuration,
            profile.FadeInDuration,
            profile.ResolveVolumeScale(state));
    }

    private bool RequestMusicTransition(
        AudioClip nextClip,
        float fadeOutDuration,
        float fadeInDuration,
        float volumeScale)
    {
        AudioSource source = ResolveMusicSource();
        if (source == null || nextClip == null)
            return false;

        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        float nextTargetVolume =
            _musicBaseVolume * Mathf.Clamp01(volumeScale);
        if (ReferenceEquals(_pendingMusicClip, nextClip) &&
            _musicFadeState == EMusicFadeState.FadingOut)
        {
            _pendingMusicTargetVolume = nextTargetVolume;
            _pendingFadeInDuration = fadeInDuration;
            return true;
        }

        if (ReferenceEquals(_currentMusicClip, nextClip) &&
            ReferenceEquals(source.clip, nextClip))
        {
            _pendingMusicClip = null;
            _musicTargetVolume = nextTargetVolume;
            if (_musicFadeState == EMusicFadeState.FadingOut)
            {
                BeginFadeIn(fadeInDuration);
            }
            else if (!Mathf.Approximately(
                         source.volume,
                         _musicTargetVolume))
            {
                BeginFadeIn(fadeInDuration);
            }
            return true;
        }

        _pendingMusicClip = nextClip;
        _pendingMusicTargetVolume = nextTargetVolume;
        _pendingFadeInDuration = fadeInDuration;
        bool hasCurrentClip = _currentMusicClip != null ||
                              source.clip != null;
        if (!hasCurrentClip || fadeOutDuration <= 0f ||
            source.volume <= 0.0001f)
        {
            SwitchToPendingMusic();
            return true;
        }

        BeginFadeOut(fadeOutDuration);
        return true;
    }

    private AudioSource ResolveMusicSource()
    {
        AudioSource resolved = main_speakers != null
            ? main_speakers.MainMusic
            : null;
        if (ReferenceEquals(resolved, _musicSource))
            return _musicSource;

        _musicSource = resolved;
        _musicFadeState = EMusicFadeState.None;
        _pendingMusicClip = null;
        _fadeElapsed = 0f;
        if (_musicSource == null)
        {
            _currentMusicClip = null;
            return null;
        }

        _musicBaseVolume = Mathf.Clamp01(_musicSource.volume);
        _musicTargetVolume = _musicBaseVolume;
        _pendingMusicTargetVolume = _musicBaseVolume;
        _currentMusicClip = _musicSource.clip;
        return _musicSource;
    }

    private void BeginFadeOut(float duration)
    {
        _musicFadeState = EMusicFadeState.FadingOut;
        _fadeStartVolume = _musicSource != null
            ? _musicSource.volume
            : 0f;
        _fadeDuration = Mathf.Max(0f, duration);
        _fadeElapsed = 0f;
    }

    private void BeginFadeIn(float duration)
    {
        _musicFadeState = EMusicFadeState.FadingIn;
        _fadeStartVolume = _musicSource != null
            ? _musicSource.volume
            : 0f;
        _fadeDuration = Mathf.Max(0f, duration);
        _fadeElapsed = 0f;
        if (_fadeDuration <= 0f)
            CompleteFadeIn();
    }

    private void TickMusicTransition(float deltaTime)
    {
        AudioSource source = ResolveMusicSource();
        if (source == null || _musicFadeState == EMusicFadeState.None)
            return;

        _fadeElapsed += Mathf.Max(0f, deltaTime);
        float progress = _fadeDuration <= 0f
            ? 1f
            : Mathf.Clamp01(_fadeElapsed / _fadeDuration);
        if (_musicFadeState == EMusicFadeState.FadingOut)
        {
            source.volume = Mathf.Lerp(_fadeStartVolume, 0f, progress);
            if (progress >= 1f)
                SwitchToPendingMusic();
            return;
        }

        source.volume = Mathf.Lerp(
            _fadeStartVolume,
            _musicTargetVolume,
            progress);
        if (progress >= 1f)
            CompleteFadeIn();
    }

    private void SwitchToPendingMusic()
    {
        if (_musicSource == null)
            return;

        AudioClip nextClip = _pendingMusicClip;
        _pendingMusicClip = null;
        _musicTargetVolume = _pendingMusicTargetVolume;
        _musicSource.Stop();
        _musicSource.clip = nextClip;
        _musicSource.loop = true;
        _currentMusicClip = nextClip;
        if (nextClip == null)
        {
            _musicSource.volume = _musicTargetVolume;
            _musicFadeState = EMusicFadeState.None;
            return;
        }

        _musicSource.volume = 0f;
        _musicSource.Play();
        BeginFadeIn(_pendingFadeInDuration);
    }

    private void CompleteFadeIn()
    {
        if (_musicSource != null)
            _musicSource.volume = _musicTargetVolume;
        _musicFadeState = EMusicFadeState.None;
        _fadeElapsed = 0f;
    }

    private void PlayPendingBgm()
    {
        DungeonBgmProfile pendingProfile = _pendingDungeonBgmProfile;
        if (pendingProfile != null)
        {
            EDungeonBgmState state = _pendingDungeonBgmState;
            AudioClip overrideClip = _pendingDungeonBgmOverride;
            _pendingDungeonBgmProfile = null;
            _pendingDungeonBgmOverride = null;
            PlayDungeonBgm(pendingProfile, state, overrideClip);
            return;
        }

        string pendingName = _pendingBgmClipName;
        if (!string.IsNullOrEmpty(pendingName))
            PlayBgm(pendingName);
    }

    private static string NormalizeClipName(string clipName)
    {
        return string.IsNullOrWhiteSpace(clipName)
            ? string.Empty
            : clipName.Trim();
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

    private static void PlayOneShot(AudioSource speaker, AudioClip clip)
    {
        if (speaker != null && clip != null)
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
        if (data?.AudioDatas == null || mixer == null)
            return;

        bool shouldMute = data.AudioDatas.mute_in_bg &&
                          (!_hasFocus || _isPaused);
        mixer.SetFloat(
            "MiB",
            shouldMute ? MutedVolume : UnmutedVolume);
    }

    private DataManager GetDataManager()
    {
        if (_manager?.Data != null)
            return _manager.Data;
        return GameManager.Instance != null
            ? GameManager.Instance.Data
            : null;
    }
}
