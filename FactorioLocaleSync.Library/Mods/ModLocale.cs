#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FactorioLocaleSync.Library.Mods;

public record ModLocale(ModInfo Mod, string LocaleName) : ICacheHolder {
    private Dictionary<string, ModLocaleFile>? _files;
    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? _localizationContent;

    public IReadOnlyDictionary<string, ModLocaleFile> Files => _files ??= GetFiles();
    public string LocaleFolder => Path.Combine(Mod.ModFolder, "locale", LocaleName);

    public Task ClearCacheAsync() {
        _localizationContent = null;
        return _files == null ? Task.CompletedTask : _files.Values.Select(file => file.ClearCacheAsync()).WhenAll();
    }

    private Dictionary<string, ModLocaleFile> GetFiles() {
        return Directory.GetFiles(LocaleFolder)
            .Select(s => new ModLocaleFile(this, Path.GetFileName(s)))
            .ToDictionary(file => file.FileName);
    }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetMergedLocalizations() {
        if (_localizationContent != null) return _localizationContent;

        var localizations = Files.Values.Select(file => file.GetContent());
        return _localizationContent = LocalizationProcessor.Merge(localizations);
    }
}