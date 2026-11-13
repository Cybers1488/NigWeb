// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Components;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Shared._NigWeb.Drugs;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.StatusEffect;
using Robust.Shared.Timing;

namespace Content.Server._NigWeb.Drugs;

/// <summary>
/// Monitors bloodstreams for Ephedrine and maintains ApocalypseTripComponent.
/// </summary>
public sealed class ApocalypseTripServerSystem : EntitySystem
{
    [Dependency] private readonly SolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly ReagentId EphedrineId = new("Ephedrine", null);
    private TimeSpan _nextCheck = TimeSpan.Zero;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        // Check all bloodstreams for Ephedrine
        var bloodQuery = EntityQueryEnumerator<BloodstreamComponent>();
        while (bloodQuery.MoveNext(out var uid, out var bloodstream))
        {
            var hasEphedrine = false;

            if (_solutionContainer.TryGetSolution(uid, bloodstream.BloodSolutionName, out _, out var solution))
            {
                if (solution.TryGetReagentQuantity(EphedrineId, out var qty) && qty > 0.05f)
                    hasEphedrine = true;
            }

            var hasStatus = _statusEffects.HasStatusEffect(uid, "ApocalypseTrip");

            if (hasEphedrine || hasStatus)
            {
                if (!HasComp<ApocalypseTripComponent>(uid))
                    AddComp<ApocalypseTripComponent>(uid);
            }
            else
            {
                if (HasComp<ApocalypseTripComponent>(uid))
                    RemComp<ApocalypseTripComponent>(uid);
            }
        }
    }
}
