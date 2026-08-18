using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PracticeBattleCharacterDataTests
{
    private readonly List<Object> _createdObjects = new();
    private CharacterSO _definition;
    private DataManager _previousDataManager;

    [SetUp]
    public void SetUp()
    {
        _previousDataManager = DataManager.Current;
        _definition = ScriptableObject.CreateInstance<CharacterSO>();
        SetPrivateField(_definition, "characterId", "practice_detached");
        SetPrivateField(_definition, "initiallyOwned", false);
    }

    [TearDown]
    public void TearDown()
    {
        SetCurrentDataManager(_previousDataManager);
        for (int index = _createdObjects.Count - 1; index >= 0; index--)
        {
            if (_createdObjects[index] != null)
                Object.DestroyImmediate(_createdObjects[index]);
        }
        _createdObjects.Clear();
        if (_definition != null)
            Object.DestroyImmediate(_definition);
    }

    [Test]
    public void DetachedRuntimeData_DoesNotCreatePersistentProgress()
    {
        CharacterCollectionData collection = new();

        CharacterData runtime =
            collection.CreateDetachedRuntimeData(_definition);

        Assert.That(runtime, Is.Not.Null);
        Assert.That(runtime.CharacterId, Is.EqualTo("practice_detached"));
        Assert.That(collection.Characters, Is.Empty);
    }

    [Test]
    public void DetachedRuntimeData_CopiesExistingProgressWithoutRegistering()
    {
        CharacterCollectionData collection = new();
        CharacterProgressData progress = collection.GetOrCreate(
            _definition,
            true);
        Assert.That(progress, Is.Not.Null);
        Assert.That(collection.Characters.Count, Is.EqualTo(1));

        CharacterData runtime =
            collection.CreateDetachedRuntimeData(_definition);

        Assert.That(runtime, Is.Not.Null);
        Assert.That(runtime.IsOwned, Is.True);
        Assert.That(collection.Characters.Count, Is.EqualTo(1));
        Assert.That(collection.Characters[0], Is.SameAs(progress));
    }

    [Test]
    public void CharacterRuntimeAwake_PracticePendingUsesDetachedData()
    {
        CharacterCollectionData collection = new();
        CreateDataManager(collection);
        DungeonPage page = CreateInactiveDungeonPage();
        DungeonDefinition practice =
            ScriptableObject.CreateInstance<DungeonDefinition>();
        _createdObjects.Add(practice);
        SetPrivateField(practice, "runMode", EDungeonRunMode.Practice);
        page.PrepareDungeon(practice);

        CharacterRuntime character = CreateInactiveCharacter(page.transform);
        InvokeAwake(character);

        Assert.That(character.Data, Is.Not.Null);
        Assert.That(
            character.Data.CharacterId,
            Is.EqualTo("practice_detached"));
        Assert.That(collection.Characters, Is.Empty);
    }

    [Test]
    public void CharacterRuntimeAwake_StandardPendingKeepsPersistentData()
    {
        CharacterCollectionData collection = new();
        CreateDataManager(collection);
        DungeonPage page = CreateInactiveDungeonPage();
        DungeonDefinition standard =
            ScriptableObject.CreateInstance<DungeonDefinition>();
        _createdObjects.Add(standard);
        SetPrivateField(standard, "runMode", EDungeonRunMode.Standard);
        page.PrepareDungeon(standard);

        CharacterRuntime character = CreateInactiveCharacter(page.transform);
        InvokeAwake(character);

        Assert.That(character.Data, Is.Not.Null);
        Assert.That(collection.Characters.Count, Is.EqualTo(1));
        Assert.That(
            collection.Characters[0].CharacterId,
            Is.EqualTo("practice_detached"));
    }

    private void CreateDataManager(CharacterCollectionData collection)
    {
        GameObject host = new("PracticeBattleCharacterDataTests_DataManager");
        host.SetActive(false);
        _createdObjects.Add(host);
        DataManager manager = host.AddComponent<DataManager>();
        manager.CharacterDatas = collection;
        SetCurrentDataManager(manager);
    }

    private DungeonPage CreateInactiveDungeonPage()
    {
        GameObject host = new(
            "PracticeBattleCharacterDataTests_DungeonPage",
            typeof(RectTransform));
        host.SetActive(false);
        _createdObjects.Add(host);
        return host.AddComponent<DungeonPage>();
    }

    private CharacterRuntime CreateInactiveCharacter(Transform parent)
    {
        GameObject prefab = Resources.Load<GameObject>(
            "Presentation/CharacterInfo");
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = Object.Instantiate(prefab, parent, false);
        _createdObjects.Add(instance);
        CharacterRuntime character =
            instance.GetComponent<CharacterRuntime>();
        Assert.That(character, Is.Not.Null);
        SetPrivateField(character, "original", _definition);
        return character;
    }

    private static void InvokeAwake(CharacterRuntime character)
    {
        MethodInfo awake = typeof(CharacterRuntime).GetMethod(
            "Awake",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(awake, Is.Not.Null);
        awake.Invoke(character, null);
    }

    private static void SetCurrentDataManager(DataManager manager)
    {
        PropertyInfo current = typeof(DataManager).GetProperty(
            nameof(DataManager.Current),
            BindingFlags.Static | BindingFlags.Public);
        MethodInfo setter = current?.GetSetMethod(true);
        Assert.That(setter, Is.Not.Null);
        setter.Invoke(null, new object[] { manager });
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }
}
