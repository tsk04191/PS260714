using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PS260714.Localization;
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
    public void LocalizationKeys_OverrideFallbackAndMissingKeysUseFallback()
    {
        GeneralItemSO item =
            ScriptableObject.CreateInstance<GeneralItemSO>();
        try
        {
            SerializedObject serialized = new(item);
            serialized.FindProperty("itemId").stringValue =
                "test.localized";
            serialized.FindProperty("nameLocalizationKey").stringValue =
                LocalizationKeys.ItemSoftCreditName;
            serialized.FindProperty(
                    "descriptionLocalizationKey").stringValue =
                LocalizationKeys.ItemSoftCreditDescription;
            serialized.FindProperty("koreanName").stringValue =
                "한글 대체 이름";
            serialized.FindProperty("englishName").stringValue =
                "English Fallback Name";
            serialized.FindProperty("koreanDescription").stringValue =
                "한글 대체 설명";
            serialized.FindProperty("englishDescription").stringValue =
                "English fallback description";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(item.GetDisplayName(true), Is.EqualTo(
                "인게임 크레딧"));
            Assert.That(item.GetDisplayName(false), Is.EqualTo(
                "In-Game Credit"));
            Assert.That(item.GetDescription(false), Is.EqualTo(
                "Basic currency earned and spent through gameplay."));

            serialized.Update();
            serialized.FindProperty("nameLocalizationKey").stringValue =
                "item.missing.name";
            serialized.FindProperty(
                    "descriptionLocalizationKey").stringValue =
                "item.missing.description";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(item.GetDisplayName(true), Is.EqualTo(
                "한글 대체 이름"));
            Assert.That(item.GetDisplayName(false), Is.EqualTo(
                "English Fallback Name"));
            Assert.That(item.GetDescription(true), Is.EqualTo(
                "한글 대체 설명"));
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void CoreItems_HaveResolvableLocalizationKeys()
    {
        string[] itemIds =
        {
            CoreItemIds.SoftCredit,
            CoreItemIds.PaidCredit,
            CoreItemIds.FreeCredit,
            CoreItemIds.StandardRecruitTicket,
            CoreItemIds.BasicUpgradeMaterial,
            CoreBattleItemIds.Focus,
            CoreBattleItemIds.Molotov,
            CoreBattleItemIds.PrecisionShot,
            CoreBattleItemIds.OverSupply,
            CoreBattleItemIds.Overheat,
        };

        ItemDefinitionCatalog.Invalidate();
        foreach (string itemId in itemIds)
        {
            ItemDefinitionSO item = ItemDefinitionCatalog.Get(itemId);
            Assert.That(item, Is.Not.Null, itemId);
            Assert.That(
                GeneratedLocalizationTables.ReferenceEntries.ContainsKey(
                    item.NameLocalizationKey),
                Is.True,
                $"{itemId} name key: {item.NameLocalizationKey}");
            Assert.That(
                GeneratedLocalizationTables.ReferenceEntries.ContainsKey(
                    item.DescriptionLocalizationKey),
                Is.True,
                $"{itemId} description key: " +
                item.DescriptionLocalizationKey);
        }
    }

    [Test]
    public void BattleItemLocalization_FormatsEffectArguments()
    {
        BattleItemSO item =
            ScriptableObject.CreateInstance<BattleItemSO>();
        string previousLocale = LocalizationService.CurrentLocale;
        try
        {
            ConfigureBattleItem(
                item,
                "test.localized.battle",
                BattleItemUsePolicy.SingleUse,
                2,
                0,
                0f);
            SerializedObject serialized = new(item);
            serialized.FindProperty(
                    "descriptionLocalizationKey").stringValue =
                LocalizationKeys.ItemFocusEffect;
            SerializedProperty effects =
                serialized.FindProperty("effects");
            effects.arraySize = 1;
            SerializedProperty effect = effects.GetArrayElementAtIndex(0);
            effect.FindPropertyRelative("effectType").enumValueIndex =
                (int)BattleItemEffectType.ForcePriorityTarget;
            effect.FindPropertyRelative("duration").floatValue = 5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(
                LocalizationService.SetLocale("en-US", false),
                Is.True);

            Assert.That(
                item.GetLocalizedDescription(),
                Is.EqualTo(
                    "Mark the selected enemy as the highest-priority " +
                    "target for 5 seconds."));
        }
        finally
        {
            LocalizationService.SetLocale(previousLocale, false);
            Object.DestroyImmediate(item);
        }
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
    public void BattleItemRunState_LimitedReusableRestoresPerBattle()
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
            Assert.That(state.Acquire(item), Is.False);
            Assert.That(state.CompleteSuccessfulUse(item), Is.True);
            Assert.That(state.RemainingUses, Is.EqualTo(2));
            Assert.That(state.IsOwned, Is.True);
            state.BeginBattle(item);
            Assert.That(state.RemainingUses, Is.EqualTo(3));
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
    public void BattleItemRunState_DisposableIsRemovedAndNeverRestored()
    {
        BattleItemSO item = ScriptableObject.CreateInstance<BattleItemSO>();
        try
        {
            ConfigureModernBattleItem(
                item,
                "test.battle.disposable",
                BattleItemLifecycle.Disposable,
                BattleItemChargeMode.Limited,
                1);
            BattleItemRunState state = new(item);

            Assert.That(state.Acquire(item), Is.True);
            Assert.That(state.CompleteSuccessfulUse(item), Is.True);
            Assert.That(state.IsOwned, Is.False);
            Assert.That(state.IsRemoved, Is.True);

            state.BeginBattle(item);
            Assert.That(state.IsOwned, Is.False);
            Assert.That(state.RemainingUses, Is.Zero);
            Assert.That(state.Acquire(item), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void BattleItemRunState_ReusableChargesRestoreAtBattleStart()
    {
        BattleItemSO item = ScriptableObject.CreateInstance<BattleItemSO>();
        try
        {
            ConfigureModernBattleItem(
                item,
                "test.battle.reusable",
                BattleItemLifecycle.Reusable,
                BattleItemChargeMode.Limited,
                2);
            BattleItemRunState state = new(item);

            Assert.That(state.Acquire(item), Is.True);
            Assert.That(state.CompleteSuccessfulUse(item), Is.True);
            Assert.That(state.CompleteSuccessfulUse(item), Is.True);
            Assert.That(state.IsOwned, Is.True);
            Assert.That(state.RemainingUses, Is.Zero);
            Assert.That(state.CanUse(item), Is.False);

            state.BeginBattle(item);
            Assert.That(state.IsOwned, Is.True);
            Assert.That(state.RemainingUses, Is.EqualTo(2));
            Assert.That(state.CanUse(item), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void BattleItemEffect_PermanentDungeonModifierKeepsExplicitScope()
    {
        BattleItemSO item = ScriptableObject.CreateInstance<BattleItemSO>();
        try
        {
            SerializedObject serialized = new(item);
            serialized.FindProperty("itemId").stringValue =
                "test.battle.permanent";
            SerializedProperty effects = serialized.FindProperty("effects");
            effects.arraySize = 1;
            SerializedProperty effect = effects.GetArrayElementAtIndex(0);
            effect.FindPropertyRelative("schemaVersion").intValue = 1;
            effect.FindPropertyRelative("effectType").enumValueIndex =
                (int)BattleItemEffectType.CharacterModifier;
            effect.FindPropertyRelative("scope").enumValueIndex =
                (int)BattleItemEffectScope.CurrentDungeon;
            effect.FindPropertyRelative("durationMode").enumValueIndex =
                (int)BattleItemEffectDurationMode.Permanent;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            BattleItemEffectDefinition definition = item.Effects[0];
            Assert.That(
                definition.Scope,
                Is.EqualTo(BattleItemEffectScope.CurrentDungeon));
            Assert.That(definition.IsPermanent, Is.True);
            Assert.That(
                float.IsPositiveInfinity(definition.RuntimeDuration),
                Is.True);
            Assert.That(
                definition.ModifierLifetimeScope,
                Is.EqualTo(CharacterModifierLifetimeScope.Dungeon));
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
        serialized.FindProperty("usageSchemaVersion").intValue = 1;
        serialized.FindProperty("lifecycle").enumValueIndex =
            usePolicy == BattleItemUsePolicy.SingleUse
                ? (int)BattleItemLifecycle.Disposable
                : (int)BattleItemLifecycle.Reusable;
        serialized.FindProperty("chargeMode").enumValueIndex =
            usePolicy == BattleItemUsePolicy.UnlimitedUse
                ? (int)BattleItemChargeMode.Unlimited
                : (int)BattleItemChargeMode.Limited;
        serialized.FindProperty("limitedUses").intValue =
            usePolicy == BattleItemUsePolicy.SingleUse
                ? 1
                : limitedUses;
        serialized.FindProperty("maximumRunUses").intValue =
            maximumRunUses;
        serialized.FindProperty("cooldown").floatValue = cooldown;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureModernBattleItem(
        BattleItemSO item,
        string itemId,
        BattleItemLifecycle lifecycle,
        BattleItemChargeMode chargeMode,
        int usesPerBattle)
    {
        ConfigureItem(item, itemId, 0L, 0L);
        SerializedObject serialized = new(item);
        serialized.FindProperty("usageSchemaVersion").intValue = 1;
        serialized.FindProperty("lifecycle").enumValueIndex =
            (int)lifecycle;
        serialized.FindProperty("chargeMode").enumValueIndex =
            (int)chargeMode;
        serialized.FindProperty("limitedUses").intValue = usesPerBattle;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
