using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace EsotericEbbFrench;

internal static class SceneTextReplacer
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int _scans;

    public static void ReplaceExistingTexts(string reason)
    {
        _scans++;

        try
        {
            List<string> samples = new();
            (int tmpScanned, int tmpReplaced) = ReplaceExistingTextsOfType("TMPro.TMP_Text", samples);
            (int uiScanned, int uiReplaced) = ReplaceExistingTextsOfType("UnityEngine.UI.Text", samples);
            int scanned = tmpScanned + uiScanned;
            int replaced = tmpReplaced + uiReplaced;
            if (replaced > 0 || _scans <= 3)
            {
                string sampleText = samples.Count == 0 ? string.Empty : $" Samples: {string.Join(" | ", samples)}";
                Plugin.Logger.LogInfo($"Scene text scan after {reason}: scanned {scanned}, replaced {replaced} texts.{sampleText}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"Scene text scan after {reason} failed: {ex.Message}");
        }
    }

    private static (int Scanned, int Replaced) ReplaceExistingTextsOfType(string typeName, List<string> samples)
    {
        Type? type = RuntimeTypeResolver.FindType(typeName);
        if (type == null)
        {
            return (0, 0);
        }

        PropertyInfo? textProperty = type.GetProperty("text", MemberFlags);
        if (textProperty == null || textProperty.SetMethod == null)
        {
            return (0, 0);
        }

        int scanned = 0;
        int replaced = 0;
        foreach (object component in FindObjectsOfTypeAll(type))
        {
            scanned++;
            string? value = textProperty.GetValue(component) as string;
            if (_scans <= 3 && samples.Count < 10 && !string.IsNullOrWhiteSpace(value))
            {
                samples.Add(Shorten(value));
            }

            if (!TranslationCatalog.TryGetReverseText(value, out string replacement))
            {
                continue;
            }

            textProperty.SetValue(component, replacement);
            TextReplacementPatchLog.Log(typeName, value!, replacement);
            replaced++;
        }

        return (scanned, replaced);
    }

    private static IEnumerable<object> FindObjectsOfTypeAll(Type type)
    {
        object? il2CppType = ConvertToIl2CppType(type);
        MethodInfo? method = FindIl2CppObjectScanMethod();

        if (il2CppType == null || method == null)
        {
            yield break;
        }

        object? objects = method.Invoke(null, new[] { il2CppType });
        if (objects is not System.Collections.IEnumerable enumerable)
        {
            yield break;
        }

        foreach (object item in enumerable)
        {
            yield return item;
        }
    }

    private static object? ConvertToIl2CppType(Type type)
    {
        Type? il2CppTypeHelper = RuntimeTypeResolver.FindType("Il2CppInterop.Runtime.Il2CppType");
        MethodInfo? from = il2CppTypeHelper?.GetMethods(BindingFlags.Static | BindingFlags.Public)
            .FirstOrDefault(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return method.Name == "From"
                    && parameters.Length == 2
                    && parameters[0].ParameterType == typeof(Type)
                    && parameters[1].ParameterType == typeof(bool);
            });

        return from?.Invoke(null, new object[] { type, false });
    }

    private static MethodInfo? FindIl2CppObjectScanMethod()
    {
        return typeof(UnityEngine.Object).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return method.Name == "FindObjectsOfTypeAll"
                    && parameters.Length == 1
                    && parameters[0].ParameterType.FullName == "Il2CppSystem.Type";
            });
    }

    private static string Shorten(string value)
    {
        const int limit = 50;
        value = value.Replace("\r", "\\r").Replace("\n", "\\n");
        return value.Length <= limit ? value : value[..limit] + "...";
    }
}

[HarmonyPatch]
internal static class CanvasManagerStartPatch
{
    private static MethodBase? TargetMethod()
    {
        return RuntimeTypeResolver.Method("CanvasManager", "Start");
    }

    private static void Postfix()
    {
        SceneTextReplacer.ReplaceExistingTexts("CanvasManager.Start");
    }
}

[HarmonyPatch]
internal static class CanvasManagerGetUiPatch
{
    private static MethodBase? TargetMethod()
    {
        return RuntimeTypeResolver.Method("CanvasManager", "GetUI");
    }

    private static void Postfix()
    {
        SceneTextReplacer.ReplaceExistingTexts("CanvasManager.GetUI");
    }
}
