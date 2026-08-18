using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI-facing contract for the practice battle tools. The dungeon runtime owns
/// all mutations; the scene panel only presents catalogs and forwards intent.
/// </summary>
public interface IPracticeBattleController
{
    bool IsPracticeBattle { get; }
    bool IsDebugVisualizationEnabled { get; }
    IReadOnlyList<CharacterSO> CharacterCatalog { get; }
    IReadOnlyList<EnemySO> EnemyCatalog { get; }
    IReadOnlyList<BattleCardSO> CardCatalog { get; }
    IReadOnlyList<CharacterRuntime> ActiveCharacters { get; }
    string LastMessageKey { get; }

    event Action Changed;

    bool TrySetCharacter(CharacterSO definition, int slotIndex);
    bool TryRemoveCharacter(int slotIndex);
    bool TryPlaceCharacter(int slotIndex, Vector2 worldPoint);
    bool TrySpawnEnemy(EnemySO definition, int count, bool queue);
    bool TryAddCard(BattleCardSO definition);
    bool TryClearEnemies();
    bool TryRestoreParty();
    bool TryRestoreCore();
    bool TryRefillEnergy();
    bool TrySetDebugVisualization(bool enabled);
    bool TryResetPractice();
    void ExitPractice();
}

public enum PracticeBattleCatalogCategory
{
    Characters = 0,
    Enemies = 1,
    Cards = 2,
}
