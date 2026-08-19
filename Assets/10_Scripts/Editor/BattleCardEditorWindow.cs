using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BattleCardEditorWindow : EditorWindow
{
    public const string MenuPath = PS260714EditorMenu.BattleCardEditor;

    private const string AssetFolder = "Assets/06_Runtime/Resources/Cards";
    private const string RenameControlName = "BattleCardAssetRenameField";
    private static readonly string[] RarityFilterLabels =
    {
        "All Rarities",
        "Common",
        "Uncommon",
        "Rare",
        "Epic",
        "Legendary",
    };
    private static readonly string[] AffiliationFilterLabels =
    {
        "All Affiliations",
        "Neutral",
        "Character-bound",
    };

    private readonly List<BattleCardSO> cards = new();
    private BattleCardSO selected;
    private SerializedObject serialized;
    private Vector2 listScroll;
    private Vector2 detailScroll;
    private string search = string.Empty;
    private int rarityFilter;
    private int affiliationFilter;
    private string renameText = string.Empty;
    private bool renaming;
    private bool focusRename;
    private bool _clearEditingFocusRequested;

    [MenuItem(
        MenuPath,
        false,
        PS260714EditorMenu.BattleCardEditorPriority)]
    public static void OpenFromMenu()
    {
        Open(Selection.activeObject as BattleCardSO);
    }

    public static void Open(BattleCardSO card)
    {
        BattleCardEditorWindow window = GetWindow<BattleCardEditorWindow>();
        window.titleContent = new GUIContent("Battle Card Editor");
        window.minSize = new Vector2(860f, 600f);
        window.RefreshCards(card);
        window.Focus();
    }

    [MenuItem(
        MenuPath,
        true,
        PS260714EditorMenu.BattleCardEditorPriority)]
    private static bool ValidateOpenFromMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Battle Card Editor");
        minSize = new Vector2(860f, 600f);
        EditorApplication.projectChanged += HandleProjectChanged;
        Selection.selectionChanged += HandleSelectionChanged;
        RefreshLocalizationKeys();
        RefreshCards(selected);
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= HandleProjectChanged;
        Selection.selectionChanged -= HandleSelectionChanged;
    }

    private void HandleProjectChanged()
    {
        RefreshLocalizationKeys();
        RefreshCards(selected);
    }

    private void HandleSelectionChanged()
    {
        if (Selection.activeObject is BattleCardSO card)
            Select(card);
        Repaint();
    }

    private void OnGUI()
    {
        ApplyPendingEditingFocusClear();
        DrawToolbar();
        if (renaming)
            DrawRenameRow();
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawList();
            DrawDetails();
        }
    }

    private void DrawToolbar()
    {
        PS260714AssetEditorToolbar.Draw(
            $"Cards: {cards.Count}",
            selected != null,
            CreateCard,
            SaveSelected,
            DuplicateSelected,
            BeginRename,
            DeleteSelected,
            () => PS260714AssetEditorList.Ping(selected),
            () =>
            {
                RefreshLocalizationKeys();
                RefreshCards(selected);
            });
    }

    private void DrawRenameRow()
    {
        PS260714AssetRenameCommand command =
            PS260714EditorAssetUtility.DrawRenameRow(
                "SO File Name",
                RenameControlName,
                ref renameText,
                ref focusRename);
        if (command == PS260714AssetRenameCommand.Apply)
            RenameSelected();
        else if (command == PS260714AssetRenameCommand.Cancel)
            CancelRename();
    }

    private void DrawList()
    {
        using (new EditorGUILayout.VerticalScope(
                   GUILayout.Width(PS260714AssetEditorList.Width)))
        {
            search = PS260714AssetEditorList.DrawSearchField(search);
            rarityFilter = EditorGUILayout.Popup(
                "Rarity",
                rarityFilter,
                RarityFilterLabels);
            affiliationFilter = EditorGUILayout.Popup(
                "Affiliation",
                affiliationFilter,
                AffiliationFilterLabels);
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            int visible = 0;
            foreach (BattleCardSO card in cards)
            {
                if (card == null ||
                    !MatchesSearch(card) ||
                    !MatchesFilters(
                        card,
                        rarityFilter,
                        affiliationFilter))
                    continue;
                visible++;
                if (PS260714AssetEditorList.DrawAssetRow(
                        card == selected,
                        card,
                        card.Icon,
                        PS260714EditorAssetDisplayName.Get(card),
                        $"{card.Affiliation} / Cost {card.EnergyCost}",
                        card.CardId))
                {
                    Select(card);
                }
            }
            EditorGUILayout.EndScrollView();
            PS260714AssetEditorList.DrawCountFooter(visible, cards.Count);
        }
    }

    private void DrawDetails()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
        {
            if (selected == null || serialized == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a BattleCardSO or create one with New.",
                    MessageType.Info);
                return;
            }

            serialized.UpdateIfRequiredOrScript();
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            EditorGUILayout.LabelField(
                selected.GetLocalizedDisplayName(),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                AssetDatabase.GetAssetPath(selected),
                EditorStyles.miniLabel);
            EditorGUILayout.Space(6f);

            EditorGUI.BeginChangeCheck();
            DrawValidation();
            DrawIdentityAndPresentation();
            DrawAffiliation();
            DrawSection(
                "Draw & Play Rules",
                "energyCost",
                "minimumMaximumEnergy",
                "recyclePolicy",
                "availableAsStartingCard",
                "availableAsDungeonReward");
            DrawAbility();

            if (EditorGUI.EndChangeCheck())
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(selected);
                BattleCardCatalog.Invalidate();
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawAffiliation()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "Card Affiliation",
                EditorStyles.boldLabel);
            DrawProperty("affiliation", "Type");
            BattleCardAffiliation affiliation =
                (BattleCardAffiliation)(Find("affiliation")?.enumValueIndex ?? 0);
            if (affiliation == BattleCardAffiliation.CharacterExclusive)
            {
                DrawProperty("ownerCharacter", "Owner Character");
                DrawProperty("sourcePolicy", "Source Policy");
            }
            else if (affiliation == BattleCardAffiliation.CharacterDependent)
            {
                DrawProperty("requiredCharacters", "Required Characters", true);
                DrawProperty("requirementMode", "Requirement Match");
                DrawProperty("sourcePolicy", "Source Policy");
                if (Find("sourcePolicy")?.enumValueIndex ==
                    (int)BattleCardSourcePolicy.FixedCharacter)
                {
                    DrawProperty("ownerCharacter", "Fixed Source");
                }
            }
            else
            {
                DrawProperty("sourcePolicy", "Source Policy");
                if (Find("sourcePolicy")?.enumValueIndex ==
                    (int)BattleCardSourcePolicy.FixedCharacter)
                {
                    DrawProperty("ownerCharacter", "Fixed Source");
                }
            }
        }
    }

    private void DrawIdentityAndPresentation()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "Identity & Presentation",
                EditorStyles.boldLabel);
            DrawProperty("cardId", "Card ID");
            DrawProperty("rarity", "Rarity");
            DrawProperty("sortOrder", "Sort Order");

            EditorGUILayout.Space(4f);
            DrawLocalizationControls();
            DrawLocalizationKey(
                "nameLocalizationKey",
                "Name Localization Key");
            DrawProperty("fallbackName", "Name (Fallback)");
            DrawLocalizationKey(
                "descriptionLocalizationKey",
                "Description Localization Key");
            DrawProperty(
                "fallbackDescription",
                "Description (Fallback)");

            EditorGUILayout.Space(4f);
            DrawProperty("icon", "Icon");
            DrawProperty("illustration", "Illustration");
        }
    }

    private void DrawLocalizationControls()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                "Localization",
                EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Refresh Keys", GUILayout.Width(88f)))
                RefreshLocalizationKeys();
        }

        PS260714LocalizationKeyField.DrawLoadError();
    }

    private void DrawLocalizationKey(
        string propertyName,
        string label)
    {
        PS260714LocalizationKeyField.Draw(Find(propertyName), label);
    }

    private void DrawAbility()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Battle Ability", EditorStyles.boldLabel);
            DrawProperty("targetFaction", "Target Faction");
            DrawProperty("subject", "Target Selection");
            DrawProperty("subjectMetric", "Selection Metric");
            BattleAbilityEditorGUI.DrawAreaDefinition(
                Find("areaDefinition"),
                Find("subject"),
                selected);
            BattleAbilityEditorGUI.DrawTargetCount(
                Find("targetCount"),
                Find("areaDefinition"));
            DrawProperty(
                "primaryTargetFilter",
                "Primary Target Filter",
                true);
            DrawSecondaryTarget();
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                "Direct Shared Effects",
                EditorStyles.miniBoldLabel);
            BattleAbilityEditorGUI.DrawEffectList(
                Find("abilityEffects"),
                selected);
            EditorGUILayout.Space(6f);
            DrawOperations();
        }
    }

    private void DrawSecondaryTarget()
    {
        SerializedProperty secondary = Find("secondaryTarget");
        if (secondary == null)
        {
            EditorGUILayout.HelpBox(
                "Secondary target definition was not found.",
                MessageType.Error);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "Secondary Target",
                EditorStyles.miniBoldLabel);
            SerializedProperty enabled =
                secondary.FindPropertyRelative("enabled");
            EditorGUILayout.PropertyField(enabled, new GUIContent("Enabled"));
            if (enabled?.boolValue != true)
                return;

            DrawRelativeProperty(secondary, "worldPoint", "World Point");
            DrawRelativeProperty(
                secondary,
                "targetFaction",
                "Target Faction");
            SerializedProperty subject =
                secondary.FindPropertyRelative("subject");
            EditorGUILayout.PropertyField(
                subject,
                new GUIContent("Target Selection"));
            DrawRelativeProperty(
                secondary,
                "subjectMetric",
                "Selection Metric");
            BattleAbilityEditorGUI.DrawAreaDefinition(
                secondary.FindPropertyRelative("areaDefinition"),
                subject,
                selected);
            BattleAbilityEditorGUI.DrawTargetCount(
                secondary.FindPropertyRelative("targetCount"),
                secondary.FindPropertyRelative("areaDefinition"));
            DrawRelativeProperty(
                secondary,
                "filter",
                "Secondary Target Filter",
                true);
        }
    }

    private void DrawOperations()
    {
        SerializedProperty operations = Find("operations");
        if (operations == null || !operations.isArray)
        {
            EditorGUILayout.HelpBox(
                "Card operation list was not found.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField(
            $"Ordered Operations ({operations.arraySize})",
            EditorStyles.boldLabel);
        int removeIndex = -1;
        int moveFrom = -1;
        int moveTo = -1;
        for (int index = 0; index < operations.arraySize; index++)
        {
            SerializedProperty operation =
                operations.GetArrayElementAtIndex(index);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    SerializedProperty operationIdProperty =
                        operation.FindPropertyRelative("operationId");
                    string operationId = operationIdProperty?.stringValue;
                    operation.isExpanded = EditorGUILayout.Foldout(
                        operation.isExpanded,
                        string.IsNullOrWhiteSpace(operationId)
                            ? $"Operation {index + 1}"
                            : $"{index + 1}. {operationId}",
                        true);
                    using (new EditorGUI.DisabledScope(index == 0))
                    {
                        if (GUILayout.Button("↑", GUILayout.Width(24f)))
                        {
                            moveFrom = index;
                            moveTo = index - 1;
                        }
                    }
                    using (new EditorGUI.DisabledScope(
                               index >= operations.arraySize - 1))
                    {
                        if (GUILayout.Button("↓", GUILayout.Width(24f)))
                        {
                            moveFrom = index;
                            moveTo = index + 1;
                        }
                    }
                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                        removeIndex = index;
                }

                if (operation.isExpanded)
                    DrawOperation(operation);
            }

            if (removeIndex >= 0 || moveFrom >= 0)
                break;
        }

        if (removeIndex >= 0)
        {
            operations.DeleteArrayElementAtIndex(removeIndex);
            GUI.changed = true;
        }
        else if (moveFrom >= 0)
        {
            operations.MoveArrayElement(moveFrom, moveTo);
            GUI.changed = true;
        }

        if (GUILayout.Button("+ Add Operation"))
        {
            int index = operations.arraySize;
            operations.InsertArrayElementAtIndex(index);
            SerializedProperty added = operations.GetArrayElementAtIndex(index);
            SerializedProperty id = added.FindPropertyRelative("operationId");
            if (id != null)
                id.stringValue = string.Empty;
            added.isExpanded = true;
            GUI.changed = true;
        }
    }

    private void DrawOperation(SerializedProperty operation)
    {
        DrawRelativeProperty(operation, "operationId", "Operation ID");
        SerializedProperty type = operation.FindPropertyRelative("type");
        EditorGUILayout.PropertyField(type, new GUIContent("Operation Type"));
        DrawRelativeProperty(operation, "targetScope", "Target Scope");
        DrawRelativeProperty(operation, "condition", "Condition", true);

        BattleCardOperationType operationType = type != null
            ? (BattleCardOperationType)type.enumValueIndex
            : default;
        if (operationType == BattleCardOperationType.SharedEffect ||
            operationType == BattleCardOperationType.CreateZone)
        {
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField(
                "Shared Effect",
                EditorStyles.miniBoldLabel);
            BattleAbilityEditorGUI.DrawEffect(
                operation.FindPropertyRelative("sharedEffect"),
                selected);
        }

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField(
            "References & Filters",
            EditorStyles.miniBoldLabel);
        DrawRelativeProperty(operation, "requiredRole", "Required Role");
        DrawRelativeProperty(
            operation,
            "requiredCharacter",
            "Required Character");
        DrawRelativeProperty(operation, "statusEffect", "Status Effect");
        DrawRelativeProperty(
            operation,
            "requiredStatus",
            "Required Target Status");

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Values", EditorStyles.miniBoldLabel);
        DrawRelativeProperty(operation, "amount", "Amount");
        DrawRelativeProperty(operation, "ratio", "Ratio");
        DrawRelativeProperty(operation, "count", "Count / Uses");
        DrawRelativeProperty(
            operation,
            "minimumSelectionCount",
            "Minimum Selection Count");
        DrawRelativeProperty(
            operation,
            "maximumSelectionCount",
            "Maximum Selection Count");
        DrawRelativeProperty(operation, "duration", "Duration");
        DrawRelativeProperty(
            operation,
            "delaySeconds",
            "Delay Seconds");
        DrawRelativeProperty(operation, "radius", "Radius / Distance");
        DrawRelativeProperty(
            operation,
            "statusDuration",
            "Status Duration");
        DrawRelativeProperty(
            operation,
            "statusStacks",
            "Status Stacks");
        DrawRelativeProperty(
            operation,
            "usePreviousChangedCount",
            "Use Previous Changed Count");
        DrawRelativeProperty(
            operation,
            "oncePerTarget",
            "Once Per Target");

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Modes", EditorStyles.miniBoldLabel);
        DrawRelativeProperty(operation, "movementMode", "Movement Mode");
        DrawRelativeProperty(operation, "zoneTrigger", "Zone Trigger");
        DrawRelativeProperty(
            operation,
            "costModifierMode",
            "Cost Modifier Mode");
        DrawRelativeProperty(operation, "spatialZone", "Spatial Zone");
    }

    private static void DrawRelativeProperty(
        SerializedProperty parent,
        string propertyName,
        string label,
        bool includeChildren = false)
    {
        SerializedProperty property =
            parent?.FindPropertyRelative(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox(
                $"Property '{propertyName}' was not found.",
                MessageType.Error);
            return;
        }
        EditorGUILayout.PropertyField(
            property,
            new GUIContent(label),
            includeChildren);
    }

    private void DrawValidation()
    {
        string id = Find("cardId")?.stringValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            EditorGUILayout.HelpBox("Card ID is required.", MessageType.Error);
        }
        else
        {
            int matches = 0;
            foreach (BattleCardSO card in cards)
            {
                if (card != null && string.Equals(
                        card.CardId,
                        id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    matches++;
                }
            }
            if (matches > 1)
            {
                EditorGUILayout.HelpBox(
                    $"Card ID '{id}' is duplicated.",
                MessageType.Error);
            }
        }

        DrawRequiredLocalizationKeyValidation(
            "nameLocalizationKey",
            "Name");
        DrawRequiredLocalizationKeyValidation(
            "descriptionLocalizationKey",
            "Description");

        BattleCardAffiliation affiliation =
            (BattleCardAffiliation)(Find("affiliation")?.enumValueIndex ?? 0);
        if (affiliation == BattleCardAffiliation.CharacterExclusive &&
            Find("ownerCharacter")?.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "Character-exclusive cards require an owner character.",
                MessageType.Error);
        }
        if (affiliation == BattleCardAffiliation.CharacterDependent &&
            (Find("requiredCharacters")?.arraySize ?? 0) == 0)
        {
            EditorGUILayout.HelpBox(
                "Character-dependent cards require at least one character.",
                MessageType.Error);
        }
        if (Find("sourcePolicy")?.enumValueIndex ==
                (int)BattleCardSourcePolicy.FixedCharacter &&
            Find("ownerCharacter")?.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "Fixed-character source policy requires a source character.",
                MessageType.Error);
        }

        if (selected != null &&
            !BattleCardDefinitionValidator.TryValidate(
                selected,
                out string abilityError))
        {
            EditorGUILayout.HelpBox(
                abilityError,
                MessageType.Error);
        }
    }

    private void DrawRequiredLocalizationKeyValidation(
        string propertyName,
        string label)
    {
        string key = Find(propertyName)?.stringValue?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(key))
            return;

        EditorGUILayout.HelpBox(
            $"{label} Localization Key is required. " +
            "Select a key from strings.csv.",
            MessageType.Error);
    }

    private void DrawSection(string title, params string[] properties)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (string property in properties)
                DrawProperty(property, ObjectNames.NicifyVariableName(property));
        }
    }

    private void DrawProperty(
        string propertyName,
        string label,
        bool includeChildren = false)
    {
        SerializedProperty property = Find(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox(
                $"Property '{propertyName}' was not found.",
                MessageType.Error);
            return;
        }
        EditorGUILayout.PropertyField(
            property,
            new GUIContent(label),
            includeChildren);
    }

    private SerializedProperty Find(string propertyName)
    {
        return serialized?.FindProperty(propertyName);
    }

    private void CreateCard()
    {
        EnsureAssetFolder();
        BattleCardSO card = CreateInstance<BattleCardSO>();
        SerializedObject data = new(card);
        data.FindProperty("cardId").stringValue =
            CreateUniqueCardId("card.new", cards);
        data.FindProperty("fallbackName").stringValue = "New Battle Card";
        SerializedProperty effects = data.FindProperty("abilityEffects");
        BattleAbilityEditorGUI.AddDefaultEffect(effects);
        data.ApplyModifiedPropertiesWithoutUndo();

        string path = AssetDatabase.GenerateUniqueAssetPath(
            $"{AssetFolder}/NewBattleCard.asset");
        AssetDatabase.CreateAsset(card, path);
        AssetDatabase.SaveAssets();
        BattleCardCatalog.Invalidate();
        RefreshCards(card);
        Selection.activeObject = card;
    }

    private void SaveSelected()
    {
        if (selected == null)
            return;
        serialized?.ApplyModifiedProperties();
        PS260714EditorAssetUtility.Save(selected);
        BattleCardCatalog.Invalidate();
        RefreshCards(selected);
        ShowNotification(new GUIContent("Battle card saved."));
    }

    private void DuplicateSelected()
    {
        if (selected == null)
            return;
        SaveSelected();
        if (!PS260714EditorAssetUtility.TryDuplicate(
                selected,
                AssetFolder,
                " Copy",
                out BattleCardSO duplicate,
                out string error))
        {
            EditorUtility.DisplayDialog("Battle Card Editor", error, "OK");
            return;
        }
        SerializedObject duplicateData = new(duplicate);
        duplicateData.FindProperty("cardId").stringValue =
            CreateUniqueCardId($"{selected.CardId}.copy", cards);
        duplicateData.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        BattleCardCatalog.Invalidate();
        RefreshCards(duplicate);
    }

    private void BeginRename()
    {
        if (selected == null)
            return;
        renaming = true;
        renameText = selected.name;
        focusRename = true;
    }

    private void RenameSelected()
    {
        if (selected == null)
        {
            CancelRename();
            return;
        }
        if (!PS260714EditorAssetUtility.TryRename(
                selected,
                renameText,
                out string error))
        {
            if (!string.IsNullOrWhiteSpace(error))
                EditorUtility.DisplayDialog("Battle Card Editor", error, "OK");
            focusRename = true;
            return;
        }
        CancelRename();
        RefreshCards(selected);
    }

    private void CancelRename()
    {
        renaming = false;
        focusRename = false;
        renameText = string.Empty;
    }

    private void DeleteSelected()
    {
        if (selected == null || !PS260714SafeAssetDelete.TryMoveToTrash(
                selected,
                "Battle Card"))
        {
            return;
        }
        selected = null;
        serialized = null;
        BattleCardCatalog.Invalidate();
        RefreshCards(null);
    }

    private void RefreshCards(BattleCardSO preferred)
    {
        string path = PS260714EditorAssetUtility.CapturePath(
            preferred != null ? preferred : selected);
        PS260714EditorAssetUtility.LoadAssets(
            cards,
            "t:BattleCardSO",
            null,
            CompareCards);
        Select(PS260714EditorAssetUtility.RestoreSelection(
            path,
            cards,
            preferred));
        Repaint();
    }

    private void Select(BattleCardSO card)
    {
        if (!ReferenceEquals(selected, card))
            RequestEditingFocusClear();

        selected = card;
        serialized = card != null ? new SerializedObject(card) : null;
        detailScroll = Vector2.zero;
        CancelRename();
    }

    private void RequestEditingFocusClear()
    {
        _clearEditingFocusRequested = true;
        GUIUtility.keyboardControl = 0;
        EditorGUIUtility.editingTextField = false;
        if (Event.current != null)
            ApplyPendingEditingFocusClear();
    }

    private void ApplyPendingEditingFocusClear()
    {
        if (!_clearEditingFocusRequested)
            return;

        GUI.FocusControl(null);
        GUIUtility.keyboardControl = 0;
        EditorGUIUtility.editingTextField = false;
        _clearEditingFocusRequested = false;
    }

    private static void RefreshLocalizationKeys()
    {
        PS260714LocalizationKeyField.Refresh();
    }

    private bool MatchesSearch(BattleCardSO card)
    {
        string query = search?.Trim() ?? string.Empty;
        return query.Length == 0 ||
               Contains(card.name, query) ||
               Contains(card.CardId, query) ||
               Contains(
                   PS260714EditorAssetDisplayName.Get(card),
                   query) ||
               Contains(card.GetLocalizedDisplayName(), query) ||
               Contains(card.Affiliation.ToString(), query);
    }

    internal static bool MatchesFilters(
        BattleCardSO card,
        int rarityFilterIndex,
        int affiliationFilterIndex)
    {
        if (card == null)
            return false;

        bool matchesRarity = rarityFilterIndex <= 0 ||
                             card.Rarity ==
                             (ItemRarity)(rarityFilterIndex - 1);
        bool matchesAffiliation = affiliationFilterIndex switch
        {
            1 => card.Affiliation == BattleCardAffiliation.Neutral,
            2 => card.Affiliation != BattleCardAffiliation.Neutral,
            _ => true,
        };
        return matchesRarity && matchesAffiliation;
    }

    private static bool Contains(string value, string query)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int CompareCards(BattleCardSO left, BattleCardSO right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;
        int order = left.SortOrder.CompareTo(right.SortOrder);
        return order != 0
            ? order
            : string.Compare(
                left.CardId,
                right.CardId,
                StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateUniqueCardId(
        string requested,
        IReadOnlyList<BattleCardSO> existing)
    {
        string root = string.IsNullOrWhiteSpace(requested)
            ? "card.new"
            : requested.Trim();
        string candidate = root;
        int suffix = 2;
        while (ContainsCardId(existing, candidate))
            candidate = $"{root}.{suffix++}";
        return candidate;
    }

    private static bool ContainsCardId(
        IReadOnlyList<BattleCardSO> existing,
        string candidate)
    {
        if (existing == null)
            return false;
        foreach (BattleCardSO card in existing)
        {
            if (card != null && string.Equals(
                    card.CardId,
                    candidate,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static void EnsureAssetFolder()
    {
        if (AssetDatabase.IsValidFolder(AssetFolder))
            return;
        if (!AssetDatabase.IsValidFolder("Assets/06_Runtime/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        AssetDatabase.CreateFolder("Assets/06_Runtime/Resources", "Cards");
    }
}
