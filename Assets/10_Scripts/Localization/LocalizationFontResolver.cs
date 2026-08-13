using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PS260714.Localization
{
    /// <summary>
    /// Install once for the client. It applies the localization catalog font
    /// to every loaded TMP text, including inactive and runtime-created views.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class LocalizationFontResolver : MonoBehaviour
    {
        [SerializeField] private LocalizationFontCatalog fontCatalog;
        [SerializeField] private LocalizationMarkupCatalog markupCatalog;
        [SerializeField] private bool applyToChildren = true;
        [SerializeField] private bool includeInactive = true;
        [SerializeField] private bool reapplyWhenChildrenChange = true;
        [Tooltip("Reapply fonts to TMP objects created or overwritten later.")]
        [SerializeField] private bool scanForRuntimeText = true;
        [SerializeField, Min(0.1f)] private float runtimeScanInterval = 0.5f;
        [SerializeField] private bool keepAcrossScenes;

        private readonly HashSet<int> boundTextIds = new();
        private float nextRuntimeScan;

        public static LocalizationFontResolver Current { get; private set; }

        public LocalizationFontCatalog FontCatalog => fontCatalog;
        public LocalizationMarkupCatalog MarkupCatalog => markupCatalog;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                enabled = false;
                return;
            }

            Current = this;
            if (keepAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            LocalizationService.Configure(fontCatalog, markupCatalog);
            fontCatalog ??= LocalizationService.FontCatalog;
            markupCatalog ??= LocalizationService.MarkupCatalog;
        }

        private void OnEnable()
        {
            LocalizationService.LocaleChanged += HandleLocaleChanged;
            LocalizationService.FontChanged += HandleFontChanged;
            if (applyToChildren)
            {
                ApplyToHierarchy();
            }
        }

        private void OnDisable()
        {
            LocalizationService.LocaleChanged -= HandleLocaleChanged;
            LocalizationService.FontChanged -= HandleFontChanged;
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled &&
                applyToChildren &&
                reapplyWhenChildrenChange)
            {
                ApplyToHierarchy();
            }
        }

        private void LateUpdate()
        {
            if (!scanForRuntimeText || Time.unscaledTime < nextRuntimeScan)
            {
                return;
            }

            nextRuntimeScan = Time.unscaledTime + runtimeScanInterval;
            BindNewRuntimeText();
        }

        public TMP_FontAsset Resolve(string fontRole = null)
        {
            if (fontCatalog != null)
            {
                TMP_FontAsset resolved = fontCatalog.Resolve(
                    LocalizationService.CurrentLocale,
                    fontRole,
                    LocalizationService.CurrentFontId);
                return PrepareResolvedFont(resolved);
            }

            return LocalizationSystemFontFallback.Resolve(
                       LocalizationService.CurrentLocale) ??
                   TMP_Settings.defaultFontAsset;
        }

        public void Apply(TMP_Text text, string fontRole = null)
        {
            if (text == null)
            {
                return;
            }

            TMP_FontAsset font = Resolve(fontRole);
            if (font != null)
            {
                text.font = font;
            }

            if (markupCatalog != null && markupCatalog.SpriteAsset != null)
            {
                text.spriteAsset = markupCatalog.SpriteAsset;
            }
        }

        [ContextMenu("Apply Fonts To Client")]
        public void ApplyToHierarchy()
        {
            TMP_Text[] texts = FindClientTexts();
            for (int index = 0; index < texts.Length; index++)
            {
                LocalizedText localizedText =
                    texts[index].GetComponent<LocalizedText>();
                if (localizedText != null)
                {
                    localizedText.Refresh();
                }
                else
                {
                    Apply(texts[index]);
                }

                boundTextIds.Add(texts[index].GetInstanceID());
            }
        }

        private void BindNewRuntimeText()
        {
            TMP_Text[] texts = FindClientTexts();
            bool enforceCatalogFont = fontCatalog != null &&
                                      fontCatalog.GlobalDefaultFont != null;
            for (int index = 0; index < texts.Length; index++)
            {
                TMP_Text text = texts[index];
                if (text == null)
                {
                    continue;
                }

                bool isNewText = boundTextIds.Add(text.GetInstanceID());
                if (enforceCatalogFont)
                {
                    // Some pages copy a template font after their first
                    // initialization. Reapply the catalog-owned font so
                    // those late writes cannot split the client typography.
                    LocalizedText roleAwareText =
                        text.GetComponent<LocalizedText>();
                    if (roleAwareText != null)
                        roleAwareText.Refresh();
                    else
                        Apply(text);
                    continue;
                }

                if (!isNewText)
                    continue;

                LocalizedText localizedText = text.GetComponent<LocalizedText>();
                if (localizedText != null)
                {
                    localizedText.Refresh();
                }
                else
                {
                    Apply(text);
                }
            }
        }

        public static void RefreshAllClientText()
        {
            if (Current != null)
            {
                Current.ApplyToHierarchy();
                return;
            }

            TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < texts.Length; index++)
                ApplyGameDefault(texts[index]);
        }

        public static void ApplyGameDefault(
            TMP_Text text,
            string fontRole = null)
        {
            if (text == null)
            {
                return;
            }

            if (Current != null)
            {
                Current.Apply(text, fontRole);
                return;
            }

            LocalizationService.Initialize();
            LocalizationFontCatalog catalog =
                LocalizationService.FontCatalog;
            TMP_FontAsset font = catalog != null
                ? catalog.Resolve(
                    LocalizationService.CurrentLocale,
                    fontRole,
                    LocalizationService.CurrentFontId)
                : TMP_Settings.defaultFontAsset;
            if (catalog != null && Application.isPlaying)
                font = catalog.PrepareFallbacks(font);
            font = EnableDynamicAtlasGrowth(font);
            if (LocalizationSystemFontFallback.NeedsKoreanFallback(
                    LocalizationService.CurrentLocale,
                    font))
            {
                font = LocalizationSystemFontFallback.Resolve(
                           LocalizationService.CurrentLocale) ??
                       font;
                font = EnableDynamicAtlasGrowth(font);
            }

            if (font != null)
            {
                text.font = font;
            }

            LocalizationMarkupCatalog markupCatalog =
                LocalizationService.MarkupCatalog;
            if (markupCatalog != null && markupCatalog.SpriteAsset != null)
            {
                text.spriteAsset = markupCatalog.SpriteAsset;
            }
        }

        private void HandleLocaleChanged(string unusedLocale)
        {
            if (applyToChildren)
            {
                ApplyToHierarchy();
            }
        }

        private void HandleFontChanged(string unusedFontId)
        {
            if (applyToChildren)
            {
                ApplyToHierarchy();
            }
        }

        private TMP_Text[] FindClientTexts()
        {
            return UnityEngine.Object.FindObjectsByType<TMP_Text>(
                includeInactive
                    ? FindObjectsInactive.Include
                    : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        }

        private TMP_FontAsset PrepareResolvedFont(TMP_FontAsset font)
        {
            if (fontCatalog != null)
                font = fontCatalog.PrepareFallbacks(font);

            font = EnableDynamicAtlasGrowth(font);

            if (LocalizationSystemFontFallback.NeedsKoreanFallback(
                    LocalizationService.CurrentLocale,
                    font))
            {
                TMP_FontAsset systemFont =
                    LocalizationSystemFontFallback.Resolve(
                        LocalizationService.CurrentLocale);
                if (systemFont != null)
                    return EnableDynamicAtlasGrowth(systemFont);
            }

            return font;
        }

        private static TMP_FontAsset EnableDynamicAtlasGrowth(
            TMP_FontAsset font)
        {
            if (font != null &&
                font.atlasPopulationMode != AtlasPopulationMode.Static &&
                (font.hideFlags & HideFlags.DontSave) != 0 &&
                !font.isMultiAtlasTexturesEnabled)
            {
                // The localization source contains more Hangul glyphs than a
                // single 1024 atlas can hold. Project font assets are not
                // tracked in the scripts-only repository, so guarantee this
                // at runtime instead of relying on an Inspector-only setting.
                font.isMultiAtlasTexturesEnabled = true;
            }

            return font;
        }
    }
}
