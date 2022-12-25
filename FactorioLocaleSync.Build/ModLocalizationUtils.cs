#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FactorioLocaleSync.Library;
using FactorioLocaleSync.Library.Mods;
using NuGet.Packaging;
using Nuke.Common.IO;
using Serilog;
public static class ModLocalizationUtils {
    static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public static IEnumerable<ModLocale> ProcessModsToGetLocalizable(IEnumerable<ModInfo> mods, string targetLanguage, ILogger? logger)
        => mods.ToDictionary(info => info, info => GetDefaultLocale(info, targetLanguage, logger))
            .Where(pair => pair.Value != null)
            .Select(pair => pair.Key.Locales[pair.Value!]);

    static string? GetDefaultLocale(ModInfo mod, string targetLanguage, ILogger? logger) {
        if (mod.Locales.Count == 0) {
            logger?.Debug("Skipped: Mod {mod} has no locales.", mod.Name);
            return null;
        }

        if (mod.Locales.All(pair => pair.Key == targetLanguage)) {
            logger?.Debug("Skipped: Mod {ModName} doesn't have any localization except target.", mod.Name);
            return null;
        }

        var defaultLocale = mod.Locales.ExceptKeys(targetLanguage)!.GetValueOrDefault("en")
                         ?? mod.Locales.ExceptKeys(targetLanguage).First().Value;

        if (!mod.Locales.TryGetValue(targetLanguage, out var targetLocale)) {
            logger?.Debug("Proceed: Mod {ModName} doesn't have target {TargetLocale} localization.", mod.Name, targetLanguage);
            return defaultLocale.LocaleName;
        }

        var defaultLocalizations = defaultLocale.GetMergedLocalizations();
        var targetLocalizations = targetLocale.GetMergedLocalizations();
        var contentLocalized = LocalizationProcessor.ContentLocalized(defaultLocalizations, targetLocalizations);
        if (contentLocalized) {
            logger?.Debug("Skipped: Mod {ModName} is already localized to {TargetLocale}.", mod.Name, targetLanguage);
            return null;
        }

        logger?.Debug("Proceed: Mod {ModName} is not completely localized to {TargetLocale}.", mod.Name, targetLanguage);
        return defaultLocale.LocaleName;
    }

    public static void WriteModsInitialLocaleFiles(IEnumerable<ModLocale> modLocales, AbsolutePath path, ILogger? logger) {
        FileSystemTasks.EnsureExistingDirectory(path);
        var files = modLocales.SelectMany(locale => locale.Files);
        foreach (var file in files) WriteModInitialLocaleFile(file.Value, path, logger);
    }

    public static void WriteModInitialLocaleFile(ModLocaleFile file, AbsolutePath path, ILogger? logger) {
        var filePath = file.GetTargetPath(path);
        var content = JsonSerializer.Serialize(file.GetContent(), JsonSerializerOptions);
        File.WriteAllText(filePath, content);
        logger?.Debug("Mod {ModName}, file {FileName}, with {LocaleName} written to {FilePath}", file.Locale.Mod.Name, file.FileName, file.Locale.LocaleName, filePath);
    }

    public static void AppendDependentMods(IEnumerable<ModInfo> mods, AbsolutePath path) {
        var dependencies = path.Exists()
            ? JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(path))
            : new HashSet<string>();
        dependencies.AddRange(mods.Select(info => info.InfoJson.InternalName));
        File.WriteAllText(path, JsonSerializer.Serialize(dependencies, JsonSerializerOptions));
    }

    public static void AppendAlreadyLocalizedContent(IEnumerable<ModInfo> mods, IEnumerable<ModLocale> initialLocales, AbsolutePath targetLocalePath, string targetLocale, ILogger? logger) {
        FileSystemTasks.EnsureExistingDirectory(targetLocalePath);
        var allLocalizations = mods.SelectMany(info => info.Locales)
            .Where(pair => pair.Key == targetLocale)
            .Select(pair => pair.Value.GetMergedLocalizations());
        var mergedLocalizations = LocalizationProcessor.Merge(allLocalizations);

        var files = initialLocales.SelectMany(locale => locale.Files);
        foreach (var (_, file) in files) {
            logger?.Debug("Appending already localized content to {FilePath}", file.GetTargetPath(targetLocalePath));
            AppendAlreadyLocalizedContentToFile(file, mergedLocalizations, targetLocalePath);
        }
    }

    static void AppendAlreadyLocalizedContentToFile(ModLocaleFile file, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> existedLocalization, AbsolutePath targetLocalePath) {
        var filePath = file.GetTargetPath(targetLocalePath);
        var localeDictionary = File.Exists(filePath)
            ? JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(filePath))!
            : new Dictionary<string, Dictionary<string, string>>();

        var initialLocalization = file.GetContent();
        foreach (var (sectionKey, sectionContent) in initialLocalization) {
            var section = localeDictionary.GetOrAdd(sectionKey, () => new Dictionary<string, string>());

            foreach (var (localeKey, _) in sectionContent) {
                if (section.ContainsKey(localeKey)) continue;
                var alreadyLocalized = existedLocalization!.GetValueOrDefault(sectionKey)!?.GetValueOrDefault(localeKey);
                if (alreadyLocalized != null) section.Add(localeKey, alreadyLocalized);
            }
        }

        var content = JsonSerializer.Serialize(localeDictionary, JsonSerializerOptions);
        File.WriteAllText(filePath, content);
    }
}