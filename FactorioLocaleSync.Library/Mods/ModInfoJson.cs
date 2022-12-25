using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FactorioLocaleSync.Library.Mods;

public class ModInfoJson {
    [JsonPropertyName("Name")]
    public string InternalName { get; set; } = null!;

    public Version Version { get; set; } = null!;
    public Version? FactorioVersion { get; set; }
    public string Title { get; set; } = null!;
    public string Author { get; set; } = null!;
    public string? Contact { get; set; }
    public string? Homepage { get; set; }
    public List<string>? Dependencies { get; set; }
    public string? Description { get; set; }

    public static ModInfoJson FromFile(string filePath) {
        using var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read);
        return JsonSerializer.Deserialize<ModInfoJson>(fileStream)!;
    }
}