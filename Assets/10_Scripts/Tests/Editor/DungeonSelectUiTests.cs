using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class DungeonSelectUiTests
{
    [Test]
    public void ClientScene_HasSerializedTwoLevelDungeonSelect()
    {
        Scene scene = SceneManager.GetSceneByPath(
            DungeonSelectUiInstaller.ClientScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened)
        {
            scene = EditorSceneManager.OpenScene(
                DungeonSelectUiInstaller.ClientScenePath,
                OpenSceneMode.Additive);
        }

        try
        {
            Assert.That(DungeonSelectUiInstaller.ValidateScene(scene), Is.Empty);
            StageSelectPage page = FindOne<StageSelectPage>(scene);
            Assert.That(page, Is.Not.Null);
            Assert.That(
                FindDescendant(page.transform,
                    DungeonSelectUiInstaller.CategoryViewName),
                Is.Not.Null);
            Assert.That(
                FindDescendant(page.transform,
                    DungeonSelectUiInstaller.DungeonViewName),
                Is.Not.Null);
            Assert.That(
                page.GetComponentsInChildren<Transform>(true)
                    .Any(value => value.name.StartsWith("btnStage_")),
                Is.False);
            UiMaskedCoverImageView[] covers =
                page.GetComponentsInChildren<UiMaskedCoverImageView>(true);
            Assert.That(covers.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(covers.All(value => value.HasDesignerReferences),
                Is.True);
            Assert.That(
                covers.All(value =>
                    value.Viewport.GetComponent<RectMask2D>() != null &&
                    !value.Artwork.raycastTarget),
                Is.True);

            SerializedObject pageSerialized = new(page);
            UiMaskedCoverImageView backdrop = pageSerialized
                .FindProperty("backdropView")
                .objectReferenceValue as UiMaskedCoverImageView;
            UiMaskedCoverImageView detail = pageSerialized
                .FindProperty("detailHeroView")
                .objectReferenceValue as UiMaskedCoverImageView;
            Material blur = AssetDatabase.LoadAssetAtPath<Material>(
                DungeonSelectUiInstaller.BackdropBlurMaterialPath);
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(detail, Is.Not.Null);
            Assert.That(blur, Is.Not.Null);
            Assert.That(
                blur.shader.name,
                Is.EqualTo(
                    DungeonSelectUiInstaller.BackdropBlurShaderName));
            Assert.That(backdrop.Artwork.material, Is.SameAs(blur));
            Assert.That(
                detail.Viewport.rect.size,
                Is.EqualTo(
                    DungeonSelectArtworkLayout.DetailCoverViewportSize));
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void CategoryCard_RoutesPreviewAndOpenWithoutDuplicateListeners()
    {
        DungeonSelectCategoryCardView prefab =
            AssetDatabase.LoadAssetAtPath<DungeonSelectCategoryCardView>(
                DungeonSelectUiInstaller.CategoryPrefabPath);
        DungeonCategorySO category =
            AssetDatabase.LoadAssetAtPath<DungeonCategorySO>(
                "Assets/06_Runtime/Resources/DungeonCategories/" +
                "DebugRoom.asset");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(category, Is.Not.Null);

        DungeonSelectCategoryCardView instance =
            Object.Instantiate(prefab);
        try
        {
            int previews = 0;
            int opens = 0;
            instance.Configure(category, _ => previews++, _ => opens++);
            instance.Configure(category, _ => previews++, _ => opens++);

            instance.OnPointerEnter(null);
            instance.Button.onClick.Invoke();

            Assert.That(previews, Is.EqualTo(1));
            Assert.That(opens, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(instance.gameObject);
        }
    }

    [Test]
    public void CategoryCard_UsesMaskedCoverWithNonBlockingArtwork()
    {
        DungeonSelectCategoryCardView prefab =
            AssetDatabase.LoadAssetAtPath<DungeonSelectCategoryCardView>(
                DungeonSelectUiInstaller.CategoryPrefabPath);

        Assert.That(prefab, Is.Not.Null);
        UiMaskedCoverImageView cover =
            prefab.GetComponentInChildren<UiMaskedCoverImageView>(true);
        Assert.That(cover, Is.Not.Null);
        Assert.That(cover.HasDesignerReferences, Is.True);
        Assert.That(cover.Viewport.GetComponent<RectMask2D>(), Is.Not.Null);
        Assert.That(cover.Artwork.raycastTarget, Is.False);
    }

    [Test]
    public void MaskedCoverGeometry_FillsViewportAndClampsFocus()
    {
        Vector2 viewport = new(1920f, 1080f);
        Vector2 rendered = UiMaskedCoverImageView.CalculateRenderedSize(
            viewport,
            new Vector2(1024f, 1024f),
            1f);

        Assert.That(rendered, Is.EqualTo(new Vector2(1920f, 1920f)));
        Vector2 anchored = UiMaskedCoverImageView.CalculateAnchoredPosition(
            viewport,
            rendered,
            new Vector2(1f, 1f));
        Assert.That(anchored.x, Is.Zero.Within(0.001f));
        Assert.That(anchored.y, Is.EqualTo(-420f).Within(0.001f));
        Rect visible = UiMaskedCoverImageView.CalculateVisibleSourceRect(
            viewport,
            rendered,
            anchored);
        Assert.That(visible.width, Is.EqualTo(1f).Within(0.001f));
        Assert.That(visible.height, Is.EqualTo(0.5625f).Within(0.001f));
        Assert.That(visible.yMin, Is.EqualTo(0.4375f).Within(0.001f));
    }

    [Test]
    public void FramingPreview_UsesExactRuntimeDetailCoverViewport()
    {
        Texture2D texture = new(1920, 1080);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1920f, 1080f),
            new Vector2(0.5f, 0.5f));
        try
        {
            Vector2 viewport =
                DungeonSelectArtworkLayout.DetailCoverViewportSize;
            Vector2 focus = new(0.68f, 0.37f);
            const float zoom = 1.35f;
            Rect editorVisible =
                UiArtworkFramingEditorGUI.ResolveVisibleSourceRect(
                    sprite,
                    viewport,
                    focus,
                    zoom);
            Vector2 rendered =
                UiMaskedCoverImageView.CalculateRenderedSize(
                    viewport,
                    sprite.rect.size,
                    zoom);
            Vector2 anchored =
                UiMaskedCoverImageView.CalculateAnchoredPosition(
                    viewport,
                    rendered,
                    focus);
            Rect runtimeVisible =
                UiMaskedCoverImageView.CalculateVisibleSourceRect(
                    viewport,
                    rendered,
                    anchored);

            Assert.That(editorVisible.x, Is.EqualTo(runtimeVisible.x));
            Assert.That(editorVisible.y, Is.EqualTo(runtimeVisible.y));
            Assert.That(editorVisible.width,
                Is.EqualTo(runtimeVisible.width));
            Assert.That(editorVisible.height,
                Is.EqualTo(runtimeVisible.height));
            Assert.That(
                viewport.x / viewport.y,
                Is.EqualTo(3.4f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(value => value.name == name);
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
}
