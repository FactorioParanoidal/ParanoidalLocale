#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FactorioLocaleSync.Library;

public static class ExtensionMethods {
    public static Task WhenAll(this IEnumerable<Task> tasks) {
        return Task.WhenAll(tasks);
    }

    public static Task<T[]> WhenAll<T>(this IEnumerable<Task<T>> tasks) {
        return Task.WhenAll(tasks);
    }
}