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

namespace CPSteamLauncher;

internal sealed partial class GamesPage : ListPage, IDisposable {

    private readonly GameLibrary gameLibrary = GameLibrary.Instance;
    private readonly LoginUserParser loginUser = LoginUserParser.Instance;

    private IListItem[] _result = [];

    public GamesPage()
    {
        Icon = IconHelpers.FromRelativePaths("Assets/SteamLauncher.light.png", "Assets/SteamLauncher.dark.png");
        Title = Resource.PluginName;
        ShowDetails = true;
        Name = "";

        gameLibrary.Changed += (s, e) => {
            BuildGameList();
        };
        loginUser.Changed += (s, e) => {
            BuildGameList();
        };
        BuildGameList();
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
        List<Helper.ScoredListItem> result = [];
        foreach (var game in gameLibrary.SteamGames) {
            GameInfoGenerator.CreateGameEntry(game);
        }
        _result = [.. result.OrderByDescending(o => o.LastPlayed)];
        RaiseItemsChanged(_result.Length);
    }

    public void Dispose() {
        gameLibrary.Dispose();
        loginUser.Dispose();
    }
}
