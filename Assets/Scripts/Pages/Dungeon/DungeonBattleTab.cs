using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DungeonBattleTab : MonoBehaviour
{
    [Header("Time Controls")]
    [SerializeField] private Button speedButton;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private Button pauseButton;
    [SerializeField] private TextMeshProUGUI pauseText;
    [SerializeField] private GameObject pauseOverlay;

    [Header("Debug Controls")]
    [SerializeField] private Button debugButton;
    [SerializeField] private GameObject debugPopup;
    [SerializeField] private Button spawnEnemyButton;

    [Header("Enemy Spawn Queue")]
    [SerializeField] private DungeonSpawnQueueView spawnQueueView;

    [Header("Page Navigation")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject dungeonPage;
    [SerializeField] private GameObject settingPage;

    private BattleManager _battleManager;
    private bool _initialized;
    private bool _controlEventsBound;
    private bool _battleEventsBound;

    private void OnEnable()
    {
        if (!_initialized)
            return;

        BindControlEvents();
        BindBattleEvents();
        Refresh();
    }

    private void OnDisable()
    {
        HideDebugPopup();
        UnbindControlEvents();
        UnbindBattleEvents();
    }

    private void OnDestroy()
    {
        Teardown();
    }

    public bool Initialize(BattleManager battleManager)
    {
        if (battleManager == null)
        {
            Debug.LogError("DungeonBattleTab requires a BattleManager.", this);
            return false;
        }

        if (!_initialized && !ValidateReferences())
            return false;

        if (_battleManager != battleManager)
        {
            UnbindBattleEvents();
            _battleManager = battleManager;
        }

        _initialized = true;
        if (isActiveAndEnabled)
        {
            BindControlEvents();
            BindBattleEvents();
            Refresh();
        }

        return true;
    }

    public void Teardown()
    {
        UnbindControlEvents();
        UnbindBattleEvents();
        _battleManager = null;
        _initialized = false;
    }

    public void Refresh()
    {
        RefreshSpawnQueue();
        RefreshTimeControls();
    }

    public void HideDebugPopup()
    {
        if (debugPopup != null)
            debugPopup.SetActive(false);
    }

    private bool ValidateReferences()
    {
        if (speedButton == null || speedText == null || pauseButton == null ||
            pauseText == null || pauseOverlay == null || debugButton == null ||
            debugPopup == null || spawnEnemyButton == null || spawnQueueView == null ||
            settingsButton == null || dungeonPage == null || settingPage == null)
        {
            Debug.LogError("DungeonBattleTab scene references are incomplete.", this);
            return false;
        }

        return spawnQueueView.Initialize();
    }

    private void BindControlEvents()
    {
        if (_controlEventsBound)
            return;

        speedButton.onClick.AddListener(HandleSpeedClicked);
        pauseButton.onClick.AddListener(HandlePauseClicked);
        debugButton.onClick.AddListener(HandleDebugClicked);
        spawnEnemyButton.onClick.AddListener(HandleSpawnEnemyClicked);
        settingsButton.onClick.AddListener(HandleSettingsClicked);
        _controlEventsBound = true;
    }

    private void UnbindControlEvents()
    {
        if (!_controlEventsBound)
            return;

        if (speedButton != null)
            speedButton.onClick.RemoveListener(HandleSpeedClicked);
        if (pauseButton != null)
            pauseButton.onClick.RemoveListener(HandlePauseClicked);
        if (debugButton != null)
            debugButton.onClick.RemoveListener(HandleDebugClicked);
        if (spawnEnemyButton != null)
            spawnEnemyButton.onClick.RemoveListener(HandleSpawnEnemyClicked);
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(HandleSettingsClicked);
        _controlEventsBound = false;
    }

    private void BindBattleEvents()
    {
        if (_battleEventsBound || _battleManager == null)
            return;

        _battleManager.StateChanged += HandleBattleStateChanged;
        _battleManager.SpawnQueueChanged += RefreshSpawnQueue;
        _battleManager.SpawnTimerChanged += RefreshSpawnTimer;
        _battleManager.TimeControlChanged += RefreshTimeControls;
        _battleEventsBound = true;
    }

    private void UnbindBattleEvents()
    {
        if (!_battleEventsBound)
            return;

        if (_battleManager != null)
        {
            _battleManager.StateChanged -= HandleBattleStateChanged;
            _battleManager.SpawnQueueChanged -= RefreshSpawnQueue;
            _battleManager.SpawnTimerChanged -= RefreshSpawnTimer;
            _battleManager.TimeControlChanged -= RefreshTimeControls;
        }

        _battleEventsBound = false;
    }

    private void HandleSpeedClicked()
    {
        _battleManager?.CycleGameSpeed();
    }

    private void HandlePauseClicked()
    {
        _battleManager?.TogglePause();
    }

    private void HandleDebugClicked()
    {
        if (debugPopup != null)
            debugPopup.SetActive(!debugPopup.activeSelf);
    }

    private void HandleSpawnEnemyClicked()
    {
        _battleManager?.SpawnNextEnemyImmediately();
    }

    private void HandleSettingsClicked()
    {
        PageControl.PagToPag(dungeonPage, settingPage, PageOpenMode.Fresh);
    }

    private void HandleBattleStateChanged(EBattleState _)
    {
        RefreshTimeControls();
    }

    private void RefreshSpawnQueue()
    {
        if (spawnQueueView == null)
            return;

        IReadOnlyList<DungeonEnemyData> enemies = _battleManager != null
            ? _battleManager.SpawnQueue
            : Array.Empty<DungeonEnemyData>();
        spawnQueueView.RefreshQueue(enemies);
        RefreshSpawnTimer();
    }

    private void RefreshSpawnTimer()
    {
        if (spawnQueueView == null)
            return;

        spawnQueueView.RefreshTimer(
            _battleManager != null ? _battleManager.SpawnTimeRemaining : 0f,
            _battleManager != null ? _battleManager.SpawnInterval : 0f,
            _battleManager != null ? _battleManager.PendingEnemyCount : 0,
            _battleManager != null && _battleManager.IsBoardFull);
    }

    private void RefreshTimeControls()
    {
        float gameSpeed = _battleManager != null ? _battleManager.GameSpeed : 1f;
        bool isPaused = _battleManager != null && _battleManager.IsPaused;

        if (speedText != null)
            speedText.text = $"{gameSpeed:0.#}X";
        if (pauseText != null)
            pauseText.text = isPaused ? "RESUME" : "PAUSE";
        if (pauseOverlay != null)
            pauseOverlay.SetActive(isPaused);
    }
}
