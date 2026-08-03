using System;
using System.IO;
using PS260714.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MainLobbySceneUpdater
{
    private const string ClientScenePath =
        "Assets/Scenes/ClientScene.unity";
    private const string RequestPath =
        "Temp/ApplyMainLobbyLayout.request";
    private const string ApplyMenuPath =
        "PS260714/UI/Apply Main Lobby Layout";
    private const string CharacterSpritePath =
        "Assets/Sprite/Characters/SUIREN/3_Standing.png";

    private static readonly Color LobbyBackground =
        new(0.018f, 0.035f, 0.034f, 1f);
    private static readonly Color LobbyLeftField =
        new(0.04f, 0.10f, 0.095f, 1f);
    private static readonly Color LobbyFloor =
        new(0.025f, 0.055f, 0.052f, 1f);
    private static readonly Color OperationColor =
        new(0.055f, 0.38f, 0.43f, 0.98f);
    private static readonly Color OperatorColor =
        new(0.12f, 0.24f, 0.22f, 0.98f);
    private static readonly Color ShopColor =
        new(0.12f, 0.29f, 0.26f, 0.98f);
    private static readonly Color RecruitColor =
        new(0.36f, 0.28f, 0.11f, 0.98f);
    private static readonly Color BaseColor =
        new(0.10f, 0.22f, 0.19f, 0.98f);
    private static readonly Color StorageColor =
        new(0.14f, 0.17f, 0.16f, 0.98f);

    static MainLobbySceneUpdater()
    {
        EditorApplication.delayCall += ApplyRequestedLayout;
    }

    [MenuItem(ApplyMenuPath)]
    public static void ApplyMainLobbyLayout()
    {
        ApplyLayout(openAdditively: true);
    }

    public static void ApplyMainLobbyLayoutBatch()
    {
        if (ApplyLayout(openAdditively: false) &&
            File.Exists(RequestPath))
        {
            File.Delete(RequestPath);
        }
    }

    private static void ApplyRequestedLayout()
    {
        if (!File.Exists(RequestPath))
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ApplyRequestedLayout;
            return;
        }

        if (ApplyLayout(openAdditively: true))
            File.Delete(RequestPath);
    }

    private static bool ApplyLayout(bool openAdditively)
    {
        Scene scene = SceneManager.GetSceneByPath(ClientScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
        {
            scene = EditorSceneManager.OpenScene(
                ClientScenePath,
                openAdditively
                    ? OpenSceneMode.Additive
                    : OpenSceneMode.Single);
        }
        else if (scene.isDirty)
        {
            Debug.LogWarning(
                "Main lobby layout was not applied because ClientScene " +
                "contains unsaved editor changes. Save or discard those " +
                "changes, then run " + ApplyMenuPath + ".");
            return false;
        }

        try
        {
            GameObject layClient = FindSceneObject(scene, "layClient");
            GameObject titleObject =
                FindDirectChild(layClient, "pagTitle");
            GameObject mainObject =
                FindDirectChild(layClient, "pagMain");
            GameObject baseObject =
                FindDirectChild(layClient, "pagBase") ??
                FindDirectChild(layClient, "pagCodex");
            GameObject recruitObject =
                FindDirectChild(layClient, "pagRecruit") ??
                FindDirectChild(layClient, "pagQuest");
            GameObject rosterObject =
                FindDirectChild(layClient, "pagRoster");
            GameObject shopObject =
                FindDirectChild(layClient, "pagShop");
            GameObject storageObject =
                FindDirectChild(layClient, "pagStorage");
            GameObject stageSelectObject =
                FindDirectChild(layClient, "pagStageSelect");
            GameObject settingObject =
                FindDirectChild(layClient, "pagSetting");
            GameObject characterCodexObject =
                FindDirectChild(layClient, "pagCharacterCodex");

            if (layClient == null || titleObject == null ||
                mainObject == null ||
                baseObject == null || recruitObject == null ||
                rosterObject == null || shopObject == null ||
                storageObject == null || stageSelectObject == null ||
                settingObject == null ||
                characterCodexObject == null)
            {
                Debug.LogError(
                    "Main lobby layout requires the existing client pages.");
                return false;
            }

            baseObject.name = "pagBase";
            recruitObject.name = "pagRecruit";

            TitlePage titlePage =
                titleObject.GetComponent<TitlePage>();
            MainPage mainPage = mainObject.GetComponent<MainPage>();
            MainSubPage basePage = baseObject.GetComponent<MainSubPage>();
            MainSubPage rosterPage =
                rosterObject.GetComponent<MainSubPage>();
            MainSubPage recruitPage =
                recruitObject.GetComponent<MainSubPage>();
            if (titlePage == null || mainPage == null ||
                basePage == null ||
                rosterPage == null ||
                recruitPage == null)
            {
                Debug.LogError(
                    "Main lobby page components are missing.");
                return false;
            }

            ConfigureMainReferences(
                mainPage,
                stageSelectObject,
                baseObject,
                rosterObject,
                shopObject,
                recruitObject,
                storageObject,
                settingObject);
            ConfigureSubPage(basePage, EMainSubPageType.Base);
            ConfigureSubPage(rosterPage, EMainSubPageType.Roster);
            ConfigureSubPage(recruitPage, EMainSubPageType.Recruit);
            ConfigureRosterReferences(
                rosterPage,
                characterCodexObject);

            if (!titlePage.HasDesignerLayout)
            {
                titlePage.RebuildEditorPreview();
                titlePage.MarkDesignerLayoutCurrent();
            }
            ConfigureTitleLobby(titleObject, titlePage);
            if (!basePage.HasDesignerLayout)
            {
                basePage.RebuildEditorPreview();
                basePage.MarkDesignerLayoutCurrent();
            }
            if (!rosterPage.HasDesignerLayout)
            {
                rosterPage.RebuildEditorPreview();
                rosterPage.MarkDesignerLayoutCurrent();
            }
            if (!recruitPage.SyncRecruitEditorPreview(
                    0,
                    0,
                    out string recruitPreviewError))
            {
                Debug.LogError(
                    "Recruit designer preview synchronization failed: " +
                    recruitPreviewError,
                    recruitPage);
                return false;
            }

            ConfigureMainLobby(mainObject, mainPage);

            baseObject.SetActive(false);
            rosterObject.SetActive(false);
            recruitObject.SetActive(false);
            EditorUtility.SetDirty(baseObject);
            EditorUtility.SetDirty(rosterObject);
            EditorUtility.SetDirty(recruitObject);
            EditorUtility.SetDirty(titleObject);
            EditorUtility.SetDirty(mainObject);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Failed to save the main lobby scene.");
                return false;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Main lobby layout applied and saved.");
            return true;
        }
        finally
        {
            if (openedHere && openAdditively && scene.IsValid() &&
                scene.isLoaded && !scene.isDirty)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void ConfigureMainReferences(
        MainPage page,
        GameObject stageSelectPage,
        GameObject basePage,
        GameObject rosterPage,
        GameObject shopPage,
        GameObject recruitPage,
        GameObject storagePage,
        GameObject settingPage)
    {
        SerializedObject serialized = new(page);
        SetReference(serialized, "stageSelectPage", stageSelectPage);
        SetReference(serialized, "basePage", basePage);
        SetReference(serialized, "rosterPage", rosterPage);
        SetReference(serialized, "shopPage", shopPage);
        SetReference(serialized, "recruitPage", recruitPage);
        SetReference(serialized, "storagePage", storagePage);
        SetReference(serialized, "settingPage", settingPage);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(page);
    }

    private static void ConfigureSubPage(
        MainSubPage page,
        EMainSubPageType pageType)
    {
        SerializedObject serialized = new(page);
        SerializedProperty type = serialized.FindProperty("pageType");
        if (type != null)
            type.enumValueIndex = (int)pageType;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(page);
    }

    private static void ConfigureRosterReferences(
        MainSubPage page,
        GameObject characterCodexPage)
    {
        SerializedObject serialized = new(page);
        SetReference(
            serialized,
            "characterCodexPage",
            characterCodexPage);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(page);
    }

    private static void ConfigureTitleLobby(
        GameObject titleObject,
        TitlePage titlePage)
    {
        MenuPageSceneBuilder.RestoreTitleMenuDefaultLayout(titlePage);
        Transform root = titleObject.transform.Find(
            RuntimeMenuPageBase.RuntimeRootObjectName);
        Transform panel = root != null
            ? root.Find("grpMenuPanel")
            : null;
        if (root == null || panel == null)
            throw new InvalidOperationException(
                "The title page designer hierarchy is incomplete.");

        Image rootImage = root.GetComponent<Image>();
        rootImage.color = LobbyBackground;
        rootImage.raycastTarget = true;

        GameObject backdrop = GetOrCreateImage(
            root,
            "imgTitleBackdrop");
        Stretch((RectTransform)backdrop.transform);
        Image backdropImage = backdrop.GetComponent<Image>();
        backdropImage.color = new Color(0.025f, 0.075f, 0.07f, 1f);
        backdropImage.raycastTarget = false;
        backdrop.transform.SetAsFirstSibling();

        TextMeshProUGUI title = panel.Find("txtPageTitle")
            ?.GetComponent<TextMeshProUGUI>();
        if (title != null)
        {
            title.fontSize = 72f;
            title.fontSizeMax = 72f;
            title.fontSizeMin = 42f;
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.91f, 0.94f, 0.89f, 1f);
        }

        TextMeshProUGUI description =
            panel.Find("txtPageDescription")
                ?.GetComponent<TextMeshProUGUI>();
        if (description != null)
        {
            description.fontSize = 24f;
            description.fontSizeMax = 24f;
            description.fontSizeMin = 17f;
            description.color =
                new Color(0.42f, 0.76f, 0.69f, 1f);
        }

        ConfigureTitleCornerButton(
            root.Find("btnNOTICEOverlay"),
            new Color(0.08f, 0.19f, 0.17f, 0.98f),
            18f);
        ConfigureTitleCornerButton(
            root.Find("btnSETTINGSOverlay"),
            new Color(0.09f, 0.15f, 0.14f, 0.98f),
            17f);
        ConfigureTitleNoticePopup(root);
        MenuPageSceneBuilder.RestoreTitleMenuDefaultLayout(titlePage);
        titlePage.MarkDesignerLayoutCurrent();
    }

    private static void ConfigureTitleCornerButton(
        Transform buttonTransform,
        Color color,
        float fontSize)
    {
        if (buttonTransform == null)
            return;

        Image image = buttonTransform.GetComponent<Image>();
        Button button = buttonTransform.GetComponent<Button>();
        if (image != null)
        {
            image.color = color;
            image.raycastTarget = true;
        }
        if (button != null && image != null)
        {
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor =
                Color.Lerp(color, Color.white, 0.14f);
            colors.pressedColor =
                Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor =
                Color.Lerp(color, Color.black, 0.5f);
            button.colors = colors;
        }

        TextMeshProUGUI label = buttonTransform.Find("txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        if (label == null)
            return;

        label.fontSize = fontSize;
        label.fontSizeMax = fontSize;
        label.fontSizeMin = Mathf.Max(12f, fontSize - 5f);
        label.enableAutoSizing = true;
    }

    private static void ConfigureTitleNoticePopup(Transform root)
    {
        GameObject popup = GetOrCreateImage(root, "grpNoticePopup");
        Stretch((RectTransform)popup.transform);
        Image popupImage = popup.GetComponent<Image>();
        popupImage.color = new Color(0f, 0f, 0f, 0.72f);
        popupImage.raycastTarget = true;

        GameObject panel = GetOrCreateImage(
            popup.transform,
            "grpNoticePanel");
        ConfigureRect(
            (RectTransform)panel.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(680f, 320f));
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.045f, 0.09f, 0.08f, 1f);
        panelImage.raycastTarget = true;

        TextMeshProUGUI popupTitle = GetOrCreateText(
            panel.transform,
            "txtNoticeTitle",
            32f,
            TextAlignmentOptions.Center);
        ConfigureRect(
            popupTitle.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -36f),
            new Vector2(600f, 54f));
        ApplyLocalizedText(
            popupTitle,
            LocalizationKeys.UiTitleNotice);
        popupTitle.fontStyle = FontStyles.Bold;
        popupTitle.color = new Color(0.91f, 0.94f, 0.89f, 1f);

        TextMeshProUGUI message = GetOrCreateText(
            panel.transform,
            "txtNoticeMessage",
            21f,
            TextAlignmentOptions.Center);
        ConfigureRect(
            message.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 12f),
            new Vector2(600f, 112f));
        ApplyLocalizedText(
            message,
            LocalizationKeys.UiTitleNoticeEmpty);
        message.color = new Color(0.75f, 0.82f, 0.78f, 1f);

        GameObject closeObject = GetOrCreateImage(
            panel.transform,
            "btnNOTICECLOSE");
        ConfigureRect(
            (RectTransform)closeObject.transform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 32f),
            new Vector2(180f, 60f));
        Image closeImage = closeObject.GetComponent<Image>();
        closeImage.color = OperatorColor;
        closeImage.raycastTarget = true;
        Button closeButton = closeObject.GetComponent<Button>();
        if (closeButton == null)
            closeButton = closeObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        ColorBlock closeColors = closeButton.colors;
        closeColors.normalColor = OperatorColor;
        closeColors.highlightedColor =
            Color.Lerp(OperatorColor, Color.white, 0.14f);
        closeColors.pressedColor =
            Color.Lerp(OperatorColor, Color.black, 0.18f);
        closeColors.selectedColor = closeColors.highlightedColor;
        closeColors.disabledColor =
            Color.Lerp(OperatorColor, Color.black, 0.5f);
        closeButton.colors = closeColors;

        TextMeshProUGUI closeLabel = GetOrCreateText(
            closeObject.transform,
            "txtLabel",
            20f,
            TextAlignmentOptions.Center);
        ConfigureStretchText(
            closeLabel.rectTransform,
            new Vector2(12f, 4f),
            new Vector2(-12f, -4f));
        ApplyLocalizedText(
            closeLabel,
            LocalizationKeys.UiCommonOk);
        closeLabel.fontStyle = FontStyles.Bold;
        closeLabel.color = Color.white;

        popup.transform.SetAsLastSibling();
        popup.SetActive(false);
    }

    private static void ConfigureMainLobby(
        GameObject mainObject,
        MainPage mainPage)
    {
        Transform root = mainObject.transform.Find(
            RuntimeMenuPageBase.RuntimeRootObjectName);
        Transform panel = root != null
            ? root.Find("grpMenuPanel")
            : null;
        Transform buttonRoot = panel != null
            ? panel.Find("grpMenuButtons")
            : null;
        if (root == null || panel == null || buttonRoot == null)
            throw new InvalidOperationException(
                "The main page designer hierarchy is incomplete.");

        RenameChild(buttonRoot, "btnCODEX", "btnBASE");
        RenameChild(buttonRoot, "btnQUEST", "btnRECRUIT");
        EnsureMainUtilityButton(
            root,
            "btnNOTICEOverlay",
            LocalizationKeys.UiTitleNotice);
        EnsureMainUtilityButton(
            root,
            "btnATTENDANCEOverlay",
            LocalizationKeys.UiMainAttendance);
        MenuPageSceneBuilder.RestoreMainMenuDefaultLayout(mainPage);

        Image rootImage = root.GetComponent<Image>();
        rootImage.color = LobbyBackground;
        rootImage.raycastTarget = true;

        GameObject backdrop = GetOrCreateImage(
            root,
            "imgLobbyBackdrop");
        Stretch((RectTransform)backdrop.transform);
        backdrop.GetComponent<Image>().color = LobbyLeftField;
        backdrop.GetComponent<Image>().raycastTarget = false;

        GameObject floor = GetOrCreateImage(root, "imgLobbyFloor");
        RectTransform floorRect = (RectTransform)floor.transform;
        floorRect.anchorMin = Vector2.zero;
        floorRect.anchorMax = new Vector2(0.64f, 0.2f);
        floorRect.offsetMin = Vector2.zero;
        floorRect.offsetMax = Vector2.zero;
        floor.GetComponent<Image>().color = LobbyFloor;
        floor.GetComponent<Image>().raycastTarget = false;

        GameObject lightBand = GetOrCreateImage(
            root,
            "imgLobbyLightBand");
        RectTransform bandRect = (RectTransform)lightBand.transform;
        ConfigureRect(
            bandRect,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(270f, 20f),
            new Vector2(760f, 150f));
        bandRect.localRotation = Quaternion.Euler(0f, 0f, -8f);
        Image bandImage = lightBand.GetComponent<Image>();
        bandImage.color = new Color(0.12f, 0.36f, 0.32f, 0.16f);
        bandImage.raycastTarget = false;

        GameObject character = GetOrCreateImage(
            root,
            "imgLobbyCharacter");
        RectTransform characterRect =
            (RectTransform)character.transform;
        ConfigureRect(
            characterRect,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            new Vector2(120f, 0f),
            new Vector2(720f, 1440f));
        Image characterImage = character.GetComponent<Image>();
        characterImage.sprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(CharacterSpritePath);
        characterImage.color = Color.white;
        characterImage.preserveAspect = true;
        characterImage.raycastTarget = false;

        ConfigureIdentityPlate(root);
        ConfigureBrand(root);
        ConfigureCurrencyBar(root);
        ConfigureMenuPanel(panel, buttonRoot);
        ConfigureMainUtilityButtons(root);
        ConfigureSettingsButton(root);

        backdrop.transform.SetSiblingIndex(0);
        floor.transform.SetSiblingIndex(1);
        lightBand.transform.SetSiblingIndex(2);
        character.transform.SetSiblingIndex(3);
        panel.SetSiblingIndex(4);
        Transform identity = root.Find("grpLobbyIdentity");
        if (identity != null)
            identity.SetSiblingIndex(5);
        Transform brand = root.Find("txtLobbyBrand");
        if (brand != null)
            brand.SetSiblingIndex(6);
        Transform currency = root.Find("grpCurrencyBar");
        if (currency != null)
            currency.SetSiblingIndex(7);
        Transform notice = root.Find("btnNOTICEOverlay");
        if (notice != null)
            notice.SetAsLastSibling();
        Transform attendance = root.Find("btnATTENDANCEOverlay");
        if (attendance != null)
            attendance.SetAsLastSibling();
        Transform settings = root.Find("btnSETTINGSOverlay");
        if (settings != null)
            settings.SetAsLastSibling();

        mainPage.MarkDesignerLayoutCurrent();
    }

    private static void ConfigureMenuPanel(
        Transform panel,
        Transform buttonRoot)
    {
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = Color.clear;
        panelImage.raycastTarget = false;
        DisableLayout(panel);
        DisableLayout(buttonRoot);

        ConfigureLobbyButton(
            buttonRoot.Find("btnPLAY"),
            new Vector2(0f, 214f),
            new Vector2(720f, 210f),
            OperationColor,
            42f,
            true);
        ConfigureLobbyButton(
            buttonRoot.Find("btnROSTER"),
            new Vector2(0f, 28f),
            new Vector2(720f, 130f),
            OperatorColor,
            30f,
            true);
        ConfigureLobbyButton(
            buttonRoot.Find("btnSHOP"),
            new Vector2(-184f, -118f),
            new Vector2(352f, 130f),
            ShopColor,
            28f,
            false);
        ConfigureLobbyButton(
            buttonRoot.Find("btnRECRUIT"),
            new Vector2(184f, -118f),
            new Vector2(352f, 130f),
            RecruitColor,
            28f,
            false);
        ConfigureLobbyButton(
            buttonRoot.Find("btnBASE"),
            new Vector2(-96f, -259f),
            new Vector2(528f, 120f),
            BaseColor,
            28f,
            true);
        ConfigureLobbyButton(
            buttonRoot.Find("btnSTORAGE"),
            new Vector2(272f, -259f),
            new Vector2(176f, 120f),
            StorageColor,
            24f,
            false);

        Transform play = buttonRoot.Find("btnPLAY");
        if (play != null)
        {
            TextMeshProUGUI subtitle = GetOrCreateText(
                play,
                "txtSubtitle",
                18f,
                TextAlignmentOptions.BottomLeft);
            ConfigureStretchText(
                subtitle.rectTransform,
                new Vector2(30f, 20f),
                new Vector2(-30f, -82f));
            ApplyLocalizedText(
                subtitle,
                LocalizationKeys.UiStageSelectDescription);
            subtitle.color = new Color(0.82f, 0.94f, 0.92f, 0.9f);
        }
    }

    private static void ConfigureLobbyButton(
        Transform buttonTransform,
        Vector2 position,
        Vector2 size,
        Color color,
        float fontSize,
        bool leftAligned)
    {
        if (buttonTransform == null)
            return;

        RectTransform rect = (RectTransform)buttonTransform;
        ConfigureRect(
            rect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            position,
            size);

        Image image = buttonTransform.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        Button button = buttonTransform.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor =
            Color.Lerp(color, Color.white, 0.13f);
        colors.pressedColor =
            Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor =
            Color.Lerp(color, Color.black, 0.5f);
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        TextMeshProUGUI label = buttonTransform.Find("txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        if (label != null)
        {
            label.fontSize = fontSize;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = Mathf.Max(16f, fontSize - 10f);
            label.enableAutoSizing = true;
            label.alignment = leftAligned
                ? TextAlignmentOptions.MidlineLeft
                : TextAlignmentOptions.Center;
            ConfigureStretchText(
                label.rectTransform,
                leftAligned
                    ? new Vector2(30f, 10f)
                    : new Vector2(16f, 8f),
                leftAligned
                    ? new Vector2(-30f, -10f)
                    : new Vector2(-16f, -8f));
        }

        GameObject accent = GetOrCreateImage(
            buttonTransform,
            "imgLobbyTileAccent");
        RectTransform accentRect = (RectTransform)accent.transform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(6f, 0f);
        Image accentImage = accent.GetComponent<Image>();
        accentImage.color = new Color(0.68f, 0.95f, 0.88f, 0.9f);
        accentImage.raycastTarget = false;
        accent.transform.SetAsFirstSibling();
    }

    private static void ConfigureIdentityPlate(Transform root)
    {
        GameObject plate = GetOrCreateImage(root, "grpLobbyIdentity");
        RectTransform plateRect = (RectTransform)plate.transform;
        ConfigureRect(
            plateRect,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            new Vector2(48f, 36f),
            new Vector2(420f, 88f));
        Image plateImage = plate.GetComponent<Image>();
        plateImage.color = new Color(0.02f, 0.035f, 0.034f, 0.92f);
        plateImage.raycastTarget = false;

        TextMeshProUGUI name = GetOrCreateText(
            plate.transform,
            "txtLobbyCharacterName",
            28f,
            TextAlignmentOptions.MidlineLeft);
        ConfigureStretchText(
            name.rectTransform,
            new Vector2(24f, 30f),
            new Vector2(-20f, -8f));
        name.text = "SUIREN";
        name.fontStyle = FontStyles.Bold;
        name.color = new Color(0.91f, 0.94f, 0.89f, 1f);

        TextMeshProUGUI caption = GetOrCreateText(
            plate.transform,
            "txtLobbyCharacterCaption",
            14f,
            TextAlignmentOptions.MidlineLeft);
        ConfigureStretchText(
            caption.rectTransform,
            new Vector2(24f, 8f),
            new Vector2(-20f, -48f));
        caption.text = "OPERATOR // 03";
        caption.color = new Color(0.42f, 0.76f, 0.69f, 1f);
    }

    private static void ConfigureBrand(Transform root)
    {
        TextMeshProUGUI brand = GetOrCreateText(
            root,
            "txtLobbyBrand",
            24f,
            TextAlignmentOptions.TopLeft);
        RectTransform rect = brand.rectTransform;
        ConfigureRect(
            rect,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(48f, -32f),
            new Vector2(420f, 64f));
        brand.text = "PS260714  //  LOBBY";
        brand.fontStyle = FontStyles.Bold;
        brand.color = new Color(0.78f, 0.88f, 0.82f, 0.9f);
    }

    private static void ConfigureCurrencyBar(Transform root)
    {
        Transform placeholder = root.Find("txtCurrencyPlaceholder");
        if (placeholder != null)
            UnityEngine.Object.DestroyImmediate(placeholder.gameObject);

        GameObject bar = GetOrCreateRect(root, "grpCurrencyBar");
        RectTransform barRect = (RectTransform)bar.transform;
        ConfigureRect(
            barRect,
            Vector2.one,
            Vector2.one,
            Vector2.one,
            new Vector2(-144f, -32f),
            new Vector2(624f, 64f));

        ConfigureCurrencySlot(
            bar.transform,
            "grpSoftCredit",
            0f,
            LocalizationKeys.UiCurrencySoft,
            new Color(0.76f, 0.67f, 0.38f, 1f));
        ConfigureCurrencySlot(
            bar.transform,
            "grpFreeCredit",
            212f,
            LocalizationKeys.UiCurrencyFree,
            new Color(0.31f, 0.74f, 0.91f, 1f));
        ConfigureCurrencySlot(
            bar.transform,
            "grpPaidCredit",
            424f,
            LocalizationKeys.UiCurrencyPaid,
            new Color(0.89f, 0.67f, 0.28f, 1f));
    }

    private static void ConfigureCurrencySlot(
        Transform parent,
        string objectName,
        float x,
        string localizationKey,
        Color accentColor)
    {
        GameObject slot = GetOrCreateImage(parent, objectName);
        RectTransform slotRect = (RectTransform)slot.transform;
        ConfigureRect(
            slotRect,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(x, 0f),
            new Vector2(200f, 64f));
        Image slotImage = slot.GetComponent<Image>();
        slotImage.color = new Color(0.035f, 0.065f, 0.06f, 0.96f);
        slotImage.raycastTarget = false;

        GameObject icon = GetOrCreateImage(slot.transform, "imgCurrencyIcon");
        RectTransform iconRect = (RectTransform)icon.transform;
        ConfigureRect(
            iconRect,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(16f, 0f),
            new Vector2(34f, 34f));
        Image iconImage = icon.GetComponent<Image>();
        iconImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Sprite/UI/ToggleSlider/Circle_64_64.png");
        iconImage.color = accentColor;
        iconImage.raycastTarget = false;

        TextMeshProUGUI label = GetOrCreateText(
            slot.transform,
            "txtCurrencyName",
            14f,
            TextAlignmentOptions.TopLeft);
        ConfigureRect(
            label.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(58f, -8f),
            new Vector2(130f, 24f));
        ApplyLocalizedText(label, localizationKey);
        label.color = new Color(0.7f, 0.76f, 0.72f, 1f);

        TextMeshProUGUI value = GetOrCreateText(
            slot.transform,
            "txtCurrencyValue",
            22f,
            TextAlignmentOptions.BottomRight);
        ConfigureRect(
            value.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(58f, 7f),
            new Vector2(130f, 30f));
        value.text = "—";
        value.fontStyle = FontStyles.Bold;
        value.color = Color.white;
    }

    private static void ConfigureSettingsButton(Transform root)
    {
        Transform settings = root.Find("btnSETTINGSOverlay");
        if (settings == null)
            return;

        RectTransform rect = (RectTransform)settings;
        ConfigureRect(
            rect,
            Vector2.one,
            Vector2.one,
            Vector2.one,
            new Vector2(-48f, -32f),
            new Vector2(80f, 64f));
        Image image = settings.GetComponent<Image>();
        image.color = new Color(0.09f, 0.15f, 0.14f, 0.98f);
        TextMeshProUGUI label = settings.Find("txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        if (label != null)
        {
            label.fontSize = 17f;
            label.fontSizeMax = 17f;
            label.fontSizeMin = 13f;
            ConfigureStretchText(
                label.rectTransform,
                new Vector2(6f, 4f),
                new Vector2(-6f, -4f));
        }
    }

    private static void EnsureMainUtilityButton(
        Transform root,
        string objectName,
        string localizationKey)
    {
        GameObject buttonObject = GetOrCreateRect(root, objectName);
        Image image = buttonObject.GetComponent<Image>();
        if (image == null)
            image = buttonObject.AddComponent<Image>();
        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
            button = buttonObject.AddComponent<Button>();
        image.raycastTarget = true;
        button.targetGraphic = image;

        TextMeshProUGUI label = GetOrCreateText(
            buttonObject.transform,
            "txtLabel",
            17f,
            TextAlignmentOptions.Center);
        ConfigureStretchText(
            label.rectTransform,
            new Vector2(10f, 4f),
            new Vector2(-10f, -4f));
        ApplyLocalizedText(label, localizationKey);
        label.fontStyle = FontStyles.Bold;
    }

    private static void ConfigureMainUtilityButtons(Transform root)
    {
        ConfigureMainUtilityButton(
            root.Find("btnNOTICEOverlay"),
            48f,
            160f,
            new Color(0.08f, 0.19f, 0.17f, 0.98f));
        Transform attendance = root.Find("btnATTENDANCEOverlay");
        ConfigureMainUtilityButton(
            attendance,
            220f,
            184f,
            new Color(0.14f, 0.25f, 0.18f, 0.98f));

        if (attendance == null)
            return;

        GameObject badge = GetOrCreateImage(
            attendance,
            "imgAttendanceAvailable");
        RectTransform badgeRect = (RectTransform)badge.transform;
        ConfigureRect(
            badgeRect,
            Vector2.one,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            new Vector2(-4f, -4f),
            new Vector2(18f, 18f));
        Image badgeImage = badge.GetComponent<Image>();
        badgeImage.color = new Color(0.95f, 0.3f, 0.2f, 1f);
        badgeImage.raycastTarget = false;
        badge.SetActive(false);
    }

    private static void ConfigureMainUtilityButton(
        Transform buttonTransform,
        float left,
        float width,
        Color color)
    {
        if (buttonTransform == null)
            return;

        RectTransform rect = (RectTransform)buttonTransform;
        ConfigureRect(
            rect,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(left, -104f),
            new Vector2(width, 52f));
        Image image = buttonTransform.GetComponent<Image>();
        if (image != null)
        {
            image.color = color;
            image.raycastTarget = true;
        }

        Button button = buttonTransform.GetComponent<Button>();
        if (button != null && image != null)
        {
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor =
                Color.Lerp(color, Color.white, 0.14f);
            colors.pressedColor =
                Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor =
                Color.Lerp(color, Color.black, 0.5f);
            button.colors = colors;
        }

        TextMeshProUGUI label = buttonTransform.Find("txtLabel")
            ?.GetComponent<TextMeshProUGUI>();
        if (label != null)
        {
            label.fontSize = 17f;
            label.fontSizeMax = 17f;
            label.fontSizeMin = 13f;
            ConfigureStretchText(
                label.rectTransform,
                new Vector2(8f, 4f),
                new Vector2(-8f, -4f));
        }
    }

    private static void ApplyLocalizedText(
        TextMeshProUGUI text,
        string localizationKey)
    {
        LocalizedText localized =
            text.GetComponent<LocalizedText>();
        if (localized == null)
            localized = text.gameObject.AddComponent<LocalizedText>();
        localized.SetKey(localizationKey);
        EditorUtility.SetDirty(localized);
    }

    private static TextMeshProUGUI GetOrCreateText(
        Transform parent,
        string objectName,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = GetOrCreateRect(parent, objectName);
        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = textObject.AddComponent<TextMeshProUGUI>();
        LocalizationFontResolver.ApplyGameDefault(text);
        text.fontSize = fontSize;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = Mathf.Max(11f, fontSize - 6f);
        text.enableAutoSizing = true;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject GetOrCreateImage(
        Transform parent,
        string objectName)
    {
        GameObject child = GetOrCreateRect(parent, objectName);
        if (child.GetComponent<CanvasRenderer>() == null)
            child.AddComponent<CanvasRenderer>();
        if (child.GetComponent<Image>() == null)
            child.AddComponent<Image>();
        return child;
    }

    private static GameObject GetOrCreateRect(
        Transform parent,
        string objectName)
    {
        Transform existing = parent != null
            ? parent.Find(objectName)
            : null;
        if (existing != null)
            return existing.gameObject;

        GameObject child = new(objectName, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void RenameChild(
        Transform parent,
        string oldName,
        string newName)
    {
        Transform current = parent.Find(newName);
        if (current != null)
            return;
        Transform legacy = parent.Find(oldName);
        if (legacy != null)
            legacy.name = newName;
    }

    private static void SetReference(
        SerializedObject serialized,
        string propertyName,
        GameObject value)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void DisableLayout(Transform transform)
    {
        LayoutGroup layout = transform.GetComponent<LayoutGroup>();
        if (layout != null)
            layout.enabled = false;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void ConfigureStretchText(
        RectTransform rect,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void ConfigureRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static GameObject FindSceneObject(
        Scene scene,
        string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindRecursive(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindRecursive(
        Transform current,
        string objectName)
    {
        if (current.name == objectName)
            return current;

        for (int index = 0; index < current.childCount; index++)
        {
            Transform found = FindRecursive(
                current.GetChild(index),
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
