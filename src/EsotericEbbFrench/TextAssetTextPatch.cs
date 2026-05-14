using HarmonyLib;
using UnityEngine;

namespace EsotericEbbFrench;

[HarmonyPatch(typeof(TextAsset), nameof(TextAsset.text), MethodType.Getter)]
internal static class TextAssetTextPatch
{
    private static bool Prefix(TextAsset __instance, ref string __result)
    {
        if (__instance != null && TranslationCatalog.TryGetText(__instance.name, out string text))
        {
            __result = text;
            return false;
        }

        return true;
    }
}
