using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using SteamGameInfoParser;
using System.Globalization;

namespace CPSteamLauncher.Helper {
    internal partial class GameLibrary: IDisposable {

        [GeneratedRegex("\"path\"\\s*\"([^\"]*)\"")]
        private static partial Regex LibraryPathMather();

        [GeneratedRegex("\"appid\"\\s*\"(\\d*)\"")]
        private static partial Regex GameIdMatcher();

        [GeneratedRegex("\"name\"\\s*\"([^\"]*)\"")]
        private static partial Regex GameNameMatcher();

        private Exception? InitializedFailedReason;

        private int _mutCounter;
        private readonly Mutex _mutex = new(false);

        private readonly string SteamPath;
        private readonly string PreferredLang;
        private bool _disposed;
        private readonly List<SteamGame> steamGames = [];
        private readonly List<FileSystemWatcher> _fileSystemWatchers = [];

        private Dictionary<uint, App> appList;


        private readonly LoginUserParser loginUser;

        public Dictionary<string, GameInfo> GameInfo { get => loginUser.GameInfoDict; }

        public GameLibrary() {
            SteamPath =
                Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam", "InstallPath", null)?.ToString()
                ?? Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Valve\\Steam", "InstallPath", null)?.ToString()
                ?? throw new SteamNotFoundException();

            PreferredLang =
                Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam", "Language", null)?.ToString()
                ?? Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Valve\\Steam", "Language", null)?.ToString()
                ?? "english";

            loginUser = new(SteamPath);
            loginUser.StartWatch();
            appList = [];
        }


        public Exception? GetInitializedFailedReason() {
            return InitializedFailedReason;
        }

        public string GetSteamPath() {
            return SteamPath;
        }

        public List<SteamGame> GetSteamGames() {
            return steamGames;
        }

        public static string MappingGameType(string type) {
            return type switch {
                "game" => Resource.GameTypeGame,
                "application" => Resource.GameTypeApp,
                "tool" => Resource.GameTypeTool,
                _ => Resource.GameTypeGame
            };
        }


        public SteamGame[] GetGames(string path) {
            var _library = new DirectoryInfo(path);
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
                if (id == null || name == null) {
                    continue;
                }
                string? localizationName = null;
                string? icon = null;
                string? large_icon = null;
                string type = "game";

                var appDetail = appList!.GetValueOrDefault(uint.Parse(id), null);
                if (appDetail != null) {
                    var common = appDetail.Data.common;
                    type = common.type;
                    icon = Path.Combine(SteamPath, "steam", "games", common.clienticon + ".ico");
                    if (!File.Exists(icon)) {
                        icon = $"https://steamcdn-a.akamaihd.net/steamcommunity/public/images/apps/{id}/{common.clienticon}.ico";
                    }

                    large_icon = Path.Combine(SteamPath, "appcache", "librarycache", id, "header.jpg");
                    if (!File.Exists(large_icon)) {
                        large_icon = icon;
                    }
                    if (common.name_localized != null) { 
                        localizationName = common.name_localized!.GetValueOrDefault(PreferredLang, null);
                    }
                }
                FoundGames.Add(new SteamGame {
                    id = id,
                    name = name,
                    icon = icon,
                    type = type.ToLower(CultureInfo.InvariantCulture),
                    large_icon = large_icon,
                    localizationName = localizationName
                });

            }
            return [.. FoundGames];
        }

        private void CleanFSWatcher() {
            foreach (var watcher in _fileSystemWatchers) {
                watcher.Dispose();
            }
            _fileSystemWatchers.Clear();
        }

        public void ReloadData() {
            try {
                InitSteamData();
                InitializedFailedReason = null;
            } catch (Exception e) {
                InitializedFailedReason = e;
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

            CleanFSWatcher();
            steamGames.Clear();
            
            appList = LibraryVdfParser.Read(Path.Combine(SteamPath, "appcache", "appinfo.vdf"));

            var libWatcher = new FileSystemWatcher(Path.Combine(SteamPath, "config")) {
                Filter = "libraryfolders.vdf",
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };
            libWatcher.Created += (object sender, FileSystemEventArgs e) => InitSteamData();
            libWatcher.Deleted += (object sender, FileSystemEventArgs e) => InitSteamData();
            _fileSystemWatchers.Add(libWatcher);

            var SteamLibrariesData = File.ReadAllLines(Path.Combine(SteamPath, "config", "libraryfolders.vdf"));
            List<string> SteamLibraries = [];
            foreach (string line in SteamLibrariesData) {
                var m = LibraryPathMather().Match(line);
                if (m.Success) {
                    var path = Path.Combine(Uri.UnescapeDataString(m.Groups[1].Value), "steamapps");
                    SteamLibraries.Add(path);
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
                steamGames.AddRange(GetGames(library));
            }
            _mutex.ReleaseMutex();

        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_disposed && disposing) {
                CleanFSWatcher();
                _mutex.Dispose();
                loginUser.Dispose();
                _disposed = true;
            }
        }

        public struct SteamGame {
            public string id;
            public string name;
            public string type;
            public string? icon;
            public string? large_icon;
            public string? localizationName;
        }

        public class SteamNotFoundException: Exception { }

        class GameSetting {
            public bool Hidden { get; set; }
        };

        class Settings {
            public int Version { get; set; }
            public Dictionary<string, GameSetting> Data { get; set; } = [];
        }

    }
}
