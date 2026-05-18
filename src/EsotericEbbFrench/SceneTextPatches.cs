using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace EsotericEbbFrench;

internal static class SceneTextReplacer
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int _scans;
    private static int _scheduledScans;
    private static DateTime _nextScheduledScanUtc = DateTime.MinValue;

    public static void ReplaceExistingTexts(string reason)
    {
        if (!RuntimeTranslationState.DynamicTextEnabled)
        {
            return;
        }

        _scans++;

        try
        {
            List<string> samples = new();
            List<string> activeUnmatched = new();
            (int tmpScanned, int tmpActive, int tmpReplaced) = ReplaceExistingTextsOfType("TMPro.TMP_Text", samples, activeUnmatched);
            (int uiScanned, int uiActive, int uiReplaced) = ReplaceExistingTextsOfType("UnityEngine.UI.Text", samples, activeUnmatched);
            int scanned = tmpScanned + uiScanned;
            int active = tmpActive + uiActive;
            int replaced = tmpReplaced + uiReplaced;
            if (replaced > 0 || _scans <= 3)
            {
                string sampleText = samples.Count == 0 ? string.Empty : $" Samples: {string.Join(" | ", samples)}";
                string unmatchedText = activeUnmatched.Count == 0 ? string.Empty : $" Active unmatched: {string.Join(" | ", activeUnmatched)}";
                Plugin.Logger.LogInfo($"Scene text scan after {reason}: scanned {scanned}, active {active}, replaced {replaced} texts.{sampleText}{unmatchedText}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"Scene text scan after {reason} failed: {ex.Message}");
        }
    }

    public static void ReplaceExistingTextsThrottled(string reason)
    {
        if (!RuntimeTranslationState.DynamicTextEnabled)
        {
            return;
        }

        if (_scheduledScans >= 20)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        if (now < _nextScheduledScanUtc)
        {
            return;
        }

        _scheduledScans++;
        _nextScheduledScanUtc = now.AddSeconds(1);
        ReplaceExistingTexts($"{reason} scheduled scan {_scheduledScans}");
    }

    private static (int Scanned, int Active, int Replaced) ReplaceExistingTextsOfType(string typeName, List<string> samples, List<string> activeUnmatched)
    {
        Type? type = RuntimeTypeResolver.FindType(typeName);
        if (type == null)
        {
            return (0, 0, 0);
        }

        PropertyInfo? textProperty = type.GetProperty("text", MemberFlags);
        if (textProperty == null || textProperty.SetMethod == null)
        {
            return (0, 0, 0);
        }

        int scanned = 0;
        int active = 0;
        int casted = 0;
        int replaced = 0;
        foreach (object unityObject in FindObjectsOfTypeAll(type))
        {
            scanned++;
            object? component = CastIl2CppObject(unityObject, type);
            if (component == null)
            {
                continue;
            }

            casted++;
            string? value = textProperty.GetValue(component) as string;
            bool isActive = IsActiveInHierarchy(component);
            if (isActive && !string.IsNullOrWhiteSpace(value))
            {
                active++;
            }

            if (_scans <= 3 && samples.Count < 10 && !string.IsNullOrWhiteSpace(value))
            {
                samples.Add(Shorten(value));
            }

            if (!TranslationCatalog.TryGetRuntimeText(value, out string replacement))
            {
                if (_scans <= 3 && isActive && activeUnmatched.Count < 20 && !string.IsNullOrWhiteSpace(value))
                {
                    activeUnmatched.Add(Shorten(value));
                }

                continue;
            }

            textProperty.SetValue(component, replacement);
            TextReplacementPatchLog.Log(typeName, value!, replacement);
            replaced++;
        }

        if (_scans <= 3)
        {
            Plugin.Logger.LogInfo($"Scene text scan details for {typeName}: raw {scanned}, casted {casted}, active {active}, replaced {replaced}.");
        }

        return (scanned, active, replaced);
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

    private static object? CastIl2CppObject(object instance, Type targetType)
    {
        if (targetType.IsInstanceOfType(instance))
        {
            return instance;
        }

        Type? baseType = RuntimeTypeResolver.FindType("Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase");
        if (baseType == null || !baseType.IsInstanceOfType(instance))
        {
            return null;
        }

        MethodInfo? tryCast = baseType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method => method.Name == "TryCast" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0);

        try
        {
            return tryCast?.MakeGenericMethod(targetType).Invoke(instance, Array.Empty<object>());
        }
        catch
        {
            return null;
        }
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

    private static bool IsActiveInHierarchy(object component)
    {
        try
        {
            object? gameObject = component.GetType().GetProperty("gameObject", MemberFlags)?.GetValue(component);
            object? active = gameObject?.GetType().GetProperty("activeInHierarchy", MemberFlags)?.GetValue(gameObject);
            return active is bool value && value;
        }
        catch
        {
            return false;
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
        if (RuntimeTranslationState.DynamicTextEnabled)
        {
            Plugin.Logger.LogInfo("Skipped CanvasManager.Start scene scan while still in menu-safe mode.");
        }
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
        if (RuntimeTranslationState.DynamicTextEnabled)
        {
            Plugin.Logger.LogInfo("Skipped CanvasManager.GetUI scene scan while still in menu-safe mode.");
        }
    }
}

[HarmonyPatch]
internal static class CanvasManagerLateUpdatePatch
{
    private static MethodBase? TargetMethod()
    {
        return RuntimeTypeResolver.Method("CanvasManager", "LateUpdate");
    }

    private static void Postfix()
    {
        return;
    }
}

[HarmonyPatch]
internal static class MenuControllerTextPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string methodName in new[]
        {
            "Start",
            "Update",
            "ReturnToMenu",
            "UpdateText",
            "UpdateCreation",
            "PCBackgroundRefresh",
            "PCBackgroundChanged",
            "Options",
            "Credits"
        })
        {
            MethodBase? method = RuntimeTypeResolver.Method("MenuController", methodName);
            if (method != null)
            {
                yield return method;
            }
        }
    }

    private static void Postfix(MethodBase __originalMethod)
    {
        SceneTextReplacer.ReplaceExistingTextsThrottled($"MenuController.{__originalMethod.Name}");
    }
}

[HarmonyPatch]
internal static class DynamicTextDisablePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach ((string typeName, string methodName) in new[]
        {
            ("MenuController", "LoadGame"),
            ("MenuController", "OnContinueButtonClicked"),
            ("MenuController", "FinishMessage"),
            ("MenuController", "CheatLoad"),
            ("MenuController", "LoadTable"),
            ("SaveManager", "LoadGameFromFile"),
            ("SaveManager", "LoadLocation"),
            ("SaveManager", "LoadLocationWithoutLoadingScreen")
        })
        {
            MethodBase? method = RuntimeTypeResolver.Method(typeName, methodName);
            if (method != null)
            {
                yield return method;
            }
        }
    }

    private static void Prefix(MethodBase __originalMethod)
    {
        RuntimeTranslationState.DisableDynamicText($"{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}");
    }
}
