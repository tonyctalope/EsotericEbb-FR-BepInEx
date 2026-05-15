using System.Reflection;
using HarmonyLib;

namespace EsotericEbbFrench;

[HarmonyPatch]
internal static class ParseCsvPatch
{
    private static MethodBase? TargetMethod()
    {
        return RuntimeTypeResolver.Method("LocalizationManager", "ParseCSV", typeof(string));
    }

    private static void Prefix(ref string __0)
    {
        if (!TranslationCatalog.TryGetReplacementCsv(__0, out string replacement, out string assetName))
        {
            return;
        }

        __0 = replacement;
        Plugin.Logger.LogInfo($"Replaced LocalizationManager.ParseCSV input for {assetName}.");
    }
}
