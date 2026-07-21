using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
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
    public int CurrentBattleNumber
    {
        get
        {
            int count = 0;
            int end = Mathf.Min(CurrentStepIndex, StepCount - 1);
            for (int index = 0; index <= end; index++)
            {
                if (phaseSequence[index] == EDungeonPhase.Battle)
                    count++;
            }

            return Mathf.Max(1, count);
        }
    }
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

        BindLocalizedPlaceholder(
            eventTab,
            "txtEventPlaceholder",
            LocalizationKeys.UiDungeonEvent);
        BindLocalizedPlaceholder(
            restTab,
            "txtRestPlaceholder",
            LocalizationKeys.UiDungeonRest);
        BindLocalizedPlaceholder(
            shopTab,
            "txtShopPlaceholder",
            LocalizationKeys.UiCommonShop);

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

        return StartRun(phaseSequence);
    }

    public bool StartRun(IReadOnlyList<EDungeonPhase> phases)
    {
        if (!_initialized && !Initialize())
            return false;
        if (phases == null || phases.Count == 0)
            return false;

        phaseSequence = new EDungeonPhase[phases.Count];
        for (int index = 0; index < phases.Count; index++)
            phaseSequence[index] = phases[index];

        CurrentStepIndex = 0;
        CurrentPhase = phaseSequence[0];
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

    public bool RefreshCurrentPhaseView()
    {
        if (!_initialized)
            return Initialize();

        return ApplyCurrentPhase(false);
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

    private static void BindLocalizedPlaceholder(
        GameObject tab,
        string objectName,
        string localizationKey)
    {
        if (tab == null)
            return;

        TextMeshProUGUI[] texts =
            tab.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int index = 0; index < texts.Length; index++)
        {
            TextMeshProUGUI text = texts[index];
            if (text == null || text.name != objectName)
                continue;

            LocalizedText localizedText = text.GetComponent<LocalizedText>();
            if (localizedText == null)
                localizedText = text.gameObject.AddComponent<LocalizedText>();
            localizedText.SetKey(localizationKey);
            return;
        }
    }
}
