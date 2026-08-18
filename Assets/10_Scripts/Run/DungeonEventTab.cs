using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DungeonEventTab
{
    private const int RewardChoiceCount = 3;
    private const int RewardSeedSalt = unchecked((int)0xA511E9B3);

    private enum ERewardOptionType
    {
        NewTurret,
        EnergyUpgrade,
        BattleItem,
        BattleCard,
        BattleCardCategory,
        ShieldRecovery,
        BattleItemCategory,
    }

    private enum EViewMode
    {
        None,
        StartingSelection,
        StartingItemSelection,
        StartingConfigurationError,
        StartingItemConfigurationError,
        RunResult,
        RewardSelection,
        RewardContentSelection,
        ReplacementSelection,
    }

    private readonly struct RewardOption
    {
        public ERewardOptionType Type { get; }
        public int TurretSlotIndex { get; }
        public int DungeonUpgradeDefinitionIndex { get; }
        public CharacterDungeonUpgradeType DungeonUpgradeType { get; }
        public string DungeonUpgradeId { get; }
        public CharacterSO TurretDefinition { get; }
        public EDungeonEnergyUpgradeType EnergyUpgradeType { get; }
        public BattleItemSO BattleItem { get; }
        public BattleCardSO BattleCard { get; }

        private RewardOption(
            ERewardOptionType type,
            int turretSlotIndex,
            int dungeonUpgradeDefinitionIndex,
            CharacterDungeonUpgradeType dungeonUpgradeType,
            string dungeonUpgradeId,
            CharacterSO turretDefinition,
            EDungeonEnergyUpgradeType energyUpgradeType,
            BattleItemSO battleItem,
            BattleCardSO battleCard = null)
        {
            Type = type;
            TurretSlotIndex = turretSlotIndex;
            DungeonUpgradeDefinitionIndex = dungeonUpgradeDefinitionIndex;
            DungeonUpgradeType = dungeonUpgradeType;
            DungeonUpgradeId = dungeonUpgradeId ?? string.Empty;
            TurretDefinition = turretDefinition;
            EnergyUpgradeType = energyUpgradeType;
            BattleItem = battleItem;
            BattleCard = battleCard;
        }

        public static RewardOption CreateNewTurret(CharacterSO definition)
        {
            return new RewardOption(
                ERewardOptionType.NewTurret,
                -1,
                -1,
                default,
                string.Empty,
                definition,
                default,
                default);
        }

        public static RewardOption CreateEnergyUpgrade(
            EDungeonEnergyUpgradeType upgradeType)
        {
            return new RewardOption(
                ERewardOptionType.EnergyUpgrade,
                -1,
                -1,
                default,
                string.Empty,
                null,
                upgradeType,
                default);
        }

        public static RewardOption CreateBattleItem(BattleItemSO item)
        {
            return new RewardOption(
                ERewardOptionType.BattleItem,
                -1,
                -1,
                default,
                string.Empty,
                null,
                default,
                item);
        }

        public static RewardOption CreateBattleCard(BattleCardSO card)
        {
            return new RewardOption(
                ERewardOptionType.BattleCard,
                -1,
                -1,
                default,
                string.Empty,
                null,
                default,
                null,
                card);
        }

        public static RewardOption CreateCategory(ERewardOptionType type)
        {
            return new RewardOption(
                type,
                -1,
                -1,
                default,
                string.Empty,
                null,
                default,
                null);
        }
    }

    private readonly struct RewardCardContent
    {
        public string Category { get; }
        public string Title { get; }
        public string Description { get; }
        public string Footer { get; }
        public Color AccentColor { get; }

        public RewardCardContent(
            string category,
            string title,
            string description,
            string footer,
            Color accentColor)
        {
            Category = category ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Footer = footer ?? string.Empty;
            AccentColor = accentColor;
        }
    }

    private DungeonPage _page;
    private GameObject _root;
    private RectTransform _panel;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _descriptionText;
    private RectTransform _rewardCardRoot;
    private GridLayoutGroup _rewardCardLayout;
    private RectTransform _buttonRoot;
    private Button _preparationNavigationButton;
    private TextMeshProUGUI _preparationNavigationText;
    private RectTransform _firstStartingChoiceRect;
    private readonly List<RewardOption> _currentRewardOptions = new();
    private readonly List<CharacterSO> _startingChoices = new();
    private readonly List<DungeonRewardCardView> _rewardCards = new();
    private readonly List<DungeonStartingItemSlotView> _startingItemSlots =
        new();
    private readonly List<DungeonDynamicChoiceButtonView> _choiceButtons =
        new();
    private EViewMode _viewMode;
    private int _startingAvailableCount;
    private int _startingItemAvailableCount;
    private int _startingItemRequiredCount;
    private EDungeonRunResult _currentRunResult;
    private CharacterSO _replacementDefinition;
    private bool _initialized;
    private bool _localizationEventsBound;
    private bool _isBattleRewardOverlay;

    public RectTransform FirstStartingChoiceRect =>
        _firstStartingChoiceRect;

    public void Initialize(
        GameObject root,
        DungeonPage page,
        bool isBattleRewardOverlay = false)
    {
        if (root == null || page == null)
            return;

        _root = root;
        _page = page;
        _isBattleRewardOverlay = isBattleRewardOverlay;
        if (_initialized)
        {
            BindLocalizationEvents();
            return;
        }

        BuildRuntimeUi();
        _initialized = true;
        BindLocalizationEvents();
    }

    public void Teardown()
    {
        UnbindLocalizationEvents();
        _startingChoices.Clear();
        _currentRewardOptions.Clear();
        _rewardCards.Clear();
        _startingItemSlots.Clear();
        _choiceButtons.Clear();
        _replacementDefinition = null;
        _startingItemAvailableCount = 0;
        _startingItemRequiredCount = 0;
        _viewMode = EViewMode.None;
        _initialized = false;
        _titleText = null;
        _descriptionText = null;
        _rewardCardRoot = null;
        _rewardCardLayout = null;
        _buttonRoot = null;
        _preparationNavigationButton = null;
        _preparationNavigationText = null;
        _firstStartingChoiceRect = null;
        _panel = null;
        _root = null;
        _page = null;
    }

    public void SetPanelVisible(bool visible)
    {
        if (_panel != null)
            _panel.gameObject.SetActive(visible);
        if (!visible && _preparationNavigationButton != null)
            _preparationNavigationButton.gameObject.SetActive(false);
    }

    public void ShowUpgradeEvent()
    {
        if (!EnsureInitialized())
            return;

        SetPanelVisible(true);
        GenerateRewardOptions();
        if (_currentRewardOptions.Count == 0)
        {
            Debug.LogWarning(
                "No eligible battle completion reward is configured.");
            SetPanelVisible(false);
            _page.CompleteBattleReward();
            return;
        }
        _viewMode = EViewMode.RewardSelection;
        RenderRewardSelection();
    }

    public void ShowStartingCharacterSelection(
        IReadOnlyList<CharacterSO> choices)
    {
        if (!EnsureInitialized())
            return;

        SetPanelVisible(true);
        _startingChoices.Clear();
        if (choices != null)
        {
            for (int index = 0; index < choices.Count; index++)
                _startingChoices.Add(choices[index]);
        }
        _viewMode = EViewMode.StartingSelection;
        RenderStartingCharacterSelection();
    }

    public void ShowStartingItemSelection()
    {
        if (!EnsureInitialized())
            return;

        SetPanelVisible(true);
        _viewMode = EViewMode.StartingItemSelection;
        RenderStartingItemSelection();
    }

    public void ShowStartingItemConfigurationError(
        int availableCount,
        int requiredCount)
    {
        if (!EnsureInitialized())
            return;

        SetPanelVisible(true);
        _startingItemAvailableCount = Mathf.Max(0, availableCount);
        _startingItemRequiredCount = Mathf.Max(0, requiredCount);
        _viewMode = EViewMode.StartingItemConfigurationError;
        RenderStartingItemConfigurationError();
    }

    private void RenderStartingCharacterSelection()
    {
        if (!EnsureInitialized())
            return;

        ClearButtons();
        SetRewardCardMode(true);
        RefreshRuntimeLayout();
        _titleText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonStartTitle);
        _descriptionText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonStartDescription);

        int choiceCount = Mathf.Min(
            DungeonPage.StartingCharacterChoiceCount,
            _startingChoices.Count);
        for (int index = 0; index < choiceCount; index++)
        {
            CharacterSO selectedDefinition = _startingChoices[index];
            if (selectedDefinition == null)
                continue;

            RectTransform cardRect = CreateRewardCard(
                GetStartingTurretCardContent(selectedDefinition),
                () => _page.TrySelectStartingCharacter(selectedDefinition));
            if (_firstStartingChoiceRect == null)
                _firstStartingChoiceRect = cardRect;
        }
    }

    private void RenderStartingItemSelection()
    {
        if (!EnsureInitialized())
            return;

        ClearButtons();
        SetStartingItemMode();
        RefreshRuntimeLayout();
        _titleText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonStartingItemsTitle);
        _descriptionText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonStartingItemsDescription,
            LocalizationService.Arg(
                "count",
                _page.StartingItemChoices.Count),
            LocalizationService.Arg(
                "rerolls",
                _page.CurrentDungeon?.StartingItemRule
                    .RerollsPerSlot ?? 0));

        IReadOnlyList<BattleItemSO> items = _page.StartingItemChoices;
        for (int index = 0; index < items.Count; index++)
        {
            CreateStartingItemSlot(index, items[index]);
        }

        CreateButton(
            LocalizationService.Get(
                LocalizationKeys.UiDungeonStartingItemsConfirm),
            () => _page.TryConfirmStartingItems());
    }

    private void RenderStartingItemConfigurationError()
    {
        if (!EnsureInitialized())
            return;

        ClearButtons();
        SetRewardCardMode(false);
        RefreshRuntimeLayout();
        _titleText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonStartingItemsErrorTitle);
        _descriptionText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonStartingItemsErrorDescription,
            LocalizationService.Arg(
                "required",
                _startingItemRequiredCount),
            LocalizationService.Arg(
                "available",
                _startingItemAvailableCount));
        CreateButton(
            LocalizationService.Get(LocalizationKeys.UiDungeonStartRetry),
            _page.StartNewDungeonRun);
    }

    public void ShowStartingCharacterConfigurationError(int availableCount)
    {
        if (!EnsureInitialized())
            return;

        SetPanelVisible(true);
        _startingAvailableCount = Mathf.Max(0, availableCount);
        _viewMode = EViewMode.StartingConfigurationError;
        RenderStartingCharacterConfigurationError();
    }

    private void RenderStartingCharacterConfigurationError()
    {
        if (!EnsureInitialized())
            return;

        ClearButtons();
        SetRewardCardMode(false);
        RefreshRuntimeLayout();
        _titleText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonStartErrorTitle);
        _descriptionText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonStartErrorDescription,
            LocalizationService.Arg("count", _startingAvailableCount));
        CreateButton(
            LocalizationService.Get(LocalizationKeys.UiDungeonStartRetry),
            _page.StartNewDungeonRun);
    }

    public void ShowRunResult(EDungeonRunResult result)
    {
        if (!EnsureInitialized())
            return;

        SetPanelVisible(true);
        _currentRunResult = result;
        _viewMode = EViewMode.RunResult;
        RenderRunResult();
    }

    private void RenderRunResult()
    {
        if (!EnsureInitialized())
            return;

        ClearButtons();
        SetRewardCardMode(false);
        RefreshRuntimeLayout();
        bool cleared = _currentRunResult == EDungeonRunResult.Clear;
        _titleText.text = LocalizationService.Get(cleared
            ? LocalizationKeys.UiDungeonResultClearTitle
            : LocalizationKeys.UiDungeonDefeat);
        _descriptionText.text = cleared
            ? LocalizationService.Get(
                LocalizationKeys.UiDungeonResultClearDescription,
                LocalizationService.Arg("count", _page.TotalBattleCount))
            : LocalizationService.Get(
                LocalizationKeys.UiDungeonResultResetDescription);
        CreateButton(
            LocalizationService.Get(
                LocalizationKeys.UiDungeonResultStartNewRun),
            _page.StartNewDungeonRun);
        if (!cleared)
        {
            CreateButton(
                LocalizationService.Get(
                    _page.CurrentDungeon != null &&
                    _page.CurrentDungeon.CompletionDestination ==
                    EDungeonCompletionDestination.StageSelect
                        ? LocalizationKeys.UiTutorialReturn
                        : LocalizationKeys.UiDungeonResultReturnMain),
                _page.ReturnFromRunResult);
        }
    }

    private void GenerateRewardOptions()
    {
        _currentRewardOptions.Clear();
        if (_page.GetBattleCardRewardCandidates().Count > 0)
        {
            _currentRewardOptions.Add(RewardOption.CreateCategory(
                ERewardOptionType.BattleCardCategory));
        }
        if (_page.ResolveBattleShieldRecoveryAmount() > 0)
        {
            _currentRewardOptions.Add(RewardOption.CreateCategory(
                ERewardOptionType.ShieldRecovery));
        }
        if (_page.GetConsumableRewardCandidates().Count > 0)
        {
            _currentRewardOptions.Add(RewardOption.CreateCategory(
                ERewardOptionType.BattleItemCategory));
        }
        if (_page.MaximumEnergy < 5)
        {
            _currentRewardOptions.Add(RewardOption.CreateEnergyUpgrade(
                EDungeonEnergyUpgradeType.MaximumEnergy));
        }
        if (_page.EnergyRechargeDuration >
            DungeonPage.MinimumEnergyRechargeDuration)
        {
            _currentRewardOptions.Add(RewardOption.CreateEnergyUpgrade(
                EDungeonEnergyUpgradeType.RechargeSpeed));
        }
    }

    private void GenerateRewardContentOptions(ERewardOptionType category)
    {
        int battlePlanIndex = _page.CurrentBattleNumber - 1;
        int rewardSeed = battlePlanIndex >= 0 &&
                         battlePlanIndex < _page.BattlePlans.Count
            ? _page.BattlePlans[battlePlanIndex].RandomSeed ^
              RewardSeedSalt ^ (int)category
            : Environment.TickCount;
        System.Random random = new(rewardSeed);
        List<RewardOption> candidates = new();
        if (category == ERewardOptionType.BattleCardCategory)
        {
            foreach (BattleCardSO card in
                     _page.GetBattleCardRewardCandidates())
            {
                candidates.Add(RewardOption.CreateBattleCard(card));
            }
        }
        else if (category == ERewardOptionType.BattleItemCategory)
        {
            foreach (BattleItemSO item in
                     _page.GetConsumableRewardCandidates())
            {
                candidates.Add(RewardOption.CreateBattleItem(item));
            }
        }

        _currentRewardOptions.Clear();
        int choiceCount = Mathf.Min(RewardChoiceCount, candidates.Count);
        for (int index = 0; index < choiceCount; index++)
        {
            int swapIndex = random.Next(index, candidates.Count);
            (candidates[index], candidates[swapIndex]) =
                (candidates[swapIndex], candidates[index]);
            _currentRewardOptions.Add(candidates[index]);
        }
    }

    private void ShowCurrentRewardOptions()
    {
        _replacementDefinition = null;
        _viewMode = EViewMode.RewardSelection;
        RenderRewardSelection();
    }

    private void RenderRewardSelection()
    {
        if (!EnsureInitialized())
            return;

        ClearButtons();
        SetRewardCardMode(true);
        RefreshRuntimeLayout();
        bool choosingContent =
            _viewMode == EViewMode.RewardContentSelection;
        _titleText.text = LocalizationService.Get(choosingContent
            ? LocalizationKeys.UiDungeonRewardContentTitle
            : LocalizationKeys.UiDungeonRewardTitle);
        _descriptionText.text = choosingContent
            ? LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardContentSummary,
                LocalizationService.Arg(
                    "count",
                    _currentRewardOptions.Count))
            : LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardSummary,
                LocalizationService.Arg(
                    "current",
                    _page.CurrentBattleNumber),
                LocalizationService.Arg("total", _page.TotalBattleCount),
                LocalizationService.Arg(
                    "scale",
                    _page.CurrentDifficultyScale),
                LocalizationService.Arg(
                    "next",
                    _page.GetBattleDifficultyScale(
                        _page.CurrentBattleNumber + 1)),
                LocalizationService.Arg(
                    "count",
                    _currentRewardOptions.Count));

        foreach (RewardOption option in _currentRewardOptions)
        {
            RewardOption selectedOption = option;
            CreateRewardCard(GetRewardCardContent(option), () =>
            {
                SelectRewardOption(selectedOption);
            });
        }
    }

    private RewardCardContent GetRewardCardContent(RewardOption option)
    {
        if (option.Type == ERewardOptionType.BattleCardCategory)
        {
            return new RewardCardContent(
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCategoryCard),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCardSelectTitle),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCardSelectDescription),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardRunFooter),
                new Color(0.75f, 0.22f, 0.28f, 1f));
        }

        if (option.Type == ERewardOptionType.ShieldRecovery)
        {
            int amount = _page.ResolveBattleShieldRecoveryAmount();
            int before = _page.DungeonShieldCurrentHealth;
            int after = Mathf.Min(
                _page.DungeonShieldMaximumHealth,
                before + amount);
            return new RewardCardContent(
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCategoryShield),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardShieldTitle),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardShieldChange,
                    LocalizationService.Arg("before", before),
                    LocalizationService.Arg("after", after),
                    LocalizationService.Arg(
                        "maximum",
                        _page.DungeonShieldMaximumHealth)),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardShieldFooter,
                    LocalizationService.Arg("amount", amount)),
                new Color(0.22f, 0.62f, 0.82f, 1f));
        }

        if (option.Type == ERewardOptionType.BattleItemCategory)
        {
            return new RewardCardContent(
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCategoryItem),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardItemSelectTitle),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardItemSelectDescription),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardRunFooter),
                new Color(0.8f, 0.35f, 0.22f, 1f));
        }

        if (option.Type == ERewardOptionType.EnergyUpgrade)
        {
            bool maximumEnergy = option.EnergyUpgradeType ==
                                 EDungeonEnergyUpgradeType.MaximumEnergy;
            return new RewardCardContent(
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCategoryEnergy),
                LocalizationService.Get(maximumEnergy
                    ? LocalizationKeys.UiDungeonRewardEnergyMaxTitle
                    : LocalizationKeys.UiDungeonRewardEnergyRechargeTitle),
                maximumEnergy
                    ? LocalizationService.Get(
                        LocalizationKeys.UiDungeonRewardEnergyMaxChange,
                        LocalizationService.Arg(
                            "before",
                            _page.MaximumEnergy),
                        LocalizationService.Arg(
                            "after",
                            _page.MaximumEnergy + 1))
                    : LocalizationService.Get(
                        LocalizationKeys.UiDungeonRewardEnergyRechargeChange,
                        LocalizationService.Arg(
                            "before",
                            _page.EnergyRechargeDuration),
                        LocalizationService.Arg(
                            "after",
                            Mathf.Max(
                                DungeonPage.MinimumEnergyRechargeDuration,
                                _page.EnergyRechargeDuration -
                                DungeonPage.EnergyRechargeUpgradeAmount))),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardRunFooter),
                new Color(0.82f, 0.64f, 0.2f, 1f));
        }

        if (option.Type == ERewardOptionType.BattleItem)
        {
            BattleItemSO item = option.BattleItem;
            int ownedUses = _page.GetBattleItemCount(item);
            int grantedUses = item != null && !item.HasUnlimitedUses
                ? item.UsesPerAcquisition
                : 0;
            string footer = item != null && item.HasUnlimitedUses
                ? "∞"
                : LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardItemFooter,
                    LocalizationService.Arg(
                        "owned",
                        ownedUses + grantedUses));
            return new RewardCardContent(
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCategoryItem),
                item != null
                    ? item.GetLocalizedDisplayName()
                    : string.Empty,
                item != null
                    ? item.GetLocalizedDescription()
                    : string.Empty,
                footer,
                new Color(0.8f, 0.35f, 0.22f, 1f));
        }

        if (option.Type == ERewardOptionType.BattleCard)
        {
            BattleCardSO card = option.BattleCard;
            int acquiredCopies = _page.GetAcquiredBattleCardCount(card);
            return new RewardCardContent(
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCategoryCard),
                card != null
                    ? card.GetLocalizedDisplayName()
                    : string.Empty,
                card != null
                    ? card.GetLocalizedDescription()
                    : string.Empty,
                card != null
                    ? LocalizationService.Get(
                        LocalizationKeys.UiDungeonRewardCardFooter,
                        LocalizationService.Arg(
                            "cost",
                            card.EnergyCost),
                        LocalizationService.Arg(
                            "count",
                            acquiredCopies + 1))
                    : string.Empty,
                new Color(0.75f, 0.22f, 0.28f, 1f));
        }

        if (option.Type == ERewardOptionType.NewTurret)
        {
            CharacterData newTurretData =
                DungeonPage.CreateCharacterPreviewData(
                    option.TurretDefinition);
            string description = newTurretData == null
                ? LocalizationService.Get(
                    LocalizationKeys
                        .UiDungeonRewardNewTurretEmptyDescription)
                : CharacterLocalization.GetCompactSummary(newTurretData);
            string footer = newTurretData != null
                ? LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardNewTurretFooter,
                    LocalizationService.Arg(
                        "cost",
                        newTurretData.ActiveSkillCost),
                    LocalizationService.Arg(
                        "cooldown",
                        newTurretData.AttackCooldown))
                : LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardNewTurretEmptySlot);
            return new RewardCardContent(
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCategoryNewTurret),
                newTurretData != null
                    ? CharacterLocalization.GetName(newTurretData)
                    : LocalizationService.Get(
                        LocalizationKeys.UiDungeonRewardUnknownTurret),
                description,
                footer,
                new Color(0.25f, 0.52f, 0.78f, 1f));
        }

        return new RewardCardContent(
            "REWARD",
            "INVALID REWARD",
            string.Empty,
            string.Empty,
            Color.gray);
    }

    private static RewardCardContent GetStartingTurretCardContent(
        CharacterSO definition)
    {
        CharacterData data =
            DungeonPage.CreateCharacterPreviewData(definition);
        if (data == null)
        {
            return new RewardCardContent(
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardCategoryStartingTurret),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardUnknownTurret),
                LocalizationService.Get(
                    LocalizationKeys.UiDungeonRewardTurretUnavailable),
                string.Empty,
                new Color(0.25f, 0.52f, 0.78f, 1f));
        }

        string description =
            CharacterLocalization.GetNormalAttackDescription(data);
        Color accent = new(0.25f, 0.52f, 0.78f, 1f);
        return new RewardCardContent(
            LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardCategoryStartingTurret),
            CharacterLocalization.GetName(data),
            description,
            LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardStartFooter,
                LocalizationService.Arg("cost", data.ActiveSkillCost),
                LocalizationService.Arg("cooldown", data.AttackCooldown)),
            accent);
    }

    private void SelectRewardOption(RewardOption option)
    {
        if (option.Type == ERewardOptionType.BattleCardCategory ||
            option.Type == ERewardOptionType.BattleItemCategory)
        {
            GenerateRewardContentOptions(option.Type);
            _viewMode = EViewMode.RewardContentSelection;
            RenderRewardSelection();
            return;
        }

        if (option.Type == ERewardOptionType.ShieldRecovery)
        {
            _page.TryRecoverBattleShield();
            return;
        }

        if (option.Type == ERewardOptionType.EnergyUpgrade)
        {
            _page.TryApplyEnergyUpgrade(option.EnergyUpgradeType);
            return;
        }

        if (option.Type == ERewardOptionType.BattleItem)
        {
            _page.TryAcquireBattleItem(option.BattleItem);
            return;
        }

        if (option.Type == ERewardOptionType.BattleCard)
        {
            _page.TryAcquireBattleCard(option.BattleCard);
            return;
        }

        CharacterSO definition = option.TurretDefinition;
        if (definition == null)
            return;

        if (_page.OwnedTurrets.Count < DungeonPage.MaximumPartySize)
            _page.TryAcquireTurret(definition);
        else
            ShowReplacementSelection(definition);
    }

    private void ShowReplacementSelection(CharacterSO newDefinition)
    {
        _replacementDefinition = newDefinition;
        _viewMode = EViewMode.ReplacementSelection;
        RenderReplacementSelection();
    }

    private void RenderReplacementSelection()
    {
        if (!EnsureInitialized() || _replacementDefinition == null)
            return;

        ClearButtons();
        SetRewardCardMode(false);
        RefreshRuntimeLayout();
        CharacterSO selectedDefinition = _replacementDefinition;
        CharacterData newData =
            DungeonPage.CreateCharacterPreviewData(selectedDefinition);
        string newName = newData != null
            ? CharacterLocalization.GetName(newData)
            : LocalizationService.Get(
                LocalizationKeys.UiDungeonRewardUnknownTurret);
        _titleText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonReplaceTitle);
        _descriptionText.text = LocalizationService.Get(
            LocalizationKeys.UiDungeonReplaceDescription,
            LocalizationService.Arg("name", newName));

        IReadOnlyList<CharacterRuntime> turrets = _page.OwnedTurrets;
        for (int index = 0; index < turrets.Count; index++)
        {
            int slotIndex = index;
            CreateButton(
                GetTurretSummary(slotIndex, turrets[index]),
                () => _page.TryAcquireTurret(
                    selectedDefinition,
                    slotIndex));
        }
        CreateButton(
            LocalizationService.Get(LocalizationKeys.UiCommonBack),
            ShowCurrentRewardOptions);
    }

    private void BindLocalizationEvents()
    {
        if (_localizationEventsBound)
            return;

        LocalizationService.LocaleChanged += HandleLocalizationChanged;
        LocalizationService.FontChanged += HandleLocalizationChanged;
        _localizationEventsBound = true;
    }

    private void UnbindLocalizationEvents()
    {
        if (!_localizationEventsBound)
            return;

        LocalizationService.LocaleChanged -= HandleLocalizationChanged;
        LocalizationService.FontChanged -= HandleLocalizationChanged;
        _localizationEventsBound = false;
    }

    private void HandleLocalizationChanged(string unusedValue)
    {
        if (!_initialized)
            return;

        LocalizationFontResolver.ApplyGameDefault(_titleText);
        LocalizationFontResolver.ApplyGameDefault(_descriptionText);
        RenderCurrentView();
    }

    private void RenderCurrentView()
    {
        switch (_viewMode)
        {
            case EViewMode.StartingSelection:
                RenderStartingCharacterSelection();
                break;
            case EViewMode.StartingItemSelection:
                RenderStartingItemSelection();
                break;
            case EViewMode.StartingConfigurationError:
                RenderStartingCharacterConfigurationError();
                break;
            case EViewMode.StartingItemConfigurationError:
                RenderStartingItemConfigurationError();
                break;
            case EViewMode.RunResult:
                RenderRunResult();
                break;
            case EViewMode.RewardSelection:
            case EViewMode.RewardContentSelection:
                RenderRewardSelection();
                break;
            case EViewMode.ReplacementSelection:
                RenderReplacementSelection();
                break;
        }
    }

    private bool EnsureInitialized()
    {
        if (!_initialized && _root != null && _page != null)
            Initialize(_root, _page);
        return _initialized && _root != null && _page != null;
    }

    private string GetTurretSummary(int slotIndex, CharacterRuntime turret)
    {
        CharacterData data = turret?.Data;
        if (data == null)
        {
            return LocalizationService.Get(
                LocalizationKeys.UiDungeonReplaceEmptySlot,
                LocalizationService.Arg("slot", slotIndex + 1));
        }

        return $"S{slotIndex + 1} {CharacterLocalization.GetName(data)} | " +
               CharacterLocalization.GetCompactSummary(data);
    }

    private void BuildRuntimeUi()
    {
        _panel = _root.transform.Find("grpRuntimeEventPanel")
            as RectTransform;
        _titleText = _panel?.Find("txtEventTitle")
            ?.GetComponent<TextMeshProUGUI>();
        _descriptionText = _panel?.Find("txtEventDescription")
            ?.GetComponent<TextMeshProUGUI>();
        _rewardCardRoot = _panel?.Find("grpRewardCards")
            as RectTransform;
        _rewardCardLayout = _rewardCardRoot != null
            ? _rewardCardRoot.GetComponent<GridLayoutGroup>()
            : null;
        _buttonRoot = _panel?.Find("grpEventButtons")
            as RectTransform;
        BindPreparationNavigationButton();
        if (_panel == null || _titleText == null ||
            _descriptionText == null || _rewardCardRoot == null ||
            _rewardCardLayout == null || _buttonRoot == null ||
            _preparationNavigationButton == null ||
            _preparationNavigationText == null)
        {
            Debug.LogError(
                "Dungeon event fixed UI is incomplete. Author it in the " +
                "Scene instead of creating it at runtime.",
                _root);
            return;
        }

        CollectAuthoredViews(_rewardCardRoot, _rewardCards);
        CollectAuthoredViews(_rewardCardRoot, _startingItemSlots);
        CollectAuthoredViews(_buttonRoot, _choiceButtons);

        _rewardCardRoot.gameObject.SetActive(false);
        _buttonRoot.gameObject.SetActive(false);
    }

    private void BindPreparationNavigationButton()
    {
        _preparationNavigationButton ??= _root.transform
            .Find("btnPreparationReturnToStage")?.GetComponent<Button>();
        if (_preparationNavigationButton == null)
            return;

        _preparationNavigationButton.onClick.RemoveAllListeners();
        _preparationNavigationButton.onClick.AddListener(
            () => _page?.ReturnToStageSelect());
        _preparationNavigationText = _preparationNavigationButton
            .GetComponentInChildren<TextMeshProUGUI>(true);
        _preparationNavigationButton.gameObject.SetActive(false);
    }

    private void RefreshPreparationNavigationButton()
    {
        if (_preparationNavigationButton == null)
            return;

        bool visible = _viewMode == EViewMode.StartingSelection ||
                       _viewMode == EViewMode.StartingItemSelection;
        if (_preparationNavigationText != null)
        {
            _preparationNavigationText.text = LocalizationService.Get(
                LocalizationKeys.UiDungeonReturnToStage);
        }
        _preparationNavigationButton.gameObject.SetActive(visible);
    }

    private void SetRewardCardMode(bool showRewardCards)
    {
        if (_rewardCardLayout != null)
            _rewardCardLayout.constraintCount = RewardChoiceCount;
        if (_rewardCardRoot != null)
            _rewardCardRoot.gameObject.SetActive(showRewardCards);
        if (_buttonRoot != null)
        {
            _buttonRoot.gameObject.SetActive(!showRewardCards);
            LayoutElement layout =
                _buttonRoot.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredHeight = 420f;
                layout.flexibleHeight = 1f;
            }
        }
    }

    private void SetStartingItemMode()
    {
        if (_rewardCardLayout != null)
        {
            _rewardCardLayout.constraintCount = Mathf.Max(
                1,
                _page?.StartingItemChoices?.Count ?? RewardChoiceCount);
        }
        if (_rewardCardRoot != null)
            _rewardCardRoot.gameObject.SetActive(true);
        if (_buttonRoot != null)
        {
            _buttonRoot.gameObject.SetActive(true);
            LayoutElement layout =
                _buttonRoot.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredHeight = 64f;
                layout.flexibleHeight = 0f;
            }
        }
    }

    private void RefreshRuntimeLayout()
    {
        _panel?.GetComponent<ResponsivePanelFitter>()?.RefreshLayout();
        _rewardCardRoot?.GetComponent<ResponsiveGridConstraint>()
            ?.RefreshLayout();
    }

    private void CreateStartingItemSlot(int slotIndex, BattleItemSO item)
    {
        if (_rewardCardRoot == null || item == null)
            return;

        DungeonStartingItemSlotView slotPrefab =
            _page.StartingItemSlotPrefab;
        if (slotPrefab == null)
        {
            Debug.LogError(
                "Starting item slot prefab is not assigned on DungeonPage.");
            return;
        }

        DungeonStartingItemSlotView slot = AcquireView(
            _startingItemSlots,
            slotPrefab,
            _rewardCardRoot);
        slot.name = $"grpStartingItem{slotIndex + 1}";
        if (!slot.BindItem(item))
        {
            slot.gameObject.SetActive(false);
            return;
        }

        int remaining = _page.GetStartingItemRerollsRemaining(slotIndex);
        bool canReroll = _page.CanRerollStartingItem(slotIndex);
        string label = remaining > 0
            ? LocalizationService.Get(
                LocalizationKeys.UiDungeonStartingItemsReroll,
                LocalizationService.Arg("count", remaining))
            : LocalizationService.Get(
                LocalizationKeys.UiDungeonStartingItemsRerollUsed);
        slot.BindReroll(
            label,
            canReroll,
            () => _page.TryRerollStartingItem(slotIndex));
    }

    private RectTransform CreateRewardCard(
        RewardCardContent content,
        Action action)
    {
        if (_rewardCardRoot == null)
            return null;

        DungeonRewardCardView prefab = _page.RewardCardPrefab;
        if (prefab == null)
        {
            Debug.LogError(
                "Dungeon reward card prefab is not assigned on DungeonPage.");
            return null;
        }

        DungeonRewardCardView card = AcquireView(
            _rewardCards,
            prefab,
            _rewardCardRoot);
        card.name = "btnRewardCard";
        card.Bind(
            content.Category,
            content.Title,
            content.Description,
            content.Footer,
            content.AccentColor,
            action);
        return card.transform as RectTransform;
    }

    private void CreateButton(string label, Action action)
    {
        DungeonDynamicChoiceButtonView prefab =
            _page.ChoiceButtonPrefab;
        if (prefab == null)
        {
            Debug.LogError(
                "Dungeon choice button prefab is not assigned on " +
                "DungeonPage.");
            return;
        }

        DungeonDynamicChoiceButtonView button = AcquireView(
            _choiceButtons,
            prefab,
            _buttonRoot);
        button.name = "btnEventChoice";
        button.Bind(label, true, action);
    }

    private void ClearButtons()
    {
        _firstStartingChoiceRect = null;
        DeactivateViews(_rewardCards);
        DeactivateViews(_startingItemSlots);
        DeactivateViews(_choiceButtons);
        RefreshPreparationNavigationButton();
    }

    private static void CollectAuthoredViews<T>(
        Transform root,
        List<T> views)
        where T : Component
    {
        if (root == null)
            return;

        for (int index = 0; index < root.childCount; index++)
        {
            T view = root.GetChild(index).GetComponent<T>();
            if (view == null || views.Contains(view))
                continue;

            view.gameObject.SetActive(false);
            views.Add(view);
        }
    }

    private static T AcquireView<T>(
        List<T> views,
        T prefab,
        Transform parent)
        where T : Component
    {
        for (int index = 0; index < views.Count; index++)
        {
            T view = views[index];
            if (view == null || view.gameObject.activeSelf)
                continue;

            view.gameObject.SetActive(true);
            return view;
        }

        T instance = UnityEngine.Object.Instantiate(
            prefab,
            parent,
            false);
        views.Add(instance);
        return instance;
    }

    private static void DeactivateViews<T>(List<T> views)
        where T : Component
    {
        for (int index = 0; index < views.Count; index++)
        {
            if (views[index] != null)
                views[index].gameObject.SetActive(false);
        }
    }
}
