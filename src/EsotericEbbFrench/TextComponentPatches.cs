using System.Reflection;
using HarmonyLib;

namespace EsotericEbbFrench;

internal static class TextReplacementPatchLog
{
    private static int _hits;

    public static void Log(string component, string source, string replacement)
    {
        _hits++;
        if (_hits <= 20)
        {
            Plugin.Logger.LogInfo($"Replaced {component} text '{Shorten(source)}' -> '{Shorten(replacement)}'.");
        }
    }

    private static string Shorten(string value)
    {
        const int limit = 80;
        value = value.Replace("\r", "\\r").Replace("\n", "\\n");
        return value.Length <= limit ? value : value[..limit] + "...";
    }
}

[HarmonyPatch]
internal static class TmpTextSetterPatch
{
    private static bool Prepare()
    {
        return RuntimeTypeResolver.PropertySetter("TMPro.TMP_Text", "text") != null;
    }

    private static MethodBase? TargetMethod()
    {
        return RuntimeTypeResolver.PropertySetter("TMPro.TMP_Text", "text");
    }

    private static void Prefix(ref string __0)
    {
        Replace(ref __0, "TMP_Text");
    }

    private static void Replace(ref string value, string component)
    {
        if (TranslationCatalog.TryGetReverseText(value, out string replacement))
        {
            TextReplacementPatchLog.Log(component, value, replacement);
            value = replacement;
        }
    }
}

[HarmonyPatch]
internal static class UnityUiTextSetterPatch
{
    private static bool Prepare()
    {
        return RuntimeTypeResolver.PropertySetter("UnityEngine.UI.Text", "text") != null;
    }

    private static MethodBase? TargetMethod()
    {
        return RuntimeTypeResolver.PropertySetter("UnityEngine.UI.Text", "text");
    }

    private static void Prefix(ref string __0)
    {
        if (TranslationCatalog.TryGetReverseText(__0, out string replacement))
        {
            TextReplacementPatchLog.Log("UnityEngine.UI.Text", __0, replacement);
            __0 = replacement;
        }
    }
}
