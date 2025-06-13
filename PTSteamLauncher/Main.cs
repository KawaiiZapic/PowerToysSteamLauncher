using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using Wox.Infrastructure;
using Wox.Plugin;
using Community.PowerToys.Run.Plugin.SteamLauncher.Localizations;
using Wox.Infrastructure.Storage;
using SteamGameInfoParser;

namespace Community.PowerToys.Run.Plugin.SteamLauncher {
    public class Main: IPlugin, IReloadable, IContextMenu, IDisposable {

        public string Name => Resource.plugin_name;
        public string Description => Resource.plugin_description;
        public static string PluginID => "9e13e2aa-da92-4094-84f8-6f2e2d3e90db";

        private GameLibrary library { get; set; } = GameLibrary.Instance;
        private readonly Dictionary<string, GameSetting> _GameSettings;
        private readonly PluginJsonStorage<Settings> _storage;

        class GameSetting {
            public bool Hidden { get; set; }
        };

        class Settings {
            public int Version { get; set; }
            public Dictionary<string, GameSetting> Data { get; set; } = [];
        }

        public Main() {
            _storage = new();

            if (!File.Exists(_storage.FilePath)) {
                var conf = _storage.Load();
                conf.Version = 1;
                conf.Data = [];

                _GameSettings = conf.Data;
                _GameSettings.Add("228980", new GameSetting { Hidden = true }); // Default hide steamwork common shared

                _storage.Save();
            }
            _GameSettings = _storage.Load().Data;
            
        }
        

        private bool _disposed;

        public void Init(PluginInitContext context) {
            ReloadData();
        }

        public List<Result> Query(Query query) {
            if (library.InitializedFailedReason != null) {
                if (library.InitializedFailedReason is GameLibrary.SteamNotFoundException) {
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
                        SubTitle = library.InitializedFailedReason.Message,
                        Action = (e) => {
                            throw library.InitializedFailedReason;
                        }
                    }
                ];
            }
            List<Result> results = [];
            var hasActionKeyword = query.ActionKeyword != "";
            foreach (var game in library.SteamGames) {
                MatchResult? rawMatch = null;
                MatchResult? localMatch = null;
                ;
                if (query.Search.Length > 0) {
                    rawMatch = StringMatcher.FuzzySearch(query.Search, game.name);
                    localMatch = game.localizationName != null ? StringMatcher.FuzzySearch(query.Search, game.localizationName) : null;
                }
                if (hasActionKeyword && query.Search.Length == 0
                    || (rawMatch?.Success == true)
                    || (localMatch?.Success == true)
                ) {
                    if (!hasActionKeyword && _GameSettings.TryGetValue(game.id, out var setting) && setting.Hidden) {
                        continue;
                    }
                    results.Add(new Result {
                        Title = game.localizationName ?? game.name,
                        SubTitle = (game.localizationName != null && game.localizationName.Trim() != game.name.Trim()) ? game.name : Resource.game_description,
                        IcoPath = game.icon,
                        ContextData = game.id,
                        Action = (e) => {
                            Process.Start(Path.Combine(library.SteamPath, "steam.exe"), "steam://launch/" + game.id);
                            return true;
                        },
                        Score = (rawMatch?.Score ?? 0) + (localMatch?.Score ?? 0),
                    });
                }
            }
            return results;
        }

        public void ReloadData() {
            library.ReloadData();
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult) {
            if (selectedResult.ContextData is not string id) {
                return [];
            }
            if (!_GameSettings.TryGetValue(id, out GameSetting? game)) {
                game = new() {
                    Hidden = false
                };
            }
            return [
                new ContextMenuResult {
                    Glyph = "\xE768",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Title = Resource.action_play,
                    Action = (e) => {
                        Process.Start(Path.Combine(library.SteamPath, "steam.exe"), "steam://launch/" + id);
                        return true;
                    }
                },
                new ContextMenuResult {
                    Glyph = "\xE713",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Title = Resource.action_options,
                    Action = (e) => {
                        Process.Start(Path.Combine(library.SteamPath, "steam.exe"), "steam://gameproperties/" + id);
                        return true;
                    }
                },
                new ContextMenuResult {
                    Glyph = game.Hidden ? "\xE890" : "\xED1A",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Title = game.Hidden ? Resource.action_unhide : Resource.action_hide,
                    Action = (e) => {
                        game.Hidden = !game.Hidden;
                        _GameSettings.TryAdd(id, game);
                        _storage.Save();
                        return true;
                    }
                }
            ];
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_disposed && disposing) {
                library.Dispose();
                _disposed = true;
            }
        }
    }
}
