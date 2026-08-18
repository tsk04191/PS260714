using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static TestReflection;

public sealed class EnemyRosterSchemaTests
{
    private readonly List<Object> _createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        foreach (Object createdObject in _createdObjects)
        {
            if (createdObject != null)
                Object.DestroyImmediate(createdObject);
        }
        _createdObjects.Clear();
    }

    [Test]
    public void TypeDefaults_PreservePersistentEnemyId()
    {
        EnemySO enemy = CreateEnemy("persistent_roster_id");
        SetPrivateField(enemy, "type", EEnemyType.Heavy);
        MethodInfo applyDefaults = typeof(EnemySO).GetMethod(
            "ApplyCurrentTypeDefaults",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(applyDefaults, Is.Not.Null);
        applyDefaults.Invoke(enemy, null);

        Assert.That(enemy.EnemyId, Is.EqualTo("persistent_roster_id"));
        Assert.That(enemy.Type, Is.EqualTo(EEnemyType.Heavy));
        Assert.That(
            enemy.RoleTags,
            Does.Contain(EnemyTypeDisplay.GetId(EEnemyType.Heavy)));
    }

    [Test]
    public void RosterMigration_StagesLegacyMetadataWithoutChangingId()
    {
        EnemySO enemy = CreateEnemy("legacy_roster_id", currentRoster: false);
        SetPrivateField(enemy, "grade", EEnemyGrade.Special);
        SetPrivateField(enemy, "type", EEnemyType.Medic);
        SetPrivateField(enemy, "coreAttackDamage", 7);

        Assert.That(EnemyRosterSchemaMigration.ApplyMigration(enemy), Is.True);

        Assert.That(enemy.EnemyId, Is.EqualTo("legacy_roster_id"));
        Assert.That(
            enemy.RosterSchemaVersion,
            Is.EqualTo(EnemySO.CurrentRosterSchemaVersion));
        Assert.That(enemy.RosterTier, Is.EqualTo(EnemyRosterTier.Special));
        Assert.That(enemy.RoleTags, Does.Contain("medic"));
        Assert.That(enemy.RecommendedMaxPerWave, Is.EqualTo(2));
        Assert.That(
            enemy.CoreAttackDamagePolicy,
            Is.EqualTo(EnemyCoreAttackDamagePolicy.LegacyInteger));
        Assert.That(enemy.CoreAttackDamageValue, Is.EqualTo(7f));
    }

    [Test]
    public void FractionalCoreDamage_UsesDeterministicCarry()
    {
        float remainder = 0f;
        int[] resolved = new int[4];
        for (int index = 0; index < resolved.Length; index++)
        {
            resolved[index] = EnemyCoreAttackDamageResolver.Resolve(
                1.75f,
                EnemyCoreAttackDamagePolicy.AccumulateFraction,
                ref remainder);
        }

        Assert.That(resolved, Is.EqualTo(new[] { 1, 2, 2, 2 }));
        Assert.That(resolved.Sum(), Is.EqualTo(7));
        Assert.That(remainder, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void PreciseCoreDamage_PreservesLegacyIntegerField()
    {
        EnemySO enemy = CreateEnemy("precise_damage_enemy");
        SetPrivateField(enemy, "coreAttackDamage", 2);
        SetPrivateField(
            enemy,
            "coreAttackDamagePolicy",
            EnemyCoreAttackDamagePolicy.AccumulateFraction);
        SetPrivateField(enemy, "preciseCoreAttackDamage", 1.75f);

        Assert.That(enemy.CoreAttackDamage, Is.EqualTo(2));
        Assert.That(enemy.CoreAttackDamageValue, Is.EqualTo(1.75f));
        Assert.That(
            EnemyDefinitionValidator.Validate(enemy).ErrorCount,
            Is.Zero);
    }

    [Test]
    public void WorldRadiusTarget_IsValidatedAsWorldDistance()
    {
        EnemySO enemy = CreateEnemy("world_radius_enemy");
        EnemyAbilityDefinition ability = CreateSpecializedAbility(
            "world_aura",
            EnemyAbilityOperationType.ModifyCoreAttackDamage);
        EnemyAbilityTargetDefinition target = ability.Target;
        SetPrivateField(
            target,
            "faction",
            EnemyAbilityTargetFaction.EnemyAllies);
        SetPrivateField(
            target,
            "subject",
            EnemyAbilityTargetSubject.WorldRadius);
        SetPrivateField(target, "worldRadius", 2.5f);
        SetPrivateField(
            target,
            "layerScope",
            EnemyWorldLayerScope.All);
        SetPrivateField(
            enemy,
            "abilities",
            new List<EnemyAbilityDefinition> { ability });

        EnemyDefinitionValidationResult valid =
            EnemyDefinitionValidator.Validate(enemy);
        Assert.That(valid.ErrorCount, Is.Zero, BuildFailureMessage(valid));

        SetPrivateField(target, "worldRadius", 0f);
        EnemyDefinitionValidationResult invalid =
            EnemyDefinitionValidator.Validate(enemy);
        Assert.That(
            HasCode(invalid, "ability.target_world_radius_invalid"),
            Is.True,
            BuildFailureMessage(invalid));
    }

    [Test]
    public void SpecializedOnlyAbility_IsExecutableAndCatalogVisible()
    {
        EnemySO enemy = CreateEnemy("specialized_enemy");
        EnemyAbilityDefinition ability = CreateSpecializedAbility(
            "summon_contract",
            EnemyAbilityOperationType.ModifyCoreAttackInterval);
        SetPrivateField(
            enemy,
            "abilities",
            new List<EnemyAbilityDefinition> { ability });

        Assert.That(ability.HasUnifiedEffects, Is.False);
        Assert.That(ability.HasExecutableContent, Is.True);
        Assert.That(ability.BattleEffects, Is.Empty);
        Assert.That(
            enemy.EnumerateBattleAbilities().Single(),
            Is.SameAs(ability));
    }

    [Test]
    public void CompositeTriggersAndHealthCooldownOverrides_AreDataDriven()
    {
        EnemySO enemy = CreateEnemy("compound_trigger_enemy");
        EnemyAbilityDefinition ability = CreateSpecializedAbility(
            "threshold_or_contact",
            EnemyAbilityOperationType.ModifyCoreAttackDamage);
        SetPrivateField(
            ability,
            "trigger",
            EnemyAbilityTrigger.OnCooldown);
        SetPrivateField(
            ability,
            "triggerEvents",
            new List<EnemyAbilityTrigger>
            {
                EnemyAbilityTrigger.OnCoreContact
            });
        SetPrivateField(ability, "cooldown", 10f);
        EnemyAbilityCooldownOverrideDefinition cooldownRule = new();
        SetPrivateField(cooldownRule, "healthAtOrBelowPercent", 50f);
        SetPrivateField(cooldownRule, "cooldown", 6f);
        SetPrivateField(
            ability,
            "cooldownOverrides",
            new List<EnemyAbilityCooldownOverrideDefinition>
            {
                cooldownRule
            });
        SetPrivateField(
            enemy,
            "abilities",
            new List<EnemyAbilityDefinition> { ability });

        Assert.That(
            ability.RespondsToTrigger(EnemyAbilityTrigger.OnCooldown),
            Is.True);
        Assert.That(
            ability.RespondsToTrigger(EnemyAbilityTrigger.OnCoreContact),
            Is.True);
        Assert.That(ability.ResolveCooldown(75f), Is.EqualTo(10f));
        Assert.That(ability.ResolveCooldown(40f), Is.EqualTo(6f));
        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.Validate(enemy);
        Assert.That(result.ErrorCount, Is.Zero, BuildFailureMessage(result));
    }

    [Test]
    public void NonRecursiveSelfSummon_IsValid_AndRecursiveSummonRequiresCap()
    {
        EnemySO enemy = CreateEnemy("recursive_summoner");
        EnemyAbilityDefinition ability = CreateSpecializedAbility(
            "recursive_spawn",
            EnemyAbilityOperationType.SummonEnemy);
        EnemyAbilityOperationDefinition operation = ability.Operations[0];
        EnemySummonDefinition summon = operation.Summon;
        EnemyReferenceDefinition reference = new();
        SetPrivateField(reference, "enemyId", enemy.EnemyId);
        SetPrivateField(
            summon,
            "candidates",
            new List<EnemyReferenceDefinition> { reference });
        SetPrivateField(summon, "maximumActive", 0);
        SetPrivateField(
            enemy,
            "abilities",
            new List<EnemyAbilityDefinition> { ability });

        SetPrivateField(summon, "allowRecursiveSummon", false);
        EnemyDefinitionValidationResult oneGeneration =
            EnemyDefinitionValidator.Validate(enemy);
        Assert.That(
            oneGeneration.ErrorCount,
            Is.Zero,
            BuildFailureMessage(oneGeneration));

        SetPrivateField(reference, "enemyId", "external_minion");
        SetPrivateField(summon, "allowRecursiveSummon", true);
        EnemyDefinitionValidationResult invalid =
            EnemyDefinitionValidator.Validate(enemy);
        Assert.That(
            HasCode(invalid, "ability.recursive_summon_cap_missing"),
            Is.True,
            BuildFailureMessage(invalid));

        SetPrivateField(summon, "maximumActive", 12);
        EnemyDefinitionValidationResult valid =
            EnemyDefinitionValidator.Validate(enemy);
        Assert.That(valid.ErrorCount, Is.Zero, BuildFailureMessage(valid));
    }

    [Test]
    public void BossPhases_RequireUniqueCompleteHealthCoverage()
    {
        EnemySO enemy = CreateEnemy("boss_phase_enemy");
        SetPrivateField(enemy, "grade", EEnemyGrade.Boss);
        SetPrivateField(enemy, "rosterTier", EnemyRosterTier.Boss);
        SetPrivateField(enemy, "encounterOnly", true);
        EnemyAbilityDefinition p1 = CreateSpecializedAbility(
            "phase_one",
            EnemyAbilityOperationType.SummonEnemy);
        ConfigureExternalSummon(enemy, p1.Operations[0].Summon);
        EnemyAbilityDefinition p2 = CreateSpecializedAbility(
            "phase_two",
            EnemyAbilityOperationType.ModifyCoreRecovery);
        EnemyAbilityDefinition p3 = CreateSpecializedAbility(
            "phase_three",
            EnemyAbilityOperationType.ChargeCoreAttack);
        SetPrivateField(p3.Charge, "enabled", true);
        SetPrivateField(p3.Charge, "duration", 5f);
        SetPrivateField(
            enemy,
            "abilities",
            new List<EnemyAbilityDefinition> { p1, p2, p3 });

        EnemyBossPhaseDefinition phase1 = CreatePhase(
            "P1", 66, 100, "phase_one");
        EnemyBossPhaseDefinition phase2 = CreatePhase(
            "P2", 31, 65, "phase_two");
        EnemyBossPhaseDefinition phase3 = CreatePhase(
            "P3", 0, 30, "phase_three");
        SetPrivateField(
            enemy,
            "phaseDefinitions",
            new List<EnemyBossPhaseDefinition>
            {
                phase1,
                phase2,
                phase3
            });

        EnemyDefinitionValidationResult valid =
            EnemyDefinitionValidator.Validate(enemy);
        Assert.That(valid.ErrorCount, Is.Zero, BuildFailureMessage(valid));

        SetPrivateField(phase2, "minimumHealthPercent", 30);
        EnemyDefinitionValidationResult overlap =
            EnemyDefinitionValidator.Validate(enemy);
        Assert.That(
            HasCode(overlap, "enemy.boss_phase_coverage_overlap"),
            Is.True,
            BuildFailureMessage(overlap));
    }

    [Test]
    public void SerializedEditorContract_ExposesRosterAndAbilityFields()
    {
        EnemySO enemy = CreateEnemy("serialized_contract_enemy");
        EnemyAbilityDefinition ability = CreateSpecializedAbility(
            "serialized_contract_ability",
            EnemyAbilityOperationType.ApplyCoreEffect);
        SetPrivateField(
            enemy,
            "abilities",
            new List<EnemyAbilityDefinition> { ability });
        SerializedObject serialized = new(enemy);

        Assert.That(serialized.FindProperty("rosterTier"), Is.Not.Null);
        Assert.That(serialized.FindProperty("roleTags"), Is.Not.Null);
        Assert.That(serialized.FindProperty("counterTags"), Is.Not.Null);
        Assert.That(
            serialized.FindProperty("recommendedMaxPerWave"),
            Is.Not.Null);
        Assert.That(serialized.FindProperty("spawnBudget"), Is.Not.Null);
        Assert.That(serialized.FindProperty("encounterOnly"), Is.Not.Null);
        Assert.That(
            serialized.FindProperty("preciseCoreAttackDamage"),
            Is.Not.Null);
        Assert.That(
            serialized.FindProperty("phaseDefinitions"),
            Is.Not.Null);

        SerializedProperty abilityProperty = serialized.FindProperty(
            "abilities.Array.data[0]");
        Assert.That(abilityProperty, Is.Not.Null);
        Assert.That(
            abilityProperty.FindPropertyRelative("abilityTypeId"),
            Is.Not.Null);
        Assert.That(
            abilityProperty.FindPropertyRelative("parameters"),
            Is.Not.Null);
        Assert.That(
            abilityProperty.FindPropertyRelative("triggerEvents"),
            Is.Not.Null);
        Assert.That(
            abilityProperty.FindPropertyRelative("cooldownOverrides"),
            Is.Not.Null);
        Assert.That(
            abilityProperty.FindPropertyRelative("charge"),
            Is.Not.Null);
        Assert.That(
            abilityProperty.FindPropertyRelative("telegraph"),
            Is.Not.Null);
    }

    [Test]
    public void ExistingTriggerAndOperationOrdinals_RemainStable()
    {
        Assert.That((int)EnemyAbilityTrigger.OnSpawn, Is.Zero);
        Assert.That(
            (int)EnemyAbilityTrigger.OnTargetPriorityEvaluation,
            Is.EqualTo(6));
        Assert.That(
            (int)EnemyAbilityTrigger.AlwaysWhileActive,
            Is.EqualTo(7));

        Assert.That((int)EnemyAbilityOperationType.ExecuteEffects, Is.Zero);
        Assert.That(
            (int)EnemyAbilityOperationType.ModifyTargetPriority,
            Is.EqualTo(6));
        Assert.That(
            (int)EnemyAbilityOperationType.ModifyCoreAttackDamage,
            Is.EqualTo(7));
    }

    [Test]
    public void DisabledSpecializedOperation_IsNotExecutable()
    {
        EnemyAbilityDefinition ability = CreateSpecializedAbility(
            "disabled_specialized",
            EnemyAbilityOperationType.SummonEnemy);
        SetPrivateField(ability.Operations[0], "enabled", false);

        Assert.That(ability.HasExecutableContent, Is.False);
        Assert.That(ability.HasUnifiedEffects, Is.False);
        Assert.That(ability.BattleEffects, Is.Empty);
    }

    [Test]
    public void RepeatedDamageSourceCondition_UsesTypedHistoryWindow()
    {
        EnemySO enemy = CreateEnemy("repeated_source_enemy");
        EnemyAbilityDefinition ability = CreateSpecializedAbility(
            "repeated_source_guard",
            EnemyAbilityOperationType.ModifyIncomingDamage);
        SetPrivateField(
            ability,
            "trigger",
            EnemyAbilityTrigger.BeforeSelfDamage);
        EnemyAbilityConditionDefinition condition = new();
        SetPrivateField(
            condition,
            "type",
            EnemyAbilityConditionType.RepeatedDamageSource);
        SetPrivateField(condition, "windowDuration", 2f);
        SetPrivateField(
            ability,
            "conditions",
            new List<EnemyAbilityConditionDefinition> { condition });
        SetPrivateField(
            enemy,
            "abilities",
            new List<EnemyAbilityDefinition> { ability });

        float observedWindow = 0f;
        bool matches = EnemyAbilityConditionEvaluator.MatchesSourceOnly(
            ability,
            new EnemyRuntime(enemy),
            false,
            window =>
            {
                observedWindow = window;
                return true;
            });

        Assert.That(matches, Is.True);
        Assert.That(observedWindow, Is.EqualTo(2f));
        EnemyDefinitionValidationResult result =
            EnemyDefinitionValidator.Validate(enemy);
        Assert.That(result.ErrorCount, Is.Zero, BuildFailureMessage(result));
    }

    private EnemySO CreateEnemy(
        string enemyId,
        bool currentRoster = true)
    {
        EnemySO enemy = ScriptableObject.CreateInstance<EnemySO>();
        _createdObjects.Add(enemy);
        SetPrivateField(enemy, "enemyId", enemyId);
        SetPrivateField(enemy, "displayName", enemyId);
        SetPrivateField(
            enemy,
            "combatStatSchemaVersion",
            EnemySO.CurrentCombatStatSchemaVersion);
        SetPrivateField(
            enemy,
            "rosterSchemaVersion",
            currentRoster ? EnemySO.CurrentRosterSchemaVersion : 0);
        SetPrivateField(
            enemy,
            "roleTags",
            currentRoster
                ? new List<string> { "test_role" }
                : new List<string>());
        return enemy;
    }

    private static EnemyAbilityDefinition CreateSpecializedAbility(
        string abilityId,
        EnemyAbilityOperationType operationType)
    {
        EnemyAbilityTargetDefinition target = new();
        SetPrivateField(
            target,
            "faction",
            EnemyAbilityTargetFaction.Self);
        SetPrivateField(
            target,
            "subject",
            EnemyAbilityTargetSubject.Self);
        EnemyAbilityOperationDefinition operation = new();
        SetPrivateField(operation, "type", operationType);
        SetPrivateField(operation, "enabled", true);
        SetPrivateField(operation, "multiplier", 1f);
        EnemyAbilityDefinition ability = new();
        SetPrivateField(ability, "abilityId", abilityId);
        SetPrivateField(ability, "abilityTypeId", abilityId);
        SetPrivateField(ability, "fallbackName", abilityId);
        SetPrivateField(
            ability,
            "trigger",
            EnemyAbilityTrigger.AlwaysWhileActive);
        SetPrivateField(ability, "target", target);
        SetPrivateField(
            ability,
            "operations",
            new List<EnemyAbilityOperationDefinition> { operation });
        return ability;
    }

    private static EnemyBossPhaseDefinition CreatePhase(
        string phaseId,
        int minimum,
        int maximum,
        string abilityId)
    {
        EnemyBossPhaseDefinition phase = new();
        SetPrivateField(phase, "phaseId", phaseId);
        SetPrivateField(phase, "fallbackName", phaseId);
        SetPrivateField(phase, "minimumHealthPercent", minimum);
        SetPrivateField(phase, "maximumHealthPercent", maximum);
        SetPrivateField(
            phase,
            "abilityIds",
            new List<string> { abilityId });
        return phase;
    }

    private static void ConfigureExternalSummon(
        EnemySO enemy,
        EnemySummonDefinition summon)
    {
        EnemyReferenceDefinition reference = new();
        SetPrivateField(reference, "enemyId", $"{enemy.EnemyId}_minion");
        SetPrivateField(
            summon,
            "candidates",
            new List<EnemyReferenceDefinition> { reference });
        SetPrivateField(summon, "allowRecursiveSummon", false);
    }

    private static bool HasCode(
        EnemyDefinitionValidationResult result,
        string code)
    {
        return result.Diagnostics.Any(
            diagnostic => diagnostic.Code == code);
    }

    private static string BuildFailureMessage(
        EnemyDefinitionValidationResult result)
    {
        return string.Join("\n", result.Diagnostics.Select(
            diagnostic => diagnostic.ToString()));
    }
}
