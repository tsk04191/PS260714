using System;
using UnityEngine;

public enum EAudioChannel
{
    Master,
    Music,
    SFX,
    UI,
}

public class GameEventManager
{
    public bool IsDataReady { get; private set; }

    public event Action SaveAllRequested;
    public event Action LoadAllRequested;
    public event Action DataLoaded;
    public event Action DataSaved;
    public event Action DataReady;

    public event Action<int> DisplayBrightnessChangeRequested;
    public event Action<int> DisplayFPSChangeRequested;
    public event Action<int> DisplayModeChangeRequested;
    public event Action<string> ResolutionChangeRequested;
    public event Action<int> DisplayBrightnessChanged;
    public event Action<int> DisplayFPSChanged;
    public event Action<int> DisplayModeChanged;
    public event Action<string> ResolutionChanged;

    public event Action<EAudioChannel, int> AudioVolumeChangeRequested;
    public event Action<bool> MuteInBackgroundChangeRequested;
    public event Action<EAudioChannel, int> AudioVolumeChanged;
    public event Action<bool> MuteInBackgroundChanged;

    public event Action<string> BgmRequested;
    public event Action<string> SfxRequested;
    public event Action<AudioClip> SfxClipRequested;
    public event Action<string> UiSoundRequested;

    public void RequestSaveAll()
    {
        SaveAllRequested?.Invoke();
    }

    public void RequestLoadAll()
    {
        LoadAllRequested?.Invoke();
    }

    public void NotifyDataLoaded()
    {
        DataLoaded?.Invoke();
    }

    public void NotifyDataSaved()
    {
        DataSaved?.Invoke();
    }

    public void NotifyDataReady()
    {
        IsDataReady = true;
        DataReady?.Invoke();
    }

    public void RequestDisplayBrightnessChange(int value)
    {
        DisplayBrightnessChangeRequested?.Invoke(value);
    }

    public void RequestDisplayFPSChange(int value)
    {
        DisplayFPSChangeRequested?.Invoke(value);
    }

    public void RequestDisplayModeChange(int value)
    {
        DisplayModeChangeRequested?.Invoke(value);
    }

    public void RequestResolutionChange(string value)
    {
        ResolutionChangeRequested?.Invoke(value);
    }

    public void NotifyDisplayBrightnessChanged(int value)
    {
        DisplayBrightnessChanged?.Invoke(value);
    }

    public void NotifyDisplayFPSChanged(int value)
    {
        DisplayFPSChanged?.Invoke(value);
    }

    public void NotifyDisplayModeChanged(int value)
    {
        DisplayModeChanged?.Invoke(value);
    }

    public void NotifyResolutionChanged(string value)
    {
        ResolutionChanged?.Invoke(value);
    }

    public void RequestAudioVolumeChange(EAudioChannel channel, int value)
    {
        AudioVolumeChangeRequested?.Invoke(channel, value);
    }

    public void RequestMuteInBackgroundChange(bool value)
    {
        MuteInBackgroundChangeRequested?.Invoke(value);
    }

    public void NotifyAudioVolumeChanged(EAudioChannel channel, int value)
    {
        AudioVolumeChanged?.Invoke(channel, value);
    }

    public void NotifyMuteInBackgroundChanged(bool value)
    {
        MuteInBackgroundChanged?.Invoke(value);
    }

    public void RequestBgm(string clipName)
    {
        BgmRequested?.Invoke(clipName);
    }

    public void RequestSfx(string clipName)
    {
        SfxRequested?.Invoke(clipName);
    }

    public void RequestSfx(AudioClip clip)
    {
        SfxClipRequested?.Invoke(clip);
    }

    public void RequestUiSound(string clipName)
    {
        UiSoundRequested?.Invoke(clipName);
    }
}
