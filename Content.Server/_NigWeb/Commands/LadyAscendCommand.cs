// SPDX-License-Identifier: MIT
using Content.Server.Administration;
using Content.Server._NigWeb.Altar;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._NigWeb.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class LadyAscendCommand : IConsoleCommand
{
    public string Command => "lady_ascend";
    public string Description => "Triggers the Flayed Lady of Sorrow Ascension effect worldwide.";
    public string Help => "Usage: lady_ascend";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var ladySys = entMan.System<LadyAltarSystem>();

        ladySys.TriggerAscensionWorldwide();
        shell.WriteLine("Flayed Lady Ascension triggered worldwide.");
    }
}
