#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IniParser;
using IniParser.Model;

namespace FactorioLocaleSync.Library;

public static class LocalizationProcessor
{
    public const string DefaultSectionKey = $"[{nameof(FactorioLocaleSync)}.{nameof(DefaultSectionKey)}]";
    private static readonly FileIniDataParser FileIniDataParser = new(new FactorioLocaleIniParser());

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadFromFile(string filePath)
    {
        var data = FileIniDataParser.ReadFile(filePath, Encoding.UTF8);
        var dataEnumerable = data.Sections
            .Select(sectionData => (sectionData.SectionName, sectionData.Keys.AsEnumerable()));
        if (data.Global.Count != 0) dataEnumerable = dataEnumerable.Prepend((DefaultSectionKey, data.Global));

        return dataEnumerable.ToDictionary(sectionData => sectionData.SectionName,
            tuple => GetSectionsFromIni(tuple.Item2));

        IReadOnlyDictionary<string, string> GetSectionsFromIni(IEnumerable<KeyData> sectionData)
        {
            return sectionData.ToDictionary(keyData => keyData.KeyName, keyData => keyData.Value);
        }
    }

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Merge(
        IEnumerable<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> sections)
    {
        return sections
            .SelectMany(dictionary => dictionary)
            .GroupBy(section => section.Key)
            .ToDictionary(section => section.Key, MergeGroupedSections);

        IReadOnlyDictionary<string, string> MergeGroupedSections(
            IEnumerable<KeyValuePair<string, IReadOnlyDictionary<string, string>>> grouping)
        {
            return grouping
                .SelectMany(pair => pair.Value)
                .GroupBy(pair => pair.Key)
                .Select(pairs => pairs.Last())
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }

    public static bool ContentLocalized(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> source,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> target)
    {
        foreach (var s in source)
        {
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