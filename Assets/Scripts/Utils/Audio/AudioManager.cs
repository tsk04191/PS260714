using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    private const float MutedVolume = -80f;
    private const float UnmutedVolume = 0f;

    public AudioMixer mixer;
    [SerializeField] public Speakers main_speakers;

    private GameManager _manager;
    private GameEventManager _events;
    private bool _hasFocus = true;
    private bool _isPaused;

    public void Setup(GameManager manager)
    {
        if (_manager == manager && _events == (manager != null ? manager.Events : null))
            return;

        Teardown();

        _manager = manager;
        SetEventManager(manager != null ? manager.Events : null);
        ApplyMuteInBackground();
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
        events.BgmRequested += PlayBgm;
        events.SfxRequested += PlaySfx;
        events.SfxClipRequested += PlaySfx;
        events.UiSoundRequested += PlayUiSound;
        events.MuteInBackgroundChanged += OnMuteInBackgroundChanged;
    }

    private void UnsubscribeEventManager(GameEventManager events)
    {
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
        PlayBgm(FindBgmClip(clipName));
    }

    public void PlayBgm(AudioClip clip)
    {
        Speakers speakers = main_speakers;
        
        if (speakers == null || speakers.MainMusic == null || clip == null)
            return;

        speakers.MainMusic.clip = clip;
        speakers.MainMusic.Play();
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
