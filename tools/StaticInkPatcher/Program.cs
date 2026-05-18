using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

internal sealed record Options(
    string Command,
    string GameDir,
    string TranslationsDir,
    string BackupDir,
    bool DryRun,
    bool Verbose);

internal sealed record TranslationEntry(string Source, string Target);

internal static partial class Program
{
    private static readonly string[] LocalizedTextAssetNames =
    [
        "Dialogs",
        "UIElements",
        "QuestPoints",
        "GlossaryTerms",
        "Feats",
        "SheetInfo"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        try
        {
            Options options = ParseOptions(args);
            return options.Command switch
            {
                "patch" => Patch(options),
                "scan" => Patch(options with { DryRun = true, Verbose = true }),
                "restore" => Restore(options),
                _ => Usage($"Commande inconnue: {options.Command}")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erreur: {ex.Message}");
            return 1;
        }
    }

    private static int Patch(Options options)
    {
        string dataDir = GetDataDir(options.GameDir);
        string dialogsPath = Path.Combine(options.TranslationsDir, "Dialogs.txt");
        if (!File.Exists(dialogsPath))
        {
            throw new FileNotFoundException("Dialogs.txt introuvable.", dialogsPath);
        }

        if (!options.DryRun)
        {
            EnsureGameIsClosed();
            Directory.CreateDirectory(options.BackupDir);
        }

        Dictionary<string, Dictionary<string, TranslationEntry>> translations = LoadDialogTranslations(dialogsPath);
        Dictionary<string, string> textAssetReplacements = LoadTextAssetReplacements(options.TranslationsDir);
        Console.WriteLine($"Traductions Ink chargees: {translations.Sum(pair => pair.Value.Count)} lignes dans {translations.Count} stories.");
        Console.WriteLine($"Tables statiques chargees: {textAssetReplacements.Count} TextAssets.");

        int filesTouched = 0;
        int storiesTouched = 0;
        int tablesTouched = 0;
        int stringsReplaced = 0;
        foreach (string assetPath in EnumerateCandidateAssets(dataDir))
        {
            PatchFileResult result = PatchAssetsFile(assetPath, translations, textAssetReplacements, options);
            if (result.Replacements == 0)
            {
                continue;
            }

            filesTouched++;
            storiesTouched += result.Stories;
            tablesTouched += result.Tables;
            stringsReplaced += result.Replacements;
        }

        string mode = options.DryRun ? "Simulation" : "Patch";
        Console.WriteLine($"{mode} termine: {filesTouched} fichiers Unity, {storiesTouched} stories Ink, {tablesTouched} tables, {stringsReplaced} textes remplaces.");
        return 0;
    }

    private static PatchFileResult PatchAssetsFile(
        string assetPath,
        Dictionary<string, Dictionary<string, TranslationEntry>> translations,
        Dictionary<string, string> textAssetReplacements,
        Options options)
    {
        AssetsManager manager = new();
        AssetsFileInstance instance;
        try
        {
            instance = manager.LoadAssetsFile(assetPath, true);
        }
        catch
        {
            manager.UnloadAll(true);
            return new PatchFileResult(0, 0, 0);
        }

        List<AssetFileInfo> textAssets = instance.file.GetAssetsOfType(AssetClassID.TextAsset);

        int stories = 0;
        int tables = 0;
        int replacements = 0;
        try
        {
            foreach (AssetFileInfo info in textAssets)
            {
                byte[] raw = ReadAssetBytes(instance.file, info);
                if (!TryReadTextAsset(raw, out string name, out string script))
                {
                    continue;
                }

                if (textAssetReplacements.TryGetValue(name, out string? replacementTable))
                {
                    if (!TextEquals(script, replacementTable))
                    {
                        tables++;
                        replacements++;
                        if (options.Verbose)
                        {
                            Console.WriteLine($"{Path.GetFileName(assetPath)}: table {name} remplacee");
                        }

                        if (!options.DryRun)
                        {
                            byte[] newRaw = WriteTextAsset(name, replacementTable);
                            info.SetNewData(newRaw);
                        }
                    }

                    continue;
                }

                if (!translations.TryGetValue(name, out Dictionary<string, TranslationEntry>? storyTranslations))
                {
                    continue;
                }

                string text = StripBom(script);
                if (!text.Contains("\"inkVersion\"", StringComparison.Ordinal))
                {
                    continue;
                }

                string patched = PatchInkJson(name, text, storyTranslations, out int storyReplacements);
                if (storyReplacements == 0)
                {
                    continue;
                }

                stories++;
                replacements += storyReplacements;
                if (options.Verbose)
                {
                    Console.WriteLine($"{Path.GetFileName(assetPath)}: {name} -> {storyReplacements} textes");
                }

                if (!options.DryRun)
                {
                    byte[] newRaw = WriteTextAsset(name, "\uFEFF" + patched);
                    info.SetNewData(newRaw);
                }
            }

            if (replacements > 0 && !options.DryRun)
            {
                BackupOnce(assetPath, options);
                string tempPath = assetPath + ".frpatch.tmp";
                using (FileStream output = File.Create(tempPath))
                using (AssetsFileWriter writer = new(output))
                {
                    instance.file.Write(writer, 0);
                }

                manager.UnloadAssetsFile(instance);
                File.Copy(tempPath, assetPath, overwrite: true);
                File.Delete(tempPath);
            }
        }
        finally
        {
            manager.UnloadAll(true);
        }

        return new PatchFileResult(stories, tables, replacements);
    }

    private static bool TextEquals(string current, string replacement)
    {
        return string.Equals(
            StripBom(current).ReplaceLineEndings("\n"),
            StripBom(replacement).ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

    private static string PatchInkJson(
        string storyName,
        string json,
        Dictionary<string, TranslationEntry> translations,
        out int replacements)
    {
        JsonNode? root = JsonNode.Parse(json);
        if (root == null)
        {
            replacements = 0;
            return json;
        }

        replacements = 0;
        PatchNode(root, storyName, translations, ref replacements);
        return root.ToJsonString(JsonOptions);
    }

    private static void PatchNode(
        JsonNode node,
        string storyName,
        Dictionary<string, TranslationEntry> translations,
        ref int replacements)
    {
        if (node is JsonArray array)
        {
            PatchArray(array, storyName, translations, ref replacements);
            foreach (JsonNode? child in array)
            {
                if (child != null)
                {
                    PatchNode(child, storyName, translations, ref replacements);
                }
            }
        }
        else if (node is JsonObject obj)
        {
            foreach (KeyValuePair<string, JsonNode?> child in obj)
            {
                if (child.Value != null)
                {
                    PatchNode(child.Value, storyName, translations, ref replacements);
                }
            }
        }
    }

    private static void PatchArray(
        JsonArray array,
        string storyName,
        Dictionary<string, TranslationEntry> translations,
        ref int replacements)
    {
        for (int i = 0; i < array.Count; i++)
        {
            string? loc = ReadString(array[i]);
            if (loc == null || !loc.StartsWith("^LOC_", StringComparison.Ordinal))
            {
                continue;
            }

            string locId = loc[1..].Trim();
            if (!translations.TryGetValue(locId, out TranslationEntry? entry))
            {
                continue;
            }

            int textIndex = FindDisplayStringBeforeLoc(array, i);
            if (textIndex < 0)
            {
                continue;
            }

            string? original = ReadString(array[textIndex]);
            if (original == null || !original.StartsWith('^'))
            {
                continue;
            }

            string replacement = BuildInkString(original, entry.Target);
            if (replacement.Equals(original, StringComparison.Ordinal))
            {
                continue;
            }

            array[textIndex] = replacement;
            replacements++;
        }
    }

    private static int FindDisplayStringBeforeLoc(JsonArray array, int locIndex)
    {
        int cursor = locIndex - 1;
        while (cursor >= 0)
        {
            string? value = ReadString(array[cursor]);
            if (value == "#")
            {
                int candidate = cursor - 1;
                string? candidateValue = candidate >= 0 ? ReadString(array[candidate]) : null;
                if (candidateValue == "/#")
                {
                    cursor = candidate - 2;
                    continue;
                }

                return candidate;
            }

            cursor--;
        }

        return -1;
    }

    private static string BuildInkString(string original, string target)
    {
        string prefix = original.StartsWith('^') ? "^" : string.Empty;
        string payload = prefix.Length == 0 ? original : original[1..];
        string trailingWhitespace = GetTrailingWhitespace(payload);
        string translated = target.Trim();
        return prefix + translated + trailingWhitespace;
    }

    private static string GetTrailingWhitespace(string value)
    {
        int index = value.Length;
        while (index > 0 && char.IsWhiteSpace(value[index - 1]))
        {
            index--;
        }

        return value[index..];
    }

    private static string? ReadString(JsonNode? node)
    {
        return node is JsonValue value && value.TryGetValue(out string? text) ? text : null;
    }

    private static Dictionary<string, Dictionary<string, TranslationEntry>> LoadDialogTranslations(string path)
    {
        string csv = File.ReadAllText(path, Encoding.UTF8);
        List<string[]> rows = ParseCsv(csv);
        if (rows.Count < 2)
        {
            return new Dictionary<string, Dictionary<string, TranslationEntry>>(StringComparer.OrdinalIgnoreCase);
        }

        string[] header = rows[0];
        int keyIndex = IndexOf(header, "Key");
        int sourceIndex = IndexOf(header, "EN");
        int targetIndex = IndexOf(header, "FR");
        if (keyIndex < 0 || sourceIndex < 0 || targetIndex < 0)
        {
            throw new InvalidDataException("Dialogs.txt doit contenir les colonnes Key, EN et FR.");
        }

        Dictionary<string, Dictionary<string, TranslationEntry>> result = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < rows.Count; i++)
        {
            string[] row = rows[i];
            if (keyIndex >= row.Length || sourceIndex >= row.Length || targetIndex >= row.Length)
            {
                continue;
            }

            string key = row[keyIndex].Trim();
            string target = row[targetIndex].Trim();
            if (key.Length == 0 || target.Length == 0)
            {
                continue;
            }

            Match match = DialogKeyRegex().Match(key);
            if (!match.Success)
            {
                continue;
            }

            string story = match.Groups["story"].Value;
            string loc = "LOC_" + match.Groups["loc"].Value;
            if (!result.TryGetValue(story, out Dictionary<string, TranslationEntry>? storyRows))
            {
                storyRows = new Dictionary<string, TranslationEntry>(StringComparer.OrdinalIgnoreCase);
                result[story] = storyRows;
            }

            storyRows[loc] = new TranslationEntry(row[sourceIndex], target);
        }

        return result;
    }

    private static Dictionary<string, string> LoadTextAssetReplacements(string translationsDir)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in LocalizedTextAssetNames)
        {
            string path = Path.Combine(translationsDir, name + ".txt");
            if (File.Exists(path))
            {
                result[name] = File.ReadAllText(path, Encoding.UTF8);
            }
        }

        return result;
    }

