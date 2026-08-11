using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BattleCardEditorWindow : EditorWindow
{
    public const string MenuPath = PS260714EditorMenu.BattleCardEditor;

    private const string AssetFolder = "Assets/Resources/Cards";
    private const string RenameControlName = "BattleCardAssetRenameField";

    private readonly List<BattleCardSO> cards = new();
    private BattleCardSO selected;
    private SerializedObject serialized;
    private Vector2 listScroll;
    private Vector2 detailScroll;
    private string search = string.Empty;
    private string renameText = string.Empty;
    private bool renaming;
    private bool focusRename;

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
        RefreshCards(selected);
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= HandleProjectChanged;
        Selection.selectionChanged -= HandleSelectionChanged;
    }

    private void HandleProjectChanged()
    {
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
            () => RefreshCards(selected));
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
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            int visible = 0;
            foreach (BattleCardSO card in cards)
            {
                if (card == null || !MatchesSearch(card))
                    continue;
                visible++;
                if (PS260714AssetEditorList.DrawAssetRow(
                        card == selected,
                        card,
                        card.Icon,
                        card.GetLocalizedDisplayName(),
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
            DrawSection(
                "Identity & Presentation",
                "cardId",
                "rarity",
                "sortOrder",
                "nameLocalizationKey",
                "descriptionLocalizationKey",
                "koreanName",
                "englishName",
                "koreanDescription",
                "englishDescription",
                "icon",
                "illustration");
            DrawAffiliation();
            DrawSection(
                "Draw & Play Rules",
                "energyCost",
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
            EditorGUILayout.Space(4f);
            BattleAbilityEditorGUI.DrawEffectList(
                Find("abilityEffects"),
                selected);
        }
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

        if ((Find("abilityEffects")?.arraySize ?? 0) == 0)
        {
            EditorGUILayout.HelpBox(
                "A battle card requires at least one shared effect.",
                MessageType.Error);
        }
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
        data.FindProperty("koreanName").stringValue = "New Battle Card";
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
        selected = card;
        serialized = card != null ? new SerializedObject(card) : null;
        detailScroll = Vector2.zero;
        CancelRename();
    }

    private bool MatchesSearch(BattleCardSO card)
    {
        string query = search?.Trim() ?? string.Empty;
        return query.Length == 0 ||
               Contains(card.name, query) ||
               Contains(card.CardId, query) ||
               Contains(card.GetLocalizedDisplayName(), query) ||
               Contains(card.Affiliation.ToString(), query);
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
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        AssetDatabase.CreateFolder("Assets/Resources", "Cards");
    }
}
