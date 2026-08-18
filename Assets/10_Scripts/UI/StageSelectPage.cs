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

    public bool RequestMainMenuBgm()
    {
        if (mainPage == null ||
            !mainPage.TryGetComponent(out PageBgmSelection selection))
        {
            return false;
        }

        return selection.RequestSelectedBgm();
    }

    [Header("Stage Progress Presentation")]
    [SerializeField] private Sprite clearedMarkerSprite;
    [SerializeField] private Sprite unclearedMarkerSprite;

    [SerializeField, HideInInspector] private ScrollRect _stageScroll;
    [SerializeField, HideInInspector] private RectTransform _stageContent;
    [SerializeField, HideInInspector] private int _stageLayoutVersion;
    [SerializeField, HideInInspector] private int _stageBannerLayoutVersion;

    protected override string PageTitle => "DUNGEON STAGE";
    protected override string PageDescription => "SELECT A STAGE";
    protected override string PageTitleLocalizationKey =>
        LocalizationKeys.UiStageSelectTitle;
    protected override string PageDescriptionLocalizationKey =>
        LocalizationKeys.UiStageSelectDescription;

    public override void Open(PageOpenMode mode = PageOpenMode.Fresh)
    {
        base.Open(mode);
        RefreshStageTrack(false);
    }

    protected override void BuildButtons()
    {
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

            bool tracksProgress = definition.PersistsDungeonProgress;
            bool cleared = tracksProgress && progress != null &&
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
                !editorPreview,
                tracksProgress);
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
            if (tracksProgress)
                previousCleared = cleared;
        }
    }

    private static void ApplyStageTitle(
        TextMeshProUGUI title,
        DungeonDefinition definition)
    {
        if (title == null || definition == null)
            return;

        if (string.IsNullOrWhiteSpace(definition.TitleLocalizationKey))
        {
            title.text = definition.FallbackTitle;
            return;
        }

        LocalizedText localized = title.GetComponent<LocalizedText>();
        title.text = definition.FallbackTitle;
        if (localized != null)
            localized.SetKey(definition.TitleLocalizationKey);
    }

    private void UpdateStageNode(
        Transform node,
        DungeonDefinition definition,
        bool cleared,
        bool updateStateVisuals,
        bool tracksProgress)
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
        ApplyStageTitle(title, definition);
        if (updateStateVisuals)
        {
            marker.gameObject.SetActive(tracksProgress);
            if (progressLine != null)
                progressLine.gameObject.SetActive(tracksProgress);
            if (!tracksProgress)
            {
                button.targetGraphic = cover;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(
                    () => HandleStageClicked(definition));
                return;
            }

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
        if (Application.isPlaying)
        {
            error = "Stage Select UI cannot be synchronized in Play Mode.";
            return false;
        }

        if (!TryBindStageContainers(out error))
            return false;

        IReadOnlyList<DungeonDefinition> definitions =
            DungeonDefinitionCatalog.GetStageSelectDefinitions();
        List<Transform> orderedNodes = new(definitions.Count);
        List<string> expectedConnectorNames = new(
            Mathf.Max(0, definitions.Count - 1));
        for (int index = 0; index < definitions.Count; index++)
        {
            DungeonDefinition definition = definitions[index];
            Transform node = definition != null
                ? _stageContent.Find(GetStageNodeName(definition))
                : null;
            if (!HasRequiredStageNodeReferences(node))
            {
                error = $"{name}: saved stage node for " +
                        $"'{definition?.DungeonId ?? "unknown"}' is " +
                        "missing or incomplete.";
                return false;
            }

            orderedNodes.Add(node);
            if (index > 0)
            {
                expectedConnectorNames.Add(GetConnectorName(
                    definitions[index - 1],
                    definition));
            }
        }

        if (!TryReorderSavedStageChildrenForEditor(
                _stageContent,
                orderedNodes,
                expectedConnectorNames,
                out error))
        {
            error = $"{name}: {error}";
            return false;
        }

        int numberedStageIndex = 0;
        for (int index = 0; index < definitions.Count; index++)
        {
            TextMeshProUGUI sequence = orderedNodes[index].Find(
                    TitleBannerObjectName + "/" + SequenceTextObjectName)
                ?.GetComponent<TextMeshProUGUI>();
            if (sequence != null)
            {
                sequence.text = definitions[index].IsPractice
                    ? "PRACTICE"
                    : $"STAGE {numberedStageIndex}";
            }
            if (!definitions[index].IsPractice)
                numberedStageIndex++;
        }

        RefreshStageTrack(true);
        return TryBindSavedStageUi(out error);
    }

    internal static bool TryReorderSavedStageChildrenForEditor(
        RectTransform content,
        IReadOnlyList<Transform> orderedNodes,
        IReadOnlyList<string> expectedConnectorNames,
        out string error)
    {
        error = string.Empty;
        if (content == null || orderedNodes == null ||
            expectedConnectorNames == null ||
            expectedConnectorNames.Count !=
            Mathf.Max(0, orderedNodes.Count - 1))
        {
            error = "saved Stage Select order is invalid.";
            return false;
        }

        for (int index = 0; index < orderedNodes.Count; index++)
        {
            if (orderedNodes[index] == null ||
                orderedNodes[index].parent != content)
            {
                error = "saved stage nodes do not belong to the Stage " +
                        "Select content.";
                return false;
            }
        }

        List<Transform> connectors = new();
        for (int index = 0; index < content.childCount; index++)
        {
            Transform child = content.GetChild(index);
            if (!child.name.StartsWith(
                    ConnectorPrefix,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (child.Find("imgLine")?.GetComponent<Image>() == null)
            {
                error = $"saved connector '{child.name}' is incomplete.";
                return false;
            }
            connectors.Add(child);
        }

        if (connectors.Count != expectedConnectorNames.Count)
        {
            error = $"saved connector count is {connectors.Count}, " +
                    $"expected {expectedConnectorNames.Count}.";
            return false;
        }

        Transform[] assigned = new Transform[connectors.Count];
        HashSet<Transform> used = new();
        for (int index = 0; index < expectedConnectorNames.Count; index++)
        {
            string expectedName = expectedConnectorNames[index];
            for (int candidateIndex = 0;
                 candidateIndex < connectors.Count;
                 candidateIndex++)
            {
                Transform candidate = connectors[candidateIndex];
                if (used.Contains(candidate) ||
                    !string.Equals(
                        candidate.name,
                        expectedName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                assigned[index] = candidate;
                used.Add(candidate);
                break;
            }
        }

        int fallbackIndex = 0;
        for (int index = 0; index < assigned.Length; index++)
        {
            if (assigned[index] != null)
                continue;
            while (fallbackIndex < connectors.Count &&
                   used.Contains(connectors[fallbackIndex]))
            {
                fallbackIndex++;
            }

            if (fallbackIndex >= connectors.Count)
            {
                error = "saved connectors could not be reassigned.";
                return false;
            }

            assigned[index] = connectors[fallbackIndex];
            used.Add(connectors[fallbackIndex]);
            fallbackIndex++;
        }

        for (int index = 0; index < assigned.Length; index++)
        {
            assigned[index].name = ConnectorPrefix +
                                   "sync_pending_" +
                                   index.ToString("D2") + "_" +
                                   assigned[index].GetInstanceID();
        }
        for (int index = 0; index < assigned.Length; index++)
            assigned[index].name = expectedConnectorNames[index];

        int siblingIndex = 0;
        for (int index = 0; index < orderedNodes.Count; index++)
        {
            if (index > 0)
                assigned[index - 1].SetSiblingIndex(siblingIndex++);
            orderedNodes[index].SetSiblingIndex(siblingIndex++);
        }

        return true;
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
