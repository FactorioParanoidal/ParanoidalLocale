#nullable enable
using System;
using System.Linq;
using FactorioLocaleSync.Library.Mods;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.Git;
using Serilog;
class Build : NukeBuild {
    [Parameter("Path, where paranoidal should be located")]
    readonly AbsolutePath LocalizationsFolder = RootDirectory / "ParanoidalLocale.Data" / "locale";

    [Parameter("Path, where paranoidal should be located")]
    readonly AbsolutePath ParanoidalDirectory = TemporaryDirectory / "paranoidal-git";

    [Parameter("Target locale to localize to")]
    readonly string TargetLocale = "ru";

    Target CloneParanoidalRepository => _ => _
        .Executes(() =>
        {
            try {
                if ((ParanoidalDirectory / ".git").DirectoryExists()) {
                    Log.Information("Detected existing repository, trying to reset it to remote state");
                    GitTasks.Git("fetch origin", ParanoidalDirectory);
                    GitTasks.Git("reset --hard origin/master", ParanoidalDirectory);
                    GitTasks.Git("clean -ffdx", ParanoidalDirectory);
                    return;
                }
            }
            catch (Exception e) {
                Log.Warning(e, "Failed to reset repository, deleting it and cloning again");
            }
            FileSystemTasks.EnsureCleanDirectory(ParanoidalDirectory);
            GitTasks.Git("clone https://gitlab.com/paranoidal/modpack.git/ .", ParanoidalDirectory, customLogger: (type, s) => Log.Debug(s));
        });

    Target ExtractLocalizationToJsons => _ => _
        .DependsOn(CloneParanoidalRepository)
        .Executes(() =>
        {
            var modsFolder = ParanoidalDirectory / "mods";
            Log.Information("Starting mods discovery in {TargetFolder}.", modsFolder);
            var modInfos = ModInfo.GetMods(modsFolder).ToList();
            Log.Information("Found {ModCount} mods.", modInfos.Count);

            Log.Information("Processing mods, target locale {TargetLocale}:", TargetLocale);
            var modLocalesToProcess = ModLocalizationUtils.ProcessModsToGetLocalizable(modInfos, TargetLocale, Log.Logger).ToList();
            var modsToProcess = modLocalesToProcess.GroupBy(locale => locale.Mod).Select(locales => locales.Key).ToList();
            Log.Information("Found {FilesCount} files from {ModsCount} mods to process.", modLocalesToProcess.Sum(locale => locale.Files.Count), modsToProcess.Count);

            var initialFolder = LocalizationsFolder / "initial";
            Log.Information("Writing mods to {TargetFolder}.", initialFolder);
            ModLocalizationUtils.WriteModsInitialLocaleFiles(modLocalesToProcess, initialFolder, Log.Logger);

            var dependenciesJsonPath = LocalizationsFolder / "dependencies.json";
            Log.Information("Writing localized mods to {DependenciesJsonPath}.", dependenciesJsonPath);
            ModLocalizationUtils.AppendDependentMods(modsToProcess, dependenciesJsonPath);

            var targetLocalizationsFolder = LocalizationsFolder / TargetLocale;
            Log.Information("Appending already localized string to target localizations in {TargetFolder}.", targetLocalizationsFolder);
            ModLocalizationUtils.AppendAlreadyLocalizedContent(modInfos, modLocalesToProcess, targetLocalizationsFolder, TargetLocale, Log.Logger);
        });

    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.ExtractLocalizationToJsons);
}