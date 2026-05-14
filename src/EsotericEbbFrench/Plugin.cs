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
    public const string PluginVersion = "0.1.2";

    internal static ManualLogSource Logger { get; private set; } = null!;

    private Harmony? _harmony;

    public override void Load()
    {
        Logger = Log;

        ConfigEntry<string> profile = Config.Bind(
            "General",
            "Profile",
            "english-slot",
            "Translation profile to load. Use 'english-slot' to show French by default, 'german-slot' to use the German language slot, or 'fr-columns' if the game exposes a French language slot.");

        string pluginDirectory = Path.Combine(Paths.PluginPath, "EsotericEbbFrench");
        string translationsDirectory = Path.Combine(pluginDirectory, "translations", profile.Value.Trim());

        if (!TranslationCatalog.Load(translationsDirectory, profile.Value.Trim(), Logger))
        {
            string fallback = Path.Combine(pluginDirectory, "translations", "english-slot");
            if (!translationsDirectory.Equals(fallback, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning($"Profile '{profile.Value}' could not be loaded. Falling back to english-slot.");
                TranslationCatalog.Load(fallback, "english-slot", Logger);
            }
        }

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(Assembly.GetExecutingAssembly());

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded with {TranslationCatalog.AssetCount} translated TextAssets and {TranslationCatalog.LineCount} localized IDs.");
    }
}
