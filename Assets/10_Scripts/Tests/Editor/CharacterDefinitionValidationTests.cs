using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CharacterDefinitionValidationTests
{
    private const string FireStatusPath =
        "Assets/06_Runtime/Resources/StatusEffects/Fire.asset";
    private const string PoisonStatusPath =
        "Assets/06_Runtime/Resources/StatusEffects/Poison.asset";

    private readonly List<UnityEngine.Object> _createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = _createdObjects.Count - 1; index >= 0; index--)
        {
            if (_createdObjects[index] != null)
                UnityEngine.Object.DestroyImmediate(_createdObjects[index]);
        }

        _createdObjects.Clear();
    }

    [Test]
    public void Validate_NullDefinition_ReturnsRootError()
    {
        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(null);

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(result, "character.null"),
            Is.True);
    }

    [Test]
    public void Validate_AttackWithoutRequiredSections_ReturnsErrors()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        attacks.GetArrayElementAtIndex(0)
            .FindPropertyRelative("sections")
            .arraySize = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "attack.subject_required"),
            Is.True);
        Assert.That(
            HasDiagnostic(result, "attack.ability_required"),
            Is.True);
    }

    [Test]
    public void OnValidate_PreservesLegacySectionsAndAuthoredAreaOffsets()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
        SetSections(
            attack.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.LegacyDamageAmount);
        SerializedProperty offsets =
            attack.FindPropertyRelative("areaOffsets");
        offsets.arraySize = 1;
        offsets.GetArrayElementAtIndex(0)
            .FindPropertyRelative("rowOffset").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        MethodInfo onValidate = typeof(CharacterSO).GetMethod(
            "OnValidate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(onValidate, Is.Not.Null);
        onValidate.Invoke(definition, null);

        Assert.That(
            definition.AttackDefinitions[0].HasSection(
                CharacterAttackSectionType.LegacyDamageAmount),
            Is.True);
        Assert.That(definition.AttackDefinitions[0].AreaOffsets, Has.Count.EqualTo(1));
        Assert.That(
            definition.AttackDefinitions[0].AreaOffsets[0].RowOffset,
            Is.EqualTo(1));
    }

    [Test]
    public void ExplicitLegacyAttackMigration_CreatesSharedDamageEffect()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
        SetSections(
            attack.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.LegacyDamageAmount);
        attack.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        attack.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        attack.FindPropertyRelative("damageAmount").floatValue = 7f;

        MethodInfo migrate = typeof(CharacterEditorWindow).GetMethod(
            "MigrateLegacyAttackAbility",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(migrate, Is.Not.Null);
        migrate.Invoke(null, new object[] { attack });
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterAttackDefinition migrated =
            definition.AttackDefinitions[0];
        Assert.That(
            migrated.HasSection(CharacterAttackSectionType.Ability),
            Is.True);
        Assert.That(
            migrated.HasSection(
                CharacterAttackSectionType.LegacyDamageAmount),
            Is.False);
        Assert.That(migrated.Effects, Has.Count.EqualTo(1));
        Assert.That(
            migrated.Effects[0].Type,
            Is.EqualTo(CharacterEffectType.Damage));
        Assert.That(migrated.Effects[0].Amount, Is.EqualTo(7f));
    }

    [Test]
    public void Validate_PassiveWithoutAbility_ReturnsError()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty passives =
            serialized.FindProperty("passiveDefinitions");
        passives.arraySize = 1;
        SerializedProperty passive = passives.GetArrayElementAtIndex(0);
        SetSections(
            passive.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.Condition);
        SerializedProperty conditions =
            passive.FindPropertyRelative("numericConditions");
        conditions.arraySize = 1;
        conditions.GetArrayElementAtIndex(0)
            .FindPropertyRelative("threshold")
            .floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "passive.ability_required"),
            Is.True);
    }

    [Test]
    public void Validate_CooldownPassiveWithNoneSubject_ReturnsError()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty passives =
            serialized.FindProperty("passiveDefinitions");
        passives.arraySize = 1;
        SerializedProperty passive = passives.GetArrayElementAtIndex(0);
        SetSections(
            passive.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.Linkage,
            (int)CharacterPassiveSectionType.Subject,
            (int)CharacterPassiveSectionType.Ability);
        passive.FindPropertyRelative("trigger").enumValueIndex =
            (int)CharacterPassiveTrigger.OnCooldown;
        passive.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.None;
        passive.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        passive.FindPropertyRelative("damageAmount").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "passive.cooldown_target_missing"),
            Is.True);
    }

    [Test]
    public void Validate_KillPassiveWithNoneSubject_ReturnsError()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty passives =
            serialized.FindProperty("passiveDefinitions");
        passives.arraySize = 1;
        SerializedProperty passive = passives.GetArrayElementAtIndex(0);
        SetSections(
            passive.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.Linkage,
            (int)CharacterPassiveSectionType.Subject,
            (int)CharacterPassiveSectionType.Ability);
        passive.FindPropertyRelative("trigger").enumValueIndex =
            (int)CharacterPassiveTrigger.OnKill;
        passive.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.None;
        passive.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        passive.FindPropertyRelative("damageAmount").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "passive.kill_target_missing"),
            Is.True);
    }

    [Test]
    public void Validate_SpecificKillerWithoutCharacter_ReturnsError()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty passives =
            serialized.FindProperty("passiveDefinitions");
        passives.arraySize = 1;
        SerializedProperty passive = passives.GetArrayElementAtIndex(0);
        SetSections(
            passive.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.Linkage,
            (int)CharacterPassiveSectionType.Subject,
            (int)CharacterPassiveSectionType.Ability);
        passive.FindPropertyRelative("trigger").enumValueIndex =
            (int)CharacterPassiveTrigger.OnKill;
        passive.FindPropertyRelative("killSource").enumValueIndex =
            (int)CharacterPassiveKillSource.SpecificCharacter;
        passive.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Self;
        passive.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        passive.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        passive.FindPropertyRelative("damageAmount").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "passive.kill_character_required"),
            Is.True);
    }

    [Test]
    public void Validate_SelfStatusCostWithoutStatus_ReturnsError()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty passives =
            serialized.FindProperty("passiveDefinitions");
        passives.arraySize = 1;
        SerializedProperty passive = passives.GetArrayElementAtIndex(0);
        SetSections(
            passive.FindPropertyRelative("sections"),
            (int)CharacterPassiveSectionType.SelfStatusCost,
            (int)CharacterPassiveSectionType.Ability);
        passive.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        passive.FindPropertyRelative("damageAmount").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "passive.cost_status_required"),
            Is.True);
    }

    [Test]
    public void Validate_AllyDirectDamage_ReturnsError()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
        SetSections(
            attack.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.Subject,
            (int)CharacterAttackSectionType.Ability);
        attack.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        attack.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Self;
        SerializedProperty effects =
            attack.FindPropertyRelative("effects");
        effects.arraySize = 1;
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.Damage;
        effect.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Physical;
        effect.FindPropertyRelative("damageAmount").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "effect.ally_damage_unsupported"),
            Is.True);
    }

    [Test]
    public void Validate_StatusEffectFactionMismatch_ReturnsError()
    {
        StatusEffectSO fire =
            AssetDatabase.LoadAssetAtPath<StatusEffectSO>(FireStatusPath);
        Assert.That(fire, Is.Not.Null);

        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty skills =
            serialized.FindProperty("skillDefinitions");
        skills.arraySize = 1;
        SerializedProperty skill = skills.GetArrayElementAtIndex(0);
        SetSections(
            skill.FindPropertyRelative("sections"),
            (int)CharacterSkillSectionType.Subject,
            (int)CharacterSkillSectionType.Ability);
        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Self;
        SerializedProperty effects =
            skill.FindPropertyRelative("effects");
        effects.arraySize = 1;
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.ApplyStatus;
        effect.FindPropertyRelative("statusEffect").objectReferenceValue =
            fire;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "effect.status_faction_mismatch"),
            Is.True);
    }

    [Test]
    public void Validate_StatusStacksWithoutExplicitStatus_ReturnsError()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
        SetSections(
            attack.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.Subject,
            (int)CharacterAttackSectionType.Condition,
            (int)CharacterAttackSectionType.Ability);
        attack.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        attack.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        SerializedProperty conditions =
            attack.FindPropertyRelative("numericConditions");
        conditions.arraySize = 1;
        SerializedProperty condition = conditions.GetArrayElementAtIndex(0);
        condition.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterConditionType.Numeric;
        condition.FindPropertyRelative("metric").enumValueIndex =
            (int)CharacterNumericConditionMetric.StatusStackCount;
        condition.FindPropertyRelative("comparison").enumValueIndex =
            (int)CharacterNumericComparison.GreaterThanOrEqual;
        condition.FindPropertyRelative("threshold").floatValue = 1f;
        attack.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        attack.FindPropertyRelative("damageAmount").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "condition.status_required"),
            Is.True);
    }

    [Test]
    public void Validate_StatusScopeDoesNotRequireExplicitStatusAssets()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
        SetSections(
            attack.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.Subject,
            (int)CharacterAttackSectionType.Condition,
            (int)CharacterAttackSectionType.Ability);
        attack.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        attack.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        SerializedProperty conditions =
            attack.FindPropertyRelative("numericConditions");
        conditions.arraySize = 1;
        SerializedProperty condition =
            conditions.GetArrayElementAtIndex(0);
        condition.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterConditionType.Numeric;
        condition.FindPropertyRelative("metric").enumValueIndex =
            (int)CharacterNumericConditionMetric.StatusStackCount;
        condition.FindPropertyRelative(
            "statusSelectionScope").enumValueIndex =
            (int)CharacterStatusSelectionScope.AllDebuffs;
        condition.FindPropertyRelative("statusMatchMode").enumValueIndex =
            (int)CharacterStatusConditionMatchMode.AtLeastCount;
        condition.FindPropertyRelative("statusMatchCount").intValue = 3;
        condition.FindPropertyRelative("comparison").enumValueIndex =
            (int)CharacterNumericComparison.GreaterThanOrEqual;
        condition.FindPropertyRelative("threshold").floatValue = 1f;
        attack.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        attack.FindPropertyRelative("damageAmount").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "condition.status_required"),
            Is.False);
        Assert.That(
            HasDiagnostic(
                result,
                "condition.status_match_count_exceeds_selection"),
            Is.False);
    }

    [Test]
    public void Validate_SourceConditionRejectsEnemyOnlyMetric()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
        SetSections(
            attack.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.Subject,
            (int)CharacterAttackSectionType.Condition,
            (int)CharacterAttackSectionType.Ability);
        attack.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        attack.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        SerializedProperty conditions =
            attack.FindPropertyRelative("numericConditions");
        conditions.arraySize = 1;
        SerializedProperty condition = conditions.GetArrayElementAtIndex(0);
        condition.FindPropertyRelative("target").enumValueIndex =
            (int)CharacterConditionTarget.Source;
        condition.FindPropertyRelative("metric").enumValueIndex =
            (int)CharacterNumericConditionMetric.StackCount;
        condition.FindPropertyRelative("threshold").floatValue = 1f;
        attack.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        attack.FindPropertyRelative("damageAmount").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "condition.metric_faction_mismatch"),
            Is.True);
    }

    [Test]
    public void Validate_StatusEffectWithoutExplicitStatus_ReturnsError()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
        SetSections(
            attack.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.Subject,
            (int)CharacterAttackSectionType.Ability);
        attack.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        attack.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        SerializedProperty effects =
            attack.FindPropertyRelative("effects");
        effects.arraySize = 1;
        effects.GetArrayElementAtIndex(0)
            .FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.ApplyStatus;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "effect.status_required"),
            Is.True);
    }

    [Test]
    public void Validate_SingleRemovalWithoutExplicitStatus_ReturnsError()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
        SetSections(
            attack.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.Subject,
            (int)CharacterAttackSectionType.Ability);
        attack.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        attack.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Self;
        SerializedProperty effects =
            attack.FindPropertyRelative("effects");
        effects.arraySize = 1;
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.RemoveStatus;
        effect.FindPropertyRelative("statusRemovalTarget").enumValueIndex =
            (int)CharacterStatusRemovalTarget.Single;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "effect.removal_status_required"),
            Is.True);
    }

    [Test]
    public void Validate_StatusRemovalRatioOutsideRange_ReturnsError()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
        SetSections(
            attack.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.Subject,
            (int)CharacterAttackSectionType.Ability);
        attack.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        attack.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Self;
        SerializedProperty effects =
            attack.FindPropertyRelative("effects");
        effects.arraySize = 1;
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.RemoveStatus;
        effect.FindPropertyRelative("statusRemovalTarget").enumValueIndex =
            (int)CharacterStatusRemovalTarget.All;
        effect.FindPropertyRelative(
            "statusRemovalAmountMode").enumValueIndex =
            (int)CharacterStatusRemovalAmountMode.CurrentStacksRatio;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterEffectDefinition runtimeEffect =
            definition.AttackDefinitions[0].Effects[0];
        FieldInfo ratioField = typeof(CharacterEffectDefinition).GetField(
            "statusRemovalRatio",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(ratioField, Is.Not.Null);
        ratioField.SetValue(runtimeEffect, 0f);

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "effect.removal_ratio_invalid"),
            Is.True);
    }

    [Test]
    public void Validate_StatusRemovalRandomCountBelowOne_ReturnsError()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
        SetSections(
            attack.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.Subject,
            (int)CharacterAttackSectionType.Ability);
        attack.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Ally;
        attack.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Self;
        SerializedProperty effects =
            attack.FindPropertyRelative("effects");
        effects.arraySize = 1;
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.RemoveStatus;
        effect.FindPropertyRelative("statusRemovalTarget").enumValueIndex =
            (int)CharacterStatusRemovalTarget.Buff;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterEffectDefinition runtimeEffect =
            definition.AttackDefinitions[0].Effects[0];
        FieldInfo pickModeField =
            typeof(CharacterEffectDefinition).GetField(
                "statusRemovalPickMode",
                BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo pickCountField =
            typeof(CharacterEffectDefinition).GetField(
                "statusRemovalPickCount",
                BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(pickModeField, Is.Not.Null);
        Assert.That(pickCountField, Is.Not.Null);
        pickModeField.SetValue(
            runtimeEffect,
            CharacterStatusRemovalPickMode.RandomCount);
        pickCountField.SetValue(runtimeEffect, 0);

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "effect.removal_pick_count_invalid"),
            Is.True);
    }

    [Test]
    public void Validate_ExplicitMultipleStatusRemoval_AcceptsUniqueStatuses()
    {
        StatusEffectSO fire =
            AssetDatabase.LoadAssetAtPath<StatusEffectSO>(FireStatusPath);
        StatusEffectSO poison =
            AssetDatabase.LoadAssetAtPath<StatusEffectSO>(PoisonStatusPath);
        Assert.That(fire, Is.Not.Null);
        Assert.That(poison, Is.Not.Null);

        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
        SetSections(
            attack.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.Subject,
            (int)CharacterAttackSectionType.Ability);
        attack.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        attack.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;

        SerializedProperty effects =
            attack.FindPropertyRelative("effects");
        effects.arraySize = 1;
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.RemoveStatus;
        effect.FindPropertyRelative("statusRemovalTarget").enumValueIndex =
            (int)CharacterStatusRemovalTarget.Single;
        SerializedProperty statuses =
            effect.FindPropertyRelative("statusRemovalEffects");
        statuses.arraySize = 2;
        statuses.GetArrayElementAtIndex(0).objectReferenceValue = fire;
        statuses.GetArrayElementAtIndex(1).objectReferenceValue = poison;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "effect.removal_status_required"),
            Is.False);
        Assert.That(
            HasDiagnostic(result, "effect.removal_status_duplicate"),
            Is.False);
    }

    [Test]
    public void Validate_ExplicitMultipleStatusRemoval_RejectsDuplicates()
    {
        StatusEffectSO fire =
            AssetDatabase.LoadAssetAtPath<StatusEffectSO>(FireStatusPath);
        Assert.That(fire, Is.Not.Null);

        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
        SetSections(
            attack.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.Subject,
            (int)CharacterAttackSectionType.Ability);
        attack.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        attack.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;

        SerializedProperty effects =
            attack.FindPropertyRelative("effects");
        effects.arraySize = 1;
        SerializedProperty effect = effects.GetArrayElementAtIndex(0);
        effect.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.RemoveStatus;
        effect.FindPropertyRelative("statusRemovalTarget").enumValueIndex =
            (int)CharacterStatusRemovalTarget.Single;
        SerializedProperty statuses =
            effect.FindPropertyRelative("statusRemovalEffects");
        statuses.arraySize = 2;
        statuses.GetArrayElementAtIndex(0).objectReferenceValue = fire;
        statuses.GetArrayElementAtIndex(1).objectReferenceValue = fire;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "effect.removal_status_duplicate"),
            Is.True);
    }

    [Test]
    public void Validate_ExplicitDamageAndStatusSkill_Succeeds()
    {
        StatusEffectSO fire =
            AssetDatabase.LoadAssetAtPath<StatusEffectSO>(FireStatusPath);
        Assert.That(fire, Is.Not.Null);

        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty skills =
            serialized.FindProperty("skillDefinitions");
        skills.arraySize = 1;
        SerializedProperty skill = skills.GetArrayElementAtIndex(0);
        SetSections(
            skill.FindPropertyRelative("sections"),
            (int)CharacterSkillSectionType.Cost,
            (int)CharacterSkillSectionType.Subject,
            (int)CharacterSkillSectionType.Ability);
        skill.FindPropertyRelative("cost").intValue = 2;
        skill.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        skill.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        skill.FindPropertyRelative("subjectCount").intValue = 1;

        SerializedProperty effects =
            skill.FindPropertyRelative("effects");
        effects.arraySize = 2;
        SerializedProperty damage = effects.GetArrayElementAtIndex(0);
        damage.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.Damage;
        damage.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        damage.FindPropertyRelative("damageAmountMode").enumValueIndex =
            (int)CharacterDamageAmountMode.Fixed;
        damage.FindPropertyRelative("damageAmount").floatValue = 4f;

        SerializedProperty status = effects.GetArrayElementAtIndex(1);
        status.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterEffectType.ApplyStatus;
        status.FindPropertyRelative("statusEffect").objectReferenceValue =
            fire;
        status.FindPropertyRelative("statusDuration").floatValue = 3f;
        status.FindPropertyRelative("statusStacks").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            result.IsValid,
            Is.True,
            string.Join("\n", result.Diagnostics));
        Assert.That(
            HasDiagnostic(result, "ability.legacy_fallback"),
            Is.False);
    }

    [Test]
    public void Validate_LegacyAbility_ReturnsUnsupportedSchemaError()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty attack = attacks.GetArrayElementAtIndex(0);
        SetSections(
            attack.FindPropertyRelative("sections"),
            (int)CharacterAttackSectionType.Subject,
            (int)CharacterAttackSectionType.Ability);
        attack.FindPropertyRelative("targetFaction").enumValueIndex =
            (int)CharacterTargetFaction.Enemy;
        attack.FindPropertyRelative("subject").enumValueIndex =
            (int)CharacterAttackSubject.Random;
        attack.FindPropertyRelative("damageType").enumValueIndex =
            (int)CharacterAttackDamageType.Fixed;
        attack.FindPropertyRelative("damageAmount").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            HasDiagnostic(
                result,
                "attack.legacy_ability_unsupported",
                CharacterDefinitionDiagnosticSeverity.Error),
            Is.True);
    }

    [Test]
    public void LegacyHasStatus_Validate_MigratesToStatusStackCondition()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty attacks =
            serialized.FindProperty("attackDefinitions");
        attacks.arraySize = 1;
        SerializedProperty conditions = attacks.GetArrayElementAtIndex(0)
            .FindPropertyRelative("numericConditions");
        conditions.arraySize = 1;
        SerializedProperty serializedCondition =
            conditions.GetArrayElementAtIndex(0);
        serializedCondition.FindPropertyRelative("type").enumValueIndex =
            (int)CharacterConditionType.HasStatus;
        serializedCondition.FindPropertyRelative("metric").enumValueIndex =
            (int)CharacterNumericConditionMetric.Health;
        serializedCondition.FindPropertyRelative("comparison").enumValueIndex =
            (int)CharacterNumericComparison.LessThan;
        serializedCondition.FindPropertyRelative("threshold").floatValue = 9f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterNumericCondition condition =
            definition.AttackDefinitions[0].NumericConditions[0];
        condition.Validate();

        Assert.That(
            condition.Type,
            Is.EqualTo(CharacterConditionType.Numeric));
        Assert.That(
            condition.Metric,
            Is.EqualTo(CharacterNumericConditionMetric.StatusStackCount));
        Assert.That(
            condition.Comparison,
            Is.EqualTo(CharacterNumericComparison.GreaterThanOrEqual));
        Assert.That(condition.Threshold, Is.EqualTo(1f));
    }

    [Test]
    public void Validate_DuplicateUpgradeAndWrongTotal_ReturnsErrors()
    {
        CharacterSO definition = CreateDefinition();
        SerializedObject serialized = new(definition);
        SerializedProperty upgrades =
            serialized.FindProperty("dungeonUpgradeDefinitions");
        upgrades.arraySize = 1;
        SerializedProperty entries = upgrades.GetArrayElementAtIndex(0)
            .FindPropertyRelative("entries");
        entries.arraySize = 2;
        for (int index = 0; index < entries.arraySize; index++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("type").enumValueIndex =
                (int)CharacterDungeonUpgradeType.AttackPower;
            entry.FindPropertyRelative("probability").floatValue = 40f;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CharacterDefinitionValidationResult result =
            CharacterDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "upgrade.type_duplicate"),
            Is.True);
        Assert.That(
            HasDiagnostic(result, "upgrade.probability_total"),
            Is.True);
    }

    [Test]
    [Category("ContentValidation")]
    public void AllCharacterAssets_PassSharedEditorValidation()
    {
        LoadCharacterAssets(
            out List<string> paths,
            out List<CharacterSO> definitions);

        int errorCount = 0;
        StringBuilder message = new();
        for (int index = 0; index < definitions.Count; index++)
        {
            CharacterDefinitionValidationResult result =
                CharacterDefinitionValidator.Validate(
                    definitions[index],
                    definitions);
            foreach (CharacterDefinitionDiagnostic diagnostic in
                     result.Diagnostics)
            {
                if (diagnostic.Severity ==
                    CharacterDefinitionDiagnosticSeverity.Warning)
                {
                    TestContext.Progress.WriteLine(
                        $"{paths[index]} :: {diagnostic}");
                    continue;
                }

                errorCount++;
                message.Append("- ")
                    .Append(paths[index])
                    .Append(" :: ")
                    .AppendLine(diagnostic.ToString());
            }
            if (!AbilityDefinitionValidator.TryValidateProvider(
                    definitions[index],
                    out string abilityError))
            {
                errorCount++;
                message.Append("- ")
                    .Append(paths[index])
                    .Append(" :: common ability contract: ")
                    .AppendLine(abilityError);
            }
        }

        Assert.That(
            errorCount,
            Is.Zero,
            $"{errorCount} CharacterSO validation error(s):\n{message}");
    }

    [Test]
    [Category("ContentValidation")]
    public void Validate_DoesNotMutateCharacterAssets()
    {
        LoadCharacterAssets(
            out _,
            out List<CharacterSO> definitions);
        List<string> before = new(definitions.Count);
        foreach (CharacterSO definition in definitions)
            before.Add(EditorJsonUtility.ToJson(definition));

        CharacterDefinitionValidator.ValidateAll(definitions);

        for (int index = 0; index < definitions.Count; index++)
        {
            Assert.That(
                EditorJsonUtility.ToJson(definitions[index]),
                Is.EqualTo(before[index]),
                $"Validator mutated '{definitions[index].name}'.");
        }
    }

    [Test]
    [Category("ContentValidation")]
    public void AllStatusEffectAssets_HaveNoDefinitionErrors()
    {
        LoadStatusEffectAssets(
            out List<string> paths,
            out List<StatusEffectSO> definitions);

        int errorCount = 0;
        StringBuilder message = new();
        for (int index = 0; index < definitions.Count; index++)
        {
            StatusEffectDefinitionValidationResult result =
                StatusEffectDefinitionValidator.Validate(
                    definitions[index],
                    definitions);
            foreach (StatusEffectDefinitionDiagnostic diagnostic in
                     result.Diagnostics)
            {
                if (diagnostic.Severity ==
                    CharacterDefinitionDiagnosticSeverity.Warning)
                {
                    TestContext.Progress.WriteLine(
                        $"{paths[index]} :: {diagnostic}");
                    continue;
                }

                errorCount++;
                message.Append("- ")
                    .Append(paths[index])
                    .Append(" :: ")
                    .AppendLine(diagnostic.ToString());
            }
            if (!AbilityDefinitionValidator.TryValidateProvider(
                    definitions[index],
                    out string abilityError))
            {
                errorCount++;
                message.Append("- ")
                    .Append(paths[index])
                    .Append(" :: common ability contract: ")
                    .AppendLine(abilityError);
            }
        }

        Assert.That(
            errorCount,
            Is.Zero,
            $"{errorCount} StatusEffectSO validation error(s):\n{message}");
    }

    [Test]
    public void ValidateStatusEffect_SupportedLifecycleAndAllyOperations_Succeeds()
    {
        StatusEffectSO lifecycle = CreateStatusEffectDefinition(
            true,
            false,
            4);
        StatusEffectOperationTrigger[] lifecycleTriggers =
        {
            StatusEffectOperationTrigger.OnApply,
            StatusEffectOperationTrigger.OnStackChanged,
            StatusEffectOperationTrigger.OnRemove,
            StatusEffectOperationTrigger.OnExpire,
        };
        for (int index = 0; index < lifecycleTriggers.Length; index++)
        {
            ConfigureStatusOperation(
                lifecycle,
                index,
                lifecycleTriggers[index],
                StatusEffectOperationType.InstantDamage,
                StatusEffectValueMode.Fixed,
                1f,
                true);
        }

        StatusEffectSO ally = CreateStatusEffectDefinition(
            false,
            true,
            3);
        ConfigureStatusOperation(
            ally,
            0,
            StatusEffectOperationTrigger.OnApply,
            StatusEffectOperationType.AttackPowerModifier,
            StatusEffectValueMode.Fixed,
            2f,
            true);
        ConfigureStatusOperation(
            ally,
            1,
            StatusEffectOperationTrigger.OnApply,
            StatusEffectOperationType.AttackSpeedModifier,
            StatusEffectValueMode.Ratio,
            0.25f,
            false);
        ConfigureStatusOperation(
            ally,
            2,
            StatusEffectOperationTrigger.OnApply,
            StatusEffectOperationType.DisableAction,
            StatusEffectValueMode.Fixed,
            1f,
            false);

        StatusEffectDefinitionValidationResult lifecycleResult =
            StatusEffectDefinitionValidator.Validate(lifecycle);
        StatusEffectDefinitionValidationResult allyResult =
            StatusEffectDefinitionValidator.Validate(ally);

        Assert.That(
            lifecycleResult.IsValid,
            Is.True,
            string.Join("\n", lifecycleResult.Diagnostics));
        Assert.That(
            allyResult.IsValid,
            Is.True,
            string.Join("\n", allyResult.Diagnostics));
    }

    [Test]
    public void ValidateStatusEffect_InvalidInstantDamageTriggerAndFaction_ReturnsErrors()
    {
        StatusEffectSO definition = CreateStatusEffectDefinition(
            false,
            true,
            1);
        ConfigureStatusOperation(
            definition,
            0,
            StatusEffectOperationTrigger.OnTick,
            StatusEffectOperationType.InstantDamage,
            StatusEffectValueMode.Fixed,
            1f,
            false);

        StatusEffectDefinitionValidationResult result =
            StatusEffectDefinitionValidator.Validate(definition);

        Assert.That(
            HasDiagnostic(result, "status.instant_damage_trigger_invalid"),
            Is.True);
        Assert.That(
            HasDiagnostic(result, "status.instant_damage_target_unsupported"),
            Is.True);
    }

    [Test]
    public void ValidateStatusEffect_DoesNotMutateAssets()
    {
        LoadStatusEffectAssets(
            out _,
            out List<StatusEffectSO> definitions);
        List<string> before = new(definitions.Count);
        foreach (StatusEffectSO definition in definitions)
            before.Add(EditorJsonUtility.ToJson(definition));

        StatusEffectDefinitionValidator.ValidateAll(definitions);

        for (int index = 0; index < definitions.Count; index++)
        {
            Assert.That(
                EditorJsonUtility.ToJson(definitions[index]),
                Is.EqualTo(before[index]),
                $"Validator mutated '{definitions[index].name}'.");
        }
    }

    private StatusEffectSO CreateStatusEffectDefinition(
        bool canTargetEnemy,
        bool canTargetAlly,
        int operationCount)
    {
        StatusEffectSO definition =
            ScriptableObject.CreateInstance<StatusEffectSO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(definition);

        SerializedObject serialized = new(definition);
        serialized.FindProperty("statusId").stringValue =
            Guid.NewGuid().ToString("N");
        serialized.FindProperty("nameLocalizationKey").stringValue =
            "status.fire.name";
        serialized.FindProperty("descriptionLocalizationKey").stringValue =
            "status.fire.description";
        serialized.FindProperty("canTargetEnemy").boolValue = canTargetEnemy;
        serialized.FindProperty("canTargetAlly").boolValue = canTargetAlly;
        serialized.FindProperty("defaultDuration").floatValue = 1f;
        serialized.FindProperty("tickInterval").floatValue = 1f;
        serialized.FindProperty("operations").arraySize =
            Mathf.Max(0, operationCount);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static void ConfigureStatusOperation(
        StatusEffectSO definition,
        int index,
        StatusEffectOperationTrigger trigger,
        StatusEffectOperationType operationType,
        StatusEffectValueMode valueMode,
        float value,
        bool scaleWithStacks)
    {
        SerializedObject serialized = new(definition);
        SerializedProperty operation = serialized
            .FindProperty("operations")
            .GetArrayElementAtIndex(index);
        operation.FindPropertyRelative("trigger").enumValueIndex =
            (int)trigger;
        operation.FindPropertyRelative("operationType").enumValueIndex =
            (int)operationType;
        operation.FindPropertyRelative("valueMode").enumValueIndex =
            (int)valueMode;
        operation.FindPropertyRelative("value").floatValue = value;
        operation.FindPropertyRelative("scaleWithStacks").boolValue =
            scaleWithStacks;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private CharacterSO CreateDefinition()
    {
        CharacterSO definition =
            ScriptableObject.CreateInstance<CharacterSO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        _createdObjects.Add(definition);

        SerializedObject serialized = new(definition);
        serialized.FindProperty("characterId").stringValue =
            Guid.NewGuid().ToString("N");
        serialized.FindProperty("nameLocalizationKey").stringValue =
            "character.suiren.name";
        serialized.FindProperty("descriptionLocalizationKey").stringValue =
            "character.suiren.description";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return definition;
    }

    private static void SetSections(
        SerializedProperty sections,
        params int[] values)
    {
        sections.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
        {
            sections.GetArrayElementAtIndex(index).enumValueIndex =
                values[index];
        }
    }

    private static bool HasDiagnostic(
        CharacterDefinitionValidationResult result,
        string code)
    {
        foreach (CharacterDefinitionDiagnostic diagnostic in
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

    private static bool HasDiagnostic(
        CharacterDefinitionValidationResult result,
        string code,
        CharacterDefinitionDiagnosticSeverity severity)
    {
        foreach (CharacterDefinitionDiagnostic diagnostic in
                 result.Diagnostics)
        {
            if (diagnostic.Severity == severity &&
                string.Equals(
                    diagnostic.Code,
                    code,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDiagnostic(
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

    private static void LoadCharacterAssets(
        out List<string> paths,
        out List<CharacterSO> definitions)
    {
        string[] guids = AssetDatabase.FindAssets("t:CharacterSO");
        paths = new List<string>(guids.Length);
        foreach (string guid in guids)
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort(StringComparer.Ordinal);

        Assert.That(
            paths,
            Is.Not.Empty,
            "No CharacterSO assets were imported in the project.");

        definitions = new List<CharacterSO>(paths.Count);
        foreach (string path in paths)
        {
            CharacterSO definition =
                AssetDatabase.LoadAssetAtPath<CharacterSO>(path);
            Assert.That(
                definition,
                Is.Not.Null,
                $"Failed to load CharacterSO at '{path}'.");
            definitions.Add(definition);
        }
    }

    private static void LoadStatusEffectAssets(
        out List<string> paths,
        out List<StatusEffectSO> definitions)
    {
        string[] guids = AssetDatabase.FindAssets("t:StatusEffectSO");
        paths = new List<string>(guids.Length);
        foreach (string guid in guids)
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort(StringComparer.Ordinal);

        Assert.That(
            paths,
            Is.Not.Empty,
            "No StatusEffectSO assets were imported in the project.");

        definitions = new List<StatusEffectSO>(paths.Count);
        foreach (string path in paths)
        {
            StatusEffectSO definition =
                AssetDatabase.LoadAssetAtPath<StatusEffectSO>(path);
            Assert.That(
                definition,
                Is.Not.Null,
                $"Failed to load StatusEffectSO at '{path}'.");
            definitions.Add(definition);
        }
    }
}
