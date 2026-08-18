using System;
using System.Collections.Generic;
using PS260714.Localization;
using UnityEngine;

public partial class DungeonPage : IPracticeBattleController
{
    private const string PracticeUnavailableMessage =
        LocalizationKeys.UiPracticeUnavailable;
    private const string PracticeSelectionPendingMessage =
        LocalizationKeys.UiPracticeSelectionPending;
    private const string PracticeBoardFullMessage =
        LocalizationKeys.UiPracticeBoardFull;

    private string _practiceLastMessageKey = string.Empty;

    public bool IsPracticeBattle => IsPracticeMode;
    public bool IsDebugVisualizationEnabled =>
        (board as IPracticeBattleDebugVisualization)
        ?.PracticeDebugVisualizationEnabled == true;
    public IReadOnlyList<CharacterSO> CharacterCatalog =>
        CharacterDefinitionCatalog.GetAll();
    public IReadOnlyList<EnemySO> EnemyCatalog =>
        EnemyDefinitionCatalog.GetAll();
    public IReadOnlyList<BattleCardSO> CardCatalog =>
        BattleCardCatalog.GetAll();
    public IReadOnlyList<CharacterRuntime> ActiveCharacters =>
        _ownedTurrets;
    public string LastMessageKey => _practiceLastMessageKey;

    public event Action Changed;

    public bool TrySetCharacter(
        CharacterSO definition,
        int slotIndex)
    {
        if (!CanMutatePracticeBattle() || definition == null ||
            slotIndex < 0 || slotIndex >= MaximumPartySize ||
            playerCharacters == null ||
            slotIndex >= playerCharacters.Length)
        {
            return SetPracticeFailure(PracticeUnavailableMessage);
        }

        for (int index = 0; index < playerCharacters.Length; index++)
        {
            CharacterRuntime candidate = playerCharacters[index];
            if (index != slotIndex && candidate != null &&
                candidate.gameObject.activeSelf &&
                ReferenceEquals(candidate.Definition, definition))
            {
                return SetPracticeFailure(PracticeUnavailableMessage);
            }
        }

        CharacterRuntime slot = playerCharacters[slotIndex];
        if (slot == null)
            return SetPracticeFailure(PracticeUnavailableMessage);

        bool wasActive = slot.gameObject.activeSelf;
        CharacterSO previousDefinition = slot.Definition;
        if (wasActive && !_battleManager.TrySetBattleCharacters(
                BuildPracticeParty(slot)))
        {
            return SetPracticeFailure(PracticeUnavailableMessage);
        }
        if (!slot.ConfigureDetachedDefinition(definition))
        {
            if (wasActive && previousDefinition != null)
            {
                slot.ConfigureDetachedDefinition(previousDefinition);
                _battleManager.TrySetBattleCharacters(
                    BuildPracticeParty(),
                    true);
            }
            return SetPracticeFailure(PracticeUnavailableMessage);
        }

        slot.ConfigurePartySlot(slotIndex, partySlotColors[slotIndex]);
        slot.BeginDungeonRun();
        slot.gameObject.SetActive(true);
        RebuildPracticePartyFromSlots();
        if (!SynchronizePracticeParty())
            return SetPracticeFailure(PracticeUnavailableMessage);

        return SetPracticeSuccess();
    }

    public bool TryRemoveCharacter(int slotIndex)
    {
        if (!CanMutatePracticeBattle() || playerCharacters == null ||
            slotIndex < 0 || slotIndex >= playerCharacters.Length ||
            playerCharacters[slotIndex] == null ||
            !playerCharacters[slotIndex].gameObject.activeSelf)
        {
            return SetPracticeFailure(PracticeUnavailableMessage);
        }

        CharacterRuntime removed = playerCharacters[slotIndex];
        List<IBattleCharacter> next = BuildPracticeParty(
            removed);
        if (!_battleManager.TrySetBattleCharacters(next))
            return SetPracticeFailure(PracticeUnavailableMessage);

        removed.gameObject.SetActive(false);
        RebuildPracticePartyFromSlots();
        RebindPracticeCardRuntime(next);
        return SetPracticeSuccess();
    }

