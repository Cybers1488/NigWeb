// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Gameplay;
using Content.Client._Shitmed.UserInterface.Systems.Targeting.Widgets;
using Content.Shared._Shitmed.Targeting;
using Content.Client._Shitmed.Targeting;
using Content.Shared._Shitmed.Targeting.Events;
using Content.Shared.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.Player;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;

namespace Content.Client._Shitmed.UserInterface.Systems.Targeting;

public sealed class TargetingUIController : UIController,
    IOnStateEntered<GameplayState>,
    IOnStateExited<GameplayState>,
    IOnSystemChanged<TargetingSystem>
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IEntityNetworkManager _net = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private TargetingComponent? _targetingComponent;
    private TargetingControl? TargetingControl => UIManager.GetActiveUIWidgetOrNull<TargetingControl>();

    public void OnSystemLoaded(TargetingSystem system)
    {
        system.TargetingStartup += AddTargetingControl;
        system.TargetingShutdown += RemoveTargetingControl;
        system.TargetChange += CycleTarget;
    }

    public void OnSystemUnloaded(TargetingSystem system)
    {
        system.TargetingStartup -= AddTargetingControl;
        system.TargetingShutdown -= RemoveTargetingControl;
        system.TargetChange -= CycleTarget;
    }

    public void OnStateEntered(GameplayState state)
    {
        if (TargetingControl == null)
            return;

        TargetingControl.SetTargetDollVisible(_targetingComponent != null);

        if (_targetingComponent != null)
            TargetingControl.SetBodyPartsVisible(_targetingComponent.Target);

        // NigWeb: Bind arrow keys for targeting navigation (State type, only fire on Down)
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.TargetNavUp,    InputCmdHandler.FromDelegate(_ => NavigateTarget(Direction.North), null, false))
            .Bind(ContentKeyFunctions.TargetNavDown,  InputCmdHandler.FromDelegate(_ => NavigateTarget(Direction.South), null, false))
            .Bind(ContentKeyFunctions.TargetNavLeft,  InputCmdHandler.FromDelegate(_ => NavigateTarget(Direction.West),  null, false))
            .Bind(ContentKeyFunctions.TargetNavRight, InputCmdHandler.FromDelegate(_ => NavigateTarget(Direction.East),  null, false))
            .Register<TargetingUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<TargetingUIController>();
    }

    public void AddTargetingControl(TargetingComponent component)
    {
        _targetingComponent = component;

        if (TargetingControl != null)
        {
            TargetingControl.SetTargetDollVisible(_targetingComponent != null);

            if (_targetingComponent != null)
                TargetingControl.SetBodyPartsVisible(_targetingComponent.Target);
        }
    }

    public void RemoveTargetingControl()
    {
        if (TargetingControl != null)
            TargetingControl.SetTargetDollVisible(false);

        _targetingComponent = null;
    }

    public void CycleTarget(TargetBodyPart bodyPart)
    {
        if (_playerManager.LocalEntity is not { } user
            || _entManager.GetComponent<TargetingComponent>(user) is not { } targetingComponent
            || TargetingControl == null)
            return;

        var player = _entManager.GetNetEntity(user);
        if (bodyPart != targetingComponent.Target)
        {
            var msg = new TargetChangeEvent(player, bodyPart);
            _net.SendSystemNetworkMessage(msg);
            TargetingControl?.SetBodyPartsVisible(bodyPart);
        }
    }

    // NigWeb: Arrow key navigation map for targeting doll
    // RightArm is on LEFT side of screen, LeftArm is on RIGHT side
    // RightLeg is on LEFT side of screen, LeftLeg is on RIGHT side
    private static readonly Dictionary<TargetBodyPart, (TargetBodyPart? Up, TargetBodyPart? Down, TargetBodyPart? Left, TargetBodyPart? Right)> NavMap = new()
    {
        { TargetBodyPart.Head,      (null,                   TargetBodyPart.Chest,     null,                    null)                    },
        { TargetBodyPart.Chest,     (TargetBodyPart.Head,    TargetBodyPart.Groin,     TargetBodyPart.RightArm, TargetBodyPart.LeftArm)  },
        { TargetBodyPart.Groin,     (TargetBodyPart.Chest,   null,                     TargetBodyPart.RightLeg, TargetBodyPart.LeftLeg)  },
        // LeftArm is on RIGHT side of screen → press LEFT to go back to chest
        { TargetBodyPart.LeftArm,   (null,                   TargetBodyPart.LeftHand,  TargetBodyPart.Chest,    null)                    },
        { TargetBodyPart.LeftHand,  (TargetBodyPart.LeftArm, null,                     null,                    null)                    },
        // RightArm is on LEFT side of screen → press RIGHT to go back to chest
        { TargetBodyPart.RightArm,  (null,                   TargetBodyPart.RightHand, null,                    TargetBodyPart.Chest)    },
        { TargetBodyPart.RightHand, (TargetBodyPart.RightArm,null,                     null,                    null)                    },
        // LeftLeg is on RIGHT side of screen → press LEFT to go back to groin
        { TargetBodyPart.LeftLeg,   (TargetBodyPart.Groin,   TargetBodyPart.LeftFoot,  TargetBodyPart.Groin,    null)                    },
        { TargetBodyPart.LeftFoot,  (TargetBodyPart.LeftLeg, null,                     null,                    null)                    },
        // RightLeg is on LEFT side of screen → press RIGHT to go back to groin
        { TargetBodyPart.RightLeg,  (TargetBodyPart.Groin,   TargetBodyPart.RightFoot, null,                    TargetBodyPart.Groin)    },
        { TargetBodyPart.RightFoot, (TargetBodyPart.RightLeg,null,                     null,                    null)                    },
    };

    private void NavigateTarget(Direction dir)
    {
        if (_playerManager.LocalEntity is not { } user
            || !_entManager.TryGetComponent<TargetingComponent>(user, out var targetingComponent))
            return;

        if (!NavMap.TryGetValue(targetingComponent.Target, out var nav))
            return;

        TargetBodyPart? next = dir switch
        {
            Direction.North => nav.Up,
            Direction.South => nav.Down,
            Direction.West  => nav.Left,
            Direction.East  => nav.Right,
            _ => null
        };

        if (next.HasValue)
            CycleTarget(next.Value);
    }
}
