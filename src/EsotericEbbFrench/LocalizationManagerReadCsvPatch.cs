using System.Reflection;
using HarmonyLib;

namespace EsotericEbbFrench;

[HarmonyPatch]
internal static class ReadCsvPatch
{
    private static int _runs;

    private static bool Prepare()
    {
        return true;
    }

    private static MethodBase? TargetMethod()
    {
        return RuntimeTypeResolver.Method("LocalizationManager", "ReadCSV");
    }

    private static void Postfix(object __instance)
    {
        _runs++;

        try
        {
            int localized = Il2CppLocalizationMutator.MutateLocalizedTexts(__instance);
            int dialog = Il2CppLocalizationMutator.MutateDialogDatabase(__instance);

            Plugin.Logger.LogInfo($"Applied French localization after LocalizationManager.ReadCSV run {_runs}: {localized} localized texts, {dialog} dialog lines.");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Failed to apply French localization after LocalizationManager.ReadCSV: {ex}");
        }
    }
}
