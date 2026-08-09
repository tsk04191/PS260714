using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RecruitDesignerUiTests
{
    [Test]
    public void RecruitScene_HasDesignerOwnedFixedUi()
    {
        Scene scene = OpenClientScene(out bool opened);
        try
        {
            MainSubPage page = FindRecruitPage(scene);
            Assert.That(page, Is.Not.Null);
            Assert.That(
                page.ValidateRecruitEditorUi(out string error),
                Is.True,
                error);

            RecruitBannerDesignerBindings banner =
                page.GetComponentInChildren<
                    RecruitBannerDesignerBindings>(true);
            RecruitRevealDesignerBindings reveal =
                page.GetComponentInChildren<
                    RecruitRevealDesignerBindings>(true);
            Assert.That(banner.HasRequiredReferences, Is.True);
            Assert.That(reveal.HasRequiredReferences, Is.True);
            Assert.That(reveal.ResultRows.Count, Is.EqualTo(10));
            foreach (RectTransform row in reveal.ResultRows)
                Assert.That(row.Find("imgRewardIcon"), Is.Not.Null);
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void RecruitPreview_RebindsSavedRuntimeWrappers()
    {
        Scene scene = OpenClientScene(out bool opened);
        try
        {
            MainSubPage page = FindRecruitPage(scene);
            Assert.That(page, Is.Not.Null);
            Assert.That(
                page.SyncRecruitEditorPreview(0, 0, out string firstError),
                Is.True,
                firstError);

            SetPrivateField(page, "_recruitBannerView", null);
            SetPrivateField(page, "_recruitRevealOverlay", null);
            Assert.That(
                page.SyncRecruitEditorPreview(0, 0, out string rebindError),
                Is.True,
                rebindError);
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void RecruitPreview_CanShowTenSavedRowsAndHideAgain()
    {
        Scene scene = OpenClientScene(out bool opened);
        try
        {
            MainSubPage page = FindRecruitPage(scene);
            Assert.That(
                page.SyncRecruitEditorPreview(0, 10, out string showError),
                Is.True,
                showError);
            RecruitRevealDesignerBindings reveal =
                page.GetComponentInChildren<
                    RecruitRevealDesignerBindings>(true);
            Assert.That(reveal.gameObject.activeSelf, Is.True);
            Assert.That(
                reveal.ResultRows.All(row => row.gameObject.activeSelf),
                Is.True);

            Assert.That(
                page.SyncRecruitEditorPreview(0, 0, out string hideError),
                Is.True,
                hideError);
            Assert.That(reveal.gameObject.activeSelf, Is.False);
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void DesignerOwnedBanner_DoesNotRestoreDefaultRectOnRebind()
    {
        Scene scene = OpenClientScene(out bool opened);
        try
        {
            MainSubPage page = FindRecruitPage(scene);
            RecruitBannerDesignerBindings banner =
                page.GetComponentInChildren<
                    RecruitBannerDesignerBindings>(true);
            Vector2 designerPosition = new(37f, -29f);
            banner.Root.anchoredPosition = designerPosition;

            RecruitBannerView.Build(banner.Root.parent);
            Assert.That(
                banner.Root.anchoredPosition,
                Is.EqualTo(designerPosition));
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void RecruitRuntimeBinding_DoesNotCreateMissingSceneUi()
    {
        GameObject host = new(
            "RecruitRuntimeBindingHost",
            typeof(RectTransform));
        try
        {
            Assert.Throws<System.InvalidOperationException>(
                () => RecruitBannerView.Build(host.transform));
            Assert.That(
                host.transform.Find("grpRecruitBannerView"),
                Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    private static Scene OpenClientScene(out bool opened)
    {
        const string path = "Assets/Scenes/ClientScene.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        opened = !scene.IsValid() || !scene.isLoaded;
        return opened
            ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)
            : scene;
    }

    private static MainSubPage FindRecruitPage(Scene scene)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root =>
                root.GetComponentsInChildren<MainSubPage>(true))
            .FirstOrDefault(page => page.IsRecruitPage);
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
