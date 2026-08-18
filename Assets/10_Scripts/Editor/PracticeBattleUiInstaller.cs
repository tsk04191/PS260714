using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PracticeBattleUiInstaller
{
    internal const string ClientScenePath =
        "Assets/04_Scenes/ClientScene.unity";
    internal const string CatalogItemPrefabPath =
        "Assets/07_Prefabs/UI/Practice/btnPracticeCatalogItem.prefab";
    internal const string PracticeToolsRootName = "grpPracticeTools";
    internal const string PracticeDebugButtonName = "btnPracticeDebug";
    internal const string PracticeDebugOverlayName =
        "grpPracticeDebugVisualization";
    internal const string PracticeStageNodeName =
        "btnStage_practice_battle";
    internal const string TutorialPracticeConnectorName =
        "imgStageConnector_tutorial_field_practice_battle";
    internal const string PracticeFreeConnectorName =
        "imgStageConnector_practice_battle_free_battle";

    private const string StageContentPath =
        "grpRuntimeMenuPage/grpMenuPanel/grpMenuButtons/" +
        "scrStageTrack/vptStageTrack/grpStageContent";
    private const string LegacyTutorialNodeName = "btnStage_test_field";
    private const string LegacyTutorialButtonName = "btnSTAGE0TESTFIELD";
    private const string TutorialButtonName = "btnSTAGE0TUTORIALFIELD";
    private const string LegacyTutorialLocalizationKey =
        "ui.stage_select.test_field";
    private const string StageNodePrefix = "btnStage_";
    private const string ConnectorPrefix = "imgStageConnector_";

    private static readonly Color PanelColor =
        new(0.035f, 0.05f, 0.043f, 0.96f);
    private static readonly Color HeaderColor =
        new(0.08f, 0.14f, 0.105f, 0.98f);
    private static readonly Color ButtonColor =
        new(0.12f, 0.21f, 0.155f, 0.98f);
    private static readonly Color ButtonHighlightColor =
        new(0.18f, 0.34f, 0.235f, 1f);
    private static readonly Color DestructiveButtonColor =
        new(0.31f, 0.12f, 0.105f, 0.98f);
    private static readonly Color TextColor =
        new(0.91f, 0.96f, 0.92f, 1f);
    private static readonly Color MutedTextColor =
        new(0.6f, 0.7f, 0.63f, 1f);

    [MenuItem(
        "PS260714/UI/Install Practice Battle UI",
        false,
        117)]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Practice battle UI cannot be installed in Play Mode.");
        }

        PracticeBattleCatalogItemView itemPrefab =
            EnsureCatalogItemPrefab();
        Scene scene = EditorSceneManager.OpenScene(
            ClientScenePath,
            OpenSceneMode.Single);
        itemPrefab = LoadCatalogItemPrefab();
        Require(itemPrefab, "Practice catalog item prefab");
        MigrateLegacyTutorialReferences(scene);
        InstallStageSelectTrack(scene);
        InstallPracticePanel(scene, itemPrefab);
        InstallPracticeDebugOverlay(scene);

        IReadOnlyList<string> issues = ValidateScene(scene);
        if (issues.Count > 0)
        {
            throw new InvalidOperationException(
                "Practice battle UI validation failed:\n- " +
                string.Join("\n- ", issues));
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ClientScenePath))
        {
            throw new InvalidOperationException(
                "Failed to save ClientScene practice battle UI.");
        }
        AssetDatabase.SaveAssets();
        Debug.Log(
            "Installed serialized practice battle UI and stage node in " +
            "ClientScene.");
    }

    public static void InstallFromCommandLine()
    {
        try
        {
            Install();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    internal static IReadOnlyList<string> ValidateScene(Scene scene)
    {
        List<string> issues = new();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            issues.Add("ClientScene is not loaded.");
            return issues;
        }

        DungeonBattleTab tab = FindOne<DungeonBattleTab>(scene);
        if (tab == null)
        {
            issues.Add("DungeonBattleTab is missing.");
        }
        else
        {
            PracticeBattlePanelView panel = tab.PracticeBattlePanel;
            if (panel == null)
            {
                issues.Add("DungeonBattleTab practice panel is unbound.");
            }
            else
            {
                if (!panel.TryValidateDesignerReferences(
                        out string panelError))
                {
                    issues.Add(
                        "Practice panel designer references are " +
                        "incomplete: " + panelError + ".");
                }
                if (panel.gameObject.activeSelf)
                {
                    issues.Add(
                        "Practice tools must be hidden by default.");
                }
                if (!ReferenceEquals(
                        panel.transform.parent,
                        tab.transform))
                {
                    issues.Add(
                        "Practice tools are not a direct tabBattle child.");
                }
                if (panel.transform is RectTransform rect &&
                    (!Mathf.Approximately(rect.anchorMin.x, 1f) ||
                     !Mathf.Approximately(rect.anchorMax.x, 1f)))
                {
                    issues.Add(
                        "Practice tools are not right-anchored.");
                }
            }
        }

        PracticeBattleCatalogItemView prefab =
            AssetDatabase.LoadAssetAtPath<PracticeBattleCatalogItemView>(
                CatalogItemPrefabPath);
        if (prefab == null || !prefab.HasDesignerReferences)
        {
            issues.Add("Practice catalog item prefab is missing or invalid.");
        }

        ValidatePracticeDebugOverlay(scene, issues);

        StageSelectPage stagePage = FindOne<StageSelectPage>(scene);
        Transform content = stagePage != null
            ? stagePage.transform.Find(StageContentPath)
            : null;
        if (content == null)
        {
            issues.Add("Stage Select content hierarchy is missing.");
        }
        else
        {
            if (content.Find(PracticeStageNodeName) == null)
                issues.Add("practice_battle stage node is missing.");
            if (content.Find(TutorialPracticeConnectorName + "/imgLine") ==
                null)
            {
                issues.Add("Tutorial-to-practice connector is missing.");
            }
            if (content.Find(PracticeFreeConnectorName + "/imgLine") == null)
                issues.Add("Practice-to-free connector is missing.");
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            LocalizedText[] localizedTexts =
                root.GetComponentsInChildren<LocalizedText>(true);
            for (int index = 0; index < localizedTexts.Length; index++)
            {
                if (string.Equals(
                        localizedTexts[index].Key,
                        LegacyTutorialLocalizationKey,
                        StringComparison.Ordinal))
                {
                    issues.Add(
                        "Legacy test_field localization remains in " +
                        localizedTexts[index].gameObject.name + ".");
                }
            }
        }

        return issues;
    }

    private static void MigrateLegacyTutorialReferences(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (string.Equals(
                        transforms[index].name,
                        LegacyTutorialButtonName,
                        StringComparison.Ordinal))
                {
                    transforms[index].name = TutorialButtonName;
                }
            }

            LocalizedText[] localizedTexts =
                root.GetComponentsInChildren<LocalizedText>(true);
            for (int index = 0; index < localizedTexts.Length; index++)
            {
                if (string.Equals(
                        localizedTexts[index].Key,
                        LegacyTutorialLocalizationKey,
                        StringComparison.Ordinal))
                {
                    localizedTexts[index].SetKey(
                        LocalizationKeys.UiStageSelectTutorialField,
                        false);
                }
            }
        }
    }

    private static PracticeBattleCatalogItemView EnsureCatalogItemPrefab()
    {
        EnsureAssetFolder("Assets/07_Prefabs/UI/Practice");
        PracticeBattleCatalogItemView existing =
            AssetDatabase.LoadAssetAtPath<PracticeBattleCatalogItemView>(
                CatalogItemPrefabPath);
        if (existing != null)
        {
            if (!existing.HasDesignerReferences)
            {
                throw new InvalidOperationException(
                    "Existing practice catalog prefab is incomplete. " +
                    "Repair it in the Prefab editor before reinstalling.");
            }
            return existing;
        }

        GameObject root = CreateRectObject("btnPracticeCatalogItem", null);
        try
        {
            RectTransform rootRect = root.transform as RectTransform;
            rootRect.sizeDelta = new Vector2(0f, 68f);
            Image background = root.AddComponent<Image>();
            background.color = ButtonColor;
            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            ApplyButtonColors(button, ButtonColor, ButtonHighlightColor);
            LayoutElement layout = root.AddComponent<LayoutElement>();
            layout.preferredHeight = 68f;
            layout.minHeight = 68f;

            Image icon = CreateImage(
                "imgEntryIcon",
                root.transform,
                Color.white);
            SetRect(
                icon.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(12f, 0f),
                new Vector2(50f, 50f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TextMeshProUGUI name = CreateText(
                "txtEntryName",
                root.transform,
                "ENTRY",
                18f,
                TextAlignmentOptions.MidlineLeft,
                TextColor);
            SetStretch(name.rectTransform, 72f, 29f, 54f, 5f);
            TextMeshProUGUI id = CreateText(
                "txtEntryId",
                root.transform,
                "ID",
                12f,
                TextAlignmentOptions.MidlineLeft,
                MutedTextColor);
            SetStretch(id.rectTransform, 72f, 5f, 54f, 35f);
            TextMeshProUGUI action = CreateText(
                "txtAction",
                root.transform,
                "+",
                28f,
                TextAlignmentOptions.Center,
                TextColor);
            SetRect(
                action.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f),
                new Vector2(38f, 50f));

            PracticeBattleCatalogItemView view =
                root.AddComponent<PracticeBattleCatalogItemView>();
            SerializedObject serialized = new(view);
            SetReference(serialized, "actionButton", button);
            SetReference(serialized, "background", background);
            SetReference(serialized, "icon", icon);
            SetReference(serialized, "nameText", name);
            SetReference(serialized, "idText", id);
            SetReference(serialized, "actionText", action);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                root,
                CatalogItemPrefabPath);
            if (saved == null)
            {
                throw new InvalidOperationException(
                    "Failed to save practice catalog item prefab.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        AssetDatabase.ImportAsset(
            CatalogItemPrefabPath,
            ImportAssetOptions.ForceSynchronousImport);
        PracticeBattleCatalogItemView created =
            AssetDatabase.LoadAssetAtPath<PracticeBattleCatalogItemView>(
                CatalogItemPrefabPath);
        if (created == null || !created.HasDesignerReferences)
        {
            throw new InvalidOperationException(
                "Created practice catalog item prefab is invalid.");
        }
        return created;
    }

    private static PracticeBattleCatalogItemView LoadCatalogItemPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CatalogItemPrefabPath);
        return prefab != null
            ? prefab.GetComponent<PracticeBattleCatalogItemView>()
            : null;
    }

    private static void InstallPracticePanel(
        Scene scene,
        PracticeBattleCatalogItemView itemPrefab)
    {
        DungeonBattleTab tab = FindOne<DungeonBattleTab>(scene);
        Require(tab, "DungeonBattleTab");
        Transform existing = tab.transform.Find(PracticeToolsRootName);
        PracticeBattlePanelView existingPanel = existing != null
            ? existing.GetComponent<PracticeBattlePanelView>()
            : null;
        if (existingPanel != null)
        {
            UpgradePracticePanelDebugControls(existingPanel);
            if (!existingPanel.HasDesignerReferences)
            {
                throw new InvalidOperationException(
                    "Existing practice panel is incomplete. Repair its " +
                    "serialized references instead of rebuilding it.");
            }
            BindPanelToTab(tab, existingPanel);
            return;
        }
        if (existing != null)
        {
            throw new InvalidOperationException(
                "grpPracticeTools exists without PracticeBattlePanelView.");
        }

        GameObject root = CreateRectObject(
            PracticeToolsRootName,
            tab.transform);
        root.SetActive(false);
        RectTransform rootRect = root.transform as RectTransform;
        SetRect(
            rootRect,
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0.5f),
            new Vector2(-22f, 0f),
            new Vector2(500f, -76f));

        GameObject body = CreateRectObject("grpPracticePanel", root.transform);
        SetStretch(body.transform as RectTransform, 0f, 0f, 0f, 0f);
        Image bodyImage = body.AddComponent<Image>();
        bodyImage.color = PanelColor;

        Button collapse = CreateButton(
            "btnPracticeCollapse",
            root.transform,
            ">",
            string.Empty,
            ButtonColor);
        SetRect(
            collapse.transform as RectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-8f, -10f),
            new Vector2(42f, 46f));
        TextMeshProUGUI collapseText =
            collapse.GetComponentInChildren<TextMeshProUGUI>(true);

        Image header = CreateImage(
            "imgPracticeHeader",
            body.transform,
            HeaderColor);
        SetRect(
            header.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, 0f),
            new Vector2(0f, 54f));
        TextMeshProUGUI title = CreateText(
            "txtPracticeTitle",
            header.transform,
            "PRACTICE",
            25f,
            TextAlignmentOptions.MidlineLeft,
            TextColor);
        SetStretch(title.rectTransform, 18f, 0f, 56f, 0f);
        AddLocalization(title, LocalizationKeys.UiPracticeTitle);

        Button debug = CreateButton(
            PracticeDebugButtonName,
            header.transform,
            "DEBUG ON",
            string.Empty,
            ButtonColor);
        SetRect(
            debug.transform as RectTransform,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-12f, 0f),
            new Vector2(142f, 34f));
        TextMeshProUGUI debugText = debug
            .GetComponentInChildren<TextMeshProUGUI>(true);

        TMP_InputField search = CreateSearchField(body.transform);
        SetRect(
            search.transform as RectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -64f),
            new Vector2(-24f, 40f));

        GameObject categories = CreateRectObject(
            "grpPracticeCategories",
            body.transform);
        SetRect(
            categories.transform as RectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -112f),
            new Vector2(-24f, 36f));
        Button characters = CreateAnchoredButton(
            categories.transform,
            "btnPracticeCharacters",
            "CHARACTERS",
            LocalizationKeys.UiPracticeCharacters,
            0f,
            1f / 3f);
        Button enemies = CreateAnchoredButton(
            categories.transform,
            "btnPracticeEnemies",
            "ENEMIES",
            LocalizationKeys.UiPracticeEnemies,
            1f / 3f,
            2f / 3f);
        Button cards = CreateAnchoredButton(
            categories.transform,
            "btnPracticeCards",
            "CARDS",
            LocalizationKeys.UiPracticeCards,
            2f / 3f,
            1f);

        GameObject slots = CreateRectObject(
            "grpPracticeCharacterSlots",
            body.transform);
        SetRect(
            slots.transform as RectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -158f),
            new Vector2(-24f, 72f));
        Button[] slotButtons = new Button[DungeonPage.MaximumPartySize];
        TextMeshProUGUI[] slotTexts =
            new TextMeshProUGUI[DungeonPage.MaximumPartySize];
        for (int index = 0; index < DungeonPage.MaximumPartySize; index++)
        {
            float minimum = index / (float)DungeonPage.MaximumPartySize;
            float maximum =
                (index + 1f) / DungeonPage.MaximumPartySize;
            slotButtons[index] = CreateAnchoredButton(
                slots.transform,
                $"btnPracticeSlot{index + 1}",
                $"{index + 1} · EMPTY",
                string.Empty,
                minimum,
                maximum);
            RectTransform slotRect =
                slotButtons[index].transform as RectTransform;
            slotRect.anchorMin = new Vector2(minimum, 1f);
            slotRect.anchorMax = new Vector2(maximum, 1f);
            slotRect.pivot = new Vector2(0.5f, 1f);
            slotRect.anchoredPosition = Vector2.zero;
            slotRect.sizeDelta = new Vector2(-4f, 34f);
            slotTexts[index] = slotButtons[index]
                .GetComponentInChildren<TextMeshProUGUI>(true);
        }
        Button removeCharacter = CreateButton(
            "btnPracticeRemoveCharacter",
            slots.transform,
            "REMOVE CHARACTER",
            LocalizationKeys.UiPracticeRemoveCharacter,
            ButtonColor);
        SetRect(
            removeCharacter.transform as RectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            Vector2.zero,
            new Vector2(0f, 30f));

        GameObject spawn = CreateRectObject(
            "grpPracticeSpawnControls",
            body.transform);
        SetRect(
            spawn.transform as RectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -238f),
            new Vector2(-24f, 36f));
        Button decrease = CreateButton(
            "btnPracticeSpawnDecrease",
            spawn.transform,
            "−",
            string.Empty,
            ButtonColor);
        SetRect(
            decrease.transform as RectTransform,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            Vector2.zero,
            new Vector2(42f, 0f));
        TextMeshProUGUI countText = CreateText(
            "txtPracticeSpawnCount",
            spawn.transform,
            "1",
            20f,
            TextAlignmentOptions.Center,
            TextColor);
        SetRect(
            countText.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(48f, 0f),
            new Vector2(54f, 0f));
        Button increase = CreateButton(
            "btnPracticeSpawnIncrease",
            spawn.transform,
            "+",
            string.Empty,
            ButtonColor);
        SetRect(
            increase.transform as RectTransform,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(108f, 0f),
            new Vector2(42f, 0f));
        Button queueMode = CreateButton(
            "btnPracticeQueueMode",
            spawn.transform,
            "DIRECT",
            string.Empty,
            ButtonColor);
        SetRect(
            queueMode.transform as RectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(92f, 0f),
            new Vector2(-184f, 0f));
        TextMeshProUGUI queueText = queueMode
            .GetComponentInChildren<TextMeshProUGUI>(true);

        ScrollRect catalog = CreateCatalogScroll(
            body.transform,
            out RectTransform catalogContent);
        RectTransform catalogRect = catalog.transform as RectTransform;
        catalogRect.anchorMin = Vector2.zero;
        catalogRect.anchorMax = Vector2.one;
        catalogRect.offsetMin = new Vector2(12f, 184f);
        catalogRect.offsetMax = new Vector2(-12f, -282f);

        GameObject actions = CreateRectObject(
            "grpPracticeActions",
            body.transform);
        RectTransform actionsRect = actions.transform as RectTransform;
        actionsRect.anchorMin = new Vector2(0f, 0f);
        actionsRect.anchorMax = new Vector2(1f, 0f);
        actionsRect.pivot = new Vector2(0.5f, 0f);
        actionsRect.anchoredPosition = new Vector2(0f, 40f);
        actionsRect.sizeDelta = new Vector2(-24f, 132f);

        Button clearEnemies = CreateGridButton(
            actions.transform,
            "btnPracticeClearEnemies",
            "CLEAR ENEMIES",
            LocalizationKeys.UiPracticeClearEnemies,
            0,
            0,
            ButtonColor);
        Button restoreParty = CreateGridButton(
            actions.transform,
            "btnPracticeRestoreParty",
            "RESTORE PARTY",
            LocalizationKeys.UiPracticeRestoreParty,
            1,
            0,
            ButtonColor);
        Button restoreCore = CreateGridButton(
            actions.transform,
            "btnPracticeRestoreCore",
            "RESTORE CORE",
            LocalizationKeys.UiPracticeRestoreCore,
            0,
            1,
            ButtonColor);
        Button refillEnergy = CreateGridButton(
            actions.transform,
            "btnPracticeRefillEnergy",
            "REFILL ENERGY",
            LocalizationKeys.UiPracticeRefillEnergy,
            1,
            1,
            ButtonColor);
        Button resetPractice = CreateGridButton(
            actions.transform,
            "btnPracticeReset",
            "RESET",
            LocalizationKeys.UiPracticeReset,
            0,
            2,
            DestructiveButtonColor);
        Button exitPractice = CreateGridButton(
            actions.transform,
            "btnPracticeExit",
            "EXIT",
            LocalizationKeys.UiPracticeExit,
            1,
            2,
            DestructiveButtonColor);

        TextMeshProUGUI status = CreateText(
            "txtPracticeStatus",
            body.transform,
            string.Empty,
            13f,
            TextAlignmentOptions.MidlineLeft,
            MutedTextColor);
        SetRect(
            status.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 7f),
            new Vector2(-24f, 26f));

        PracticeBattlePanelView panel =
            root.AddComponent<PracticeBattlePanelView>();
        SerializedObject serialized = new(panel);
        SetReference(serialized, "panelBody", body);
        SetReference(serialized, "collapseButton", collapse);
        SetReference(serialized, "collapseText", collapseText);
        SetReference(serialized, "statusText", status);
        SetReference(serialized, "searchInput", search);
        SetReference(serialized, "charactersButton", characters);
        SetReference(serialized, "enemiesButton", enemies);
        SetReference(serialized, "cardsButton", cards);
        SetReference(serialized, "catalogScroll", catalog);
        SetReference(serialized, "catalogContent", catalogContent);
        SetReference(serialized, "catalogItemPrefab", itemPrefab);
        SetReferenceArray(serialized, "characterSlotButtons", slotButtons);
        SetReferenceArray(serialized, "characterSlotTexts", slotTexts);
        SetReference(
            serialized,
            "removeCharacterButton",
            removeCharacter);
        SetReference(
            serialized,
            "decreaseSpawnCountButton",
            decrease);
        SetReference(
            serialized,
            "increaseSpawnCountButton",
            increase);
        SetReference(serialized, "spawnCountText", countText);
        SetReference(serialized, "queueModeButton", queueMode);
        SetReference(serialized, "queueModeText", queueText);
        SetReference(serialized, "clearEnemiesButton", clearEnemies);
        SetReference(serialized, "restorePartyButton", restoreParty);
        SetReference(serialized, "restoreCoreButton", restoreCore);
        SetReference(serialized, "refillEnergyButton", refillEnergy);
        SetReference(serialized, "resetPracticeButton", resetPractice);
        SetReference(serialized, "exitPracticeButton", exitPractice);
        SetReference(serialized, "debugButton", debug);
        SetReference(serialized, "debugButtonText", debugText);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        BindPanelToTab(tab, panel);
        root.SetActive(false);
    }

    internal static void UpgradePracticePanelDebugControls(
        PracticeBattlePanelView panel)
    {
        Require(panel, "PracticeBattlePanelView");
        SerializedObject serialized = new(panel);
        serialized.UpdateIfRequiredOrScript();
        SerializedProperty buttonProperty =
            serialized.FindProperty("debugButton");
        SerializedProperty textProperty =
            serialized.FindProperty("debugButtonText");
        if (buttonProperty == null || textProperty == null)
        {
            throw new InvalidOperationException(
                "PracticeBattlePanelView debug fields are missing. " +
                "Compile the runtime debug UI contract before installing.");
        }

        Button button = buttonProperty.objectReferenceValue as Button;
        TextMeshProUGUI text =
            textProperty.objectReferenceValue as TextMeshProUGUI;
        if (button == null && text != null)
            button = text.GetComponentInParent<Button>();

        if (button == null)
        {
            Transform header = panel.transform.Find(
                "grpPracticePanel/imgPracticeHeader");
            Require(header, "Practice panel header");
            Transform existing = header.Find(PracticeDebugButtonName);
            if (existing != null)
            {
                button = existing.GetComponent<Button>();
                Require(button, PracticeDebugButtonName + " Button");
            }
            else
            {
                button = CreateButton(
                    PracticeDebugButtonName,
                    header,
                    "DEBUG ON",
                    string.Empty,
                    ButtonColor);
                SetRect(
                    button.transform as RectTransform,
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f),
                    new Vector2(-12f, 0f),
                    new Vector2(142f, 34f));
            }
        }

        if (text == null)
        {
            text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            Require(text, PracticeDebugButtonName + " label");
        }

        buttonProperty.objectReferenceValue = button;
        textProperty.objectReferenceValue = text;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(panel);
    }

    private static void InstallPracticeDebugOverlay(Scene scene)
    {
        DungeonBoardView board = FindOne<DungeonBoardView>(scene);
        Require(board, "DungeonBoardView");
        SerializedObject boardSerialized = new(board);
        boardSerialized.UpdateIfRequiredOrScript();
        SerializedProperty inputProperty =
            boardSerialized.FindProperty("worldInputView");
        SerializedProperty overlayProperty =
            boardSerialized.FindProperty("practiceDebugOverlay");
        if (inputProperty == null || overlayProperty == null)
        {
            throw new InvalidOperationException(
                "DungeonBoardView practice debug serialized fields are " +
                "missing.");
        }

        DungeonWorldInputView input =
            inputProperty.objectReferenceValue as DungeonWorldInputView;
        Require(input, "DungeonBoardView worldInputView");
        RectMask2D inputMask = input.GetComponent<RectMask2D>();
        if (inputMask == null)
            inputMask = input.gameObject.AddComponent<RectMask2D>();
        EditorUtility.SetDirty(inputMask);
        PracticeBattleDebugOverlayView overlay =
            overlayProperty.objectReferenceValue as
                PracticeBattleDebugOverlayView;
        bool created = false;
        if (overlay == null)
        {
            Transform existing = input.transform.Find(
                PracticeDebugOverlayName);
            if (existing != null)
            {
                overlay = existing.GetComponent<
                    PracticeBattleDebugOverlayView>();
                if (overlay == null)
                {
                    overlay = existing.gameObject.AddComponent<
                        PracticeBattleDebugOverlayView>();
                }
            }
            else
            {
                GameObject root = CreateRectObject(
                    PracticeDebugOverlayName,
                    input.transform);
                root.layer = input.gameObject.layer;
                RectTransform rect = root.transform as RectTransform;
                SetStretch(rect, 0f, 0f, 0f, 0f);
                if (input.transform is RectTransform inputRect)
                    rect.pivot = inputRect.pivot;
                root.transform.SetAsLastSibling();
                overlay = root.AddComponent<
                    PracticeBattleDebugOverlayView>();
                created = true;
            }
        }
        Require(overlay, "PracticeBattleDebugOverlayView");

        PracticeBattleDebugOverlayGraphic graphic =
            overlay.GetComponent<PracticeBattleDebugOverlayGraphic>();
        if (graphic == null)
        {
            graphic = overlay.gameObject.AddComponent<
                PracticeBattleDebugOverlayGraphic>();
        }
        graphic.raycastTarget = false;
        graphic.enabled = false;

        SerializedObject overlaySerialized = new(overlay);
        overlaySerialized.UpdateIfRequiredOrScript();
        SerializedProperty graphicProperty =
            overlaySerialized.FindProperty("overlayGraphic");
        if (graphicProperty == null)
        {
            throw new InvalidOperationException(
                "PracticeBattleDebugOverlayView.overlayGraphic is " +
                "missing.");
        }
        if (graphicProperty.objectReferenceValue == null)
            graphicProperty.objectReferenceValue = graphic;
        else if (!ReferenceEquals(
                     graphicProperty.objectReferenceValue,
                     graphic))
        {
            throw new InvalidOperationException(
                "Practice debug overlay references a Graphic on a " +
                "different GameObject.");
        }
        overlaySerialized.ApplyModifiedPropertiesWithoutUndo();

        overlayProperty.objectReferenceValue = overlay;
        boardSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(board);
        EditorUtility.SetDirty(overlay);
        EditorUtility.SetDirty(graphic);

        if (created && overlay.transform is RectTransform createdRect &&
            input.transform is RectTransform createdInputRect)
        {
            createdRect.pivot = createdInputRect.pivot;
        }
    }

    private static void ValidatePracticeDebugOverlay(
        Scene scene,
        ICollection<string> issues)
    {
        DungeonBoardView board = FindOne<DungeonBoardView>(scene);
        if (board == null)
        {
            issues.Add("DungeonBoardView is missing.");
            return;
        }

        SerializedObject boardSerialized = new(board);
        DungeonWorldInputView input = boardSerialized
            .FindProperty("worldInputView")?.objectReferenceValue as
                DungeonWorldInputView;
        PracticeBattleDebugOverlayView overlay = boardSerialized
            .FindProperty("practiceDebugOverlay")?.objectReferenceValue as
                PracticeBattleDebugOverlayView;
        if (input == null)
            issues.Add("DungeonBoardView world input is unbound.");
        else if (input.GetComponent<RectMask2D>() == null)
            issues.Add("DungeonBoardView world input RectMask2D is missing.");
        if (overlay == null)
        {
            issues.Add("DungeonBoardView practice debug overlay is unbound.");
            return;
        }

        SerializedObject overlaySerialized = new(overlay);
        PracticeBattleDebugOverlayGraphic graphic = overlaySerialized
            .FindProperty("overlayGraphic")?.objectReferenceValue as
                PracticeBattleDebugOverlayGraphic;
        if (graphic == null)
        {
            issues.Add("Practice debug overlay Graphic is unbound.");
            return;
        }
        if (!ReferenceEquals(graphic.gameObject, overlay.gameObject))
        {
            issues.Add(
                "Practice debug overlay components are on different " +
                "GameObjects.");
        }
        if (graphic.raycastTarget)
            issues.Add("Practice debug overlay blocks pointer raycasts.");
        if (graphic.enabled)
            issues.Add("Practice debug overlay must be off by default.");
        if (graphic.GetComponent<CanvasRenderer>() == null)
            issues.Add("Practice debug overlay CanvasRenderer is missing.");
        if (input == null)
            return;
        if (!ReferenceEquals(overlay.transform.parent, input.transform))
        {
            issues.Add(
                "Practice debug overlay is not a world input child.");
            return;
        }
        if (overlay.transform is not RectTransform rect ||
            input.transform is not RectTransform inputRect)
        {
            issues.Add("Practice debug overlay RectTransform is missing.");
            return;
        }
        if (rect.anchorMin != Vector2.zero ||
            rect.anchorMax != Vector2.one ||
            rect.offsetMin != Vector2.zero ||
            rect.offsetMax != Vector2.zero ||
            rect.pivot != inputRect.pivot)
        {
            issues.Add(
                "Practice debug overlay does not match the world input " +
                "RectTransform.");
        }
        if (rect.GetSiblingIndex() != input.transform.childCount - 1)
        {
            issues.Add(
                "Practice debug overlay is not above world input children.");
        }
    }

    private static void InstallStageSelectTrack(Scene scene)
    {
        StageSelectPage page = FindOne<StageSelectPage>(scene);
        Require(page, "StageSelectPage");
        Transform content = page.transform.Find(StageContentPath);
        Require(content, "Stage Select content");

        Transform legacyTutorial = content.Find(LegacyTutorialNodeName);
        if (legacyTutorial != null &&
            content.Find("btnStage_tutorial_field") == null)
        {
            legacyTutorial.name = "btnStage_tutorial_field";
        }

        GameObject nodeTemplate = FindFirstDirectChild(
            content,
            StageNodePrefix);
        GameObject connectorTemplate = FindFirstDirectChild(
            content,
            ConnectorPrefix);
        Require(nodeTemplate, "Stage node template");
        Require(connectorTemplate, "Stage connector template");

        DungeonDefinitionCatalog.Invalidate();
        IReadOnlyList<DungeonDefinition> definitions =
            DungeonDefinitionCatalog.GetStageSelectDefinitions();
        HashSet<string> expectedConnectors =
            new(StringComparer.Ordinal);
        List<Transform> ordered = new();
        for (int index = 0; index < definitions.Count; index++)
        {
            DungeonDefinition definition = definitions[index];
            string nodeName = StageNodePrefix + definition.DungeonId;
            Transform node = content.Find(nodeName);
            bool created = node == null;
            if (created)
            {
                GameObject clone = UnityEngine.Object.Instantiate(
                    nodeTemplate,
                    content,
                    false);
                clone.name = nodeName;
                node = clone.transform;
            }
            InitializeStageNode(node, definition, index, created);

            if (index > 0)
            {
                DungeonDefinition previous = definitions[index - 1];
                string connectorName = ConnectorPrefix +
                                       previous.DungeonId + "_" +
                                       definition.DungeonId;
                expectedConnectors.Add(connectorName);
                Transform connector = content.Find(connectorName);
                if (connector == null)
                {
                    GameObject clone = UnityEngine.Object.Instantiate(
                        connectorTemplate,
                        content,
                        false);
                    clone.name = connectorName;
                    connector = clone.transform;
                }
                ordered.Add(connector);
            }
            ordered.Add(node);
        }

        for (int index = content.childCount - 1; index >= 0; index--)
        {
            Transform child = content.GetChild(index);
            if (child.name.StartsWith(
                    ConnectorPrefix,
                    StringComparison.Ordinal) &&
                !expectedConnectors.Contains(child.name))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        for (int index = 0; index < ordered.Count; index++)
            ordered[index].SetSiblingIndex(index);

        if (!page.ValidateEditorUi(out string error))
        {
            throw new InvalidOperationException(
                "Stage Select UI validation failed: " + error);
        }
    }

    internal static void InitializeStageNode(
        Transform node,
        DungeonDefinition definition,
        int index,
        bool applyCoverPresentation)
    {
        Image cover = node.Find("imgStageCover")?.GetComponent<Image>();
        if (cover != null && applyCoverPresentation)
        {
            cover.sprite = definition.StageCoverSprite;
            cover.color = cover.sprite != null
                ? Color.white
                : new Color(0.12f, 0.17f, 0.145f, 1f);
        }

        TextMeshProUGUI title = node.Find(
                "grpStageTitleBanner/txtStageTitle")
            ?.GetComponent<TextMeshProUGUI>() ??
            node.Find("txtStageTitle")?.GetComponent<TextMeshProUGUI>();
        if (title != null)
        {
            title.text = definition.FallbackTitle;
            LocalizedText localized = title.GetComponent<LocalizedText>();
            localized?.SetKey(definition.TitleLocalizationKey, false);
        }

        TextMeshProUGUI sequence = node.Find(
                "grpStageTitleBanner/txtStageSequence")
            ?.GetComponent<TextMeshProUGUI>();
        if (sequence != null)
        {
            sequence.text = definition.IsPractice
                ? "PRACTICE"
                : index == 0 ? "STAGE 0" : $"STAGE {index}";
        }
        node.gameObject.SetActive(true);
    }

    private static TMP_InputField CreateSearchField(Transform parent)
    {
        GameObject root = CreateRectObject("inpPracticeSearch", parent);
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.07f, 0.1f, 0.085f, 1f);
        TMP_InputField input = root.AddComponent<TMP_InputField>();
        input.targetGraphic = background;

        GameObject viewport = CreateRectObject("vptSearchText", root.transform);
        RectTransform viewportRect = viewport.transform as RectTransform;
        SetStretch(viewportRect, 12f, 4f, 12f, 4f);
        viewport.AddComponent<RectMask2D>();
        TextMeshProUGUI text = CreateText(
            "txtSearchValue",
            viewport.transform,
            string.Empty,
            16f,
            TextAlignmentOptions.MidlineLeft,
            TextColor);
        SetStretch(text.rectTransform, 0f, 0f, 0f, 0f);
        TextMeshProUGUI placeholder = CreateText(
            "txtSearchPlaceholder",
            viewport.transform,
            "NAME OR ID",
            16f,
            TextAlignmentOptions.MidlineLeft,
            MutedTextColor);
        SetStretch(placeholder.rectTransform, 0f, 0f, 0f, 0f);
        AddLocalization(
            placeholder,
            LocalizationKeys.UiPracticeSearchPlaceholder);
        input.textViewport = viewportRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }

    private static ScrollRect CreateCatalogScroll(
        Transform parent,
        out RectTransform content)
    {
        GameObject root = CreateRectObject("scrPracticeCatalog", parent);
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.025f, 0.035f, 0.03f, 0.92f);
        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = CreateRectObject(
            "vptPracticeCatalog",
            root.transform);
        RectTransform viewportRect = viewport.transform as RectTransform;
        SetStretch(viewportRect, 4f, 4f, 4f, 4f);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = Color.white;
        viewportImage.raycastTarget = true;
        viewport.AddComponent<RectMask2D>();

        GameObject contentObject = CreateRectObject(
            "grpPracticeCatalogContent",
            viewport.transform);
        content = contentObject.transform as RectTransform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout =
            contentObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter =
            contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewportRect;
        scroll.content = content;
        return scroll;
    }

    private static Button CreateGridButton(
        Transform parent,
        string name,
        string fallback,
        string localizationKey,
        int column,
        int row,
        Color color)
    {
        Button button = CreateButton(
            name,
            parent,
            fallback,
            localizationKey,
            color);
        RectTransform rect = button.transform as RectTransform;
        float left = column * 0.5f;
        float right = (column + 1f) * 0.5f;
        rect.anchorMin = new Vector2(left, 1f);
        rect.anchorMax = new Vector2(right, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -row * 44f);
        rect.sizeDelta = new Vector2(-5f, 39f);
        return button;
    }

    private static Button CreateAnchoredButton(
        Transform parent,
        string name,
        string fallback,
        string localizationKey,
        float anchorMinimum,
        float anchorMaximum,
        float bottom = 0f,
        float height = 0f)
    {
        Button button = CreateButton(
            name,
            parent,
            fallback,
            localizationKey,
            ButtonColor);
        RectTransform rect = button.transform as RectTransform;
        rect.anchorMin = new Vector2(anchorMinimum, bottom > 0f ? 0f : 0f);
        rect.anchorMax = new Vector2(
            anchorMaximum,
            height > 0f ? 0f : 1f);
        rect.pivot = new Vector2(0.5f, height > 0f ? 0f : 0.5f);
        rect.anchoredPosition = new Vector2(0f, bottom);
        rect.sizeDelta = new Vector2(-4f, height);
        return button;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string fallback,
        string localizationKey,
        Color color)
    {
        GameObject root = CreateRectObject(name, parent);
        Image image = root.AddComponent<Image>();
        image.color = color;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        ApplyButtonColors(button, color, ButtonHighlightColor);
        TextMeshProUGUI text = CreateText(
            "txtLabel",
            root.transform,
            fallback,
            14f,
            TextAlignmentOptions.Center,
            TextColor);
        SetStretch(text.rectTransform, 7f, 3f, 7f, 3f);
        if (!string.IsNullOrWhiteSpace(localizationKey))
            AddLocalization(text, localizationKey);
        return button;
    }

    private static void ApplyButtonColors(
        Button button,
        Color normal,
        Color highlighted)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = highlighted / Mathf.Max(
            0.01f,
            normal.maxColorComponent);
        colors.pressedColor = new Color(0.65f, 0.75f, 0.68f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.35f, 0.38f, 0.36f, 0.7f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Color color)
    {
        GameObject root = CreateRectObject(name, parent);
        Image image = root.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string value,
        float size,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject root = CreateRectObject(name, parent);
        TextMeshProUGUI text = root.AddComponent<TextMeshProUGUI>();
        text.text = value ?? string.Empty;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static void AddLocalization(
        TextMeshProUGUI text,
        string key)
    {
        if (text == null || string.IsNullOrWhiteSpace(key))
            return;
        LocalizedText localized = text.gameObject.AddComponent<LocalizedText>();
        localized.SetKey(key, false);
    }

    private static GameObject CreateRectObject(
        string name,
        Transform parent)
    {
        GameObject result = new(name, typeof(RectTransform));
        if (parent != null)
            result.transform.SetParent(parent, false);
        return result;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMinimum,
        Vector2 anchorMaximum,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMinimum;
        rect.anchorMax = anchorMaximum;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
    }

    private static void SetStretch(
        RectTransform rect,
        float left,
        float bottom,
        float right,
        float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;
    }

    private static void BindPanelToTab(
        DungeonBattleTab tab,
        PracticeBattlePanelView panel)
    {
        SerializedObject serialized = new(tab);
        SetReference(serialized, "practiceBattlePanel", panel);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tab);
        EditorUtility.SetDirty(panel);
    }

    private static void SetReference(
        SerializedObject serialized,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"Serialized property '{propertyName}' is missing on " +
                serialized.targetObject.GetType().Name + ".");
        }
        property.objectReferenceValue = value;
    }

    private static void SetReferenceArray<T>(
        SerializedObject serialized,
        string propertyName,
        IReadOnlyList<T> values)
        where T : UnityEngine.Object
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            throw new InvalidOperationException(
                $"Serialized array '{propertyName}' is missing.");
        }
        property.arraySize = values?.Count ?? 0;
        for (int index = 0; index < property.arraySize; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue =
                values[index];
        }
    }

    private static GameObject FindFirstDirectChild(
        Transform parent,
        string prefix)
    {
        if (parent == null)
            return null;
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child.name.StartsWith(prefix, StringComparison.Ordinal))
                return child.gameObject;
        }
        return null;
    }

    private static T FindOne<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
                return found;
        }
        return null;
    }

    private static void EnsureAssetFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];
        for (int index = 1; index < segments.Length; index++)
        {
            string next = current + "/" + segments[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[index]);
            current = next;
        }
    }

    private static void Require(UnityEngine.Object value, string label)
    {
        if (value == null)
        {
            throw new InvalidOperationException(label + " is missing.");
        }
    }
}
