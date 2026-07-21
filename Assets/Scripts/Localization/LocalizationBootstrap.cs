using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PS260714.Localization
{
    /// <summary>
    /// Safety-net installer for scenes that do not yet serialize a resolver.
    /// A configured scene resolver always wins; otherwise the first root Canvas
    /// receives a resolver using generated text and TMP/system font fallbacks.
    /// </summary>
    public static class LocalizationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            LocalizationSystemFontFallback.ResetCache();
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureInstalled()
        {
            LocalizationService.Initialize();
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (canvases.Length == 0)
            {
                return;
            }

            Canvas target = canvases[0];
            for (int index = 0; index < canvases.Length; index++)
            {
                if (canvases[index].isRootCanvas)
                {
                    target = canvases[index];
                    break;
                }
            }

            LocalizationFontResolver resolver =
                UnityEngine.Object.FindFirstObjectByType<
                    LocalizationFontResolver>(FindObjectsInactive.Include);
            if (resolver == null)
            {
                target.gameObject.AddComponent<LocalizationFontResolver>();
            }

            if (target.GetComponent<SceneLocalizedTextBinder>() == null)
            {
                target.gameObject.AddComponent<SceneLocalizedTextBinder>();
            }
        }

        private static void HandleSceneLoaded(
            Scene unusedScene,
            LoadSceneMode unusedMode)
        {
            EnsureInstalled();
        }
    }

    internal static class LocalizationSystemFontFallback
    {
        private static readonly Dictionary<string, TMP_FontAsset> Cache =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] KoreanCandidates =
        {
            "Malgun Gothic",
            "\uB9D1\uC740 \uACE0\uB515",
            "Noto Sans CJK KR",
            "Noto Sans KR",
            "Apple SD Gothic Neo",
            "NanumGothic",
        };

        public static bool NeedsKoreanFallback(
            string locale,
            TMP_FontAsset font)
        {
            return IsKorean(locale) &&
                   (font == null ||
                    !font.HasCharacter(
                        '\uD55C',
                        searchFallbacks: true,
                        tryAddCharacter: true));
        }

        public static void ResetCache()
        {
            Cache.Clear();
        }

        public static TMP_FontAsset Resolve(string locale)
        {
            if (!IsKorean(locale))
            {
                return null;
            }

            if (Cache.TryGetValue(locale, out TMP_FontAsset cached))
            {
                if (ReferenceEquals(cached, null))
                {
                    return null;
                }

                if (cached != null)
                {
                    return cached;
                }

                Cache.Remove(locale);
            }

            HashSet<string> installed = new HashSet<string>(
                Font.GetOSInstalledFontNames(),
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < KoreanCandidates.Length; index++)
            {
                string family = KoreanCandidates[index];
                if (!installed.Contains(family))
                {
                    continue;
                }

                try
                {
                    TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
                        family,
                        "Regular");
                    if (created != null)
                    {
                        created.name = family + " (Localization DynamicOS)";
                        created.hideFlags = HideFlags.DontSave;
                        Cache[locale] = created;
                        return created;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[Localization] DynamicOS fallback '{family}' " +
                        $"failed: {exception.Message}");
                }
            }

            Cache[locale] = null;
            Debug.LogWarning(
                "[Localization] No Korean-capable TMP font or supported OS " +
                "font was found. Assign one in LocalizationFontCatalog.");
            return null;
        }

        private static bool IsKorean(string locale)
        {
            return !string.IsNullOrWhiteSpace(locale) &&
                   locale.StartsWith("ko", StringComparison.OrdinalIgnoreCase);
        }
    }
}
