using System.Reflection;
using HarmonyLib;

namespace EsotericEbbFrench;

internal static class RuntimeTextTranslator
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static int _hits;

    public static bool Translate(ref string? value, string context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!TranslationCatalog.TryGetReverseText(value, out string replacement))
        {
            return false;
        }

        string source = value;
        value = replacement;
        _hits++;
        if (_hits <= 80)
        {
            Plugin.Logger.LogInfo($"Translated runtime text via {context}: '{Shorten(source)}' -> '{Shorten(replacement)}'.");
        }

        return true;
    }

    public static void TranslateResult(ref string __result, string context)
    {
        string? value = __result;
        if (Translate(ref value, context))
        {
            __result = value!;
        }
    }

    public static IEnumerable<MethodBase> FindMethodsWithStringParameter(string typeName, params string[] methodNames)
    {
        return FindMethodsWithStringParameter(typeName, stringParameterIndex: null, methodNames);
    }

    public static IEnumerable<MethodBase> FindMethodsWithStringParameterAt(string typeName, int stringParameterIndex, params string[] methodNames)
    {
        return FindMethodsWithStringParameter(typeName, stringParameterIndex, methodNames);
    }

    private static IEnumerable<MethodBase> FindMethodsWithStringParameter(string typeName, int? stringParameterIndex, params string[] methodNames)
    {
        Type? type = RuntimeTypeResolver.FindType(typeName);
        if (type == null)
        {
            yield break;
        }

        HashSet<string> names = new(methodNames, StringComparer.Ordinal);
        foreach (MethodInfo method in type.GetMethods(MemberFlags))
        {
            if (!names.Contains(method.Name))
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            bool matches = stringParameterIndex.HasValue
                ? stringParameterIndex.Value < parameters.Length && parameters[stringParameterIndex.Value].ParameterType == typeof(string)
                : parameters.Any(parameter => parameter.ParameterType == typeof(string));

            if (matches)
            {
                yield return method;
            }
        }
    }

    public static IEnumerable<MethodBase> FindStringReturnMethods(string typeName, params string[] methodNames)
    {
        Type? type = RuntimeTypeResolver.FindType(typeName);
        if (type == null)
        {
            yield break;
        }

        HashSet<string> names = new(methodNames, StringComparer.Ordinal);
        foreach (MethodInfo method in type.GetMethods(MemberFlags))
        {
            if (names.Contains(method.Name) && method.ReturnType == typeof(string))
            {
                yield return method;
            }
        }
    }

    private static string Shorten(string value)
    {
        const int limit = 90;
        value = value.Replace("\r", "\\r").Replace("\n", "\\n");
        return value.Length <= limit ? value : value[..limit] + "...";
    }
}

[HarmonyPatch]
internal static class DialogFirstStringArgumentPatch
{
    private static bool Prepare()
    {
        return TargetMethods().Any();
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in RuntimeTextTranslator.FindMethodsWithStringParameterAt(
            "DialogManager",
            0,
            "AddText",
            "AddChoiceText",
            "AddGlossaryResponse",
            "DelayMessageUntilLongRestisDone",
            "AddDCspeaker"))
        {
            yield return method;
        }

        foreach (MethodBase method in RuntimeTextTranslator.FindMethodsWithStringParameterAt(
            "CanvasManager",
            0,
            "CreateTextNote",
            "HoverBoxToggle"))
        {
            yield return method;
        }

        foreach (MethodBase method in RuntimeTextTranslator.FindMethodsWithStringParameterAt(
            "CornerText",
            0,
            "AddCornerText"))
        {
            yield return method;
        }

        foreach (MethodBase method in RuntimeTextTranslator.FindMethodsWithStringParameterAt(
            "GenericConfirmationBox",
            0,
            "UpdateBox"))
        {
            yield return method;
        }

        foreach (MethodBase method in RuntimeTextTranslator.FindMethodsWithStringParameterAt(
            "Journal",
            0,
            "ClickingOnGlossaryTerm"))
        {
            yield return method;
        }

        foreach (MethodBase method in RuntimeTextTranslator.FindMethodsWithStringParameterAt(
            "CenterText",
            0,
            "DisplayAreaText",
            "DisplayBigMessage",
            "DisplayTimeTextWithDelay"))
        {
            yield return method;
        }

