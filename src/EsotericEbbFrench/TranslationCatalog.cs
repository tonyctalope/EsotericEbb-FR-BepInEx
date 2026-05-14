using BepInEx.Logging;
using System.Text;

namespace EsotericEbbFrench;

internal static class TranslationCatalog
{
    private static readonly Dictionary<string, string> Texts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Lines = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> DialogLines = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> ReverseTexts = new(StringComparer.Ordinal);
    private static readonly HashSet<string> AmbiguousReverseTexts = new(StringComparer.Ordinal);

    public static int AssetCount => Texts.Count;
    public static int LineCount => Lines.Count + DialogLines.Count;

    public static bool Load(string directory, string profile, ManualLogSource logger)
    {
        Texts.Clear();
        Lines.Clear();
        DialogLines.Clear();
        ReverseTexts.Clear();
        AmbiguousReverseTexts.Clear();

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

        LoadReverseTexts(directory, logger);
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

    public static bool TryGetReplacementCsv(string? originalCsv, out string replacement, out string assetName)
    {
        replacement = string.Empty;
        assetName = string.Empty;

        if (string.IsNullOrWhiteSpace(originalCsv))
        {
            return false;
        }

        assetName = DetectAssetName(originalCsv);
        if (assetName.Length == 0)
        {
            return false;
        }

        if (Texts.TryGetValue(assetName, out string? value))
        {
            replacement = value;
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

    public static bool TryGetReverseText(string? source, out string text)
    {
        text = string.Empty;

        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (ReverseTexts.TryGetValue(source, out string? value))
        {
            text = value;
            return true;
        }

        string trimmed = source.Trim();
        if (trimmed.Length != source.Length && ReverseTexts.TryGetValue(trimmed, out value))
        {
            int leading = source.Length - source.TrimStart().Length;
            int trailing = source.Length - source.TrimEnd().Length;
            text = source[..leading] + value + source[(source.Length - trailing)..];
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

    private static void LoadReverseTexts(string directory, ManualLogSource logger)
    {
        string? profilesDirectory = Directory.GetParent(directory)?.FullName;
        string sourceDirectory = profilesDirectory == null
            ? directory
            : Path.Combine(profilesDirectory, "fr-columns");

        if (!Directory.Exists(sourceDirectory))
        {
            sourceDirectory = directory;
        }

        foreach (string path in Directory.EnumerateFiles(sourceDirectory, "*.txt", SearchOption.TopDirectoryOnly))
        {
            string assetName = Normalize(Path.GetFileNameWithoutExtension(path));
            LoadReverseTextsFromAsset(assetName, File.ReadAllText(path));
        }

        logger.LogInfo($"Indexed {ReverseTexts.Count} unambiguous source text replacements.");
    }

    private static void LoadReverseTextsFromAsset(string assetName, string content)
    {
        List<string[]> rows = ParseCsv(content);
        if (rows.Count < 2)
        {
            return;
        }

        string[] header = rows[0];
        bool dialogs = assetName.Equals("Dialogs", StringComparison.OrdinalIgnoreCase);
        int sourceIndex = IndexOf(header, dialogs ? "EN" : "ENGLISH");
        int targetIndex = IndexOf(header, dialogs ? "FR" : "FRENCH");

        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        for (int i = 1; i < rows.Count; i++)
        {
            string[] row = rows[i];
            if (sourceIndex >= row.Length || targetIndex >= row.Length)
            {
                continue;
            }

            AddReverseText(row[sourceIndex], row[targetIndex]);
        }
    }

    private static void AddReverseText(string source, string target)
    {
        source = source.Trim();
        target = target.Trim();

        if (source.Length < 2 || target.Length == 0 || source.Equals(target, StringComparison.Ordinal))
        {
            return;
        }

        if (AmbiguousReverseTexts.Contains(source))
        {
            return;
        }

        if (ReverseTexts.TryGetValue(source, out string? existing))
        {
            if (!existing.Equals(target, StringComparison.Ordinal))
            {
                ReverseTexts.Remove(source);
                AmbiguousReverseTexts.Add(source);
            }

            return;
        }

        ReverseTexts[source] = target;
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

    private static string DetectAssetName(string csv)
    {
        string header = FirstCsvLine(csv);
        string firstKey = FirstCsvFieldOfSecondLine(csv);

        if (header.StartsWith("Key,Speaker,Area,Type,", StringComparison.OrdinalIgnoreCase))
        {
            return "Dialogs";
        }

        if (header.Contains("ResponseAS,Tags,DC", StringComparison.OrdinalIgnoreCase))
        {
            return "GlossaryTerms";
        }

        if (header.Contains("CHINESE", StringComparison.OrdinalIgnoreCase)
            && header.Contains("SPANISH", StringComparison.OrdinalIgnoreCase)
            && firstKey.StartsWith("Quest_", StringComparison.OrdinalIgnoreCase))
        {
            return "QuestPoints";
        }

        if (firstKey.StartsWith("UI_", StringComparison.OrdinalIgnoreCase))
        {
            return "UIElements";
        }

        if (firstKey.StartsWith("FEAT_", StringComparison.OrdinalIgnoreCase))
        {
            return "Feats";
        }

        if (header.StartsWith("ID,ENGLISH,GERMAN", StringComparison.OrdinalIgnoreCase))
        {
            return "SheetInfo";
        }

        return string.Empty;
    }

    private static string FirstCsvLine(string csv)
    {
        int end = csv.IndexOf('\n');
        string line = end >= 0 ? csv[..end] : csv;
        return line.TrimEnd('\r');
    }

    private static string FirstCsvFieldOfSecondLine(string csv)
    {
        int firstLineEnd = csv.IndexOf('\n');
        if (firstLineEnd < 0 || firstLineEnd + 1 >= csv.Length)
        {
            return string.Empty;
        }

        int secondLineEnd = csv.IndexOf('\n', firstLineEnd + 1);
        string line = secondLineEnd >= 0
            ? csv.Substring(firstLineEnd + 1, secondLineEnd - firstLineEnd - 1)
            : csv[(firstLineEnd + 1)..];

        int comma = line.IndexOf(',');
        string field = comma >= 0 ? line[..comma] : line;
        return field.Trim().Trim('"');
    }

    private static string Normalize(string value)
    {
        string normalized = value.Replace('\\', '/').Trim();
        normalized = Path.GetFileNameWithoutExtension(normalized);
        return normalized;
    }
}
