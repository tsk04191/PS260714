using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

internal static class TestReflection
{
    internal static void SetField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field = FindField(target, fieldName);
        Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
        field.SetValue(target, value);
    }

    internal static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        SetField(target, fieldName, value);
    }

    internal static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = FindField(target, fieldName);
        Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
        return (T)field.GetValue(target);
    }

    internal static T GetPrivateField<T>(object target, string fieldName)
    {
        return GetField<T>(target, fieldName);
    }

    internal static List<T> GetPrivateList<T>(
        object target,
        string fieldName)
    {
        return GetField<List<T>>(target, fieldName);
    }

    internal static void InvokeMethod(
        object target,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing method: {methodName}");
        method.Invoke(target, arguments);
    }

    private static FieldInfo FindField(object target, string fieldName)
    {
        Assert.That(target, Is.Not.Null);
        for (System.Type type = target.GetType();
             type != null;
             type = type.BaseType)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
                return field;
        }

        return null;
    }
}
