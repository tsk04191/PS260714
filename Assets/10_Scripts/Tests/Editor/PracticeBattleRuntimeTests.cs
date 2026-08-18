using System;
using System.Collections.Generic;
using NUnit.Framework;
using PS260714.Localization;
using UnityEngine;
using static TestReflection;

public sealed class PracticeBattleRuntimeTests
{
    private readonly List<UnityEngine.Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            if (createdObjects[index] != null)
                UnityEngine.Object.DestroyImmediate(createdObjects[index]);
        }
        createdObjects.Clear();
    }

    [Test]
    public void DungeonPageDebugRouting_PreservesActualStateAndDisablesOnModeChange()
    {
        GameObject host = new("PracticeBattleRuntimeTests_DebugRouting");
        host.SetActive(false);
        createdObjects.Add(host);
        DungeonBoardView board = host.AddComponent<DungeonBoardView>();
        DungeonPage page = host.AddComponent<DungeonPage>();
        SetField(page, "board", board);
        DungeonDefinition practice =
            ScriptableObject.CreateInstance<DungeonDefinition>();
        createdObjects.Add(practice);
        SetField(practice, "runMode", EDungeonRunMode.Practice);
        page.RunSession.Begin(
            practice,
            1,
            1,
            new[] { EDungeonPhase.Battle });
        int changedCount = 0;
        page.Changed += () => changedCount++;

        Assert.That(page.IsPracticeBattle, Is.True);
        Assert.That(page.TrySetDebugVisualization(true), Is.False);
        Assert.That(page.IsDebugVisualizationEnabled, Is.False);
        Assert.That(
            page.LastMessageKey,
            Is.EqualTo(LocalizationKeys.UiPracticeUnavailable));
        Assert.That(changedCount, Is.EqualTo(1));

        SetField(board, "_practiceDebugVisualizationEnabled", true);
        Assert.That(page.IsDebugVisualizationEnabled, Is.True);
        Assert.That(page.TrySetDebugVisualization(false), Is.True);
        Assert.That(page.IsDebugVisualizationEnabled, Is.False);
        Assert.That(page.LastMessageKey, Is.Empty);
        Assert.That(changedCount, Is.EqualTo(2));

        SetField(board, "_practiceDebugVisualizationEnabled", true);
        page.RunSession.Reset();
        InvokeMethod(page, "NotifyPracticeModeChanged");

        Assert.That(page.IsPracticeBattle, Is.False);
        Assert.That(page.IsDebugVisualizationEnabled, Is.False);
        Assert.That(changedCount, Is.EqualTo(3));
        Assert.That(page.TrySetDebugVisualization(true), Is.False);
        Assert.That(changedCount, Is.EqualTo(3));
    }

    [Test]
    public void StandardAndPracticeOptions_KeepExistingCompletionDefault()
    {
        Assert.That(
            BattleSessionOptions.Standard.CompletionPolicy,
            Is.EqualTo(BattleCompletionPolicy.Standard));
        Assert.That(
            BattleSessionOptions.Practice.CompletionPolicy,
            Is.EqualTo(BattleCompletionPolicy.None));

        BattleManager standardManager = CreateManager();
        PracticeBoard standardBoard = new();
        Assert.That(
            standardManager.StartBattle(
                standardBoard,
                Array.Empty<IBattleCharacter>(),
                Array.Empty<EnemyRuntime>(),
                1f),
            Is.True);
        Assert.That(standardManager.State, Is.EqualTo(EBattleState.Completed));
        Assert.That(standardManager.Result, Is.EqualTo(EBattleResult.Victory));
        Assert.That(standardManager.EndBattle(standardBoard), Is.True);

        BattleManager practiceManager = CreateManager();
        BattleCoreRuntime objective = new();
        objective.Configure(100, true);
        PracticeBoard practiceBoard = new(objective);
        Assert.That(
            practiceManager.StartBattle(
                practiceBoard,
                Array.Empty<IBattleCharacter>(),
                Array.Empty<EnemyRuntime>(),
                1f,
                1f,
                sessionOptions: BattleSessionOptions.Practice),
            Is.True);

        Assert.That(practiceManager.State, Is.EqualTo(EBattleState.Running));
        InvokeMethod(practiceManager, "TickBattleTimer", 2f);
        Assert.That(practiceManager.State, Is.EqualTo(EBattleState.Running));
        Assert.That(practiceManager.BattleTimeRemaining, Is.EqualTo(1f));
        Assert.That(objective.TakeDamage(100), Is.EqualTo(100));
        Assert.That(objective.IsDestroyed, Is.True);
        Assert.That(practiceManager.State, Is.EqualTo(EBattleState.Running));
        Assert.That(practiceManager.EndBattle(practiceBoard), Is.True);
    }

    [Test]
    public void PracticeCommands_RestoreDestroyedObjectiveAndRefillEnergy()
    {
        BattleManager manager = CreateManager();
        BattleCoreRuntime objective = new();
        objective.Configure(75, true);
        PracticeBoard board = new(objective);
        Assert.That(
            manager.StartBattle(
                board,
                Array.Empty<IBattleCharacter>(),
                Array.Empty<EnemyRuntime>(),
                1f,
                sessionOptions: BattleSessionOptions.Practice),
            Is.True);

        Assert.That(manager.TrySpend(2), Is.True);
        Assert.That(manager.ActiveSkillResource, Is.EqualTo(1));
        Assert.That(manager.TryRefillActiveSkillResource(), Is.True);
        Assert.That(
            manager.ActiveSkillResource,
            Is.EqualTo(manager.MaximumActiveSkillResource));
        Assert.That(manager.ActiveSkillRechargeRemaining, Is.Zero);

        Assert.That(objective.TakeDamage(75), Is.EqualTo(75));
        Assert.That(objective.IsDestroyed, Is.True);
        Assert.That(manager.TryRestoreObjective(), Is.True);
        Assert.That(objective.IsDestroyed, Is.False);
        Assert.That(objective.CurrentHealth, Is.EqualTo(75));

        Assert.That(objective.TryGrantDamageImmunity(3f), Is.True);
        Assert.That(manager.TryRestoreObjective(), Is.True);
        Assert.That(objective.IsDamageImmune, Is.False);

        SetPrivateField(manager, "_manualTargetSelectionPending", true);
        Assert.That(manager.TryRefillActiveSkillResource(), Is.False);
        Assert.That(manager.TryRestoreObjective(), Is.False);
        SetPrivateField(manager, "_manualTargetSelectionPending", false);
        Assert.That(manager.EndBattle(board), Is.True);
    }

    [Test]
    public void TrySetBattleCharacters_UpdatesBoardAtomicallyAndGuardsSelection()
    {
        BattleManager manager = CreateManager();
        PracticeBoard board = new();
        PracticeCharacter first = new();
        PracticeCharacter second = new();
        Assert.That(
            manager.StartBattle(
                board,
                new IBattleCharacter[] { first },
                Array.Empty<EnemyRuntime>(),
                1f,
                sessionOptions: BattleSessionOptions.Practice),
            Is.True);

        int changedCount = 0;
        manager.CharactersChanged += _ => changedCount++;
        Assert.That(
            manager.TrySetBattleCharacters(
                new IBattleCharacter[] { first, second }),
            Is.True);
        Assert.That(manager.Characters, Is.EqualTo(new[] { first, second }));
        Assert.That(board.Characters, Is.EqualTo(new[] { first, second }));
        Assert.That(second.InitializeCount, Is.EqualTo(1));
        Assert.That(second.ResetCount, Is.EqualTo(1));
        Assert.That(second.IsBound, Is.True);
        Assert.That(changedCount, Is.EqualTo(1));

        Assert.That(
            manager.TrySetBattleCharacters(
                new IBattleCharacter[] { first, first }),
            Is.False);
        Assert.That(manager.Characters, Is.EqualTo(new[] { first, second }));

        SetPrivateField(manager, "_manualTargetSelectionPending", true);
        Assert.That(
            manager.TrySetBattleCharacters(Array.Empty<IBattleCharacter>()),
            Is.False);
        Assert.That(manager.Characters, Is.EqualTo(new[] { first, second }));
        SetPrivateField(manager, "_manualTargetSelectionPending", false);

        Assert.That(
            manager.TrySetBattleCharacters(Array.Empty<IBattleCharacter>()),
            Is.True);
        Assert.That(first.IsBound, Is.False);
        Assert.That(second.IsBound, Is.False);
        Assert.That(board.Characters, Is.Empty);
        Assert.That(changedCount, Is.EqualTo(2));
        Assert.That(manager.EndBattle(board), Is.True);
    }

    [Test]
    public void EnemyPracticeCommands_TrackImmediateSpawnAndClearAllSources()
    {
        BattleManager manager = CreateManager();
        PracticeBoard board = new();
        Assert.That(
            manager.StartBattle(
                board,
                Array.Empty<IBattleCharacter>(),
                Array.Empty<EnemyRuntime>(),
                2f,
                sessionOptions: BattleSessionOptions.Practice),
            Is.True);
        EnemyRuntime immediate = CreateEnemy("practice.immediate");
        EnemyRuntime queued = CreateEnemy("practice.queued");

        Assert.That(manager.TrySpawnEnemyImmediately(immediate), Is.True);
        Assert.That(board.LivingEnemyCount, Is.EqualTo(1));
        Assert.That(manager.SpawnedEnemyCount, Is.EqualTo(1));
        Assert.That(manager.MaximumEnemyCount, Is.EqualTo(1));
        Assert.That(manager.QueueEnemy(queued), Is.True);
        Assert.That(
            manager.TryScheduleSummon(
                immediate,
                "practice.schedule",
                new EnemySummonDefinition(),
                10f),
            Is.True);
        Assert.That(manager.PendingEnemyCount, Is.EqualTo(1));
        Assert.That(manager.PendingScheduledSummonCount, Is.EqualTo(1));
        Assert.That(
            manager.TryAddSpawnIntervalModifier(
                "practice.speed",
                0.5f,
                10f),
            Is.True);

        Assert.That(manager.TryClearAllEnemiesAndSpawns(), Is.True);
        Assert.That(board.LivingEnemyCount, Is.Zero);
        Assert.That(manager.PendingEnemyCount, Is.Zero);
        Assert.That(manager.PendingScheduledSummonCount, Is.Zero);
        Assert.That(manager.ActiveSummonCount, Is.Zero);
        Assert.That(manager.SpawnedEnemyCount, Is.Zero);
        Assert.That(manager.MaximumEnemyCount, Is.Zero);
        Assert.That(manager.State, Is.EqualTo(EBattleState.Running));

        SetPrivateField(manager, "_manualTargetSelectionPending", true);
        Assert.That(
            manager.TrySpawnEnemyImmediately(CreateEnemy("practice.blocked")),
            Is.False);
        Assert.That(manager.TryClearAllEnemiesAndSpawns(), Is.False);
        SetPrivateField(manager, "_manualTargetSelectionPending", false);
        Assert.That(manager.EndBattle(board), Is.True);
    }

    [Test]
    public void CardInjection_CreatesUniqueInstancesAndGuardsZoneSelection()
    {
        BattleCardDeckRuntime deck = new();
        Assert.That(
            deck.ConfigureResolvedDeck(
                new BattleCardDeckRules(),
                Array.Empty<BattleCardSO>(),
                42),
            Is.False);
        BattleCardSO firstDefinition = CreateCard();
        BattleCardSO secondDefinition = CreateCard();
        int changedCount = 0;
        deck.Changed += () => changedCount++;

        Assert.That(
            deck.TryCreateCardInZone(
                firstDefinition,
                BattleCardZone.Hand,
                out BattleCardInstance first),
            Is.True);
        Assert.That(
            deck.TryCreateCardInZone(
                secondDefinition,
                BattleCardZone.Hand,
                out BattleCardInstance second),
            Is.True);
        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);
        Assert.That(first.InstanceId, Is.Not.EqualTo(second.InstanceId));
        Assert.That(deck.Hand, Is.EqualTo(new[] { first, second }));
        Assert.That(deck.Phase, Is.EqualTo(BattleCardDeckPhase.Ready));
        Assert.That(changedCount, Is.EqualTo(2));

        Assert.That(
            deck.TryBeginZoneSelection(BattleCardZone.Hand, 0, 1),
            Is.True);
        int changedBeforeRejectedInjection = changedCount;
        Assert.That(
            deck.TryCreateCardInZone(
                firstDefinition,
                BattleCardZone.DiscardPile,
                out BattleCardInstance rejected),
            Is.False);
        Assert.That(rejected, Is.Null);
        Assert.That(changedCount, Is.EqualTo(changedBeforeRejectedInjection));
        Assert.That(deck.CancelZoneSelection(), Is.True);
        Assert.That(
            deck.TryCreateCardInZone(
                firstDefinition,
                BattleCardZone.ExhaustPile,
                out BattleCardInstance exhausted),
            Is.True);
        Assert.That(deck.ExhaustPile, Does.Contain(exhausted));

        BattleCardDeckRuntime inactive = new();
        Assert.That(
            inactive.TryCreateCardInZone(
                firstDefinition,
                BattleCardZone.Hand,
                out BattleCardInstance inactiveResult),
            Is.False);
        Assert.That(inactiveResult, Is.Null);
    }

    private BattleManager CreateManager()
    {
        GameObject host = new("PracticeBattleRuntimeTests_Manager");
        host.SetActive(false);
        createdObjects.Add(host);
        BattleManager manager = host.AddComponent<BattleManager>();
        GameManager gameManager = host.AddComponent<GameManager>();
        SetPrivateField(manager, "_manager", gameManager);
        return manager;
    }

    private EnemyRuntime CreateEnemy(string enemyId)
    {
        EnemySO definition = ScriptableObject.CreateInstance<EnemySO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        createdObjects.Add(definition);
        SetField(definition, "enemyId", enemyId);
        SetField(definition, "baseHealth", 20);
        return definition.CreateRuntime();
    }

    private BattleCardSO CreateCard()
    {
        BattleCardSO definition =
            ScriptableObject.CreateInstance<BattleCardSO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        createdObjects.Add(definition);
        return definition;
    }

    private sealed class PracticeBoard :
        IBattleBoard,
        IBattleObjectiveProvider
    {
        private readonly List<EnemyRuntime> enemies = new();
        private readonly BattleCoreRuntime objective;

        public IReadOnlyList<IBattleCharacter> Characters { get; private set; }
            = Array.Empty<IBattleCharacter>();
        public int InitialEnemyCapacity => 16;
        public int LivingEnemyCount
        {
            get
            {
                int count = 0;
                foreach (EnemyRuntime enemy in enemies)
                {
                    if (enemy?.Health > 0)
                        count++;
                }
                return count;
            }
        }
        public bool HasEmptyEnemyTile => true;
        public IBattleObjective Objective => objective;

        public event Action OccupancyChanged;
        public event Action<BattleEnemyDefeatedEvent> EnemyDefeated;
        public event Action<BattleStatusAppliedEvent> StatusApplied;

        public PracticeBoard(BattleCoreRuntime objective = null)
        {
            this.objective = objective;
        }

        public bool TryAddEnemy(EnemyRuntime enemy)
        {
            if (enemy == null || enemies.Contains(enemy))
                return false;
            enemies.Add(enemy);
            OccupancyChanged?.Invoke();
            return true;
        }

        public bool TryAddEnemiesToDistinctTiles(
            IReadOnlyList<EnemyRuntime> additions)
        {
            if (additions == null || additions.Count == 0)
                return false;
            for (int index = 0; index < additions.Count; index++)
            {
                if (additions[index] == null ||
                    enemies.Contains(additions[index]))
                {
                    return false;
                }
            }
            for (int index = 0; index < additions.Count; index++)
                enemies.Add(additions[index]);
            OccupancyChanged?.Invoke();
            return true;
        }

        public void ClearAllEnemies()
        {
            enemies.Clear();
            OccupancyChanged?.Invoke();
        }

        public void TickStatusEffects(float deltaTime)
        {
        }

        public void TickEnemyAbilities(
            float deltaTime,
            IReadOnlyList<IBattleCharacter> characters)
        {
        }

        public void SetBattleCharacters(
            IReadOnlyList<IBattleCharacter> characters)
        {
            Characters = characters != null
                ? new List<IBattleCharacter>(characters)
                : Array.Empty<IBattleCharacter>();
        }

        public void NotifyStatusApplied(BattleStatusAppliedEvent eventData)
        {
            StatusApplied?.Invoke(eventData);
        }

        public void NotifyEnemyDefeated(BattleEnemyDefeatedEvent eventData)
        {
            EnemyDefeated?.Invoke(eventData);
        }

        public IReadOnlyList<EnemyRuntime> SelectCharacterTargets(
            IBattleCharacter source,
            CharacterAttackSubject subject,
            CharacterAttackSubjectMetric metric,
            int targetCount,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions)
        {
            return Array.Empty<EnemyRuntime>();
        }

        public IReadOnlyList<IBattleCharacter> SelectAlliedCharacters(
            IBattleCharacter source,
            CharacterAttackSubject subject,
            CharacterAttackSubjectMetric metric,
            int targetCount,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions)
        {
            return Characters;
        }

        public IReadOnlyList<EnemyRuntime> FilterCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions)
        {
            return targets ?? Array.Empty<EnemyRuntime>();
        }

        public IReadOnlyList<IBattleCharacter> FilterAlliedCharacters(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            CharacterConditionMatchMode conditionMatchMode,
            IReadOnlyList<CharacterNumericCondition> numericConditions)
        {
            return targets ?? Array.Empty<IBattleCharacter>();
        }

        public IReadOnlyList<EnemyRuntime> ExpandCharacterAreaTargets(
            IReadOnlyList<EnemyRuntime> centerTargets,
            IReadOnlyList<CharacterTargetAreaOffset> areaOffsets)
        {
            return centerTargets ?? Array.Empty<EnemyRuntime>();
        }

        public int TryDamageCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            int damage,
            CharacterAttackDamageType damageType,
            bool showAttackRange)
        {
            return 0;
        }

        public int TryHealCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            int amount,
            bool showAttackRange)
        {
            return 0;
        }

        public int TryHealAlliedCharacters(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            int amount)
        {
            return 0;
        }

        public int TryGrantShieldToCharacterTargets(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            int amount,
            bool showAttackRange)
        {
            return 0;
        }

        public int TryGrantShieldToAlliedCharacters(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            int amount)
        {
            return 0;
        }

        public bool TryApplyCharacterStatus(
            BattleAbilityUser user,
            IReadOnlyList<EnemyRuntime> targets,
            StatusEffectSO statusEffect,
            float duration,
            float stacks,
            float tickInterval,
            bool showAttackRange)
        {
            return false;
        }

        public bool TryApplyAlliedCharacterStatus(
            BattleAbilityUser user,
            IReadOnlyList<IBattleCharacter> targets,
            StatusEffectSO statusEffect,
            float duration,
            float stacks)
        {
            return false;
        }

        public bool TryRemoveCharacterStatus(
            IBattleCharacter source,
            IReadOnlyList<EnemyRuntime> targets,
            CharacterStatusRemovalSelection removalSelection,
            CharacterStatusRemovalAmount removalAmount,
            bool showAttackRange)
        {
            return false;
        }

        public bool TryRemoveAlliedCharacterStatus(
            IBattleCharacter source,
            IReadOnlyList<IBattleCharacter> targets,
            CharacterStatusRemovalSelection removalSelection,
            CharacterStatusRemovalAmount removalAmount)
        {
            return false;
        }
    }

    private sealed class PracticeCharacter : IBattleCharacter
    {
        private int currentHealth = 100;

        public int InitializeCount { get; private set; }
        public int ResetCount { get; private set; }
        public bool IsBound { get; private set; }
        public int PartySlotIndex => 0;
        public int TotalDamageDealt => 0;
        public int CurrentHealth => currentHealth;
        public int MaximumHealth => 100;
        public int CurrentShield => 0;
        public float DisabledTimeRemaining => 0f;
        public float CurrentAttackPower => 1f;
        public float CurrentAttackSpeed => 1f;

        public event Action<BattleStatusChangedEvent> StatusChanged
        {
            add { }
            remove { }
        }

        public bool HasStatusEffect(StatusEffectSO statusEffect)
        {
            return false;
        }

        public int GetStatusStackCount(StatusEffectSO statusEffect)
        {
            return 0;
        }

        public IReadOnlyList<BattleStatusSnapshot> GetActiveStatusEffects()
        {
            return Array.Empty<BattleStatusSnapshot>();
        }

        public bool TryConsumeStatusStacks(
            StatusEffectSO statusEffect,
            int stackCount)
        {
            return false;
        }

        public int Heal(int amount)
        {
            int previous = currentHealth;
            currentHealth = Mathf.Min(100, currentHealth + Mathf.Max(0, amount));
            return currentHealth - previous;
        }

        public int GainShield(int amount)
        {
            return 0;
        }

        public int TakeDamage(int amount)
        {
            int applied = Mathf.Min(currentHealth, Mathf.Max(0, amount));
            currentHealth -= applied;
            return applied;
        }

        public bool CanSpendHealth(int amount)
        {
            return amount > 0 && currentHealth - amount >= 1;
        }

        public bool TrySpendHealth(int amount)
        {
            if (!CanSpendHealth(amount))
                return false;
            currentHealth -= amount;
            return true;
        }

        public bool Initialize()
        {
            InitializeCount++;
            return true;
        }

        public void BindBattle(
            IActiveSkillResource activeSkillResource,
            IBattleBoard board)
        {
            IsBound = activeSkillResource != null && board != null;
        }

        public void ResetRuntime()
        {
            ResetCount++;
            currentHealth = 100;
        }

        public void TickBattle(float deltaTime, IBattleBoard board)
        {
        }

        public void RecordDamageDealt(int damage)
        {
        }

        public void DisableFor(float duration)
        {
        }

        public bool ApplyStatusEffect(
            StatusEffectSO definition,
            float duration,
            int stacks)
        {
            return false;
        }

        public bool ApplyStatusEffect(
            StatusEffectSO definition,
            float duration,
            int stacks,
            IBattleCharacter source)
        {
            return false;
        }

        public int RemoveStatusEffects(
            CharacterStatusRemovalSelection removalSelection,
            CharacterStatusRemovalAmount removalAmount)
        {
            return 0;
        }
    }
}
