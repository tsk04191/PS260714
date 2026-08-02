using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ItemDefinitionTests
{
    private const string InventoryKey = "Inventory.Collection.v1";
    private const string InventoryBackupKey =
        InventoryKey + ".backup";
    private const string InventoryCorruptKey =
        InventoryKey + ".corrupt";
    private const string CharacterKey = "Characters.Collection.v1";
    private const string CharacterBackupKey =
        CharacterKey + ".backup";
    private const string CharacterCorruptKey =
        CharacterKey + ".corrupt";

    private static readonly string[] SaveKeys =
    {
        InventoryKey,
        InventoryBackupKey,
        InventoryCorruptKey,
        CharacterKey,
        CharacterBackupKey,
        CharacterCorruptKey,
    };

    private readonly Dictionary<string, (bool Exists, string Value)>
        _savedPlayerPrefs = new();

    [SetUp]
    public void PreservePlayerPrefs()
    {
        _savedPlayerPrefs.Clear();
        foreach (string key in SaveKeys)
        {
            bool exists = PlayerPrefs.HasKey(key);
            _savedPlayerPrefs[key] = (
                exists,
                exists ? PlayerPrefs.GetString(key) : string.Empty);
            PlayerPrefs.DeleteKey(key);
        }

        PlayerPrefs.Save();
    }

    [TearDown]
    public void RestorePlayerPrefs()
    {
        foreach (string key in SaveKeys)
        {
            (bool exists, string value) = _savedPlayerPrefs[key];
            if (exists)
                PlayerPrefs.SetString(key, value);
            else
                PlayerPrefs.DeleteKey(key);
        }

        PlayerPrefs.Save();
    }

    [Test]
    public void InitialAmount_IsClampedToMaximumStack()
    {
        CurrencyItemSO item =
            ScriptableObject.CreateInstance<CurrencyItemSO>();
        try
        {
            ConfigureItem(item, "test.item", 100L, 250L);

            Assert.That(item.MaximumStack, Is.EqualTo(100L));
            Assert.That(item.InitialAmount, Is.EqualTo(100L));
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void BattleItemRunState_SingleUseIsConsumedAfterSuccess()
    {
        BattleItemSO item =
            ScriptableObject.CreateInstance<BattleItemSO>();
        try
        {
            ConfigureBattleItem(
                item,
                "test.battle.single",
                BattleItemUsePolicy.SingleUse,
                2,
                0,
                0f);
            BattleItemRunState state = new(item);

            Assert.That(state.Acquire(item), Is.True);
            Assert.That(state.RemainingUses, Is.EqualTo(1));
            Assert.That(state.CanUse(item), Is.True);
            Assert.That(state.CompleteSuccessfulUse(item), Is.True);
            Assert.That(state.IsOwned, Is.False);
            Assert.That(state.RemainingUses, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void BattleItemRunState_LimitedUsesAccumulateAndRespectCap()
    {
        BattleItemSO item =
            ScriptableObject.CreateInstance<BattleItemSO>();
        try
        {
            ConfigureBattleItem(
                item,
                "test.battle.limited",
                BattleItemUsePolicy.LimitedUse,
                3,
                5,
                0f);
            BattleItemRunState state = new(item);

            Assert.That(state.Acquire(item), Is.True);
            Assert.That(state.RemainingUses, Is.EqualTo(3));
            Assert.That(state.Acquire(item), Is.True);
            Assert.That(state.RemainingUses, Is.EqualTo(5));
            Assert.That(state.Acquire(item), Is.False);
            Assert.That(state.CompleteSuccessfulUse(item), Is.True);
            Assert.That(state.RemainingUses, Is.EqualTo(4));
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void BattleItemRunState_UnlimitedUsesRequireOwnershipAndKeepIt()
    {
        BattleItemSO item =
            ScriptableObject.CreateInstance<BattleItemSO>();
        try
        {
            ConfigureBattleItem(
                item,
                "test.battle.unlimited",
                BattleItemUsePolicy.UnlimitedUse,
                2,
                0,
                2f);
            BattleItemRunState state = new(item);

            Assert.That(state.CanUse(item), Is.False);
            Assert.That(state.Acquire(item), Is.True);
            Assert.That(state.Acquire(item), Is.False);
            Assert.That(state.CompleteSuccessfulUse(item), Is.True);
            Assert.That(state.IsOwned, Is.True);
            Assert.That(state.RemainingUses, Is.Zero);
            Assert.That(state.CanUse(item), Is.False);
            Assert.That(state.TickCooldown(2f), Is.True);
            Assert.That(state.CanUse(item), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void BattleItemCatalog_ContainsMigratedBattleItems()
    {
        ItemDefinitionCatalog.Invalidate();
        HashSet<string> expected = new()
        {
            CoreBattleItemIds.Focus,
            CoreBattleItemIds.Molotov,
            CoreBattleItemIds.PrecisionShot,
            CoreBattleItemIds.OverSupply,
            CoreBattleItemIds.Overheat,
        };

        foreach (BattleItemSO item in BattleItemCatalog.GetAll())
        {
            Assert.That(item, Is.Not.Null);
            Assert.That(item.Effects, Is.Not.Empty);
            Assert.That(item.HasCompatibleEffects, Is.True);
            expected.Remove(item.ItemId);
        }

        Assert.That(expected, Is.Empty);
    }

    [Test]
    public void NewAccount_ReceivesConfiguredInitialAmount()
    {
        CurrencyItemSO item =
            ScriptableObject.CreateInstance<CurrencyItemSO>();
        try
        {
            ConfigureItem(item, "test.initial", 999L, 125L);
            InventoryData inventory = new();
            MethodInfo initialize = typeof(InventoryData).GetMethod(
                "InitializeNewAccount",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(
                    System.Collections.Generic.IReadOnlyList<
                        ItemDefinitionSO>) },
                null);

            Assert.That(initialize, Is.Not.Null);
            initialize.Invoke(
                inventory,
                new object[]
                {
                    new ItemDefinitionSO[] { item },
                });

            Assert.That(
                inventory.GetAmount("test.initial"),
                Is.EqualTo(125L));
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void InventoryImport_InvalidJsonPreservesCurrentState()
    {
        InventoryData inventory = new();
        Assert.That(
            inventory.ImportJson(
                "{\"version\":1,\"entries\":[" +
                "{\"itemId\":\"test.saved\",\"amount\":17}]}"),
            Is.True);

        Assert.That(inventory.ImportJson("{}"), Is.False);
        Assert.That(
            inventory.ImportJson(
                "{\"version\":1,\"entries\":null}"),
            Is.False);
        Assert.That(inventory.GetAmount("test.saved"), Is.EqualTo(17L));
    }

    [Test]
    public void InventoryLoad_CorruptPrimaryPreservesRawValueAndBlocksSave()
    {
        const string corruptJson = "{broken";
        PlayerPrefs.SetString(InventoryKey, corruptJson);

        LogAssert.Expect(
            LogType.Error,
            "Inventory save data is corrupt or uses an unsupported version. " +
            "The original PlayerPrefs value was preserved and inventory " +
            "saving is blocked until local data is reset or recovered.");
        InventoryData inventory = new();
        Assert.That(inventory.Load(), Is.EqualTo(LocalDataLoadStatus.Corrupt));
        Assert.That(inventory.IsSaveBlocked, Is.True);

        LogAssert.Expect(
            LogType.Warning,
            "Inventory save was skipped because the primary save data " +
            "could not be loaded safely. Reset or recover local data before " +
            "saving again.");
        inventory.Save();

        Assert.That(
            PlayerPrefs.GetString(InventoryKey),
            Is.EqualTo(corruptJson));
    }

    [Test]
    public void InventoryLoad_UnsupportedVersionIsNotOverwritten()
    {
        const string futureJson = "{\"version\":99,\"entries\":[]}";
        PlayerPrefs.SetString(InventoryKey, futureJson);

        LogAssert.Expect(
            LogType.Error,
            "Inventory save data is corrupt or uses an unsupported version. " +
            "The original PlayerPrefs value was preserved and inventory " +
            "saving is blocked until local data is reset or recovered.");
        InventoryData inventory = new();

        Assert.That(
            inventory.Load(),
            Is.EqualTo(LocalDataLoadStatus.UnsupportedVersion));
        Assert.That(inventory.IsSaveBlocked, Is.True);
        Assert.That(
            PlayerPrefs.GetString(InventoryKey),
            Is.EqualTo(futureJson));
    }

    [Test]
    public void InventoryLoad_RecoversBackupAndPreservesRejectedPrimary()
    {
        const string corruptJson = "{broken";
        const string backupJson =
            "{\"version\":1,\"entries\":[" +
            "{\"itemId\":\"test.backup\",\"amount\":23}]}";
        PlayerPrefs.SetString(InventoryKey, corruptJson);
        PlayerPrefs.SetString(InventoryBackupKey, backupJson);

        LogAssert.Expect(
            LogType.Warning,
            "Inventory save data was restored from its last valid backup. " +
            "The rejected primary value was preserved under the corrupt key.");
        InventoryData inventory = new();

        Assert.That(
            inventory.Load(),
            Is.EqualTo(LocalDataLoadStatus.RecoveredFromBackup));
        Assert.That(inventory.IsSaveBlocked, Is.False);
        Assert.That(inventory.GetAmount("test.backup"), Is.EqualTo(23L));
        Assert.That(
            PlayerPrefs.GetString(InventoryCorruptKey),
            Is.EqualTo(corruptJson));

        inventory.Save();
        Assert.That(
            PlayerPrefs.GetString(InventoryKey),
            Does.Contain("test.backup"));
    }

    [Test]
    public void CharacterImport_LegacySaveMigratesToVersionedEnvelope()
    {
        CharacterCollectionData characters = new();

        Assert.That(
            characters.TryImportJson(
                "{\"version\":1,\"characters\":null}"),
            Is.False);
        Assert.That(
            characters.TryImportJson("{\"characters\":[]}"),
            Is.True);
        Assert.That(
            characters.LastLoadStatus,
            Is.EqualTo(LocalDataLoadStatus.Migrated));
        Assert.That(characters.ExportJson(), Does.Contain("\"version\":1"));
    }

    [Test]
    public void CharacterLoad_CorruptPrimaryPreservesRawValueAndBlocksSave()
    {
        const string corruptJson = "{}";
        PlayerPrefs.SetString(CharacterKey, corruptJson);

        LogAssert.Expect(
            LogType.Error,
            "Character progress save data is corrupt or uses an unsupported " +
            "version. The original PlayerPrefs value was preserved and " +
            "character saving is blocked until local data is reset or " +
            "recovered.");
        CharacterCollectionData characters = new();
        Assert.That(characters.Load(), Is.EqualTo(LocalDataLoadStatus.Corrupt));
        Assert.That(characters.IsSaveBlocked, Is.True);

        LogAssert.Expect(
            LogType.Warning,
            "Character progress save was skipped because the primary save " +
            "data could not be loaded safely. Reset or recover local data " +
            "before saving again.");
        characters.Save();

        Assert.That(PlayerPrefs.GetString(CharacterKey), Is.EqualTo(corruptJson));
    }

    [Test]
    public void CharacterLoad_UnsupportedVersionIsNotOverwritten()
    {
        const string futureJson =
            "{\"version\":99,\"characters\":[]}";
        PlayerPrefs.SetString(CharacterKey, futureJson);

        LogAssert.Expect(
            LogType.Error,
            "Character progress save data is corrupt or uses an unsupported " +
            "version. The original PlayerPrefs value was preserved and " +
            "character saving is blocked until local data is reset or " +
            "recovered.");
        CharacterCollectionData characters = new();

        Assert.That(
            characters.Load(),
            Is.EqualTo(LocalDataLoadStatus.UnsupportedVersion));
        Assert.That(characters.IsSaveBlocked, Is.True);
        Assert.That(
            PlayerPrefs.GetString(CharacterKey),
            Is.EqualTo(futureJson));
    }

    [Test]
    public void CharacterLoad_RecoversBackupAndPreservesRejectedPrimary()
    {
        const string corruptJson = "{broken";
        const string backupJson =
            "{\"version\":1,\"characters\":[" +
            "{\"characterId\":\"test.saved\",\"isOwned\":true}]}";
        PlayerPrefs.SetString(CharacterKey, corruptJson);
        PlayerPrefs.SetString(CharacterBackupKey, backupJson);

        LogAssert.Expect(
            LogType.Warning,
            "Character progress was restored from its last valid backup. " +
            "The rejected primary value was preserved under the corrupt key.");
        CharacterCollectionData characters = new();

        Assert.That(
            characters.Load(),
            Is.EqualTo(LocalDataLoadStatus.RecoveredFromBackup));
        Assert.That(characters.IsSaveBlocked, Is.False);
        Assert.That(characters.Characters, Has.Count.EqualTo(1));
        Assert.That(
            characters.Characters[0].CharacterId,
            Is.EqualTo("test.saved"));
        Assert.That(
            PlayerPrefs.GetString(CharacterCorruptKey),
            Is.EqualTo(corruptJson));

        characters.Save();
        Assert.That(
            PlayerPrefs.GetString(CharacterKey),
            Does.Contain("test.saved"));
    }

    private static void ConfigureItem(
        ItemDefinitionSO item,
        string itemId,
        long maximumStack,
        long initialAmount)
    {
        SerializedObject serialized = new(item);
        serialized.FindProperty("itemId").stringValue = itemId;
        serialized.FindProperty("maximumStack").longValue =
            maximumStack;
        serialized.FindProperty("initialAmount").longValue =
            initialAmount;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureBattleItem(
        BattleItemSO item,
        string itemId,
        BattleItemUsePolicy usePolicy,
        int limitedUses,
        int maximumRunUses,
        float cooldown)
    {
        ConfigureItem(item, itemId, 0L, 0L);
        SerializedObject serialized = new(item);
        serialized.FindProperty("usePolicy").enumValueIndex =
            (int)usePolicy;
        serialized.FindProperty("limitedUses").intValue = limitedUses;
        serialized.FindProperty("maximumRunUses").intValue =
            maximumRunUses;
        serialized.FindProperty("cooldown").floatValue = cooldown;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
