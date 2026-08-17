using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PS260714.Localization;
using UnityEditor;
using UnityEngine;
using static TestReflection;

public sealed class EnemyDefinitionValidationTests
{
    private readonly List<UnityEngine.Object> _createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        foreach (UnityEngine.Object createdObject in _createdObjects)
        {
            if (createdObject != null)
                UnityEngine.Object.DestroyImmediate(createdObject);
        }
        _createdObjects.Clear();
    }

    [Test]
    public void AllEnemyAssets_HaveNoDefinitionErrors()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:EnemySO",
            new[] { "Assets/06_Runtime/Resources/Enemies" });
        List<EnemySO> definitions = new();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemySO definition =
                AssetDatabase.LoadAssetAtPath<EnemySO>(path);
            if (definition != null)
                definitions.Add(definition);
        }

        Assert.That(definitions, Has.Count.EqualTo(8));
        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.ValidateAll(definitions);
        foreach (EnemySO definition in definitions)
        {
            Assert.That(
                AbilityDefinitionValidator.TryValidateProvider(
                    definition,
                    out string providerError),
                Is.True,
                $"{definition.name}: {providerError}");
        }

        Assert.That(
            result.ErrorCount,
            Is.Zero,
            BuildFailureMessage(result));
    }

    [Test]
    public void NewEnemy_RequiresExplicitPersistentGuidGeneration()
    {
        EnemySO definition = CreateEnemy();
        SerializedObject serialized = new(definition);
        serialized.FindProperty("enemyId").stringValue = string.Empty;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        InvokeOnValidate(definition);

        Assert.That(definition.EnemyId, Is.Empty);

        definition.RegenerateEnemyId();
        Assert.That(Guid.TryParseExact(definition.EnemyId, "N", out _),
            Is.True);
        string generatedId = definition.EnemyId;

        InvokeOnValidate(definition);

        Assert.That(definition.EnemyId, Is.EqualTo(generatedId));
    }

    [Test]
    public void StatusOnValidate_PreservesAuthoredIdAndRemovalFlags()
    {
        StatusEffectSO status =
            ScriptableObject.CreateInstance<StatusEffectSO>();
        _createdObjects.Add(status);
        SerializedObject serialized = new(status);
        serialized.FindProperty("statusId").stringValue = string.Empty;
        serialized.FindProperty("removable").boolValue = false;
        serialized.FindProperty("includedInRandomRemoval").boolValue = true;
        serialized.FindProperty("includedInAllRemoval").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        MethodInfo onValidate = typeof(StatusEffectSO).GetMethod(
            "OnValidate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(onValidate, Is.Not.Null);
        onValidate.Invoke(status, null);

        Assert.That(status.StatusId, Is.Empty);
        Assert.That(status.Removable, Is.False);
        Assert.That(status.ConfiguredIncludedInRandomRemoval, Is.True);
        Assert.That(status.ConfiguredIncludedInAllRemoval, Is.True);

        StatusEffectDefinitionValidationResult result =
            StatusEffectDefinitionValidator.Validate(status);
        Assert.That(HasStatusCode(result, "status.id_missing"), Is.True);
        Assert.That(
            HasStatusCode(result, "status.non_removable_in_removal_pool"),
            Is.True);
    }

    [Test]
    public void MissingBoardSprite_IsReportedAsWarning()
    {
        EnemySO definition = CreateEnemy("missing_board_sprite");

        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.Validate(definition);

        Assert.That(result.ErrorCount, Is.Zero);
        Assert.That(
            HasCode(result, "enemy.board_sprite_missing"),
            Is.True);
    }

    [Test]
    public void NonSquareBoardSprite_IsAccepted()
    {
        EnemySO definition = CreateEnemy("non_square_board_sprite");
        Texture2D texture = new(128, 64);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 128f, 64f),
            new Vector2(0.5f, 0.5f));
        _createdObjects.Add(sprite);
        _createdObjects.Add(texture);

        SerializedObject serialized = new(definition);
        serialized.FindProperty("boardSprite").objectReferenceValue = sprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.Validate(definition);

        Assert.That(
            HasCode(result, "enemy.board_sprite_missing"),
            Is.False);
        Assert.That(result.ErrorCount, Is.Zero);
    }

    [Test]
    public void SquareBoardSprite_IsAccepted()
    {
        EnemySO definition = CreateEnemy("square_board_sprite");
        Texture2D texture = new(128, 128);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 128f, 128f),
            new Vector2(0.5f, 0.5f));
        _createdObjects.Add(sprite);
        _createdObjects.Add(texture);

        SerializedObject serialized = new(definition);
        serialized.FindProperty("boardSprite").objectReferenceValue = sprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.Validate(definition);

        Assert.That(
            HasCode(result, "enemy.board_sprite_missing"),
            Is.False);
        Assert.That(result.ErrorCount, Is.Zero);
    }

    [Test]
    public void DuplicateEnemyId_IsRejected()
    {
        EnemySO first = CreateEnemy("duplicate_enemy");
        EnemySO second = CreateEnemy("duplicate_enemy");

        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.ValidateAll(new[] { first, second });

        Assert.That(
            HasCode(result, "enemy.id_duplicate"),
            Is.True,
            BuildFailureMessage(result));
    }

    [Test]
    public void InvalidAuthoredStatsAndFootprint_AreRejectedBeforeClamping()
    {
        EnemySO definition = CreateEnemy("invalid_authored_enemy");
        SetPrivateField(definition, "healthScale", -2f);
        SetPrivateField(definition, "initialArmor", -1);
        SetPrivateField(definition, "unlockDifficulty", -2);
        SetPrivateField(
            definition,
            "footprintWidth",
            EnemySO.MaximumFootprintSize + 1);
        SetPrivateField(definition, "footprintHeight", 2);
        SetPrivateField(
            definition,
            "stackingPolicy",
            EnemyStackingPolicy.Stackable);

        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.Validate(definition);

        Assert.That(HasCode(result, "enemy.health_scale_invalid"), Is.True);
        Assert.That(HasCode(result, "enemy.initial_defense_invalid"), Is.True);
        Assert.That(HasCode(result, "enemy.unlock_difficulty_invalid"), Is.True);
        Assert.That(HasCode(result, "enemy.footprint_invalid"), Is.True);
        Assert.That(
            HasCode(result, "enemy.large_footprint_must_be_exclusive"),
            Is.True,
            BuildFailureMessage(result));
    }

    [Test]
    public void ValidCooldownAbility_PassesDefinitionValidation()
    {
        EnemySO definition = CreateEnemy("valid_cooldown_enemy");
        SerializedObject serialized = new(definition);
        ConfigureAbility(
            serialized.FindProperty("abilities"),
            0,
            "periodic_heal",
            EnemyAbilityTrigger.OnCooldown,
            EnemyAbilityOperationType.ExecuteEffects,
            4f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.Validate(definition);

        Assert.That(
            result.ErrorCount,
            Is.Zero,
            BuildFailureMessage(result));
    }

    [Test]
    public void StatusScopeCondition_DoesNotRequireExplicitStatusAssets()
    {
        EnemySO definition = CreateEnemy("status_scope_enemy");
        SerializedObject serialized = new(definition);
        SerializedProperty abilities =
            serialized.FindProperty("abilities");
        ConfigureAbility(
            abilities,
            0,
            "status_scope_heal",
            EnemyAbilityTrigger.OnCooldown,
            EnemyAbilityOperationType.ExecuteEffects,
            4f);
        SerializedProperty conditions = abilities
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("conditions");
        conditions.arraySize = 1;
        SerializedProperty condition =
            conditions.GetArrayElementAtIndex(0);
        condition.FindPropertyRelative("type").enumValueIndex =
            (int)EnemyAbilityConditionType.SourceHasStatus;
        condition.FindPropertyRelative(
            "statusSelectionScope").enumValueIndex =
            (int)CharacterStatusSelectionScope.AllBuffs;
        condition.FindPropertyRelative("statusMatchMode").enumValueIndex =
            (int)CharacterStatusConditionMatchMode.AtLeastCount;
        condition.FindPropertyRelative("statusMatchCount").intValue = 2;
        condition.FindPropertyRelative("expected").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.Validate(definition);

        Assert.That(
            HasCode(result, "ability.condition_status_missing"),
            Is.False,
            BuildFailureMessage(result));
        Assert.That(
            HasCode(
                result,
                "ability.condition_status_match_count_exceeds_selection"),
            Is.False,
            BuildFailureMessage(result));
    }

    [Test]
    public void DuplicateAbilityId_IsRejected()
    {
        EnemySO definition = CreateEnemy("duplicate_ability_enemy");
        SerializedObject serialized = new(definition);
        SerializedProperty abilities =
            serialized.FindProperty("abilities");
        ConfigureAbility(
            abilities,
            0,
            "spawn_armor",
            EnemyAbilityTrigger.OnSpawn,
            EnemyAbilityOperationType.GrantArmor);
        ConfigureAbility(
            abilities,
            1,
            "spawn_armor",
            EnemyAbilityTrigger.OnSpawn,
            EnemyAbilityOperationType.GrantArmor);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.Validate(definition);

        Assert.That(
            HasCode(result, "ability.id_duplicate"),
            Is.True,
            BuildFailureMessage(result));
    }

    [Test]
    public void CooldownTrigger_RequiresPositiveCooldown()
    {
        EnemySO definition = CreateEnemy("invalid_cooldown_enemy");
        SerializedObject serialized = new(definition);
        ConfigureAbility(
            serialized.FindProperty("abilities"),
            0,
            "periodic_heal",
            EnemyAbilityTrigger.OnCooldown,
            EnemyAbilityOperationType.ExecuteEffects,
            0f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.Validate(definition);

        Assert.That(
            HasCode(result, "ability.cooldown_required"),
            Is.True,
            BuildFailureMessage(result));
    }

    [Test]
    public void SpecializedOperation_WithWrongTriggerIsRejected()
    {
        EnemySO definition = CreateEnemy("invalid_operation_enemy");
        SerializedObject serialized = new(definition);
        ConfigureAbility(
            serialized.FindProperty("abilities"),
            0,
            "invalid_redirect",
            EnemyAbilityTrigger.OnCooldown,
            EnemyAbilityOperationType.RedirectDamage,
            3f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.Validate(definition);

        Assert.That(
            HasCode(result, "ability.operation_trigger_mismatch"),
            Is.True,
            BuildFailureMessage(result));
    }

    [Test]
    public void IncompleteTargetDefinition_IsRejected()
    {
        EnemySO definition = CreateEnemy("invalid_target_enemy");
        SerializedObject serialized = new(definition);
        SerializedProperty abilities =
            serialized.FindProperty("abilities");
        ConfigureAbility(
            abilities,
            0,
            "spawn_armor",
            EnemyAbilityTrigger.OnSpawn,
            EnemyAbilityOperationType.GrantArmor);
        SerializedProperty target = abilities
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("target");
        target.FindPropertyRelative("faction").enumValueIndex =
            (int)EnemyAbilityTargetFaction.EnemyAllies;
        target.FindPropertyRelative("subject").enumValueIndex =
            (int)EnemyAbilityTargetSubject.None;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.Validate(definition);

        Assert.That(
            HasCode(result, "ability.target_incomplete"),
            Is.True,
            BuildFailureMessage(result));
    }

    [Test]
    public void EnemyEditor_AddAbility_CreatesValidationSafeCooldownDefault()
    {
        EnemySO definition = CreateEnemy("editor_default_enemy");
        SerializedObject serialized = new(definition);
        SerializedProperty abilities =
            serialized.FindProperty("abilities");

        InvokeEnemyEditorMethod("AddAbility", abilities);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(definition.Abilities, Has.Count.EqualTo(1));
        EnemyAbilityDefinition ability = definition.Abilities[0];
        Assert.That(ability.AbilityId, Is.EqualTo("ability_1"));
        Assert.That(ability.Trigger,
            Is.EqualTo(EnemyAbilityTrigger.OnCooldown));
        Assert.That(ability.Cooldown, Is.EqualTo(1f));
        Assert.That(ability.Target.Faction,
            Is.EqualTo(EnemyAbilityTargetFaction.Self));
        Assert.That(ability.Target.Subject,
            Is.EqualTo(EnemyAbilityTargetSubject.Self));
        Assert.That(ability.Operations, Has.Count.EqualTo(1));
        Assert.That(
            ability.Operations[0].Type,
            Is.EqualTo(EnemyAbilityOperationType.ExecuteEffects));
        Assert.That(
            ability.Operations[0].Effects,
            Has.Count.EqualTo(1));
        Assert.That(
            ability.Operations[0].Effects[0].Type,
            Is.EqualTo(CharacterEffectType.Damage));

        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.Validate(definition);
        Assert.That(
            result.ErrorCount,
            Is.Zero,
            BuildFailureMessage(result));
    }

    [Test]
    public void EnemyEditor_AddCondition_CreatesUsableHealthThreshold()
    {
        EnemySO definition = CreateEnemy("editor_condition_enemy");
        SerializedObject serialized = new(definition);
        SerializedProperty abilities =
            serialized.FindProperty("abilities");
        InvokeEnemyEditorMethod("AddAbility", abilities);
        SerializedProperty conditions = abilities
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("conditions");

        InvokeEnemyEditorMethod("AddCondition", conditions);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnemyAbilityConditionDefinition condition =
            definition.Abilities[0].Conditions[0];
        Assert.That(
            condition.Type,
            Is.EqualTo(
                EnemyAbilityConditionType.SourceHealthPercentage));
        Assert.That(
            condition.Comparison,
            Is.EqualTo(
                CharacterNumericComparison.LessThanOrEqual));
        Assert.That(condition.Threshold, Is.EqualTo(50f));
        Assert.That(condition.Expected, Is.True);
    }

    [TestCase(EnemyAbilityTrigger.OnSpawn)]
    [TestCase(EnemyAbilityTrigger.BeforeSelfDamage)]
    [TestCase(EnemyAbilityTrigger.BeforeAllyDamage)]
    [TestCase(EnemyAbilityTrigger.OnSpawnQueueEvaluation)]
    [TestCase(EnemyAbilityTrigger.OnTargetPriorityEvaluation)]
    public void EnemyEditor_AddOperation_AlwaysUsesSharedEffects(
        EnemyAbilityTrigger trigger)
    {
        EnemySO definition = CreateEnemy(
            $"editor_operation_{(int)trigger}");
        SerializedObject serialized = new(definition);
        SerializedProperty abilities =
            serialized.FindProperty("abilities");
        InvokeEnemyEditorMethod("AddAbility", abilities);
        SerializedProperty operations = abilities
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("operations");
        operations.ClearArray();

        InvokeEnemyEditorMethod("AddOperation", operations, trigger);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(
            definition.Abilities[0].Operations,
            Has.Count.EqualTo(1));
        Assert.That(
            definition.Abilities[0].Operations[0].Type,
            Is.EqualTo(EnemyAbilityOperationType.ExecuteEffects));
    }

    [Test]
    public void EnemySO_HasNoRetiredSerializedFields()
    {
        EnemySO definition = CreateEnemy("modular_storage_enemy");
        SerializedObject serialized = new(definition);
        string[] retiredFields =
        {
            "schemaVersion",
            "targetPriorityExcluded",
            "initialArmorMultiplier",
            "guardedHitCount",
            "companionSpawnCount",
            "abilityCooldown",
            "abilityPower",
            "disableDuration",
            "disableStatusEffect",
        };

        foreach (string field in retiredFields)
            Assert.That(serialized.FindProperty(field), Is.Null, field);
    }

    [Test]
    public void HeavyIncompatibleAbilityWasDeleted()
    {
        EnemySO definition = CreateAssetClone("Heavy");
        Assert.That(definition.Abilities, Is.Empty);
    }

    [Test]
    public void MechanicAbilityAsset_RequiresPositiveDamageTarget()
    {
        EnemySO definition = CreateAssetClone("Mechanic");

        EnemyAbilityDefinition ability = FindAbility(
            definition,
            EnemyAbilityIds.DisableHighestDamage);
        Assert.That(ability.Target.Faction,
            Is.EqualTo(
                EnemyAbilityTargetFaction.PlayerCharacters));
        Assert.That(ability.Target.Subject,
            Is.EqualTo(EnemyAbilityTargetSubject.HighestValue));
        Assert.That(ability.Target.Metric,
            Is.EqualTo(
                EnemyAbilityTargetMetric.TotalDamageDealt));
        Assert.That(ability.Conditions, Has.Count.EqualTo(1));
        Assert.That(
            ability.Conditions[0].Type,
            Is.EqualTo(
                EnemyAbilityConditionType.TargetTotalDamageDealt));
        Assert.That(
            ability.Conditions[0].Comparison,
            Is.EqualTo(CharacterNumericComparison.GreaterThan));
        Assert.That(ability.Conditions[0].Threshold, Is.Zero);
        CharacterEffectDefinition effect =
            ability.Operations[0].Effects[0];
        Assert.That(
            effect.Type,
            Is.EqualTo(CharacterEffectType.ApplyStatus));
        Assert.That(
            effect.StatusEffect.StatusId,
            Is.EqualTo(StatusEffectIds.Stun));
        Assert.That(effect.StatusDuration, Is.EqualTo(5f));
    }

    [Test]
    public void ShieldBearerIncompatibleAbilitiesWereDeleted()
    {
        EnemySO definition = CreateAssetClone("ShieldBearer");
        Assert.That(definition.Abilities, Is.Empty);
    }

    [TestCase(EEnemyType.Basic, 0)]
    [TestCase(EEnemyType.Assault, 0)]
    [TestCase(EEnemyType.Heavy, 0)]
    [TestCase(EEnemyType.Medic, 1)]
    [TestCase(EEnemyType.Mechanic, 1)]
    [TestCase(EEnemyType.Pointman, 0)]
    [TestCase(EEnemyType.ShieldBearer, 0)]
    [TestCase(EEnemyType.Infiltrator, 0)]
    public void RuntimeDefault_UsesValidAbilityPreset(
        EEnemyType enemyType,
        int expectedAbilityCount)
    {
        EnemySO definition = CreateRuntimeDefault(enemyType);

        Assert.That(
            definition.Abilities,
            Has.Count.EqualTo(expectedAbilityCount));
        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.Validate(definition);
        Assert.That(
            result.ErrorCount,
            Is.Zero,
            BuildFailureMessage(result));
    }

    [Test]
    public void EnemyCodexAbilityDescription_UsesModularAbilityValues()
    {
        EnemySO medic = CreateAssetClone("Medic");
        SerializedObject medicSerialized = new(medic);
        SerializedProperty medicAbility = medicSerialized
            .FindProperty("abilities")
            .GetArrayElementAtIndex(0);
        medicAbility.FindPropertyRelative("cooldown").floatValue = 7f;
        medicAbility.FindPropertyRelative("operations")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("damageAmount").floatValue = 4f;
        medicSerialized.ApplyModifiedPropertiesWithoutUndo();
        EnemyAbilityDefinition adjacentHeal =
            FindAbility(medic, EnemyAbilityIds.AdjacentHeal);
        CharacterEffectDefinition heal =
            adjacentHeal.Operations[0].Effects[0];
        Assert.That(
            EnemyLocalization.GetAbility(medic),
            Is.EqualTo(LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbilityMedic,
                LocalizationService.Arg(
                    "cooldown",
                    adjacentHeal.Cooldown),
                LocalizationService.Arg(
                    "power",
                    heal.DamageAmount))));

        EnemySO mechanic = CreateAssetClone("Mechanic");
        SerializedObject mechanicSerialized = new(mechanic);
        SerializedProperty mechanicAbility = mechanicSerialized
            .FindProperty("abilities")
            .GetArrayElementAtIndex(0);
        mechanicAbility.FindPropertyRelative("cooldown").floatValue = 8f;
        mechanicAbility.FindPropertyRelative("operations")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("effects")
            .GetArrayElementAtIndex(0)
            .FindPropertyRelative("statusDuration").floatValue = 3f;
        mechanicSerialized.ApplyModifiedPropertiesWithoutUndo();
        EnemyAbilityDefinition disable = FindAbility(
            mechanic,
            EnemyAbilityIds.DisableHighestDamage);
        CharacterEffectDefinition stun =
            disable.Operations[0].Effects[0];
        Assert.That(
            EnemyLocalization.GetAbility(mechanic),
            Is.EqualTo(LocalizationService.Get(
                LocalizationKeys.CodexEnemyAbilityMechanic,
                LocalizationService.Arg(
                    "cooldown",
                    disable.Cooldown),
                LocalizationService.Arg(
                    "duration",
                    stun.StatusDuration))));
    }

    [Test]
    public void DeletedPriorityAbility_IsNotReportedByCodex()
    {
        EnemySO infiltrator = CreateAssetClone("Infiltrator");
        EnemySO basic = CreateAssetClone("Basic");

        Assert.That(
            EnemyLocalization.HasTargetPriorityExclusion(infiltrator),
            Is.False);
        Assert.That(
            EnemyLocalization.HasTargetPriorityExclusion(basic),
            Is.False);
    }

    [Test]
    public void EnemyCodexAbilityDescription_UsesAuthoredFallbackForCustomAbility()
    {
        EnemySO definition = CreateEnemy("custom_codex_enemy");
        SerializedObject serialized = new(definition);
        SerializedProperty abilities =
            serialized.FindProperty("abilities");
        InvokeEnemyEditorMethod("AddAbility", abilities);
        SerializedProperty ability = abilities.GetArrayElementAtIndex(0);
        ability.FindPropertyRelative("abilityId").stringValue =
            "custom_storm_call";
        ability.FindPropertyRelative("fallbackName").stringValue =
            "Storm Call";
        ability.FindPropertyRelative("fallbackDescription").stringValue =
            "Uses authored modular data.";
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(
            EnemyLocalization.GetAbility(definition),
            Is.EqualTo(
                "Storm Call\nUses authored modular data."));
    }

    private EnemySO CreateEnemy(string enemyId = null)
    {
        EnemySO definition = ScriptableObject.CreateInstance<EnemySO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(definition);
        SerializedObject serialized = new(definition);
        serialized.FindProperty("enemyId").stringValue =
            enemyId ?? Guid.NewGuid().ToString("N");
        serialized.FindProperty("displayName").stringValue = "TEST ENEMY";
        serialized.FindProperty("combatStatSchemaVersion").intValue =
            EnemySO.CurrentCombatStatSchemaVersion;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private EnemySO CreateAssetClone(string assetName)
    {
        EnemySO source = AssetDatabase.LoadAssetAtPath<EnemySO>(
            $"Assets/06_Runtime/Resources/Enemies/{assetName}.asset");
        Assert.That(
            source,
            Is.Not.Null,
            $"Missing EnemySO test asset: {assetName}");
        EnemySO definition =
            UnityEngine.Object.Instantiate(source);
        definition.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(definition);
        return definition;
    }

    private EnemySO CreateRuntimeDefault(EEnemyType enemyType)
    {
        MethodInfo method = typeof(EnemySO).GetMethod(
            "CreateRuntimeDefault",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        EnemySO definition = (EnemySO)method.Invoke(
            null,
            new object[] { enemyType, 20 });
        Assert.That(definition, Is.Not.Null);
        _createdObjects.Add(definition);
        return definition;
    }

    private static EnemyAbilityDefinition FindAbility(
        EnemySO definition,
        string abilityId)
    {
        foreach (EnemyAbilityDefinition ability in
                 definition.Abilities)
        {
            if (ability != null &&
                string.Equals(
                    ability.AbilityId,
                    abilityId,
                    StringComparison.Ordinal))
            {
                return ability;
            }
        }

        Assert.Fail(
            $"Missing migrated ability '{abilityId}'.");
        return null;
    }

    private static void InvokeEnemyEditorMethod(
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = typeof(EnemyEditorWindow).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(
            method,
            Is.Not.Null,
            $"EnemyEditorWindow.{methodName} was not found.");
        method.Invoke(null, arguments);
    }

    private static void ConfigureAbility(
        SerializedProperty abilities,
        int index,
        string abilityId,
        EnemyAbilityTrigger trigger,
        EnemyAbilityOperationType operationType,
        float cooldown = 0f)
    {
        abilities.arraySize = Mathf.Max(abilities.arraySize, index + 1);
        SerializedProperty ability = abilities.GetArrayElementAtIndex(index);
        ability.FindPropertyRelative("abilityId").stringValue = abilityId;
        ability.FindPropertyRelative("fallbackName").stringValue =
            abilityId;
        ability.FindPropertyRelative("trigger").enumValueIndex =
            (int)trigger;
        ability.FindPropertyRelative("cooldown").floatValue = cooldown;

        SerializedProperty operations =
            ability.FindPropertyRelative("operations");
        operations.arraySize = 1;
        SerializedProperty operation =
            operations.GetArrayElementAtIndex(0);
        operation.FindPropertyRelative("type").enumValueIndex =
            (int)operationType;

        if (operationType == EnemyAbilityOperationType.ExecuteEffects)
        {
            SerializedProperty effects =
                operation.FindPropertyRelative("effects");
            effects.arraySize = 1;
            SerializedProperty effect = effects.GetArrayElementAtIndex(0);
            effect.FindPropertyRelative("type").enumValueIndex =
                (int)CharacterEffectType.Heal;
            effect.FindPropertyRelative("damageAmountMode").enumValueIndex =
                (int)CharacterDamageAmountMode.Fixed;
            effect.FindPropertyRelative("damageAmount").floatValue = 1f;
        }
    }

    private static void InvokeOnValidate(EnemySO definition)
    {
        MethodInfo method = typeof(EnemySO).GetMethod(
            "OnValidate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(definition, null);
    }

    private static bool HasCode(
        EnemyDefinitionValidationResult result,
        string code)
    {
        foreach (EnemyDefinitionDiagnostic diagnostic in
                 result.Diagnostics)
        {
            if (string.Equals(
                    diagnostic.Code,
                    code,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasStatusCode(
        StatusEffectDefinitionValidationResult result,
        string code)
    {
        foreach (StatusEffectDefinitionDiagnostic diagnostic in
                 result.Diagnostics)
        {
            if (string.Equals(
                    diagnostic.Code,
                    code,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildFailureMessage(
        EnemyDefinitionValidationResult result)
    {
        if (result == null || result.Diagnostics.Count == 0)
            return "No diagnostics.";

        return string.Join(
            Environment.NewLine,
            result.Diagnostics);
    }
}

public sealed class AbilityDefinitionContractTests
{
    [Test]
    public void CharacterSkill_UsesCurrentCommonContract()
    {
        CharacterSkillDefinition skill = new();
        SetField(skill, "actionId", "skill.contract_test");
        SetField(
            skill,
            "sections",
            new List<CharacterSkillSectionType>
            {
                CharacterSkillSectionType.Subject,
                CharacterSkillSectionType.Ability,
            });
        SetField(skill, "subject", CharacterAttackSubject.Manual);
        SetField(
            skill,
            "effects",
            new List<CharacterEffectDefinition>
            {
                new(),
            });

        CharacterAreaDefinition area = new();
        SetField(
            area,
            "shapeType",
            CharacterAreaShapeType.CircleSector);
        SetField(
            area,
            "originMode",
            CharacterAreaOriginMode.DesignatedPoint);
        SetField(skill, "areaDefinition", area);
        skill.Validate();

        IBattleAbilityDefinition ability = skill;
        Assert.That(ability.AbilityId, Is.EqualTo("skill.contract_test"));
        Assert.That(
            ability.ExecutionDomain,
            Is.EqualTo(AbilityExecutionDomain.Battle));
        Assert.That(ability.AbilitySchemaVersion, Is.EqualTo(1));
        Assert.That(ability.UsesLegacyEffectStorage, Is.False);
        Assert.That(
            ability.Targeting.SelectionMode,
            Is.EqualTo(BattleAbilitySelectionMode.Manual));
        Assert.That(ability.Targeting.UsesWorldArea, Is.True);
        Assert.That(
            AbilityDefinitionValidator.TryValidate(ability, out string error),
            Is.True,
            error);
    }

    [Test]
    public void CharacterNoneSubject_ProjectsToInheritedTargets()
    {
        CharacterSkillDefinition skill = new();
        SetField(skill, "actionId", "skill.inherited_target_test");
        SetField(
            skill,
            "sections",
            new List<CharacterSkillSectionType>
            {
                CharacterSkillSectionType.Subject,
                CharacterSkillSectionType.Ability,
            });
        SetField(skill, "subject", CharacterAttackSubject.None);
        SetField(
            skill,
            "effects",
            new List<CharacterEffectDefinition> { new() });
        skill.Validate();

        Assert.That(
            skill.Targeting.SelectionMode,
            Is.EqualTo(BattleAbilitySelectionMode.Inherit));
        Assert.That(skill.Targeting.HasTarget, Is.True);
        Assert.That(
            AbilityDefinitionValidator.TryValidate(skill, out string error),
            Is.True,
            error);
    }

    [Test]
    public void CharacterAttackAndPassive_UseSkillTargetAreaEffectContract()
    {
        CharacterAttackDefinition attack = new();
        SetField(attack, "actionId", "attack.contract_test");
        SetField(
            attack,
            "sections",
            new List<CharacterAttackSectionType>
            {
                CharacterAttackSectionType.Subject,
                CharacterAttackSectionType.Ability,
            });
        SetField(attack, "subject", CharacterAttackSubject.Manual);
        SetField(
            attack,
            "effects",
            new List<CharacterEffectDefinition> { new() });
        SetField(attack, "areaDefinition", CreateWorldArea());
        attack.Validate();

        CharacterPassiveDefinition passive = new();
        SetField(passive, "actionId", "passive.contract_test");
        SetField(
            passive,
            "sections",
            new List<CharacterPassiveSectionType>
            {
                CharacterPassiveSectionType.Subject,
                CharacterPassiveSectionType.Ability,
            });
        SetField(passive, "subject", CharacterAttackSubject.Manual);
        SetField(
            passive,
            "effects",
            new List<CharacterEffectDefinition> { new() });
        SetField(passive, "areaDefinition", CreateWorldArea());
        passive.Validate();

        Assert.That(
            AbilityDefinitionValidator.TryValidate(
                attack,
                out string attackError),
            Is.True,
            attackError);
        Assert.That(
            AbilityDefinitionValidator.TryValidate(
                passive,
                out string passiveError),
            Is.True,
            passiveError);
        Assert.That(attack.Targeting.UsesWorldArea, Is.True);
        Assert.That(passive.Targeting.UsesWorldArea, Is.True);
    }

    [Test]
    public void CircularArea_AllowsAutomaticPriorityAndZeroForAllTargets()
    {
        CharacterSkillDefinition skill = new();
        SetField(skill, "actionId", "skill.area_all_test");
        SetField(
            skill,
            "sections",
            new List<CharacterSkillSectionType>
            {
                CharacterSkillSectionType.Subject,
                CharacterSkillSectionType.Ability,
            });
        SetField(skill, "subject", CharacterAttackSubject.Random);
        SetField(skill, "subjectCount", 0);
        SetField(skill, "areaDefinition", CreateWorldArea());
        SetField(
            skill,
            "effects",
            new List<CharacterEffectDefinition> { new() });
        skill.Validate();

        Assert.That(skill.SubjectCount, Is.Zero);
        Assert.That(skill.Targeting.UsesWorldArea, Is.True);
        Assert.That(
            skill.Targeting.SelectionMode,
            Is.EqualTo(BattleAbilitySelectionMode.Random));
        Assert.That(
            AbilityDefinitionValidator.TryValidate(skill, out string error),
            Is.True,
            error);
    }

    [Test]
    public void CharacterEnemyItemAndStatus_UseTargetlessCardDrawContract()
    {
        CharacterEffectDefinition effect = new();
        SetField(effect, "type", CharacterEffectType.CardDraw);
        SetField(
            effect,
            "damageAmountMode",
            CharacterDamageAmountMode.Fixed);
        SetField(effect, "damageAmount", 2f);
        effect.Validate();

        CharacterSkillDefinition skill = new();
        SetField(skill, "actionId", "skill.contract_test");
        SetField(
            skill,
            "sections",
            new List<CharacterSkillSectionType>
            {
                CharacterSkillSectionType.Ability,
            });
        SetField(skill, "subject", CharacterAttackSubject.None);
        SetField(
            skill,
            "effects",
            new List<CharacterEffectDefinition> { effect });
        skill.Validate();

        EnemyAbilityOperationDefinition operation = new();
        SetField(
            operation,
            "effects",
            new List<CharacterEffectDefinition> { effect });
        EnemyAbilityDefinition enemyAbility = new();
        SetField(enemyAbility, "abilityId", "enemy.contract_test");
        SetField(
            enemyAbility,
            "operations",
            new List<EnemyAbilityOperationDefinition> { operation });
        enemyAbility.Validate();

        BattleItemSO item = ScriptableObject.CreateInstance<BattleItemSO>();
        StatusEffectSO status = ScriptableObject.CreateInstance<StatusEffectSO>();
        try
        {
            SetField(item, "itemId", "item.contract_test");
            SetField(
                item,
                "abilityEffects",
                new List<CharacterEffectDefinition> { effect });
            status.RegenerateStatusId();
            StatusEffectTriggerBlockDefinition triggerBlock = new();
            SetField(
                triggerBlock,
                "effects",
                new List<CharacterEffectDefinition> { effect });
            SetField(
                status,
                "triggerBlocks",
                new List<StatusEffectTriggerBlockDefinition>
                {
                    triggerBlock,
                });
            status.ValidateDefinition();

            AssertCommonAbility(skill, BattleEffectOriginKind.CharacterSkill);
            AssertCommonAbility(enemyAbility, BattleEffectOriginKind.EnemyAbility);
            AssertCommonAbility(item, BattleEffectOriginKind.BattleItem);
            AssertCommonAbility(status, BattleEffectOriginKind.StatusEffect);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(status);
        }
    }

    [Test]
    public void EveryBattleOwner_RejectsInvalidSharedStatusEffect()
    {
        CharacterEffectDefinition effect = new();
        SetField(effect, "type", CharacterEffectType.ApplyStatus);
        SetField(effect, "statusEffect", null);

        CharacterSkillDefinition skill = new();
        SetField(skill, "actionId", "skill.invalid_shared_effect");
        SetField(
            skill,
            "sections",
            new List<CharacterSkillSectionType>
            {
                CharacterSkillSectionType.Ability,
            });
        SetField(
            skill,
            "effects",
            new List<CharacterEffectDefinition> { effect });

        EnemyAbilityOperationDefinition operation = new();
        SetField(
            operation,
            "effects",
            new List<CharacterEffectDefinition> { effect });
        EnemyAbilityDefinition enemyAbility = new();
        SetField(enemyAbility, "abilityId", "enemy.invalid_shared_effect");
        SetField(
            enemyAbility,
            "operations",
            new List<EnemyAbilityOperationDefinition> { operation });

        BattleItemSO item = ScriptableObject.CreateInstance<BattleItemSO>();
        BattleCardSO card = ScriptableObject.CreateInstance<BattleCardSO>();
        StatusEffectSO status = ScriptableObject.CreateInstance<StatusEffectSO>();
        try
        {
            SetField(item, "itemId", "item.invalid_shared_effect");
            SetField(
                item,
                "abilityEffects",
                new List<CharacterEffectDefinition> { effect });
            SetField(card, "cardId", "card.invalid_shared_effect");
            SetField(
                card,
                "abilityEffects",
                new List<CharacterEffectDefinition> { effect });
            status.RegenerateStatusId();
            StatusEffectTriggerBlockDefinition triggerBlock = new();
            SetField(
                triggerBlock,
                "effects",
                new List<CharacterEffectDefinition> { effect });
            SetField(
                status,
                "triggerBlocks",
                new List<StatusEffectTriggerBlockDefinition>
                {
                    triggerBlock,
                });

            Assert.That(
                AbilityDefinitionValidator.TryValidate(skill, out _),
                Is.False);
            Assert.That(
                AbilityDefinitionValidator.TryValidate(enemyAbility, out _),
                Is.False);
            Assert.That(
                AbilityDefinitionValidator.TryValidate(item, out _),
                Is.False);
            Assert.That(
                AbilityDefinitionValidator.TryValidate(card, out _),
                Is.False);
            Assert.That(
                AbilityDefinitionValidator.TryValidate(status, out _),
                Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(card);
            UnityEngine.Object.DestroyImmediate(status);
        }
    }

    [Test]
    public void RunActionsAndEnemyCoreStatsStayOutsideAbilityProviders()
    {
        IRunAbilityDefinition runAction = new DungeonRoomChoiceDefinition();
        Assert.That(
            runAction.ExecutionDomain,
            Is.EqualTo(AbilityExecutionDomain.Run));
        Assert.That(
            AbilityDefinitionValidator.TryValidate(runAction, out string runError),
            Is.True,
            runError);

        EnemySO enemy = ScriptableObject.CreateInstance<EnemySO>();
        try
        {
            enemy.RegenerateEnemyId();
            List<IBattleAbilityDefinition> discovered = new(
                enemy.EnumerateBattleAbilities());
            Assert.That(discovered, Is.Empty);
            Assert.That(
                AbilityDefinitionValidator.TryValidateProvider(
                    enemy,
                    out string providerError),
                Is.True,
                providerError);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemy);
        }
    }

    private static void AssertCommonAbility(
        IBattleAbilityDefinition ability,
        BattleEffectOriginKind expectedOrigin)
    {
        Assert.That(ability, Is.Not.Null);
        Assert.That(ability.OriginKind, Is.EqualTo(expectedOrigin));
        Assert.That(ability.HasExecutableContent, Is.True);
        Assert.That(
            BattleAbilityRules.RequiresActionTargets(ability),
            Is.False);
        Assert.That(
            AbilityDefinitionValidator.TryValidate(ability, out string error),
            Is.True,
            error);
    }

    private static BattleAreaDefinition CreateWorldArea()
    {
        BattleAreaDefinition area = new();
        SetField(
            area,
            "shapeType",
            CharacterAreaShapeType.CircleSector);
        SetField(
            area,
            "originMode",
            CharacterAreaOriginMode.DesignatedPoint);
        return area;
    }

}
