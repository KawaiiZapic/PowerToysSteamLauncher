using Microsoft.Win32;
using System.Globalization;

namespace SteamGameInfoParser {
    public class GameLibrary: IDisposable {

        public Exception? InitializedFailedReason { get; private set; }
        public string SteamPath { get; private set; }
        public List<SteamGame> SteamGames { get; private set; } = [];

        private bool _disposed;

        private readonly string PreferredLang;
        private Dictionary<uint, App> AppList;

        private readonly LibraryFolderVdfParser gameLibrary;

        public event EventHandler? Changed;

        private static GameLibrary? _instance;

        public static GameLibrary Instance {
            get {
                _instance ??= new GameLibrary();
                return _instance;
            }
        }

        private GameLibrary() {
            SteamPath =
                Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam", "InstallPath", null)?.ToString()
                ?? Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Valve\\Steam", "InstallPath", null)?.ToString()
                ?? throw new SteamNotFoundException();

            PreferredLang =
                Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam", "Language", null)?.ToString()
                ?? Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Valve\\Steam", "Language", null)?.ToString()
                ?? "english";

            gameLibrary = new(SteamPath);
            gameLibrary.Changed += (sender, e) => {
                ReloadData();
            };
            gameLibrary.StartWatch();

            AppList = [];
            InitData();
        }

        public void InitData() {
            try {
                ReloadData();
                InitializedFailedReason = null;
            } catch (Exception e) {
                InitializedFailedReason = e;
            }
        }

        public void ReloadData() {
            AppList = LibraryVdfParser.Read(Path.Combine(SteamPath, "appcache", "appinfo.vdf"));

            SteamGames.Clear();

            foreach (var id in gameLibrary.apps) {
                string? localizationName = null;
                string? icon = null;
                string? large_icon = null;
                string name = id;
                string type = "game";

                var appDetail = AppList!.GetValueOrDefault(uint.Parse(id, CultureInfo.InvariantCulture), null);
                if (appDetail != null) {
                    var common = appDetail.Data.common;

                    type = common.type;
                    name = common.name;
                    icon = Path.Combine(SteamPath, "steam", "games", common.clienticon + ".ico");
                    if (!File.Exists(icon)) {
                        if (Directory.Exists(Path.Combine(SteamPath, "steam", "games"))) {
                            Task.Run(async () => {
                                try {
                                    using var client = new HttpClient();
                                    var res = await client.GetAsync($"https://steamcdn-a.akamaihd.net/steamcommunity/public/images/apps/{id}/{common.clienticon}.ico");
                                    if (res.IsSuccessStatusCode) {
                                        await res.Content.CopyToAsync(new FileStream(icon, FileMode.CreateNew));
                                    }
                                } finally { }
                            });
                        }

                    }
                    large_icon = Path.Combine(SteamPath, "appcache", "librarycache", id, "header.jpg");
                    if (!File.Exists(large_icon)) {
                        large_icon = icon;
                    }
                    if (common.name_localized != null) {
                        localizationName = common.name_localized!.GetValueOrDefault(PreferredLang, null);
                    }
                }

                SteamGames.Add(new SteamGame {
                    id = id,
                    name = name,
                    icon = icon,
                    type = type.ToLower(CultureInfo.InvariantCulture),
                    large_icon = large_icon,
                    localizationName = localizationName
                });
            }
            Changed?.Invoke(this, new());
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_disposed && disposing) {
                gameLibrary.Dispose();
                _instance = null;
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
