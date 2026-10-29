// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Shared._NigWeb.Bosses.EternalSummoner;
using Robust.Client.GameObjects;

namespace Content.Client._NigWeb.Bosses.EternalSummoner;

public sealed class EternalSummonerVisualsSystem : VisualizerSystem<EternalSummonerComponent>
{
    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[EternalSummonerVisualsSystem] Initiating Eternal Summoner visual system...");
    }

    protected override void OnAppearanceChange(EntityUid uid, EternalSummonerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<EternalSummonerVisualState>(uid, EternalSummonerVisuals.VisualState, out var state, args.Component))
        {
            string headState;
            if (state.IsDead)
            {
                headState = "death";
            }
            else
            {
                headState = state.HeadState switch
                {
                    EternalHeadState.ElderIdle => "elder_idle",
                    EternalHeadState.ElderToMask => "elder_to_mask",
                    EternalHeadState.MaskIdle => "mask_idle",
                    EternalHeadState.MaskToElder => "mask_to_elder",
                    EternalHeadState.MaskToSkull => "mask_to_skull",
                    EternalHeadState.SkullIdle => "skull_idle",
                    _ => "elder_idle"
                };
            }

            if (args.Sprite.AllLayers.Count() >= 2)
            {
                args.Sprite.LayerSetState(0, "body_idle");
                args.Sprite.LayerSetState(1, headState);
                Log.Debug($"[EternalSummonerVisualsSystem] Updated {uid}: Body=body_idle, Head={headState}");
            }
            else
            {
                string compositeState = state.HeadState switch
                {
                    EternalHeadState.SkullIdle or EternalHeadState.MaskToSkull => "death",
                    EternalHeadState.MaskIdle or EternalHeadState.MaskToElder => "composite_mask_idle",
                    _ => "composite_elder_idle"
                };
                args.Sprite.LayerSetState(0, compositeState);
                Log.Debug($"[EternalSummonerVisualsSystem] Updated {uid} composite: {compositeState}");
            }
        }
    }
}
