using Content.Shared._NigWeb.Lifeweb;
using Robust.Client.GameObjects;

namespace Content.Client._NigWeb.Lifeweb
{
    public sealed class LifewebStationBatteryVisualsSystem : VisualizerSystem<LifewebStationBatteryComponent>
    {
        protected override void OnAppearanceChange(EntityUid uid, LifewebStationBatteryComponent component, ref AppearanceChangeEvent args)
        {
            if (args.Sprite == null)
                return;

            if (AppearanceSystem.TryGetData<string>(uid, LifewebStationBatteryVisuals.State, out var state, args.Component))
            {
                args.Sprite.LayerSetState(LifewebStationBatteryVisuals.State, state);
            }
        }
    }
}