        foreach (MethodBase method in RuntimeTextTranslator.FindMethodsWithStringParameterAt(
            "Febucci.UI.Core.TAnimPlayerBase",
            0,
            "ShowText"))
        {
            yield return method;
        }

        foreach (MethodBase method in RuntimeTextTranslator.FindMethodsWithStringParameterAt(
            "Febucci.UI.TextAnimator",
            0,
            "SetText",
            "AppendText",
            "set_text",
            "_SetText",
            "_ApplyTextToCharacters"))
        {
            yield return method;
        }
    }

    private static void Prefix(ref string __0, MethodBase __originalMethod)
    {
        string? value = __0;
        if (RuntimeTextTranslator.Translate(ref value, $"{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}[0]"))
        {
            __0 = value!;
        }
    }
}

[HarmonyPatch]
internal static class DialogSecondStringArgumentPatch
{
    private static bool Prepare()
    {
        return TargetMethods().Any();
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in RuntimeTextTranslator.FindMethodsWithStringParameterAt(
            "DialogManager",
            1,
            "CheckSpeaker",
            "TagCheck",
            "AddDCspeaker"))
        {
            yield return method;
        }

        foreach (MethodBase method in RuntimeTextTranslator.FindMethodsWithStringParameterAt(
            "CenterText",
            1,
            "DisplayAreaText",
            "DisplayBigMessage"))
        {
            yield return method;
        }
    }

    private static void Prefix(ref string __1, MethodBase __originalMethod)
    {
        string? value = __1;
        if (RuntimeTextTranslator.Translate(ref value, $"{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}[1]"))
        {
            __1 = value!;
        }
    }
}

[HarmonyPatch]
internal static class ThreeStringArgumentPatch
{
    private static bool Prepare()
    {
        return TargetMethods().Any();
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in RuntimeTextTranslator.FindMethodsWithStringParameterAt(
            "CenterText",
            2,
            "DisplayTimeText",
            "DisplayTimeTextInCutscene"))
        {
            yield return method;
        }

        foreach (MethodBase method in RuntimeTextTranslator.FindMethodsWithStringParameterAt(
            "CanvasManager",
            2,
            "SelectInfoToggle"))
        {
            yield return method;
        }
    }

    private static void Prefix(ref string __0, ref string __1, ref string __2, MethodBase __originalMethod)
    {
        string context = $"{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}";
        string? first = __0;
        string? second = __1;
        string? third = __2;
        if (RuntimeTextTranslator.Translate(ref first, $"{context}[0]"))
        {
            __0 = first!;
        }

        if (RuntimeTextTranslator.Translate(ref second, $"{context}[1]"))
        {
            __1 = second!;
        }

        if (RuntimeTextTranslator.Translate(ref third, $"{context}[2]"))
        {
            __2 = third!;
        }
    }
}

[HarmonyPatch]
internal static class DialogStringResultPatch
{
    private static bool Prepare()
    {
        return TargetMethods().Any();
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in RuntimeTextTranslator.FindStringReturnMethods(
            "DialogManager",
            "CleanChoiceText",
            "CheckSpeaker",
            "TagCheck",
            "AddDCspeaker"))
        {
            yield return method;
        }

        foreach (MethodBase method in RuntimeTextTranslator.FindStringReturnMethods(
            "Idea",
            "CalculateModifiers"))
        {
            yield return method;
        }
    }

    private static void Postfix(ref string __result, MethodBase __originalMethod)
    {
        RuntimeTextTranslator.TranslateResult(ref __result, $"{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}:result");
    }
}

[HarmonyPatch]
internal static class FourthStringArgumentPatch
{
    private static bool Prepare()
    {
        return TargetMethods().Any();
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in RuntimeTextTranslator.FindMethodsWithStringParameterAt(
            "CanvasManager",
            3,
            "UpdateClock"))
        {
            yield return method;
        }
    }

    private static void Prefix(ref string __3, MethodBase __originalMethod)
    {
        string? value = __3;
        if (RuntimeTextTranslator.Translate(ref value, $"{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}[3]"))
        {
            __3 = value!;
        }
    }
}
