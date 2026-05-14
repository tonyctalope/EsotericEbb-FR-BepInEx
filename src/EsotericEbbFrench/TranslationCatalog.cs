using BepInEx.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EsotericEbbFrench;

internal static class TranslationCatalog
{
    private static readonly Dictionary<string, string> Texts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, TextAsset> CreatedAssets = new(StringComparer.OrdinalIgnoreCase);

    public static int Count => Texts.Count;

    public static bool Load(string directory, ManualLogSource logger)
    {
        Texts.Clear();
        CreatedAssets.Clear();

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

    public static bool TryCreateAsset(string? assetNameOrPath, out Object replacement)
    {
        replacement = null!;

        if (string.IsNullOrWhiteSpace(assetNameOrPath))
        {
            return false;
        }

        string assetName = Normalize(assetNameOrPath);
        if (!Texts.TryGetValue(assetName, out string? text))
        {
            return false;
        }

        if (!CreatedAssets.TryGetValue(assetName, out TextAsset? asset))
        {
            asset = new TextAsset(text)
            {
                name = assetName
            };
            CreatedAssets[assetName] = asset;
        }

        replacement = asset;
        return true;
    }

    private static string Normalize(string value)
    {
        string normalized = value.Replace('\\', '/').Trim();
        normalized = Path.GetFileNameWithoutExtension(normalized);
        return normalized;
    }
}
