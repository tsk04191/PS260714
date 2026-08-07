using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class DungeonSpawnQueueView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Image timerFill;
    [SerializeField] private RectTransform content;
    [SerializeField] private DungeonSpawnQueueItemView itemPrefab;

    private readonly List<DungeonSpawnQueueItemView> _items = new();
    private bool _initialized;
    private int _visibleItemCount;
    private RectTransform _bottomReservedArea;
    private float _authoredHeight;
    private bool _refreshingLayout;

    public void ConfigureResponsiveBounds(
        RectTransform bottomReservedArea)
    {
        _bottomReservedArea = bottomReservedArea;
        RectTransform rect = transform as RectTransform;
        if (_authoredHeight <= 0f && rect != null && rect.rect.height > 0f)
            _authoredHeight = rect.rect.height;
        RefreshResponsiveLayout();
    }

    public bool Initialize()
    {
        if (_initialized)
            return true;

        if (timerText == null || timerFill == null || content == null || itemPrefab == null)
        {
            Debug.LogError("DungeonSpawnQueueView scene and prefab references are incomplete.", this);
            return false;
        }

        LocalizationFontResolver.ApplyGameDefault(timerText);
        _initialized = true;
        return true;
    }

    private void OnRectTransformDimensionsChange()
    {
        RefreshResponsiveLayout();
    }

    private void RefreshResponsiveLayout()
    {
        if (_refreshingLayout)
            return;

        RectTransform rect = transform as RectTransform;
        RectTransform parent = rect != null
            ? rect.parent as RectTransform
            : null;
        if (rect == null || parent == null || parent.rect.height <= 0f)
            return;

        if (_authoredHeight <= 0f)
            _authoredHeight = rect.rect.height;
        if (_authoredHeight <= 0f)
            return;

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        float availableTop = parent.InverseTransformPoint(corners[1]).y;
        float availableBottom = parent.rect.yMin;
        if (_bottomReservedArea != null)
        {
            _bottomReservedArea.GetWorldCorners(corners);
            availableBottom = parent.InverseTransformPoint(corners[1]).y;
        }

        float height = Mathf.Min(
            _authoredHeight,
            Mathf.Max(0f, availableTop - availableBottom));
        _refreshingLayout = true;
        rect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            height);
        _refreshingLayout = false;
    }

    public void RefreshQueue(IReadOnlyList<EnemyRuntime> enemies)
    {
        if ((!_initialized && !Initialize()) || enemies == null)
            return;

        bool resetScrollPosition = enemies.Count > _visibleItemCount;
        while (_items.Count < enemies.Count)
        {
            DungeonSpawnQueueItemView item = Instantiate(itemPrefab, content);
            item.ApplyGameDefaultFonts();
            item.name = $"grpSpawnQueueItem_{_items.Count + 1}";
            _items.Add(item);
        }

        for (int index = 0; index < _items.Count; index++)
        {
            DungeonSpawnQueueItemView item = _items[index];
            bool visible = index < enemies.Count;
            item.gameObject.SetActive(visible);

            if (visible)
                item.Setup(index + 1, enemies[index]);
        }

        _visibleItemCount = enemies.Count;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        if (resetScrollPosition)
        {
            content.anchoredPosition = new Vector2(
                content.anchoredPosition.x,
                0f);
        }
    }

    public void RefreshTimer(
        float remainingTime,
        float duration,
        int queuedEnemyCount,
        bool boardFull)
    {
        if (!_initialized && !Initialize())
            return;

        remainingTime = Mathf.Max(0f, remainingTime);
        timerFill.fillAmount = duration > 0f
            ? Mathf.Clamp01(remainingTime / duration)
            : 0f;

        if (boardFull)
        {
            timerText.text = LocalizationService.Get(
                LocalizationKeys.UiDungeonQueueBoardFull,
                LocalizationService.Arg("count", queuedEnemyCount));
        }
        else if (queuedEnemyCount <= 0)
        {
            timerText.text = LocalizationService.Get(
                LocalizationKeys.UiDungeonQueueEmpty);
        }
        else
        {
            float displayedTime = TimePrecision.FloorToTenth(remainingTime);
            timerText.text = LocalizationService.Get(
                LocalizationKeys.UiDungeonQueueNext,
                LocalizationService.Arg("seconds", displayedTime),
                LocalizationService.Arg("count", queuedEnemyCount));
        }
    }
}
