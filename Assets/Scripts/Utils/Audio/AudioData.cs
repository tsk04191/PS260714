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

    public void Save(bool flush = true)
    {
        master = Mathf.Clamp(master, 0, 100);
        music = Mathf.Clamp(music, 0, 100);
        sfx = Mathf.Clamp(sfx, 0, 100);
        ui = Mathf.Clamp(ui, 0, 100);

        PlayerPrefs.SetInt("Sound.Master", master);
        PlayerPrefs.SetInt("Sound.Music", music);
        PlayerPrefs.SetInt("Sound.SFX", sfx);
        PlayerPrefs.SetInt("Sound.UI", ui);

        PlayerPrefs.SetInt("Sound.MuteInBackground", CommonUtil.BoolToInt(mute_in_bg));

        if (flush)
            PlayerPrefs.Save();
    }

    public void Load()
    {
        master = Mathf.Clamp(PlayerPrefs.GetInt("Sound.Master", master), 0, 100);
        music = Mathf.Clamp(PlayerPrefs.GetInt("Sound.Music", music), 0, 100);
        sfx = Mathf.Clamp(PlayerPrefs.GetInt("Sound.SFX", sfx), 0, 100);
        ui = Mathf.Clamp(PlayerPrefs.GetInt("Sound.UI", ui), 0, 100);
        
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
        master = Mathf.Clamp(volume, 0, 100);
        Save(false);
        Apply(GetMixer());
    }
    public void SetMusicVolume(int volume)
    {
        music = Mathf.Clamp(volume, 0, 100);
        Save(false);
        Apply(GetMixer());
    }
    public void SetSFXVolume(int volume)
    {
        sfx = Mathf.Clamp(volume, 0, 100);
        Save(false);
        Apply(GetMixer());
    }
    public void SetUIVolume(int volume)
    {
        ui = Mathf.Clamp(volume, 0, 100);
        Save(false);
        Apply(GetMixer());
    }
    public void SetMiB(bool b)
    {
        mute_in_bg = b;
        Save(false);
    }
}
