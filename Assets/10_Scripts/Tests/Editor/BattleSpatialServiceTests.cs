using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleSpatialServiceTests
{
    private sealed class TestCharacter : IBattleCharacter
    {
        public int PartySlotIndex { get; }
        public int TotalDamageDealt { get; private set; }
        public int CurrentHealth { get; private set; } = 100;
        public int MaximumHealth => 100;
        public int CurrentShield { get; private set; }
        public float DisabledTimeRemaining { get; private set; }
        public float CurrentAttackPower => 10f;
        public float CurrentAttackSpeed => 1f;

        public event Action<BattleStatusChangedEvent> StatusChanged
        {
            add { }
            remove { }
        }

        public TestCharacter(int partySlotIndex)
        {
            PartySlotIndex = partySlotIndex;
        }

        public bool HasStatusEffect(StatusEffectSO statusEffect) => false;

        public int GetStatusStackCount(StatusEffectSO statusEffect) => 0;

        public IReadOnlyList<BattleStatusSnapshot> GetActiveStatusEffects()
        {
            return Array.Empty<BattleStatusSnapshot>();
        }

        public bool TryConsumeStatusStacks(
            StatusEffectSO statusEffect,
            int stackCount) => false;

        public int Heal(int amount)
        {
            int applied = Mathf.Min(
                Mathf.Max(0, amount),
                MaximumHealth - CurrentHealth);
            CurrentHealth += applied;
            return applied;
        }

        public int GainShield(int amount)
        {
            int applied = Mathf.Max(0, amount);
            CurrentShield += applied;
            return applied;
        }

        public int TakeDamage(int amount)
        {
            int applied = Mathf.Min(
                CurrentHealth,
                Mathf.Max(0, amount));
            CurrentHealth -= applied;
            return applied;
        }

        public bool CanSpendHealth(int amount)
        {
            return amount > 0 && CurrentHealth - amount >= 1;
        }

        public bool TrySpendHealth(int amount)
        {
            if (!CanSpendHealth(amount))
                return false;
            CurrentHealth -= amount;
            return true;
        }

        public bool Initialize() => true;

        public void BindBattle(
            IActiveSkillResource activeSkillResource,
            IBattleBoard board)
        {
        }

        public void ResetRuntime()
        {
        }

        public void TickBattle(float deltaTime, IBattleBoard board)
        {
        }

        public void RecordDamageDealt(int damage)
        {
            TotalDamageDealt += Mathf.Max(0, damage);
        }

        public void DisableFor(float duration)
        {
            DisabledTimeRemaining = Mathf.Max(
                DisabledTimeRemaining,
                duration);
        }

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

    [Test]
    public void Defaults_KeepDocumentedSpatialValues()
    {
        Assert.That(BattleSpatialDefaults.NearbyRadius, Is.EqualTo(1.5f));
        Assert.That(BattleSpatialDefaults.MovementStep, Is.EqualTo(1f));
        Assert.That(
            BattleSpatialDefaults.InnerZoneRadiusRatio,
            Is.EqualTo(0.5f));
        Assert.That(
            BattleSpatialDefaults.RecentCoreAttackWindow,
            Is.EqualTo(5f));
    }

    [Test]
    public void CircularBoard_InstantMoveAndSwap_UpdateSpatialQueries()
    {
        GameObject gameObject = new(
            "BattleSpatialServiceTests",
            typeof(RectTransform));
        DungeonBoardView board = gameObject.AddComponent<DungeonBoardView>();
        try
        {
            board.ConfigureArena(
                BattleArenaSetup.CreateCircular(worldRadius: 4f));
            TestCharacter first = new(0);
            TestCharacter second = new(1);
            board.SetBattleCharacters(new IBattleCharacter[]
            {
                first,
                second,
            });

            Assert.That(board.IsAvailable, Is.True);
            Assert.That(board.ArenaRadius, Is.EqualTo(3.65f).Within(0.001f));
            Assert.That(
                board.MoveAlliesToPoint(
                    new IBattleCharacter[] { first },
                    new Vector2(-1f, 0f),
                    true),
                Is.EqualTo(1));
            Assert.That(
                board.MoveAlliesToPoint(
                    new IBattleCharacter[] { second },
                    new Vector2(1f, 0f),
                    true),
                Is.EqualTo(1));

            Assert.That(board.TrySwapAllies(first, second), Is.True);
            Assert.That(
                board.TryGetUnitPosition(
                    BattleStatusTarget.FromAlly(first),
                    out Vector2 firstPosition),
                Is.True);
            Assert.That(firstPosition.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                board.TryGetUnitPosition(
                    BattleStatusTarget.FromAlly(second),
                    out Vector2 secondPosition),
                Is.True);
            Assert.That(secondPosition.x, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(
                board.TryGetAllyFacingDirection(
                    first,
                    out Vector2 firstFacingDirection),
                Is.True);
            Assert.That(firstFacingDirection.x, Is.GreaterThan(0f));
            Assert.That(
                board.TryGetAllyFacingDirection(
                    second,
                    out Vector2 secondFacingDirection),
                Is.True);
            Assert.That(secondFacingDirection.x, Is.LessThan(0f));

            Assert.That(
                board.MoveAlliesToPoint(
                    new IBattleCharacter[] { second },
                    new Vector2(10f, 0f),
                    true),
                Is.EqualTo(1));
            Assert.That(
                board.GetUnitZone(BattleStatusTarget.FromAlly(second)),
                Is.EqualTo(BattleSpatialZone.Outer));
            Assert.That(
                board.TryGetAllyFacingDirection(
                    second,
                    out Vector2 latestFacingDirection),
                Is.True);
            Assert.That(latestFacingDirection.x, Is.GreaterThan(0f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }
}
