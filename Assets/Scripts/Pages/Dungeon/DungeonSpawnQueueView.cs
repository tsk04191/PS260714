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

    [Header("Collapsible Panel")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Button collapseButton;
    [SerializeField] private TextMeshProUGUI collapseArrowText;
    [SerializeField] private GameObject[] expandedOnly =
        System.Array.Empty<GameObject>();
    [SerializeField, Min(40f)] private float expandedWidth = 300f;
    [SerializeField, Min(40f)] private float collapsedWidth = 48f;

    private readonly List<DungeonSpawnQueueItemView> _items = new();
    private bool _initialized;
    private bool _collapsed;
    private int _visibleItemCount;

    public bool Initialize()
    {
        if (_initialized)
            return true;

        if (timerText == null || timerFill == null || content == null ||
            itemPrefab == null || panelRect == null ||
            collapseButton == null || collapseArrowText == null)
        {
            Debug.LogError("DungeonSpawnQueueView scene and prefab references are incomplete.", this);
            return false;
        }

        LocalizationFontResolver.ApplyGameDefault(timerText);
        LocalizationFontResolver.ApplyGameDefault(collapseArrowText);
        CollectAuthoredItems();
        collapseButton.onClick.AddListener(ToggleCollapsed);
        ApplyCollapsedState();
        _initialized = true;
        return true;
    }

    private void OnDestroy()
    {
        if (collapseButton != null)
            collapseButton.onClick.RemoveListener(ToggleCollapsed);
    }

    private void ToggleCollapsed()
    {
        _collapsed = !_collapsed;
        ApplyCollapsedState();
    }

    private void ApplyCollapsedState()
    {
        if (panelRect != null)
        {
            Vector2 size = panelRect.sizeDelta;
            size.x = _collapsed
                ? Mathf.Max(40f, collapsedWidth)
                : Mathf.Max(collapsedWidth, expandedWidth);
            panelRect.sizeDelta = size;
        }

        if (expandedOnly != null)
        {
            for (int index = 0; index < expandedOnly.Length; index++)
            {
                if (expandedOnly[index] != null)
                    expandedOnly[index].SetActive(!_collapsed);
            }
        }

        if (collapseArrowText != null)
            collapseArrowText.text = _collapsed ? "<" : ">";
    }

    private void CollectAuthoredItems()
    {
        for (int index = 0; index < content.childCount; index++)
        {
            DungeonSpawnQueueItemView item = content.GetChild(index)
                .GetComponent<DungeonSpawnQueueItemView>();
            if (item == null || _items.Contains(item))
                continue;

            item.gameObject.SetActive(false);
            _items.Add(item);
        }
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
