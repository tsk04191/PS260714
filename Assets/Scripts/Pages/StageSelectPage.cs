using System;
using System.Collections.Generic;
using PS260714.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StageSelectPage : RuntimeMenuPageBase
{
    private const string ScrollObjectName = "scrStageTrack";
    private const string ViewportObjectName = "vptStageTrack";
    private const string ContentObjectName = "grpStageContent";
    private const string StageNodePrefix = "btnStage_";
    private const string ConnectorPrefix = "imgStageConnector_";
    private const string TitleBannerObjectName = "grpStageTitleBanner";
    private const string SequenceTextObjectName = "txtStageSequence";
    private const string ProgressLineObjectName = "imgStageProgressLine";
    private const string MarkerGlyphObjectName = "txtStageMarkerGlyph";
    private const int ReferenceLayoutVersion = 2;
    private const int SquareBannerLayoutVersion = 1;
    private const float StageNodeWidth = 340f;
    private const float StageNodeHeight = 540f;
    private const float CoverWidth = 320f;
    private const float CoverHeight = CoverWidth;
    private const float CoverTop = 86f;
    private const float TitleBannerWidth = 304f;
    private const float TitleBannerHeight = 88f;
    private const float TitleBannerTop = 58f;
    private const float MarkerTop = 36f;
    private const float MarkerSize = 58f;
    private const float ConnectorWidth = 64f;

    private static readonly Color CoverPlaceholderColor =
        new(0.12f, 0.17f, 0.145f, 1f);
    private static readonly Color ClearedMarkerColor =
        new(0.34f, 0.88f, 0.56f, 1f);
    private static readonly Color UnclearedMarkerColor =
        new(0.34f, 0.39f, 0.35f, 1f);
    private static readonly Color ClearedConnectorColor =
        new(0.28f, 0.72f, 0.46f, 1f);
    private static readonly Color UnclearedConnectorColor =
        new(0.22f, 0.27f, 0.235f, 1f);
    private static readonly Color TitleBannerColor =
        new(0.025f, 0.03f, 0.028f, 0.96f);
    private static readonly Color SequenceTextColor =
        new(0.68f, 0.73f, 0.69f, 1f);

    [Header("Page Navigation")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject dungeonPage;

    [Header("Stage Progress Presentation")]
    [SerializeField] private Sprite clearedMarkerSprite;
    [SerializeField] private Sprite unclearedMarkerSprite;

    [SerializeField, HideInInspector] private ScrollRect _stageScroll;
    [SerializeField, HideInInspector] private RectTransform _stageContent;
    [SerializeField, HideInInspector] private int _stageLayoutVersion;
    [SerializeField, HideInInspector] private int _stageBannerLayoutVersion;

#if UNITY_EDITOR
    private bool _editorSyncInProgress;
    private bool _applyEditorDefaults;
#endif

    protected override string PageTitle => "DUNGEON STAGE";
    protected override string PageDescription => "SELECT A STAGE";
    protected override string PageTitleLocalizationKey =>
        LocalizationKeys.UiStageSelectTitle;
    protected override string PageDescriptionLocalizationKey =>
        LocalizationKeys.UiStageSelectDescription;
    protected override Vector2 PanelSize => new(1680f, 860f);
    protected override bool RequiresSavedDesignerUiAtRuntime => true;

    public override void Open(PageOpenMode mode = PageOpenMode.Fresh)
    {
        base.Open(mode);
        RefreshStageTrack(false);
    }

    protected override void BuildButtons()
    {
#if UNITY_EDITOR
        if (_editorSyncInProgress)
        {
            EnsureEditorUi();
            return;
        }
#endif
        if (!TryBindSavedStageUi(out string error))
        {
            Debug.LogError(error, this);
            return;
        }

        BindLocalizedOverlayMenuButton(
            "btnBACK",
            LocalizationKeys.UiCommonBack,
            HandleBackClicked);
        RefreshStageTrack(false);
    }

    private void ConfigurePageLayout()
    {
        if (PanelRoot != null)
        {
            PanelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            PanelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            PanelRoot.pivot = new Vector2(0.5f, 0.5f);
            PanelRoot.anchoredPosition = Vector2.zero;
            PanelRoot.sizeDelta = PanelSize;

            VerticalLayoutGroup panelLayout =
                PanelRoot.GetComponent<VerticalLayoutGroup>();
            if (panelLayout != null)
            {
                panelLayout.enabled = true;
                panelLayout.padding = new RectOffset(28, 28, 22, 22);
                panelLayout.spacing = 10f;
                panelLayout.childAlignment = TextAnchor.UpperCenter;
                panelLayout.childControlWidth = true;
                panelLayout.childControlHeight = true;
                panelLayout.childForceExpandWidth = true;
                panelLayout.childForceExpandHeight = false;
            }

            ConfigurePageHeadingDefaults();
        }

        if (ButtonRoot == null)
            return;

        LayoutElement rootLayout = ButtonRoot.GetComponent<LayoutElement>();
        if (rootLayout != null)
        {
            rootLayout.preferredHeight = 0f;
            rootLayout.flexibleHeight = 1f;
            rootLayout.flexibleWidth = 1f;
        }

        VerticalLayoutGroup buttonLayout =
            ButtonRoot.GetComponent<VerticalLayoutGroup>();
        if (buttonLayout != null)
        {
            buttonLayout.enabled = true;
            buttonLayout.padding = new RectOffset(0, 0, 0, 0);
            buttonLayout.spacing = 0f;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = true;
            buttonLayout.childForceExpandHeight = true;
        }
    }

    private void ConfigurePageHeadingDefaults()
    {
        if (PanelRoot == null)
            return;

        ConfigureHeading(
            PanelRoot.Find("txtPageTitle") as RectTransform,
            48f,
            34f);
        ConfigureHeading(
            PanelRoot.Find("txtPageDescription") as RectTransform,
            28f,
            16f);
    }

    private static void ConfigureHeading(
        RectTransform target,
        float preferredHeight,
        float fontSize)
    {
        if (target == null)
            return;
        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout != null)
            layout.preferredHeight = preferredHeight;
        TextMeshProUGUI text = target.GetComponent<TextMeshProUGUI>();
        if (text == null)
            return;
        text.fontSize = fontSize;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = Mathf.Max(12f, fontSize - 8f);
    }

    private void HideLegacyButtons()
    {
        SetChildActive(ButtonRoot, "btnSTAGE0TESTFIELD", false);
        SetChildActive(ButtonRoot, "btnFREEBATTLE", false);
        SetChildActive(ButtonRoot, "btnBACK", false);
    }

    private void BuildStageScroll()
    {
        if (ButtonRoot == null)
            return;

        GameObject scrollObject = GetOrCreateChild(
            ButtonRoot,
            ScrollObjectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ScrollRect),
            typeof(LayoutElement));
        scrollObject.SetActive(true);
        RectTransform scrollRectTransform =
            (RectTransform)scrollObject.transform;
        StretchToParent(scrollRectTransform);
        Image scrollBackground = scrollObject.GetComponent<Image>();
        scrollBackground.color = new Color(0f, 0f, 0f, 0.01f);
        scrollBackground.raycastTarget = true;
        LayoutElement scrollLayout = scrollObject.GetComponent<LayoutElement>();
        scrollLayout.preferredHeight = StageNodeHeight + 72f;
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.flexibleWidth = 1f;

        GameObject viewportObject = GetOrCreateChild(
            scrollObject.transform,
            ViewportObjectName,
            typeof(RectTransform),
            typeof(RectMask2D));
        RectTransform viewport = (RectTransform)viewportObject.transform;
        StretchToParent(viewport);

        GameObject contentObject = GetOrCreateChild(
            viewportObject.transform,
            ContentObjectName,
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(ContentSizeFitter));
        _stageContent = (RectTransform)contentObject.transform;
        _stageContent.anchorMin = new Vector2(0f, 0.5f);
        _stageContent.anchorMax = new Vector2(0f, 0.5f);
        _stageContent.pivot = new Vector2(0f, 0.5f);
        _stageContent.anchoredPosition = Vector2.zero;
        _stageContent.sizeDelta = Vector2.zero;

        HorizontalLayoutGroup contentLayout =
            contentObject.GetComponent<HorizontalLayoutGroup>();
        contentLayout.padding = new RectOffset(36, 36, 16, 16);
        contentLayout.spacing = 0f;
        contentLayout.childAlignment = TextAnchor.MiddleLeft;
        contentLayout.childControlWidth = false;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = false;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _stageScroll = scrollObject.GetComponent<ScrollRect>();
        _stageScroll.viewport = viewport;
        _stageScroll.content = _stageContent;
        _stageScroll.horizontal = true;
        _stageScroll.vertical = false;
        _stageScroll.inertia = true;
        _stageScroll.decelerationRate = 0.135f;
        _stageScroll.movementType = ScrollRect.MovementType.Clamped;
        _stageScroll.scrollSensitivity = 34f;
    }

    private void RefreshStageTrack(bool editorPreview)
    {
        if (_stageContent == null)
            return;

        IReadOnlyList<DungeonDefinition> definitions =
            DungeonDefinitionCatalog.GetStageSelectDefinitions();
        DungeonProgressData progress = editorPreview
            ? null
            : DataManager.Current?.DungeonProgressDatas;
        bool previousCleared = false;
        for (int index = 0; index < definitions.Count; index++)
        {
            DungeonDefinition definition = definitions[index];
            if (definition == null)
                continue;

            bool cleared = progress != null &&
                           progress.IsCleared(definition);
            Transform node = _stageContent.Find(
                GetStageNodeName(definition));
            if (node == null)
            {
                Debug.LogError(
                    $"{name}: saved stage node for " +
                    $"'{definition.DungeonId}' is missing. Synchronize " +
                    "the Stage Select UI in the editor and save the scene.",
                    this);
                continue;
            }

            node.gameObject.SetActive(true);
            UpdateStageNode(
                node,
                definition,
                cleared,
                !editorPreview);
            if (index > 0)
            {
                DungeonDefinition previous = definitions[index - 1];
                Transform connector = previous != null
                    ? _stageContent.Find(
                        GetConnectorName(previous, definition))
                    : null;
                Image line = connector != null
                    ? connector.Find("imgLine")?.GetComponent<Image>()
                    : null;
                if (line != null && !editorPreview)
                {
                    line.color = previousCleared
                        ? ClearedConnectorColor
                        : UnclearedConnectorColor;
                }
            }
            previousCleared = cleared;
        }
    }

    private void CreateStageNode(
        DungeonDefinition definition,
        bool cleared)
    {
        GameObject nodeObject = new(
            StageNodePrefix + SanitizeObjectName(definition.DungeonId),
            typeof(RectTransform),
            typeof(Button),
            typeof(LayoutElement),
            typeof(VerticalLayoutGroup));
        nodeObject.transform.SetParent(_stageContent, false);
        RectTransform nodeRect = (RectTransform)nodeObject.transform;
        nodeRect.sizeDelta = new Vector2(StageNodeWidth, StageNodeHeight);
        LayoutElement nodeLayout = nodeObject.GetComponent<LayoutElement>();
        nodeLayout.minWidth = StageNodeWidth;
        nodeLayout.preferredWidth = StageNodeWidth;
        nodeLayout.minHeight = StageNodeHeight;
        nodeLayout.preferredHeight = StageNodeHeight;

        VerticalLayoutGroup vertical =
            nodeObject.GetComponent<VerticalLayoutGroup>();
        vertical.enabled = false;

        GameObject coverObject = new(
            "imgStageCover",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LayoutElement));
        coverObject.transform.SetParent(nodeObject.transform, false);
        Image cover = coverObject.GetComponent<Image>();
        cover.sprite = definition.StageCoverSprite;
        cover.color = cover.sprite != null
            ? Color.white
            : CoverPlaceholderColor;
        cover.preserveAspect = true;
        cover.raycastTarget = true;
        LayoutElement coverLayout = coverObject.GetComponent<LayoutElement>();
        coverLayout.ignoreLayout = true;
        coverLayout.minWidth = CoverWidth;
        coverLayout.preferredWidth = CoverWidth;
        coverLayout.minHeight = CoverHeight;
        coverLayout.preferredHeight = CoverHeight;

        TextMeshProUGUI title = CreateText(
            nodeObject.transform,
            "txtStageTitle",
            24f,
            54f,
            true);
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.textWrappingMode = TextWrappingModes.Normal;
        ApplyStageTitle(title, definition, true);

        GameObject markerAreaObject = new(
            "grpStageMarker",
            typeof(RectTransform),
            typeof(LayoutElement));
        markerAreaObject.transform.SetParent(nodeObject.transform, false);
        LayoutElement markerAreaLayout =
            markerAreaObject.GetComponent<LayoutElement>();
        markerAreaLayout.ignoreLayout = true;
        markerAreaLayout.minHeight = 72f;
        markerAreaLayout.preferredHeight = 72f;

        GameObject markerObject = new(
            "imgStageClearState",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        markerObject.transform.SetParent(markerAreaObject.transform, false);
        RectTransform markerRect = (RectTransform)markerObject.transform;
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = Vector2.zero;
        markerRect.sizeDelta = Vector2.one * MarkerSize;
        Image marker = markerObject.GetComponent<Image>();
        marker.sprite = cleared
            ? clearedMarkerSprite
            : unclearedMarkerSprite;
        marker.color = cleared
            ? ClearedMarkerColor
            : UnclearedMarkerColor;
        marker.preserveAspect = true;
        marker.raycastTarget = false;

        Button button = nodeObject.GetComponent<Button>();
        button.targetGraphic = cover;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => HandleStageClicked(definition));
    }

    private void CreateConnector(
        DungeonDefinition previous,
        DungeonDefinition current,
        bool completed)
    {
        GameObject connectorObject = new(
            GetConnectorName(previous, current),
            typeof(RectTransform),
            typeof(LayoutElement));
        connectorObject.transform.SetParent(_stageContent, false);
        RectTransform connectorRect =
            (RectTransform)connectorObject.transform;
        connectorRect.sizeDelta = new Vector2(
            ConnectorWidth,
            StageNodeHeight);
        LayoutElement connectorLayout =
            connectorObject.GetComponent<LayoutElement>();
        connectorLayout.minWidth = ConnectorWidth;
        connectorLayout.preferredWidth = ConnectorWidth;
        connectorLayout.minHeight = StageNodeHeight;
        connectorLayout.preferredHeight = StageNodeHeight;

        GameObject lineObject = new(
            "imgLine",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        lineObject.transform.SetParent(connectorObject.transform, false);
        RectTransform lineRect = (RectTransform)lineObject.transform;
        lineRect.anchorMin = new Vector2(0f, 1f);
        lineRect.anchorMax = new Vector2(1f, 1f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.anchoredPosition = new Vector2(0f, -MarkerTop);
        lineRect.sizeDelta = new Vector2(0f, 4f);
        Image line = lineObject.GetComponent<Image>();
        line.color = completed
            ? ClearedConnectorColor
            : UnclearedConnectorColor;
        line.raycastTarget = false;
    }

    private static void ApplyStageTitle(
        TextMeshProUGUI title,
        DungeonDefinition definition,
        bool allowComponentCreation)
    {
        if (title == null || definition == null)
            return;

        if (string.IsNullOrWhiteSpace(definition.TitleLocalizationKey))
        {
            title.text = definition.FallbackTitle;
            return;
        }

        LocalizedText localized = title.GetComponent<LocalizedText>();
        if (localized == null && allowComponentCreation)
            localized = title.gameObject.AddComponent<LocalizedText>();
        title.text = definition.FallbackTitle;
        if (localized != null)
            localized.SetKey(definition.TitleLocalizationKey);
    }

    private void UpdateStageNode(
        Transform node,
        DungeonDefinition definition,
        bool cleared,
        bool updateStateVisuals)
    {
        if (node == null || definition == null)
            return;

        Image cover = node.Find("imgStageCover")?.GetComponent<Image>();
        TextMeshProUGUI title = FindStageTitle(node);
        Image marker = node.Find("grpStageMarker/imgStageClearState")
            ?.GetComponent<Image>();
        TextMeshProUGUI markerGlyph = marker != null
            ? marker.transform.Find(MarkerGlyphObjectName)
                ?.GetComponent<TextMeshProUGUI>()
            : null;
        Image progressLine = node.Find(ProgressLineObjectName)
            ?.GetComponent<Image>();
        Button button = node.GetComponent<Button>();
        if (cover == null || title == null || marker == null ||
            button == null)
        {
            Debug.LogError(
                $"{name}: saved stage node '{node.name}' has incomplete " +
                "designer references.",
                this);
            return;
        }

        cover.sprite = definition.StageCoverSprite;
        cover.color = cover.sprite != null
            ? Color.white
            : CoverPlaceholderColor;
        cover.preserveAspect = true;
        ApplyStageTitle(title, definition, false);
        if (updateStateVisuals)
        {
            marker.sprite = cleared
                ? clearedMarkerSprite
                : unclearedMarkerSprite;
            Color stateColor = cleared
                ? ClearedMarkerColor
                : UnclearedMarkerColor;
            marker.color = marker.sprite != null
                ? stateColor
                : Color.clear;
            if (markerGlyph != null)
            {
                markerGlyph.text = cleared ? "●" : "○";
                markerGlyph.color = stateColor;
                markerGlyph.gameObject.SetActive(marker.sprite == null);
            }
            if (progressLine != null)
            {
                progressLine.color = cleared
                    ? ClearedConnectorColor
                    : UnclearedConnectorColor;
            }
        }
        button.targetGraphic = cover;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => HandleStageClicked(definition));
    }

    private bool TryBindSavedStageUi(out string error)
    {
        error = string.Empty;
        if (!TryBindStageContainers(out error))
            return false;

        Transform savedRoot = RuntimeRoot != null
            ? RuntimeRoot
            : transform.Find(RuntimeRootObjectName);
        Transform back = savedRoot != null
            ? savedRoot.Find("btnBACK")
            : null;
        if (back == null || back.GetComponent<Button>() == null ||
            back.Find("txtLabel")?.GetComponent<TextMeshProUGUI>() == null)
        {
            error = $"{name}: saved Stage Select back button is missing.";
            return false;
        }

        IReadOnlyList<DungeonDefinition> definitions =
            DungeonDefinitionCatalog.GetStageSelectDefinitions();
        for (int index = 0; index < definitions.Count; index++)
        {
            DungeonDefinition definition = definitions[index];
            if (definition == null)
                continue;

            Transform node = _stageContent.Find(
                GetStageNodeName(definition));
            if (!HasRequiredStageNodeReferences(node))
            {
                error = $"{name}: saved stage node for " +
                        $"'{definition.DungeonId}' is missing or incomplete.";
                return false;
            }

            if (index <= 0)
                continue;
            DungeonDefinition previous = definitions[index - 1];
            Transform connector = previous != null
                ? _stageContent.Find(
                    GetConnectorName(previous, definition))
                : null;
            if (connector == null ||
                connector.Find("imgLine")?.GetComponent<Image>() == null)
            {
                error = $"{name}: saved connector before " +
                        $"'{definition.DungeonId}' is missing.";
                return false;
            }
        }

        return true;
    }

    private bool TryBindStageContainers(out string error)
    {
        error = string.Empty;
        Transform savedButtonRoot = ButtonRoot != null
            ? ButtonRoot
            : transform.Find(
                RuntimeRootObjectName +
                "/grpMenuPanel/grpMenuButtons");
        Transform scrollTransform = savedButtonRoot != null
            ? savedButtonRoot.Find(ScrollObjectName)
            : null;
        Transform viewport = scrollTransform != null
            ? scrollTransform.Find(ViewportObjectName)
            : null;
        Transform content = viewport != null
            ? viewport.Find(ContentObjectName)
            : null;
        _stageScroll = scrollTransform != null
            ? scrollTransform.GetComponent<ScrollRect>()
            : null;
        _stageContent = content as RectTransform;
        if (_stageScroll == null || _stageContent == null ||
            viewport?.GetComponent<RectMask2D>() == null ||
            _stageContent.GetComponent<HorizontalLayoutGroup>() == null ||
            _stageContent.GetComponent<ContentSizeFitter>() == null)
        {
            error = $"{name}: saved Stage Select scroll hierarchy is " +
                    "missing or incomplete.";
            return false;
        }

        _stageScroll.viewport = viewport as RectTransform;
        _stageScroll.content = _stageContent;
        return true;
    }

    private static bool HasRequiredStageNodeReferences(Transform node)
    {
        return node != null &&
               node.GetComponent<Button>() != null &&
               node.Find("imgStageCover")?.GetComponent<Image>() != null &&
               node.Find(ProgressLineObjectName)
                   ?.GetComponent<Image>() != null &&
               node.Find(TitleBannerObjectName + "/" +
                   SequenceTextObjectName)
                   ?.GetComponent<TextMeshProUGUI>() != null &&
               FindStageTitle(node) != null &&
               node.Find("grpStageMarker/imgStageClearState")
                   ?.GetComponent<Image>() != null &&
               node.Find("grpStageMarker/imgStageClearState/" +
                   MarkerGlyphObjectName)
                   ?.GetComponent<TextMeshProUGUI>() != null;
    }

    private static TextMeshProUGUI FindStageTitle(Transform node)
    {
        if (node == null)
            return null;
        return node.Find(
                   TitleBannerObjectName + "/txtStageTitle")
                   ?.GetComponent<TextMeshProUGUI>() ??
               node.Find("txtStageTitle")
                   ?.GetComponent<TextMeshProUGUI>();
    }

    private static string GetStageNodeName(DungeonDefinition definition)
    {
        return StageNodePrefix + SanitizeObjectName(
            definition != null ? definition.DungeonId : string.Empty);
    }

    private static string GetConnectorName(
        DungeonDefinition previous,
        DungeonDefinition current)
    {
        return ConnectorPrefix +
               SanitizeObjectName(
                   previous != null ? previous.DungeonId : string.Empty) +
               "_" +
               SanitizeObjectName(
                   current != null ? current.DungeonId : string.Empty);
    }

#if UNITY_EDITOR
    private bool EnsureReferenceStageNodeHierarchy(
        Transform node,
        DungeonDefinition definition)
    {
        if (node == null)
            return false;

        bool changed = false;

        Transform banner = node.Find(TitleBannerObjectName);
        if (banner == null)
        {
            GameObject bannerObject = new(
                TitleBannerObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            bannerObject.transform.SetParent(node, false);
            banner = bannerObject.transform;
            UnityEditor.Undo.RegisterCreatedObjectUndo(
                bannerObject,
                "Create Stage Title Banner");
            changed = true;
        }

        Image bannerImage = banner.GetComponent<Image>();
        if (bannerImage == null)
        {
            bannerImage = UnityEditor.Undo.AddComponent<Image>(
                banner.gameObject);
            changed = true;
        }
        bannerImage.raycastTarget = false;

        TextMeshProUGUI title = FindStageTitle(node);
        if (title != null && title.transform.parent != banner)
        {
            UnityEditor.Undo.SetTransformParent(
                title.transform,
                banner,
                "Move Stage Title Into Banner");
            changed = true;
        }

        TextMeshProUGUI sequence = banner.Find(SequenceTextObjectName)
            ?.GetComponent<TextMeshProUGUI>();
        if (sequence == null)
        {
            sequence = CreateText(
                banner,
                SequenceTextObjectName,
                13f,
                20f,
                true);
            UnityEditor.Undo.RegisterCreatedObjectUndo(
                sequence.gameObject,
                "Create Stage Sequence Label");
            changed = true;
        }
        sequence.text = $"EPISODE {Mathf.Max(1, definition.StageOrder + 1):00}";

        Transform progressLine = node.Find(ProgressLineObjectName);
        if (progressLine == null)
        {
            GameObject lineObject = new(
                ProgressLineObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            lineObject.transform.SetParent(node, false);
            progressLine = lineObject.transform;
            UnityEditor.Undo.RegisterCreatedObjectUndo(
                lineObject,
                "Create Stage Progress Line");
            changed = true;
        }
        Image progressImage = progressLine.GetComponent<Image>();
        if (progressImage == null)
        {
            progressImage = UnityEditor.Undo.AddComponent<Image>(
                progressLine.gameObject);
            changed = true;
        }
        progressImage.raycastTarget = false;

        Transform marker = node.Find(
            "grpStageMarker/imgStageClearState");
        TextMeshProUGUI markerGlyph = marker != null
            ? marker.Find(MarkerGlyphObjectName)
                ?.GetComponent<TextMeshProUGUI>()
            : null;
        if (marker != null && markerGlyph == null)
        {
            markerGlyph = CreateText(
                marker,
                MarkerGlyphObjectName,
                48f,
                MarkerSize,
                true);
            markerGlyph.text = "○";
            UnityEditor.Undo.RegisterCreatedObjectUndo(
                markerGlyph.gameObject,
                "Create Stage Marker Glyph");
            changed = true;
        }
        return changed;
    }

    private void ApplyReferencePageLayout()
    {
        ConfigurePageLayout();

        LayoutElement scrollLayout = _stageScroll != null
            ? _stageScroll.GetComponent<LayoutElement>()
            : null;
        if (scrollLayout != null)
        {
            UnityEditor.Undo.RecordObject(
                scrollLayout,
                "Apply Stage Select Reference Layout");
            scrollLayout.preferredHeight = StageNodeHeight + 64f;
            scrollLayout.flexibleHeight = 1f;
        }

        HorizontalLayoutGroup contentLayout = _stageContent != null
            ? _stageContent.GetComponent<HorizontalLayoutGroup>()
            : null;
        if (contentLayout != null)
        {
            UnityEditor.Undo.RecordObject(
                contentLayout,
                "Apply Stage Select Reference Layout");
            contentLayout.padding = new RectOffset(52, 52, 26, 26);
            contentLayout.spacing = 0f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = false;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = false;
        }
    }

    private static void ApplyReferenceStageNodeLayout(Transform node)
    {
        if (node is not RectTransform nodeRect)
            return;

        UnityEditor.Undo.RecordObject(
            nodeRect,
            "Apply Stage Card Reference Layout");
        nodeRect.sizeDelta = new Vector2(
            StageNodeWidth,
            StageNodeHeight);

        LayoutElement nodeLayout = node.GetComponent<LayoutElement>();
        if (nodeLayout != null)
        {
            UnityEditor.Undo.RecordObject(
                nodeLayout,
                "Apply Stage Card Reference Layout");
            nodeLayout.minWidth = StageNodeWidth;
            nodeLayout.preferredWidth = StageNodeWidth;
            nodeLayout.minHeight = StageNodeHeight;
            nodeLayout.preferredHeight = StageNodeHeight;
        }

        VerticalLayoutGroup vertical =
            node.GetComponent<VerticalLayoutGroup>();
        if (vertical != null && vertical.enabled)
        {
            UnityEditor.Undo.RecordObject(
                vertical,
                "Disable Legacy Stage Card Layout");
            vertical.enabled = false;
        }

        RectTransform cover = node.Find("imgStageCover") as RectTransform;
        SetTopAnchored(
            cover,
            0.5f,
            0f,
            CoverTop,
            CoverWidth,
            CoverHeight,
            new Vector2(0.5f, 1f));
        LayoutElement coverLayout = cover != null
            ? cover.GetComponent<LayoutElement>()
            : null;
        if (coverLayout != null)
        {
            UnityEditor.Undo.RecordObject(
                coverLayout,
                "Apply Stage Cover Reference Layout");
            coverLayout.ignoreLayout = true;
            coverLayout.preferredWidth = CoverWidth;
            coverLayout.preferredHeight = CoverHeight;
        }

        RectTransform progress = node.Find(ProgressLineObjectName)
            as RectTransform;
        if (progress != null)
        {
            UnityEditor.Undo.RecordObject(
                progress,
                "Apply Stage Progress Line Layout");
            progress.anchorMin = new Vector2(0f, 1f);
            progress.anchorMax = new Vector2(1f, 1f);
            progress.pivot = new Vector2(0.5f, 0.5f);
            progress.anchoredPosition = new Vector2(0f, -MarkerTop);
            progress.sizeDelta = new Vector2(0f, 4f);
            progress.SetSiblingIndex(1);
            Image progressImage = progress.GetComponent<Image>();
            if (progressImage != null)
            {
                UnityEditor.Undo.RecordObject(
                    progressImage,
                    "Apply Stage Progress Line Style");
                progressImage.color = UnclearedConnectorColor;
                progressImage.raycastTarget = false;
            }
        }

        RectTransform banner = node.Find(TitleBannerObjectName)
            as RectTransform;
        SetTopAnchored(
            banner,
            0f,
            36f,
            TitleBannerTop,
            TitleBannerWidth,
            TitleBannerHeight,
            new Vector2(0f, 1f));
        if (banner != null)
        {
            banner.SetSiblingIndex(2);
            Image image = banner.GetComponent<Image>();
            if (image != null)
            {
                UnityEditor.Undo.RecordObject(
                    image,
                    "Apply Stage Title Banner Style");
                image.color = TitleBannerColor;
                image.raycastTarget = false;
            }
        }

        TextMeshProUGUI sequence = banner != null
            ? banner.Find(SequenceTextObjectName)
                ?.GetComponent<TextMeshProUGUI>()
            : null;
        if (sequence != null)
        {
            ApplyBannerTextLayout(
                sequence,
                10f,
                5f,
                18f,
                13f,
                SequenceTextColor,
                FontStyles.Bold);
        }

        TextMeshProUGUI title = FindStageTitle(node);
        if (title != null)
        {
            ApplyBannerTextLayout(
                title,
                10f,
                25f,
                54f,
                25f,
                Color.white,
                FontStyles.Bold);
        }

        RectTransform markerArea = node.Find("grpStageMarker")
            as RectTransform;
        if (markerArea != null)
        {
            UnityEditor.Undo.RecordObject(
                markerArea,
                "Apply Stage Marker Area Layout");
            markerArea.anchorMin = new Vector2(0f, 1f);
            markerArea.anchorMax = new Vector2(1f, 1f);
            markerArea.pivot = new Vector2(0.5f, 1f);
            markerArea.anchoredPosition = Vector2.zero;
            markerArea.sizeDelta = new Vector2(0f, 72f);
            markerArea.SetSiblingIndex(3);
            LayoutElement markerLayout =
                markerArea.GetComponent<LayoutElement>();
            if (markerLayout != null)
            {
                UnityEditor.Undo.RecordObject(
                    markerLayout,
                    "Apply Stage Marker Area Layout");
                markerLayout.ignoreLayout = true;
            }
        }

        RectTransform marker = markerArea != null
            ? markerArea.Find("imgStageClearState") as RectTransform
            : null;
        if (marker != null)
        {
            UnityEditor.Undo.RecordObject(
                marker,
                "Apply Stage Marker Reference Layout");
            marker.anchorMin = new Vector2(0f, 1f);
            marker.anchorMax = new Vector2(0f, 1f);
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.anchoredPosition = new Vector2(24f, -MarkerTop);
            marker.sizeDelta = Vector2.one * MarkerSize;

            Image markerImage = marker.GetComponent<Image>();
            TextMeshProUGUI markerGlyph = marker.Find(
                    MarkerGlyphObjectName)
                ?.GetComponent<TextMeshProUGUI>();
            if (markerImage != null && markerImage.sprite == null)
            {
                UnityEditor.Undo.RecordObject(
                    markerImage,
                    "Apply Stage Marker Fallback Style");
                markerImage.color = Color.clear;
            }
            if (markerGlyph != null)
            {
                UnityEditor.Undo.RecordObject(
                    markerGlyph,
                    "Apply Stage Marker Fallback Style");
                RectTransform glyphRect = markerGlyph.rectTransform;
                glyphRect.anchorMin = Vector2.zero;
                glyphRect.anchorMax = Vector2.one;
                glyphRect.offsetMin = Vector2.zero;
                glyphRect.offsetMax = Vector2.zero;
                markerGlyph.fontSize = 48f;
                markerGlyph.fontSizeMax = 48f;
                markerGlyph.fontSizeMin = 40f;
                markerGlyph.alignment = TextAlignmentOptions.Center;
                markerGlyph.color = UnclearedMarkerColor;
                markerGlyph.raycastTarget = false;
                markerGlyph.gameObject.SetActive(
                    markerImage == null || markerImage.sprite == null);
                LayoutElement glyphLayout =
                    markerGlyph.GetComponent<LayoutElement>();
                if (glyphLayout != null)
                    glyphLayout.ignoreLayout = true;
            }
        }

        if (cover != null)
            cover.SetSiblingIndex(0);
    }

    private static void ApplySquareStageBannerLayout(Transform node)
    {
        RectTransform cover = node != null
            ? node.Find("imgStageCover") as RectTransform
            : null;
        if (cover == null)
            return;

        float width = cover.sizeDelta.x > 0f
            ? cover.sizeDelta.x
            : CoverWidth;
        UnityEditor.Undo.RecordObject(
            cover,
            "Make Stage Banner Square");
        cover.sizeDelta = new Vector2(width, width);

        LayoutElement layout = cover.GetComponent<LayoutElement>();
        if (layout == null)
            return;

        UnityEditor.Undo.RecordObject(
            layout,
            "Make Stage Banner Square");
        float preferredWidth = layout.preferredWidth > 0f
            ? layout.preferredWidth
            : width;
        layout.preferredHeight = preferredWidth;
        if (layout.minWidth > 0f)
            layout.minHeight = layout.minWidth;
    }

    private static void ApplyReferenceConnectorLayout(Transform connector)
    {
        if (connector is not RectTransform connectorRect)
            return;
        UnityEditor.Undo.RecordObject(
            connectorRect,
            "Apply Stage Connector Reference Layout");
        connectorRect.sizeDelta = new Vector2(
            ConnectorWidth,
            StageNodeHeight);

        LayoutElement layout = connector.GetComponent<LayoutElement>();
        if (layout != null)
        {
            UnityEditor.Undo.RecordObject(
                layout,
                "Apply Stage Connector Reference Layout");
            layout.minWidth = ConnectorWidth;
            layout.preferredWidth = ConnectorWidth;
            layout.minHeight = StageNodeHeight;
            layout.preferredHeight = StageNodeHeight;
        }

        RectTransform line = connector.Find("imgLine") as RectTransform;
        if (line == null)
            return;
        UnityEditor.Undo.RecordObject(
            line,
            "Apply Stage Connector Line Layout");
        line.anchorMin = new Vector2(0f, 1f);
        line.anchorMax = new Vector2(1f, 1f);
        line.pivot = new Vector2(0.5f, 0.5f);
        line.anchoredPosition = new Vector2(0f, -MarkerTop);
        line.sizeDelta = new Vector2(0f, 4f);
    }

    private static void SetTopAnchored(
        RectTransform rect,
        float anchorX,
        float x,
        float top,
        float width,
        float height,
        Vector2 pivot)
    {
        if (rect == null)
            return;
        UnityEditor.Undo.RecordObject(
            rect,
            "Apply Stage Reference Layout");
        rect.anchorMin = new Vector2(anchorX, 1f);
        rect.anchorMax = new Vector2(anchorX, 1f);
        rect.pivot = pivot;
        rect.anchoredPosition = new Vector2(x, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void ApplyBannerTextLayout(
        TextMeshProUGUI text,
        float left,
        float top,
        float height,
        float fontSize,
        Color color,
        FontStyles style)
    {
        UnityEditor.Undo.RecordObject(
            text,
            "Apply Stage Banner Text Layout");
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-10f, -top);
        text.fontSize = fontSize;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = Mathf.Max(10f, fontSize - 7f);
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.Normal;
        LayoutElement layout = text.GetComponent<LayoutElement>();
        if (layout != null)
        {
            UnityEditor.Undo.RecordObject(
                layout,
                "Apply Stage Banner Text Layout");
            layout.ignoreLayout = true;
        }
    }

    public bool ValidateEditorUi(out string error)
    {
        if (Application.isPlaying)
        {
            error = "Stage Select UI cannot be validated in Play Mode.";
            return false;
        }
        return TryBindSavedStageUi(out error);
    }

    public bool SyncEditorUi(out string error)
    {
        error = string.Empty;
        if (Application.isPlaying)
        {
            error = "Stage Select UI cannot be synchronized in Play Mode.";
            return false;
        }

        Transform existingRoot = transform.Find(RuntimeRootObjectName);
        if (existingRoot != null &&
            (existingRoot.Find("grpMenuPanel") == null ||
             existingRoot.Find("grpMenuPanel/grpMenuButtons") == null))
        {
            error = $"{name}: existing designer UI root is incomplete. " +
                    "Repair it manually; synchronization will not replace it.";
            return false;
        }

        bool createdRoot = existingRoot == null;
        _editorSyncInProgress = true;
        _applyEditorDefaults = createdRoot;
        try
        {
            Init();
            EnsureEditorUi();
            if (!TryBindSavedStageUi(out error))
                return false;

            RefreshStageTrack(true);
            MarkDesignerLayoutCurrent();
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                gameObject.scene);
            return true;
        }
        catch (Exception exception)
        {
            error = $"{name}: Stage Select UI synchronization failed. " +
                    exception.Message;
            return false;
        }
        finally
        {
            _editorSyncInProgress = false;
            _applyEditorDefaults = false;
        }
    }

    private void EnsureEditorUi()
    {
        if (_applyEditorDefaults)
            ConfigurePageLayout();
        HideLegacyButtons();

        Transform existingScroll = ButtonRoot != null
            ? ButtonRoot.Find(ScrollObjectName)
            : null;
        if (existingScroll == null)
        {
            BuildStageScroll();
            if (_stageScroll != null)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(
                    _stageScroll.gameObject,
                    "Create Stage Select Scroll");
            }
        }
        else if (!TryBindStageContainers(out string containerError))
        {
            throw new InvalidOperationException(containerError);
        }

        Transform existingBack = RuntimeRoot != null
            ? RuntimeRoot.Find("btnBACK")
            : null;
        if (existingBack != null &&
            (existingBack.GetComponent<Button>() == null ||
             existingBack.Find("txtLabel")
                 ?.GetComponent<TextMeshProUGUI>() == null))
        {
            throw new InvalidOperationException(
                "The existing Stage Select back button is incomplete.");
        }
        bool createdBack = existingBack == null;
        Button back = CreateLocalizedTopLeftOverlayMenuButton(
            "btnBACK",
            LocalizationKeys.UiCommonBack,
            HandleBackClicked);
        if (createdBack && back != null)
        {
            UnityEditor.Undo.RegisterCreatedObjectUndo(
                back.gameObject,
                "Create Stage Select Back Button");
        }

        SynchronizeEditorStageNodes();
    }

    private void SynchronizeEditorStageNodes()
    {
        if (_stageContent == null)
            throw new InvalidOperationException(
                "The Stage Select content root is missing.");

        IReadOnlyList<DungeonDefinition> definitions =
            DungeonDefinitionCatalog.GetStageSelectDefinitions();
        bool applyReferenceLayout =
            _stageLayoutVersion < ReferenceLayoutVersion;
        bool applySquareBannerLayout =
            _stageBannerLayoutVersion < SquareBannerLayoutVersion;
        if (applyReferenceLayout)
            ApplyReferencePageLayout();
        HashSet<Transform> expected = new();
        int siblingIndex = 0;
        for (int index = 0; index < definitions.Count; index++)
        {
            DungeonDefinition definition = definitions[index];
            if (definition == null)
                continue;

            if (index > 0)
            {
                DungeonDefinition previous = definitions[index - 1];
                string connectorName = GetConnectorName(
                    previous,
                    definition);
                Transform connector = _stageContent.Find(connectorName);
                bool createdConnector = false;
                if (connector == null)
                {
                    Transform legacy = _stageContent.Find(
                        $"{ConnectorPrefix}{index - 1}");
                    if (legacy != null)
                    {
                        UnityEditor.Undo.RecordObject(
                            legacy.gameObject,
                            "Migrate Stage Connector Name");
                        legacy.name = connectorName;
                        connector = legacy;
                    }
                    else
                    {
                        CreateConnector(previous, definition, false);
                        connector = _stageContent.Find(connectorName);
                        createdConnector = connector != null;
                        if (connector != null)
                        {
                            UnityEditor.Undo.RegisterCreatedObjectUndo(
                                connector.gameObject,
                                "Create Stage Connector");
                        }
                    }
                }
                if (connector == null ||
                    connector.Find("imgLine")?.GetComponent<Image>() == null)
                {
                    throw new InvalidOperationException(
                        $"Connector before '{definition.DungeonId}' is " +
                        "incomplete.");
                }
                expected.Add(connector);
                UnityEditor.Undo.RecordObject(
                    connector,
                    "Order Stage Connector");
                connector.SetSiblingIndex(siblingIndex++);
                connector.gameObject.SetActive(true);
                if (applyReferenceLayout || createdConnector)
                    ApplyReferenceConnectorLayout(connector);
            }

            string nodeName = GetStageNodeName(definition);
            Transform node = _stageContent.Find(nodeName);
            bool createdNode = false;
            if (node == null)
            {
                CreateStageNode(definition, false);
                node = _stageContent.Find(nodeName);
                createdNode = node != null;
                if (node != null)
                {
                    UnityEditor.Undo.RegisterCreatedObjectUndo(
                        node.gameObject,
                        "Create Stage Select Node");
                }
            }
            bool hierarchyChanged =
                EnsureReferenceStageNodeHierarchy(node, definition);
            if (!HasRequiredStageNodeReferences(node))
            {
                throw new InvalidOperationException(
                    $"Stage node '{definition.DungeonId}' is incomplete.");
            }
            expected.Add(node);
            UnityEditor.Undo.RecordObject(node, "Order Stage Select Node");
            node.SetSiblingIndex(siblingIndex++);
            node.gameObject.SetActive(true);
            if (applyReferenceLayout || createdNode || hierarchyChanged)
                ApplyReferenceStageNodeLayout(node);
            else if (applySquareBannerLayout)
                ApplySquareStageBannerLayout(node);
            UpdateStageNode(node, definition, false, false);
        }

        for (int index = 0; index < _stageContent.childCount; index++)
        {
            Transform child = _stageContent.GetChild(index);
            bool generated = child.name.StartsWith(
                                 StageNodePrefix,
                                 StringComparison.Ordinal) ||
                             child.name.StartsWith(
                                 ConnectorPrefix,
                                 StringComparison.Ordinal);
            if (generated && !expected.Contains(child) &&
                child.gameObject.activeSelf)
            {
                UnityEditor.Undo.RecordObject(
                    child.gameObject,
                    "Disable Obsolete Stage Element");
                child.gameObject.SetActive(false);
            }
        }

        if (applyReferenceLayout)
        {
            UnityEditor.Undo.RecordObject(
                this,
                "Apply Stage Select Reference Layout");
            _stageLayoutVersion = ReferenceLayoutVersion;
            UnityEditor.EditorUtility.SetDirty(this);
        }
        if (applyReferenceLayout || applySquareBannerLayout)
        {
            UnityEditor.Undo.RecordObject(
                this,
                "Migrate Square Stage Banners");
            _stageBannerLayoutVersion = SquareBannerLayoutVersion;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_stageContent);
    }
#endif

    private void HandleStageClicked(DungeonDefinition definition)
    {
        if (definition == null)
            return;

        if (dungeonPage != null &&
            dungeonPage.TryGetComponent(out DungeonPage dungeon))
        {
            dungeon.PrepareDungeon(definition);
        }

        NavigateTo(dungeonPage, PageOpenMode.Fresh);
    }

    private void HandleBackClicked()
    {
        NavigateTo(mainPage, PageOpenMode.Resume);
    }

    private static void StretchToParent(RectTransform rect)
    {
        if (rect == null)
            return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void SetChildActive(
        Transform parent,
        string childName,
        bool active)
    {
        Transform child = parent != null
            ? parent.Find(childName)
            : null;
        if (child != null)
            child.gameObject.SetActive(active);
    }

    private static GameObject GetOrCreateChild(
        Transform parent,
        string objectName,
        params Type[] componentTypes)
    {
        Transform existing = parent != null
            ? parent.Find(objectName)
            : null;
        GameObject result = existing != null
            ? existing.gameObject
            : new GameObject(objectName, typeof(RectTransform));
        if (existing == null)
            result.transform.SetParent(parent, false);

        foreach (Type componentType in componentTypes)
        {
            if (componentType == typeof(RectTransform) ||
                result.GetComponent(componentType) != null)
            {
                continue;
            }
            result.AddComponent(componentType);
        }
        return result;
    }

    private static string SanitizeObjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        char[] characters = value.Trim().ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            char current = characters[index];
            if (!char.IsLetterOrDigit(current) && current != '_' &&
                current != '-')
            {
                characters[index] = '_';
            }
        }
        return new string(characters);
    }
}