    private static IEnumerable<string> EnumerateCandidateAssets(string dataDir)
    {
        foreach (string path in Directory.EnumerateFiles(dataDir, "*", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(path);
            if (name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (name.EndsWith(".assets", StringComparison.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }
    }

    private static byte[] ReadAssetBytes(AssetsFile file, AssetFileInfo info)
    {
        long absoluteOffset = info.GetAbsoluteByteOffset(file);
        byte[] bytes = new byte[info.ByteSize];
        lock (file.Reader)
        {
            file.Reader.BaseStream.Position = absoluteOffset;
            int read = file.Reader.BaseStream.Read(bytes, 0, bytes.Length);
            if (read != bytes.Length)
            {
                throw new EndOfStreamException($"Lecture asset incomplete: {read}/{bytes.Length}");
            }
        }

        return bytes;
    }

    private static bool TryReadTextAsset(byte[] raw, out string name, out string script)
    {
        name = string.Empty;
        script = string.Empty;
        int offset = 0;
        if (!TryReadAlignedString(raw, ref offset, out name))
        {
            return false;
        }

        return TryReadAlignedString(raw, ref offset, out script);
    }

    private static bool TryReadAlignedString(byte[] raw, ref int offset, out string value)
    {
        value = string.Empty;
        if (offset + 4 > raw.Length)
        {
            return false;
        }

        int length = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(offset, 4));
        offset += 4;
        if (length < 0 || offset + length > raw.Length)
        {
            return false;
        }

        value = Encoding.UTF8.GetString(raw, offset, length);
        offset += length;
        while (offset % 4 != 0)
        {
            offset++;
            if (offset > raw.Length)
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] WriteTextAsset(string name, string script)
    {
        using MemoryStream stream = new();
        WriteAlignedString(stream, name);
        WriteAlignedString(stream, script);
        return stream.ToArray();
    }

    private static void WriteAlignedString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        stream.Write(length);
        stream.Write(bytes);
        while (stream.Position % 4 != 0)
        {
            stream.WriteByte(0);
        }
    }

    private static string StripBom(string value)
    {
        return value.Length > 0 && value[0] == '\uFEFF' ? value[1..] : value;
    }

    private static bool ContainsAscii(string path, string needle)
    {
        byte[] needleBytes = Encoding.ASCII.GetBytes(needle);
        const int bufferSize = 1024 * 1024;
        byte[] buffer = new byte[bufferSize + needleBytes.Length - 1];
        int overlap = 0;

        using FileStream stream = File.OpenRead(path);
        while (true)
        {
            int read = stream.Read(buffer, overlap, bufferSize);
            if (read == 0)
            {
                return false;
            }

            int length = overlap + read;
            if (IndexOf(buffer.AsSpan(0, length), needleBytes) >= 0)
            {
                return true;
            }

            overlap = Math.Min(needleBytes.Length - 1, length);
            buffer.AsSpan(length - overlap, overlap).CopyTo(buffer);
        }
    }

    private static int IndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.Slice(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }

    private static void BackupOnce(string assetPath, Options options)
    {
        string dataDir = GetDataDir(options.GameDir);
        string relative = Path.GetRelativePath(dataDir, assetPath);
        string backupPath = Path.Combine(options.BackupDir, relative);
        if (File.Exists(backupPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        File.Copy(assetPath, backupPath, overwrite: false);
        Console.WriteLine($"Backup: {relative}");
    }

    private static int Restore(Options options)
    {
        string dataDir = GetDataDir(options.GameDir);
        if (!Directory.Exists(options.BackupDir))
        {
            throw new DirectoryNotFoundException($"Backup introuvable: {options.BackupDir}");
        }

        EnsureGameIsClosed();

        int restored = 0;
        foreach (string backupPath in Directory.EnumerateFiles(options.BackupDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(options.BackupDir, backupPath);
            string target = Path.Combine(dataDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(backupPath, target, overwrite: true);
            restored++;
            Console.WriteLine($"Restored: {relative}");
        }

        Console.WriteLine($"Restauration terminee: {restored} fichiers.");
        return 0;
    }

    private static void EnsureGameIsClosed()
    {
        Process[] processes = Process.GetProcessesByName("Esoteric Ebb");
        if (processes.Length > 0)
        {
            throw new InvalidOperationException("Ferme Esoteric Ebb avant d'appliquer/restaurer le patch statique.");
        }
    }

    private static string GetDataDir(string gameDir)
    {
        string fullGameDir = Path.GetFullPath(gameDir);
        string dataDir = Path.Combine(fullGameDir, "Esoteric Ebb_Data");
        if (!Directory.Exists(dataDir))
        {
            throw new DirectoryNotFoundException($"Dossier Unity introuvable: {dataDir}");
        }

        return dataDir;
    }

    private static Options ParseOptions(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase))
        {
            Environment.Exit(Usage());
        }

        string command = args[0].Trim().ToLowerInvariant();
        string gameDir = Directory.GetCurrentDirectory();
        string? translationsDir = null;
        string? backupDir = null;
        bool dryRun = false;
        bool verbose = false;

        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--game-dir":
                    gameDir = RequireValue(args, ref i, arg);
                    break;
                case "--translations-dir":
                    translationsDir = RequireValue(args, ref i, arg);
                    break;
                case "--backup-dir":
                    backupDir = RequireValue(args, ref i, arg);
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                default:
                    throw new ArgumentException($"Argument inconnu: {arg}");
            }
        }

        translationsDir ??= ResolveDefaultTranslationsDir(gameDir);
        backupDir ??= Path.Combine(gameDir, "EsotericEbb-FR-StaticBackup");

        return new Options(
            command,
            Path.GetFullPath(gameDir),
            Path.GetFullPath(translationsDir),
            Path.GetFullPath(backupDir),
            dryRun,
            verbose);
    }

    private static string ResolveDefaultTranslationsDir(string gameDir)
    {
        string rootTranslations = Path.Combine(gameDir, "translations", "english-slot");
        if (Directory.Exists(rootTranslations))
        {
            return rootTranslations;
        }

        string installedTranslations = Path.Combine(
            gameDir,
            "BepInEx",
            "plugins",
            "EsotericEbbFrench",
            "translations",
            "english-slot");
        if (Directory.Exists(installedTranslations))
        {
            return installedTranslations;
        }

        string repoTranslations = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "assets",
            "translations",
            "english-slot"));
        if (Directory.Exists(repoTranslations))
        {
            return repoTranslations;
        }

        return installedTranslations;
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        index++;
        if (index >= args.Length)
        {
            throw new ArgumentException($"{option} attend une valeur.");
        }

        return args[index];
    }

    private static int Usage(string? error = null)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            Console.Error.WriteLine(error);
        }

        Console.WriteLine("""
Usage:
  StaticInkPatcher scan    --game-dir "E:\...\Esoteric Ebb"
  StaticInkPatcher patch   --game-dir "E:\...\Esoteric Ebb"
  StaticInkPatcher restore --game-dir "E:\...\Esoteric Ebb"

Options:
  --translations-dir <dir>  Dossier contenant Dialogs.txt, par defaut translations/english-slot.
  --backup-dir <dir>        Backup des .assets originaux, par defaut EsotericEbb-FR-StaticBackup.
  --dry-run                 Simule patch sans ecrire.
  --verbose                 Affiche les stories modifiees.
""");
        return string.IsNullOrWhiteSpace(error) ? 0 : 1;
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

    [GeneratedRegex(@"^(?<story>.+)_(?<loc>\d+)$", RegexOptions.Compiled)]
    private static partial Regex DialogKeyRegex();

    private sealed record PatchFileResult(int Stories, int Tables, int Replacements);
}
