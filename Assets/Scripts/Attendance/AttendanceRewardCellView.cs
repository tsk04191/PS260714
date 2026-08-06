using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AttendanceRewardCellView : MonoBehaviour
{
    [SerializeField] private Image rewardIcon;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI additionalCountText;
    [SerializeField] private Image claimedOverlay;
    [SerializeField] private GameObject todayBorder;

    public bool HasRequiredReferences =>
        rewardIcon != null && amountText != null &&
        additionalCountText != null && claimedOverlay != null &&
        todayBorder != null;

    public void Bind(
        AttendanceDayReward reward,
        bool claimed,
        bool today)
    {
        IReadOnlyList<AttendanceItemReward> rewards = reward?.Rewards;
        AttendanceItemReward primary = rewards != null && rewards.Count > 0
            ? rewards[0]
            : null;
        rewardIcon.sprite = primary?.Item?.Icon;
        rewardIcon.enabled = rewardIcon.sprite != null;
        amountText.text = primary != null
            ? $"×{primary.Amount:N0}"
            : string.Empty;
        int additionalCount = Mathf.Max(0, (rewards?.Count ?? 0) - 1);
        additionalCountText.text = additionalCount > 0
            ? $"+{additionalCount}"
            : string.Empty;
        claimedOverlay.gameObject.SetActive(claimed);
        todayBorder.SetActive(today);
    }
}
