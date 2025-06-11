// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CPSteamLauncher;

public partial class CPSteamLauncherCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;

    public CPSteamLauncherCommandsProvider()
    {
        DisplayName = Resource.PluginName;
        Icon = IconHelpers.FromRelativePaths("Assets/SteamLauncher.light.png", "Assets/SteamLauncher.dark.png");
        _commands = [
            new CommandItem(new GamesPage()) { 
                Title = DisplayName, 
                Subtitle = Resource.PluginDescription, 
                Icon = IconHelpers.FromRelativePaths("Assets/SteamLauncher.light.png", "Assets/SteamLauncher.dark.png") 
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

}
