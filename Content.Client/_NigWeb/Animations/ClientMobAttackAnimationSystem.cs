// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._NigWeb.Animations;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Animations;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Client._NigWeb.Animations;

public sealed class ClientMobAttackAnimationSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AnimationPlayerSystem _animPlayer = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        Log.Info("[MobAttackAnimationSystem] Initiating...");

        SubscribeNetworkEvent<MeleeLungeEvent>(OnMeleeLunge);
        SubscribeLocalEvent<MobAttackAnimationComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<MobAttackAnimationComponent, AnimationCompletedEvent>(OnAnimComplete);
    }

    private void OnMeleeHit(Entity<MobAttackAnimationComponent> ent, ref MeleeHitEvent args)
    {
        if (args.User != ent.Owner)
            return;

        Log.Debug($"[MobAttackAnimationSystem] OnMeleeHit Processing Entity {ent.Owner}");
        PlayAttackAnimation(ent.Owner, ent.Comp);
    }

    private void OnMeleeLunge(MeleeLungeEvent ev)
    {
        var user = GetEntity(ev.Entity);
        if (!TryComp<MobAttackAnimationComponent>(user, out var comp))
            return;

        // Skip if this is local player since OnMeleeHit already processed it locally
        if (TryComp<ActorComponent>(user, out var actor) && actor.PlayerSession == _playerManager.LocalSession)
            return;

        Log.Debug($"[MobAttackAnimationSystem] OnMeleeLunge Processing Entity {user}");
        PlayAttackAnimation(user, comp);
    }

    private void PlayAttackAnimation(EntityUid uid, MobAttackAnimationComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
        {
            Log.Error($"[MobAttackAnimationSystem] Failed to find SpriteComponent for {uid}");
            return;
        }

        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Dead)
            return;

        if (comp.AttackStates.Count == 0)
            return;

        var state = _random.Pick(comp.AttackStates);

        var anim = new Animation
        {
            Length = TimeSpan.FromSeconds(comp.AnimationLength),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = comp.LayerKey,
                    KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(state, 0f) }
                }
            }
        };

        var player = EnsureComp<AnimationPlayerComponent>(uid);
        _animPlayer.Stop(uid, comp.AnimationKey);
        _animPlayer.Play(uid, anim, comp.AnimationKey);
    }

    private void OnAnimComplete(Entity<MobAttackAnimationComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != ent.Comp.AnimationKey)
            return;

        Log.Debug($"[MobAttackAnimationSystem] Animation completed on {ent.Owner}");

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        sprite.LayerSetAutoAnimated(ent.Comp.LayerKey, true);

        // Restore base movement layer (walk or idle) if mob uses SpriteMovement
        if (TryComp<SpriteMovementComponent>(ent, out var move))
        {
            var dict = move.IsMoving ? move.MovementLayers : move.NoMovementLayers;
            if (dict.TryGetValue(ent.Comp.LayerKey, out var layerData))
            {
                _sprite.LayerSetData((ent.Owner, sprite), ent.Comp.LayerKey, layerData);
            }
        }
    }
}
