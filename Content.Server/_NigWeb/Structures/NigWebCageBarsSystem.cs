using Content.Shared.Destructible;
using Robust.Server.GameObjects;

namespace Content.Server._NigWeb.Structures;

public sealed class NigWebCageBarsSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[NigWebCageBarsSystem] Initiating NigWebCageBarsSystem...");
        SubscribeLocalEvent<NigWebCageBarsComponent, BreakageEventArgs>(OnBreakage);
    }

    private void OnBreakage(EntityUid uid, NigWebCageBarsComponent component, BreakageEventArgs args)
    {
        Log.Debug($"[NigWebCageBarsSystem] Processing Entity {uid} for breakage...");

        if (Deleted(uid) || EntityManager.IsQueuedForDeletion(uid))
            return;

        if (!TryComp<TransformComponent>(uid, out var xform))
        {
            Log.Error($"[NigWebCageBarsSystem] Failed to find component TransformComponent for {uid}");
            return;
        }

        var broken = Spawn(component.BrokenPrototype, xform.Coordinates);
        _transform.SetLocalRotation(broken, xform.LocalRotation);
        Log.Info($"[NigWebCageBarsSystem] Replaced {uid} with broken cage bars {broken} at {xform.Coordinates} with rotation {xform.LocalRotation}");

        QueueDel(uid);
    }
}
