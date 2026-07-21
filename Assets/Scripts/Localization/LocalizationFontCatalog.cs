using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PS260714.Localization
{
    [Serializable]
    public sealed class LocalizationLocaleFontDefinition
    {
        [SerializeField] private string locale = "ko-KR";
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Optional DynamicOS fallback, for example Malgun Gothic.")]
        [SerializeField] private string osFontFamily;
        [SerializeField] private string osFontStyle = "Regular";

        public string Locale => locale;
        public TMP_FontAsset Font => font;
        public string OsFontFamily => osFontFamily;
        public string OsFontStyle => osFontStyle;
    }

    [Serializable]
    public sealed class LocalizationRoleFontDefinition
    {
        [Tooltip("Locale code, or * to apply to every locale.")]
        [SerializeField] private string locale = "*";
        [SerializeField] private string role = "body";
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Optional DynamicOS fallback when Font is empty.")]
        [SerializeField] private string osFontFamily;
        [SerializeField] private string osFontStyle = "Regular";

        public string Locale => locale;
        public string Role => role;
        public TMP_FontAsset Font => font;
        public string OsFontFamily => osFontFamily;
        public string OsFontStyle => osFontStyle;
    }

    [Serializable]
    public sealed class LocalizationSelectableFontDefinition
    {
        [SerializeField] private string id = "FONT_ID";
        [SerializeField] private string displayName = "Font";
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Optional DynamicOS fallback when Font is empty.")]
        [SerializeField] private string osFontFamily;
        [SerializeField] private string osFontStyle = "Regular";

        public string Id => id;
        public string DisplayName => displayName;
        public TMP_FontAsset Font => font;
        public string OsFontFamily => osFontFamily;
        public string OsFontStyle => osFontStyle;
    }

    /// <summary>
    /// Resolves fonts by semantic role without storing Unity references in CSV.
    /// Missing role and locale assets always fall through to the global font.
    /// </summary>
    [CreateAssetMenu(
        fileName = "LocalizationFontCatalog",
        menuName = "PS260714/Localization/Font Catalog")]
    public sealed class LocalizationFontCatalog : ScriptableObject
    {
        [Header("Game-wide default")]
        [SerializeField] private TMP_FontAsset globalDefaultFont;
        [Tooltip("Optional DynamicOS fallback when the default asset is empty.")]
        [SerializeField] private string globalDefaultOsFontFamily;
        [SerializeField] private string globalDefaultOsFontStyle = "Regular";

        [Header("Locale defaults")]
        [SerializeField]
        private List<LocalizationLocaleFontDefinition> localeFonts = new();

        [Header("Optional locale + role overrides")]
        [SerializeField]
        private List<LocalizationRoleFontDefinition> roleFonts = new();

        [Header("Recommended TMP fallback assets")]
        [SerializeField] private List<TMP_FontAsset> fallbackFonts = new();

        [Header("Optional player-selectable global fonts")]
        [SerializeField]
        private List<LocalizationSelectableFontDefinition> selectableFonts =
            new();

        private readonly Dictionary<string, TMP_FontAsset> dynamicOsFonts =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, TMP_FontAsset> fallbackClones = new();

        public TMP_FontAsset GlobalDefaultFont => globalDefaultFont;
        public IReadOnlyList<LocalizationLocaleFontDefinition> LocaleFonts =>
            localeFonts;
        public IReadOnlyList<LocalizationRoleFontDefinition> RoleFonts =>
            roleFonts;
        public IReadOnlyList<TMP_FontAsset> FallbackFonts => fallbackFonts;
        public IReadOnlyList<LocalizationSelectableFontDefinition>
            SelectableFonts => selectableFonts;

        public TMP_FontAsset ResolveGlobalDefault()
        {
            if (globalDefaultFont != null)
            {
                return globalDefaultFont;
            }

            TMP_FontAsset font = ResolveOsFont(
                globalDefaultOsFontFamily,
                globalDefaultOsFontStyle);
            return font != null ? font : TMP_Settings.defaultFontAsset;
        }

        public TMP_FontAsset Resolve(string locale, string role)
        {
            return Resolve(locale, role, null);
        }

        public TMP_FontAsset Resolve(
            string locale,
            string role,
            string preferredFontId)
        {
            // A concrete player choice is a game-wide override. AUTO retains
            // locale and semantic-role selection.
            TMP_FontAsset font = ResolveSelectable(preferredFontId);
            if (font != null)
            {
                return font;
            }

            font = FindRoleFont(locale, role);
            if (font != null)
            {
                return font;
            }

            font = FindRoleFont("*", role);
            if (font != null)
            {
                return font;
            }

            for (int index = 0; index < localeFonts.Count; index++)
            {
                LocalizationLocaleFontDefinition definition =
                    localeFonts[index];
                if (definition != null &&
                    LocaleEquals(definition.Locale, locale))
                {
                    font = ResolveAssetOrOs(
                        definition.Font,
                        definition.OsFontFamily,
                        definition.OsFontStyle);
                    if (font != null)
                    {
                        return font;
                    }
                }
            }

            if (globalDefaultFont != null)
            {
                return globalDefaultFont;
            }

            font = ResolveOsFont(
                globalDefaultOsFontFamily,
                globalDefaultOsFontStyle);
            if (font != null)
            {
                return font;
            }

            return TMP_Settings.defaultFontAsset;
        }

        public TMP_FontAsset ResolveSelectable(string fontId)
        {
            if (string.IsNullOrWhiteSpace(fontId) ||
                string.Equals(fontId, "AUTO", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            for (int index = 0; index < selectableFonts.Count; index++)
            {
                LocalizationSelectableFontDefinition definition =
                    selectableFonts[index];
                if (definition != null &&
                    string.Equals(
                        definition.Id,
                        fontId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return ResolveAssetOrOs(
                        definition.Font,
                        definition.OsFontFamily,
                        definition.OsFontStyle);
                }
            }

            return null;
        }

        public bool ContainsSelectableFont(string fontId)
        {
            if (string.IsNullOrWhiteSpace(fontId) ||
                string.Equals(fontId, "AUTO", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            for (int index = 0; index < selectableFonts.Count; index++)
            {
                LocalizationSelectableFontDefinition definition =
                    selectableFonts[index];
                if (definition != null &&
                    string.Equals(
                        definition.Id,
                        fontId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public TMP_FontAsset PrepareFallbacks(TMP_FontAsset source)
        {
            if (source == null || fallbackFonts.Count == 0)
            {
                return source;
            }

            int sourceId = source.GetInstanceID();
            if (fallbackClones.TryGetValue(
                sourceId,
                out TMP_FontAsset cached) &&
                cached != null)
            {
                return cached;
            }

            TMP_FontAsset clone = Instantiate(source);
            clone.name = source.name + " (Localization Runtime)";
            clone.hideFlags = HideFlags.DontSave;
            clone.fallbackFontAssetTable = source.fallbackFontAssetTable != null
                ? new List<TMP_FontAsset>(source.fallbackFontAssetTable)
                : new List<TMP_FontAsset>();

            for (int index = 0; index < fallbackFonts.Count; index++)
            {
                TMP_FontAsset fallback = fallbackFonts[index];
                if (fallback != null &&
                    fallback != source &&
                    !clone.fallbackFontAssetTable.Contains(fallback))
                {
                    clone.fallbackFontAssetTable.Add(fallback);
                }
            }

            fallbackClones[sourceId] = clone;
            return clone;
        }

        private void OnDisable()
        {
            foreach (TMP_FontAsset clone in fallbackClones.Values)
            {
                if (clone == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(clone);
                }
                else
                {
                    DestroyImmediate(clone);
                }
            }

            fallbackClones.Clear();
            dynamicOsFonts.Clear();
        }

        public IEnumerable<string> EnumerateRoles()
        {
            HashSet<string> seen = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < roleFonts.Count; index++)
            {
                LocalizationRoleFontDefinition definition = roleFonts[index];
                if (definition != null &&
                    !string.IsNullOrWhiteSpace(definition.Role) &&
                    seen.Add(definition.Role))
                {
                    yield return definition.Role;
                }
            }
        }

        private TMP_FontAsset FindRoleFont(string locale, string role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return null;
            }

            for (int index = 0; index < roleFonts.Count; index++)
            {
                LocalizationRoleFontDefinition definition = roleFonts[index];
                if (definition != null &&
                    LocaleEquals(definition.Locale, locale) &&
                    string.Equals(
                        definition.Role,
                        role,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return ResolveAssetOrOs(
                        definition.Font,
                        definition.OsFontFamily,
                        definition.OsFontStyle);
                }
            }

            return null;
        }

        private TMP_FontAsset ResolveAssetOrOs(
            TMP_FontAsset font,
            string family,
            string style)
        {
            return font != null ? font : ResolveOsFont(family, style);
        }

        private TMP_FontAsset ResolveOsFont(string family, string style)
        {
            if (string.IsNullOrWhiteSpace(family))
            {
                return null;
            }

            string normalizedStyle = string.IsNullOrWhiteSpace(style)
                ? "Regular"
                : style.Trim();
            string cacheKey = family.Trim() + "\n" + normalizedStyle;
            if (dynamicOsFonts.TryGetValue(
                    cacheKey,
                    out TMP_FontAsset cached))
            {
                if (ReferenceEquals(cached, null))
                {
                    return null;
                }

                if (cached != null)
                {
                    return cached;
                }
            }

            dynamicOsFonts.Remove(cacheKey);

            try
            {
                TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
                    family.Trim(),
                    normalizedStyle);
                dynamicOsFonts[cacheKey] = created;
                if (created != null)
                {
                    created.name = family.Trim() + " (DynamicOS)";
                    created.hideFlags = HideFlags.DontSave;
                }

                return created;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[Localization] Could not create DynamicOS font " +
                    $"'{family} {normalizedStyle}': {exception.Message}",
                    this);
                dynamicOsFonts[cacheKey] = null;
                return null;
            }
        }

        private static bool LocaleEquals(string left, string right)
        {
            return string.Equals(
                left ?? string.Empty,
                right ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
