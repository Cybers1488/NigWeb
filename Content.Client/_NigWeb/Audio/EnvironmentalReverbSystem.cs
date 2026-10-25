// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Light.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Effects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._NigWeb.Audio;

/// <summary>
/// Physical acoustic environment reverb system:
/// routes in-world audio through OpenAL hardware EFX EaxReverb to simulate
/// realistic spatial reverberation in dark stone dungeons, halls, and corridors,
/// while dynamically adapting to dry, open-air acoustics when on planet surfaces.
/// </summary>
public sealed class EnvironmentalReverbSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly RoofSystem _roof = default!;

    private EntityUid? _auxiliaryUid;
    private EntityUid? _effectUid;
    private bool? _wasIndoors;

    public override void Initialize()
    {
        base.Initialize();

        Log.Info("[EnvironmentalReverbSystem] Initiating physical acoustic environment reverb with indoor/outdoor differentiation...");
        _config.OnValueChanged(CCVars.EnvironmentalReverbGain, OnReverbGainChanged);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        Log.Info("[EnvironmentalReverbSystem] Shutting down physical acoustic environment reverb...");
        _config.UnsubValueChanged(CCVars.EnvironmentalReverbGain, OnReverbGainChanged);
        CleanupAuxiliary();
    }

    private void OnReverbGainChanged(float gain)
    {
        if (_wasIndoors != null)
        {
            SetEnvironmentAcoustics(_wasIndoors.Value);
        }
    }

    private void SetEnvironmentAcoustics(bool indoors)
    {
        if (_effectUid == null || !TryComp<AudioEffectComponent>(_effectUid.Value, out var efxComp))
            return;

        var gainMult = _config.GetCVar(CCVars.EnvironmentalReverbGain);
        var preset = indoors ? CreateIndoorDungeonPreset(gainMult) : CreateOutdoorPlanetPreset(gainMult);
        _audio.SetEffectPreset(_effectUid.Value, efxComp, preset);
        Log.Debug($"[EnvironmentalReverbSystem] Applied acoustic preset: {(indoors ? "Indoor Stone Chamber" : "Outdoor Open Planet")}, gain: {gainMult}");
    }

    /// <summary>
    /// Dark stone chamber/dungeon acoustics:
    /// High-amplitude stone wall reflections, rich reverberation tail, and atmospheric repeating echo.
    /// </summary>
    private ReverbProperties CreateIndoorDungeonPreset(float gainMult)
    {
        return new ReverbProperties(
            density: 1.0f,
            diffusion: 0.95f,
            gain: Math.Clamp(0.85f * gainMult, 0.0f, 1.0f),
            gainHF: 0.75f,
            gainLF: 1.0f,
            decayTime: 2.3f, // 2.3s cavernous stone hall tail
            decayHFRatio: 0.75f,
            decayLFRatio: 1.0f,
            reflectionsGain: 0.90f,
            reflectionsDelay: 0.020f,
            reflectionsPan: Vector3.Zero,
            lateReverbGain: 1.20f,
            lateReverbDelay: 0.030f,
            lateReverbPan: Vector3.Zero,
            echoTime: 0.22f, // 220ms spatial delay
            echoDepth: 0.40f, // Rich atmospheric echo repetition
            modulationTime: 0.25f,
            modulationDepth: 0.0f,
            airAbsorptionGainHF: 0.994f,
            hfReference: 5000.0f,
            lfReference: 250.0f,
            roomRolloffFactor: 0.0f,
            decayHFLimit: 1
        );
    }

    /// <summary>
    /// Open planet wasteland acoustics:
    /// Completely dry, open-air direct audio with ZERO echo or reverb.
    /// Sounds are raw, crisp, punchy, and visceral without any artificial reflections.
    /// </summary>
    private ReverbProperties CreateOutdoorPlanetPreset(float gainMult)
    {
        return new ReverbProperties(
            density: 0.0f,
            diffusion: 0.0f,
            gain: 0.0f, // 100% dry, ZERO reverb!
            gainHF: 0.0f,
            gainLF: 0.0f,
            decayTime: 0.1f, // Minimum valid OpenAL decay
            decayHFRatio: 0.1f,
            decayLFRatio: 0.1f,
            reflectionsGain: 0.0f, // Zero reflection
            reflectionsDelay: 0.0f,
            reflectionsPan: Vector3.Zero,
            lateReverbGain: 0.0f, // Zero late reverb
            lateReverbDelay: 0.0f,
            lateReverbPan: Vector3.Zero,
            echoTime: 0.075f, // Minimum valid OpenAL parameter
            echoDepth: 0.0f, // ZERO echo on the planet!
            modulationTime: 0.25f,
            modulationDepth: 0.0f,
            airAbsorptionGainHF: 1.0f,
            hfReference: 5000.0f,
            lfReference: 250.0f,
            roomRolloffFactor: 0.0f,
            decayHFLimit: 1
        );
    }

    private bool IsPlayerIndoors()
    {
        var localEnt = _playerManager.LocalEntity;
        if (localEnt == null || !TryComp<TransformComponent>(localEnt, out var xform))
            return true;

        var gridUid = xform.GridUid;
        if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var grid))
        {
            // Outside on map, barren terrain or space -> outdoors
            return false;
        }

        // Grids with ImplicitRoofComponent are fully enclosed stations or shuttles
        if (HasComp<ImplicitRoofComponent>(gridUid.Value))
            return true;

        var tileIndices = _mapSystem.TileIndicesFor(gridUid.Value, grid, xform.Coordinates);

        // Check if explicit roof exists on this tile (e.g. bunker, castle, shelter on planet)
        if (TryComp<RoofComponent>(gridUid.Value, out var roof))
        {
            if (_roof.IsRooved((gridUid.Value, grid, roof), tileIndices))
                return true;
        }

        // Check tile prototype definition
        var tileRef = _mapSystem.GetTileRef(gridUid.Value, grid, tileIndices);
        if (tileRef.Tile.IsEmpty)
            return false;

        if (_tileDefManager[tileRef.Tile.TypeId] is ContentTileDefinition contentTile)
        {
            // Planet surface turf (dirt, grass, gravel, snow, lava) where weather strikes -> outdoors!
            if (contentTile.Weather)
                return false;
        }

        return true;
    }

    private void EnsureEffects()
    {
        if (_auxiliaryUid != null && _effectUid != null && !Deleted(_auxiliaryUid.Value) && !Deleted(_effectUid.Value))
            return;

        var (auxEnt, auxComp) = _audio.CreateAuxiliary();
        var (efxEnt, efxComp) = _audio.CreateEffect();

        _auxiliaryUid = auxEnt;
        _effectUid = efxEnt;

        var indoors = _wasIndoors ?? IsPlayerIndoors();
        _wasIndoors = indoors;

        var gainMult = _config.GetCVar(CCVars.EnvironmentalReverbGain);
        var preset = indoors ? CreateIndoorDungeonPreset(gainMult) : CreateOutdoorPlanetPreset(gainMult);

        _audio.SetEffectPreset(efxEnt, efxComp, preset);
        _audio.SetEffect(auxEnt, auxComp, efxEnt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_config.GetCVar(CCVars.EnvironmentalReverb))
        {
            if (_auxiliaryUid != null)
            {
                CleanupAuxiliary();
            }
            return;
        }

        // If local player is in Critical or Dead state, CritEchoSystem manages the near-death dream echo
        var localEnt = _playerManager.LocalEntity;
        if (localEnt != null && TryComp<MobStateComponent>(localEnt, out var mobState))
        {
            if (mobState.CurrentState == MobState.Critical || mobState.CurrentState == MobState.Dead)
                return;
        }

        EnsureEffects();

        if (_auxiliaryUid == null)
            return;

        // Check for acoustic environment change (Indoors vs Planet Outdoors)
        var indoors = IsPlayerIndoors();
        if (_wasIndoors == null || _wasIndoors.Value != indoors)
        {
            _wasIndoors = indoors;
            Log.Debug($"[EnvironmentalReverbSystem] Acoustic environment shifted: {(indoors ? "INDOORS (Stone Chamber)" : "OUTDOORS (Open Planet)")}");
            SetEnvironmentAcoustics(indoors);
        }

        // Route in-world spatial audio through physical environmental reverb directly on OpenAL source.
        // SetAuxiliaryDirect does not dirty the networked component, so it never triggers OnAudioState
        // and never skips the start of short sounds or footsteps!
        var query = AllEntityQuery<AudioComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // Do not apply reverb to global UI or music audio
            if (comp.Global)
                continue;

            if (comp.Auxiliary != _auxiliaryUid)
            {
                _audio.SetAuxiliaryDirect(uid, comp, _auxiliaryUid.Value);
            }
        }
    }

    private void CleanupAuxiliary()
    {
        if (_auxiliaryUid != null)
        {
            var query = AllEntityQuery<AudioComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.Auxiliary == _auxiliaryUid)
                {
                    _audio.SetAuxiliaryDirect(uid, comp, null);
                }
            }

            QueueDel(_auxiliaryUid.Value);
            if (_effectUid != null)
                QueueDel(_effectUid.Value);

            _auxiliaryUid = null;
            _effectUid = null;
            _wasIndoors = null;
        }
    }
}
