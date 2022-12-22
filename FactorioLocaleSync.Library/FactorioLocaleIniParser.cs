#nullable enable
using IniParser.Parser;

namespace FactorioLocaleSync.Library;

public class FactorioLocaleIniParser : IniDataParser {
    protected override bool LineContainsAComment(string line) {
        if (string.IsNullOrWhiteSpace(line)) return true;
        if (line.StartsWith('#')) return true;
        if (line.StartsWith(';')) return true;
        return false;
    }

    protected override string ExtractComment(string line) {
        return string.Empty;
    }
}