#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using FactorioLocaleSync.Library.Mods;
public static class ExtensionMethods {
    [return: NotNullIfNotNull("defaultValue")]
    public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue>? dictionary, TKey key, TValue defaultValue = default) where TKey : notnull {
        if (dictionary == null) return defaultValue;
        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public static IReadOnlyDictionary<TKey, TValue> ExceptKeys<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, params TKey[] keys) where TKey : notnull
        => dictionary.Where(kvp => !keys.Contains(kvp.Key)).ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value);

    public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> factory) {
        if (dictionary.TryGetValue(key, out var existed)) return existed;

        var value = factory();
        dictionary.Add(key, value);
        return value;
    }

    public static string GetTargetPath(this ModLocaleFile file, string directory)
        => Path.Combine(directory, $"{file.Locale.Mod.Name}.{Path.GetFileNameWithoutExtension(file.FileName)}.json");
}