using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DataManager : MonoBehaviour
{
    public static DataManager Current { get; private set; }

    [HideInInspector] public bool IsSetupDone = false;

    public Image imgBrightness;

    [Header("Client UI")]
    [Tooltip(
        "Game-wide TMP font. When empty, the global font from " +
        "LocalizationFontCatalog is used.")]
    [SerializeField] private TMP_FontAsset clientDefaultFont;

    [HideInInspector] public DisplayData DisplayDatas;
    [HideInInspector] public AudioData AudioDatas;

    [Header("Audio Clip")]
    public AudioClipList MusicList = new AudioClipList();
    public AudioClipList SFXList = new AudioClipList();
    public AudioClipList UIList = new AudioClipList();

    private GameEventManager _events;

    public TMP_FontAsset ClientDefaultFont => clientDefaultFont;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
    }

    void Awake()
    {
        if (Current == null || Current == this)
            Current = this;

        IsSetupDone = false;
        LocalizationFontResolver.RefreshAllClientText();
    }

    void Start()
    {
        SetEventManager(GameManager.Instance.Events);

        DisplayDatas = new DisplayData();
        AudioDatas = new AudioData();

        LoadALL();
        SaveALL();

        IsSetupDone = true;
        _events?.NotifyDataReady();
    }

    private void OnDestroy()
    {
        SetEventManager(null);
        if (Current == this)
            Current = null;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            LocalizationFontResolver.RefreshAllClientText();
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
        events.SaveAllRequested += SaveALL;
        events.LoadAllRequested += LoadALL;
        events.DisplayBrightnessChangeRequested += SetDisplayBrightness;
        events.DisplayFPSChangeRequested += SetDisplayFPS;
        events.DisplayModeChangeRequested += SetDisplayMode;
        events.ResolutionChangeRequested += SetResolution;
        events.AudioVolumeChangeRequested += SetAudioVolume;
        events.MuteInBackgroundChangeRequested += SetMuteInBackground;
    }

    private void UnsubscribeEventManager(GameEventManager events)
    {
        events.SaveAllRequested -= SaveALL;
        events.LoadAllRequested -= LoadALL;
        events.DisplayBrightnessChangeRequested -= SetDisplayBrightness;
        events.DisplayFPSChangeRequested -= SetDisplayFPS;
        events.DisplayModeChangeRequested -= SetDisplayMode;
        events.ResolutionChangeRequested -= SetResolution;
        events.AudioVolumeChangeRequested -= SetAudioVolume;
        events.MuteInBackgroundChangeRequested -= SetMuteInBackground;
    }

    public void SaveALL()
    {
        DisplayDatas.Save(false);
        AudioDatas.Save(false);
        PlayerPrefs.Save();

        _events?.NotifyDataSaved();
    }

    public void LoadALL()
    {
        DisplayDatas.Load();
        AudioDatas.Load();
        
        NotifyCurrentSettings();
        _events?.NotifyDataLoaded();
    }

    #region Display
    public int GetDisplayBrightness()
    {
        return DisplayDatas.brightness;
    }

    public void SetDisplayBrightness(int brightness)
    {
        DisplayDatas.brightness = Mathf.Clamp(brightness, 0, 100);
        DisplayDatas.ApplyBrightness();
        DisplayDatas.Save(false);
        _events?.NotifyDisplayBrightnessChanged(DisplayDatas.brightness);
    }

    public int GetDisplayFPS()
    {
        return DisplayDatas.fps;
    }

    public void SetDisplayFPS(int fps)
    {
        if (!DisplayData.IsValidFpsMode(fps))
            return;

        DisplayDatas.fps = fps;
        DisplayDatas.ApplyFPS();
        DisplayDatas.Save(false);
        _events?.NotifyDisplayFPSChanged(DisplayDatas.fps);
    }

    public int GetDisplayMode()
    {
        return DisplayDatas.displayMode;
    }

    public void SetDisplayMode(int mode)
    {
        if (!DisplayData.IsValidDisplayMode(mode))
            return;

        DisplayDatas.displayMode = mode;
        DisplayDatas.ApplyResolution();
        DisplayDatas.Save(false);
        _events?.NotifyDisplayModeChanged(DisplayDatas.displayMode);
    }

    public string GetResolution()
    {
        return DisplayDatas.resolution;
    }

    public void SetResolution(string resolution)
    {
        if (!DisplayData.TryNormalizeResolution(resolution, out string normalized))
            return;

        DisplayDatas.resolution = normalized;
        DisplayDatas.ApplyResolution();
        DisplayDatas.Save(false);
        _events?.NotifyResolutionChanged(DisplayDatas.resolution);
    }
    #endregion Display

    #region Audio
    public void SetAudioVolume(EAudioChannel channel, int volume)
    {
        volume = Mathf.Clamp(volume, 0, 100);

        switch (channel)
        {
            case EAudioChannel.Master:
                AudioDatas.SetMasterVolume(volume);
                break;
            case EAudioChannel.Music:
                AudioDatas.SetMusicVolume(volume);
                break;
            case EAudioChannel.SFX:
                AudioDatas.SetSFXVolume(volume);
                break;
            case EAudioChannel.UI:
                AudioDatas.SetUIVolume(volume);
                break;
        }

        _events?.NotifyAudioVolumeChanged(channel, volume);
    }

    public void SetMuteInBackground(bool value)
    {
        AudioDatas.SetMiB(value);
        _events?.NotifyMuteInBackgroundChanged(AudioDatas.mute_in_bg);
    }

    private void NotifyCurrentSettings()
    {
        if (DisplayDatas != null)
        {
            _events?.NotifyDisplayBrightnessChanged(DisplayDatas.brightness);
            _events?.NotifyDisplayFPSChanged(DisplayDatas.fps);
            _events?.NotifyDisplayModeChanged(DisplayDatas.displayMode);
            _events?.NotifyResolutionChanged(DisplayDatas.resolution);
        }

        if (AudioDatas != null)
        {
            _events?.NotifyAudioVolumeChanged(EAudioChannel.Master, AudioDatas.master);
            _events?.NotifyAudioVolumeChanged(EAudioChannel.Music, AudioDatas.music);
            _events?.NotifyAudioVolumeChanged(EAudioChannel.SFX, AudioDatas.sfx);
            _events?.NotifyAudioVolumeChanged(EAudioChannel.UI, AudioDatas.ui);
            _events?.NotifyMuteInBackgroundChanged(AudioDatas.mute_in_bg);
        }
    }
    #endregion Audio
}
