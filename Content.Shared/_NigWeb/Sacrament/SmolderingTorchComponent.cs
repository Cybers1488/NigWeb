// SPDX-License-Identifier: MIT
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._NigWeb.Sacrament;

/// <summary>
/// Component for the Smoldering Brand (Смоляной Светоч).
/// A wooden torch steeped in resin and ash that can be lit or extinguished.
/// Emits light and deals bonus heat damage when striking enemies.
/// Has a cumulative burn duration of 10 minutes (600s).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SmolderingTorchComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsLit;

    /// <summary>
    /// Cumulative remaining burn duration in seconds.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RemainingBurnTime = 600f;

    /// <summary>
    /// Total burn duration in seconds.
    /// </summary>
    [DataField]
    public float TotalBurnTime = 600f;

    /// <summary>
    /// Whether the torch fuel is completely exhausted and cannot be relit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsSpent;

    [DataField]
    public SoundSpecifier LightSound = new SoundCollectionSpecifier("NigWebTorchLight");

    [DataField]
    public SoundSpecifier SnuffSound = new SoundCollectionSpecifier("NigWebTorchSnuff");

    [DataField]
    public float BonusHeatDamage = 12f;
}

[Serializable, NetSerializable]
public enum SmolderingTorchVisuals : byte
{
    Lit,
}

[Serializable, NetSerializable]
public enum SmolderingTorchVisualLayers : byte
{
    Base,
    Flame,
}
