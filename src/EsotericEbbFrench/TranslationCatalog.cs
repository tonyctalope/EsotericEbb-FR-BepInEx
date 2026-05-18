using BepInEx.Logging;
using System.Text;

namespace EsotericEbbFrench;

internal static class TranslationCatalog
{
    private static readonly Dictionary<string, string> Texts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Lines = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> DialogLines = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> ReverseTexts = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> NormalizedReverseTexts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> RuntimeTexts = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> NormalizedRuntimeTexts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> AmbiguousReverseTexts = new(StringComparer.Ordinal);
    private static readonly HashSet<string> AmbiguousNormalizedReverseTexts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> AmbiguousRuntimeTexts = new(StringComparer.Ordinal);
    private static readonly HashSet<string> AmbiguousNormalizedRuntimeTexts = new(StringComparer.OrdinalIgnoreCase);

    public static int AssetCount => Texts.Count;
    public static int LineCount => Lines.Count + DialogLines.Count;

    public static bool Load(string directory, string profile, ManualLogSource logger)
    {
        Texts.Clear();
        Lines.Clear();
        DialogLines.Clear();
        ReverseTexts.Clear();
        NormalizedReverseTexts.Clear();
        RuntimeTexts.Clear();
        NormalizedRuntimeTexts.Clear();
        AmbiguousReverseTexts.Clear();
        AmbiguousNormalizedReverseTexts.Clear();
        AmbiguousRuntimeTexts.Clear();
        AmbiguousNormalizedRuntimeTexts.Clear();

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
        LoadRuntimeTerms(directory, logger);
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
        return TryGetDisplayText(source, ReverseTexts, NormalizedReverseTexts, out text);
    }

