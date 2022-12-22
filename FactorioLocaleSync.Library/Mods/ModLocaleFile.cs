#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FactorioLocaleSync.Library.Mods;

public record ModLocaleFile(ModLocale Locale, string FileName) : ICacheHolder {
    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? _fileContent;
    public string FilePath => Path.Combine(Locale.LocaleFolder, FileName);

    public Task ClearCacheAsync() {
        _fileContent = null;
        return Task.CompletedTask;
    }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> GetContent() {
        _fileContent ??= LocalizationProcessor.LoadFromFile(FilePath);
        return _fileContent;
    }
}