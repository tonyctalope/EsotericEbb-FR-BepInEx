using System.Reflection;
using HarmonyLib;

namespace EsotericEbbFrench;

internal static class LocalizationPatchLog
{
    private static int _hits;

    public static void LogHit(string method, string id)
    {
        _hits++;
        if (_hits <= 12)
        {
            Plugin.Logger.LogInfo($"Localized {method}('{id}') via EsotericEbbFrench.");
        }
    }
}

[HarmonyPatch]
internal static class CheckLanguagePatch
{
    private static MethodBase? TargetMethod()
    {
        return RuntimeTypeResolver.Method("LocalizationManager", "CheckLanguage", typeof(string));
    }

    private static void Postfix(string __0, ref string __result)
    {
        if (TranslationCatalog.TryGetLine(__0, out string text))
        {
            __result = text;
            LocalizationPatchLog.LogHit(nameof(CheckLanguagePatch), __0);
        }
    }
}

[HarmonyPatch]
internal static class CheckDialogLanguagePatch
{
    private static MethodBase? TargetMethod()
    {
        return RuntimeTypeResolver.Method("LocalizationManager", "CheckDialogLanguage", typeof(string));
    }

    private static void Postfix(string __0, ref string __result)
    {
        if (TranslationCatalog.TryGetDialogLine(__0, out string text))
        {
            __result = text;
            LocalizationPatchLog.LogHit(nameof(CheckDialogLanguagePatch), __0);
        }
    }
}