    public bool TryPlaceCharacter(int slotIndex, Vector2 worldPoint)
    {
        if (!CanMutatePracticeBattle() || playerCharacters == null ||
            slotIndex < 0 || slotIndex >= playerCharacters.Length)
        {
            return SetPracticeFailure(PracticeUnavailableMessage);
        }

        CharacterRuntime character = playerCharacters[slotIndex];
        IBattleSpatialService spatial =
            (board as IBattleSpatialServiceProvider)?.SpatialService;
        if (character == null || !character.gameObject.activeSelf ||
            spatial == null || spatial.MoveAlliesToPoint(
                new IBattleCharacter[] { character },
                worldPoint,
                true) <= 0)
        {
            return SetPracticeFailure(PracticeUnavailableMessage);
        }

        return SetPracticeSuccess();
    }

    public bool TrySpawnEnemy(
        EnemySO definition,
        int count,
        bool queue)
    {
        if (!CanMutatePracticeBattle() || definition == null || count <= 0)
            return SetPracticeFailure(PracticeUnavailableMessage);

        count = Mathf.Clamp(count, 1, 50);
        int accepted = 0;
        for (int index = 0; index < count; index++)
        {
            EnemyRuntime enemy = definition.CreateRuntime();
            bool added = queue
                ? _battleManager.QueueEnemy(enemy)
                : _battleManager.TrySpawnEnemyImmediately(enemy);
            if (!added)
                break;
            accepted++;
        }

        if (accepted <= 0)
        {
            return SetPracticeFailure(
                queue
                    ? PracticeUnavailableMessage
                    : PracticeBoardFullMessage);
        }

        SetPracticeMessage(
            accepted < count ? PracticeBoardFullMessage : string.Empty);
        return true;
    }

    public bool TryAddCard(BattleCardSO definition)
    {
        if (!CanMutatePracticeBattle() || definition == null ||
            !_battleCardDeck.TryCreateCardInZone(
                definition,
                BattleCardZone.Hand,
                out _))
        {
            return SetPracticeFailure(PracticeUnavailableMessage);
        }

        return SetPracticeSuccess();
    }

    public bool TryClearEnemies()
    {
        if (!CanMutatePracticeBattle() ||
            !_battleManager.TryClearAllEnemiesAndSpawns())
        {
            return SetPracticeFailure(PracticeUnavailableMessage);
        }

        return SetPracticeSuccess();
    }

    public bool TryRestoreParty()
    {
        if (!CanMutatePracticeBattle())
            return SetPracticeFailure(PracticeUnavailableMessage);

        for (int index = 0; index < _ownedTurrets.Count; index++)
        {
            CharacterRuntime character = _ownedTurrets[index];
            character?.ResetRuntime();
            character?.BeginDungeonRun();
        }
        return SetPracticeSuccess();
    }

    public bool TryRestoreCore()
    {
        if (!CanMutatePracticeBattle() ||
            !_battleManager.TryRestoreObjective())
        {
            return SetPracticeFailure(PracticeUnavailableMessage);
        }

        CaptureDungeonShieldHealth();
        return SetPracticeSuccess();
    }

    public bool TryRefillEnergy()
    {
        if (!CanMutatePracticeBattle() ||
            !_battleManager.TryRefillActiveSkillResource())
        {
            return SetPracticeFailure(PracticeUnavailableMessage);
        }

        return SetPracticeSuccess();
    }

    public bool TrySetDebugVisualization(bool enabled)
    {
        IPracticeBattleDebugVisualization visualization =
            board as IPracticeBattleDebugVisualization;
        if (enabled && !IsPracticeMode)
            return false;
        if (visualization == null)
            return SetPracticeFailure(PracticeUnavailableMessage);

        bool target = enabled && IsPracticeMode;
        if (visualization.PracticeDebugVisualizationEnabled == target)
            return true;

        visualization.SetPracticeDebugVisualization(target);
        if (visualization.PracticeDebugVisualizationEnabled != target)
            return SetPracticeFailure(PracticeUnavailableMessage);

        return SetPracticeSuccess();
    }

