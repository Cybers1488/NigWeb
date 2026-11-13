// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Systems;
using Content.Server.Movement.Components;
using Content.Server.Movement.Systems;
using Content.Shared.Camera;
using Content.Shared.Chat;
using Content.Shared.Hands;
using Content.Shared.Movement.Components;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;

namespace Content.Server.Wieldable;

public sealed class WieldableSystem : SharedWieldableSystem
{
    [Dependency] private readonly ContentEyeSystem _eye = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CursorOffsetRequiresWieldComponent, ItemUnwieldedEvent>(OnEyeOffsetUnwielded);
        SubscribeLocalEvent<CursorOffsetRequiresWieldComponent, ItemWieldedEvent>(OnEyeOffsetWielded);
        SubscribeLocalEvent<CursorOffsetRequiresWieldComponent, HeldRelayedEvent<GetEyePvsScaleRelayedEvent>>(OnGetEyePvsScale);

        SubscribeLocalEvent<WieldableComponent, ItemWieldedEvent>(OnWieldedEmote);
    }

    private void OnWieldedEmote(Entity<WieldableComponent> entity, ref ItemWieldedEvent args)
    {
        _chat.TrySendInGameICMessage(args.User, Loc.GetString("wieldable-component-wield-emote"), InGameICChatType.Emote, ChatTransmitRange.Normal);
    }

    private void OnEyeOffsetUnwielded(Entity<CursorOffsetRequiresWieldComponent> entity, ref ItemUnwieldedEvent args)
    {
        _eye.UpdatePvsScale(args.User);
    }

    private void OnEyeOffsetWielded(Entity<CursorOffsetRequiresWieldComponent> entity, ref ItemWieldedEvent args)
    {
        _eye.UpdatePvsScale(args.User);
    }

    private void OnGetEyePvsScale(Entity<CursorOffsetRequiresWieldComponent> entity,
        ref HeldRelayedEvent<GetEyePvsScaleRelayedEvent> args)
    {
        if (!TryComp(entity, out EyeCursorOffsetComponent? eyeCursorOffset) || !TryComp(entity.Owner, out WieldableComponent? wieldableComp))
            return;

        if (!wieldableComp.Wielded)
            return;

        args.Args.Scale += eyeCursorOffset.PvsIncrease;
    }
}
