#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
public static class ModMetaInfoUtils {
    public static IEnumerable<string> GetAndProcessDependencies(string dependenciesJsonPath) {
        var dependenciesJsonText = File.ReadAllText(dependenciesJsonPath);
        var dependenciesNames = JsonSerializer.Deserialize<IEnumerable<string>>(dependenciesJsonText)!;
        var dependencies = dependenciesNames.Select(s => $"? {s}")
            .Prepend("base >= 1.1.0");
        return dependencies;
    }
}