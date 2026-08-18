using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EnemyFormationSettingsTests
{
    private const string WorldActorPrefabPath =
        "Assets/06_Runtime/Resources/Presentation/DungeonWorld/" +
        "DungeonWorldActor.prefab";
    private const string HudPresentationPath =
        "Assets/06_Runtime/Resources/Presentation/" +
        "DungeonHudPresentation.asset";
    private static readonly BindingFlags InstanceFields =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    private readonly List<Object> _createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = _createdObjects.Count - 1;
             index >= 0;
             index--)
        {
            if (_createdObjects[index] != null)
                Object.DestroyImmediate(_createdObjects[index]);
        }

        _createdObjects.Clear();
    }

    [Test]
    public void ArenaSetup_MaximumActiveEnemiesControlsCapacity()
    {
        BattleArenaSetup setup = BattleArenaSetup.CreateCircular(
            maximumActiveEnemies: 21);

        Assert.That(setup.MaximumActiveEnemies, Is.EqualTo(21));
        Assert.That(setup.MaximumEnemyCapacity, Is.EqualTo(21));
        Assert.That(
            setup.WithMaximumActiveEnemies(33)
                .MaximumEnemyCapacity,
            Is.EqualTo(33));
    }

    [Test]
    public void DefaultArenaAndDungeonAssets_AllowOneHundredActiveEnemies()
    {
        Assert.That(
            BattleArenaSetup.CreateCircular().MaximumActiveEnemies,
            Is.EqualTo(100));

        string[] paths =
        {
            "Assets/06_Runtime/Resources/Dungeons/TutorialField.asset",
            "Assets/06_Runtime/Resources/Dungeons/PracticeBattle.asset",
            "Assets/06_Runtime/Resources/Dungeons/FreeBattle.asset",
        };
        foreach (string path in paths)
        {
            DungeonDefinition definition =
                AssetDatabase.LoadAssetAtPath<DungeonDefinition>(path);
            Assert.That(definition, Is.Not.Null, path);
            Assert.That(
                definition.MaximumActiveEnemies,
                Is.EqualTo(100),
                path);
        }
    }

    [Test]
    public void WorldEnemyActor_HasAuthoredHealthBarReferencesAndStyle()
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(WorldActorPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        DungeonWorldActorPrefabView view =
            prefab.GetComponent<DungeonWorldActorPrefabView>();
        Assert.That(view, Is.Not.Null);
        Assert.That(view.HasRequiredReferences, Is.True);
        Assert.That(view.EnemyHealthTrack, Is.Not.Null);
        Assert.That(view.EnemyHealthFill, Is.Not.Null);

        DungeonHudPresentationSO style =
            AssetDatabase.LoadAssetAtPath<DungeonHudPresentationSO>(
                HudPresentationPath);
        Assert.That(style, Is.Not.Null);
        Assert.That(style.EnemyHealthBarWidth, Is.GreaterThan(0f));
        Assert.That(style.EnemyHealthBarThickness, Is.GreaterThan(0f));
        Assert.That(
            style.EnemyHealthCriticalThreshold,
            Is.InRange(0.01f, 1f));
    }

    [TestCase(1, BattleArenaSetup.MinimumMaximumActiveEnemies)]
    [TestCase(1000, BattleArenaSetup.MaximumActiveEnemiesLimit)]
    public void ArenaSetup_MaximumActiveEnemiesIsNormalized(
        int authoredValue,
        int expected)
    {
        BattleArenaSetup setup = BattleArenaSetup.CreateCircular(
            maximumActiveEnemies: authoredValue);

        Assert.That(
            setup.MaximumActiveEnemies,
            Is.EqualTo(expected));
    }

    [Test]
    public void BattleSetup_InitialEnemiesRemainBoundedByLegacyWaveLimit()
    {
        BattleArenaSetup arena = BattleArenaSetup.CreateCircular(
            maximumLayerCount: 3,
            maximumEnemiesPerLayer: 5);
        List<EnemyRuntime> enemies = new();
        for (int index = 0; index < 20; index++)
            enemies.Add(null);

        BattleSetup setup = new(
            8,
            1,
            1f,
            60f,
            new BattleEnemyGradeCounts(20, 0, 0, 0),
            enemies,
            initialEnemyCount: 20,
            arena: arena);

        Assert.That(setup.InitialEnemyCount, Is.EqualTo(5));
    }

    [Test]
    public void DungeonDefinition_ProvidesClampedActiveEnemyDefault()
    {
        DungeonDefinition definition = Create<DungeonDefinition>();

        Assert.That(
            definition.MaximumActiveEnemies,
            Is.EqualTo(
                DungeonDefinition.DefaultMaximumActiveEnemies));

        SetField(definition, "maximumActiveEnemies", 1);
        Assert.That(
            definition.MaximumActiveEnemies,
            Is.EqualTo(BattleArenaSetup.MinimumMaximumActiveEnemies));

        SetField(definition, "maximumActiveEnemies", 1000);
        Assert.That(
            definition.MaximumActiveEnemies,
            Is.EqualTo(
                BattleArenaSetup.MaximumActiveEnemiesLimit));
    }

    [Test]
    public void BattleSetting_InheritsDungeonActiveMaximumWhenOverrideIsDisabled()
    {
        BattleSO battle = Create<BattleSO>();
        SetField(
            battle,
            "overrideDungeonMaximumActiveEnemies",
            false);
        SetField(battle, "circularMaximumActiveEnemies", 5);

        Assert.That(
            battle.ResolveMaximumActiveEnemies(23),
            Is.EqualTo(23));
        Assert.That(
            battle.CreateArenaSetup(23).MaximumActiveEnemies,
            Is.EqualTo(23));
        Assert.That(
            battle.ResolveMaximumActiveEnemies(1),
            Is.EqualTo(BattleArenaSetup.MinimumMaximumActiveEnemies));
        Assert.That(
            battle.ResolveMaximumActiveEnemies(1000),
            Is.EqualTo(
                BattleArenaSetup.MaximumActiveEnemiesLimit));
    }

    [Test]
    public void BattleSetting_OverrideUsesBattleActiveMaximum()
    {
        BattleSO battle = Create<BattleSO>();
        SetField(
            battle,
            "overrideDungeonMaximumActiveEnemies",
            true);
        SetField(battle, "circularMaximumActiveEnemies", 29);

        Assert.That(battle.OverrideDungeonMaximumActiveEnemies, Is.True);
        Assert.That(battle.CircularMaximumActiveEnemies, Is.EqualTo(29));
        Assert.That(
            battle.ResolveMaximumActiveEnemies(31),
            Is.EqualTo(29));
        Assert.That(
            battle.CreateArenaSetup(31).MaximumActiveEnemies,
            Is.EqualTo(29));
        Assert.That(
            battle.CreateArenaSetup().MaximumActiveEnemies,
            Is.EqualTo(29));
    }

    private T Create<T>() where T : ScriptableObject
    {
        T instance = ScriptableObject.CreateInstance<T>();
        instance.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(instance);
        return instance;
    }

    private static void SetField<T>(
        object target,
        string fieldName,
        T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            InstanceFields);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }
}
