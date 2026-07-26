using System.Collections.Generic;
using NUnit.Framework;
using PS260714.Localization;
using PS260714.Localization.Editor;
using UnityEditor;

public sealed class LocalizationEditorTests
{
    private const string MarkupCatalogPath =
        "Assets/Resources/Localization/LocalizationMarkupCatalog.asset";

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
