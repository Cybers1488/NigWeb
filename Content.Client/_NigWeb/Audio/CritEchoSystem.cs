using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Effects;
using Robust.Shared.GameObjects;
using Robust.Client.Player;
using System.Numerics;

namespace Content.Client._NigWeb.Audio;

public sealed class CritEchoSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    
    private EntityUid? _critAuxiliary;
    private EntityUid? _critEffect;
    
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        
        var localEnt = _playerManager.LocalEntity;
        bool shouldHaveEcho = false;
        
        // We apply echo in both Critical AND Dead states, because players often bleed into Dead
        // or stay dead on the ground, and they want the atmospheric echo to continue until respawn/revive!
        if (localEnt != null && TryComp<MobStateComponent>(localEnt, out var mobState))
        {
            if (mobState.CurrentState == MobState.Critical || mobState.CurrentState == MobState.Dead)
            {
                shouldHaveEcho = true;
            }
        }
        
        if (shouldHaveEcho)
        {
            if (_critAuxiliary == null)
            {
                var (ent, aux) = _audio.CreateAuxiliary();
                var (efxEnt, efx) = _audio.CreateEffect();
                
                // Big atmospheric echo effect
                ReverbProperties preset = new ReverbProperties(
                    density: 1.0f,
                    diffusion: 1.0f,
                    gain: 1.0f,
                    gainHF: 0.89f,
                    gainLF: 1.0f,
                    decayTime: 5.0f, // very long echo
                    decayHFRatio: 0.83f,
                    decayLFRatio: 1.0f,
                    reflectionsGain: 2.0f,
                    reflectionsDelay: 0.05f,
                    reflectionsPan: Vector3.Zero,
                    lateReverbGain: 2.0f,
                    lateReverbDelay: 0.1f,
                    lateReverbPan: Vector3.Zero,
                    echoTime: 0.25f,
                    echoDepth: 0.5f,
                    modulationTime: 0.25f,
                    modulationDepth: 0.0f,
                    airAbsorptionGainHF: 0.994f,
                    hfReference: 5000.0f,
                    lfReference: 250.0f,
                    roomRolloffFactor: 0.0f,
                    decayHFLimit: 1
                );
                
                _audio.SetEffectPreset(efxEnt, efx, preset);
                _audio.SetEffect(ent, aux, efxEnt);
                _critAuxiliary = ent;
                _critEffect = efxEnt;
            }
            
            // Apply to all existing sounds constantly
            var query = AllEntityQuery<AudioComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.Auxiliary != _critAuxiliary.Value)
                {
                    _audio.SetAuxiliary(uid, comp, _critAuxiliary.Value);
                }
            }
        }
        else
        {
            if (_critAuxiliary != null)
            {
                // Remove from all existing sounds
                var query = AllEntityQuery<AudioComponent>();
                while (query.MoveNext(out var uid, out var comp))
                {
                    if (comp.Auxiliary == _critAuxiliary.Value)
                        _audio.SetAuxiliary(uid, comp, null);
                }
            
                QueueDel(_critAuxiliary.Value);
                _critAuxiliary = null;
            }
            if (_critEffect != null)
            {
                QueueDel(_critEffect.Value);
                _critEffect = null;
            }
        }
    }
}
