using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AudioClipList
{
    [SerializeField] public List<AudioClipData> list = new List<AudioClipData>();

    public void Add(string clip_name, AudioClip clip)
    {
        AudioClipData data = new AudioClipData(clip_name, clip);
        list.Add(data);
    }

    public void Add(AudioClipData data)
    {
        list.Add(data);
    }

    public bool Remove(string clip_name)
    {
        AudioClipData data = FindData(clip_name);

        if (data == null)
            return false;

        return list.Remove(data);
    }

    public bool Remove(AudioClipData data)
    {
        return list.Remove(data);
    }

    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= list.Count)
            return false;

        list.RemoveAt(index);
        return true;
    }

    public AudioClipData FindData(string clip_name)
    {
        foreach (AudioClipData data in list)
        {
            if (data.clip_name == clip_name)
            {
                return data;
            }
        }
        return null;
    }

    public AudioClip Find(string clip_name)
    {
        AudioClipData data = FindData(clip_name);
        
        if (data == null)
            return null;

        return data.clip;
    }

    public void Clear()
    {
        list.Clear();
    }
}

[System.Serializable]
public class AudioClipData
{
    public string clip_name;
    public AudioClip clip;

    public AudioClipData(string clip_name, AudioClip clip)
    {
        this.clip_name = clip_name;
        this.clip = clip;
    }
}

[System.Serializable]
public class Speakers
{
    public AudioSource MainMusic;
    public AudioSource MainSFX;
    public AudioSource MainUI;
}
