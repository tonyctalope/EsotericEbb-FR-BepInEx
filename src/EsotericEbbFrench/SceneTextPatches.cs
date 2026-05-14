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
            int replaced = ReplaceExistingTexts("TMPro.TMP_Text") + ReplaceExistingTexts("UnityEngine.UI.Text");
            if (replaced > 0 || _scans <= 3)
            {
                Plugin.Logger.LogInfo($"Scene text scan after {reason}: replaced {replaced} texts.");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"Scene text scan after {reason} failed: {ex.Message}");
        }
    }

    private static int ReplaceExistingTexts(string typeName)
    {
        Type? type = AccessTools.TypeByName(typeName);
        if (type == null)
        {
            return 0;
        }

        PropertyInfo? textProperty = type.GetProperty("text", MemberFlags);
        if (textProperty == null || textProperty.SetMethod == null)
        {
            return 0;
        }

        int replaced = 0;
        foreach (UnityEngine.Object component in Resources.FindObjectsOfTypeAll(type))
        {
            string? value = textProperty.GetValue(component) as string;
            if (!TranslationCatalog.TryGetReverseText(value, out string replacement))
            {
                continue;
            }

            textProperty.SetValue(component, replacement);
            TextReplacementPatchLog.Log(typeName, value!, replacement);
            replaced++;
        }

        return replaced;
    }
}

[HarmonyPatch]
internal static class CanvasManagerStartPatch
{
    private static MethodBase? TargetMethod()
    {
        return AccessTools.Method("CanvasManager:Start");
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
        return AccessTools.Method("CanvasManager:GetUI");
    }

    private static void Postfix()
    {
        SceneTextReplacer.ReplaceExistingTexts("CanvasManager.GetUI");
    }
}
