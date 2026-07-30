using NUnit.Framework;
using UnityEngine;

public sealed class RecruitRevealTests
{
    [Test]
    public void Presentation_TenRowsCompleteAfterFifteenPointFiveSeconds()
    {
        RecruitRevealPresentationSO presentation =
            RecruitRevealPresentationSO.Load();

        Assert.That(presentation, Is.Not.Null);
        Assert.That(presentation.MaximumRows, Is.EqualTo(10));
        Assert.That(presentation.RowStartInterval, Is.EqualTo(1.5f));
        Assert.That(presentation.RowSpinDuration, Is.EqualTo(2f));
        Assert.That(
            presentation.GetMinimumRevealDuration(10),
            Is.EqualTo(15.5f).Within(0.001f));
    }

    [Test]
    public void Presentation_TenRowsFitInsideShortScreenArea()
    {
        RecruitRevealPresentationSO presentation =
            RecruitRevealPresentationSO.Load();
        const float availableHeight = 489.6f;

        float rowHeight =
            presentation.GetFittedMultiRowHeight(
                availableHeight,
                10,
                out float spacing);
        float occupiedHeight =
            rowHeight * 10f + spacing * 9f;

        Assert.That(rowHeight, Is.GreaterThan(0f));
        Assert.That(
            occupiedHeight,
            Is.LessThanOrEqualTo(availableHeight + 0.001f));
    }

    [Test]
    public void RevealEntry_FromDummyPreservesDisplayNameAndGrade()
    {
        RecruitDummyPoolEntry dummy =
            JsonUtility.FromJson<RecruitDummyPoolEntry>(
                "{\"displayName\":\"TEST OPERATOR\"," +
                "\"grade\":3,\"rate\":1}");

        RecruitRevealEntry entry =
            RecruitRevealEntry.FromDummy(dummy, 4);

        Assert.That(entry.RewardId, Is.EqualTo("dummy.4"));
        Assert.That(entry.DisplayName, Is.EqualTo("TEST OPERATOR"));
        Assert.That(entry.Grade, Is.EqualTo(CharacterGrade.Grade3));
        Assert.That(entry.Icon, Is.Null);
        Assert.That(entry.IsNew, Is.False);
    }

    [Test]
    public void RevealEntry_ClampsUnsupportedGrade()
    {
        RecruitRevealEntry entry = new(
            "future.operator",
            "FUTURE OPERATOR",
            (CharacterGrade)99,
            null,
            true);

        Assert.That(entry.Grade, Is.EqualTo(CharacterGrade.Grade3));
        Assert.That(entry.IsNew, Is.True);
    }
}
