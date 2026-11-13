// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Console;

namespace Content.Client._NigWeb.Nuke;

public sealed class HotlineNukeClientCommand : IConsoleCommand
{
    public string Command => "nukeblast_client";
    public string Description => "Triggers the Hotline Miami nuclear explosion cinematic locally for testing.";
    public string Help => "nukeblast_client";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var sys = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<HotlineNukeSystem>();
        sys.TriggerCinematic();
        shell.WriteLine("Hotline Miami nuclear blast triggered locally.");
    }
}
