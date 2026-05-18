namespace EsotericEbbFrench;

internal static class RuntimeTranslationState
{
    private static bool _dynamicTextEnabled = true;

    public static bool DynamicTextEnabled => _dynamicTextEnabled;

    public static void DisableDynamicText(string reason)
    {
        if (!_dynamicTextEnabled)
        {
            return;
        }

        _dynamicTextEnabled = false;
        Plugin.Logger.LogInfo($"Disabled dynamic text replacement: {reason}.");
    }
}
