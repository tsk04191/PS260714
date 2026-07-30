using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CharacterGradeTests
{
    [Test]
    public void CharacterDefinition_StoresGradeThree()
    {
        CharacterSO definition =
            ScriptableObject.CreateInstance<CharacterSO>();
        try
        {
            SerializedObject serialized = new(definition);
            serialized.FindProperty("grade").enumValueIndex =
                (int)CharacterGrade.Grade3;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                definition.Grade,
                Is.EqualTo(CharacterGrade.Grade3));
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void SharedPalette_ProvidesStyleForEveryGrade()
    {
        CharacterGradePresentation.Invalidate();
        Assert.That(CharacterGradePresentation.Palette, Is.Not.Null);

        for (int value = 0; value <= 3; value++)
        {
            CharacterGrade grade = (CharacterGrade)value;
            CharacterGradeStyle style =
                CharacterGradePresentation.GetStyle(grade);
            Assert.That(style, Is.Not.Null);
            Assert.That(style.PrimaryColor.a, Is.GreaterThan(0f));
            Assert.That(style.BackgroundColor.a, Is.GreaterThan(0f));
            Assert.That(style.OutlineColor.a, Is.GreaterThan(0f));
            Assert.That(style.TextColor.a, Is.GreaterThan(0f));
        }
    }

    [Test]
    public void DummyPoolEntry_UsesSharedGradeColor()
    {
        RecruitDummyPoolEntry entry =
            JsonUtility.FromJson<RecruitDummyPoolEntry>(
                "{\"grade\":2,\"rate\":1}");

        Assert.That(
            entry.Grade,
            Is.EqualTo(CharacterGrade.Grade2));
        Assert.That(
            entry.DisplayColor,
            Is.EqualTo(
                CharacterGradePresentation.GetPrimaryColor(
                    CharacterGrade.Grade2)));
    }
}
