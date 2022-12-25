#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FactorioLocaleSync.Library.Mods;

public record ModInfo(string ModFolder) : ICacheHolder {
    private ModInfoJson? _infoJson;
    private Dictionary<string, ModLocale>? _locales;

    public string Name => Regex.Replace(Path.GetFileName(ModFolder), @"_[0-9.]+$", "");

    public IReadOnlyDictionary<string, ModLocale> Locales => _locales ??= GetLocales();

    public ModInfoJson InfoJson => _infoJson ??= ModInfoJson.FromFile(Path.Combine(ModFolder, "info.json"));

    public Task ClearCacheAsync() {
        return _locales == null ? Task.CompletedTask : _locales.Values.Select(locale => locale.ClearCacheAsync()).WhenAll();
    }

    private Dictionary<string, ModLocale> GetLocales() {
        var localesPath = Path.Combine(ModFolder, "locale");
        if (!Directory.Exists(localesPath)) return new Dictionary<string, ModLocale>();

        return Directory.GetDirectories(localesPath)
            .Where(s => Directory.GetFiles(s).Any())
            .Select(s => new ModLocale(this, Path.GetFileName(s)!))
            .ToDictionary(locale => locale.LocaleName);
    }

    public static IEnumerable<ModInfo> GetMods(string modsPath) {
        return Directory.GetDirectories(modsPath)
            .Select(s => new ModInfo(s));
    }
}