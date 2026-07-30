using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RecruitRuntimeTests
{
    private const string InventoryPlayerPrefsKey =
        "Inventory.Collection.v1";

    [Test]
    public void PaymentRoute_PrefersOwnedAndAffordableResource()
    {
        RecruitTicketItemSO ticket =
            ScriptableObject.CreateInstance<RecruitTicketItemSO>();
        CurrencyItemSO credit =
            ScriptableObject.CreateInstance<CurrencyItemSO>();
        try
        {
            ConfigureItem(ticket, "test.ticket");
            ConfigureItem(credit, "test.credit");

            RecruitPaymentRouteDefinition ticketRoute =
                CreateRoute(ticket, 1L, 1);
            RecruitPaymentRouteDefinition creditRoute =
                CreateRoute(credit, 500L, 2);
            RecruitBannerPageDefinition banner =
                CreateBanner(
                    ticketRoute,
                    creditRoute);

            InventoryData ticketInventory = CreateInventory(
                ("test.ticket", 1L),
                ("test.credit", 1000L));
            RecruitPaymentRouteSelection ticketSelection =
                banner.ResolvePaymentRoute(1, ticketInventory);
            Assert.That(ticketSelection.Item, Is.SameAs(ticket));

            InventoryData creditInventory = CreateInventory(
                ("test.ticket", 0L),
                ("test.credit", 1000L));
            RecruitPaymentRouteSelection creditSelection =
                banner.ResolvePaymentRoute(1, creditInventory);
            Assert.That(creditSelection.Item, Is.SameAs(credit));

            InventoryData tenInventory = CreateInventory(
                ("test.ticket", 5L),
                ("test.credit", 5000L));
            RecruitPaymentRouteSelection tenSelection =
                banner.ResolvePaymentRoute(10, tenInventory);
            Assert.That(tenSelection.Item, Is.SameAs(credit));
        }
        finally
        {
            Object.DestroyImmediate(ticket);
            Object.DestroyImmediate(credit);
        }
    }

    [Test]
    public void RecruitExecution_SpendsCurrencyAndReturnsResult()
    {
        bool hadSave = PlayerPrefs.HasKey(
            InventoryPlayerPrefsKey);
        string previousSave = hadSave
            ? PlayerPrefs.GetString(InventoryPlayerPrefsKey)
            : string.Empty;

        try
        {
            CurrencyItemSO credit =
                Resources.Load<CurrencyItemSO>(
                    "Items/Currency/FreeCredit");
            Assert.That(credit, Is.Not.Null);

            RecruitBannerPageDefinition banner =
                CreateBanner(CreateRoute(credit, 500L, 0));
            InventoryData inventory = CreateInventory(
                (credit.ItemId, 1000L));

            bool succeeded = banner.TryRecruit(
                inventory,
                1,
                true,
                out RecruitExecutionResult result,
                out string error);

            Assert.That(succeeded, Is.True, error);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.DrawCount, Is.EqualTo(1));
            Assert.That(
                inventory.GetAmount(credit),
                Is.EqualTo(500L));
        }
        finally
        {
            if (hadSave)
            {
                PlayerPrefs.SetString(
                    InventoryPlayerPrefsKey,
                    previousSave);
            }
            else
            {
                PlayerPrefs.DeleteKey(
                    InventoryPlayerPrefsKey);
            }
            PlayerPrefs.Save();
        }
    }

    private static RecruitBannerPageDefinition CreateBanner(
        params RecruitPaymentRouteDefinition[] routes)
    {
        RecruitBannerPageDefinition banner = new();
        SetPrivateField(
            banner,
            "paymentRoutes",
            new List<RecruitPaymentRouteDefinition>(routes));
        SetPrivateField(
            banner,
            "dummyPool",
            new List<RecruitDummyPoolEntry>
            {
                new(),
            });
        return banner;
    }

    private static RecruitPaymentRouteDefinition CreateRoute(
        ItemDefinitionSO item,
        long singleCost,
        int priority)
    {
        RecruitPaymentRouteDefinition route = new();
        SetPrivateField(route, "item", item);
        SetPrivateField(route, "singleCost", singleCost);
        SetPrivateField(route, "priority", priority);
        return route;
    }

    private static InventoryData CreateInventory(
        params (string itemId, long amount)[] entries)
    {
        InventoryData inventory = new();
        string json = "{\"version\":1,\"entries\":[";
        for (int index = 0; index < entries.Length; index++)
        {
            if (index > 0)
                json += ",";
            json +=
                $"{{\"itemId\":\"{entries[index].itemId}\"," +
                $"\"amount\":{entries[index].amount}}}";
        }
        json += "]}";
        inventory.ImportJson(json);
        return inventory;
    }

    private static void ConfigureItem(
        ItemDefinitionSO item,
        string itemId)
    {
        SerializedObject serialized = new(item);
        serialized.FindProperty("itemId").stringValue = itemId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(
            field,
            Is.Not.Null,
            $"Field '{fieldName}' was not found.");
        field.SetValue(target, value);
    }
}
