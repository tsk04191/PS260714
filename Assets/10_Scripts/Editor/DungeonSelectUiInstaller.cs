using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DungeonSelectUiInstaller
{
    internal const string ClientScenePath =
        "Assets/04_Scenes/ClientScene.unity";
    internal const string CategoryPrefabPath =
        "Assets/07_Prefabs/UI/DungeonSelect/" +
        "btnDungeonCategoryCard.prefab";
    internal const string DungeonRowPrefabPath =
        "Assets/07_Prefabs/UI/DungeonSelect/btnDungeonRow.prefab";
    internal const string BackdropBlurMaterialPath =
        "Assets/07_Prefabs/UI/DungeonSelect/" +
        "matDungeonSelectBackdropBlur.mat";
    internal const string BackdropBlurShaderName =
        "PS260714/UI/Dungeon Select Backdrop Blur";
    internal const string BackdropName = "grpDungeonSelectBackdropViewport";
    private const string LegacyBackdropName = "imgDungeonSelectBackdrop";
    internal const string BreadcrumbName = "txtDungeonBreadcrumb";
    internal const string CategoryViewName = "grpDungeonCategories";
    internal const string DungeonViewName = "grpDungeonListAndDetail";

    private static readonly Color Dark =
        new(0.025f, 0.032f, 0.03f, 0.96f);
    private static readonly Color Panel =
        new(0.045f, 0.055f, 0.052f, 0.94f);
    private static readonly Color Light =
        new(0.91f, 0.93f, 0.88f, 1f);
    private static readonly Color Muted =
        new(0.62f, 0.67f, 0.64f, 1f);
    private static readonly Color Accent =
        new(0.82f, 0.42f, 0.36f, 1f);
    private static readonly Color BackdropArtworkTint =
        new(0.74f, 0.76f, 0.78f, 0.82f);
    private const float BackdropBlurRadius = 8f;

    [MenuItem("PS260714/UI/Install Dungeon Select UI", false, 118)]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "Dungeon Select UI cannot be installed in Play Mode.");
        }

        EnsurePrefabs();
        Scene scene = EditorSceneManager.OpenScene(
            ClientScenePath,
            OpenSceneMode.Single);
        InstallIntoScene(scene);
        IReadOnlyList<string> issues = ValidateScene(scene);
        if (issues.Count > 0)
        {
            throw new InvalidOperationException(
                "Dungeon Select UI validation failed:\n- " +
                string.Join("\n- ", issues));
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ClientScenePath))
        {
            throw new InvalidOperationException(
                "Failed to save ClientScene Dungeon Select UI.");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Installed serialized two-level Dungeon Select UI.");
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

    internal static void EnsurePrefabs()
    {
        EnsureAssetFolder("Assets/07_Prefabs/UI/DungeonSelect");
        EnsureBackdropBlurMaterial();
        EnsureCategoryPrefab();
        EnsureDungeonRowPrefab();
    }

    internal static void InstallIntoScene(Scene scene)
    {
        StageSelectPage page = FindOne<StageSelectPage>(scene);
        Require(page, "StageSelectPage");
        EnsurePrefabs();

        if (page.TryValidateDesignerReferences(out _))
        {
            UpgradeBackdropPresentation(page);
            return;
        }

        SerializedObject pageSerialized = new(page);
        RectTransform runtimeRoot = GetReference<RectTransform>(
            pageSerialized,
            "_runtimeRoot");
        RectTransform buttonRoot = GetReference<RectTransform>(
            pageSerialized,
            "_buttonRoot");
        RectTransform panelRoot = GetReference<RectTransform>(
            pageSerialized,
            "_panel");
        Require(runtimeRoot, "StageSelect runtime root");
        Require(buttonRoot, "StageSelect button root");

        Image panelImage = panelRoot != null
            ? panelRoot.GetComponent<Image>()
            : null;
        if (panelImage != null)
        {
            Color panelColor = panelImage.color;
            panelColor.a = 0.68f;
            panelImage.color = panelColor;
        }

        RemoveOwnedChild(runtimeRoot, BackdropName);
        RemoveOwnedChild(runtimeRoot, LegacyBackdropName);
        RemoveOwnedChild(runtimeRoot, BreadcrumbName);
        ClearChildren(buttonRoot);

        UiMaskedCoverImageView backdrop = CreateMaskedCoverView(
            BackdropName,
            runtimeRoot,
            new Color(0.03f, 0.035f, 0.04f, 0.56f));
        SetStretch(backdrop.Viewport, 0f, 0f, 0f, 0f);
        ApplyBackdropPresentation(backdrop);
        backdrop.transform.SetAsFirstSibling();

        TextMeshProUGUI breadcrumb = CreateText(
            BreadcrumbName,
            runtimeRoot,
            "DUNGEON / SELECT",
            18f,
            TextAlignmentOptions.MidlineLeft,
            Light);
        SetRect(
            breadcrumb.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(54f, -30f),
            new Vector2(-108f, 34f));

        GameObject categoryView = CreateRectObject(
            CategoryViewName,
            buttonRoot);
        SetStretch(categoryView.transform as RectTransform, 0f, 0f, 0f, 0f);
        ScrollRect categoryScroll = CreateHorizontalCategoryScroll(
            categoryView.transform,
            out RectTransform categoryContent);

        GameObject dungeonView = CreateRectObject(
            DungeonViewName,
            buttonRoot);
        SetStretch(dungeonView.transform as RectTransform, 0f, 0f, 0f, 0f);
        BuildDungeonBrowser(
            dungeonView.transform,
            out TextMeshProUGUI categoryTitle,
            out ScrollRect dungeonScroll,
            out RectTransform dungeonContent,
            out UiMaskedCoverImageView hero,
            out TextMeshProUGUI detailCategory,
            out TextMeshProUGUI detailTitle,
            out TextMeshProUGUI detailDescription,
            out TextMeshProUGUI detailRules,
            out TextMeshProUGUI detailProgress,
            out Button enter,
            out TextMeshProUGUI enterText);
        dungeonView.SetActive(false);

        DungeonSelectCategoryCardView categoryPrefab =
            AssetDatabase.LoadAssetAtPath<DungeonSelectCategoryCardView>(
                CategoryPrefabPath);
        DungeonSelectDungeonRowView rowPrefab =
            AssetDatabase.LoadAssetAtPath<DungeonSelectDungeonRowView>(
                DungeonRowPrefabPath);
        Require(categoryPrefab, "Dungeon category card prefab");
        Require(rowPrefab, "Dungeon row prefab");

        SetReference(pageSerialized, "backdropView", backdrop);
        SetReference(pageSerialized, "breadcrumbText", breadcrumb);
        SetReference(pageSerialized, "categoryView", categoryView);
        SetReference(pageSerialized, "categoryScroll", categoryScroll);
        SetReference(pageSerialized, "categoryContent", categoryContent);
        SetReference(pageSerialized, "categoryCardPrefab", categoryPrefab);
        SetReference(pageSerialized, "dungeonView", dungeonView);
        SetReference(pageSerialized, "categoryTitleText", categoryTitle);
        SetReference(pageSerialized, "dungeonScroll", dungeonScroll);
        SetReference(pageSerialized, "dungeonContent", dungeonContent);
        SetReference(pageSerialized, "dungeonRowPrefab", rowPrefab);
        SetReference(pageSerialized, "detailHeroView", hero);
        SetReference(pageSerialized, "detailCategoryText", detailCategory);
        SetReference(pageSerialized, "detailTitleText", detailTitle);
        SetReference(
            pageSerialized,
            "detailDescriptionText",
            detailDescription);
        SetReference(pageSerialized, "detailRulesText", detailRules);
        SetReference(pageSerialized, "detailProgressText", detailProgress);
        SetReference(pageSerialized, "enterButton", enter);
        SetReference(pageSerialized, "enterButtonText", enterText);
        pageSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(page);
    }

    internal static IReadOnlyList<string> ValidateScene(Scene scene)
    {
        List<string> issues = new();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            issues.Add("ClientScene is not loaded.");
            return issues;
        }

        StageSelectPage page = FindOne<StageSelectPage>(scene);
        if (page == null)
        {
            issues.Add("StageSelectPage is missing.");
        }
        else if (!page.TryValidateDesignerReferences(out string error))
        {
            issues.Add(error);
        }

        DungeonSelectCategoryCardView categoryPrefab =
            AssetDatabase.LoadAssetAtPath<DungeonSelectCategoryCardView>(
                CategoryPrefabPath);
        if (categoryPrefab == null || !categoryPrefab.HasDesignerReferences)
            issues.Add("Dungeon category card prefab is missing or invalid.");

        DungeonSelectDungeonRowView rowPrefab =
            AssetDatabase.LoadAssetAtPath<DungeonSelectDungeonRowView>(
                DungeonRowPrefabPath);
        if (rowPrefab == null || !rowPrefab.HasDesignerReferences)
            issues.Add("Dungeon row prefab is missing or invalid.");

        if (page != null)
        {
            ValidateBackdropPresentation(page, issues);
            ValidateDetailCoverViewport(page, issues);
            Transform category = FindDescendant(page.transform, CategoryViewName);
            Transform dungeon = FindDescendant(page.transform, DungeonViewName);
            if (category == null)
                issues.Add("Dungeon category view is missing.");
            if (dungeon == null)
                issues.Add("Dungeon list/detail view is missing.");
            if (FindDescendant(page.transform, "btnStage_tutorial_field") != null ||
                FindDescendant(page.transform, "scrStageTrack") != null)
            {
                issues.Add("Legacy flat Stage Select track remains.");
            }
        }

        return issues;
    }

    private static void UpgradeBackdropPresentation(StageSelectPage page)
    {
        SerializedObject serialized = new(page);
        UiMaskedCoverImageView backdrop = GetReference<
            UiMaskedCoverImageView>(serialized, "backdropView");
        Require(backdrop, "Dungeon Select backdropView");
        ApplyBackdropPresentation(backdrop);
        EditorUtility.SetDirty(page);
    }

    private static void ApplyBackdropPresentation(
        UiMaskedCoverImageView backdrop)
    {
        Require(backdrop, "Dungeon Select backdropView");
        Require(backdrop.Artwork, "Dungeon Select backdrop artwork");
        Material material = EnsureBackdropBlurMaterial();
        backdrop.Artwork.material = material;
        backdrop.Artwork.color = BackdropArtworkTint;
        EditorUtility.SetDirty(backdrop.Artwork);
        EditorUtility.SetDirty(backdrop);
    }

    private static void ValidateBackdropPresentation(
        StageSelectPage page,
        ICollection<string> issues)
    {
        SerializedObject serialized = new(page);
        UiMaskedCoverImageView backdrop = GetReference<
            UiMaskedCoverImageView>(serialized, "backdropView");
        Material expected = AssetDatabase.LoadAssetAtPath<Material>(
            BackdropBlurMaterialPath);
        if (expected == null || expected.shader == null ||
            !string.Equals(
                expected.shader.name,
                BackdropBlurShaderName,
                StringComparison.Ordinal))
        {
            issues.Add("Dungeon Select backdrop blur material is invalid.");
            return;
        }
        if (backdrop == null || backdrop.Artwork == null ||
            !ReferenceEquals(backdrop.Artwork.material, expected))
        {
            issues.Add("Dungeon Select backdrop blur material is unbound.");
        }
    }

    private static void ValidateDetailCoverViewport(
        StageSelectPage page,
        ICollection<string> issues)
    {
        SerializedObject serialized = new(page);
        UiMaskedCoverImageView detail = GetReference<
            UiMaskedCoverImageView>(serialized, "detailHeroView");
        if (detail == null || detail.Viewport == null)
        {
            issues.Add("Dungeon Select detail cover is unbound.");
            return;
        }

        Vector2 actual = detail.Viewport.rect.size;
        Vector2 expected =
            DungeonSelectArtworkLayout.DetailCoverViewportSize;
        if (Mathf.Abs(actual.x - expected.x) > 0.1f ||
            Mathf.Abs(actual.y - expected.y) > 0.1f)
        {
            issues.Add(
                $"Dungeon Select detail cover viewport is {actual.x:0.#} x " +
                $"{actual.y:0.#}; the framing editor expects " +
                $"{expected.x:0.#} x {expected.y:0.#}.");
        }
    }

    private static Material EnsureBackdropBlurMaterial()
    {
        Shader shader = Shader.Find(BackdropBlurShaderName);
        if (shader == null)
        {
            throw new InvalidOperationException(
                "Dungeon Select backdrop blur shader is missing.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            BackdropBlurMaterialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "matDungeonSelectBackdropBlur",
            };
            AssetDatabase.CreateAsset(material, BackdropBlurMaterialPath);
        }
        else if (!ReferenceEquals(material.shader, shader))
        {
            material.shader = shader;
        }
        material.SetFloat("_BlurRadius", BackdropBlurRadius);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureCategoryPrefab()
    {
        DungeonSelectCategoryCardView existing =
            AssetDatabase.LoadAssetAtPath<DungeonSelectCategoryCardView>(
                CategoryPrefabPath);
        if (existing != null && existing.HasDesignerReferences)
            return;

        GameObject root = CreateRectObject("btnDungeonCategoryCard", null);
        RectTransform rect = root.transform as RectTransform;
        rect.sizeDelta = new Vector2(
            DungeonSelectArtworkLayout.CategoryCardViewportSize.x,
            540f);
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.preferredWidth =
            DungeonSelectArtworkLayout.CategoryCardViewportSize.x;
        layout.preferredHeight = 540f;
        Image hit = root.AddComponent<Image>();
        hit.color = Dark;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = hit;

        UiMaskedCoverImageView cover = CreateMaskedCoverView(
            "grpCoverViewport",
            root.transform,
            Dark);
        SetRect(
            cover.Viewport,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(
                0f,
                DungeonSelectArtworkLayout.CategoryCardViewportSize.y));

        Image info = CreateImage("imgInformation", root.transform, Dark);
        SetRect(
            info.rectTransform,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0f),
            Vector2.zero,
            new Vector2(
                0f,
                -DungeonSelectArtworkLayout.CategoryCardViewportSize.y));
        info.raycastTarget = false;

        Image selection = CreateImage(
            "imgSelectionBar",
            root.transform,
            Accent);
        SetRect(
            selection.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            Vector2.zero,
            new Vector2(5f, 0f));
        selection.raycastTarget = false;

        TextMeshProUGUI title = CreateText(
            "txtTitle",
            info.transform,
            "CATEGORY",
            28f,
            TextAlignmentOptions.TopLeft,
            Light);
        SetRect(
            title.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(28f, -24f),
            new Vector2(-82f, 44f));
        title.fontStyle = FontStyles.Bold;

        TextMeshProUGUI count = CreateText(
            "txtCount",
            info.transform,
            "00",
            18f,
            TextAlignmentOptions.TopRight,
            Muted);
        SetRect(
            count.rectTransform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-24f, -28f),
            new Vector2(48f, 32f));

        TextMeshProUGUI description = CreateText(
            "txtDescription",
            info.transform,
            "CATEGORY DESCRIPTION",
            18f,
            TextAlignmentOptions.TopLeft,
            Muted);
        SetStretch(description.rectTransform, 28f, 24f, 24f, 76f);
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Ellipsis;

        DungeonSelectCategoryCardView view =
            root.AddComponent<DungeonSelectCategoryCardView>();
        SerializedObject serialized = new(view);
        SetReference(serialized, "button", button);
        SetReference(serialized, "coverView", cover);
        SetReference(serialized, "informationPanel", info);
        SetReference(serialized, "selectionBar", selection);
        SetReference(serialized, "titleText", title);
        SetReference(serialized, "descriptionText", description);
        SetReference(serialized, "countText", count);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, CategoryPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static void EnsureDungeonRowPrefab()
    {
        DungeonSelectDungeonRowView existing =
            AssetDatabase.LoadAssetAtPath<DungeonSelectDungeonRowView>(
                DungeonRowPrefabPath);
        if (existing != null && existing.HasDesignerReferences)
            return;

        GameObject root = CreateRectObject("btnDungeonRow", null);
        RectTransform rect = root.transform as RectTransform;
        rect.sizeDelta = new Vector2(420f, 74f);
        LayoutElement layout = root.AddComponent<LayoutElement>();
        layout.preferredWidth = 420f;
        layout.preferredHeight = 74f;
        Image background = root.AddComponent<Image>();
        background.color = Dark;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = background;

        Image selection = CreateImage(
            "imgSelectionBar",
            root.transform,
            Accent);
        SetRect(
            selection.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            Vector2.zero,
            new Vector2(4f, 0f));
        selection.raycastTarget = false;

        TextMeshProUGUI sequence = CreateText(
            "txtSequence",
            root.transform,
            "01",
            18f,
            TextAlignmentOptions.MidlineLeft,
            Muted);
        SetRect(
            sequence.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(22f, 0f),
            new Vector2(48f, 0f));

        TextMeshProUGUI title = CreateText(
            "txtTitle",
            root.transform,
            "DUNGEON",
            20f,
            TextAlignmentOptions.MidlineLeft,
            Light);
        SetRect(
            title.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(76f, 0f),
            new Vector2(-190f, 0f));

        TextMeshProUGUI state = CreateText(
            "txtState",
            root.transform,
            "NOT CLEARED",
            12f,
            TextAlignmentOptions.MidlineRight,
            Muted);
        SetRect(
            state.rectTransform,
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0.5f),
            new Vector2(-18f, 0f),
            new Vector2(150f, 0f));

        DungeonSelectDungeonRowView view =
            root.AddComponent<DungeonSelectDungeonRowView>();
        SerializedObject serialized = new(view);
        SetReference(serialized, "button", button);
        SetReference(serialized, "background", background);
        SetReference(serialized, "selectionBar", selection);
        SetReference(serialized, "sequenceText", sequence);
        SetReference(serialized, "titleText", title);
        SetReference(serialized, "stateText", state);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, DungeonRowPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static ScrollRect CreateHorizontalCategoryScroll(
        Transform parent,
        out RectTransform content)
    {
        GameObject root = CreateRectObject("scrDungeonCategories", parent);
        SetStretch(root.transform as RectTransform, 0f, 0f, 0f, 0f);
        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;

        GameObject viewport = CreateRectObject(
            "vptDungeonCategories",
            root.transform);
        SetStretch(viewport.transform as RectTransform, 0f, 0f, 0f, 0f);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
        viewport.AddComponent<RectMask2D>();

        GameObject contentObject = CreateRectObject(
            "grpDungeonCategoryContent",
            viewport.transform);
        content = contentObject.transform as RectTransform;
        SetRect(
            content,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            Vector2.zero,
            new Vector2(0f, 0f));
        HorizontalLayoutGroup layout =
            contentObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter =
            contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        scroll.viewport = viewport.transform as RectTransform;
        scroll.content = content;
        return scroll;
    }

    private static void BuildDungeonBrowser(
        Transform parent,
        out TextMeshProUGUI categoryTitle,
        out ScrollRect dungeonScroll,
        out RectTransform dungeonContent,
        out UiMaskedCoverImageView hero,
        out TextMeshProUGUI detailCategory,
        out TextMeshProUGUI detailTitle,
        out TextMeshProUGUI detailDescription,
        out TextMeshProUGUI detailRules,
        out TextMeshProUGUI detailProgress,
        out Button enter,
        out TextMeshProUGUI enterText)
    {
        Image listPanel = CreateImage("grpDungeonList", parent, Panel);
        SetRect(
            listPanel.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            Vector2.zero,
            new Vector2(440f, 0f));
        categoryTitle = CreateText(
            "txtCategoryTitle",
            listPanel.transform,
            "CATEGORY",
            28f,
            TextAlignmentOptions.MidlineLeft,
            Light);
        SetRect(
            categoryTitle.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(24f, -18f),
            new Vector2(-48f, 54f));

        dungeonScroll = CreateVerticalDungeonScroll(
            listPanel.transform,
            out dungeonContent);
        SetStretch(
            dungeonScroll.transform as RectTransform,
            10f,
            14f,
            10f,
            76f);

        Image detail = CreateImage("grpDungeonDetail", parent, Dark);
        SetStretch(
            detail.rectTransform,
            DungeonSelectArtworkLayout.DetailLeftInset,
            0f,
            0f,
            0f);

        hero = CreateMaskedCoverView(
            "grpDungeonHeroViewport",
            detail.transform,
            Panel);
        SetRect(
            hero.Viewport,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(
                0f,
                DungeonSelectArtworkLayout.DetailCoverHeight));

        detailCategory = CreateText(
            "txtDetailCategory",
            detail.transform,
            "CATEGORY",
            16f,
            TextAlignmentOptions.TopLeft,
            Muted);
        SetRect(
            detailCategory.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, -364f),
            new Vector2(-220f, 28f));

        detailProgress = CreateText(
            "txtDetailProgress",
            detail.transform,
            "NOT CLEARED",
            14f,
            TextAlignmentOptions.TopRight,
            Muted);
        SetRect(
            detailProgress.rectTransform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -364f),
            new Vector2(210f, 28f));

        detailTitle = CreateText(
            "txtDetailTitle",
            detail.transform,
            "DUNGEON",
            38f,
            TextAlignmentOptions.TopLeft,
            Light);
        SetRect(
            detailTitle.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, -398f),
            new Vector2(0f, 52f));
        detailTitle.fontStyle = FontStyles.Bold;

        detailDescription = CreateText(
            "txtDetailDescription",
            detail.transform,
            "DUNGEON DESCRIPTION",
            19f,
            TextAlignmentOptions.TopLeft,
            Light);
        SetRect(
            detailDescription.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, -458f),
            new Vector2(0f, 102f));
        detailDescription.textWrappingMode = TextWrappingModes.Normal;

        detailRules = CreateText(
            "txtDetailRules",
            detail.transform,
            "RULES",
            15f,
            TextAlignmentOptions.TopLeft,
            Muted);
        SetRect(
            detailRules.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 78f),
            new Vector2(-260f, 70f));
        detailRules.textWrappingMode = TextWrappingModes.Normal;

        enter = CreateButton(
            "btnEnterDungeon",
            detail.transform,
            Accent);
        SetRect(
            enter.transform as RectTransform,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 24f),
            new Vector2(230f, 58f));
        enterText = CreateText(
            "txtLabel",
            enter.transform,
            "ENTER DUNGEON",
            17f,
            TextAlignmentOptions.Center,
            Light);
        SetStretch(enterText.rectTransform, 12f, 4f, 12f, 4f);
    }

    private static ScrollRect CreateVerticalDungeonScroll(
        Transform parent,
        out RectTransform content)
    {
        GameObject root = CreateRectObject("scrDungeonList", parent);
        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;

        GameObject viewport = CreateRectObject("vptDungeonList", root.transform);
        SetStretch(viewport.transform as RectTransform, 0f, 0f, 0f, 0f);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
        viewport.AddComponent<RectMask2D>();

        GameObject contentObject = CreateRectObject(
            "grpDungeonListContent",
            viewport.transform);
        content = contentObject.transform as RectTransform;
        SetRect(
            content,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            Vector2.zero);
        VerticalLayoutGroup layout =
            contentObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter =
            contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.transform as RectTransform;
        scroll.content = content;
        return scroll;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        Color color)
    {
        GameObject root = CreateRectObject(name, parent);
        Image image = root.AddComponent<Image>();
        image.color = color;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static UiMaskedCoverImageView CreateMaskedCoverView(
        string name,
        Transform parent,
        Color fallbackColor)
    {
        GameObject root = CreateRectObject(name, parent);
        Image background = root.AddComponent<Image>();
        background.color = fallbackColor;
        background.raycastTarget = false;
        root.AddComponent<RectMask2D>();

        Image artwork = CreateImage(
            "imgArtwork",
            root.transform,
            Color.white);
        SetRect(
            artwork.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(100f, 100f));
        artwork.raycastTarget = false;
        artwork.enabled = false;

        UiMaskedCoverImageView view =
            root.AddComponent<UiMaskedCoverImageView>();
        SerializedObject serialized = new(view);
        SetReference(serialized, "viewport", root.transform as RectTransform);
        SetReference(serialized, "artwork", artwork);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return view;
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
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
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
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
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

    private static T GetReference<T>(
        SerializedObject serialized,
        string propertyName)
        where T : UnityEngine.Object
    {
        return serialized.FindProperty(propertyName)?.objectReferenceValue as T;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
    }

    private static void RemoveOwnedChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null)
            return null;
        Transform[] values = root.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < values.Length; index++)
        {
            if (string.Equals(values[index].name, name, StringComparison.Ordinal))
                return values[index];
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
            throw new InvalidOperationException(label + " is missing.");
    }
}
