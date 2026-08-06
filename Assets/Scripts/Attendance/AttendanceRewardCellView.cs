using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AttendanceRewardCellView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Image rewardIcon;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI additionalCountText;
    [SerializeField] private Image claimedOverlay;
    [SerializeField] private GameObject todayBorder;
    [SerializeField] private GameObject tooltip;
    [SerializeField] private TextMeshProUGUI tooltipText;

    private PopupLayerPlacement _tooltipPlacement;

    public bool HasRequiredReferences =>
        rewardIcon != null && amountText != null &&
        additionalCountText != null && claimedOverlay != null &&
        todayBorder != null && tooltip != null && tooltipText != null;

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
        tooltipText.text = FormatRewards(reward);
        HideTooltip();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltip == null || string.IsNullOrWhiteSpace(tooltipText.text))
            return;
        tooltip.SetActive(true);
        _tooltipPlacement = PopupLayerUtility.MoveToPopupLayer(
            tooltip.transform as RectTransform,
            transform as RectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    private void OnDisable()
    {
        HideTooltip();
    }

    private void HideTooltip()
    {
        if (tooltip != null)
            tooltip.SetActive(false);
        if (_tooltipPlacement.IsActive)
            PopupLayerUtility.Restore(_tooltipPlacement);
        _tooltipPlacement = default;
    }

    private static string FormatRewards(AttendanceDayReward day)
    {
        if (day?.Rewards == null)
            return string.Empty;
        List<string> lines = new();
        for (int index = 0; index < day.Rewards.Count; index++)
        {
            AttendanceItemReward reward = day.Rewards[index];
            if (reward?.Item == null)
                continue;
            lines.Add(
                $"{reward.Item.GetLocalizedDisplayName()} ×" +
                $"{reward.Amount:N0}");
        }
        return string.Join("\n", lines);
    }
}
