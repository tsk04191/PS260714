using UnityEngine;

public enum EDungeonBgmState
{
    Ready,
    Battle,
    Rest,
}

[CreateAssetMenu(
    fileName = "DungeonBgmProfile",
    menuName = "Dungeon/BGM Profile")]
public sealed class DungeonBgmProfile : ScriptableObject
{
    [Header("Default Music")]
    [SerializeField] private AudioClip readyClip;
    [SerializeField, Range(0, 100)] private int readyVolumePercent = 100;
    [SerializeField] private AudioClip battleClip;
    [SerializeField, Range(0, 100)] private int battleVolumePercent = 100;
    [SerializeField] private AudioClip restClip;
    [SerializeField, Range(0, 100)] private int restVolumePercent = 100;

    [Header("Sequential Fade")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.5f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.5f;

    public AudioClip ReadyClip => readyClip;
    public AudioClip BattleClip => battleClip;
    public AudioClip RestClip => restClip;
    public int ReadyVolumePercent =>
        Mathf.Clamp(readyVolumePercent, 0, 100);
    public int BattleVolumePercent =>
        Mathf.Clamp(battleVolumePercent, 0, 100);
    public int RestVolumePercent =>
        Mathf.Clamp(restVolumePercent, 0, 100);
    public float FadeOutDuration => Mathf.Max(0f, fadeOutDuration);
    public float FadeInDuration => Mathf.Max(0f, fadeInDuration);

    public AudioClip ResolveClip(
        EDungeonBgmState state,
        AudioClip overrideClip = null)
    {
        if (overrideClip != null)
            return overrideClip;

        return state switch
        {
            EDungeonBgmState.Battle => battleClip,
            EDungeonBgmState.Rest => restClip,
            _ => readyClip,
        };
    }

    public int ResolveVolumePercent(EDungeonBgmState state)
    {
        return state switch
        {
            EDungeonBgmState.Battle => BattleVolumePercent,
            EDungeonBgmState.Rest => RestVolumePercent,
            _ => ReadyVolumePercent,
        };
    }

    public float ResolveVolumeScale(EDungeonBgmState state)
    {
        return ResolveVolumePercent(state) / 100f;
    }

    public bool TryValidate(out string error)
    {
        if (readyClip == null)
        {
            error = "Ready Clip is required.";
            return false;
        }
        if (battleClip == null)
        {
            error = "Battle Clip is required.";
            return false;
        }
        if (restClip == null)
        {
            error = "Rest Clip is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void OnValidate()
    {
        readyVolumePercent = Mathf.Clamp(readyVolumePercent, 0, 100);
        battleVolumePercent = Mathf.Clamp(battleVolumePercent, 0, 100);
        restVolumePercent = Mathf.Clamp(restVolumePercent, 0, 100);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
    }
}
