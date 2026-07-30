using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ItemDefinitionTests
{
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
}
