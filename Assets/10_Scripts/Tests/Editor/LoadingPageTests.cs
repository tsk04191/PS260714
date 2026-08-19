using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LoadingPageTests
{
    [Test]
    public void ClientScene_LoadingPageIsSerializedAndBlocking()
    {
        Scene scene = SceneManager.GetSceneByPath(
            LoadingPageInstaller.ClientScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened)
        {
            scene = EditorSceneManager.OpenScene(
                LoadingPageInstaller.ClientScenePath,
                OpenSceneMode.Additive);
        }

        try
        {
            Assert.That(LoadingPageInstaller.ValidateScene(scene), Is.Empty);
            LoadingPage[] pages = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LoadingPage>(
                    true))
                .ToArray();
            Assert.That(pages, Has.Length.EqualTo(1));
            Assert.That(pages[0].HasDesignerReferences, Is.True);
            Assert.That(
                pages[0].GetComponent<Image>().raycastTarget,
                Is.True);
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void Installer_IsIdempotentAndKeepsSingleLoadingPage()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        GameObject popup = new("layPopup", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(popup, scene);
        try
        {
            LoadingPage first = LoadingPageInstaller.InstallIntoScene(scene);
            LoadingPage second = LoadingPageInstaller.InstallIntoScene(scene);

            Assert.That(second, Is.SameAs(first));
            Assert.That(LoadingPageInstaller.ValidateScene(scene), Is.Empty);
            Assert.That(
                popup.GetComponentsInChildren<LoadingPage>(true),
                Has.Length.EqualTo(1));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void LoadingTargets_AreLimitedToMaterializingPages()
    {
        Assert.That(IsLoadingTarget<StageSelectPage>(), Is.True);
        Assert.That(IsLoadingTarget<DungeonPage>(), Is.True);
        Assert.That(IsLoadingTarget<CharacterCodexPage>(), Is.True);
        Assert.That(IsLoadingTarget<EnemyCodexPage>(), Is.True);
        Assert.That(IsLoadingTarget<BattleCardCodexPage>(), Is.True);
        Assert.That(IsLoadingTarget<MainSubPage>(), Is.True);
        Assert.That(IsLoadingTarget<TitlePage>(), Is.False);
        Assert.That(IsLoadingTarget<MainPage>(), Is.False);
        Assert.That(IsLoadingTarget<SettingPage>(), Is.False);
    }

    [Test]
    public void DataManager_DefersCompletionUntilAfterStartupFrame()
    {
        MethodInfo start = typeof(DataManager).GetMethod(
            "Start",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(start, Is.Not.Null);
        Assert.That(start.ReturnType, Is.EqualTo(typeof(IEnumerator)));
    }

    private static bool IsLoadingTarget<T>()
    {
        return typeof(IPageLoadingTarget).IsAssignableFrom(typeof(T));
    }
}
