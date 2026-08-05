using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PS260714.Localization;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

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
    public void PresentationSprites_ExposeIconAndCardIllustration()
    {
        GeneralItemSO item =
            ScriptableObject.CreateInstance<GeneralItemSO>();
        Texture2D texture = new(2, 2);
        Sprite icon = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            Vector2.one * 0.5f);
        Sprite illustration = Sprite.Create(
            texture,
            new Rect(1f, 0f, 1f, 2f),
            Vector2.one * 0.5f);
        try
        {
            SerializedObject serialized = new(item);
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("illustration").objectReferenceValue =
                illustration;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(item.Icon, Is.SameAs(icon));
            Assert.That(item.Illustration, Is.SameAs(illustration));
        }
        finally
        {
            Object.DestroyImmediate(icon);
            Object.DestroyImmediate(illustration);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void BattleItemCardPrefab_UsesFiveBySevenArtworkLayout()
    {
        BattleItemSO item =
            ScriptableObject.CreateInstance<BattleItemSO>();
        Texture2D texture = new(4, 4);
        Sprite icon = Sprite.Create(
            texture,
            new Rect(0f, 0f, 2f, 2f),
            Vector2.one * 0.5f);
        Sprite illustration = Sprite.Create(
            texture,
            new Rect(0f, 0f, 4f, 4f),
            Vector2.one * 0.5f);
        GameObject prefab = Resources.Load<GameObject>(
            "Presentation/BattleItemCard");
        Assert.That(prefab, Is.Not.Null);
        GameObject cardObject = Object.Instantiate(prefab);
        try
        {
            SerializedObject serialized = new(item);
            serialized.FindProperty("itemId").stringValue = "test.card";
            serialized.FindProperty("koreanName").stringValue = "테스트 카드";
            serialized.FindProperty("englishName").stringValue = "Test Card";
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("illustration").objectReferenceValue =
                illustration;
            serialized.FindProperty("energyCost").intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            RectTransform cardRect = cardObject.transform as RectTransform;
            DungeonItemCardView card =
                cardObject.GetComponent<DungeonItemCardView>();
            Assert.That(card, Is.Not.Null);
            Assert.That(card.HasRequiredPrefabReferences(), Is.True);
            Assert.That(card.Initialize(item, _ => { }), Is.True);

            Image artwork = cardObject.transform
                .Find("imgItemIllustration")
                .GetComponent<Image>();
            RectTransform iconFrame = cardObject.transform
                .Find("grpItemIcon") as RectTransform;
            Image iconImage = iconFrame.Find("imgItemIcon")
                .GetComponent<Image>();
            RectTransform nameBand = cardObject.transform
                .Find("grpItemNameBand") as RectTransform;

            Assert.That(
                cardRect.rect.height / cardRect.rect.width,
                Is.EqualTo(1.4f).Within(0.001f));
            Assert.That(artwork.sprite, Is.SameAs(illustration));
            Assert.That(iconImage.sprite, Is.SameAs(icon));
            Assert.That(
                iconFrame.rect.width,
                Is.EqualTo(iconFrame.rect.height));
            Assert.That(nameBand.anchorMax.y, Is.EqualTo(0f));
            Assert.That(
                cardObject.transform.Find("grpItemPopup")
                    .gameObject.activeSelf,
                Is.False);
        }
        finally
        {
            Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(icon);
            Object.DestroyImmediate(illustration);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void BattleItemCardPrefab_LeavesMissingSpritesBlank()
    {
        BattleItemSO item =
            ScriptableObject.CreateInstance<BattleItemSO>();
        GameObject prefab = Resources.Load<GameObject>(
            "Presentation/BattleItemCard");
        Assert.That(prefab, Is.Not.Null);
        GameObject cardObject = Object.Instantiate(prefab);
        try
        {
            SerializedObject serialized = new(item);
            serialized.FindProperty("itemId").stringValue =
                "test.blank-card";
            serialized.FindProperty("englishName").stringValue =
                "Blank Card";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DungeonItemCardView card =
                cardObject.GetComponent<DungeonItemCardView>();
            Assert.That(card.Initialize(item, _ => { }), Is.True);

            Image artwork = cardObject.transform
                .Find("imgItemIllustration")
                .GetComponent<Image>();
            Image iconImage = cardObject.transform
                .Find("grpItemIcon/imgItemIcon")
                .GetComponent<Image>();
            Assert.That(artwork.sprite, Is.Null);
            Assert.That(artwork.enabled, Is.False);
            Assert.That(iconImage.sprite, Is.Null);
            Assert.That(iconImage.enabled, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void BattleItemHand_ShowsOneCardPerRemainingUseWithoutCountText()
    {
        BattleItemSO item =
            ScriptableObject.CreateInstance<BattleItemSO>();
        GameObject prefab = Resources.Load<GameObject>(
            "Presentation/BattleItemCard");
        GameObject cardObject = Object.Instantiate(prefab);
        try
        {
            ConfigureBattleItem(
                item,
                "test.multiple-visible-cards",
                BattleItemUsePolicy.LimitedUse,
                3,
                0,
                0f);
            MethodInfo resolveVisibleCardCount =
                typeof(DungeonItemHandView).GetMethod(
                    "ResolveVisibleCardCount",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolveVisibleCardCount, Is.Not.Null);
            Assert.That(
                resolveVisibleCardCount.Invoke(
                    null,
                    new object[] { item, true, 3 }),
                Is.EqualTo(3));
            Assert.That(
                resolveVisibleCardCount.Invoke(
                    null,
                    new object[] { item, true, 0 }),
                Is.Zero,
                "A consumed finite-use card must disappear from the hand.");

            DungeonItemCardView card =
                cardObject.GetComponent<DungeonItemCardView>();
            Assert.That(card.Initialize(item, _ => { }), Is.True);
            card.Refresh(3, 10, true, 0f, false);
            TextMeshProUGUI stateText = cardObject.transform
                .Find("grpItemNameBand/txtItemState")
                .GetComponent<TextMeshProUGUI>();
            Assert.That(
                stateText.text,
                Is.Empty,
                "Duplicate quantity is represented by card instances, not xN text.");
        }
        finally
        {
            Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void ActiveSkillResourceHud_ShowsRadialCooldownAndHoverTooltip()
    {
        Texture2D texture = new(2, 2);
        Sprite resourceIcon = Sprite.Create(
            texture,
            new Rect(0f, 0f, 2f, 2f),
            Vector2.one * 0.5f);
        GameObject root = new(
            "TestActiveSkillResource",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(DungeonActiveSkillResourceView));
        GameObject amountObject = new(
            "txtAmount",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        amountObject.transform.SetParent(root.transform, false);
        try
        {
            DungeonActiveSkillResourceView view =
                root.GetComponent<DungeonActiveSkillResourceView>();
            TextMeshProUGUI amount =
                amountObject.GetComponent<TextMeshProUGUI>();
            view.Configure(resourceIcon, amount);
            view.Refresh(2, 5, 2f, 8f, "Recharge 2.0s");

            Image cooldownOverlay = root.transform
                .Find("grpResourceIcon/imgResourceCooldownOverlay")
                .GetComponent<Image>();
            GameObject tooltip = root.transform
                .Find("grpResourceTooltip")
                .gameObject;
            TextMeshProUGUI tooltipText = tooltip.transform
                .Find("txtResourceTooltip")
                .GetComponent<TextMeshProUGUI>();

            Assert.That(amount.text, Is.EqualTo("2/5"));
            Assert.That(cooldownOverlay.sprite, Is.SameAs(resourceIcon));
            Assert.That(cooldownOverlay.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(
                cooldownOverlay.fillMethod,
                Is.EqualTo(Image.FillMethod.Radial360));
            Assert.That(
                cooldownOverlay.fillOrigin,
                Is.EqualTo((int)Image.Origin360.Top));
            Assert.That(cooldownOverlay.fillClockwise, Is.True);
            Assert.That(cooldownOverlay.fillAmount, Is.EqualTo(0.25f));
            Assert.That(cooldownOverlay.enabled, Is.True);
            Assert.That(
                root.transform.Find("grpResourceRechargeGauge"),
                Is.Null);
            Assert.That(tooltip.activeSelf, Is.False);
            view.OnPointerEnter(null);
            Assert.That(tooltip.activeSelf, Is.True);
            Assert.That(tooltipText.text, Is.EqualTo("Recharge 2.0s"));
            view.OnPointerExit(null);
            Assert.That(tooltip.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(resourceIcon);
            Object.DestroyImmediate(texture);
        }
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
    public void BattleItemAbilityEffects_UseCharacterSkillEffectModel()
    {
        BattleItemSO item = ScriptableObject.CreateInstance<BattleItemSO>();
        try
        {
            ConfigureBattleItem(
                item,
                "test.battle.unified_ability",
                BattleItemUsePolicy.SingleUse,
                1,
                0,
                0f);
            SerializedObject serialized = new(item);
            SerializedProperty abilities =
                serialized.FindProperty("abilityEffects");
            abilities.arraySize = 1;
            SerializedProperty effect =
                abilities.GetArrayElementAtIndex(0);
            effect.FindPropertyRelative("effectId").stringValue =
                "item_damage";
            effect.FindPropertyRelative("type").enumValueIndex =
                (int)CharacterEffectType.Damage;
            effect.FindPropertyRelative("targetMode").enumValueIndex =
                (int)CharacterEffectTargetMode.InheritAction;
            effect.FindPropertyRelative("damageType").enumValueIndex =
                (int)CharacterAttackDamageType.Fixed;
            effect.FindPropertyRelative("damageAmountMode").enumValueIndex =
                (int)CharacterDamageAmountMode.Fixed;
            effect.FindPropertyRelative("damageAmount").floatValue = 5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(item.UsesUnifiedAbilityEffects, Is.True);
            Assert.That(item.AbilityEffects, Has.Count.EqualTo(1));
            Assert.That(
                item.AbilityEffects[0].Type,
                Is.EqualTo(CharacterEffectType.Damage));
            Assert.That(item.HasCompatibleEffects, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void BattleItemStatusDuration_CanBeConfiguredUntilBattleEnd()
    {
        BattleItemSO item = ScriptableObject.CreateInstance<BattleItemSO>();
        try
        {
            SerializedObject serialized = new(item);
            serialized.FindProperty("appliedStatusDurationMode")
                .enumValueIndex =
                (int)BattleItemStatusDurationMode.UntilBattleEnd;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                item.AppliedStatusDurationMode,
                Is.EqualTo(BattleItemStatusDurationMode.UntilBattleEnd));
            Assert.That(item.StatusEffectsLastUntilBattleEnd, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void EnemyBattleItem_FreshSkillSelectionIsRejectedWithoutSource()
    {
        BattleItemSO item = ScriptableObject.CreateInstance<BattleItemSO>();
        try
        {
            SerializedObject serialized = new(item);
            serialized.FindProperty("targetType").enumValueIndex =
                (int)BattleItemTargetType.Enemy;
            SerializedProperty abilities =
                serialized.FindProperty("abilityEffects");
            abilities.arraySize = 1;
            SerializedProperty effect =
                abilities.GetArrayElementAtIndex(0);
            effect.FindPropertyRelative("type").enumValueIndex =
                (int)CharacterEffectType.Damage;
            effect.FindPropertyRelative("targetMode").enumValueIndex =
                (int)CharacterEffectTargetMode.FreshSelection;
            effect.FindPropertyRelative("damageType").enumValueIndex =
                (int)CharacterAttackDamageType.Fixed;
            effect.FindPropertyRelative("damageAmountMode").enumValueIndex =
                (int)CharacterDamageAmountMode.Fixed;
            effect.FindPropertyRelative("damageAmount").floatValue = 1f;
            SerializedProperty selector =
                effect.FindPropertyRelative("targetSelector");
            selector.FindPropertyRelative("subject").enumValueIndex =
                (int)CharacterAttackSubject.Random;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(item.UsesUnifiedAbilityEffects, Is.True);
            Assert.That(item.HasCompatibleEffects, Is.False);
        }
        finally
        {
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
    public void ItemAssets_AreNotAutomaticallyCreatedOnEditorLoad()
    {
        Assert.That(
            System.Attribute.IsDefined(
                typeof(ItemAssetBootstrap),
                typeof(InitializeOnLoadAttribute)),
            Is.False);
        Assert.That(
            typeof(ItemAssetBootstrap).GetMethod(
                "CreateCoreItemAssets",
                BindingFlags.Public | BindingFlags.Static),
            Is.Null);
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
