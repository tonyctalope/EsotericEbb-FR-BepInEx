using System.Reflection;
using BepInEx.Logging;

namespace EsotericEbbFrench;

internal static class RuntimeTypeResolver
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static Type? FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    public static MethodBase? Method(string typeName, string methodName, params Type[] parameterTypes)
    {
        Type? type = FindType(typeName);
        if (type == null)
        {
            return null;
        }

        if (parameterTypes.Length == 0)
        {
            return type.GetMethod(methodName, MemberFlags);
        }

        return type.GetMethod(methodName, MemberFlags, binder: null, types: parameterTypes, modifiers: null);
    }

    public static MethodInfo? PropertySetter(string typeName, string propertyName)
    {
        Type? type = FindType(typeName);
        PropertyInfo? property = type?.GetProperty(propertyName, MemberFlags);
        return property?.GetSetMethod(nonPublic: true);
    }

    public static void LogTargetStatus(ManualLogSource logger)
    {
        Log(logger, "LocalizationManager.CheckLanguage", Method("LocalizationManager", "CheckLanguage", typeof(string)));
        Log(logger, "LocalizationManager.CheckDialogLanguage", Method("LocalizationManager", "CheckDialogLanguage", typeof(string)));
        Log(logger, "LocalizationManager.ParseCSV", Method("LocalizationManager", "ParseCSV", typeof(string)));
        Log(logger, "LocalizationManager.ReadCSV", Method("LocalizationManager", "ReadCSV"));
        Log(logger, "CanvasManager.Start", Method("CanvasManager", "Start"));
        Log(logger, "CanvasManager.GetUI", Method("CanvasManager", "GetUI"));
        Log(logger, "CanvasManager.LateUpdate", Method("CanvasManager", "LateUpdate"));
        Log(logger, "MenuController.Start", Method("MenuController", "Start"));
        Log(logger, "MenuController.Update", Method("MenuController", "Update"));
        Log(logger, "TMPro.TMP_Text.text setter", PropertySetter("TMPro.TMP_Text", "text"));
        Log(logger, "UnityEngine.UI.Text.text setter", PropertySetter("UnityEngine.UI.Text", "text"));
    }

    private static void Log(ManualLogSource logger, string label, MethodBase? method)
    {
        if (method == null)
        {
            logger.LogWarning($"Patch target not found: {label}");
            return;
        }

        logger.LogInfo($"Patch target found: {label} -> {method.DeclaringType?.Assembly.GetName().Name}");
    }
}
