using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FactorioLocaleSync.Library.Mods;

public class ModInfoJson {
    [JsonPropertyName("name")]
    public string InternalName { get; set; } = null!;

    [JsonPropertyName("version")]
    public Version Version { get; set; } = null!;

    [JsonPropertyName("factorio_version")]
    public Version? FactorioVersion { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("author")]
    public string Author { get; set; } = null!;

    [JsonPropertyName("contact")]
    public string? Contact { get; set; }

    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    [JsonPropertyOrder(100)]
    [JsonPropertyName("dependencies")]
    public List<string>? Dependencies { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    public static ModInfoJson FromFile(string filePath) {
        using var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read);
        return JsonSerializer.Deserialize<ModInfoJson>(fileStream)!;
    }
}