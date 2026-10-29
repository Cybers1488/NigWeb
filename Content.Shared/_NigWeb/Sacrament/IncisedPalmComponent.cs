// SPDX-License-Identifier: MIT
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._NigWeb.Sacrament;

/// <summary>
/// Status effect component attached to a character when they incise their palm with a sharp blade.
/// Holds the bleeding cut state used to thaw the Sorrow Patriarch.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IncisedPalmComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(60);

    [DataField]
    public SoundSpecifier SliceSound = new SoundCollectionSpecifier("NigWebPalmSlice");
}
