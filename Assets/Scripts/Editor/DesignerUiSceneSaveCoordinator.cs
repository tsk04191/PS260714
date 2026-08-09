using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DesignerUiBuildValidator : IPreprocessBuildWithReport
{
    private const string ClientScenePath = "Assets/Scenes/ClientScene.unity";

    public int callbackOrder => 50;

    public void OnPreprocessBuild(BuildReport report)
    {
        Scene scene = SceneManager.GetSceneByPath(ClientScenePath);
        bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
        if (openedForValidation)
        {
            scene = EditorSceneManager.OpenScene(
                ClientScenePath,
                OpenSceneMode.Additive);
        }

        List<string> issues = new();
        bool foundStageSelect = false;
        bool foundRecruit = false;
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (StageSelectPage page in
                         root.GetComponentsInChildren<StageSelectPage>(true))
                {
                    foundStageSelect = true;
                    if (!page.ValidateEditorUi(out string error))
                        issues.Add(error);
                }

                foreach (MainSubPage page in
                         root.GetComponentsInChildren<MainSubPage>(true))
                {
                    if (!page.IsRecruitPage)
                        continue;
                    foundRecruit = true;
                    if (!page.ValidateRecruitEditorUi(out string error))
                        issues.Add(error);
                }
            }
        }
        finally
        {
            if (openedForValidation && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        if (!foundStageSelect)
            issues.Add("Stage Select page was not found in ClientScene.");
        if (!foundRecruit)
            issues.Add("Recruit page was not found in ClientScene.");
        if (issues.Count > 0)
        {
            throw new BuildFailedException(
                "Saved designer UI validation failed:\n- " +
                string.Join("\n- ", issues));
        }
    }
}
