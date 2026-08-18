using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PracticeBattleUiInstallerTests
{
    [Test]
    public void ExistingStageNodeUpgrade_PreservesAuthoredCoverColor()
    {
        GameObject node = new(
            "btnStage_existing",
            typeof(RectTransform));
        DungeonDefinition definition =
            ScriptableObject.CreateInstance<DungeonDefinition>();
        try
        {
            GameObject coverObject = CreateChild(
                node.transform,
                "imgStageCover");
            Image cover = coverObject.AddComponent<Image>();
            Color authoredColor = Color.white;
            cover.color = authoredColor;

            PracticeBattleUiInstaller.InitializeStageNode(
                node.transform,
                definition,
                0,
                false);

            Assert.That(cover.color, Is.EqualTo(authoredColor));
        }
        finally
        {
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(node);
        }
    }

    [Test]
    public void ClientScene_PracticeDebugUiIsSerializedAndNonBlocking()
    {
        Scene scene = SceneManager.GetSceneByPath(
            PracticeBattleUiInstaller.ClientScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened)
        {
            scene = EditorSceneManager.OpenScene(
                PracticeBattleUiInstaller.ClientScenePath,
                OpenSceneMode.Additive);
        }

        try
        {
            Assert.That(
                PracticeBattleUiInstaller.ValidateScene(scene),
                Is.Empty);

            DungeonBattleTab tab = FindOne<DungeonBattleTab>(scene);
            Assert.That(tab, Is.Not.Null);
            PracticeBattlePanelView panel = tab.PracticeBattlePanel;
            Assert.That(panel, Is.Not.Null);
            SerializedObject panelSerialized = new(panel);
            Button debugButton = panelSerialized.FindProperty("debugButton")
                .objectReferenceValue as Button;
            TextMeshProUGUI debugText = panelSerialized
                .FindProperty("debugButtonText")
                .objectReferenceValue as TextMeshProUGUI;
            Assert.That(debugButton, Is.Not.Null);
            Assert.That(
                debugButton.name,
                Is.EqualTo(
                    PracticeBattleUiInstaller.PracticeDebugButtonName));
            Assert.That(debugText, Is.Not.Null);
            Assert.That(
                debugText.transform.IsChildOf(debugButton.transform),
                Is.True);

            DungeonBoardView board = FindOne<DungeonBoardView>(scene);
            Assert.That(board, Is.Not.Null);
            SerializedObject boardSerialized = new(board);
            DungeonWorldInputView input = boardSerialized
                .FindProperty("worldInputView")
                .objectReferenceValue as DungeonWorldInputView;
            PracticeBattleDebugOverlayView overlay = boardSerialized
                .FindProperty("practiceDebugOverlay")
                .objectReferenceValue as PracticeBattleDebugOverlayView;
            Assert.That(input, Is.Not.Null);
            Assert.That(input.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(overlay, Is.Not.Null);
            Assert.That(
                overlay.name,
                Is.EqualTo(
                    PracticeBattleUiInstaller.PracticeDebugOverlayName));
            Assert.That(overlay.HasRequiredReferences, Is.True);
            Assert.That(overlay.IsVisible, Is.False);
            Assert.That(overlay.transform.parent, Is.SameAs(input.transform));

            SerializedObject overlaySerialized = new(overlay);
            PracticeBattleDebugOverlayGraphic graphic = overlaySerialized
                .FindProperty("overlayGraphic")
                .objectReferenceValue as
                    PracticeBattleDebugOverlayGraphic;
            Assert.That(graphic, Is.Not.Null);
            Assert.That(graphic.gameObject, Is.SameAs(overlay.gameObject));
            Assert.That(graphic.raycastTarget, Is.False);
            Assert.That(graphic.enabled, Is.False);
            Assert.That(
                graphic.GetComponent<CanvasRenderer>(),
                Is.Not.Null);

            RectTransform rect = overlay.transform as RectTransform;
            RectTransform inputRect = input.transform as RectTransform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));
            Assert.That(rect.pivot, Is.EqualTo(inputRect.pivot));
            Assert.That(
                rect.GetSiblingIndex(),
                Is.EqualTo(input.transform.childCount - 1));
            Assert.That(
                input.GetComponentsInChildren<Transform>(true).Count(
                    item => item.name ==
                            PracticeBattleUiInstaller
                                .PracticeDebugOverlayName),
                Is.EqualTo(1));
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExistingPanelUpgrade_AddsOnlyMissingDebugControlOnce()
    {
        GameObject root = new(
            "PracticeBattleUiInstallerTests_Root",
            typeof(RectTransform));
        root.SetActive(false);
        try
        {
            PracticeBattlePanelView panel =
                root.AddComponent<PracticeBattlePanelView>();
            GameObject body = CreateChild(root.transform, "grpPracticePanel");
            GameObject header = CreateChild(
                body.transform,
                "imgPracticeHeader");
            GameObject designerMarker = CreateChild(
                header.transform,
                "imgDesignerMarker");
            RectTransform markerRect =
                designerMarker.transform as RectTransform;
            markerRect.anchoredPosition = new Vector2(37f, -19f);

            PracticeBattleUiInstaller.UpgradePracticePanelDebugControls(
                panel);

            Transform debugTransform = header.transform.Find(
                PracticeBattleUiInstaller.PracticeDebugButtonName);
            Assert.That(debugTransform, Is.Not.Null);
            Button debugButton = debugTransform.GetComponent<Button>();
            TextMeshProUGUI debugText = debugTransform
                .GetComponentInChildren<TextMeshProUGUI>(true);
            Assert.That(debugButton, Is.Not.Null);
            Assert.That(debugText, Is.Not.Null);
            Assert.That(debugText.raycastTarget, Is.False);

            SerializedObject serialized = new(panel);
            Assert.That(
                serialized.FindProperty("debugButton")
                    .objectReferenceValue,
                Is.SameAs(debugButton));
            Assert.That(
                serialized.FindProperty("debugButtonText")
                    .objectReferenceValue,
                Is.SameAs(debugText));
            Assert.That(
                markerRect.anchoredPosition,
                Is.EqualTo(new Vector2(37f, -19f)));

            int childCount = header.transform.childCount;
            PracticeBattleUiInstaller.UpgradePracticePanelDebugControls(
                panel);

            Assert.That(header.transform.childCount, Is.EqualTo(childCount));
            Assert.That(
                header.transform.Find(
                    PracticeBattleUiInstaller.PracticeDebugButtonName),
                Is.SameAs(debugTransform));
            Assert.That(
                markerRect.anchoredPosition,
                Is.EqualTo(new Vector2(37f, -19f)));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        GameObject child = new(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child;
    }

    private static T FindOne<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        return null;
    }
}