    public static bool TryGetRuntimeText(string? source, out string text)
    {
        return TryGetDisplayText(source, RuntimeTexts, NormalizedRuntimeTexts, out text);
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
            AddDashTermReverseText(row[sourceIndex], row[targetIndex]);
        }
    }

    private static void LoadRuntimeTerms(string directory, ManualLogSource logger)
    {
        string? profilesDirectory = Directory.GetParent(directory)?.FullName;
        if (profilesDirectory == null)
        {
            return;
        }

        string path = Path.Combine(profilesDirectory, "runtime_terms.csv");
        if (!File.Exists(path))
        {
            return;
        }

        List<string[]> rows = ParseCsv(File.ReadAllText(path));
        if (rows.Count < 2)
        {
            return;
        }

        string[] header = rows[0];
        int sourceIndex = IndexOf(header, "ENGLISH");
        int targetIndex = IndexOf(header, "FRENCH");
        if (sourceIndex < 0 || targetIndex < 0)
        {
            logger.LogWarning($"Runtime terms file has no ENGLISH/FRENCH header: {path}");
            return;
        }

        int added = 0;
        for (int i = 1; i < rows.Count; i++)
        {
            string[] row = rows[i];
            if (sourceIndex >= row.Length || targetIndex >= row.Length)
            {
                continue;
            }

            AddRuntimeText(row[sourceIndex], row[targetIndex]);
            added++;
        }

        logger.LogInfo($"Indexed {RuntimeTexts.Count} runtime text replacements from {added} runtime rows.");
    }

    private static void AddReverseText(string source, string target)
    {
        AddDisplayTextWithVariants(source, target, ReverseTexts, NormalizedReverseTexts, AmbiguousReverseTexts, AmbiguousNormalizedReverseTexts);
    }

    private static void AddRuntimeText(string source, string target)
    {
        AddDisplayTextWithVariants(source, target, RuntimeTexts, NormalizedRuntimeTexts, AmbiguousRuntimeTexts, AmbiguousNormalizedRuntimeTexts);
    }

    private static void AddDashTermReverseText(string source, string target)
    {
        int sourceDash = source.IndexOf(" - ", StringComparison.Ordinal);
        int targetDash = target.IndexOf(" - ", StringComparison.Ordinal);
        if (sourceDash <= 0 || targetDash <= 0)
        {
            return;
        }

        AddReverseText(source[..sourceDash], target[..targetDash]);
    }

    private static void AddNormalizedReverseText(string source, string target)
    {
        AddNormalizedDisplayText(source, target, NormalizedReverseTexts, AmbiguousNormalizedReverseTexts);
    }

    private static bool TryGetDisplayText(
        string? source,
        Dictionary<string, string> exactTexts,
        Dictionary<string, string> normalizedTexts,
        out string text)
    {
        text = string.Empty;

        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (exactTexts.TryGetValue(source, out string? value))
        {
            text = value;
            return true;
        }

        string trimmed = source.Trim();
        if (trimmed.Length != source.Length && exactTexts.TryGetValue(trimmed, out value))
        {
            int leading = source.Length - source.TrimStart().Length;
            int trailing = source.Length - source.TrimEnd().Length;
            text = source[..leading] + value + source[(source.Length - trailing)..];
            return true;
        }

        string normalized = NormalizeDisplayText(source);
        if (normalized.Length > 0 && normalizedTexts.TryGetValue(normalized, out value))
        {
            text = value;
            return true;
        }

        return false;
    }

    private static void AddDisplayTextWithVariants(
        string source,
        string target,
        Dictionary<string, string> exactTexts,
        Dictionary<string, string> normalizedTexts,
        HashSet<string> ambiguousTexts,
        HashSet<string> ambiguousNormalizedTexts)
    {
        AddDisplayText(source, target, exactTexts, normalizedTexts, ambiguousTexts, ambiguousNormalizedTexts);

        string sourceWithoutQuotes = StripMatchingQuotes(source);
        string targetWithoutQuotes = StripMatchingQuotes(target);
        if (!sourceWithoutQuotes.Equals(source.Trim(), StringComparison.Ordinal)
            && !targetWithoutQuotes.Equals(target.Trim(), StringComparison.Ordinal))
        {
            AddDisplayText(sourceWithoutQuotes, targetWithoutQuotes, exactTexts, normalizedTexts, ambiguousTexts, ambiguousNormalizedTexts);
        }
    }

    private static void AddDisplayText(
        string source,
        string target,
        Dictionary<string, string> exactTexts,
        Dictionary<string, string> normalizedTexts,
        HashSet<string> ambiguousTexts,
        HashSet<string> ambiguousNormalizedTexts)
    {
        source = source.Trim();
        target = target.Trim();

        if (source.Length < 2 || target.Length == 0 || source.Equals(target, StringComparison.Ordinal))
        {
            return;
        }

        if (ambiguousTexts.Contains(source))
        {
            return;
        }

        if (exactTexts.TryGetValue(source, out string? existing))
        {
            if (!existing.Equals(target, StringComparison.Ordinal))
            {
                exactTexts.Remove(source);
                ambiguousTexts.Add(source);
            }

            return;
        }

        exactTexts[source] = target;
        AddNormalizedDisplayText(source, target, normalizedTexts, ambiguousNormalizedTexts);
    }

    private static void AddNormalizedDisplayText(
        string source,
        string target,
        Dictionary<string, string> normalizedTexts,
        HashSet<string> ambiguousNormalizedTexts)
    {
        string normalized = NormalizeDisplayText(source);
        if (normalized.Length < 2 || ambiguousNormalizedTexts.Contains(normalized))
        {
            return;
        }

        if (normalizedTexts.TryGetValue(normalized, out string? existing))
        {
            if (!existing.Equals(target, StringComparison.Ordinal))
            {
                normalizedTexts.Remove(normalized);
                ambiguousNormalizedTexts.Add(normalized);
            }

            return;
        }

        normalizedTexts[normalized] = target;
    }

    private static string StripMatchingQuotes(string value)
    {
        value = value.Trim();
        if (value.Length < 2)
        {
            return value;
        }

        if ((value[0] == '"' && value[^1] == '"')
            || (value[0] == '\'' && value[^1] == '\''))
        {
            return value[1..^1].Trim();
        }

        return value;
    }

    private static string NormalizeDisplayText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        bool inTag = false;
        bool previousWhitespace = false;

        foreach (char c in value)
        {
            if (c == '<')
            {
                inTag = true;
                continue;
            }

            if (inTag)
            {
                if (c == '>')
                {
                    inTag = false;
                }

                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (!previousWhitespace && builder.Length > 0)
                {
                    builder.Append(' ');
                    previousWhitespace = true;
                }

                continue;
            }

            builder.Append(c);
            previousWhitespace = false;
        }

        return builder.ToString().Trim();
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
