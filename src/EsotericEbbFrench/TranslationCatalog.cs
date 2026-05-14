using BepInEx.Logging;
using System.Text;

namespace EsotericEbbFrench;

internal static class TranslationCatalog
{
    private static readonly Dictionary<string, string> Texts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Lines = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> DialogLines = new(StringComparer.OrdinalIgnoreCase);

    public static int AssetCount => Texts.Count;
    public static int LineCount => Lines.Count + DialogLines.Count;

    public static bool Load(string directory, string profile, ManualLogSource logger)
    {
        Texts.Clear();
        Lines.Clear();
        DialogLines.Clear();

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

            string content = File.ReadAllText(path);
            Texts[assetName] = content;
            LoadLines(assetName, content, profile, logger);
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

    public static bool TryGetLine(string? id, out string text)
    {
        text = string.Empty;

        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (Lines.TryGetValue(id.Trim(), out string? value))
        {
            text = value;
            return true;
        }

        return false;
    }

    public static bool TryGetDialogLine(string? id, out string text)
    {
        text = string.Empty;

        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (DialogLines.TryGetValue(id.Trim(), out string? value))
        {
            text = value;
            return true;
        }

        return false;
    }

    private static void LoadLines(string assetName, string content, string profile, ManualLogSource logger)
    {
        List<string[]> rows = ParseCsv(content);
        if (rows.Count < 2)
        {
            return;
        }

        string[] header = rows[0];
        int keyIndex = IndexOf(header, assetName.Equals("Dialogs", StringComparison.OrdinalIgnoreCase) ? "Key" : "ID");
        if (keyIndex < 0)
        {
            return;
        }

        int valueIndex = FindValueColumn(assetName, header, profile);
        if (valueIndex < 0)
        {
            logger.LogWarning($"No localized value column found for {assetName} in profile {profile}.");
            return;
        }

        Dictionary<string, string> target = assetName.Equals("Dialogs", StringComparison.OrdinalIgnoreCase)
            ? DialogLines
            : Lines;

        int added = 0;
        for (int i = 1; i < rows.Count; i++)
        {
            string[] row = rows[i];
            if (keyIndex >= row.Length || valueIndex >= row.Length)
            {
                continue;
            }

            string key = row[keyIndex].Trim();
            string value = row[valueIndex];
            if (key.Length == 0 || value.Length == 0)
            {
                continue;
            }

            target[key] = value;
            added++;
        }

        logger.LogInfo($"Indexed {added} localized IDs from {assetName}.");
    }

    private static int FindValueColumn(string assetName, string[] header, string profile)
    {
        bool dialogs = assetName.Equals("Dialogs", StringComparison.OrdinalIgnoreCase);
        string normalizedProfile = profile.Trim().ToLowerInvariant();

        string[] preferred = normalizedProfile switch
        {
            "fr-columns" => dialogs ? new[] { "FR" } : new[] { "FRENCH" },
            "german-slot" => dialogs ? new[] { "DE", "FR" } : new[] { "GERMAN", "FRENCH" },
            _ => dialogs ? new[] { "EN", "FR" } : new[] { "ENGLISH", "FRENCH" },
        };

        foreach (string column in preferred)
        {
            int index = IndexOf(header, column);
            if (index >= 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int IndexOf(string[] row, string column)
    {
        for (int i = 0; i < row.Length; i++)
        {
            if (row[i].Equals(column, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<string[]> ParseCsv(string csv)
    {
        List<string[]> rows = new();
        List<string> row = new();
        StringBuilder field = new();
        bool quoted = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];

            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\n')
            {
                row.Add(field.ToString().TrimEnd('\r'));
                rows.Add(row.ToArray());
                row.Clear();
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString().TrimEnd('\r'));
            rows.Add(row.ToArray());
        }

        return rows;
    }

    private static string Normalize(string value)
    {
        string normalized = value.Replace('\\', '/').Trim();
        normalized = Path.GetFileNameWithoutExtension(normalized);
        return normalized;
    }
}
