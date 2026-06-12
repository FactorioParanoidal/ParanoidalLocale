#nullable enable
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FactorioLocaleSync.Library;
using FactorioLocaleSync.Library.Mods;
using Nuke.Common.IO;
using Serilog;

public static class ModLocalizationUtils
{
    static readonly JsonSerializerOptions JsonSerializerOptions = new()
        { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static IEnumerable<ModLocale> ProcessModsToGetLocalizable(IEnumerable<ModInfo> mods, string targetLanguage,
        ILogger? logger)
        => mods.ToDictionary(info => info, info => GetDefaultLocale(info, targetLanguage, logger))
            .Where(pair => pair.Value != null)
            .Select(pair => pair.Key.Locales[pair.Value!]);

    static string? GetDefaultLocale(ModInfo mod, string targetLanguage, ILogger? logger)
    {
        if (mod.Locales.Count == 0)
        {
            logger?.Debug("Skipped: Mod {mod} has no locales", mod.Name);
            return null;
        }

        if (mod.Locales.All(pair => pair.Key == targetLanguage))
        {
            logger?.Debug("Skipped: Mod {ModName} doesn't have any localization except target", mod.Name);
            return null;
        }

        var defaultLocale = mod.Locales.ExceptKeys(targetLanguage)!.GetValueOrDefault("en")
                            ?? mod.Locales.ExceptKeys(targetLanguage).First().Value;

        if (!mod.Locales.TryGetValue(targetLanguage, out var targetLocale))
        {
            logger?.Debug("Proceed: Mod {ModName} doesn't have target {TargetLocale} localization", mod.Name,
                targetLanguage);
            return defaultLocale.LocaleName;
        }

        var defaultLocalizations = defaultLocale.GetMergedLocalizations();
        var targetLocalizations = targetLocale.GetMergedLocalizations();
        var contentLocalized = LocalizationProcessor.ContentLocalized(defaultLocalizations, targetLocalizations);
        if (contentLocalized)
        {
            logger?.Debug("Skipped: Mod {ModName} is already localized to {TargetLocale}", mod.Name, targetLanguage);
            return null;
        }

        logger?.Debug("Proceed: Mod {ModName} is not completely localized to {TargetLocale}", mod.Name,
            targetLanguage);
        return defaultLocale.LocaleName;
    }

    public static void WriteModsInitialLocaleFiles(IEnumerable<ModLocale> modLocales, AbsolutePath path,
        ILogger? logger)
    {
        path.CreateDirectory();
        var files = modLocales.SelectMany(locale => locale.Files)
            .Where(pair => pair.Key.EndsWith(".cfg"));
        foreach (var file in files) WriteModInitialLocaleFile(file.Value, path, logger);
    }

    public static void WriteModInitialLocaleFile(ModLocaleFile file, AbsolutePath path, ILogger? logger)
    {
        var filePath = file.GetTargetPath(path);
        var filtered = file.GetContent()
            .ToDictionary(
                section => section.Key,
                section => (IDictionary<string, string>)section.Value
                    .Where(kv => !LocalizationPlaceholders.IsPlaceholderOnly(kv.Value))
                    .ToDictionary(kv => kv.Key, kv => kv.Value));
        var content = JsonSerializer.Serialize(filtered, JsonSerializerOptions);
        File.WriteAllText(filePath, content);
        logger?.Debug("Mod {ModName}, file {FileName}, with {LocaleName} written to {FilePath}", file.Locale.Mod.Name,
            file.FileName, file.Locale.LocaleName, filePath);
    }

    public static void SyncDependentMods(IEnumerable<ModInfo> mods, AbsolutePath path)
    {
        var names = mods.Select(info => info.InfoJson.InternalName).OrderBy(x => x).ToHashSet();
        File.WriteAllText(path, JsonSerializer.Serialize(names, JsonSerializerOptions));
    }

    public static void CleanStaleFiles(IEnumerable<ModLocale> modLocales,
        AbsolutePath initialFolder, AbsolutePath targetLocaleFolder, ILogger? logger)
    {
        var expectedPaths = modLocales
            .SelectMany(l => l.Files.Values)
            .Where(f => f.FileName.EndsWith(".cfg"))
            .Select(f => Path.GetFileName(f.GetTargetPath(initialFolder)))
            .ToHashSet();

        if (!initialFolder.DirectoryExists()) return;

        foreach (var file in initialFolder.GlobFiles("*.json"))
        {
            if (expectedPaths.Contains(file.Name)) continue;
            logger?.Information("Deleting stale initial file {File}", file);
            File.Delete(file);
            var ruFile = targetLocaleFolder / file.Name;
            if (ruFile.FileExists())
            {
                logger?.Information("Deleting stale ru file {File}", ruFile);
                File.Delete(ruFile);
            }
        }
    }

    public static void MigrateTranslationsFromStaleFiles(IReadOnlyList<ModLocale> modLocales,
        AbsolutePath initialFolder, AbsolutePath targetLocaleFolder, ILogger? logger)
    {
        var expectedNames = modLocales
            .SelectMany(l => l.Files.Values)
            .Where(f => f.FileName.EndsWith(".cfg"))
            .Select(f => Path.GetFileName(f.GetTargetPath(initialFolder)))
            .ToHashSet();

        if (!initialFolder.DirectoryExists()) return;

        var staleFiles = initialFolder.GlobFiles("*.json")
            .Where(f => !expectedNames.Contains(f.Name))
            .ToList();

        if (staleFiles.Count == 0) return;

        // (section, key, englishValue) -> ruValue
        var migrationMap = new Dictionary<(string, string, string), string>();

        foreach (var staleInitialPath in staleFiles)
        {
            var staleRuPath = targetLocaleFolder / staleInitialPath.Name;
            if (!staleRuPath.FileExists()) continue;

            var initialData =
                JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                    File.ReadAllText(staleInitialPath));
            var ruData =
                JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                    File.ReadAllText(staleRuPath));

            if (initialData == null || ruData == null) continue;

            foreach (var (section, keys) in initialData)
            {
                if (!ruData.TryGetValue(section, out var ruSection)) continue;
                foreach (var (key, englishValue) in keys)
                    if (ruSection.TryGetValue(key, out var ruValue) && !string.IsNullOrWhiteSpace(ruValue))
                        migrationMap.TryAdd((section, key, englishValue), ruValue);
            }
        }