    public bool TryResetPractice()
    {
        if (!CanMutatePracticeBattle())
            return SetPracticeFailure(PracticeUnavailableMessage);

        DisablePracticeDebugVisualization();
        _battleCardRuntime.Clear();
        if (!_battleManager.EndBattle(board))
            return SetPracticeFailure(PracticeUnavailableMessage);

        _battleCardDeck.Clear();
        _dungeonShieldMaximumHealth =
            _session.Definition.BattleShieldMaximumHealth;
        _dungeonShieldCurrentHealth = _dungeonShieldMaximumHealth;
        for (int index = 0; index < _ownedTurrets.Count; index++)
        {
            CharacterRuntime character = _ownedTurrets[index];
            character?.ResetRuntime();
            character?.BeginDungeonRun();
        }

        bool started = StartNewBattle();
        if (!started)
            return SetPracticeFailure(PracticeUnavailableMessage);
        return SetPracticeSuccess();
    }

    public void ExitPractice()
    {
        if (!IsPracticeMode)
            return;

        DisablePracticeDebugVisualization();
        ReturnToStageSelect();
        SetPracticeMessage(string.Empty);
    }

    private bool CanMutatePracticeBattle()
    {
        if (!IsPracticeMode || _battleManager == null ||
            !_battleManager.HasSession ||
            _battleManager.State == EBattleState.Completed)
        {
            return false;
        }

        if (_battleManager.IsManualTargetSelectionPending ||
            _battleCardRuntime.IsExecutionPending ||
            _battleCardDeck.IsZoneSelectionPending)
        {
            SetPracticeMessage(PracticeSelectionPendingMessage);
            return false;
        }

        if (string.Equals(
                _practiceLastMessageKey,
                PracticeSelectionPendingMessage,
                StringComparison.Ordinal))
        {
            _practiceLastMessageKey = string.Empty;
        }

        return true;
    }

    private void RebuildPracticePartyFromSlots()
    {
        _ownedTurrets.Clear();
        _acquiredCharacterIds.Clear();
        if (playerCharacters == null)
            return;

        for (int index = 0; index < playerCharacters.Length; index++)
        {
            CharacterRuntime character = playerCharacters[index];
            if (character == null || !character.gameObject.activeSelf)
                continue;
            _ownedTurrets.Add(character);
            RecordAcquiredCharacter(character.Definition);
        }
    }

    private List<IBattleCharacter> BuildPracticeParty(
        CharacterRuntime excluded = null)
    {
        List<IBattleCharacter> result = new(MaximumPartySize);
        if (playerCharacters == null)
            return result;

        for (int index = 0; index < playerCharacters.Length; index++)
        {
            CharacterRuntime character = playerCharacters[index];
            if (character == null || ReferenceEquals(character, excluded) ||
                !character.gameObject.activeSelf)
            {
                continue;
            }
            result.Add(character);
        }
        return result;
    }

    private bool SynchronizePracticeParty()
    {
        List<IBattleCharacter> characters = BuildPracticeParty();
        if (!_battleManager.TrySetBattleCharacters(characters, true))
            return false;
        RebindPracticeCardRuntime(characters);
        return true;
    }

    private void RebindPracticeCardRuntime(
        IReadOnlyList<IBattleCharacter> characters)
    {
        _battleCardRuntime.Bind(
            board,
            _battleManager,
            _battleCardDeck,
            characters);
    }

    private bool SetPracticeSuccess()
    {
        SetPracticeMessage(string.Empty);
        return true;
    }

    private bool SetPracticeFailure(string messageKey)
    {
        if (string.Equals(
                _practiceLastMessageKey,
                PracticeSelectionPendingMessage,
                StringComparison.Ordinal) &&
            string.Equals(
                messageKey,
                PracticeUnavailableMessage,
                StringComparison.Ordinal))
        {
            return false;
        }

        SetPracticeMessage(messageKey);
        return false;
    }

    private void SetPracticeMessage(string messageKey)
    {
        _practiceLastMessageKey = messageKey ?? string.Empty;
        Changed?.Invoke();
        battleTab?.Refresh();
    }

    private void NotifyPracticeModeChanged()
    {
        if (!IsPracticeMode)
            DisablePracticeDebugVisualization();
        _practiceLastMessageKey = string.Empty;
        Changed?.Invoke();
    }

    private void DisablePracticeDebugVisualization()
    {
        if (board is IPracticeBattleDebugVisualization visualization &&
            visualization.PracticeDebugVisualizationEnabled)
        {
            visualization.SetPracticeDebugVisualization(false);
        }
    }
}
