using CPSteamLauncher.Command;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using SteamGameInfoParser;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CPSteamLauncher.Helper {

    partial class ScoredListItem(ICommand command): ListItem(command) {
        public int MatchScore { get; set; }
        public long LastPlayed { get; set; }
    }

    internal static class GameInfoGenerator {

        private static readonly GameLibrary gameLibrary = GameLibrary.Instance;
        private static readonly LoginUserParser loginUser = LoginUserParser.Instance;

        public static string MappingGameType(string type) {
            return type switch {
                "game" => Resource.GameTypeGame,
                "application" => Resource.GameTypeApp,
                "tool" => Resource.GameTypeTool,
                _ => Resource.GameTypeGame
            };
        }

        public static DetailsElement[] CreateMetaData(GameLibrary.SteamGame game, SteamGameInfoParser.GameInfo userInfo) {
            return [
                new DetailsElement() {
                Key = game.localizationName ?? game.name,
                Data = new DetailsLink() {
                    Text = (game.localizationName != null && game.localizationName.Trim() != game.name.Trim()) ? game.name : Resource.GameDescription
                }
            },
            new DetailsElement() {
                Key = MappingGameType(game.type),
                Data = new DetailsLink() {
                    Text = Resource.GameTypeGame
                }
            },
            new DetailsElement() {
                Key = Resource.GameLastPlayed,
                Data = new DetailsLink() {
                    Text = TimeFormat.RelativeTimestamp(userInfo.LastPlayed)
                }
            },
            new DetailsElement() {
                Key = Resource.Gametime2wks,
                Data = new DetailsLink() {
                    Text = TimeFormat.RelativeTime(userInfo.Playtime2wks * 60)
                }
            },
            new DetailsElement() {
                Key = Resource.GametimeTotal,
                Data = new DetailsLink() {
                    Text = TimeFormat.RelativeTime(userInfo.Playtime * 60)
                }
            },
            new DetailsElement() {
                Key = Resource.GameId,
                Data = new DetailsLink() {
                    Text = game.id
                }
            }
            ];
        }

        public static ScoredListItem CreateGameEntry(GameLibrary.SteamGame game) {
            var isGame = game.type == "game";
            var icon = new IconInfo(game.icon);
            var userInfo = loginUser.GameInfoDict.GetValueOrDefault(game.id, new() {
                LastPlayed = 0,
                Playtime = 0,
                Playtime2wks = 0
            });
            return new ScoredListItem(new ActionCommand(() => {
                Process.Start(Path.Combine(gameLibrary.SteamPath, "steam.exe"), "steam://launch/" + game.id);
                return CommandResult.Dismiss();
            }) {
                Name = Resource.StartGameTitle,
                Icon = Icon.Start,
            }) {
                Title = game.localizationName ?? game.name,
                Subtitle = (game.localizationName != null && game.localizationName.Trim() != game.name.Trim()) ? game.name : Resource.GameDescription,
                Icon = icon,
                LastPlayed = userInfo.LastPlayed,
                Tags = [
                    new Tag { Text = GameInfoGenerator.MappingGameType(game.type) }
                ],
                Details = new Details() {
                    HeroImage = icon,
                    Metadata = GameInfoGenerator.CreateMetaData(game, userInfo)
                },
                MoreCommands = [
                    new CommandContextItem(new ActionCommand(() => {
                       Process.Start(Path.Combine(gameLibrary.SteamPath, "steam.exe"), "steam://gameproperties/" + game.id);
                       return CommandResult.Dismiss();
                    }) {
                       Icon = Icon.Settings,
                            Name = Resource.OpenGamePropsTitle
                    })
                ]
            };
        }
    }
}
