using NUnit.Framework;

public sealed class BattleObjectiveModifierTests
{
    [Test]
    public void HealingMultiplier_AppliesAndExpires()
    {
        BattleCoreRuntime core = Core(100, 40);

        Assert.That(
            core.TryAddTimedModifier(
                "test.heal.down",
                BattleObjectiveModifierType.HealingReceivedMultiplier,
                0.5f,
                4f),
            Is.True);
        Assert.That(core.Heal(20), Is.EqualTo(10));
        Assert.That(core.CurrentHealth, Is.EqualTo(50));

        core.Tick(4f);

        Assert.That(core.ActiveModifierCount, Is.Zero);
        Assert.That(core.Heal(20), Is.EqualTo(20));
        Assert.That(core.CurrentHealth, Is.EqualTo(70));
    }

    [Test]
    public void MaximumHealthReduction_StacksToCapAndRestoresMaximumOnly()
    {
        BattleCoreRuntime core = Core(100, 100);

        Assert.That(
            core.TryAddTimedModifier(
                "test.max.down",
                BattleObjectiveModifierType.MaximumHealthReduction,
                0.1f,
                6f,
                2),
            Is.True);
        Assert.That(
            core.TryAddTimedModifier(
                "test.max.down",
                BattleObjectiveModifierType.MaximumHealthReduction,
                0.1f,
                6f,
                2),
            Is.True);
        Assert.That(
            core.TryAddTimedModifier(
                "test.max.down",
                BattleObjectiveModifierType.MaximumHealthReduction,
                0.1f,
                6f,
                2),
            Is.False);
        Assert.That(core.MaximumHealth, Is.EqualTo(80));
        Assert.That(core.CurrentHealth, Is.EqualTo(80));
        Assert.That(core.GetModifierStackCount(
            "test.max.down",
            BattleObjectiveModifierType.MaximumHealthReduction),
            Is.EqualTo(2));

        core.Tick(6f);

        Assert.That(core.MaximumHealth, Is.EqualTo(100));
        Assert.That(core.CurrentHealth, Is.EqualTo(80));
    }

    [Test]
    public void IncomingProtection_CanBePartiallyBypassed()
    {
        BattleCoreRuntime core = Core(100, 100);
        Assert.That(
            core.TryAddTimedModifier(
                "test.damage.down",
                BattleObjectiveModifierType.IncomingDamageMultiplier,
                0.5f,
                10f),
            Is.True);

        Assert.That(core.TakeDamage(20, 0f), Is.EqualTo(10));
        Assert.That(core.TakeDamage(20, 0.5f), Is.EqualTo(15));
        Assert.That(core.TryGrantDamageImmunity(3f), Is.True);
        Assert.That(core.TakeDamage(20, 0f), Is.Zero);
        Assert.That(core.TakeDamage(20, 0.25f), Is.Zero);
        Assert.That(core.CurrentHealth, Is.EqualTo(75));
    }

    [Test]
    public void DamageOverTime_StacksTicksAndExpires()
    {
        BattleCoreRuntime core = Core(100, 100);
        for (int index = 0; index < 3; index++)
        {
            Assert.That(
                core.TryApplyDamageOverTime(
                    "test.core.fire",
                    3,
                    1f,
                    4f,
                    3),
                Is.True);
        }
        Assert.That(
            core.TryApplyDamageOverTime(
                "test.core.fire",
                3,
                1f,
                4f,
                3),
            Is.False);
        Assert.That(core.GetDamageOverTimeStackCount(
            "test.core.fire"), Is.EqualTo(3));

        core.Tick(2f);
        Assert.That(core.CurrentHealth, Is.EqualTo(82));
        core.Tick(2f);

        Assert.That(core.CurrentHealth, Is.EqualTo(64));
        Assert.That(core.ActiveDamageOverTimeCount, Is.Zero);
    }

    [Test]
    public void Configure_ClearsEveryTransientObjectiveModifier()
    {
        BattleCoreRuntime core = Core(100, 50);
        core.TryAddTimedModifier(
            "test.heal.down",
            BattleObjectiveModifierType.HealingReceivedMultiplier,
            0.5f,
            4f);
        core.TryApplyDamageOverTime(
            "test.dot",
            2,
            1f,
            4f);

        core.Configure(120, true, 60);

        Assert.That(core.BaseMaximumHealth, Is.EqualTo(120));
        Assert.That(core.MaximumHealth, Is.EqualTo(120));
        Assert.That(core.CurrentHealth, Is.EqualTo(60));
        Assert.That(core.ActiveModifierCount, Is.Zero);
        Assert.That(core.ActiveDamageOverTimeCount, Is.Zero);
    }

    [Test]
    public void ZeroDurationModifier_LastUntilBattleReset()
    {
        BattleCoreRuntime core = Core(100, 100);

        Assert.That(
            core.TryAddTimedModifier(
                "permanent.maximum",
                BattleObjectiveModifierType.MaximumHealthReduction,
                0.2f,
                0f),
            Is.True);
        Assert.That(core.MaximumHealth, Is.EqualTo(80));

        core.Tick(600f);
        Assert.That(core.MaximumHealth, Is.EqualTo(80));
        Assert.That(core.ActiveModifierCount, Is.EqualTo(1));

        core.Configure(100, true, 100);
        Assert.That(core.MaximumHealth, Is.EqualTo(100));
        Assert.That(core.ActiveModifierCount, Is.Zero);
    }

    private static BattleCoreRuntime Core(
        int maximumHealth,
        int currentHealth)
    {
        BattleCoreRuntime core = new();
        core.Configure(maximumHealth, true, currentHealth);
        return core;
    }
}
