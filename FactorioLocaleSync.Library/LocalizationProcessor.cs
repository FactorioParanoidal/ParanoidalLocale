#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IniParser;
using IniParser.Model;

namespace FactorioLocaleSync.Library;

public class LocalizationProcessor {
    private static readonly FileIniDataParser FileIniDataParser = new(new FactorioLocaleIniParser());

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadFromFile(string filePath) {
        var data = FileIniDataParser.ReadFile(filePath, Encoding.UTF8);

        return data.Sections.ToDictionary(sectionData => sectionData.SectionName, GetSectionsFromIni);

        IReadOnlyDictionary<string, string> GetSectionsFromIni(SectionData sectionData) {
            return sectionData.Keys.ToDictionary(keyData => keyData.KeyName, keyData => keyData.Value);
        }
    }

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Merge(IEnumerable<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> sections) {
        return sections
            .SelectMany(dictionary => dictionary)
            .GroupBy(section => section.Key)
            .ToDictionary(section => section.Key, pairs => MergeGroupedSections(pairs));

        IReadOnlyDictionary<string, string> MergeGroupedSections(IEnumerable<KeyValuePair<string, IReadOnlyDictionary<string, string>>> grouping) {
            return grouping
                .SelectMany(pair => pair.Value)
                .GroupBy(pair => pair.Key)
                .Select(pairs => pairs.Last())
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }

    public static bool ContentLocalized(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> source, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> target) {
        foreach (var s in source) {
            if (!target.ContainsKey(s.Key)) return false;

            var innerDictS = s.Value;
            var innerDictT = target[s.Key];
            foreach (var iS in innerDictS)
                if (!innerDictT.ContainsKey(iS.Key))
                    return false;
        }

        return true;
    }
}