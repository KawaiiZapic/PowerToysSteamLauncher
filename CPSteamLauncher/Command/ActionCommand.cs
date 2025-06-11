using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;

namespace CPSteamLauncher.Command {
    partial class ActionCommand(
            Func<CommandResult?> action
        ): InvokableCommand {
        public override ICommandResult Invoke() {
            return action.Invoke() ?? CommandResult.KeepOpen();
        }
    }
}
