using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using static TestReflection;

public sealed class EnemyWaveComposerTests
{
    private readonly List<Object> created = new();
    private int nextId;

    [TearDown]
    public void TearDown()
    {
        for (int index = 0; index < created.Count; index++)
        {
            if (created[index] != null)
                Object.DestroyImmediate(created[index]);
        }
        created.Clear();
    }

    [Test]
    public void AutomaticSelection_NeverReturnsBossOrEncounterOnly()
    {
        EnemySO boss = Enemy(EEnemyGrade.Boss, encounterOnly: true);
        EnemySO encounter = Enemy(EEnemyGrade.Normal, encounterOnly: true);
        EnemyWaveCompositionState state = new();

        EnemySO selected = EnemyWaveComposer.SelectAndRegister(
            new[] { boss, encounter },
            1f,
            new System.Random(1),
            state);

        Assert.That(selected, Is.Null);
    }

    [Test]
    public void SpecialAndEliteCaps_FallBackToNormalEnemy()
    {
        EnemySO special = Enemy(EEnemyGrade.Special);
        EnemySO elite = Enemy(EEnemyGrade.Elite);
        EnemySO normal = Enemy(EEnemyGrade.Normal);
        EnemyWaveCompositionState state = new();
        System.Random random = new(2);

        Assert.That(SelectOnly(special, state, random), Is.SameAs(special));
        Assert.That(SelectOnly(special, state, random), Is.SameAs(special));
        Assert.That(
            EnemyWaveComposer.SelectAndRegister(
                new[] { special, normal },
                0.5f,
                random,
                state),
            Is.SameAs(normal));

        Assert.That(SelectOnly(elite, state, random), Is.SameAs(elite));
        Assert.That(
            EnemyWaveComposer.SelectAndRegister(
                new[] { elite, normal },
                0.5f,
                random,
                state),
            Is.SameAs(normal));
        Assert.That(state.SpecialCount, Is.EqualTo(2));
        Assert.That(state.EliteCount, Is.EqualTo(1));
    }

    [Test]
    public void SupportAndPerEnemyCaps_AreEnforced()
    {
        EnemySO support = Enemy(
            EEnemyGrade.Normal,
            roleTags: new[] { "support" });
        EnemySO limited = Enemy(
            EEnemyGrade.Normal,
            recommendedMaximum: 1);
        EnemySO normal = Enemy(EEnemyGrade.Normal);
        EnemyWaveCompositionState state = new();
        System.Random random = new(3);

        Assert.That(SelectOnly(support, state, random), Is.SameAs(support));
        Assert.That(SelectOnly(support, state, random), Is.SameAs(support));
        Assert.That(
            EnemyWaveComposer.SelectAndRegister(
                new[] { support, normal },
                0.5f,
                random,
                state),
            Is.SameAs(normal));
        Assert.That(state.SupportCount, Is.EqualTo(2));

        Assert.That(SelectOnly(limited, state, random), Is.SameAs(limited));
        Assert.That(
            EnemyWaveComposer.SelectAndRegister(
                new[] { limited, normal },
                0.5f,
                random,
                state),
            Is.SameAs(normal));
        Assert.That(state.GetCount(limited), Is.EqualTo(1));
    }

    [Test]
    public void AutomaticHealth_UsesRosterMultiplierAndPreservesLegacy()
    {
        EnemySO roster = Enemy(EEnemyGrade.Normal);
        SetField(roster, "baseHealth", 44);
        SetField(roster, "healthScale", 1f);
        Assert.That(
            DungeonPage.ScaleRosterEnemyHealth(10, roster),
            Is.EqualTo(22));

        EnemySO legacy = Enemy(EEnemyGrade.Normal);
        SetField(legacy, "rosterSchemaVersion", 0);
        SetField(legacy, "baseHealth", 60);
        SetField(legacy, "healthScale", 2f);
        Assert.That(
            DungeonPage.ScaleRosterEnemyHealth(10, legacy),
            Is.EqualTo(10));
    }

    private static EnemySO SelectOnly(
        EnemySO definition,
        EnemyWaveCompositionState state,
        System.Random random)
    {
        return EnemyWaveComposer.SelectAndRegister(
            new[] { definition },
            0.5f,
            random,
            state);
    }

    private EnemySO Enemy(
        EEnemyGrade grade,
        bool encounterOnly = false,
        IReadOnlyList<string> roleTags = null,
        int recommendedMaximum = 0)
    {
        EnemySO definition = ScriptableObject.CreateInstance<EnemySO>();
        created.Add(definition);
        SetField(definition, "enemyId", $"test.wave.{nextId++}");
        SetField(definition, "grade", grade);
        SetField(definition, "rosterSchemaVersion", 1);
        SetField(definition, "encounterOnly", encounterOnly);
        SetField(definition, "recommendedMaxPerWave", recommendedMaximum);
        SetField(definition, "spawnBudget", 1f);
        SetField(
            definition,
            "roleTags",
            roleTags != null
                ? new List<string>(roleTags)
                : new List<string>());
        return definition;
    }
}
