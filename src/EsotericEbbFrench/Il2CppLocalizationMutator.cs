using System.Reflection;

namespace EsotericEbbFrench;

internal static class Il2CppLocalizationMutator
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static int MutateLocalizedTexts(object localizationManager)
    {
        object? localizedTexts = GetMemberValue(localizationManager, "LocalizedTexts");
        return MutateLocalizedTextList(localizedTexts, dialog: false);
    }

    public static int MutateDialogDatabase(object localizationManager)
    {
        object? dialogDatabase = GetMemberValue(localizationManager, "dialogDatabase");
        object? groups = GetMemberValue(dialogDatabase, "groups");

        int mutated = 0;
        int groupCount = GetCount(groups);
        for (int i = 0; i < groupCount; i++)
        {
            object? group = GetItem(groups, i);
            object? lines = GetMemberValue(group, "lines");
            mutated += MutateLocalizedTextList(lines, dialog: true);
        }

        return mutated;
    }

    private static int MutateLocalizedTextList(object? list, bool dialog)
    {
        int mutated = 0;
        int count = GetCount(list);
        for (int i = 0; i < count; i++)
        {
            object? localizedText = GetItem(list, i);
            if (MutateLocalizedText(localizedText, dialog))
            {
                mutated++;
            }
        }

        return mutated;
    }

    private static bool MutateLocalizedText(object? localizedText, bool dialog)
    {
        string id = GetStringMember(localizedText, "ID");
        if (id.Length == 0)
        {
            return false;
        }

        string text;
        bool found = dialog
            ? TranslationCatalog.TryGetDialogLine(id, out text)
            : TranslationCatalog.TryGetLine(id, out text);

        if (!found)
        {
            return false;
        }

        object? texts = GetMemberValue(localizedText, "texts");
        return SetAllStringSlots(texts, text);
    }

    private static bool SetAllStringSlots(object? list, string value)
    {
        if (list == null)
        {
            return false;
        }

        Type type = list.GetType();
        int count = GetCount(list);
        MethodInfo? setter = GetMethod(type, "set_Item", typeof(int), typeof(string));

        if (count == 0)
        {
            MethodInfo? add = GetMethod(type, "Add", typeof(string));
            add?.Invoke(list, new object[] { value });
            return add != null;
        }

        if (setter == null)
        {
            PropertyInfo? item = type.GetProperty("Item", MemberFlags);
            setter = item?.SetMethod;
        }

        if (setter == null)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            setter.Invoke(list, new object[] { i, value });
        }

        return true;
    }

    private static object? GetMemberValue(object? instance, string name)
    {
        if (instance == null)
        {
            return null;
        }

        Type type = instance.GetType();
        PropertyInfo? property = type.GetProperty(name, MemberFlags);
        if (property != null)
        {
            return property.GetValue(instance);
        }

        FieldInfo? field = type.GetField(name, MemberFlags);
        return field?.GetValue(instance);
    }

    private static string GetStringMember(object? instance, string name)
    {
        object? value = GetMemberValue(instance, name);
        return value as string ?? value?.ToString() ?? string.Empty;
    }

    private static int GetCount(object? list)
    {
        if (list == null)
        {
            return 0;
        }

        Type type = list.GetType();
        object? count = type.GetProperty("Count", MemberFlags)?.GetValue(list)
            ?? GetMethod(type, "get_Count")?.Invoke(list, Array.Empty<object>());

        return count is int value ? value : 0;
    }

    private static object? GetItem(object? list, int index)
    {
        if (list == null)
        {
            return null;
        }

        Type type = list.GetType();
        MethodInfo? getter = GetMethod(type, "get_Item", typeof(int));
        if (getter != null)
        {
            return getter.Invoke(list, new object[] { index });
        }

        PropertyInfo? item = type.GetProperty("Item", MemberFlags);
        return item?.GetValue(list, new object[] { index });
    }

    private static MethodInfo? GetMethod(Type type, string name, params Type[] parameterTypes)
    {
        if (parameterTypes.Length == 0)
        {
            return type.GetMethod(name, MemberFlags);
        }

        return type.GetMethod(name, MemberFlags, binder: null, types: parameterTypes, modifiers: null);
    }
}
