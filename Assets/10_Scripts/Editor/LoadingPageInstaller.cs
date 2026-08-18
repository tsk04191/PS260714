using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LoadingPageInstaller
{
    internal const string ClientScenePath =
        "Assets/04_Scenes/ClientScene.unity";
    internal const string RootName = "pagLoading";

    private static readonly Color BackdropColor =
        new(0.018f, 0.026f, 0.024f, 0.96f);
    private static readonly Color AccentColor =
        new(0.47f, 0.88f, 0.66f, 1f);

    [MenuItem("PS260714/UI/Install Loading Page", false, 119)]
    public static void Install()
    {
        Scene scene = EditorSceneManager.OpenScene(
            ClientScenePath,
            OpenSceneMode.Single);
        LoadingPage page = InstallIntoScene(scene);
        List<string> issues = ValidateScene(scene);
        if (issues.Count > 0)
        {
            throw new InvalidOperationException(
                "Loading page validation failed: " +
                string.Join(" ", issues));
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ClientScenePath))
        {
            throw new InvalidOperationException(
                "Failed to save ClientScene loading page.");
        }

        Selection.activeObject = page;
        Debug.Log("Installed serialized loading page in ClientScene.");
    }

    internal static LoadingPage InstallIntoScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("ClientScene is not loaded.");

        Transform popupLayer = FindTransform(scene, "layPopup");
        if (popupLayer == null)
        {
            throw new InvalidOperationException(
                "ClientScene requires layPopup.");
        }

        Transform existing = popupLayer.Find(RootName);
        GameObject root = existing != null
            ? existing.gameObject
            : new GameObject(
                RootName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LoadingPage));
        root.layer = popupLayer.gameObject.layer;
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(popupLayer, false);
        Stretch(rootRect);
        rootRect.SetAsLastSibling();

        Image backdrop = GetOrAdd<Image>(root);
        backdrop.color = BackdropColor;
        backdrop.raycastTarget = true;

        TextMeshProUGUI message = EnsureMessage(rootRect);
        RectTransform spinner = EnsureSpinner(rootRect);
        LoadingPage page = GetOrAdd<LoadingPage>(root);
        SerializedObject serialized = new(page);
        serialized.FindProperty("backdrop").objectReferenceValue = backdrop;
        serialized.FindProperty("messageText").objectReferenceValue = message;
        serialized.FindProperty("spinner").objectReferenceValue = spinner;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(true);
        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(page);
        return page;
    }

    internal static List<string> ValidateScene(Scene scene)
    {
        List<string> issues = new();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            issues.Add("ClientScene is not loaded.");
            return issues;
        }

        Transform popupLayer = FindTransform(scene, "layPopup");
        Transform root = popupLayer != null
            ? popupLayer.Find(RootName)
            : null;
        LoadingPage page = root != null
            ? root.GetComponent<LoadingPage>()
            : null;
        if (page == null)
        {
            issues.Add("pagLoading is missing from layPopup.");
            return issues;
        }

        if (!page.HasDesignerReferences)
            issues.Add("LoadingPage references are incomplete.");
        if (!root.gameObject.activeSelf)
            issues.Add("pagLoading must be active for startup loading.");
        if (root.GetSiblingIndex() != popupLayer.childCount - 1)
            issues.Add("pagLoading must be the last popup sibling.");
        if (root.GetComponent<Image>()?.raycastTarget != true)
            issues.Add("pagLoading must block pointer input.");

        RectTransform rect = root as RectTransform;
        if (rect == null || rect.anchorMin != Vector2.zero ||
            rect.anchorMax != Vector2.one ||
            rect.anchoredPosition != Vector2.zero ||
            rect.sizeDelta != Vector2.zero)
        {
            issues.Add("pagLoading must stretch across layPopup.");
        }

        return issues;
    }

    private static TextMeshProUGUI EnsureMessage(RectTransform parent)
    {
        Transform existing = parent.Find("txtLoadingMessage");
        GameObject target = existing != null
            ? existing.gameObject
            : new GameObject(
                "txtLoadingMessage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
        target.layer = parent.gameObject.layer;
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -4f);
        rect.sizeDelta = new Vector2(640f, 64f);

        TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(target);
        text.text = "PREPARING CONTENT";
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform EnsureSpinner(RectTransform parent)
    {
        Transform existing = parent.Find("imgLoadingSpinner");
        GameObject target = existing != null
            ? existing.gameObject
            : new GameObject(
                "imgLoadingSpinner",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
        target.layer = parent.gameObject.layer;
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -54f);
        rect.sizeDelta = new Vector2(70f, 8f);

        Image image = GetOrAdd<Image>(target);
        image.color = AccentColor;
        image.raycastTarget = false;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static Transform FindTransform(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(
                true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == objectName)
                    return transforms[index];
            }
        }

        return null;
    }

    private static T GetOrAdd<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }
}
