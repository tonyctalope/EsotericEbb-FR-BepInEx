using BepInEx.Logging;

namespace EsotericEbbFrench;

internal static class TranslationCatalog
{
    private static readonly Dictionary<string, string> Texts = new(StringComparer.OrdinalIgnoreCase);

    public static int Count => Texts.Count;

    public static bool Load(string directory, ManualLogSource logger)
    {
        Texts.Clear();

        if (!Directory.Exists(directory))
        {
            logger.LogError($"Translation directory not found: {directory}");
            return false;
        }

        foreach (string path in Directory.EnumerateFiles(directory, "*.txt", SearchOption.TopDirectoryOnly))
        {
            string assetName = Normalize(Path.GetFileNameWithoutExtension(path));
            if (string.IsNullOrWhiteSpace(assetName))
            {
                continue;
            }

            Texts[assetName] = File.ReadAllText(path);
        }

        logger.LogInfo($"Loaded translation profile: {directory}");
        return Texts.Count > 0;
    }

    public static bool TryGetText(string? assetNameOrPath, out string text)
    {
        text = string.Empty;

        if (string.IsNullOrWhiteSpace(assetNameOrPath))
        {
            return false;
        }

        if (Texts.TryGetValue(Normalize(assetNameOrPath), out string? value))
        {
            text = value;
            return true;
        }

        return false;
    }

    private static string Normalize(string value)
    {
        string normalized = value.Replace('\\', '/').Trim();
        normalized = Path.GetFileNameWithoutExtension(normalized);
        return normalized;
    }
}
