using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class ItemEditorWindow : EditorWindow
{
    public const string MenuPath = PS260714EditorMenu.ItemEditor;

    private const string AssetRoot = "Assets/Resources/Items";
    private const string RenameControlName = "ItemAssetRenameField";

    private static readonly string[] CategoryLabels =
    {
        "재화",
        "모집권",
        "강화 재료",
        "소모품",
        "이벤트 재화",
    };

    private readonly List<ItemDefinitionSO> _items = new();
    private ItemDefinitionSO _selected;
    private SerializedObject _serialized;
    private Vector2 _listScroll;
    private Vector2 _detailScroll;
    private string _searchText = string.Empty;
    private bool _showAdvanced;
    private bool _renaming;
    private bool _focusRenameField;
    private string _renameText = string.Empty;

    [MenuItem(MenuPath)]
    public static void Open()
    {
        ItemEditorWindow window = GetWindow<ItemEditorWindow>();
        window.titleContent = new GUIContent("Item Editor");
        window.minSize = new Vector2(760f, 520f);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.projectChanged += OnProjectChanged;
        RefreshAssets(true);
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
    }

    private void OnProjectChanged()
    {
        RefreshAssets(true);
    }

    private void OnGUI()
    {
        DrawToolbar();
        if (_renaming)
            DrawRenameRow();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawItemList();
            DrawDetails();
        }
    }

    private void DrawToolbar()
    {
        PS260714AssetEditorToolbar.Draw(
            $"Items: {_items.Count}",
            _selected != null,
            ShowCreateMenu,
            SaveSelected,
            DuplicateSelected,
            BeginRename,
            DeleteSelected,
            PingSelected,
            () => RefreshAssets(true));
    }

    private void DrawRenameRow()
    {
        using (new EditorGUILayout.HorizontalScope(
                   EditorStyles.helpBox))
        {
            GUILayout.Label("에셋 이름", GUILayout.Width(64f));
            GUI.SetNextControlName(RenameControlName);
            _renameText = EditorGUILayout.TextField(_renameText);

            if (_focusRenameField)
            {
                EditorGUI.FocusTextInControl(RenameControlName);
                _focusRenameField = false;
            }

            if (GUILayout.Button("변경", GUILayout.Width(56f)))
                RenameSelected();
            if (GUILayout.Button("취소", GUILayout.Width(56f)))
                CancelRename();
        }

        Event current = Event.current;
        if (current.type != EventType.KeyDown ||
            GUI.GetNameOfFocusedControl() != RenameControlName)
        {
            return;
        }

        if (current.keyCode == KeyCode.Return ||
            current.keyCode == KeyCode.KeypadEnter)
        {
            RenameSelected();
            current.Use();
        }
        else if (current.keyCode == KeyCode.Escape)
        {
            CancelRename();
            current.Use();
        }
    }

    private void DrawItemList()
    {
        using (new EditorGUILayout.VerticalScope(
                   GUILayout.Width(PS260714AssetEditorList.Width)))
        {
            _searchText =
                PS260714AssetEditorList.DrawSearchField(_searchText);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            for (int index = 0; index < _items.Count; index++)
            {
                ItemDefinitionSO item = _items[index];
                if (item == null || !MatchesSearch(item))
                    continue;

                string displayName = item.GetDisplayName(true);
                string category = GetCategoryLabel(item.Category);
                GUIContent content = new(
                    $"{displayName}\n{category} · {item.ItemId}",
                    GetListIcon(item),
                    item.GetDescription(true));
                if (PS260714AssetEditorList.DrawRow(
                        item == _selected,
                        content))
                {
                    SelectItem(item);
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawDetails()
    {
        using (new EditorGUILayout.VerticalScope(
                   GUILayout.ExpandWidth(true)))
        {
            if (_selected == null || _serialized == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    "왼쪽 목록에서 아이템을 선택하거나 New로 생성하세요.",
                    EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            _detailScroll =
                EditorGUILayout.BeginScrollView(_detailScroll);
            _serialized.UpdateIfRequiredOrScript();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                _selected.GetDisplayName(true),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                AssetDatabase.GetAssetPath(_selected),
                EditorStyles.miniLabel);
            EditorGUILayout.Space(8f);

            EditorGUI.BeginChangeCheck();
            DrawPrimaryProperties();
            DrawValidationMessages();
            DrawAdvancedProperties();

            if (EditorGUI.EndChangeCheck())
            {
                NormalizeQuantities();
                _serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(_selected);
                Repaint();
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawPrimaryProperties()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "기본 정보",
                EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawIconPreview();
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawProperty("icon", "아이콘");
                    DrawProperty("koreanName", "이름");
                    DrawCategoryProperty();
                }
            }

            EditorGUILayout.Space(4f);
            DrawProperty("koreanDescription", "설명");
        }

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "수량",
                EditorStyles.boldLabel);
            DrawProperty(
                "maximumStack",
                "최대 수량 (0 = 무제한)");
            DrawProperty(
                "initialAmount",
                "계정 초기 지급 수량");

            SerializedProperty maximum =
                Find("maximumStack");
            SerializedProperty initial =
                Find("initialAmount");
            if (maximum != null &&
                initial != null &&
                maximum.longValue > 0L &&
                initial.longValue > maximum.longValue)
            {
                EditorGUILayout.HelpBox(
                    "초기 지급 수량은 최대 수량에 맞춰 자동으로 줄어듭니다.",
                    MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "초기 지급 수량은 인벤토리 저장 데이터가 없는 새 계정에만 한 번 적용됩니다.",
                MessageType.Info);
        }
    }

    private void DrawIconPreview()
    {
        Rect previewRect = GUILayoutUtility.GetRect(
            96f,
            96f,
            GUILayout.Width(96f),
            GUILayout.Height(96f));
        EditorGUI.DrawRect(
            previewRect,
            new Color(0.12f, 0.13f, 0.15f, 1f));

        Sprite icon = Find("icon")?.objectReferenceValue as Sprite;
        if (icon == null)
        {
            GUI.Label(
                previewRect,
                "ICON",
                EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Texture preview =
            AssetPreview.GetAssetPreview(icon) ??
            AssetPreview.GetMiniThumbnail(icon);
        if (preview != null)
        {
            GUI.DrawTexture(
                previewRect,
                preview,
                ScaleMode.ScaleToFit,
                true);
        }
    }

    private void DrawCategoryProperty()
    {
        SerializedProperty category = Find("category");
        if (category == null)
            return;

        int current = Mathf.Clamp(
            category.enumValueIndex,
            0,
            CategoryLabels.Length - 1);
        category.enumValueIndex = EditorGUILayout.Popup(
            "종류",
            current,
            CategoryLabels);
    }

    private void DrawAdvancedProperties()
    {
        EditorGUILayout.Space(6f);
        _showAdvanced = EditorGUILayout.Foldout(
            _showAdvanced,
            "고급 설정",
            true);
        if (!_showAdvanced)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    "에셋 타입",
                    _selected.GetType().Name);
            }

            DrawProperty("itemId", "아이템 ID");
            DrawProperty("englishName", "영문 이름");
            DrawProperty("englishDescription", "영문 설명");
            DrawProperty("rarity", "희귀도");
            DrawProperty("sortOrder", "정렬 순서");
            DrawProperty("hiddenInStorage", "창고에서 숨김");

            if (_selected is CurrencyItemSO)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(
                    "재화 설정",
                    EditorStyles.boldLabel);
                DrawProperty("currencyKind", "재화 구분");
                DrawProperty(
                    "purchasedWithRealMoney",
                    "현금 구매 재화");
            }
            else if (_selected is RecruitTicketItemSO)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(
                    "모집권 설정",
                    EditorStyles.boldLabel);
                DrawProperty("bannerGroupId", "배너 그룹 ID");
                DrawProperty("recruitsPerItem", "아이템당 모집 횟수");
            }
            else if (_selected is UpgradeMaterialItemSO)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(
                    "강화 재료 설정",
                    EditorStyles.boldLabel);
                DrawProperty("target", "강화 대상");
                DrawProperty("grade", "재료 등급");
                DrawProperty("upgradeValue", "강화 수치");
            }
        }

        if (_selected is BattleItemSO)
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "Battle Item Settings",
                    EditorStyles.boldLabel);
                DrawProperty("targetType", "Target");
                DrawProperty("usePolicy", "Use Policy");
                SerializedProperty usePolicy = Find("usePolicy");
                if (usePolicy != null &&
                    usePolicy.enumValueIndex ==
                    (int)BattleItemUsePolicy.LimitedUse)
                {
                    DrawProperty("limitedUses", "Uses Per Acquisition");
                }
                if (usePolicy == null ||
                    usePolicy.enumValueIndex !=
                    (int)BattleItemUsePolicy.UnlimitedUse)
                {
                    DrawProperty("maximumRunUses", "Maximum Run Uses");
                }
                DrawProperty("energyCost", "Energy Cost");
                DrawProperty("cooldown", "Cooldown");
                DrawProperty(
                    "availableAsDungeonReward",
                    "Dungeon Reward");
                DrawProperty(
                    "availableAsStartingItem",
                    "Starting Item");
                DrawProperty("effects", "Effects");
            }
        }
    }

    private void DrawValidationMessages()
    {
        SerializedProperty idProperty = Find("itemId");
        string itemId = idProperty?.stringValue?.Trim() ??
                        string.Empty;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            EditorGUILayout.HelpBox(
                "아이템 ID를 입력해야 저장 데이터에서 아이템을 식별할 수 있습니다.",
                MessageType.Error);
        }
        else if (HasDuplicateId(itemId))
        {
            EditorGUILayout.HelpBox(
                $"아이템 ID '{itemId}'가 다른 에셋과 중복됩니다.",
                MessageType.Error);
        }

        ItemCategory category = (ItemCategory)Mathf.Clamp(
            Find("category")?.enumValueIndex ?? 0,
            0,
            CategoryLabels.Length - 1);
        if (!IsTypeCompatible(_selected, category))
        {
            EditorGUILayout.HelpBox(
                "현재 종류와 에셋 타입이 다릅니다. 기능별 전용 설정이 필요하면 New 메뉴에서 해당 종류로 새 아이템을 생성하세요.",
                MessageType.Warning);
        }

        if (_selected is BattleItemSO)
        {
            SerializedProperty effects = Find("effects");
            if (effects == null || effects.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "Battle items require at least one effect.",
                    MessageType.Error);
            }
            else if (_selected is BattleItemSO battleItem &&
                     !battleItem.HasCompatibleEffects)
            {
                EditorGUILayout.HelpBox(
                    "One or more effects are incompatible with the selected target.",
                    MessageType.Error);
            }

            SerializedProperty usePolicy = Find("usePolicy");
            SerializedProperty limitedUses = Find("limitedUses");
            SerializedProperty maximumRunUses = Find("maximumRunUses");
            if (usePolicy != null && limitedUses != null &&
                maximumRunUses != null &&
                usePolicy.enumValueIndex ==
                (int)BattleItemUsePolicy.LimitedUse &&
                maximumRunUses.intValue > 0 &&
                maximumRunUses.intValue < limitedUses.intValue)
            {
                EditorGUILayout.HelpBox(
                    "Maximum run uses is lower than the uses granted per acquisition.",
                    MessageType.Warning);
            }
        }
    }

    private void NormalizeQuantities()
    {
        SerializedProperty maximum = Find("maximumStack");
        SerializedProperty initial = Find("initialAmount");
        if (maximum == null || initial == null)
            return;

        maximum.longValue = Math.Max(0L, maximum.longValue);
        initial.longValue = Math.Max(0L, initial.longValue);
        if (maximum.longValue > 0L)
        {
            initial.longValue = Math.Min(
                initial.longValue,
                maximum.longValue);
        }
    }

    private SerializedProperty Find(string propertyName)
    {
        return _serialized?.FindProperty(propertyName);
    }

    private void DrawProperty(string propertyName, string label)
    {
        SerializedProperty property = Find(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private void ShowCreateMenu()
    {
        GenericMenu menu = new();
        menu.AddItem(
            new GUIContent("재화"),
            false,
            () => CreateItem(
                typeof(CurrencyItemSO),
                ItemCategory.Currency,
                "Currency",
                "NewCurrencyItem",
                "새 재화"));
        menu.AddItem(
            new GUIContent("모집권"),
            false,
            () => CreateItem(
                typeof(RecruitTicketItemSO),
                ItemCategory.RecruitTicket,
                "Ticket",
                "NewRecruitTicket",
                "새 모집권"));
        menu.AddItem(
            new GUIContent("강화 재료"),
            false,
            () => CreateItem(
                typeof(UpgradeMaterialItemSO),
                ItemCategory.UpgradeMaterial,
                "Material",
                "NewUpgradeMaterial",
                "새 강화 재료"));
        menu.AddItem(
            new GUIContent("소모품"),
            false,
            () => CreateItem(
                typeof(GeneralItemSO),
                ItemCategory.Consumable,
                "General",
                "NewConsumable",
                "새 소모품"));
        menu.AddItem(
            new GUIContent("이벤트 재화"),
            false,
            () => CreateItem(
                typeof(CurrencyItemSO),
                ItemCategory.EventCurrency,
                "Event",
                "NewEventCurrency",
                "새 이벤트 재화"));
        menu.AddItem(
            new GUIContent("Battle Item"),
            false,
            () => CreateItem(
                typeof(BattleItemSO),
                ItemCategory.Consumable,
                "Battle",
                "NewBattleItem",
                "New Battle Item"));
        menu.ShowAsContext();
    }

    private void CreateItem(
        Type assetType,
        ItemCategory category,
        string folderName,
        string assetName,
        string displayName)
    {
        EnsureAssetFolder(folderName);
        string path = AssetDatabase.GenerateUniqueAssetPath(
            $"{AssetRoot}/{folderName}/{assetName}.asset");
        ItemDefinitionSO item =
            ScriptableObject.CreateInstance(assetType)
                as ItemDefinitionSO;
        if (item == null)
        {
            EditorUtility.DisplayDialog(
                "Item Editor",
                $"'{assetType.Name}' 아이템을 생성할 수 없습니다.",
                "확인");
            return;
        }

        SerializedObject serialized = new(item);
        serialized.FindProperty("itemId").stringValue =
            CreateUniqueItemId(category);
        serialized.FindProperty("category").enumValueIndex =
            (int)category;
        serialized.FindProperty("koreanName").stringValue =
            displayName;
        serialized.FindProperty("maximumStack").longValue = 0L;
        serialized.FindProperty("initialAmount").longValue = 0L;

        if (item is CurrencyItemSO)
        {
            SerializedProperty currencyKind =
                serialized.FindProperty("currencyKind");
            if (currencyKind != null)
            {
                currencyKind.enumValueIndex =
                    category == ItemCategory.EventCurrency
                        ? (int)CurrencyKind.Event
                        : (int)CurrencyKind.Soft;
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(item, path);
        EditorUtility.SetDirty(item);
        AssetDatabase.SaveAssets();
        RefreshCatalog();
        RefreshAssets(false);
        SelectItem(
            AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path));
        Selection.activeObject = _selected;
    }

    private void SaveSelected()
    {
        if (_selected == null || _serialized == null)
            return;

        NormalizeQuantities();
        _serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(_selected);
        AssetDatabase.SaveAssets();
        RefreshCatalog();
        AssetDatabase.SaveAssets();
        ItemDefinitionCatalog.Invalidate();
        RefreshAssets(true);
        ShowNotification(new GUIContent("아이템을 저장했습니다."));
    }

    private void DuplicateSelected()
    {
        if (_selected == null)
            return;

        SaveSelected();
        string sourcePath = AssetDatabase.GetAssetPath(_selected);
        string directory = Path.GetDirectoryName(sourcePath)
            ?.Replace('\\', '/') ?? AssetRoot;
        string destination = AssetDatabase.GenerateUniqueAssetPath(
            $"{directory}/{_selected.name} Copy.asset");
        if (!AssetDatabase.CopyAsset(sourcePath, destination))
        {
            EditorUtility.DisplayDialog(
                "Item Editor",
                "아이템 복제에 실패했습니다.",
                "확인");
            return;
        }

        ItemDefinitionSO duplicate =
            AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(
                destination);
        if (duplicate != null)
        {
            SerializedObject serialized = new(duplicate);
            serialized.FindProperty("itemId").stringValue =
                CreateUniqueItemId(duplicate.Category);
            SerializedProperty koreanName =
                serialized.FindProperty("koreanName");
            if (koreanName != null)
                koreanName.stringValue += " 복사본";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(duplicate);
        }

        AssetDatabase.SaveAssets();
        RefreshCatalog();
        RefreshAssets(false);
        SelectItem(duplicate);
    }

    private void BeginRename()
    {
        if (_selected == null)
            return;
        _renaming = true;
        _renameText = _selected.name;
        _focusRenameField = true;
    }

    private void CancelRename()
    {
        _renaming = false;
        _focusRenameField = false;
        _renameText = string.Empty;
        Repaint();
    }

    private void RenameSelected()
    {
        if (_selected == null)
        {
            CancelRename();
            return;
        }

        string requested = _renameText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(requested))
        {
            EditorUtility.DisplayDialog(
                "Item Editor",
                "에셋 이름을 입력하세요.",
                "확인");
            _focusRenameField = true;
            return;
        }

        string path = AssetDatabase.GetAssetPath(_selected);
        string error = AssetDatabase.RenameAsset(path, requested);
        if (!string.IsNullOrWhiteSpace(error))
        {
            EditorUtility.DisplayDialog(
                "Item Editor",
                error,
                "확인");
            _focusRenameField = true;
            return;
        }

        AssetDatabase.SaveAssets();
        CancelRename();
        RefreshAssets(true);
    }

    private void DeleteSelected()
    {
        if (_selected == null)
            return;

        string path = AssetDatabase.GetAssetPath(_selected);
        if (!EditorUtility.DisplayDialog(
                "아이템 삭제",
                $"'{_selected.GetDisplayName(true)}' 아이템을 삭제하시겠습니까?\n\n{path}",
                "삭제",
                "취소"))
        {
            return;
        }

        CancelRename();
        _selected = null;
        _serialized = null;
        if (!AssetDatabase.DeleteAsset(path))
        {
            EditorUtility.DisplayDialog(
                "Item Editor",
                "아이템 삭제에 실패했습니다.",
                "확인");
            RefreshAssets(false);
            return;
        }

        RefreshCatalog();
        AssetDatabase.SaveAssets();
        ItemDefinitionCatalog.Invalidate();
        RefreshAssets(false);
    }

    private void PingSelected()
    {
        if (_selected == null)
            return;
        Selection.activeObject = _selected;
        EditorGUIUtility.PingObject(_selected);
    }

    private void RefreshCatalog()
    {
        ItemAssetBootstrap.RefreshCatalog();
        ItemDefinitionCatalog.Invalidate();
    }

    private void RefreshAssets(bool preserveSelection)
    {
        string selectedPath = preserveSelection && _selected != null
            ? AssetDatabase.GetAssetPath(_selected)
            : string.Empty;

        _items.Clear();
        string[] guids = AssetDatabase.FindAssets(
            string.Empty,
            new[] { AssetRoot });
        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            ItemDefinitionSO item =
                AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
            if (item != null)
                _items.Add(item);
        }

        _items.Sort(CompareItems);

        ItemDefinitionSO next = null;
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            next = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(
                selectedPath);
        }
        next ??= _items.Count > 0 ? _items[0] : null;
        SelectItem(next);
        Repaint();
    }

    private void SelectItem(ItemDefinitionSO item)
    {
        if (_selected == item && _serialized != null)
            return;

        _selected = item;
        _serialized = item != null
            ? new SerializedObject(item)
            : null;
        _detailScroll = Vector2.zero;
        CancelRename();
    }

    private bool MatchesSearch(ItemDefinitionSO item)
    {
        string query = _searchText?.Trim() ?? string.Empty;
        if (query.Length == 0)
            return true;

        return Contains(item.name, query) ||
               Contains(item.ItemId, query) ||
               Contains(item.GetDisplayName(true), query) ||
               Contains(item.GetDisplayName(false), query) ||
               Contains(GetCategoryLabel(item.Category), query);
    }

    private bool HasDuplicateId(string itemId)
    {
        for (int index = 0; index < _items.Count; index++)
        {
            ItemDefinitionSO item = _items[index];
            if (item != null &&
                item != _selected &&
                string.Equals(
                    item.ItemId,
                    itemId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private string CreateUniqueItemId(ItemCategory category)
    {
        string prefix = category switch
        {
            ItemCategory.Currency => "currency",
            ItemCategory.RecruitTicket => "ticket.recruit",
            ItemCategory.UpgradeMaterial => "material.upgrade",
            ItemCategory.EventCurrency => "currency.event",
            _ => "item.consumable",
        };

        string candidate;
        do
        {
            candidate =
                $"{prefix}.{Guid.NewGuid():N}";
        }
        while (HasAnyItemId(candidate));

        return candidate;
    }

    private bool HasAnyItemId(string itemId)
    {
        for (int index = 0; index < _items.Count; index++)
        {
            ItemDefinitionSO item = _items[index];
            if (item != null &&
                string.Equals(
                    item.ItemId,
                    itemId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsTypeCompatible(
        ItemDefinitionSO item,
        ItemCategory category)
    {
        return item switch
        {
            CurrencyItemSO =>
                category == ItemCategory.Currency ||
                category == ItemCategory.EventCurrency,
            RecruitTicketItemSO =>
                category == ItemCategory.RecruitTicket,
            UpgradeMaterialItemSO =>
                category == ItemCategory.UpgradeMaterial,
            BattleItemSO =>
                category == ItemCategory.Consumable,
            GeneralItemSO =>
                category == ItemCategory.Consumable,
            _ => true,
        };
    }

    private static int CompareItems(
        ItemDefinitionSO left,
        ItemDefinitionSO right)
    {
        int order = left.SortOrder.CompareTo(right.SortOrder);
        if (order != 0)
            return order;
        return string.Compare(
            left.GetDisplayName(true),
            right.GetDisplayName(true),
            StringComparison.Ordinal);
    }

    private static Texture GetListIcon(ItemDefinitionSO item)
    {
        if (item.Icon != null)
            return AssetPreview.GetMiniThumbnail(item.Icon);
        return AssetPreview.GetMiniTypeThumbnail(item.GetType());
    }

    private static string GetCategoryLabel(ItemCategory category)
    {
        int index = Mathf.Clamp(
            (int)category,
            0,
            CategoryLabels.Length - 1);
        return CategoryLabels[index];
    }

    private static bool Contains(string value, string query)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf(
                   query,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void EnsureAssetFolder(string childFolder)
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "Items");
        EnsureFolder(AssetRoot, childFolder);
    }

    private static void EnsureFolder(
        string parent,
        string folderName)
    {
        string path = $"{parent}/{folderName}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folderName);
    }
}
