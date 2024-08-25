using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.SteamLauncher {
    public partial class Main: IPlugin, IReloadable {
        public string Name => "Steam Launcher";
        public static string PluginID => "9e13e2aa-da92-4094-84f8-6f2e2d3e90db";

        public string Description => "Launch your steam games.";

        private bool _Initialized = false;

        private string _InitialzedFailedReason = "";

        private string? SteamPath;

        private List<SteamGame> steamGames = new List<SteamGame>();


        public void Init(PluginInitContext context) {
            try {
                InitSteamData();
                _Initialized = true;
            } catch (Exception e) {
                _InitialzedFailedReason = e.Message;
                _Initialized = false;
            }
        }

        public List<Result> Query(Query query) {
            if (!_Initialized) {
                return [
                    new Result {
                        Title = "Steam Launcher can't be initialized",
                        SubTitle = _InitialzedFailedReason
                    }
                    ];
            }
            if (SteamPath == null) {
                throw new Exception("Can't detected Steam installation path.");
            }
            List<Result> results = [];
            foreach (SteamGame game in steamGames) {
                if (StringMatcher.FuzzySearch(query.Search, game.name).Success) {
                    results.Add(new Result {
                        Title = game.name,
                        SubTitle = "Steam Game",
                        IcoPath = Path.Combine(SteamPath, "appcache", "librarycache", game.id + "_icon.jpg"),
                        Action = (e) => {
                            Process.Start(Path.Combine(SteamPath, "steam.exe"), "steam://rungameid/" + game.id);
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
                _Initialized = true;
            } catch (Exception e) {
                _InitialzedFailedReason = e.Message;
                _Initialized = false;
            }
        }

        private void InitSteamData() {
            steamGames.Clear();
            SteamPath =
                Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam", "InstallPath", null)?.ToString()
                ?? Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Valve\\Steam", "InstallPath", null)?.ToString()
                ?? throw new Exception("Can't detected Steam installation path.");
            var SteamLibrariesData = File.ReadAllLines(Path.Combine(SteamPath, "config", "libraryfolders.vdf"));
            List<SteamLibrary> SteamLibraries = [];
            foreach (string line in SteamLibrariesData) {
                var m = LibraryPathMather().Match(line);
                if (m.Success) {
                    SteamLibraries.Add(new SteamLibrary(Uri.UnescapeDataString(m.Groups[1].Value)));
                }
            }

            foreach (var library in SteamLibraries) {
                steamGames.AddRange(library.GetGames());
            }
        }

        struct SteamGame {
            public string id;
            public string name;
            public string? localizationName;
        }

        partial class SteamLibrary(string _Path) {
            private readonly DirectoryInfo _library = new(Path.Combine(_Path, "steamapps"));


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
                    FoundGames.Add(new SteamGame {
                        id = id,
                        name = name,
                        localizationName = localizationName
                    });

                }
                return [.. FoundGames];
            }

            [GeneratedRegex("\"appid\"\\s*\"(\\d*)\"")]
            private static partial Regex GameIdMatcher();
            [GeneratedRegex("\"name\"\\s*\"([^\"]*)\"")]
            private static partial Regex GameNameMatcher();
        }

        [GeneratedRegex("\"path\"\\s*\"([^\"]*)\"")]
        private static partial Regex LibraryPathMather();
    }
}