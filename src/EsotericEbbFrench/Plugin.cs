using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace EsotericEbbFrench;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("Esoteric Ebb.exe")]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "fr.esotericebb.translation";
    public const string PluginName = "Esoteric Ebb - Traduction francaise";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource Logger { get; private set; } = null!;

    private Harmony? _harmony;

    public override void Load()
    {
        Logger = Log;

        ConfigEntry<string> profile = Config.Bind(
            "General",
            "Profile",
            "german-slot",
            "Translation profile to load. Use 'german-slot' for maximum compatibility, or 'fr-columns' if the game exposes a French language slot.");

        string pluginDirectory = Path.Combine(Paths.PluginPath, "EsotericEbbFrench");
        string translationsDirectory = Path.Combine(pluginDirectory, "translations", profile.Value.Trim());

        if (!TranslationCatalog.Load(translationsDirectory, Logger))
        {
            string fallback = Path.Combine(pluginDirectory, "translations", "german-slot");
            if (!translationsDirectory.Equals(fallback, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning($"Profile '{profile.Value}' could not be loaded. Falling back to german-slot.");
                TranslationCatalog.Load(fallback, Logger);
            }
        }

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(Assembly.GetExecutingAssembly());

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded with {TranslationCatalog.Count} translated TextAssets.");
    }
}
