using System;
using System.Collections.Generic;
using UnityEngine;

public enum EDungeonPhase
{
    Battle,
    Event,
    Rest,
    Shop,
}

[DisallowMultipleComponent]
public sealed class DungeonFlowController : MonoBehaviour
{
    [Header("Dungeon Tabs")]
    [SerializeField] private GameObject battleTab;
    [SerializeField] private GameObject eventTab;
    [SerializeField] private GameObject restTab;
    [SerializeField] private GameObject shopTab;

    [Header("Phase Sequence")]
    [SerializeField] private EDungeonPhase[] phaseSequence =
    {
        EDungeonPhase.Battle,
        EDungeonPhase.Event,
        EDungeonPhase.Battle,
    };

    private bool _initialized;

    public EDungeonPhase CurrentPhase { get; private set; } = EDungeonPhase.Battle;
    public int CurrentStepIndex { get; private set; }
    public int StepCount => phaseSequence != null ? phaseSequence.Length : 0;
    public int CurrentBattleNumber => CurrentPhase == EDungeonPhase.Battle
        ? CurrentStepIndex / 2 + 1
        : Mathf.Max(1, (CurrentStepIndex + 1) / 2);
    public bool IsCompleted { get; private set; }
    public IReadOnlyList<EDungeonPhase> PhaseSequence => phaseSequence;
    public GameObject EventTab => eventTab;

    public event Action<EDungeonPhase, int> PhaseChanged;
    public event Action FlowCompleted;

    public bool Initialize()
    {
        if (_initialized)
            return true;

        if (!ValidateConfiguration())
            return false;

        _initialized = true;
        CurrentStepIndex = 0;
        CurrentPhase = phaseSequence[CurrentStepIndex];
        IsCompleted = false;
        return ApplyCurrentPhase(false);
    }

    public bool ResetFlow()
    {
        if (!_initialized)
            return Initialize();

        CurrentStepIndex = 0;
        CurrentPhase = phaseSequence[CurrentStepIndex];
        IsCompleted = false;
        return ApplyCurrentPhase();
    }

    public bool StartBattleEventRun(int battleCount)
    {
        if (!_initialized && !Initialize())
            return false;

        battleCount = Mathf.Max(1, battleCount);
        phaseSequence = new EDungeonPhase[battleCount * 2 - 1];
        for (int index = 0; index < phaseSequence.Length; index++)
        {
            phaseSequence[index] = index % 2 == 0
                ? EDungeonPhase.Battle
                : EDungeonPhase.Event;
        }

        CurrentStepIndex = 0;
        CurrentPhase = EDungeonPhase.Battle;
        IsCompleted = false;
        return ApplyCurrentPhase();
    }

    public bool ShowEventTab()
    {
        if (!_initialized && !Initialize())
            return false;

        return SetActiveTab(eventTab);
    }

    public bool RefreshCurrentPhase()
    {
        if (!_initialized)
            return Initialize();

        return ApplyCurrentPhase();
    }

    public bool TryAdvance()
    {
        if (!_initialized && !Initialize())
            return false;

        if (IsCompleted)
            return false;

        int nextStepIndex = CurrentStepIndex + 1;
        if (nextStepIndex >= phaseSequence.Length)
        {
            IsCompleted = true;
            FlowCompleted?.Invoke();
            return false;
        }

        CurrentStepIndex = nextStepIndex;
        CurrentPhase = phaseSequence[CurrentStepIndex];
        return ApplyCurrentPhase();
    }

    [ContextMenu("Debug/Advance Dungeon Phase")]
    private void DebugAdvancePhase()
    {
        if (Application.isPlaying)
            TryAdvance();
    }

    [ContextMenu("Debug/Reset Dungeon Flow")]
    private void DebugResetFlow()
    {
        if (Application.isPlaying)
            ResetFlow();
    }

    private bool ApplyCurrentPhase(bool notifyPhaseChanged = true)
    {
        GameObject targetTab = GetTab(CurrentPhase);
        if (!SetActiveTab(targetTab))
            return false;

        if (notifyPhaseChanged)
            PhaseChanged?.Invoke(CurrentPhase, CurrentStepIndex);

        return true;
    }

    private bool SetActiveTab(GameObject targetTab)
    {
        if (targetTab == null)
            return false;

        battleTab.SetActive(targetTab == battleTab);
        eventTab.SetActive(targetTab == eventTab);
        restTab.SetActive(targetTab == restTab);
        shopTab.SetActive(targetTab == shopTab);
        return true;
    }

    private GameObject GetTab(EDungeonPhase phase)
    {
        switch (phase)
        {
            case EDungeonPhase.Event:
                return eventTab;
            case EDungeonPhase.Rest:
                return restTab;
            case EDungeonPhase.Shop:
                return shopTab;
            default:
                return battleTab;
        }
    }

    private bool ValidateConfiguration()
    {
        if (battleTab == null || eventTab == null || restTab == null ||
            shopTab == null)
        {
            Debug.LogError("DungeonFlowController requires all four dungeon tabs.", this);
            return false;
        }

        HashSet<GameObject> uniqueTabs = new()
        {
            battleTab,
            eventTab,
            restTab,
            shopTab,
        };
        if (uniqueTabs.Count != 4)
        {
            Debug.LogError("DungeonFlowController tabs must reference different objects.", this);
            return false;
        }

        if (phaseSequence == null || phaseSequence.Length == 0)
        {
            Debug.LogError("DungeonFlowController requires at least one phase.", this);
            return false;
        }

        return true;
    }
}
