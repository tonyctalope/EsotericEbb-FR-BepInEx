using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EsotericEbbFrench;

[HarmonyPatch(typeof(Resources), nameof(Resources.Load), typeof(string))]
internal static class ResourcesLoadPatch
{
    private static void Postfix(string path, ref Object __result)
    {
        if (__result == null && TranslationCatalog.TryCreateAsset(path, out Object replacement))
        {
            __result = replacement;
        }
    }
}

[HarmonyPatch(typeof(Resources), nameof(Resources.Load), typeof(string), typeof(Type))]
internal static class ResourcesLoadTypedPatch
{
    private static void Postfix(string path, Type? systemTypeInstance, ref Object __result)
    {
        if (__result != null || !CanReturnTextAsset(systemTypeInstance))
        {
            return;
        }

        if (TranslationCatalog.TryCreateAsset(path, out Object replacement))
        {
            __result = replacement;
        }
    }

    private static bool CanReturnTextAsset(Type? requestedType)
    {
        if (requestedType == null)
        {
            return true;
        }

        return requestedType == typeof(Object)
            || requestedType == typeof(TextAsset)
            || requestedType.IsAssignableFrom(typeof(TextAsset));
    }
}
