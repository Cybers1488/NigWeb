// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.Animations;

/// <summary>
/// Plays an attack animation on an entity when it attacks with a melee weapon.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MobAttackAnimationComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<string> AttackStates = new() { "attack", "hard_attack" };

    [DataField, AutoNetworkedField]
    public float AnimationLength = 1.0f;

    [DataField, AutoNetworkedField]
    public string LayerKey = "movement";

    [DataField, AutoNetworkedField]
    public string AnimationKey = "mob_attack_flick";
}
