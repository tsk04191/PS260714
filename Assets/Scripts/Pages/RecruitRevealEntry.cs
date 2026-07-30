using UnityEngine;

public readonly struct RecruitRevealEntry
{
    public string RewardId { get; }
    public string DisplayName { get; }
    public CharacterGrade Grade { get; }
    public Sprite Icon { get; }
    public bool IsNew { get; }

    public RecruitRevealEntry(
        string rewardId,
        string displayName,
        CharacterGrade grade,
        Sprite icon,
        bool isNew)
    {
        RewardId = rewardId?.Trim() ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? "DUMMY"
            : displayName.Trim();
        Grade = CharacterGradePresentation.Clamp(grade);
        Icon = icon;
        IsNew = isNew;
    }

    public static RecruitRevealEntry FromDummy(
        RecruitDummyPoolEntry entry,
        int index)
    {
        return entry != null
            ? new RecruitRevealEntry(
                $"dummy.{index}",
                entry.DisplayName,
                entry.Grade,
                null,
                false)
            : new RecruitRevealEntry(
                $"dummy.{index}",
                "DUMMY",
                CharacterGrade.Grade0,
                null,
                false);
    }
}
