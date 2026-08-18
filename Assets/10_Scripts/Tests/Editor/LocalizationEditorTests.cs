using System.Collections.Generic;
using NUnit.Framework;
using PS260714.Localization;
using PS260714.Localization.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class LocalizationEditorTests
{
    private const string MarkupCatalogPath =
        "Assets/06_Runtime/Resources/Localization/LocalizationMarkupCatalog.asset";
    private const string FontCatalogPath =
        "Assets/06_Runtime/Resources/Localization/LocalizationFontCatalog.asset";

    [Test]
    public void EditorText_KoreanEditorUsesKoreanAndOtherEditorsUseEnglish()
    {
        Assert.That(
            PS260714EditorText.TranslateForLanguage(
                "Permanent Stat Modifier",
                true),
            Is.EqualTo("상시 능력치 보정"));
        Assert.That(
            PS260714EditorText.TranslateForLanguage(
                "상시 능력치 보정",
                false),
            Is.EqualTo("Permanent Stat Modifier"));
    }

    [Test]
    public void EditorText_UnknownKoreanDoesNotLeakIntoEnglishEditor()
    {
        string translated = PS260714EditorText.TranslateForLanguage(
            "알 수 없는 편집기 항목",
            false);

        Assert.That(translated, Is.Not.Empty);
        Assert.That(translated, Does.Not.Match("[가-힣]"));
    }

    [Test]
    public void EditorContent_AddsTooltipWhenMissing()
    {
        GUIContent content = new("Save");

        Assert.That(content.text, Is.Not.Empty);
        Assert.That(content.tooltip, Is.Not.Empty);
    }

    [Test]
    public void ApplyStyle_WrapsSelectionAndKeepsContentSelected()
    {
        LocalizationMarkupEditResult result =
            LocalizationMarkupEditUtility.ApplyStyle(
                "alpha beta",
                10,
                6,
                "damage");

        Assert.That(
            result.Text,
            Is.EqualTo(
                "alpha [style=damage]beta[/style]"));
        Assert.That(
            result.SelectIndex,
            Is.EqualTo(result.Text.IndexOf("beta")));
        Assert.That(
            result.CursorIndex,
            Is.EqualTo(result.Text.IndexOf("[/style]")));
    }

    [Test]
    public void ApplyStyle_WithoutSelectionPlacesCaretBetweenTags()
    {
        LocalizationMarkupEditResult result =
            LocalizationMarkupEditUtility.ApplyStyle(
                "Fire",
                4,
                4,
                "fire");

        Assert.That(
            result.Text,
            Is.EqualTo("Fire[style=fire][/style]"));
        Assert.That(
            result.CursorIndex,
            Is.EqualTo(result.Text.IndexOf("[/style]")));
        Assert.That(
            result.SelectIndex,
            Is.EqualTo(result.CursorIndex));
    }

    [Test]
    public void InsertIcon_ReplacesSelectedText()
    {
        LocalizationMarkupEditResult result =
            LocalizationMarkupEditUtility.InsertIcon(
                "Deal target",
                11,
                5,
                "fire");

        Assert.That(
            result.Text,
            Is.EqualTo("Deal [icon=fire]"));
        Assert.That(result.CursorIndex, Is.EqualTo(result.Text.Length));
        Assert.That(result.SelectIndex, Is.EqualTo(result.Text.Length));
    }

    [Test]
    public void InsertLineBreak_ClampsSelectionToTextBounds()
    {
        LocalizationMarkupEditResult result =
            LocalizationMarkupEditUtility.InsertLineBreak(
                "replace me",
                100,
                -10);

        Assert.That(result.Text, Is.EqualTo("[br]"));
        Assert.That(result.CursorIndex, Is.EqualTo(4));
        Assert.That(result.SelectIndex, Is.EqualTo(4));
    }

    [Test]
    public void InsertNumericArgument_ReplacesSelectionAndKeepsCaretAfterToken()
    {
        LocalizationMarkupEditResult result =
            LocalizationMarkupEditUtility.InsertNumericArgument(
                "Armor value",
                11,
                6,
                "armor",
                "0.#");

        Assert.That(result.Text, Is.EqualTo("Armor {armor:0.#}"));
        Assert.That(result.CursorIndex, Is.EqualTo(result.Text.Length));
        Assert.That(result.SelectIndex, Is.EqualTo(result.Text.Length));
    }

    [Test]
    public void InsertNumericArgument_WithoutFormatCreatesGeneralValueToken()
    {
        LocalizationMarkupEditResult result =
            LocalizationMarkupEditUtility.InsertNumericArgument(
                "Count ",
                6,
                6,
                "count",
                string.Empty);

        Assert.That(result.Text, Is.EqualTo("Count {count}"));
    }

    [Test]
    public void InsertNumericArgument_CreatesCircleRadiusToken()
    {
        LocalizationMarkupEditResult result =
            LocalizationMarkupEditUtility.InsertNumericArgument(
                "Radius ",
                7,
                7,
                "radius",
                "0.##");

        Assert.That(result.Text, Is.EqualTo("Radius {radius:0.##}"));
    }

    [Test]
    public void UnsafeNumericArgument_DoesNotModifyText()
    {
        LocalizationMarkupEditResult unsafeName =
            LocalizationMarkupEditUtility.InsertNumericArgument(
                "safe",
                4,
                4,
                "armor}bad",
                "0.#");
        LocalizationMarkupEditResult unsafeFormat =
            LocalizationMarkupEditUtility.InsertNumericArgument(
                "safe",
                4,
                4,
                "armor",
                "0.#}");

        Assert.That(unsafeName.Text, Is.EqualTo("safe"));
        Assert.That(unsafeFormat.Text, Is.EqualTo("safe"));
    }

    [Test]
    public void UnsafeMarkupIdentifier_DoesNotModifyText()
    {
        LocalizationMarkupEditResult result =
            LocalizationMarkupEditUtility.InsertIcon(
                "safe",
                4,
                4,
                "fire]bad");

        Assert.That(result.Text, Is.EqualTo("safe"));
        Assert.That(result.CursorIndex, Is.EqualTo(4));
        Assert.That(result.SelectIndex, Is.EqualTo(4));
    }

    [Test]
    public void FromDocuments_ValidatesUnsavedMarkup()
    {
        LocalizationCsvDocument locales = CreateLocalesDocument();
        LocalizationCsvDocument strings = CreateStringsDocument(
            "[style=missing]Unsaved[/style]");

        LocalizationValidationResult result =
            LocalizationValidator.Validate(
                LocalizationSourceModel.FromDocuments(
                    locales,
                    strings),
                false);

        Assert.That(
            HasIssue(result, "Unknown style 'missing'."),
            Is.True);
    }

    [Test]
    public void CatalogSource_HasNoLocalizationValidationErrors()
    {
        LocalizationValidationResult result =
            LocalizationValidator.Validate(
                LocalizationCodeGenerator.LoadSource(),
                false);

        Assert.That(result.ErrorCount, Is.EqualTo(0));
    }

    [Test]
    public void DynamicFontValidation_UsesSourceFaceWithoutChangingAtlas()
    {
        LocalizationFontCatalog catalog =
            AssetDatabase.LoadAssetAtPath<LocalizationFontCatalog>(
                FontCatalogPath);
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.GlobalDefaultFont, Is.Not.Null);
        Assert.That(
            catalog.GlobalDefaultFont.atlasPopulationMode,
            Is.Not.EqualTo(AtlasPopulationMode.Static));
        int characterCount =
            catalog.GlobalDefaultFont.characterTable.Count;

        LocalizationValidationResult result =
            LocalizationValidator.Validate(
                LocalizationSourceModel.FromDocuments(
                    CreateLocalesDocument(),
                    CreateStringsDocument("Fire 한글")),
                true);

        Assert.That(result.WarningCount, Is.EqualTo(0));
        Assert.That(
            catalog.GlobalDefaultFont.characterTable.Count,
            Is.EqualTo(characterCount));
    }

    [Test]
    public void CsvRoundTrip_PreservesMarkupAndComma()
    {
        LocalizationCsvDocument source = CreateStringsDocument(
            "[icon=fire] [style=damage]2, damage[/style][br]Next");

        string csv = LocalizationCsv.Serialize(source);
        LocalizationCsvDocument parsed = LocalizationCsv.Parse(csv);

        Assert.That(parsed.RowCount, Is.EqualTo(source.RowCount));
        Assert.That(
            parsed.Get(1, 4),
            Is.EqualTo(source.Get(1, 4)));
    }

    [Test]
    public void StageRowDeletion_RemovesOnlyRequestedInMemoryRow()
    {
        LocalizationCsvDocument document = CreateStringsDocument("First");
        document.Rows.Add(new List<string>
        {
            "test.editor.second",
            "Editor test",
            "body",
            string.Empty,
            "Second",
        });

        bool deleted = LocalizationEditorWindow.TryStageRowDeletion(
            document,
            1);

        Assert.That(deleted, Is.True);
        Assert.That(document.RowCount, Is.EqualTo(2));
        Assert.That(document.Get(1, 0), Is.EqualTo("test.editor.second"));
        Assert.That(
            LocalizationEditorWindow.TryStageRowDeletion(document, 0),
            Is.False,
            "The CSV header must not be deletable.");
    }

    [Test]
    public void StringInputClick_SelectsOnlyPrimaryMouseDownInsideField()
    {
        Rect inputRect = new(10f, 20f, 100f, 18f);
        Event insideClick = new()
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = new Vector2(20f, 25f),
        };
        Event rightClick = new()
        {
            type = EventType.MouseDown,
            button = 1,
            mousePosition = new Vector2(20f, 25f),
        };
        Event outsideClick = new()
        {
            type = EventType.MouseDown,
            button = 0,
            mousePosition = new Vector2(200f, 25f),
        };

        Assert.That(
            LocalizationEditorWindow.IsPrimaryInputClick(
                insideClick,
                inputRect),
            Is.True);
        Assert.That(
            LocalizationEditorWindow.IsPrimaryInputClick(
                rightClick,
                inputRect),
            Is.False);
        Assert.That(
            LocalizationEditorWindow.IsPrimaryInputClick(
                outsideClick,
                inputRect),
            Is.False);
    }

    [Test]
    public void MarkupCatalog_ProvidesEditorStyleAndIconOptions()
    {
        LocalizationMarkupCatalog catalog =
            AssetDatabase.LoadAssetAtPath<LocalizationMarkupCatalog>(
                MarkupCatalogPath);

        Assert.That(catalog, Is.Not.Null);
        Assert.That(
            ContainsStyle(catalog, "damage"),
            Is.True);
        Assert.That(
            ContainsIcon(catalog, "fire"),
            Is.True);
    }

    [Test]
    public void MarkupParser_RendersCatalogStyleAndLineBreak()
    {
        LocalizationMarkupCatalog catalog =
            AssetDatabase.LoadAssetAtPath<LocalizationMarkupCatalog>(
                MarkupCatalogPath);

        string rendered = LocalizationMarkupParser.Render(
            "[style=damage]Hit[/style][br]Next",
            catalog);

        Assert.That(rendered, Does.StartWith("<color=#"));
        Assert.That(rendered, Does.Contain("<b>Hit</b></color>"));
        Assert.That(rendered, Does.EndWith("\nNext"));
    }

    [Test]
    public void MarkupParser_MissingIconUsesVisibleFallback()
    {
        LocalizationMarkupCatalog catalog =
            AssetDatabase.LoadAssetAtPath<LocalizationMarkupCatalog>(
                MarkupCatalogPath);

        string rendered = LocalizationMarkupParser.Render(
            "[icon=missing_icon]",
            catalog);

        Assert.That(rendered, Is.EqualTo("[MISSING_ICON]"));
    }

    [Test]
    public void FontCatalog_DetachesDynamicProjectFontBeforeRendering()
    {
        LocalizationFontCatalog sourceCatalog =
            AssetDatabase.LoadAssetAtPath<LocalizationFontCatalog>(
                FontCatalogPath);
        Assert.That(sourceCatalog, Is.Not.Null);
        Assert.That(sourceCatalog.GlobalDefaultFont, Is.Not.Null);
        Assert.That(
            sourceCatalog.GlobalDefaultFont.atlasPopulationMode,
            Is.Not.EqualTo(AtlasPopulationMode.Static));

        LocalizationFontCatalog runtimeCatalog =
            Object.Instantiate(sourceCatalog);
        try
        {
            TMP_FontAsset sourceFont = sourceCatalog.GlobalDefaultFont;
            TMP_FontAsset runtimeFont =
                runtimeCatalog.PrepareFallbacks(sourceFont);

            Assert.That(runtimeFont, Is.Not.Null);
            Assert.That(runtimeFont, Is.Not.SameAs(sourceFont));
            Assert.That(
                runtimeFont.hideFlags & HideFlags.DontSave,
                Is.Not.EqualTo(HideFlags.None));
            Assert.That(
                runtimeFont.atlasPopulationMode,
                Is.EqualTo(AtlasPopulationMode.Dynamic));
            Assert.That(
                runtimeFont.atlasTextures[0],
                Is.Not.SameAs(sourceFont.atlasTextures[0]));
            Assert.That(
                runtimeFont.material,
                Is.Not.SameAs(sourceFont.material));
        }
        finally
        {
            Object.DestroyImmediate(runtimeCatalog);
        }
    }

    [Test]
    public void EditorFontRestore_OnlyPersistsProjectAssets()
    {
        LocalizationFontCatalog catalog =
            AssetDatabase.LoadAssetAtPath<LocalizationFontCatalog>(
                FontCatalogPath);
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.GlobalDefaultFont, Is.Not.Null);
        Assert.That(
            LocalizationPlayModeGuard.CanPersistEditorReference(
                catalog.GlobalDefaultFont),
            Is.True);

        TMP_SpriteAsset runtimeSprite =
            ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        try
        {
            runtimeSprite.hideFlags = HideFlags.HideAndDontSave;
            Assert.That(
                LocalizationPlayModeGuard.CanPersistEditorReference(
                    runtimeSprite),
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(runtimeSprite);
        }
    }

    [Test]
    public void FontResolver_UsesLocaleRoleAndPlayerSelection()
    {
        LocalizationFontCatalog sourceCatalog =
            AssetDatabase.LoadAssetAtPath<LocalizationFontCatalog>(
                FontCatalogPath);
        Assert.That(sourceCatalog, Is.Not.Null);
        Assert.That(sourceCatalog.GlobalDefaultFont, Is.Not.Null);

        Font sourceFont = sourceCatalog.GlobalDefaultFont.sourceFontFile;
        Assert.That(sourceFont, Is.Not.Null);
        TMP_FontAsset globalFont =
            CreateTestFont(sourceFont, "Test Global Font");
        TMP_FontAsset localeFont =
            CreateTestFont(sourceFont, "Test Locale Font");
        TMP_FontAsset localeRoleFont =
            CreateTestFont(sourceFont, "Test Locale Role Font");
        TMP_FontAsset wildcardRoleFont =
            CreateTestFont(sourceFont, "Test Wildcard Role Font");
        TMP_FontAsset playerFont =
            CreateTestFont(sourceFont, "Test Player Font");

        LocalizationFontCatalog testCatalog =
            ScriptableObject.CreateInstance<LocalizationFontCatalog>();
        ConfigureFontCatalog(
            testCatalog,
            globalFont,
            localeFont,
            localeRoleFont,
            wildcardRoleFont,
            playerFont);

        LocalizationFontCatalog previousCatalog =
            LocalizationService.FontCatalog;
        LocalizationMarkupCatalog previousMarkup =
            LocalizationService.MarkupCatalog;
        string previousLocale = LocalizationService.CurrentLocale;
        string previousFontId = LocalizationService.CurrentFontId;
        GameObject resolverObject = new("Localization Resolver Test");
        resolverObject.SetActive(false);
        LocalizationFontResolver resolver =
            resolverObject.AddComponent<LocalizationFontResolver>();
        SerializedObject resolverSerialized = new(resolver);
        resolverSerialized.FindProperty("fontCatalog").objectReferenceValue =
            testCatalog;
        resolverSerialized.ApplyModifiedPropertiesWithoutUndo();

        try
        {
            LocalizationService.Configure(testCatalog, previousMarkup);
            Assert.That(
                LocalizationService.SetLocale("en-US", false),
                Is.True);
            Assert.That(
                LocalizationService.SetFont(
                    LocalizationService.AutoFontId,
                    false),
                Is.True);

            AssertResolvedFontName(
                resolver.Resolve("title"),
                localeRoleFont.name);
            AssertResolvedFontName(
                resolver.Resolve("number"),
                wildcardRoleFont.name);
            AssertResolvedFontName(
                resolver.Resolve("body"),
                localeFont.name);

            Assert.That(
                LocalizationService.SetFont("PLAYER", false),
                Is.True);
            AssertResolvedFontName(
                resolver.Resolve("title"),
                playerFont.name);

            Assert.That(
                testCatalog.Resolve(
                    "unconfigured-locale",
                    "body",
                    LocalizationService.AutoFontId),
                Is.SameAs(globalFont));
        }
        finally
        {
            LocalizationService.Configure(previousCatalog, previousMarkup);
            LocalizationService.SetLocale(previousLocale, false);
            LocalizationService.SetFont(previousFontId, false);
            Object.DestroyImmediate(resolverObject);
            Object.DestroyImmediate(testCatalog);
            Object.DestroyImmediate(globalFont);
            Object.DestroyImmediate(localeFont);
            Object.DestroyImmediate(localeRoleFont);
            Object.DestroyImmediate(wildcardRoleFont);
            Object.DestroyImmediate(playerFont);
        }
    }

    private static void ConfigureFontCatalog(
        LocalizationFontCatalog catalog,
        TMP_FontAsset globalFont,
        TMP_FontAsset localeFont,
        TMP_FontAsset localeRoleFont,
        TMP_FontAsset wildcardRoleFont,
        TMP_FontAsset playerFont)
    {
        SerializedObject serialized = new(catalog);
        serialized.FindProperty("globalDefaultFont").objectReferenceValue =
            globalFont;

        SerializedProperty localeFonts =
            serialized.FindProperty("localeFonts");
        localeFonts.arraySize = 1;
        SerializedProperty localeEntry =
            localeFonts.GetArrayElementAtIndex(0);
        localeEntry.FindPropertyRelative("locale").stringValue = "en-US";
        localeEntry.FindPropertyRelative("font").objectReferenceValue =
            localeFont;

        SerializedProperty roleFonts = serialized.FindProperty("roleFonts");
        roleFonts.arraySize = 2;
        ConfigureRoleFont(
            roleFonts.GetArrayElementAtIndex(0),
            "en-US",
            "title",
            localeRoleFont);
        ConfigureRoleFont(
            roleFonts.GetArrayElementAtIndex(1),
            "*",
            "number",
            wildcardRoleFont);

        SerializedProperty selectableFonts =
            serialized.FindProperty("selectableFonts");
        selectableFonts.arraySize = 1;
        SerializedProperty selectableEntry =
            selectableFonts.GetArrayElementAtIndex(0);
        selectableEntry.FindPropertyRelative("id").stringValue = "PLAYER";
        selectableEntry.FindPropertyRelative("displayName").stringValue =
            "Player Font";
        selectableEntry.FindPropertyRelative("font").objectReferenceValue =
            playerFont;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TMP_FontAsset CreateTestFont(
        Font sourceFont,
        string name)
    {
        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(sourceFont);
        Assert.That(font, Is.Not.Null);
        font.name = name;
        return font;
    }

    private static void ConfigureRoleFont(
        SerializedProperty entry,
        string locale,
        string role,
        TMP_FontAsset font)
    {
        entry.FindPropertyRelative("locale").stringValue = locale;
        entry.FindPropertyRelative("role").stringValue = role;
        entry.FindPropertyRelative("font").objectReferenceValue = font;
    }

    private static void AssertResolvedFontName(
        TMP_FontAsset resolved,
        string expectedSourceName)
    {
        Assert.That(resolved, Is.Not.Null);
        Assert.That(resolved.name, Does.StartWith(expectedSourceName));
    }

    private static LocalizationCsvDocument CreateLocalesDocument()
    {
        LocalizationCsvDocument document = new();
        document.Rows.Add(new List<string>
        {
            "locale",
            "display_name",
            "fallback",
            "default_font_role",
        });
        document.Rows.Add(new List<string>
        {
            "en-US",
            "English",
            string.Empty,
            "body",
        });
        return document;
    }

    private static LocalizationCsvDocument CreateStringsDocument(
        string translation)
    {
        LocalizationCsvDocument document = new();
        document.Rows.Add(new List<string>
        {
            "key",
            "context",
            "font_role",
            "note",
            "en-US",
        });
        document.Rows.Add(new List<string>
        {
            "test.editor.markup",
            "Editor test",
            "body",
            string.Empty,
            translation,
        });
        return document;
    }

    private static bool HasIssue(
        LocalizationValidationResult result,
        string message)
    {
        foreach (LocalizationValidationIssue issue in result.Issues)
        {
            if (issue.Message == message)
                return true;
        }

        return false;
    }

    private static bool ContainsStyle(
        LocalizationMarkupCatalog catalog,
        string id)
    {
        foreach (LocalizationMarkupStyleDefinition style in
                 catalog.Styles)
        {
            if (style != null && style.Id == id)
                return true;
        }

        return false;
    }

    private static bool ContainsIcon(
        LocalizationMarkupCatalog catalog,
        string id)
    {
        foreach (LocalizationIconDefinition icon in catalog.Icons)
        {
            if (icon != null && icon.Id == id)
                return true;
        }

        return false;
    }
}
