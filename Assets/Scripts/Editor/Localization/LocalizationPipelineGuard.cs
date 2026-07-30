using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PS260714.Localization.Editor
{
    public sealed class LocalizationSourcePostprocessor : AssetPostprocessor
    {
        private static bool generationQueued;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!ContainsSource(importedAssets) &&
                !ContainsSource(movedAssets))
            {
                return;
            }

            QueueGeneration();
        }

        private static bool ContainsSource(string[] paths)
        {
            if (paths == null)
            {
                return false;
            }

            for (int index = 0; index < paths.Length; index++)
            {
                if (string.Equals(
                        paths[index],
                        LocalizationCodeGenerator.LocalesPath,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        paths[index],
                        LocalizationCodeGenerator.StringsPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void QueueGeneration()
        {
            if (generationQueued)
            {
                return;
            }

            generationQueued = true;
            EditorApplication.delayCall += GenerateDeferred;
        }

        private static void GenerateDeferred()
        {
            generationQueued = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueGeneration();
                return;
            }

            LocalizationValidationResult result =
                LocalizationCodeGenerator.Generate();
            LocalizationPipelineLog.Write(result, "automatic generation");
        }
    }

    public sealed class LocalizationBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            LocalizationValidationResult validation =
                LocalizationCodeGenerator.Validate();
            if (!validation.IsValid)
            {
                LocalizationPipelineLog.Write(validation, "build validation");
                throw new BuildFailedException(
                    "Localization CSV validation failed. Open " +
                    PS260714EditorMenu.LocalizationEditor + ".");
            }

            if (LocalizationCodeGenerator.IsStale(
                out string expected,
                out string generated))
            {
                LocalizationCodeGenerator.Generate();
                throw new BuildFailedException(
                    "Localization generated C# was stale and has been " +
                    $"regenerated ({generated} -> {expected}). Wait for Unity " +
                    "to compile, then build again.");
            }
        }
    }

    [InitializeOnLoad]
    internal static class LocalizationPlayModeGuard
    {
        private static bool fontRestoreQueued;

        static LocalizationPlayModeGuard()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            QueueEditorTextFontRestore();
        }

        [MenuItem(PS260714EditorMenu.ValidateLocalization)]
        private static void ValidateMenu()
        {
            LocalizationValidationResult result =
                LocalizationCodeGenerator.Validate();
            LocalizationPipelineLog.Write(result, "manual validation");
        }

        [MenuItem(PS260714EditorMenu.GenerateLocalization)]
        private static void GenerateMenu()
        {
            LocalizationValidationResult result =
                LocalizationCodeGenerator.Generate();
            LocalizationPipelineLog.Write(result, "manual generation");
        }

        private static void HandlePlayModeChanged(
            PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                RestoreEditorTextFonts();
                return;
            }

            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            LocalizationValidationResult validation =
                LocalizationCodeGenerator.Validate();
            if (!validation.IsValid)
            {
                LocalizationPipelineLog.Write(
                    validation,
                    "play mode validation");
                EditorApplication.isPlaying = false;
                Debug.LogError(
                    "[Localization] Play mode cancelled: fix localization " +
                    "validation errors first.");
                return;
            }

            if (LocalizationCodeGenerator.IsStale(
                out string unusedExpected,
                out string unusedGenerated))
            {
                LocalizationCodeGenerator.Generate();
                EditorApplication.isPlaying = false;
                Debug.LogWarning(
                    "[Localization] Play mode cancelled once because CSV " +
                    "changes generated new C#. Enter Play again after compile.");
            }
        }

        private static void QueueEditorTextFontRestore()
        {
            if (fontRestoreQueued)
                return;

            fontRestoreQueued = true;
            EditorApplication.delayCall += RestoreEditorTextFontsWhenReady;
        }

        private static void RestoreEditorTextFontsWhenReady()
        {
            fontRestoreQueued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                QueueEditorTextFontRestore();
                return;
            }

            RestoreEditorTextFonts();
        }

        private static void RestoreEditorTextFonts()
        {
            LocalizationService.Initialize();
            LocalizationFontCatalog catalog =
                LocalizationService.FontCatalog;
            LocalizationMarkupCatalog markupCatalog =
                LocalizationService.MarkupCatalog;
            TMP_Text[] texts =
                UnityEngine.Object.FindObjectsByType<TMP_Text>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            HashSet<Scene> changedScenes = new();
            for (int index = 0; index < texts.Length; index++)
            {
                TMP_Text text = texts[index];
                if (text == null ||
                    EditorUtility.IsPersistent(text))
                {
                    continue;
                }

                TMP_FontAsset previousFont = text.font;
                TMP_SpriteAsset previousSprite = text.spriteAsset;
                LocalizedText localized =
                    text.GetComponent<LocalizedText>();
                string fontRole = localized != null
                    ? localized.FontRoleOverride
                    : null;
                TMP_FontAsset editorFont = catalog != null
                    ? catalog.Resolve(
                        LocalizationService.CurrentLocale,
                        fontRole,
                        LocalizationService.CurrentFontId)
                    : TMP_Settings.defaultFontAsset;
                if (editorFont != null)
                    text.font = editorFont;
                if (markupCatalog != null &&
                    markupCatalog.SpriteAsset != null)
                {
                    text.spriteAsset = markupCatalog.SpriteAsset;
                }

                if (text.font == previousFont &&
                    text.spriteAsset == previousSprite)
                {
                    continue;
                }

                EditorUtility.SetDirty(text);
                Scene scene = text.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded)
                    changedScenes.Add(scene);
            }

            foreach (Scene scene in changedScenes)
                EditorSceneManager.MarkSceneDirty(scene);

            if (changedScenes.Count > 0)
            {
                Canvas.ForceUpdateCanvases();
                SceneView.RepaintAll();
            }
        }
    }

    internal static class LocalizationPipelineLog
    {
        public static void Write(
            LocalizationValidationResult result,
            string operation)
        {
            foreach (LocalizationValidationIssue issue in result.Issues)
            {
                if (issue.Severity == LocalizationValidationSeverity.Error)
                {
                    Debug.LogError("[Localization] " + issue);
                }
                else
                {
                    Debug.LogWarning("[Localization] " + issue);
                }
            }

            if (result.IsValid)
            {
                Debug.Log(
                    $"[Localization] {operation} completed: " +
                    $"{result.WarningCount} warning(s).");
            }
        }
    }
}
