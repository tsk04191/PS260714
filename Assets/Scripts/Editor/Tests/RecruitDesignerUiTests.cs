using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RecruitDesignerUiTests
{
    [Test]
    public void RecruitPreview_CreatesDesignerOwnedFixedSceneUi()
    {
        GameObject pageObject = CreateRecruitPage(
            out MainSubPage page);
        try
        {
            bool synchronized =
                page.SyncRecruitEditorPreview(
                    0,
                    0,
                    out string error);

            Assert.That(synchronized, Is.True, error);
            RecruitBannerDesignerBindings banner =
                pageObject.GetComponentInChildren<
                    RecruitBannerDesignerBindings>(true);
            RecruitRevealDesignerBindings reveal =
                pageObject.GetComponentInChildren<
                    RecruitRevealDesignerBindings>(true);
            Assert.That(banner, Is.Not.Null);
            Assert.That(banner.HasDesignerLayout, Is.True);
            Assert.That(banner.HasRequiredReferences, Is.True);
            Assert.That(reveal, Is.Not.Null);
            Assert.That(reveal.HasDesignerLayout, Is.True);
            Assert.That(reveal.HasRequiredReferences, Is.True);
            Assert.That(reveal.ResultRows.Count, Is.EqualTo(10));
            for (int index = 0;
                 index < reveal.ResultRows.Count;
                 index++)
            {
                Assert.That(
                    reveal.ResultRows[index].Find("imgRewardIcon"),
                    Is.Not.Null);
            }
        }
        finally
        {
            Object.DestroyImmediate(pageObject);
        }
    }

    [Test]
    public void RecruitPreview_RebindsRuntimeWrappersAfterScriptReload()
    {
        GameObject pageObject = CreateRecruitPage(
            out MainSubPage page);
        try
        {
            Assert.That(
                page.SyncRecruitEditorPreview(
                    0,
                    0,
                    out string firstError),
                Is.True,
                firstError);

            SetPrivateField(page, "_recruitBannerView", null);
            SetPrivateField(page, "_recruitRevealOverlay", null);

            Assert.That(
                page.SyncRecruitEditorPreview(
                    0,
                    0,
                    out string rebindError),
                Is.True,
                rebindError);
        }
        finally
        {
            Object.DestroyImmediate(pageObject);
        }
    }

    [Test]
    public void RecruitPreview_CanShowTenResultsAndHideAgain()
    {
        GameObject pageObject = CreateRecruitPage(
            out MainSubPage page);
        try
        {
            Assert.That(
                page.SyncRecruitEditorPreview(
                    0,
                    10,
                    out string showError),
                Is.True,
                showError);
            RecruitRevealDesignerBindings reveal =
                pageObject.GetComponentInChildren<
                    RecruitRevealDesignerBindings>(true);
            Assert.That(reveal, Is.Not.Null);
            Assert.That(reveal.gameObject.activeSelf, Is.True);
            for (int index = 0;
                 index < reveal.ResultRows.Count;
                 index++)
            {
                Assert.That(
                    reveal.ResultRows[index].gameObject.activeSelf,
                    Is.True);
            }

            Assert.That(
                page.SyncRecruitEditorPreview(
                    0,
                    0,
                    out string hideError),
                Is.True,
                hideError);
            Assert.That(reveal.gameObject.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(pageObject);
        }
    }

    [Test]
    public void DesignerOwnedBanner_DoesNotRestoreDefaultRectOnRebind()
    {
        GameObject pageObject = CreateRecruitPage(
            out MainSubPage page);
        try
        {
            Assert.That(
                page.SyncRecruitEditorPreview(
                    0,
                    0,
                    out string error),
                Is.True,
                error);
            RecruitBannerDesignerBindings banner =
                pageObject.GetComponentInChildren<
                    RecruitBannerDesignerBindings>(true);
            Assert.That(banner, Is.Not.Null);
            Vector2 designerPosition = new(37f, -29f);
            banner.Root.anchoredPosition = designerPosition;

            Transform buttonRoot = pageObject.transform.Find(
                RuntimeMenuPageBase.RuntimeRootObjectName +
                "/grpMenuPanel/grpMenuButtons");
            Assert.That(buttonRoot, Is.Not.Null);
            RecruitBannerView.Build(buttonRoot);

            Assert.That(
                banner.Root.anchoredPosition,
                Is.EqualTo(designerPosition));
        }
        finally
        {
            Object.DestroyImmediate(pageObject);
        }
    }

    private static GameObject CreateRecruitPage(
        out MainSubPage page)
    {
        GameObject pageObject = new(
            "pagRecruitTest",
            typeof(RectTransform));
        pageObject.SetActive(false);
        page = pageObject.AddComponent<MainSubPage>();
        SerializedObject serialized = new(page);
        serialized.FindProperty("pageType").enumValueIndex =
            (int)EMainSubPageType.Recruit;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        pageObject.SetActive(true);
        pageObject.SetActive(false);
        return pageObject;
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
