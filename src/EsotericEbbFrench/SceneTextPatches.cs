using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
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
        Il2CppSystem.Type il2CppType = Il2CppType.From(type, throwOnFailure: false);
        MethodInfo? method = typeof(UnityEngine.Object).GetMethod(
            "FindObjectsOfTypeAll",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(Il2CppSystem.Type) },
            modifiers: null);

        object? objects = method?.Invoke(null, new object[] { il2CppType });
        if (objects is not System.Collections.IEnumerable enumerable)
        {
            yield break;
        }

        foreach (object item in enumerable)
        {
            yield return item;
        }
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
