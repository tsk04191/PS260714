using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuPageSceneBuilder
{
    private const string ClientScenePath = "Assets/Scenes/ClientScene.unity";
    private static readonly string[] MainMenuButtonNames =
    {
        "btnPLAY",
        "btnROSTER",
        "btnSHOP",
        "btnRECRUIT",
        "btnBASE",
        "btnSTORAGE",
    };
    private const string ValidateDesignerUiMenuPath =
        "PS260714/UI/Validate Designer UI";
    private const string MigrateRuntimeUiMenuPath =
        "PS260714/UI/Migrate Runtime UI For Designer";
    private static readonly bool LegacyBuilderDisabled = true;

    [MenuItem(ValidateDesignerUiMenuPath)]
    private static void ValidateDesignerUi()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            scene.path != ClientScenePath)
        {
            Debug.LogWarning(
                "Open Assets/Scenes/ClientScene.unity before validating UI.");
            return;
        }

        List<string> issues = CollectDesignerUiIssues(scene);
        if (issues.Count == 0)
        {
            Debug.Log(
                "Designer UI validation passed. No automatic rebuild was run.");
            return;
        }

        Debug.LogWarning(
            "Designer UI validation found:\n- " +
            string.Join("\n- ", issues));
    }

    [MenuItem(MigrateRuntimeUiMenuPath)]
    private static void MigrateRuntimeUiFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            scene.path != ClientScenePath)
        {
            EditorUtility.DisplayDialog(
                "PS260714 UI",
                "Open ClientScene and run this command again.",
                "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Migrate Runtime UI For Designer",
                "This one-time migration unlocks menu and codex layout " +
                "groups and preserves the current scene hierarchy.",
                "Migrate",
                "Cancel"))
        {
            return;
        }

        MigrateRuntimeUi(scene);
    }

    public static void MigrateRuntimeUiForDesignerBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(
            ClientScenePath,
            OpenSceneMode.Single);
        MigrateRuntimeUi(scene);
    }

    internal static void MigrateRuntimeUiForDesigner(Scene scene)
    {
        MigrateRuntimeUi(scene);
    }

    internal static IReadOnlyList<string> ValidateDesignerUiForScene(
        Scene scene)
    {
        return CollectDesignerUiIssues(scene);
    }

    private static void MigrateRuntimeUi(Scene scene)
    {
        GameObject layClient = FindSceneObject(scene, "layClient");
        if (layClient == null)
        {
            Debug.LogError("layClient was not found in ClientScene.");
            return;
        }

        int migratedCount = MigrateStaticMenuPages(layClient);
        if (EnsureRecruitDesignerUi(layClient))
            migratedCount++;
        string[] pageNames =
        {
            "pagEnemyCodex",
            "pagCharacterCodex",
            "pagSkillCodex",
            "pagItemCodex",
        };
        foreach (string pageName in pageNames)
        {
            GameObject pageObject = FindDirectChild(layClient, pageName);
            if (pageObject != null &&
                MigrateCodexPage(
                    pageObject,
                    pageName == "pagCharacterCodex"))
            {
                migratedCount++;
            }
        }

        if (migratedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (migratedCount > 0)
        {
            Debug.Log(
                $"Runtime designer UI migration complete. " +
                $"Migrated pages: {migratedCount}. " +
                "Existing designer layouts were not rebuilt.");
        }
    }

    private static bool EnsureRecruitDesignerUi(GameObject layClient)
    {
        GameObject recruitObject =
            FindDirectChild(layClient, "pagRecruit");
        MainSubPage recruitPage = recruitObject != null
            ? recruitObject.GetComponent<MainSubPage>()
            : null;
        if (recruitPage == null)
            return false;

        RecruitBannerDesignerBindings bannerBindings =
            recruitObject.GetComponentInChildren<
                RecruitBannerDesignerBindings>(true);
        RecruitRevealDesignerBindings revealBindings =
            recruitObject.GetComponentInChildren<
                RecruitRevealDesignerBindings>(true);
        bool alreadyValid =
            bannerBindings != null &&
            bannerBindings.HasDesignerLayout &&
            bannerBindings.HasRequiredReferences &&
            revealBindings != null &&
            revealBindings.HasDesignerLayout &&
            revealBindings.HasRequiredReferences;
        if (alreadyValid)
            return false;

        if (!recruitPage.SyncRecruitEditorPreview(
                0,
                0,
                out string error))
        {
            Debug.LogError(
                "Recruit designer UI migration failed: " + error,
                recruitPage);
            return false;
        }
        return true;
    }

    private static int MigrateStaticMenuPages(GameObject layClient)
    {
        string[] pageNames =
        {
            "pagTitle",
            "pagMain",
            "pagStageSelect",
            "pagBase",
            "pagRoster",
            "pagShop",
            "pagRecruit",
            "pagStorage",
        };
        int migratedCount = 0;
        foreach (string pageName in pageNames)
        {
            GameObject pageObject = FindDirectChild(layClient, pageName);
            RuntimeMenuPageBase page = pageObject != null
                ? pageObject.GetComponent<RuntimeMenuPageBase>()
                : null;
            if (page == null)
                continue;

            if (page.HasDesignerLayout)
            {
                if (RepairCollapsedStaticMenuLayout(page))
                {
                    migratedCount++;
                }

                continue;
            }

            Transform runtimeRoot = pageObject.transform.Find(
                RuntimeMenuPageBase.RuntimeRootObjectName);
            Transform panel = runtimeRoot != null
                ? runtimeRoot.Find("grpMenuPanel")
                : null;
            Transform buttonRoot = panel != null
                ? panel.Find("grpMenuButtons")
                : null;
            if (runtimeRoot == null || panel == null || buttonRoot == null)
            {
                Debug.LogWarning(
                    $"{pageName}: generated menu hierarchy is missing.");
                continue;
            }

            BakeAndDisableMenuLayout(pageObject, panel, buttonRoot);
            BindRuntimePageReferences(
                page,
                runtimeRoot as RectTransform,
                panel as RectTransform,
                buttonRoot as RectTransform);
            RepairCollapsedStaticMenuLayout(page);
            migratedCount++;
        }

        return migratedCount;
    }

    internal static void RestoreMainMenuDefaultLayout(MainPage page)
    {
        if (page == null || Application.isPlaying)
            return;

        if (!TryGetMainMenuLayout(
                page,
                out RectTransform panel,
                out RectTransform title,
                out RectTransform description,
                out RectTransform buttonRoot,
                out List<RectTransform> buttons))
        {
            Debug.LogWarning(
                "Main menu hierarchy is incomplete. Rebuild its editor " +
                "preview before restoring the layout.",
                page);
            return;
        }

        ApplyMainMenuDefaultLayout(
            panel,
            title,
            description,
            buttonRoot,
            buttons);
        ApplyMainUtilityLayout(page);
        Scene scene = page.gameObject.scene;
        if (scene.IsValid() && scene.isLoaded)
            EditorSceneManager.MarkSceneDirty(scene);
    }

    internal static void RestoreTitleMenuDefaultLayout(TitlePage page)
    {
        if (page == null || Application.isPlaying)
            return;

        if (!TryGetTitleMenuLayout(
                page,
                out RectTransform panel,
                out RectTransform title,
                out RectTransform description,
                out RectTransform buttonRoot,
                out RectTransform startButton,
                out RectTransform noticeButton,
                out RectTransform settingsButton))
        {
            Debug.LogWarning(
                "Title menu hierarchy is incomplete. Rebuild its editor " +
                "preview before restoring the layout.",
                page);
            return;
        }

        ApplyTitleMenuDefaultLayout(
            panel,
            title,
            description,
            buttonRoot,
            startButton,
            noticeButton,
            settingsButton);
        Scene scene = page.gameObject.scene;
        if (scene.IsValid() && scene.isLoaded)
            EditorSceneManager.MarkSceneDirty(scene);
    }

    internal static void RestoreStaticMenuDefaultLayout(
        RuntimeMenuPageBase page)
    {
        if (page == null || Application.isPlaying)
            return;

        if (page is TitlePage titlePage)
        {
            RestoreTitleMenuDefaultLayout(titlePage);
            return;
        }

        if (page is MainPage mainPage)
        {
            RestoreMainMenuDefaultLayout(mainPage);
            return;
        }

        if (!TryGetStaticMenuLayout(
                page,
                out RectTransform panel,
                out RectTransform title,
                out RectTransform description,
                out RectTransform buttonRoot,
                out List<RectTransform> buttons))
        {
            Debug.LogWarning(
                $"{page.name}: menu hierarchy is incomplete. Rebuild its " +
                "editor preview before restoring the layout.",
                page);
            return;
        }

        ApplyStaticMenuDefaultLayout(
            panel,
            title,
            description,
            buttonRoot,
            buttons);
        Scene scene = page.gameObject.scene;
        if (scene.IsValid() && scene.isLoaded)
            EditorSceneManager.MarkSceneDirty(scene);
    }

    private static bool MigrateCodexPage(
        GameObject pageObject,
        bool characterPage)
    {
        Transform runtimeRoot = pageObject.transform.Find(
            RuntimeMenuPageBase.RuntimeRootObjectName);
        Transform panel = runtimeRoot != null
            ? runtimeRoot.Find("grpMenuPanel")
            : null;
        Transform buttonRoot = panel != null
            ? panel.Find("grpMenuButtons")
            : null;
        Transform browser = buttonRoot != null
            ? buttonRoot.Find("grpCodexBrowser")
            : null;
        if (runtimeRoot == null || panel == null || buttonRoot == null ||
            browser == null)
        {
            Debug.LogWarning(
                $"{pageObject.name}: generated codex hierarchy is missing.");
            return false;
        }

        CodexBrowserDesignerSettings settings =
            browser.GetComponent<CodexBrowserDesignerSettings>();
        if (settings != null && settings.HasDesignerLayout)
            return false;

        Undo.SetCurrentGroupName("Migrate Codex Designer UI");
        if (settings == null)
        {
            settings =
                Undo.AddComponent<CodexBrowserDesignerSettings>(
                    browser.gameObject);
        }

        DisableLayoutGroup(panel);
        DisableLayoutGroup(buttonRoot);
        DisableLayoutGroup(browser);

        SetStretch((RectTransform)runtimeRoot, 0f, 0f, 0f, 0f);
        SetStretch((RectTransform)panel, 0f, 0f, 0f, 0f);
        SetStretch((RectTransform)buttonRoot, 24f, 24f, 24f, 112f);
        SetStretch((RectTransform)browser, 0f, 0f, 0f, 0f);

        RectTransform title = panel.Find("txtPageTitle") as RectTransform;
        if (title != null)
            SetTopCentered(title, 18f, 780f, 58f);
        RectTransform description =
            panel.Find("txtPageDescription") as RectTransform;
        if (description != null)
            SetTopCentered(description, 74f, 900f, 34f);

        Transform list = browser.Find("grpCodexList");
        Transform toolbar = list != null
            ? list.Find("grpCodexListToolbar")
            : null;
        Transform scroll = list != null
            ? list.Find("scrCodexList")
            : null;
        Transform detailHost = browser.Find("grpCodexDetailHost");
        if (list == null || toolbar == null || scroll == null ||
            detailHost == null)
        {
            Debug.LogWarning(
                $"{pageObject.name}: codex browser controls are missing.");
            return false;
        }

        DisableLayoutGroup(list);
        DisableLayoutGroup(toolbar);
        DisableLayoutGroup(detailHost);
        SetLeftStretch((RectTransform)list, 720f);
        SetTopStretch((RectTransform)toolbar, 10f, 10f, 10f, 46f);
        SetStretch((RectTransform)scroll, 10f, 10f, 10f, 66f);
        SetStretch((RectTransform)detailHost, 738f, 0f, 0f, 0f);

        RectTransform search =
            toolbar.Find("inpCodexSearch") as RectTransform;
        RectTransform searchButton =
            toolbar.Find("btnCodexSearch") as RectTransform;
        RectTransform filterButton =
            toolbar.Find("btnCodexFilter") as RectTransform;
        RectTransform sortButton =
            toolbar.Find("btnCodexSort") as RectTransform;
        if (search != null)
            SetStretch(search, 0f, 0f, 274f, 0f);
        if (searchButton != null)
            SetRightStretch(searchButton, 204f, 64f);
        if (filterButton != null)
            SetRightStretch(filterButton, 106f, 92f);
        if (sortButton != null)
            SetRightStretch(sortButton, 0f, 100f);

        string detailName = characterPage
            ? "grpCharacterDetail"
            : pageObject.GetComponent<EnemyCodexPage>() != null
                ? "grpEnemyDetail"
                : "grpBattleCardDetail";
        Transform detail = detailHost.Find(detailName);
        if (detail != null)
            SetStretch((RectTransform)detail, 0f, 0f, 0f, 0f);

        if (characterPage && detail != null)
            MigrateCharacterDetail(detail);

        BindRuntimePageReferences(
            pageObject.GetComponent<RuntimeMenuPageBase>(),
            runtimeRoot as RectTransform,
            panel as RectTransform,
            buttonRoot as RectTransform);
        settings.CaptureReferencesFromHierarchy();
        settings.MarkDesignerLayoutCurrent();
        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(pageObject);
        return true;
    }

    private static void MigrateCharacterDetail(Transform detail)
    {
        DisableLayoutGroup(detail);
        Transform visuals = detail.Find("grpCharacterVisuals");
        Transform scroll = detail.Find("scrCharacterDetails");
        if (visuals != null)
        {
            DisableLayoutGroup(visuals);
            SetLeftStretch((RectTransform)visuals, 360f, 18f, 18f);
            RectTransform standing =
                visuals.Find("imgCharacterStanding") as RectTransform;
            if (standing != null)
                SetStretch(standing, 0f, 0f, 0f, 0f);
        }

        if (scroll != null)
            SetStretch((RectTransform)scroll, 396f, 18f, 18f, 18f);
    }

    private static void BindRuntimePageReferences(
        RuntimeMenuPageBase page,
        RectTransform runtimeRoot,
        RectTransform panel,
        RectTransform buttonRoot)
    {
        if (page == null)
            return;

        SerializedObject serialized = new(page);
        serialized.FindProperty("_runtimeRoot").objectReferenceValue =
            runtimeRoot;
        serialized.FindProperty("_panel").objectReferenceValue = panel;
        serialized.FindProperty("_buttonRoot").objectReferenceValue =
            buttonRoot;
        serialized.FindProperty("_titleText").objectReferenceValue =
            panel.Find("txtPageTitle")
                ?.GetComponent<TMPro.TextMeshProUGUI>();
        serialized.FindProperty("_descriptionText").objectReferenceValue =
            panel.Find("txtPageDescription")
                ?.GetComponent<TMPro.TextMeshProUGUI>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
        page.MarkDesignerLayoutCurrent();
        EditorUtility.SetDirty(page);
    }

    private static void BakeAndDisableMenuLayout(
        GameObject pageObject,
        Transform panel,
        Transform buttonRoot)
    {
        if (panel is not RectTransform panelRect ||
            buttonRoot is not RectTransform buttonRootRect)
        {
            return;
        }

        List<GameObject> temporarilyActivated = new();
        for (Transform current = pageObject.transform;
             current != null;
             current = current.parent)
        {
            if (current.gameObject.activeSelf)
                continue;

            current.gameObject.SetActive(true);
            temporarilyActivated.Add(current.gameObject);
        }

        try
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            LayoutRebuilder.ForceRebuildLayoutImmediate(buttonRootRect);

            List<RectTransformState> panelChildStates =
                CaptureChildStates(panel);
            List<RectTransformState> buttonStates =
                CaptureChildStates(buttonRoot);

            DisableLayoutGroup(buttonRoot);
            DisableLayoutGroup(panel);
            ApplyRectTransformStates(panelChildStates);
            ApplyRectTransformStates(buttonStates);
        }
        finally
        {
            for (int index = temporarilyActivated.Count - 1;
                 index >= 0;
                 index--)
            {
                temporarilyActivated[index].SetActive(false);
            }
        }
    }

    private static List<RectTransformState> CaptureChildStates(
        Transform target)
    {
        List<RectTransformState> states = new();
        if (target == null)
            return states;

        for (int index = 0; index < target.childCount; index++)
        {
            if (target.GetChild(index) is RectTransform child)
                states.Add(new RectTransformState(child));
        }

        return states;
    }

    private static void ApplyRectTransformStates(
        List<RectTransformState> states)
    {
        foreach (RectTransformState state in states)
            state.Apply();
    }

    private static void DisableLayoutGroup(Transform target)
    {
        if (target == null)
            return;

        LayoutGroup layout = target.GetComponent<LayoutGroup>();
        if (layout == null || !layout.enabled)
            return;

        Undo.RecordObject(layout, "Disable Generated Layout");
        layout.enabled = false;
        EditorUtility.SetDirty(layout);
    }

    private readonly struct RectTransformState
    {
        private readonly RectTransform _target;
        private readonly Vector2 _anchorMin;
        private readonly Vector2 _anchorMax;
        private readonly Vector2 _pivot;
        private readonly Vector2 _anchoredPosition;
        private readonly Vector2 _sizeDelta;

        public RectTransformState(RectTransform target)
        {
            _target = target;
            _anchorMin = target.anchorMin;
            _anchorMax = target.anchorMax;
            _pivot = target.pivot;
            _anchoredPosition = target.anchoredPosition;
            _sizeDelta = target.sizeDelta;
        }

        public void Apply()
        {
            if (_target == null)
                return;

            Undo.RecordObject(_target, "Bake Generated Layout");
            _target.anchorMin = _anchorMin;
            _target.anchorMax = _anchorMax;
            _target.pivot = _pivot;
            _target.anchoredPosition = _anchoredPosition;
            _target.sizeDelta = _sizeDelta;
            EditorUtility.SetDirty(_target);
        }
    }

    private static void SetStretch(
        RectTransform rect,
        float left,
        float bottom,
        float right,
        float top)
    {
        Undo.RecordObject(rect, "Set Designer Rect");
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetLeftStretch(
        RectTransform rect,
        float width,
        float left = 0f,
        float verticalMargin = 0f)
    {
        Undo.RecordObject(rect, "Set Designer Rect");
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(left, 0f);
        rect.sizeDelta = new Vector2(
            width,
            -verticalMargin * 2f);
    }

    private static void SetTopStretch(
        RectTransform rect,
        float left,
        float right,
        float top,
        float height)
    {
        Undo.RecordObject(rect, "Set Designer Rect");
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(-(left + right), height);
    }

    private static void SetTopCentered(
        RectTransform rect,
        float top,
        float width,
        float height)
    {
        Undo.RecordObject(rect, "Set Designer Rect");
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetCentered(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height,
        string undoName)
    {
        Undo.RecordObject(rect, undoName);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        EditorUtility.SetDirty(rect);
    }

    private static bool RepairCollapsedMainMenuLayout(MainPage page)
    {
        if (!TryGetMainMenuLayout(
                page,
                out RectTransform panel,
                out RectTransform title,
                out RectTransform description,
                out RectTransform buttonRoot,
                out List<RectTransform> buttons) ||
            !IsCollapsedMainMenuLayout(buttonRoot, buttons))
        {
            return false;
        }

        ApplyMainMenuDefaultLayout(
            panel,
            title,
            description,
            buttonRoot,
            buttons);
        ApplyMainUtilityLayout(page);
        return true;
    }

    private static bool RepairCollapsedStaticMenuLayout(
        RuntimeMenuPageBase page)
    {
        if (page is TitlePage titlePage)
            return RepairCollapsedTitleMenuLayout(titlePage);

        if (page is MainPage mainPage)
            return RepairCollapsedMainMenuLayout(mainPage);

        if (!TryGetStaticMenuLayout(
                page,
                out RectTransform panel,
                out RectTransform title,
                out RectTransform description,
                out RectTransform buttonRoot,
                out List<RectTransform> buttons) ||
            !IsCollapsedStaticMenuLayout(buttonRoot, buttons))
        {
            return false;
        }

        ApplyStaticMenuDefaultLayout(
            panel,
            title,
            description,
            buttonRoot,
            buttons);
        return true;
    }

    private static bool RepairCollapsedTitleMenuLayout(TitlePage page)
    {
        if (!TryGetTitleMenuLayout(
                page,
                out RectTransform panel,
                out RectTransform title,
                out RectTransform description,
                out RectTransform buttonRoot,
                out RectTransform startButton,
                out RectTransform noticeButton,
                out RectTransform settingsButton))
        {
            return false;
        }

        bool startIsFullScreen =
            startButton.anchorMin == Vector2.zero &&
            startButton.anchorMax == Vector2.one &&
            startButton.offsetMin.sqrMagnitude < 0.01f &&
            startButton.offsetMax.sqrMagnitude < 0.01f;
        if (startIsFullScreen)
            return false;

        ApplyTitleMenuDefaultLayout(
            panel,
            title,
            description,
            buttonRoot,
            startButton,
            noticeButton,
            settingsButton);
        return true;
    }

    private static bool TryGetTitleMenuLayout(
        TitlePage page,
        out RectTransform panel,
        out RectTransform title,
        out RectTransform description,
        out RectTransform buttonRoot,
        out RectTransform startButton,
        out RectTransform noticeButton,
        out RectTransform settingsButton)
    {
        panel = null;
        title = null;
        description = null;
        buttonRoot = null;
        startButton = null;
        noticeButton = null;
        settingsButton = null;
        if (page == null)
            return false;

        Transform runtimeRoot = page.transform.Find(
            RuntimeMenuPageBase.RuntimeRootObjectName);
        panel = runtimeRoot != null
            ? runtimeRoot.Find("grpMenuPanel") as RectTransform
            : null;
        title = panel != null
            ? panel.Find("txtPageTitle") as RectTransform
            : null;
        description = panel != null
            ? panel.Find("txtPageDescription") as RectTransform
            : null;
        buttonRoot = panel != null
            ? panel.Find("grpMenuButtons") as RectTransform
            : null;
        startButton = buttonRoot != null
            ? buttonRoot.Find("btnSTARTFullscreen") as RectTransform
            : null;
        noticeButton = runtimeRoot != null
            ? runtimeRoot.Find("btnNOTICEOverlay") as RectTransform
            : null;
        settingsButton = runtimeRoot != null
            ? runtimeRoot.Find("btnSETTINGSOverlay") as RectTransform
            : null;

        return panel != null && title != null &&
               description != null && buttonRoot != null &&
               startButton != null && noticeButton != null &&
               settingsButton != null;
    }

    private static void ApplyTitleMenuDefaultLayout(
        RectTransform panel,
        RectTransform title,
        RectTransform description,
        RectTransform buttonRoot,
        RectTransform startButton,
        RectTransform noticeButton,
        RectTransform settingsButton)
    {
        SetStretch(panel, 0f, 0f, 0f, 0f);
        SetCentered(
            title,
            0f,
            88f,
            900f,
            120f,
            "Restore Title Heading");
        SetCentered(
            description,
            0f,
            8f,
            900f,
            54f,
            "Restore Title Description");
        SetStretch(buttonRoot, 0f, 0f, 0f, 0f);
        SetStretch(startButton, 0f, 0f, 0f, 0f);

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            Undo.RecordObject(panelImage, "Restore Title Panel");
            panelImage.color = Color.clear;
            panelImage.raycastTarget = false;
            EditorUtility.SetDirty(panelImage);
        }

        Image startImage = startButton.GetComponent<Image>();
        Button start = startButton.GetComponent<Button>();
        if (startImage != null)
        {
            Undo.RecordObject(startImage, "Restore Title Start Area");
            startImage.color = Color.clear;
            startImage.raycastTarget = true;
            EditorUtility.SetDirty(startImage);
        }
        if (start != null)
        {
            Undo.RecordObject(start, "Restore Title Start Area");
            ColorBlock colors = start.colors;
            colors.normalColor = Color.clear;
            colors.highlightedColor =
                new Color(1f, 1f, 1f, 0.035f);
            colors.pressedColor =
                new Color(0f, 0f, 0f, 0.08f);
            colors.selectedColor = Color.clear;
            colors.disabledColor = Color.clear;
            start.colors = colors;
            EditorUtility.SetDirty(start);
        }

        RectTransform prompt = startButton.Find("txtLabel")
            as RectTransform;
        if (prompt != null)
        {
            Undo.RecordObject(prompt, "Restore Title Start Prompt");
            prompt.anchorMin = new Vector2(0.5f, 0f);
            prompt.anchorMax = new Vector2(0.5f, 0f);
            prompt.pivot = new Vector2(0.5f, 0f);
            prompt.anchoredPosition = new Vector2(0f, 64f);
            prompt.sizeDelta = new Vector2(760f, 52f);
            TextMeshProUGUI promptText =
                prompt.GetComponent<TextMeshProUGUI>();
            if (promptText != null)
            {
                promptText.fontSize = 22f;
                promptText.fontSizeMax = 22f;
                promptText.fontSizeMin = 15f;
                promptText.fontStyle = FontStyles.Normal;
            }
            EditorUtility.SetDirty(prompt);
        }

        SetTopCorner(
            noticeButton,
            false,
            48f,
            32f,
            220f,
            64f,
            "Restore Title Notice Button");
        SetTopCorner(
            settingsButton,
            true,
            48f,
            32f,
            80f,
            64f,
            "Restore Title Settings Button");

        DisableLayoutGroup(buttonRoot);
        DisableLayoutGroup(panel);
        Transform titleBackdrop = panel.parent.Find("imgTitleBackdrop");
        if (titleBackdrop != null)
        {
            titleBackdrop.SetAsFirstSibling();
            panel.SetSiblingIndex(1);
        }
        else
        {
            panel.SetAsFirstSibling();
        }
        noticeButton.SetAsLastSibling();
        settingsButton.SetAsLastSibling();
        Transform noticePopup = panel.parent.Find("grpNoticePopup");
        if (noticePopup != null)
            noticePopup.SetAsLastSibling();
    }

    private static void SetTopCorner(
        RectTransform rect,
        bool right,
        float horizontal,
        float top,
        float width,
        float height,
        string undoName)
    {
        Undo.RecordObject(rect, undoName);
        Vector2 anchor = new(right ? 1f : 0f, 1f);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = new Vector2(
            right ? -horizontal : horizontal,
            -top);
        rect.sizeDelta = new Vector2(width, height);
        EditorUtility.SetDirty(rect);
    }

    private static bool TryGetStaticMenuLayout(
        RuntimeMenuPageBase page,
        out RectTransform panel,
        out RectTransform title,
        out RectTransform description,
        out RectTransform buttonRoot,
        out List<RectTransform> buttons)
    {
        panel = null;
        title = null;
        description = null;
        buttonRoot = null;
        buttons = new List<RectTransform>();
        if (page == null)
            return false;

        Transform runtimeRoot = page.transform.Find(
            RuntimeMenuPageBase.RuntimeRootObjectName);
        panel = runtimeRoot != null
            ? runtimeRoot.Find("grpMenuPanel") as RectTransform
            : null;
        title = panel != null
            ? panel.Find("txtPageTitle") as RectTransform
            : null;
        description = panel != null
            ? panel.Find("txtPageDescription") as RectTransform
            : null;
        buttonRoot = panel != null
            ? panel.Find("grpMenuButtons") as RectTransform
            : null;
        if (panel == null || title == null || buttonRoot == null)
            return false;

        for (int index = 0; index < buttonRoot.childCount; index++)
        {
            if (buttonRoot.GetChild(index) is not RectTransform button ||
                !button.gameObject.activeSelf ||
                button.GetComponent<Button>() == null)
            {
                continue;
            }

            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout == null || !layout.ignoreLayout)
                buttons.Add(button);
        }

        return buttons.Count > 0;
    }

    private static bool IsCollapsedStaticMenuLayout(
        RectTransform buttonRoot,
        IReadOnlyList<RectTransform> buttons)
    {
        if (buttonRoot == null || buttons == null || buttons.Count == 0)
            return false;

        Vector2 firstPosition = buttons[0].anchoredPosition;
        for (int index = 1; index < buttons.Count; index++)
        {
            if (Vector2.Distance(
                    firstPosition,
                    buttons[index].anchoredPosition) > 0.5f)
            {
                return false;
            }
        }

        return buttonRoot.rect.width <= 120f ||
               buttonRoot.rect.height <= 120f;
    }

    private static void ApplyStaticMenuDefaultLayout(
        RectTransform panel,
        RectTransform title,
        RectTransform description,
        RectTransform buttonRoot,
        IReadOnlyList<RectTransform> buttons)
    {
        VerticalLayoutGroup panelLayout =
            panel.GetComponent<VerticalLayoutGroup>();
        int panelLeft = panelLayout != null
            ? panelLayout.padding.left
            : 40;
        int panelRight = panelLayout != null
            ? panelLayout.padding.right
            : 40;
        int panelTop = panelLayout != null
            ? panelLayout.padding.top
            : 40;
        int panelBottom = panelLayout != null
            ? panelLayout.padding.bottom
            : 40;
        float panelSpacing = panelLayout != null
            ? panelLayout.spacing
            : 20f;
        float panelWidth = GetRectSize(panel, true);
        float panelHeight = GetRectSize(panel, false);
        float contentWidth = Mathf.Max(
            0f,
            panelWidth - panelLeft - panelRight);
        float cursor = panelTop;

        float titleHeight = GetPreferredHeight(title, 82f);
        SetTopLeftAnchored(
            title,
            panelLeft + contentWidth * 0.5f,
            -(cursor + titleHeight * 0.5f),
            contentWidth,
            titleHeight,
            "Restore Menu Title");
        cursor += titleHeight;

        if (description != null && description.gameObject.activeSelf)
        {
            cursor += panelSpacing;
            float descriptionHeight =
                GetPreferredHeight(description, 64f);
            SetTopLeftAnchored(
                description,
                panelLeft + contentWidth * 0.5f,
                -(cursor + descriptionHeight * 0.5f),
                contentWidth,
                descriptionHeight,
                "Restore Menu Description");
            cursor += descriptionHeight;
        }

        cursor += panelSpacing;
        float buttonRootHeight = Mathf.Max(
            0f,
            panelHeight - panelBottom - cursor);
        SetTopLeftAnchored(
            buttonRoot,
            panelLeft + contentWidth * 0.5f,
            -(cursor + buttonRootHeight * 0.5f),
            contentWidth,
            buttonRootHeight,
            "Restore Menu Button Area");
        ApplyStaticMenuButtonLayout(buttonRoot, buttons);

        DisableLayoutGroup(buttonRoot);
        DisableLayoutGroup(panel);
    }

    private static void ApplyStaticMenuButtonLayout(
        RectTransform buttonRoot,
        IReadOnlyList<RectTransform> buttons)
    {
        VerticalLayoutGroup layout =
            buttonRoot.GetComponent<VerticalLayoutGroup>();
        int left = layout != null ? layout.padding.left : 0;
        int right = layout != null ? layout.padding.right : 0;
        int top = layout != null ? layout.padding.top : 0;
        int bottom = layout != null ? layout.padding.bottom : 0;
        float spacing = layout != null ? layout.spacing : 14f;
        float rootWidth = GetRectSize(buttonRoot, true);
        float rootHeight = GetRectSize(buttonRoot, false);
        float buttonWidth = Mathf.Max(0f, rootWidth - left - right);
        float usableHeight = Mathf.Max(0f, rootHeight - top - bottom);
        float totalSpacing = spacing * Mathf.Max(0, buttons.Count - 1);
        float totalPreferredHeight = 0f;
        float[] buttonHeights = new float[buttons.Count];
        for (int index = 0; index < buttons.Count; index++)
        {
            buttonHeights[index] =
                GetPreferredHeight(buttons[index], 72f);
            totalPreferredHeight += buttonHeights[index];
        }

        float availableButtonHeight = Mathf.Max(
            0f,
            usableHeight - totalSpacing);
        if (totalPreferredHeight > availableButtonHeight &&
            totalPreferredHeight > 0f)
        {
            float scale = availableButtonHeight /
                          totalPreferredHeight;
            for (int index = 0; index < buttonHeights.Length; index++)
                buttonHeights[index] *= scale;
            totalPreferredHeight = availableButtonHeight;
        }

        float occupiedHeight = totalPreferredHeight + totalSpacing;
        float alignment = GetVerticalAlignment(layout);
        float cursor = top +
                       Mathf.Max(0f, usableHeight - occupiedHeight) *
                       alignment;
        for (int index = 0; index < buttons.Count; index++)
        {
            float height = buttonHeights[index];
            SetTopLeftAnchored(
                buttons[index],
                left + buttonWidth * 0.5f,
                -(cursor + height * 0.5f),
                buttonWidth,
                height,
                "Restore Menu Button");
            cursor += height + spacing;
        }
    }

    private static float GetRectSize(RectTransform rect, bool horizontal)
    {
        float rectSize = horizontal ? rect.rect.width : rect.rect.height;
        float deltaSize = horizontal
            ? rect.sizeDelta.x
            : rect.sizeDelta.y;
        return rectSize > 0.01f ? rectSize : Mathf.Max(0f, deltaSize);
    }

    private static float GetPreferredHeight(
        RectTransform rect,
        float fallback)
    {
        LayoutElement layout = rect.GetComponent<LayoutElement>();
        if (layout != null && layout.preferredHeight >= 0f)
            return layout.preferredHeight;

        float height = GetRectSize(rect, false);
        return height > 0.01f ? height : fallback;
    }

    private static float GetVerticalAlignment(VerticalLayoutGroup layout)
    {
        if (layout == null)
            return 0.5f;

        int row = (int)layout.childAlignment / 3;
        return row switch
        {
            <= 0 => 0f,
            1 => 0.5f,
            _ => 1f,
        };
    }

    private static void SetTopLeftAnchored(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height,
        string undoName)
    {
        Undo.RecordObject(rect, undoName);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        EditorUtility.SetDirty(rect);
    }

    private static bool TryGetMainMenuLayout(
        MainPage page,
        out RectTransform panel,
        out RectTransform title,
        out RectTransform description,
        out RectTransform buttonRoot,
        out List<RectTransform> buttons)
    {
        panel = null;
        title = null;
        description = null;
        buttonRoot = null;
        buttons = new List<RectTransform>();
        if (page == null)
            return false;

        Transform runtimeRoot = page.transform.Find(
            RuntimeMenuPageBase.RuntimeRootObjectName);
        panel = runtimeRoot != null
            ? runtimeRoot.Find("grpMenuPanel") as RectTransform
            : null;
        title = panel != null
            ? panel.Find("txtPageTitle") as RectTransform
            : null;
        description = panel != null
            ? panel.Find("txtPageDescription") as RectTransform
            : null;
        buttonRoot = panel != null
            ? panel.Find("grpMenuButtons") as RectTransform
            : null;
        if (panel == null || title == null || description == null ||
            buttonRoot == null)
        {
            return false;
        }

        foreach (string buttonName in MainMenuButtonNames)
        {
            RectTransform button =
                buttonRoot.Find(buttonName) as RectTransform;
            if (button == null)
                return false;
            buttons.Add(button);
        }

        return true;
    }

    private static bool IsCollapsedMainMenuLayout(
        RectTransform buttonRoot,
        IReadOnlyList<RectTransform> buttons)
    {
        if (buttonRoot == null || buttons == null ||
            buttons.Count != MainMenuButtonNames.Length)
        {
            return false;
        }

        Vector2 firstPosition = buttons[0].anchoredPosition;
        for (int index = 1; index < buttons.Count; index++)
        {
            if (Vector2.Distance(
                    firstPosition,
                    buttons[index].anchoredPosition) > 0.5f)
            {
                return false;
            }
        }

        return buttonRoot.rect.width <= 120f ||
               buttonRoot.rect.height <= 120f;
    }

    private static void ApplyMainMenuDefaultLayout(
        RectTransform panel,
        RectTransform title,
        RectTransform description,
        RectTransform buttonRoot,
        IReadOnlyList<RectTransform> buttons)
    {
        SetRightMiddle(
            panel,
            48f,
            -20f,
            720f,
            638f,
            "Restore Main Menu Panel");
        SetCentered(
            title,
            0f,
            329f,
            540f,
            82f,
            "Restore Main Menu Title");
        SetCentered(
            description,
            0f,
            236f,
            540f,
            64f,
            "Restore Main Menu Description");
        SetCentered(
            buttonRoot,
            0f,
            0f,
            720f,
            638f,
            "Restore Main Menu Button Area");

        SetCentered(
            buttons[0],
            0f,
            214f,
            720f,
            210f,
            "Restore Main Operation Button");
        SetCentered(
            buttons[1],
            0f,
            28f,
            720f,
            130f,
            "Restore Main Operator Button");
        SetCentered(
            buttons[2],
            -184f,
            -118f,
            352f,
            130f,
            "Restore Main Shop Button");
        SetCentered(
            buttons[3],
            184f,
            -118f,
            352f,
            130f,
            "Restore Main Recruit Button");
        SetCentered(
            buttons[4],
            -96f,
            -259f,
            528f,
            120f,
            "Restore Main Base Button");
        SetCentered(
            buttons[5],
            272f,
            -259f,
            176f,
            120f,
            "Restore Main Storage Button");
        title.gameObject.SetActive(false);
        description.gameObject.SetActive(false);

        DisableLayoutGroup(buttonRoot);
        DisableLayoutGroup(panel);
    }

    private static void ApplyMainUtilityLayout(MainPage page)
    {
        Transform runtimeRoot = page != null
            ? page.transform.Find(RuntimeMenuPageBase.RuntimeRootObjectName)
            : null;
        RectTransform notice = runtimeRoot != null
            ? runtimeRoot.Find("btnNOTICEOverlay") as RectTransform
            : null;
        RectTransform attendance = runtimeRoot != null
            ? runtimeRoot.Find("btnATTENDANCEOverlay") as RectTransform
            : null;

        if (notice != null)
        {
            SetTopCorner(
                notice,
                false,
                48f,
                104f,
                160f,
                52f,
                "Restore Main Notice Button");
            notice.SetAsLastSibling();
        }

        if (attendance != null)
        {
            SetTopCorner(
                attendance,
                false,
                220f,
                104f,
                184f,
                52f,
                "Restore Main Attendance Button");
            attendance.SetAsLastSibling();
        }

        Transform noticePopup = runtimeRoot != null
            ? runtimeRoot.Find("grpMainNoticePopup")
            : null;
        Transform attendancePopup = runtimeRoot != null
            ? runtimeRoot.Find("grpAttendancePopup")
            : null;
        if (noticePopup != null)
            noticePopup.SetAsLastSibling();
        if (attendancePopup != null)
            attendancePopup.SetAsLastSibling();
    }

    private static void SetRightMiddle(
        RectTransform rect,
        float right,
        float y,
        float width,
        float height,
        string undoName)
    {
        Undo.RecordObject(rect, undoName);
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-right, y);
        rect.sizeDelta = new Vector2(width, height);
        EditorUtility.SetDirty(rect);
    }

    private static void SetRightStretch(
        RectTransform rect,
        float right,
        float width)
    {
        Undo.RecordObject(rect, "Set Designer Rect");
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-right, 0f);
        rect.sizeDelta = new Vector2(width, 0f);
    }

    private static List<string> CollectDesignerUiIssues(Scene scene)
    {
        List<string> issues = new();
        GameObject layClient = FindSceneObject(scene, "layClient");
        if (layClient == null)
        {
            issues.Add("layClient is missing.");
            return issues;
        }

        string[] staticPageNames =
        {
            "pagTitle",
            "pagMain",
            "pagStageSelect",
            "pagBase",
            "pagRoster",
            "pagShop",
            "pagRecruit",
            "pagStorage",
        };
        foreach (string pageName in staticPageNames)
        {
            RuntimeMenuPageBase page =
                FindDirectChild(layClient, pageName)
                    ?.GetComponent<RuntimeMenuPageBase>();
            if (page == null)
                issues.Add($"{pageName} is missing.");
            else if (!page.HasDesignerLayout)
                issues.Add($"{pageName} has not been migrated.");
            else if (TryGetStaticMenuLayout(
                         page,
                         out _,
                         out _,
                         out _,
                         out RectTransform buttonRoot,
                         out List<RectTransform> buttons) &&
                     IsCollapsedStaticMenuLayout(buttonRoot, buttons))
                issues.Add(
                    $"{pageName} menu buttons overlap at the same position.");
        }

        GameObject recruitObject =
            FindDirectChild(layClient, "pagRecruit");
        RecruitBannerDesignerBindings bannerBindings =
            recruitObject != null
                ? recruitObject.GetComponentInChildren<
                    RecruitBannerDesignerBindings>(true)
                : null;
        RecruitRevealDesignerBindings revealBindings =
            recruitObject != null
                ? recruitObject.GetComponentInChildren<
                    RecruitRevealDesignerBindings>(true)
                : null;
        if (bannerBindings == null ||
            !bannerBindings.HasDesignerLayout ||
            !bannerBindings.HasRequiredReferences)
        {
            issues.Add(
                "pagRecruit banner is not bound to designer-owned scene UI.");
        }
        if (revealBindings == null ||
            !revealBindings.HasDesignerLayout ||
            !revealBindings.HasRequiredReferences)
        {
            issues.Add(
                "pagRecruit reveal overlay is not bound to designer-owned " +
                "scene UI.");
        }

        string[] pageNames =
        {
            "pagEnemyCodex",
            "pagCharacterCodex",
            "pagSkillCodex",
            "pagItemCodex",
        };
        foreach (string pageName in pageNames)
        {
            GameObject page = FindDirectChild(layClient, pageName);
            Transform browser = page != null
                ? page.transform.Find(
                    RuntimeMenuPageBase.RuntimeRootObjectName +
                    "/grpMenuPanel/grpMenuButtons/grpCodexBrowser")
                : null;
            CodexBrowserDesignerSettings settings = browser != null
                ? browser.GetComponent<CodexBrowserDesignerSettings>()
                : null;
            if (page == null)
                issues.Add($"{pageName} is missing.");
            else if (settings == null || !settings.HasDesignerLayout)
                issues.Add($"{pageName} has not been migrated.");
        }

        return issues;
    }

    private static void BuildClientPages(bool forceRebuild)
    {
        if (LegacyBuilderDisabled)
        {
            Debug.LogError(
                "Legacy runtime menu rebuild is disabled. " +
                "Edit the scene UI or use the non-destructive migration.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            scene.path != ClientScenePath)
        {
            return;
        }

        CharacterDefinitionCatalog.Invalidate();

        GameObject layClient = FindSceneObject(scene, "layClient");
        if (layClient == null)
            return;

        GameObject titleObject = FindDirectChild(layClient, "pagTitle");
        GameObject mainObject = FindDirectChild(layClient, "pagMain");
        GameObject dungeonObject = FindDirectChild(layClient, "pagDungeon");
        GameObject stageSelectObject =
            FindDirectChild(layClient, "pagStageSelect");
        GameObject settingObject = FindDirectChild(layClient, "pagSetting");
        if (titleObject == null || mainObject == null ||
            dungeonObject == null || settingObject == null)
        {
            return;
        }

        GameObject codexObject =
            FindDirectChild(layClient, "pagBase") ??
            FindDirectChild(layClient, "pagCodex");
        GameObject rosterObject = FindDirectChild(layClient, "pagRoster");
        GameObject shopObject = FindDirectChild(layClient, "pagShop");
        GameObject questObject =
            FindDirectChild(layClient, "pagRecruit") ??
            FindDirectChild(layClient, "pagQuest");
        GameObject storageObject = FindDirectChild(layClient, "pagStorage");
        if (codexObject != null)
            codexObject.name = "pagBase";
        if (questObject != null)
            questObject.name = "pagRecruit";
        GameObject enemyCodexObject =
            FindDirectChild(layClient, "pagEnemyCodex");
        GameObject characterCodexObject =
            FindDirectChild(layClient, "pagCharacterCodex");
        GameObject skillCodexObject =
            FindDirectChild(layClient, "pagSkillCodex");
        GameObject itemCodexObject =
            FindDirectChild(layClient, "pagItemCodex");
        TitlePage titlePage = titleObject.GetComponent<TitlePage>();
        MainPage mainPage = mainObject.GetComponent<MainPage>();
        DungeonPage dungeonPage = dungeonObject.GetComponent<DungeonPage>();
        DungeonFieldView dungeonFieldView =
            dungeonObject.GetComponent<DungeonFieldView>();
        StageSelectPage stageSelectPage = stageSelectObject != null
            ? stageSelectObject.GetComponent<StageSelectPage>()
            : null;
        MainSubPage codexPage = codexObject != null
            ? codexObject.GetComponent<MainSubPage>()
            : null;
        MainSubPage rosterPage = rosterObject != null
            ? rosterObject.GetComponent<MainSubPage>()
            : null;
        MainSubPage shopPage = shopObject != null
            ? shopObject.GetComponent<MainSubPage>()
            : null;
        MainSubPage questPage = questObject != null
            ? questObject.GetComponent<MainSubPage>()
            : null;
        MainSubPage storagePage = storageObject != null
            ? storageObject.GetComponent<MainSubPage>()
            : null;
        EnemyCodexPage enemyCodexPage = enemyCodexObject != null
            ? enemyCodexObject.GetComponent<EnemyCodexPage>()
            : null;
        CharacterCodexPage characterCodexPage =
            characterCodexObject != null
                ? characterCodexObject.GetComponent<CharacterCodexPage>()
                : null;
        BattleCardCodexPage skillCodexPage = skillCodexObject != null
            ? skillCodexObject.GetComponent<BattleCardCodexPage>()
            : null;
        BattleCardCodexPage itemCodexPage = itemCodexObject != null
            ? itemCodexObject.GetComponent<BattleCardCodexPage>()
            : null;
        bool titleUiExists = HasGeneratedUi(titleObject);
        bool mainUiExists = HasGeneratedUi(mainObject);
        bool stageSelectUiExists = HasStageSelectUi(stageSelectObject);
        if (!forceRebuild && titlePage != null && mainPage != null &&
            dungeonPage != null &&
            HasValidDungeonCharacterDefinitions(dungeonPage) &&
            stageSelectPage != null &&
            HasObjectReference(mainPage, "stageSelectPage") &&
            HasObjectReference(stageSelectPage, "mainPage") &&
            HasObjectReference(stageSelectPage, "dungeonPage") &&
            HasObjectReference(dungeonPage, "mainPage") &&
            HasObjectReference(dungeonPage, "stageSelectPage") &&
            dungeonFieldView != null &&
            HasObjectReference(dungeonPage, "fieldView") &&
            HasObjectReference(codexPage, "enemyCodexPage") &&
            HasObjectReference(codexPage, "characterCodexPage") &&
            HasObjectReference(codexPage, "skillCodexPage") &&
            HasObjectReference(codexPage, "itemCodexPage") &&
            enemyCodexPage != null &&
            HasObjectReference(enemyCodexPage, "codexPage") &&
            HasObjectReference(enemyCodexPage, "dungeonPage") &&
            characterCodexPage != null &&
            HasObjectReference(characterCodexPage, "codexPage") &&
            HasObjectReference(characterCodexPage, "dungeonPage") &&
            HasAllCharacterDefinitions(characterCodexPage) &&
            skillCodexPage != null &&
            HasObjectReference(skillCodexPage, "codexPage") &&
            itemCodexPage != null &&
            HasObjectReference(itemCodexPage, "codexPage") &&
            codexPage != null && rosterPage != null && shopPage != null &&
            questPage != null && storagePage != null &&
            titleUiExists && mainUiExists && stageSelectUiExists &&
            HasGeneratedUi(codexObject) && HasGeneratedUi(rosterObject) &&
            HasGeneratedUi(shopObject) && HasGeneratedUi(questObject) &&
            HasGeneratedUi(storageObject) &&
            HasCurrentCodexBrowserUi(
                enemyCodexObject,
                "grpEnemyDetail") &&
            HasCurrentCharacterCodexUi(characterCodexObject) &&
            HasCurrentCodexBrowserUi(
                skillCodexObject,
                "grpBattleCardDetail") &&
            HasCurrentCodexBrowserUi(
                itemCodexObject,
                "grpBattleCardDetail"))
        {
            return;
        }

        const string undoName = "Build Main Menu Pages";
        Undo.SetCurrentGroupName(undoName);
        int undoGroup = Undo.GetCurrentGroup();

        stageSelectObject ??= CreatePageObject(
            layClient,
            "pagStageSelect",
            undoName);
        codexObject ??= CreatePageObject(layClient, "pagBase", undoName);
        rosterObject ??= CreatePageObject(layClient, "pagRoster", undoName);
        shopObject ??= CreatePageObject(layClient, "pagShop", undoName);
        questObject ??= CreatePageObject(layClient, "pagRecruit", undoName);
        storageObject ??= CreatePageObject(layClient, "pagStorage", undoName);
        enemyCodexObject ??= CreatePageObject(
            layClient,
            "pagEnemyCodex",
            undoName);
        characterCodexObject ??= CreatePageObject(
            layClient,
            "pagCharacterCodex",
            undoName);
        skillCodexObject ??= CreatePageObject(
            layClient,
            "pagSkillCodex",
            undoName);
        itemCodexObject ??= CreatePageObject(
            layClient,
            "pagItemCodex",
            undoName);

        titlePage ??= Undo.AddComponent<TitlePage>(titleObject);
        mainPage ??= Undo.AddComponent<MainPage>(mainObject);
        dungeonPage ??= Undo.AddComponent<DungeonPage>(dungeonObject);
        dungeonFieldView ??=
            Undo.AddComponent<DungeonFieldView>(dungeonObject);
        stageSelectPage ??=
            Undo.AddComponent<StageSelectPage>(stageSelectObject);
        codexPage ??= Undo.AddComponent<MainSubPage>(codexObject);
        rosterPage ??= Undo.AddComponent<MainSubPage>(rosterObject);
        shopPage ??= Undo.AddComponent<MainSubPage>(shopObject);
        questPage ??= Undo.AddComponent<MainSubPage>(questObject);
        storagePage ??= Undo.AddComponent<MainSubPage>(storageObject);
        enemyCodexPage ??=
            Undo.AddComponent<EnemyCodexPage>(enemyCodexObject);
        characterCodexPage ??=
            Undo.AddComponent<CharacterCodexPage>(characterCodexObject);
        skillCodexPage ??=
            Undo.AddComponent<BattleCardCodexPage>(skillCodexObject);
        itemCodexPage ??=
            Undo.AddComponent<BattleCardCodexPage>(itemCodexObject);

        ConfigureFullScreenRect(titleObject, undoName);
        ConfigureFullScreenRect(mainObject, undoName);
        ConfigureFullScreenRect(stageSelectObject, undoName);
        ConfigureFullScreenRect(codexObject, undoName);
        ConfigureFullScreenRect(rosterObject, undoName);
        ConfigureFullScreenRect(shopObject, undoName);
        ConfigureFullScreenRect(questObject, undoName);
        ConfigureFullScreenRect(storageObject, undoName);
        ConfigureFullScreenRect(enemyCodexObject, undoName);
        ConfigureFullScreenRect(characterCodexObject, undoName);
        ConfigureFullScreenRect(skillCodexObject, undoName);
        ConfigureFullScreenRect(itemCodexObject, undoName);

        SetObjectReference(titlePage, "mainPage", mainObject);
        SetObjectReference(titlePage, "settingPage", settingObject);
        SetObjectReference(mainPage, "stageSelectPage", stageSelectObject);
        SetObjectReference(mainPage, "basePage", codexObject);
        SetObjectReference(mainPage, "rosterPage", rosterObject);
        SetObjectReference(mainPage, "shopPage", shopObject);
        SetObjectReference(mainPage, "recruitPage", questObject);
        SetObjectReference(mainPage, "storagePage", storageObject);
        SetObjectReference(mainPage, "settingPage", settingObject);
        SetObjectReference(stageSelectPage, "mainPage", mainObject);
        SetObjectReference(stageSelectPage, "dungeonPage", dungeonObject);
        SetObjectReference(dungeonPage, "mainPage", mainObject);
        SetObjectReference(
            dungeonPage,
            "stageSelectPage",
            stageSelectObject);
        SetObjectReference(dungeonPage, "fieldView", dungeonFieldView);
        SetObjectReference(
            codexPage,
            "enemyCodexPage",
            enemyCodexObject);
        SetObjectReference(
            codexPage,
            "characterCodexPage",
            characterCodexObject);
        SetObjectReference(codexPage, "skillCodexPage", skillCodexObject);
        SetObjectReference(codexPage, "itemCodexPage", itemCodexObject);
        SetObjectReference(enemyCodexPage, "codexPage", codexObject);
        SetObjectReference(enemyCodexPage, "dungeonPage", dungeonObject);
        SetEnemyDefinitions(enemyCodexPage);
        SetObjectReference(characterCodexPage, "codexPage", codexObject);
        SetObjectReference(
            characterCodexPage,
            "dungeonPage",
            dungeonObject);
        SetCharacterDefinitions(characterCodexPage);
        SetDungeonCharacterDefinitions(dungeonPage);
        ConfigureBattleCardCodex(
            skillCodexPage,
            codexObject,
            EBattleCardCodexCategory.Skills);
        ConfigureBattleCardCodex(
            itemCodexPage,
            codexObject,
            EBattleCardCodexCategory.Items);

        ConfigureSubPage(
            codexPage,
            EMainSubPageType.Base,
            mainObject);
        ConfigureSubPage(
            rosterPage,
            EMainSubPageType.Roster,
            mainObject);
        ConfigureSubPage(
            shopPage,
            EMainSubPageType.Shop,
            mainObject);
        ConfigureSubPage(
            questPage,
            EMainSubPageType.Recruit,
            mainObject);
        ConfigureSubPage(
            storagePage,
            EMainSubPageType.Storage,
            mainObject);

        if (forceRebuild || !titleUiExists)
            titlePage.RebuildEditorPreview();
        if (forceRebuild || !mainUiExists)
            mainPage.RebuildEditorPreview();
        if (forceRebuild || !stageSelectUiExists)
            stageSelectPage.RebuildEditorPreview();
        if (forceRebuild || !HasGeneratedUi(codexObject))
            codexPage.RebuildEditorPreview();
        if (forceRebuild || !HasGeneratedUi(rosterObject))
            rosterPage.RebuildEditorPreview();
        if (forceRebuild || !HasGeneratedUi(shopObject))
            shopPage.RebuildEditorPreview();
        if (forceRebuild || !HasGeneratedUi(questObject))
            questPage.RebuildEditorPreview();
        if (forceRebuild || !HasGeneratedUi(storageObject))
            storagePage.RebuildEditorPreview();
        if (forceRebuild || !HasCurrentCodexBrowserUi(
                enemyCodexObject,
                "grpEnemyDetail"))
            enemyCodexPage.RebuildEditorPreview();
        if (forceRebuild || !HasCurrentCharacterCodexUi(characterCodexObject))
            characterCodexPage.RebuildEditorPreview();
        if (forceRebuild || !HasCurrentCodexBrowserUi(
                skillCodexObject,
                "grpBattleCardDetail"))
            skillCodexPage.RebuildEditorPreview();
        if (forceRebuild || !HasCurrentCodexBrowserUi(
                itemCodexObject,
                "grpBattleCardDetail"))
            itemCodexPage.RebuildEditorPreview();

        SetActive(titleObject, true, undoName);
        SetActive(mainObject, false, undoName);
        SetActive(stageSelectObject, false, undoName);
        SetActive(dungeonObject, false, undoName);
        SetActive(settingObject, false, undoName);
        SetActive(codexObject, false, undoName);
        SetActive(rosterObject, false, undoName);
        SetActive(shopObject, false, undoName);
        SetActive(questObject, false, undoName);
        SetActive(storageObject, false, undoName);
        SetActive(enemyCodexObject, false, undoName);
        SetActive(characterCodexObject, false, undoName);
        SetActive(skillCodexObject, false, undoName);
        SetActive(itemCodexObject, false, undoName);
        Undo.CollapseUndoOperations(undoGroup);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetEnemyDefinitions(EnemyCodexPage page)
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:EnemySO",
            new[] { "Assets" });
        System.Array.Sort(guids, System.StringComparer.Ordinal);
        List<EnemySO> definitions = new();
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            EnemySO definition =
                AssetDatabase.LoadAssetAtPath<EnemySO>(assetPath);
            if (definition != null)
                definitions.Add(definition);
        }

        SerializedObject serializedObject = new(page);
        SerializedProperty property =
            serializedObject.FindProperty("enemyDefinitions");
        if (property == null)
            return;

        property.arraySize = definitions.Count;
        for (int index = 0; index < definitions.Count; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue =
                definitions[index];
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(page);
    }

    private static void SetCharacterDefinitions(CharacterCodexPage page)
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:CharacterSO",
            new[] { "Assets" });
        System.Array.Sort(guids, System.StringComparer.Ordinal);
        List<CharacterSO> definitions = new();
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            CharacterSO definition =
                AssetDatabase.LoadAssetAtPath<CharacterSO>(assetPath);
            if (definition != null)
                definitions.Add(definition);
        }

        SerializedObject serializedObject = new(page);
        SerializedProperty property =
            serializedObject.FindProperty("characterDefinitions");
        if (property == null)
            return;

        property.arraySize = definitions.Count;
        for (int index = 0; index < definitions.Count; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue =
                definitions[index];
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(page);
    }

    private static bool HasAllCharacterDefinitions(CharacterCodexPage page)
    {
        if (page == null)
            return false;

        string[] guids = AssetDatabase.FindAssets(
            "t:CharacterSO",
            new[] { "Assets" });
        SerializedObject serializedObject = new(page);
        SerializedProperty property =
            serializedObject.FindProperty("characterDefinitions");
        if (property == null || property.arraySize != guids.Length)
            return false;

        HashSet<CharacterSO> assigned = new();
        for (int index = 0; index < property.arraySize; index++)
        {
            CharacterSO definition = property
                .GetArrayElementAtIndex(index)
                .objectReferenceValue as CharacterSO;
            if (definition == null || !assigned.Add(definition))
                return false;
        }

        foreach (string guid in guids)
        {
            CharacterSO definition = AssetDatabase.LoadAssetAtPath<CharacterSO>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (definition == null || !assigned.Contains(definition))
                return false;
        }

        return true;
    }

    private static void SetDungeonCharacterDefinitions(DungeonPage page)
    {
        if (page == null)
            return;

        IReadOnlyList<CharacterSO> definitions =
            CharacterDefinitionCatalog.GetAll();
        SerializedObject pageObject = new(page);
        SerializedProperty slots = pageObject.FindProperty("playerCharacters");
        if (slots == null)
            return;

        for (int index = 0; index < slots.arraySize; index++)
        {
            CharacterRuntime character = slots.GetArrayElementAtIndex(index)
                .objectReferenceValue as CharacterRuntime;
            if (character == null)
                continue;

            CharacterSO expected = index < definitions.Count
                ? definitions[index]
                : null;
            SerializedObject characterObject = new(character);
            SerializedProperty original =
                characterObject.FindProperty("original");
            if (original == null || original.objectReferenceValue == expected)
                continue;

            Undo.RecordObject(character, "Sync Dungeon Characters");
            original.objectReferenceValue = expected;
            characterObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(character);
        }
    }

    private static bool HasValidDungeonCharacterDefinitions(DungeonPage page)
    {
        if (page == null)
            return false;

        IReadOnlyList<CharacterSO> definitions =
            CharacterDefinitionCatalog.GetAll();
        if (definitions.Count == 0)
            return false;

        SerializedObject pageObject = new(page);
        SerializedProperty slots = pageObject.FindProperty("playerCharacters");
        if (slots == null)
            return false;

        for (int index = 0; index < slots.arraySize; index++)
        {
            CharacterRuntime character = slots.GetArrayElementAtIndex(index)
                .objectReferenceValue as CharacterRuntime;
            if (character == null)
                continue;

            CharacterSO expected = index < definitions.Count
                ? definitions[index]
                : null;
            SerializedObject characterObject = new(character);
            SerializedProperty original =
                characterObject.FindProperty("original");
            if (original == null || original.objectReferenceValue != expected)
                return false;
        }

        return true;
    }

    private static bool HasCurrentCharacterCodexUi(GameObject pageObject)
    {
        if (!HasCurrentCodexBrowserUi(
                pageObject,
                "grpCharacterDetail"))
            return false;

        Transform runtimeRoot = pageObject.transform.Find(
            RuntimeMenuPageBase.RuntimeRootObjectName);
        Transform panel = runtimeRoot != null
            ? runtimeRoot.Find("grpMenuPanel")
            : null;
        Transform buttonRoot = panel != null
            ? panel.Find("grpMenuButtons")
            : null;
        Transform detailHost = buttonRoot != null
            ? buttonRoot.Find(
                "grpCodexBrowser/grpCodexDetailHost")
            : null;
        Transform detail = detailHost != null
            ? detailHost.Find("grpCharacterDetail")
            : null;
        Transform visuals = detail != null
            ? detail.Find("grpCharacterVisuals")
            : null;
        Transform scroll = detail != null
            ? detail.Find("scrCharacterDetails")
            : null;
        Transform viewport = scroll != null
            ? scroll.Find("vptCharacterDetails")
            : null;
        Transform content = viewport != null
            ? viewport.Find("grpCharacterDetailContent")
            : null;
        Transform obsoleteIcon = visuals != null
            ? visuals.Find("imgCharacterIcon")
            : null;
        return visuals != null &&
               visuals.Find("imgCharacterStanding") != null &&
               (obsoleteIcon == null ||
                !obsoleteIcon.gameObject.activeSelf) &&
               content != null &&
               content.Find("txtPassive") != null &&
               content.Find("txtDungeonUpgrade") != null;
    }

    private static bool HasCurrentCodexBrowserUi(
        GameObject pageObject,
        string detailObjectName)
    {
        if (!HasGeneratedUi(pageObject))
            return false;

        Transform runtimeRoot = pageObject.transform.Find(
            RuntimeMenuPageBase.RuntimeRootObjectName);
        Transform buttonRoot = runtimeRoot != null
            ? runtimeRoot.Find("grpMenuPanel/grpMenuButtons")
            : null;
        Transform browser = buttonRoot != null
            ? buttonRoot.Find("grpCodexBrowser")
            : null;
        Transform list = browser != null
            ? browser.Find("grpCodexList")
            : null;
        Transform toolbar = list != null
            ? list.Find("grpCodexListToolbar")
            : null;
        Transform scroll = list != null
            ? list.Find("scrCodexList")
            : null;
        Transform detailHost = browser != null
            ? browser.Find("grpCodexDetailHost")
            : null;
        return toolbar != null &&
               toolbar.Find("inpCodexSearch") != null &&
               toolbar.Find("btnCodexSearch") != null &&
               toolbar.Find("btnCodexFilter") != null &&
               toolbar.Find("btnCodexSort") != null &&
               scroll != null &&
               detailHost != null &&
               runtimeRoot.Find("btnBACKTOCODEX") != null &&
               detailHost.Find(detailObjectName) != null;
    }

    private static GameObject CreatePageObject(
        GameObject parent,
        string objectName,
        string undoName)
    {
        GameObject pageObject = new(objectName, typeof(RectTransform));
        pageObject.layer = parent.layer;
        pageObject.transform.SetParent(parent.transform, false);
        Undo.RegisterCreatedObjectUndo(pageObject, undoName);
        return pageObject;
    }

    private static void ConfigureBattleCardCodex(
        BattleCardCodexPage page,
        GameObject codexObject,
        EBattleCardCodexCategory category)
    {
        SerializedObject serializedObject = new(page);
        SerializedProperty codexProperty =
            serializedObject.FindProperty("codexPage");
        SerializedProperty categoryProperty =
            serializedObject.FindProperty("category");
        if (codexProperty != null)
            codexProperty.objectReferenceValue = codexObject;
        if (categoryProperty != null)
            categoryProperty.enumValueIndex = (int)category;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(page);
    }

    private static void ConfigureSubPage(
        MainSubPage page,
        EMainSubPageType pageType,
        GameObject mainObject)
    {
        SerializedObject serializedObject = new(page);
        SerializedProperty typeProperty =
            serializedObject.FindProperty("pageType");
        SerializedProperty mainProperty =
            serializedObject.FindProperty("mainPage");
        if (typeProperty != null)
            typeProperty.enumValueIndex = (int)pageType;
        if (mainProperty != null)
            mainProperty.objectReferenceValue = mainObject;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(page);
    }

    private static bool HasGeneratedUi(GameObject pageObject)
    {
        return pageObject != null && pageObject.transform.Find(
            RuntimeMenuPageBase.RuntimeRootObjectName) != null;
    }

    private static bool HasStageSelectUi(GameObject pageObject)
    {
        if (!HasGeneratedUi(pageObject))
            return false;

        Transform buttonRoot = pageObject.transform.Find(
            RuntimeMenuPageBase.RuntimeRootObjectName +
            "/grpMenuPanel/grpMenuButtons");
        return buttonRoot != null &&
               buttonRoot.Find("btnSTAGE0TESTFIELD") != null &&
               buttonRoot.Find("btnFREEBATTLE") != null &&
               buttonRoot.Find("btnBACK") != null;
    }

    private static bool HasObjectReference(
        Object target,
        string propertyName)
    {
        if (target == null)
            return false;

        SerializedObject serializedObject = new(target);
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);
        return property != null && property.objectReferenceValue != null;
    }

    private static void ConfigureFullScreenRect(
        GameObject target,
        string undoName)
    {
        if (target == null ||
            target.transform is not RectTransform rectTransform)
        {
            return;
        }

        Undo.RecordObject(rectTransform, undoName);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        EditorUtility.SetDirty(rectTransform);
    }

    private static void SetObjectReference(
        Object target,
        string propertyName,
        Object value)
    {
        SerializedObject serializedObject = new(target);
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static void SetActive(
        GameObject target,
        bool active,
        string undoName)
    {
        if (target == null || target.activeSelf == active)
            return;

        Undo.RecordObject(target, undoName);
        target.SetActive(active);
        EditorUtility.SetDirty(target);
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindDescendant(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindDescendant(
        Transform root,
        string objectName)
    {
        if (root == null)
            return null;
        if (root.name == objectName)
            return root;

        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindDescendant(
                root.GetChild(index),
                objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject FindDirectChild(
        GameObject parent,
        string objectName)
    {
        if (parent == null)
            return null;

        Transform child = parent.transform.Find(objectName);
        return child != null ? child.gameObject : null;
    }
}
