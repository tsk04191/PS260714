using UnityEngine;
using UnityEngine.Audio;

public class AudioData
{
    public int master = 100;
    public int music = 100;
    public int sfx = 100;
    public int ui = 100;

    public bool mute_in_bg = false;

    private const float MinVolume = -80f;
    private const float MaxVolume = 0f;

    public void Init()
    {
        master = 100;
        music = 100;
        sfx = 100;
        ui = 100;

        mute_in_bg = false;
        Apply(GetMixer());
    }

    public void Save()
    {
        PlayerPrefs.SetInt("Sound.Master", master);
        PlayerPrefs.SetInt("Sound.Music", music);
        PlayerPrefs.SetInt("Sound.SFX", sfx);
        PlayerPrefs.SetInt("Sound.UI", ui);

        PlayerPrefs.SetInt("Sound.MuteInBackground", CommonUtil.BoolToInt(mute_in_bg));

        PlayerPrefs.Save();
        Apply(GetMixer());
    }

    public void Load()
    {
        master = PlayerPrefs.GetInt("Sound.Master", master);
        music = PlayerPrefs.GetInt("Sound.Music", music);
        sfx = PlayerPrefs.GetInt("Sound.SFX", sfx);
        ui = PlayerPrefs.GetInt("Sound.UI", ui);
        
        mute_in_bg = CommonUtil.IntToBool(PlayerPrefs.GetInt("Sound.MuteInBackground", CommonUtil.BoolToInt(mute_in_bg)));
        
        Apply(GetMixer());
    }

    public void Apply(AudioMixer mixer)
    {
        if (mixer == null)
            return;

        mixer.SetFloat("Master", ToMixerVolume(master));
        mixer.SetFloat("Music", ToMixerVolume(music));
        mixer.SetFloat("SFX", ToMixerVolume(sfx));
        mixer.SetFloat("UI", ToMixerVolume(ui));
    }

    private AudioMixer GetMixer()
    {
        GameManager manager = GameManager.Instance;

        if (manager == null || manager.Audio == null)
            return null;

        return manager.Audio.mixer;
    }

    private float ToMixerVolume(int volume)
    {
        return Mathf.Lerp(MinVolume, MaxVolume, Mathf.Clamp01(volume / 100f));
    }

    public void SetMasterVolume(int volume)
    {
        master = volume;
        Save();
    }
    public void SetMusicVolume(int volume)
    {
        music = volume;
        Save();
    }
    public void SetSFXVolume(int volume)
    {
        sfx = volume;
        Save();
    }
    public void SetUIVolume(int volume)
    {
        ui = volume;
        Save();
    }
    public void SetMiB(bool b)
    {
        mute_in_bg = b;
        Save();
    }
}
