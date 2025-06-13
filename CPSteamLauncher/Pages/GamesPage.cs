// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CPSteamLauncher.Command;
using CPSteamLauncher.Helper;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using SteamGameInfoParser;
using System.Diagnostics;
using System.IO;

namespace CPSteamLauncher;

internal sealed partial class GamesPage : ListPage, IDisposable {
    partial class ScoredListItem(ICommand command): ListItem(command) {
        public int MatchScore { get; set; }
        public long LastPlayed { get; set; }
    }

    private readonly GameLibrary gameLibrary = new();
    private readonly LoginUserParser loginUser;

    private IListItem[] _result = [];


    public GamesPage()
    {
        Icon = IconHelpers.FromRelativePaths("Assets/SteamLauncher.light.png", "Assets/SteamLauncher.dark.png");
        Title = Resource.PluginName;
        ShowDetails = true;
        gameLibrary.ReloadData();
        Name = "";
        loginUser = new(gameLibrary.SteamPath);
        loginUser.StartWatch();

        gameLibrary.Changed += (s, e) => {
            BuildGameList();
        };
        loginUser.Changed += (s, e) => {
            BuildGameList();
        };
        BuildGameList();
    }

    public static string MappingGameType(string type) {
        return type switch {
            "game" => Resource.GameTypeGame,
            "application" => Resource.GameTypeApp,
            "tool" => Resource.GameTypeTool,
            _ => Resource.GameTypeGame
        };
    }

    public override IListItem[] GetItems()
    {
        return _result;
    }

    private void BuildGameList() {
        var _InitializedFailedReason = gameLibrary.InitializedFailedReason;
        if (_InitializedFailedReason != null) {
            if (_InitializedFailedReason is GameLibrary.SteamNotFoundException) {
                _result = [
                    new ListItem (new NoOpCommand()) {
                        Title = Resource.SteamNotFoundTitle,
                        Subtitle = Resource.SteamNotFoundDescription
                    }
                ];
            } else {
                _result = [
                    new ListItem (new ActionCommand(() => {
                            throw _InitializedFailedReason;
                        })) {
                        Title = Resource.InitializationFailedTitle,
                        Subtitle = _InitializedFailedReason.Message,
                        Details = new Details(){
                            Body = _InitializedFailedReason.ToString()
                        }
                    }
                ];
            }
        }
        List<ScoredListItem> result = [];
        foreach (var game in gameLibrary.SteamGames) {
            var isGame = game.type == "game";
            var icon = new IconInfo(game.icon);
            var userInfo = loginUser.GameInfoDict.GetValueOrDefault(game.id, new() {
                LastPlayed = 0,
                Playtime = 0,
                Playtime2wks = 0
            });
            result.Add(new ScoredListItem(new ActionCommand(() => {
                Process.Start(Path.Combine(gameLibrary.SteamPath, "steam.exe"), "steam://launch/" + game.id);
                return CommandResult.Dismiss();
            }) {
                Name = Resource.StartGameTitle,
                Icon = new IconInfo("\xE768"),
            }) {
                Title = game.localizationName ?? game.name,
                Subtitle = (game.localizationName != null && game.localizationName.Trim() != game.name.Trim()) ? game.name : Resource.GameDescription,
                Icon = icon,
                LastPlayed = userInfo.LastPlayed,
                Tags = [
                    new Tag {
                            Text = MappingGameType(game.type)
                       }
                ],
                Details = new Details() {
                    HeroImage = icon,
                    Metadata = [
                       new DetailsElement() {
                            Key = game.localizationName ?? game.name,
                            Data = new DetailsLink() {
                                Text = (game.localizationName != null && game.localizationName.Trim() != game.name.Trim()) ? game.name : Resource.GameDescription
                            }
                       },new DetailsElement() {
                            Key = isGame ? Resource.GameLastPlayed : Resource.AppLastUsed,
                            Data = new DetailsLink() {
                                Text = TimeFormat.RelativeTimestamp(userInfo.LastPlayed)
                            }
                           },
                           new DetailsElement() {
                                Key = isGame ? Resource.Gametime2wks : Resource.AppUsetime2wks,
                                Data = new DetailsLink() {
                                    Text = TimeFormat.RelativeTime(userInfo.Playtime2wks * 60)
                                }
                           },
                           new DetailsElement() {
                                Key = isGame ? Resource.GametimeTotal : Resource.AppUsetimeTotal,
                                Data = new DetailsLink() {
                                    Text = TimeFormat.RelativeTime(userInfo.Playtime * 60)
                                }
                           },new DetailsElement() {
                                Key = Resource.GameId,
                                Data = new DetailsLink() {
                                    Text = game.id
                                }
                           }
                    ]
                },
                MoreCommands = [
                    new CommandContextItem(new ActionCommand(() => {
                            Process.Start(Path.Combine(gameLibrary.SteamPath, "steam.exe"), "steam://gameproperties/" + game.id);
                            return CommandResult.Dismiss();
                        }) {
                            Icon = new IconInfo("\xE713"),
                            Name = Resource.OpenGamePropsTitle
                        })
                ]
            });
        }
        _result = [.. result.OrderByDescending(o => o.LastPlayed)];
        RaiseItemsChanged(_result.Length);
    }

    public void Dispose() {
        gameLibrary.Dispose();
        loginUser.Dispose();
    }
}
