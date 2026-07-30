using UnityEngine;

public readonly struct RecruitRevealEntry
{
    public string RewardId { get; }
    public RecruitRewardType RewardType { get; }
    public string DisplayName { get; }
    public CharacterGrade Grade { get; }
    public Sprite Icon { get; }
    public long Amount { get; }
    public bool IsNew { get; }

    public RecruitRevealEntry(
        string rewardId,
        RecruitRewardType rewardType,
        string displayName,
        CharacterGrade grade,
        Sprite icon,
        long amount,
        bool isNew)
    {
        RewardId = rewardId?.Trim() ?? string.Empty;
        RewardType = rewardType;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? "DUMMY"
            : displayName.Trim();
        Grade = CharacterGradePresentation.Clamp(grade);
        Icon = icon;
        Amount = amount < 1L ? 1L : amount;
        IsNew = isNew;
    }

    public RecruitRevealEntry(
        string rewardId,
        string displayName,
        CharacterGrade grade,
        Sprite icon,
        bool isNew)
        : this(
            rewardId,
            RecruitRewardType.Dummy,
            displayName,
            grade,
            icon,
            1L,
            isNew)
    {
    }

    public static RecruitRevealEntry FromReward(
        RecruitRewardResult entry,
        int index)
    {
        return entry != null
            ? new RecruitRevealEntry(
                string.IsNullOrWhiteSpace(entry.RewardId)
                    ? $"dummy.{index}"
                    : entry.RewardId,
                entry.RewardType,
                entry.DisplayName,
                entry.Grade,
                entry.Icon,
                entry.Amount,
                entry.IsNew)
            : new RecruitRevealEntry(
                $"dummy.{index}",
                RecruitRewardType.Dummy,
                "DUMMY",
                CharacterGrade.Grade0,
                null,
                1L,
                false);
    }

    public static RecruitRevealEntry FromDummy(
        RecruitDummyPoolEntry entry,
        int index)
    {
        return FromReward(
            entry != null
                ? new RecruitRewardResult(entry, true, false)
                : null,
            index);
    }
}
