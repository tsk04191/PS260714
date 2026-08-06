using System;
using System.Collections.Generic;
using UnityEngine;

public enum EDungeonBgmTransitionMode
{
    NextBar,
    LoopBoundary,
}

public enum EDungeonBgmExitReason
{
    Clear,
    Defeat,
    Aborted,
}

[Serializable]
public sealed class DungeonBgmPhaseLoop
{
    [SerializeField] private EDungeonPhase phase;
    [SerializeField] private string clipName;

    public EDungeonPhase Phase => phase;
    public string ClipName => Normalize(clipName);

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}

[CreateAssetMenu(
    fileName = "DungeonBgmProfile",
    menuName = "Dungeon/BGM Profile")]
public sealed class DungeonBgmProfile : ScriptableObject
{
    [Header("Intro / Loop")]
    [SerializeField] private string introClipName;
    [SerializeField] private string defaultLoopClipName;
    [SerializeField] private List<DungeonBgmPhaseLoop> phaseLoops = new();

    [Header("Exit")]
    [SerializeField] private string clearExitClipName;
    [SerializeField] private string defeatExitClipName;
    [SerializeField] private string abortedExitClipName;

    [Header("Musical Transition")]
    [SerializeField, Min(1f)] private float bpm = 120f;
    [SerializeField, Min(1)] private int beatsPerBar = 4;
    [SerializeField] private EDungeonBgmTransitionMode transitionMode =
        EDungeonBgmTransitionMode.NextBar;
    [SerializeField, Min(0.05f)] private float scheduleLeadTime = 0.1f;

    public string IntroClipName => Normalize(introClipName);
    public float Bpm => Mathf.Max(1f, bpm);
    public int BeatsPerBar => Mathf.Max(1, beatsPerBar);
    public EDungeonBgmTransitionMode TransitionMode => transitionMode;
    public float ScheduleLeadTime => Mathf.Max(0.05f, scheduleLeadTime);
    public IReadOnlyList<DungeonBgmPhaseLoop> PhaseLoops => phaseLoops;

    public string ResolveLoopClipName(EDungeonPhase phase)
    {
        if (phaseLoops != null)
        {
            foreach (DungeonBgmPhaseLoop entry in phaseLoops)
            {
                if (entry != null && entry.Phase == phase &&
                    !string.IsNullOrEmpty(entry.ClipName))
                {
                    return entry.ClipName;
                }
            }
        }

        return Normalize(defaultLoopClipName);
    }

    public string ResolveExitClipName(EDungeonBgmExitReason reason)
    {
        return reason switch
        {
            EDungeonBgmExitReason.Clear => Normalize(clearExitClipName),
            EDungeonBgmExitReason.Defeat => Normalize(defeatExitClipName),
            _ => Normalize(abortedExitClipName),
        };
    }

    private void OnValidate()
    {
        introClipName = Normalize(introClipName);
        defaultLoopClipName = Normalize(defaultLoopClipName);
        clearExitClipName = Normalize(clearExitClipName);
        defeatExitClipName = Normalize(defeatExitClipName);
        abortedExitClipName = Normalize(abortedExitClipName);
        bpm = Mathf.Max(1f, bpm);
        beatsPerBar = Mathf.Max(1, beatsPerBar);
        scheduleLeadTime = Mathf.Max(0.05f, scheduleLeadTime);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
