using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DungeonRoomView
{
    private GameObject _root;
    private DungeonPage _page;
    private EDungeonPhase _phase;
    private RectTransform _panel;
    private Image _banner;
    private AspectRatioFitter _bannerAspect;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _description;
    private TextMeshProUGUI _currency;
    private RectTransform _buttonRoot;
    private RectTransform _restCharacterSdRoot;
    private DungeonRoomSO _room;
    private int _roomIndex;
    private bool _localizationEventsBound;
    private readonly List<DungeonDynamicChoiceButtonView> _choiceButtons =
        new();
    private readonly List<Image> _restCharacterSdViews = new();
    private readonly List<BattleItemSO> _usableRestItems = new();
    private readonly List<DungeonRestActionDefinition>
        _availableRestActions = new();
    private readonly List<DungeonEventChoiceNodeDefinition>
        _activeEventChoices = new();
    private bool _restCharacterSdPrefabErrorLogged;
    private DungeonRestActionDefinition _pendingRestAction;
    private int _pendingRestActionIndex = -1;
    private BattleItemSO _pendingRestItem;
    private CharacterRuntime _selectedRestCharacter;

    public void Initialize(
        GameObject root,
        DungeonPage page,
        EDungeonPhase phase)
    {
        if (root == null || page == null)
            return;
        if (_panel != null)
            return;

        _root = root;
        _page = page;
        _phase = phase;
        BuildRuntimeUi();
        BindLocalizationEvents();
        Hide();
    }

    public void Show(DungeonRoomSO room, int roomIndex)
    {
        if (_panel == null)
            return;

        _room = room;
        _roomIndex = Mathf.Max(0, roomIndex);
        ResetRestSelection();
        _panel.gameObject.SetActive(true);
        Render();
    }

    public void Hide()
    {
        if (_panel != null)
            _panel.gameObject.SetActive(false);
    }

    public void Teardown()
    {
        UnbindLocalizationEvents();
        _root = null;
        _page = null;
        _panel = null;
        _banner = null;
        _bannerAspect = null;
        _title = null;
        _description = null;
        _currency = null;
        _buttonRoot = null;
        _restCharacterSdRoot = null;
        _room = null;
        _activeEventChoices.Clear();
        _choiceButtons.Clear();
        _restCharacterSdViews.Clear();
        _usableRestItems.Clear();
        _availableRestActions.Clear();
        ResetRestSelection();
        _restCharacterSdPrefabErrorLogged = false;
    }

    private void Render()
    {
        if (_panel == null || _page == null)
            return;

        ClearButtons();
        _banner.sprite = _room != null ? _room.Banner : null;
        if (_bannerAspect != null)
        {
            _bannerAspect.aspectRatio = _banner.sprite != null &&
                                        _banner.sprite.rect.height > 0f
                ? _banner.sprite.rect.width / _banner.sprite.rect.height
                : 16f / 9f;
        }
        _banner.color = _banner.sprite != null
            ? Color.white
            : GetFallbackBannerColor();
        _title.text = _room != null
            ? _room.DisplayName
            : GetFallbackTitle();
        _description.text = _room != null &&
                            !string.IsNullOrWhiteSpace(_room.Description)
            ? _room.Description
            : GetFallbackDescription();
        _currency.gameObject.SetActive(_phase == EDungeonPhase.Shop);
        _currency.text = $"런 재화  {_page.RunSession.RunCurrency}";
        RenderRestCharacterSds();

        if (_phase == EDungeonPhase.Event &&
            _room is DungeonEventSO dungeonEvent &&
            dungeonEvent.Choices.Count > 0 &&
            dungeonEvent.UsesChoiceGraph)
        {
            string resultDescription =
                _page.GetDungeonEventResultDescription(
                    dungeonEvent,
                    _roomIndex);
            if (!string.IsNullOrWhiteSpace(resultDescription))
                _description.text = resultDescription;
            RenderEventChoices(dungeonEvent);
        }
        else if (_phase == EDungeonPhase.Event &&
                 _room is DungeonEventSO legacyEvent &&
                 legacyEvent.Choices.Count > 0)
        {
            RenderChoices(legacyEvent.Choices, false);
        }
        else if (_phase == EDungeonPhase.Rest &&
                 _room is DungeonRestSO dungeonRest &&
                 dungeonRest.Actions.Count > 0)
        {
            RenderRestActions(dungeonRest);
        }
        else if (_phase == EDungeonPhase.Shop && _room is DungeonShopSO dungeonShop &&
                 dungeonShop.Products.Count > 0)
        {
            RenderChoices(dungeonShop.Products, true);
        }
        else
        {
            RenderFallbackChoices();
        }
    }

    private void RenderEventChoices(DungeonEventSO dungeonEvent)
    {
        _page.GetActiveDungeonEventChoices(
            dungeonEvent,
            _roomIndex,
            _activeEventChoices);
        if (_activeEventChoices.Count == 0)
        {
            Debug.LogError(
                $"Dungeon event '{dungeonEvent.EventId}' has no active " +
                "choice nodes.");
            RenderFallbackChoices();
            return;
        }

        for (int index = 0; index < _activeEventChoices.Count; index++)
        {
            DungeonEventChoiceNodeDefinition node =
                _activeEventChoices[index];
            int choiceIndex = dungeonEvent.FindChoiceIndex(node.NodeId);
            bool interactable = choiceIndex >= 0 &&
                                _page.CanUseDungeonRoomChoice(
                                    EDungeonPhase.Event,
                                    _roomIndex,
                                    choiceIndex,
                                    node);
            CreateButton(GetChoiceLabel(node, false), interactable, () =>
            {
                if (!_page.TryUseDungeonEventChoice(
                        dungeonEvent,
                        _roomIndex,
                        node))
                {
                    return;
                }

                if (_page.CurrentPhase == EDungeonPhase.Event)
                    Render();
            });
        }
    }

    private void RenderChoices(
        IReadOnlyList<DungeonRoomChoiceDefinition> choices,
        bool shop)
    {
        for (int index = 0; index < choices.Count; index++)
        {
            int choiceIndex = index;
            DungeonRoomChoiceDefinition choice = choices[index];
            bool sold = shop && choice != null && choice.SinglePurchase &&
                        _page.IsShopProductSold(_roomIndex, choiceIndex);
            bool interactable = !sold && _page.CanUseDungeonRoomChoice(
                _phase,
                _roomIndex,
                choiceIndex,
                choice);
            string label = GetChoiceLabel(choice, sold);
            CreateButton(label, interactable, () =>
            {
                if (!_page.TryUseDungeonRoomChoice(
                        _phase,
                        _roomIndex,
                        choiceIndex,
                        choice))
                {
                    return;
                }

                if (shop)
                    Render();
            });
        }

        if (shop)
            CreateLeaveShopButton();
    }

    private void RenderRestActions(DungeonRestSO rest)
    {
        int remaining = _page.GetRemainingRestActionCount(
            rest,
            _roomIndex);
        int maximum = _page.GetMaximumRestActionCount(
            rest,
            _roomIndex);
        string prompt = $"남은 행동 {remaining}/{maximum}";
        if (_pendingRestItem != null)
        {
            prompt += "\n아이템을 사용할 대원을 선택하세요.";
        }
        else if (_pendingRestAction != null &&
                 _pendingRestAction.RequiresTarget)
        {
            prompt += "\n행동을 적용할 대원을 선택하세요.";
        }
        else if (_pendingRestAction?.ActionType ==
                 EDungeonRestActionType.UseRestItem)
        {
            prompt += "\n사용할 아이템을 선택하세요.";
        }
        else if (_selectedRestCharacter != null)
        {
            prompt += $"\n{GetRestCharacterName(_selectedRestCharacter)} 선택됨";
        }
        _description.text = string.IsNullOrWhiteSpace(_description.text)
            ? prompt
            : $"{_description.text}\n\n{prompt}";

        _page.GetAvailableRestActions(
            rest,
            _roomIndex,
            _availableRestActions);
        for (int index = 0; index < _availableRestActions.Count; index++)
        {
            int actionIndex = index;
            DungeonRestActionDefinition action =
                _availableRestActions[index];
            bool interactable = _page.CanUseDungeonRestAction(
                rest,
                _roomIndex,
                actionIndex,
                action);
            string label = GetChoiceLabel(action?.Choice, false);
            if (ReferenceEquals(_pendingRestAction, action))
                label = $"> {label}";
            CreateButton(label, interactable, () =>
            {
                HandleRestActionClicked(rest, actionIndex, action);
            });
        }

        if (_pendingRestAction?.ActionType ==
            EDungeonRestActionType.UseRestItem)
        {
            RenderRestItems(rest);
        }

        CharacterRestSkillDefinition skill =
            _selectedRestCharacter?.Definition?.RestSkill;
        if (skill != null && skill.IsUsable)
        {
            bool interactable = _page.CanUseCharacterRestSkill(
                rest,
                _roomIndex,
                _selectedRestCharacter);
            string label = $"{skill.Title}\n{skill.Description}";
            CreateButton(label, interactable, () =>
            {
                if (!_page.TryUseCharacterRestSkill(
                        rest,
                        _roomIndex,
                        _selectedRestCharacter))
                {
                    return;
                }

                HandleCompletedRestAction();
            });
        }
    }

    private void HandleRestActionClicked(
        DungeonRestSO rest,
        int actionIndex,
        DungeonRestActionDefinition action)
    {
        if (action == null)
            return;

        if (action.ActionType == EDungeonRestActionType.LegacyImmediate)
        {
            if (_page.TryUseDungeonRestAction(
                    rest,
                    _roomIndex,
                    actionIndex,
                    action,
                    null))
            {
                HandleCompletedRestAction();
            }
            return;
        }

        _pendingRestAction = action;
        _pendingRestActionIndex = actionIndex;
        _pendingRestItem = null;
        Render();
    }

    private void RenderRestItems(DungeonRestSO rest)
    {
        _page.GetUsableRestItems(_usableRestItems);
        for (int index = 0; index < _usableRestItems.Count; index++)
        {
            BattleItemSO item = _usableRestItems[index];
            string label = $"{item.GetLocalizedDisplayName()}" +
                           $"  x{_page.GetBattleItemCount(item)}\n" +
                           item.GetLocalizedDescription();
            if (ReferenceEquals(_pendingRestItem, item))
                label = $"> {label}";
            CreateButton(label, true, () =>
            {
                _pendingRestItem = item;
                Render();
            });
        }
    }

    private void HandleRestCharacterClicked(CharacterRuntime character)
    {
        if (_room is not DungeonRestSO rest || character == null)
            return;

        if (_pendingRestItem != null)
        {
            if (_page.TryUseRestItem(
                    rest,
                    _roomIndex,
                    _pendingRestActionIndex,
                    _pendingRestAction,
                    _pendingRestItem,
                    character))
            {
                HandleCompletedRestAction();
            }
            return;
        }

        if (_pendingRestAction != null &&
            _pendingRestAction.RequiresTarget)
        {
            if (_page.TryUseDungeonRestAction(
                    rest,
                    _roomIndex,
                    _pendingRestActionIndex,
                    _pendingRestAction,
                    character))
            {
                HandleCompletedRestAction();
            }
            return;
        }

        _selectedRestCharacter = character;
        Render();
    }

    private void HandleCompletedRestAction()
    {
        ResetRestSelection();
        if (_page.CurrentPhase == EDungeonPhase.Rest)
            Render();
    }

    private void ResetRestSelection()
    {
        _pendingRestAction = null;
        _pendingRestActionIndex = -1;
        _pendingRestItem = null;
        _selectedRestCharacter = null;
    }

    private static string GetRestCharacterName(CharacterRuntime character)
    {
        return character?.Data?.CharacterName ??
               character?.Definition?.CharacterName ??
               "CHARACTER";
    }

    private void RenderFallbackChoices()
    {
        Debug.LogError(
            $"Dungeon {_phase} room {_roomIndex} has no configured " +
            "DungeonRoomSO.");
        CreateButton(
            "ROOM DATA NOT CONFIGURED\nCONTINUE",
            true,
            () => _page.CompleteDungeonRoom());
    }

    private void CreateLeaveShopButton()
    {
        CreateButton(
            "상점을 나간다",
            true,
            () => _page.CompleteDungeonRoom());
    }

    private static string GetChoiceLabel(
        DungeonRoomChoiceDefinition choice,
        bool sold)
    {
        if (choice == null)
            return "INVALID CHOICE";

        string label = choice.Title;
        if (!string.IsNullOrWhiteSpace(choice.Description))
            label += "\n" + choice.Description;
        if (sold)
            return label + "\n판매 완료";
        if (choice.RunCurrencyCost > 0)
            label += $"\n가격 {choice.RunCurrencyCost}";
        return label;
    }

    private string GetFallbackTitle()
    {
        return _phase switch
        {
            EDungeonPhase.Rest => LocalizationService.Get(
                LocalizationKeys.UiDungeonRest),
            EDungeonPhase.Shop => LocalizationService.Get(
                LocalizationKeys.UiCommonShop),
            _ => LocalizationService.Get(LocalizationKeys.UiDungeonEvent),
        };
    }

    private string GetFallbackDescription()
    {
        return "Dungeon room data is not configured.";
    }

    private Color GetFallbackBannerColor()
    {
        return _phase switch
        {
            EDungeonPhase.Rest => new Color(0.16f, 0.3f, 0.24f, 1f),
            EDungeonPhase.Shop => new Color(0.31f, 0.23f, 0.12f, 1f),
            _ => new Color(0.16f, 0.2f, 0.29f, 1f),
        };
    }

    private void RenderRestCharacterSds()
    {
        DeactivateRestCharacterSds();
        if (_phase != EDungeonPhase.Rest ||
            _restCharacterSdRoot == null || _page == null)
        {
            return;
        }

        IReadOnlyList<CharacterRuntime> characters = _page.OwnedTurrets;
        for (int index = 0; index < characters.Count; index++)
        {
            CharacterRuntime character = characters[index];
            Sprite sprite = character != null
                ? character.ResolveRestSdSprite()
                : null;
            if (sprite == null)
                continue;

            Image view = AcquireRestCharacterSdView();
            if (view == null)
                return;

            view.name = $"imgRestCharacterSd_{index + 1}";
            view.sprite = sprite;
            view.color = ReferenceEquals(
                character,
                _selectedRestCharacter)
                ? new Color(1f, 0.9f, 0.55f, 1f)
                : Color.white;
            view.preserveAspect = true;
            view.raycastTarget = true;
            view.enabled = true;
            Button button = view.GetComponent<Button>();
            if (button == null)
            {
                if (!_restCharacterSdPrefabErrorLogged)
                {
                    Debug.LogError(
                        "Dungeon rest character SD prefab requires an " +
                        "authored Button component.",
                        view);
                    _restCharacterSdPrefabErrorLogged = true;
                }
                continue;
            }

            button.targetGraphic = view;
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
                HandleRestCharacterClicked(character));
        }
    }

    private Image AcquireRestCharacterSdView()
    {
        for (int index = 0; index < _restCharacterSdViews.Count; index++)
        {
            Image view = _restCharacterSdViews[index];
            if (view == null || view.gameObject.activeSelf)
                continue;

            view.gameObject.SetActive(true);
            return view;
        }

        Image prefab = _page.RestCharacterSdPrefab;
        if (prefab == null)
        {
            if (!_restCharacterSdPrefabErrorLogged)
            {
                Debug.LogError(
                    "Dungeon rest character SD prefab is not assigned on " +
                    "DungeonPage.");
                _restCharacterSdPrefabErrorLogged = true;
            }
            return null;
        }

        Image instance = UnityEngine.Object.Instantiate(
            prefab,
            _restCharacterSdRoot,
            false);
        _restCharacterSdViews.Add(instance);
        return instance;
    }

    private void DeactivateRestCharacterSds()
    {
        for (int index = 0; index < _restCharacterSdViews.Count; index++)
        {
            Image view = _restCharacterSdViews[index];
            if (view == null)
                continue;

            Button button = view.GetComponent<Button>();
            if (button != null)
                button.onClick.RemoveAllListeners();
            view.sprite = null;
            view.enabled = false;
            view.gameObject.SetActive(false);
        }
    }

    private void BuildRuntimeUi()
    {
        _panel = _root.transform.Find($"grp{_phase}RoomPanel")
            as RectTransform;
        Transform content = _panel?.Find("grpRoomContent");
        _banner = _panel?.Find("imgRoomBanner")?.GetComponent<Image>();
        _bannerAspect = _banner != null
            ? _banner.GetComponent<AspectRatioFitter>()
            : null;
        _title = content?.Find("txtRoomTitle")
            ?.GetComponent<TextMeshProUGUI>();
        _description = content?.Find("txtRoomDescription")
            ?.GetComponent<TextMeshProUGUI>();
        _currency = content?.Find("txtRunCurrency")
            ?.GetComponent<TextMeshProUGUI>();
        _buttonRoot = content?.Find("grpRoomChoices")
            as RectTransform;
        _restCharacterSdRoot = _phase == EDungeonPhase.Rest
            ? _panel?.Find("grpRestCharacterSds") as RectTransform
            : null;
        if (_panel == null || _banner == null || _bannerAspect == null ||
            _title == null || _description == null || _currency == null ||
            _buttonRoot == null ||
            (_phase == EDungeonPhase.Rest &&
             _restCharacterSdRoot == null))
        {
            Debug.LogError(
                $"Dungeon {_phase} fixed room UI is incomplete. " +
                "Author it in the Scene.",
                _root);
        }

        CollectAuthoredButtons();
        CollectAuthoredRestCharacterSds();
    }

    private void CollectAuthoredRestCharacterSds()
    {
        if (_restCharacterSdRoot == null)
            return;

        for (int index = 0; index < _restCharacterSdRoot.childCount;
             index++)
        {
            Image view = _restCharacterSdRoot.GetChild(index)
                .GetComponent<Image>();
            if (view == null || _restCharacterSdViews.Contains(view))
                continue;

            view.gameObject.SetActive(false);
            _restCharacterSdViews.Add(view);
        }
    }

    private void CreateButton(
        string label,
        bool interactable,
        Action action)
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

        DungeonDynamicChoiceButtonView button = AcquireButton(prefab);
        button.name = "btnRoomChoice";
        button.Bind(label, interactable, action);
    }

    private void ClearButtons()
    {
        if (_buttonRoot == null)
            return;
        for (int index = 0; index < _choiceButtons.Count; index++)
        {
            if (_choiceButtons[index] != null)
                _choiceButtons[index].gameObject.SetActive(false);
        }
    }

    private void CollectAuthoredButtons()
    {
        if (_buttonRoot == null)
            return;

        for (int index = 0; index < _buttonRoot.childCount; index++)
        {
            DungeonDynamicChoiceButtonView button =
                _buttonRoot.GetChild(index)
                    .GetComponent<DungeonDynamicChoiceButtonView>();
            if (button == null || _choiceButtons.Contains(button))
                continue;

            button.gameObject.SetActive(false);
            _choiceButtons.Add(button);
        }
    }

    private DungeonDynamicChoiceButtonView AcquireButton(
        DungeonDynamicChoiceButtonView prefab)
    {
        for (int index = 0; index < _choiceButtons.Count; index++)
        {
            DungeonDynamicChoiceButtonView button = _choiceButtons[index];
            if (button == null || button.gameObject.activeSelf)
                continue;

            button.gameObject.SetActive(true);
            return button;
        }

        DungeonDynamicChoiceButtonView instance =
            UnityEngine.Object.Instantiate(prefab, _buttonRoot, false);
        _choiceButtons.Add(instance);
        return instance;
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
        if (_panel != null && _panel.gameObject.activeSelf)
            Render();
    }
}