        if (migrationMap.Count == 0) return;

        foreach (var modLocale in modLocales)
        foreach (var file in modLocale.Files.Values.Where(f => f.FileName.EndsWith(".cfg")))
        {
            var targetRuPath = file.GetTargetPath(targetLocaleFolder);
            var initialContent = file.GetContent();

            Dictionary<string, Dictionary<string, string>>? existingRu = null;
            if (File.Exists(targetRuPath))
                existingRu =
                    JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                        File.ReadAllText(targetRuPath));

            existingRu ??= new Dictionary<string, Dictionary<string, string>>();

            var changed = false;
            foreach (var (section, keys) in initialContent)
            {
                var ruSection = existingRu.GetOrAdd(section, () => new Dictionary<string, string>());
                foreach (var (key, englishValue) in keys)
                {
                    if (ruSection.ContainsKey(key)) continue;

                    if (migrationMap.TryGetValue((section, key, englishValue), out var migratedValue))
                    {
                        ruSection[key] = migratedValue;
                        changed = true;
                        logger?.Information("Migrated translation for [{Section}] {Key} from stale files", section,
                            key);
                    }
                }
            }

            if (changed) File.WriteAllText(targetRuPath, JsonSerializer.Serialize(existingRu, JsonSerializerOptions));
        }
    }

    public static void AppendAlreadyLocalizedContent(IEnumerable<ModInfo> mods, IEnumerable<ModLocale> initialLocales,
        AbsolutePath targetLocalePath, string targetLocale, ILogger? logger)
    {
        targetLocalePath.CreateDirectory();
        var allLocalizations = mods.SelectMany(info => info.Locales)
            .Where(pair => pair.Key == targetLocale)
            .Select(pair => pair.Value.GetMergedLocalizations());
        var mergedLocalizations = LocalizationProcessor.Merge(allLocalizations);

        var files = initialLocales.SelectMany(locale => locale.Files)
            .Where(pair => pair.Key.EndsWith(".cfg"));
        foreach (var (_, file) in files)
        {
            logger?.Debug("Appending already localized content to {FilePath}", file.GetTargetPath(targetLocalePath));
            AppendAlreadyLocalizedContentToFile(file, mergedLocalizations, targetLocalePath);
        }
    }

    static void AppendAlreadyLocalizedContentToFile(ModLocaleFile file,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> existedLocalization,
        AbsolutePath targetLocalePath)
    {
        var filePath = file.GetTargetPath(targetLocalePath);
        var localeDictionary = File.Exists(filePath)
            ? JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(filePath))!
            : new Dictionary<string, Dictionary<string, string>>();

        var initialLocalization = file.GetContent();
        var result = new Dictionary<string, Dictionary<string, string>>();
        foreach (var (sectionKey, sectionContent) in initialLocalization)
        {
            var section = new Dictionary<string, string>();
            foreach (var (localeKey, englishValue) in sectionContent)
            {
                // Skip keys that carry no translatable text in the source (placeholder-only). They are
                // omitted from initial/ too, so they must not leak into the target locale either.
                if (LocalizationPlaceholders.IsPlaceholderOnly(englishValue)) continue;

                // 1. already in ru/ file
                if (localeDictionary.TryGetValue(sectionKey, out var existingSection)
                    && existingSection.TryGetValue(localeKey, out var existing)
                    && !string.IsNullOrWhiteSpace(existing))
                {
                    section[localeKey] = existing;
                    continue;
                }


                // 2. found in modpack's own translations
                var alreadyLocalized =
                    existedLocalization?.GetValueOrDefault(sectionKey)?.GetValueOrDefault(localeKey);
                if (alreadyLocalized != null && !string.IsNullOrWhiteSpace(alreadyLocalized))
                    section[localeKey] = alreadyLocalized;
            }

            // Drop sections that ended up without any translatable keys to keep the file clean and
            // structurally aligned with the filtered initial/ file.
            if (section.Count > 0) result[sectionKey] = section;
        }

        var content = JsonSerializer.Serialize(result, JsonSerializerOptions);
        File.WriteAllText(filePath, content);
    }

    public static void ExportDictionaryJsonsToFactorioCfg(AbsolutePath fromDirectory,
        AbsolutePath targetLocaleDirectory, ILogger? logger)
    {
        foreach (var fromFile in fromDirectory.GlobFiles("*.json"))
        {
            var fromText = File.ReadAllText(fromFile);
            var dictionary = JsonSerializer.Deserialize<IDictionary<string, IDictionary<string, string>>>(fromText)!;
            var fromFileNameWithoutExtension = targetLocaleDirectory / fromFile.NameWithoutExtension + ".cfg";

            var keysCount = dictionary.Sum(x => x.Value.Count);
            if (keysCount == 0)
            {
                logger?.Debug("Skipping {FilePath} because it has no localized keys", fromFile);
                continue;
            }

            logger?.Debug("Writing {FileName} with {SectionsCount} sections and {KeysCount} keys",
                fromFileNameWithoutExtension, dictionary.Count, keysCount);
            ExportDictionaryToCfgFile(dictionary, fromFileNameWithoutExtension);
        }
    }

    public static void ExportDictionaryToCfgFile(IDictionary<string, IDictionary<string, string>> dictionary,
        string filePath)
    {
        var sb = new StringBuilder();
        foreach (var (sectionKey, sectionContent) in dictionary)
        {
            if (sectionContent.Count == 0) continue;
            if (sectionKey != LocalizationProcessor.DefaultSectionKey) sb.AppendLine($"[{sectionKey}]");

            foreach (var (localeKey, localeValue) in sectionContent)
            {
                sb.AppendLine($"{localeKey}={localeValue.ReplaceLineEndings("\\n")}");
            }

            sb.AppendLine();
        }

        File.WriteAllText(filePath, sb.ToString());
    }
}