using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuPageSceneBuilder
{
    private const string ClientScenePath = "Assets/04_Scenes/ClientScene.unity";

    [MenuItem(
        PS260714EditorMenu.ValidateDesignerUi,
        false,
        PS260714EditorMenu.ValidateDesignerUiPriority)]
    private static void ValidateDesignerUi()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded ||
            scene.path != ClientScenePath)
        {
            Debug.LogWarning(
                "Open Assets/04_Scenes/ClientScene.unity before validating UI.");
            return;
        }

        IReadOnlyList<string> issues = CollectDesignerUiIssues(scene);
        if (issues.Count == 0)
        {
            Debug.Log(
                "Designer UI validation passed. No scene objects were changed.");
            return;
        }

        Debug.LogWarning(
            "Designer UI validation found:\n- " +
            string.Join("\n- ", issues));
    }

    internal static IReadOnlyList<string> ValidateDesignerUiForScene(
        Scene scene)
    {
        return CollectDesignerUiIssues(scene);
    }

    private static List<string> CollectDesignerUiIssues(Scene scene)
    {
        List<string> issues = new();
        GameObject layClient = FindSceneObject(scene, "layClient");
        if (layClient == null)
        {
            issues.Add("layClient is missing.");
            return issues;
        }

        string[] staticPageNames =
        {
            "pagTitle",
            "pagMain",
            "pagStageSelect",
            "pagBase",
            "pagRoster",
            "pagShop",
            "pagRecruit",
            "pagStorage",
        };
        foreach (string pageName in staticPageNames)
        {
            RuntimeMenuPageBase page = FindDirectChild(layClient, pageName)
                ?.GetComponent<RuntimeMenuPageBase>();
            if (page == null)
            {
                issues.Add($"{pageName} is missing.");
                continue;
            }
            if (!page.HasDesignerLayout)
                issues.Add($"{pageName} has no saved designer layout.");
            if (HasCollapsedButtons(page))
                issues.Add($"{pageName} menu buttons overlap.");
        }

        ValidateRecruit(layClient, issues);
        ValidateCodexPages(layClient, issues);
        return issues;
    }

    private static void ValidateRecruit(
        GameObject layClient,
        ICollection<string> issues)
    {
        GameObject recruit = FindDirectChild(layClient, "pagRecruit");
        RecruitBannerDesignerBindings banner = recruit != null
            ? recruit.GetComponentInChildren<
                RecruitBannerDesignerBindings>(true)
            : null;
        RecruitRevealDesignerBindings reveal = recruit != null
            ? recruit.GetComponentInChildren<
                RecruitRevealDesignerBindings>(true)
            : null;
        if (banner == null || !banner.HasDesignerLayout ||
            !banner.HasRequiredReferences)
        {
            issues.Add(
                "pagRecruit banner is not bound to saved Scene UI.");
        }
        if (reveal == null || !reveal.HasDesignerLayout ||
            !reveal.HasRequiredReferences)
        {
            issues.Add(
                "pagRecruit reveal overlay is not bound to saved Scene UI.");
        }
    }

    private static void ValidateCodexPages(
        GameObject layClient,
        ICollection<string> issues)
    {
        string[] pageNames =
        {
            "pagEnemyCodex",
            "pagCharacterCodex",
            "pagSkillCodex",
            "pagItemCodex",
        };
        foreach (string pageName in pageNames)
        {
            GameObject page = FindDirectChild(layClient, pageName);
            Transform browser = page != null
                ? page.transform.Find(
                    RuntimeMenuPageBase.RuntimeRootObjectName +
                    "/grpMenuPanel/grpMenuButtons/grpCodexBrowser")
                : null;
            CodexBrowserDesignerSettings settings = browser != null
                ? browser.GetComponent<CodexBrowserDesignerSettings>()
                : null;
            if (page == null)
                issues.Add($"{pageName} is missing.");
            else if (settings == null || !settings.HasDesignerLayout ||
                     settings.CardTemplate == null)
                issues.Add($"{pageName} codex UI references are incomplete.");
        }
    }

    private static bool HasCollapsedButtons(RuntimeMenuPageBase page)
    {
        Transform buttonRoot = page.transform.Find(
            RuntimeMenuPageBase.RuntimeRootObjectName +
            "/grpMenuPanel/grpMenuButtons");
        if (buttonRoot is not RectTransform root)
            return false;

        List<RectTransform> buttons = new();
        for (int index = 0; index < root.childCount; index++)
        {
            if (root.GetChild(index) is not RectTransform child ||
                !child.gameObject.activeSelf ||
                child.GetComponent<Button>() == null)
            {
                continue;
            }
            LayoutElement layout = child.GetComponent<LayoutElement>();
            if (layout == null || !layout.ignoreLayout)
                buttons.Add(child);
        }
        if (buttons.Count < 2)
            return false;

        Vector2 first = buttons[0].anchoredPosition;
        for (int index = 1; index < buttons.Count; index++)
        {
            if (Vector2.Distance(first, buttons[index].anchoredPosition) > 0.5f)
                return false;
        }
        return root.rect.width <= 120f || root.rect.height <= 120f;
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindDescendant(root.transform, name);
            if (found != null)
                return found.gameObject;
        }
        return null;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindDescendant(root.GetChild(index), name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static GameObject FindDirectChild(GameObject parent, string name)
    {
        Transform child = parent != null ? parent.transform.Find(name) : null;
        return child != null ? child.gameObject : null;
    }
}
