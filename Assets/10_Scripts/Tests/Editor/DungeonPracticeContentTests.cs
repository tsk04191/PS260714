using System.Linq;
using NUnit.Framework;
using PS260714.Localization;
using UnityEditor;

public sealed class DungeonPracticeContentTests
{
    private const string TutorialFieldPath =
        "Assets/06_Runtime/Resources/Dungeons/TutorialField.asset";
    private const string PracticeBattlePath =
        "Assets/06_Runtime/Resources/Dungeons/PracticeBattle.asset";
    private const string FreeBattlePath =
        "Assets/06_Runtime/Resources/Dungeons/FreeBattle.asset";

    [SetUp]
    public void SetUp()
    {
        DungeonDefinitionCatalog.Invalidate();
    }

    [TearDown]
    public void TearDown()
    {
        DungeonDefinitionCatalog.Invalidate();
    }

    [Test]
    public void BuiltInDefinitions_UseCanonicalIdsModesAndStageOrder()
    {
        DungeonDefinition tutorial = LoadDefinition(TutorialFieldPath);
        DungeonDefinition practice = LoadDefinition(PracticeBattlePath);
        DungeonDefinition freeBattle = LoadDefinition(FreeBattlePath);

        Assert.That(
            tutorial.DungeonId,
            Is.EqualTo(DungeonDefinitionCatalog.TutorialFieldId));
        Assert.That(tutorial.RunMode, Is.EqualTo(EDungeonRunMode.Standard));
        Assert.That(tutorial.StageOrder, Is.EqualTo(1));
        Assert.That(
            tutorial.TitleLocalizationKey,
            Is.EqualTo(LocalizationKeys.UiStageSelectTutorialField));

        Assert.That(
            practice.DungeonId,
            Is.EqualTo(DungeonDefinitionCatalog.PracticeBattleId));
        Assert.That(practice.RunMode, Is.EqualTo(EDungeonRunMode.Practice));
        Assert.That(practice.IsPractice, Is.True);
        Assert.That(practice.UsesStandardBattleCompletion, Is.False);
        Assert.That(practice.AwardsBattleRewards, Is.False);
        Assert.That(practice.PersistsDungeonProgress, Is.False);
        Assert.That(practice.SelectStartingCharacter, Is.False);
        Assert.That(practice.SelectStartingItems, Is.False);
        Assert.That(practice.StageOrder, Is.Zero);
        Assert.That(
            practice.TitleLocalizationKey,
            Is.EqualTo(LocalizationKeys.UiStageSelectPracticeBattle));
        Assert.That(
            practice.BuildPhaseSequence(1, 260714),
            Is.EqualTo(new[] { EDungeonPhase.Battle }));

        Assert.That(freeBattle.RunMode, Is.EqualTo(EDungeonRunMode.Standard));
        Assert.That(freeBattle.StageOrder, Is.EqualTo(2));
        Assert.That(tutorial.TryValidate(out string tutorialError),
            Is.True,
            tutorialError);
        Assert.That(practice.TryValidate(out string practiceError),
            Is.True,
            practiceError);
        Assert.That(freeBattle.TryValidate(out string freeBattleError),
            Is.True,
            freeBattleError);
    }

    [Test]
    public void Catalog_LegacyTestFieldLookupResolvesCanonicalTutorial()
    {
        DungeonDefinition legacy = DungeonDefinitionCatalog.Get(
            DungeonDefinitionCatalog.LegacyTestFieldId);
        DungeonDefinition canonical = DungeonDefinitionCatalog.Get(
            DungeonDefinitionCatalog.TutorialFieldId);

        Assert.That(legacy, Is.SameAs(canonical));
        Assert.That(
            canonical.DungeonId,
            Is.EqualTo(DungeonDefinitionCatalog.TutorialFieldId));
        Assert.That(
            DungeonDefinitionCatalog.GetStageSelectDefinitions()
                .Count(definition => string.Equals(
                    definition.DungeonId,
                    DungeonDefinitionCatalog.TutorialFieldId)),
            Is.EqualTo(1));
    }

    [Test]
    public void ProgressImport_MergesLegacyAndCanonicalTutorialByMaximum()
    {
        const string json =
            "{\"version\":1,\"entries\":[" +
            "{\"dungeonId\":\"test_field\",\"cleared\":true," +
            "\"clearCount\":3,\"clearedContentVersion\":1}," +
            "{\"dungeonId\":\"tutorial_field\",\"cleared\":true," +
            "\"clearCount\":7,\"clearedContentVersion\":2}," +
            "{\"dungeonId\":\"free_battle\",\"cleared\":true," +
            "\"clearCount\":4,\"clearedContentVersion\":1}]}";
        DungeonProgressData progress = new();

        Assert.That(progress.ImportJson(json), Is.True);
        Assert.That(
            progress.GetClearCount(DungeonDefinitionCatalog.LegacyTestFieldId),
            Is.EqualTo(7));
        Assert.That(
            progress.GetClearCount(DungeonDefinitionCatalog.TutorialFieldId),
            Is.EqualTo(7));
        Assert.That(
            progress.GetClearCount(DungeonDefinitionCatalog.FreeBattleId),
            Is.EqualTo(4));

        string exported = progress.ExportJson();
        StringAssert.Contains("\"dungeonId\":\"tutorial_field\"", exported);
        StringAssert.DoesNotContain("\"dungeonId\":\"test_field\"", exported);
        StringAssert.Contains("\"dungeonId\":\"free_battle\"", exported);
        StringAssert.Contains("\"clearedContentVersion\":2", exported);
    }

    [Test]
    public void PracticeProgress_IsNeverRecordedThroughDefinitionOrKnownId()
    {
        DungeonDefinition practice = LoadDefinition(PracticeBattlePath);
        DungeonProgressData progress = new();

        Assert.That(progress.MarkCleared(practice, false), Is.False);
        Assert.That(
            progress.MarkCleared(
                DungeonDefinitionCatalog.PracticeBattleId,
                save: false),
            Is.False);
        Assert.That(progress.IsCleared(practice), Is.False);
        Assert.That(
            progress.GetClearCount(DungeonDefinitionCatalog.PracticeBattleId),
            Is.Zero);

        const string imported =
            "{\"version\":1,\"entries\":[" +
            "{\"dungeonId\":\"practice_battle\",\"cleared\":true," +
            "\"clearCount\":9,\"clearedContentVersion\":1}]}";
        Assert.That(progress.ImportJson(imported), Is.True);
        Assert.That(
            progress.GetClearCount(DungeonDefinitionCatalog.PracticeBattleId),
            Is.Zero);
        StringAssert.DoesNotContain(
            "\"dungeonId\":\"practice_battle\"",
            progress.ExportJson());
    }

    private static DungeonDefinition LoadDefinition(string path)
    {
        DungeonDefinition definition =
            AssetDatabase.LoadAssetAtPath<DungeonDefinition>(path);
        Assert.That(definition, Is.Not.Null, path);
        return definition;
    }
}
