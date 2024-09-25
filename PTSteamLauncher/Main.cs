using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Wox.Infrastructure;
using Wox.Plugin;
using Community.PowerToys.Run.Plugin.SteamLauncher.Localizations;

namespace Community.PowerToys.Run.Plugin.SteamLauncher {
    public partial class Main: IPlugin, IReloadable, IContextMenu, IDisposable {

        [GeneratedRegex("\"path\"\\s*\"([^\"]*)\"")]
        private static partial Regex LibraryPathMather();
        public string Name => Resource.plugin_name;
        public string Description => Resource.plugin_description;
        public static string PluginID => "9e13e2aa-da92-4094-84f8-6f2e2d3e90db";

        private Exception? _InitialzedFailedReason;

        private string SteamPath = "";

        private readonly List<SteamGame> steamGames = [];
        private readonly List<FileSystemWatcher> _fileSystemWatchers = [];

        private int _mutCounter = 0;
        private readonly Mutex _mutex = new(false);
        

        private bool _disposed;

        public void Init(PluginInitContext context) {
            ReloadData();
        }

        public List<Result> Query(Query query) {
            if (_InitialzedFailedReason != null) {
                if (_InitialzedFailedReason is SteamNotFoundException) {
                    return [
                        new Result {
                            Title = Resource.steam_not_found_title,
                            SubTitle = Resource.steam_not_found_description
                        }
                    ];
                }
                return [
                    new Result {
                        Title = Resource.initialization_failed_title,
                        SubTitle = _InitialzedFailedReason.Message,
                        Action = (e) => {
                            throw _InitialzedFailedReason;
                        }
                    }
                ];
            }
            List<Result> results = [];
            foreach (SteamGame game in steamGames) {
                if (StringMatcher.FuzzySearch(query.Search, game.name).Success || (game.localizationName != null && StringMatcher.FuzzySearch(query.Search, game.localizationName).Success)) {
                    results.Add(new Result {
                        Title = game.localizationName ?? game.name,
                        SubTitle = (game.localizationName != null && game.localizationName.Trim() != game.name.Trim()) ? game.name : Resource.game_description,
                        IcoPath = game.icon,
                        ContextData = game.id,
                        Action = (e) => {
                            Process.Start(Path.Combine(SteamPath, "steam.exe"), "steam://launch/" + game.id);
                            return true;
                        }
                    });
                }
            }
            return results;
        }

        public void ReloadData() {
            try {
                InitSteamData();
                _InitialzedFailedReason = null;
            } catch (Exception e) {
                _InitialzedFailedReason = e;
            }
        }

        private void InitSteamData() {
            _mutCounter += 1;
            Thread.Sleep(1000);
            _mutex.WaitOne();
            _mutCounter -= 1;
            if (_mutCounter != 0) {
                _mutex.ReleaseMutex();
                return;
            }

            CleanFSWathcer();
            steamGames.Clear();
            SteamPath =
                Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam", "InstallPath", null)?.ToString()
                ?? Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Valve\\Steam", "InstallPath", null)?.ToString()
                ?? throw new SteamNotFoundException();

            var libWatcher = new FileSystemWatcher(Path.Combine(SteamPath, "config")) {
                Filter = "libraryfolders.vdf",
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };
            libWatcher.Created += (object sender, FileSystemEventArgs e) => InitSteamData();
            libWatcher.Deleted += (object sender, FileSystemEventArgs e) => InitSteamData();
            _fileSystemWatchers.Add(libWatcher);

            var SteamLibrariesData = File.ReadAllLines(Path.Combine(SteamPath, "config", "libraryfolders.vdf"));
            List<SteamLibrary> SteamLibraries = [];
            foreach (string line in SteamLibrariesData) {
                var m = LibraryPathMather().Match(line);
                if (m.Success) {
                    var path = Path.Combine(Uri.UnescapeDataString(m.Groups[1].Value), "steamapps");
                    SteamLibraries.Add(new SteamLibrary(SteamPath, path));
                    var watcher = new FileSystemWatcher(path) {
                        Filter = "appmanifest_*.acf",
                        EnableRaisingEvents = true,
                        IncludeSubdirectories = false
                    };
                    watcher.Created += (object sender, FileSystemEventArgs e) => InitSteamData();
                    watcher.Deleted += (object sender, FileSystemEventArgs e) => InitSteamData();
                    _fileSystemWatchers.Add(watcher);
                }
            }

            foreach (var library in SteamLibraries) {
                steamGames.AddRange(library.GetGames());
            }
            _mutex.ReleaseMutex();

        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult) {
            if (selectedResult.ContextData is not string id) {
                return [];
            }
            return [
                new ContextMenuResult {
                    Glyph = "\xE768",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Title = Resource.action_play,
                    Action = (e) => {
                        Process.Start(Path.Combine(SteamPath, "steam.exe"), "steam://launch/" + id);
                        return true;
                    }
                },
                new ContextMenuResult {
                    Glyph = "\xE713",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Title = Resource.action_options,
                    Action = (e) => {
                        Process.Start(Path.Combine(SteamPath, "steam.exe"), "steam://gameproperties/" + id);
                        return true;
                    }
                },
            ];
        }

        private void CleanFSWathcer() {
            foreach (var watcher in _fileSystemWatchers) {
                watcher.Dispose();
            }
            _fileSystemWatchers.Clear();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_disposed && disposing) {
                CleanFSWathcer();
                _disposed = true;
            }
        }
    }

    struct SteamGame {
        public string id;
        public string name;
        public string icon;
        public string? localizationName;
    }

    partial class SteamLibrary(string SteamPath, string _Path) {

        [GeneratedRegex("\"appid\"\\s*\"(\\d*)\"")]
        private static partial Regex GameIdMatcher();
        [GeneratedRegex("\"name\"\\s*\"([^\"]*)\"")]
        private static partial Regex GameNameMatcher();

        private readonly DirectoryInfo _library = new(_Path);

        private static readonly List<string> InternalBlockList = [
            "228980" // Steamworks Shared
        ];

        public SteamGame[] GetGames() {
            _library.Refresh();
            if (!_library.Exists) {
                return [];
            }
            var FoundGames = new List<SteamGame>();
            var games = _library.GetFiles("appmanifest_*.acf");

            foreach (var game in games) {
                var GameInfo = File.ReadAllText(game.FullName);
                var id = GameIdMatcher().Match(GameInfo).Groups[1]?.Value ?? null;
                var name = GameNameMatcher().Match(GameInfo).Groups[1]?.Value ?? null;
                if (id == null || name == null || InternalBlockList.Contains(id)) {
                    continue;
                }
                var localizationName = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App " + id, "DisplayName", null)?.ToString();
                var icon = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App " + id, "DisplayIcon", null)?.ToString();
                if (!File.Exists(icon)) {
                    icon = Path.Combine(SteamPath, "appcache", "librarycache", id + "_icon.jpg");
                }
                FoundGames.Add(new SteamGame {
                    id = id,
                    name = name,
                    icon = icon,
                    localizationName = localizationName
                });

            }
            return [.. FoundGames];
        }
    }

    class SteamNotFoundException : Exception { }
}
