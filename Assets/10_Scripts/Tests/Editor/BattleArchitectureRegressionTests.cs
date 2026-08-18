using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BattleArchitectureRegressionTests
{
    [Test]
    public void EnemyCombatStats_AreMigratedAndUsedByRuntime()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:EnemySO",
            new[] { "Assets/06_Runtime/Resources/Enemies" });
        Assert.That(guids, Is.Not.Empty);
        foreach (string guid in guids)
        {
            EnemySO definition = AssetDatabase.LoadAssetAtPath<EnemySO>(
                AssetDatabase.GUIDToAssetPath(guid));
            Assert.That(definition, Is.Not.Null);
            Assert.That(
                definition.AuthoredCombatStatSchemaVersion,
                Is.EqualTo(EnemySO.CurrentCombatStatSchemaVersion));
            Assert.That(definition.AttackPower, Is.GreaterThan(0f));
            Assert.That(
                definition.FormationRadius,
                Is.GreaterThan(0f),
                $"{definition.name} must author a positive formation radius.");
            Assert.That(
                float.IsNaN(definition.FormationRadius) ||
                float.IsInfinity(definition.FormationRadius),
                Is.False,
                $"{definition.name} formation radius must be finite.");
            Assert.That(
                definition.CoreAttackRange,
                Is.GreaterThanOrEqualTo(0f),
                $"{definition.name} core range cannot be negative.");
            Assert.That(
                float.IsNaN(definition.CoreAttackRange) ||
                float.IsInfinity(definition.CoreAttackRange),
                Is.False,
                $"{definition.name} core range must be finite.");

            EnemyRuntime enemy = definition.CreateRuntime();
            Assert.That(
                enemy.FormationRadius,
                Is.EqualTo(definition.FormationRadius));
            Assert.That(
                enemy.CoreAttackRange,
                Is.EqualTo(definition.CoreAttackRange));
            BattleEffectContext context =
                BattleEffectContext.ForEnemyAbility(
                    enemy,
                    null,
                    CharacterTargetFaction.Ally,
                    Array.Empty<EnemyRuntime>(),
                    Array.Empty<IBattleCharacter>());
            Assert.That(context.User.Role, Is.EqualTo(
                BattleAbilityUserRole.Enemy));
            Assert.That(
                context.SourceAttackPower,
                Is.EqualTo(definition.AttackPower));
        }
    }

    [Test]
    public void NonCharacterAbilities_DoNotBorrowSelectedCharacterStats()
    {
        FakeCharacter character = new(attackPower: 37f);
        BattleEffectContext commonCard =
            BattleEffectContext.ForBattleCard(
                character,
                null,
                null,
                CharacterTargetFaction.Enemy,
                Array.Empty<EnemyRuntime>(),
                Array.Empty<IBattleCharacter>(),
                character.CurrentAttackPower,
                usesCharacterUser: false);
        BattleEffectContext characterCard =
            BattleEffectContext.ForBattleCard(
                character,
                null,
                null,
                CharacterTargetFaction.Enemy,
                Array.Empty<EnemyRuntime>(),
                Array.Empty<IBattleCharacter>(),
                character.CurrentAttackPower,
                usesCharacterUser: true);
        BattleEffectContext item = BattleEffectContext.ForBattleItem(
            BattleStatusTarget.FromAlly(character),
            null,
            null,
            CharacterTargetFaction.Ally,
            Array.Empty<EnemyRuntime>(),
            new IBattleCharacter[] { character },
            character.CurrentAttackPower);

        Assert.That(commonCard.User.Role, Is.EqualTo(
            BattleAbilityUserRole.CommonCard));
        Assert.That(commonCard.SourceTarget.IsValid, Is.False);
        Assert.That(commonCard.SourceAttackPower, Is.Zero);
        Assert.That(characterCard.User.Role, Is.EqualTo(
            BattleAbilityUserRole.Character));
        Assert.That(characterCard.SourceAttackPower, Is.EqualTo(37f));
        Assert.That(item.User.Role, Is.EqualTo(
            BattleAbilityUserRole.BattleItem));
        Assert.That(item.SourceTarget.IsValid, Is.False);
        Assert.That(item.SourceAttackPower, Is.Zero);
    }

    [Test]
    public void NeutralCommonCardAssets_DoNotDependOnCharacterUsers()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:BattleCardSO",
            new[] { "Assets/06_Runtime/Resources/Cards" });
        Assert.That(guids, Is.Not.Empty);
        foreach (string guid in guids)
        {
            BattleCardSO card = AssetDatabase.LoadAssetAtPath<BattleCardSO>(
                AssetDatabase.GUIDToAssetPath(guid));
            Assert.That(card, Is.Not.Null);
            if (card.Affiliation != BattleCardAffiliation.Neutral)
                continue;

            Assert.That(
                BattleCardDefinitionValidator.TryValidate(
                    card,
                    out string error),
                Is.True,
                $"{card.name}: {error}");
            foreach (IBattleEffectDefinition effect in card.BattleEffects)
            {
                Assert.That(
                    effect.BattleTargetMode,
                    Is.Not.EqualTo(BattleEffectTargetMode.Source),
                    card.name);
                if (!UsesAmountScaling(effect.BattleEffectType))
                    continue;

                ScalingValue scaling = effect.AmountScaling;
                Assert.That(scaling.SourceAttackPowerScale, Is.Zero,
                    card.name);
                Assert.That(scaling.SourceCurrentHealthScale, Is.Zero,
                    card.name);
                Assert.That(scaling.SourceMaximumHealthScale, Is.Zero,
                    card.name);
                Assert.That(scaling.SourceStatusStacksScale, Is.Zero,
                    card.name);
            }
        }
    }

    [Test]
    public void BattleEffectResult_DamageAdditionSaturates()
    {
        BattleEffectResult combined = new BattleEffectResult(
                true,
                true,
                int.MaxValue)
            .Combine(new BattleEffectResult(true, true, 1));

        Assert.That(combined.DamageDealt, Is.EqualTo(int.MaxValue));
        Assert.That(
            BattleValueMath.SaturatingAddNonNegative(int.MaxValue, 99),
            Is.EqualTo(int.MaxValue));
    }

    [TestCase(CharacterEffectType.Damage)]
    [TestCase(CharacterEffectType.GainResource)]
    [TestCase(CharacterEffectType.Heal)]
    [TestCase(CharacterEffectType.Shield)]
    public void SharedEffectRules_RejectNegativeOnlyAmounts(
        CharacterEffectType type)
    {
        CharacterEffectDefinition effect = new();
        SetField(effect, "type", type);
        SetField(effect, "damageAmountMode", CharacterDamageAmountMode.Fixed);
        SetField(effect, "damageAmount", -1f);

        Assert.That(
            BattleEffectRules.TryValidate(effect, out string error),
            Is.False);
        Assert.That(error, Does.Contain("positive"));
    }

    [Test]
    public void AssetValidation_DoesNotMutateAuthoredData()
    {
        AssertValidationIsPure<BattleCardSO>(asset =>
            BattleCardDefinitionValidator.TryValidate(asset, out _));
        AssertValidationIsPure<DungeonDefinition>(
            asset => asset.TryValidate(out _));
        AssertValidationIsPure<DungeonEventSO>(
            asset => asset.TryValidate(out _));
        AssertValidationIsPure<DungeonRestSO>(
            asset => asset.TryValidate(out _));
    }

    [Test]
    public void RestSchemaMigration_IsExplicitAndIdempotent()
    {
        DungeonRestSO rest = ScriptableObject.CreateInstance<DungeonRestSO>();
        try
        {
            Assert.That(rest.RestSchemaVersion, Is.Zero);
            Assert.That(
                rest.ApplyRestSchemaMigration(rest.Choices),
                Is.True);
            Assert.That(
                rest.RestSchemaVersion,
                Is.EqualTo(DungeonRestSO.CurrentRestSchemaVersion));
            Assert.That(
                rest.ApplyRestSchemaMigration(rest.Choices),
                Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rest);
        }
    }

    private static void AssertValidationIsPure<T>(Func<T, bool> validate)
        where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        Assert.That(guids, Is.Not.Empty);
        T asset = AssetDatabase.LoadAssetAtPath<T>(
            AssetDatabase.GUIDToAssetPath(guids[0]));
        string before = EditorJsonUtility.ToJson(asset);

        validate(asset);

        Assert.That(EditorJsonUtility.ToJson(asset), Is.EqualTo(before));
    }

    private static void SetField<T>(object target, string name, T value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }

    private static bool UsesAmountScaling(BattleEffectType type)
    {
        return type == BattleEffectType.Damage ||
               type == BattleEffectType.GainResource ||
               type == BattleEffectType.SpendResource ||
               type == BattleEffectType.Heal ||
               type == BattleEffectType.SpendHealth ||
               type == BattleEffectType.Shield ||
               type == BattleEffectType.CardDraw;
    }

    private sealed class FakeCharacter : IBattleCharacter
    {
        public int PartySlotIndex => 0;
        public int TotalDamageDealt => 0;
        public int CurrentHealth => 10;
        public int MaximumHealth => 10;
        public int CurrentShield => 0;
        public float DisabledTimeRemaining => 0f;
        public float CurrentAttackPower { get; }
        public float CurrentAttackSpeed => 1f;
        public event Action<BattleStatusChangedEvent> StatusChanged
        {
            add { }
            remove { }
        }

        public FakeCharacter(float attackPower)
        {
            CurrentAttackPower = attackPower;
        }

        public bool HasStatusEffect(StatusEffectSO statusEffect) => false;
        public int GetStatusStackCount(StatusEffectSO statusEffect) => 0;
        public System.Collections.Generic.IReadOnlyList<BattleStatusSnapshot>
            GetActiveStatusEffects() => Array.Empty<BattleStatusSnapshot>();
        public bool TryConsumeStatusStacks(
            StatusEffectSO statusEffect,
            int stackCount) => false;
        public int Heal(int amount) => 0;
        public int GainShield(int amount) => 0;
        public int TakeDamage(int amount) => 0;
        public bool CanSpendHealth(int amount) => false;
        public bool TrySpendHealth(int amount) => false;
        public bool Initialize() => true;
        public void BindBattle(
            IActiveSkillResource activeSkillResource,
            IBattleBoard board) { }
        public void ResetRuntime() { }
        public void TickBattle(float deltaTime, IBattleBoard board) { }
        public void RecordDamageDealt(int damage) { }
        public void DisableFor(float duration) { }
        public bool ApplyStatusEffect(
            StatusEffectSO definition,
            float duration,
            int stacks) => false;
        public bool ApplyStatusEffect(
            StatusEffectSO definition,
            float duration,
            int stacks,
            IBattleCharacter source) => false;
        public int RemoveStatusEffects(
            CharacterStatusRemovalSelection removalSelection,
            CharacterStatusRemovalAmount removalAmount) => 0;
    }
}
