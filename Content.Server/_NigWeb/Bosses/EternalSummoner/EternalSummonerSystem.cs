// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Server._NigWeb.Melee;
using Content.Shared._NigWeb.Bosses.EternalSummoner;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weather;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._NigWeb.Bosses.EternalSummoner;

/// <summary>
/// Controls the Summoner of Eternal Night monument across all 3 evolutionary stages:
/// - Stage 1 (0+ kills): Elder Head (ElderIdle), Sunset ambiance, amber glow.
/// - Stage 2 (10+ kills): Iron Mask (MaskIdle), Pitch black darkness, bloody red glow, global demonic broadcast.
/// - Stage 3 (30+ kills): Death Skull (SkullIdle), Abyssal blood eclipse, apocalyptic crimson blaze, global broadcast.
/// Also executes divine retribution upon any foolish mortal who attacks the sacred monument.
/// </summary>
public sealed class EternalSummonerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MeleeBloodHitEffectSystem _bloodEffect = default!;

    public override void Initialize()
    {
        base.Initialize();
        Log.Info("[EternalSummonerSystem] Initiating Eternal Summoner System...");

        SubscribeLocalEvent<EternalSummonerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EternalSummonerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EternalSummonerComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<EternalSummonerComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<EternalSummonerComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<MobStateChangedEvent>(OnGlobalMobStateChanged);
    }

    private void OnMapInit(EntityUid uid, EternalSummonerComponent comp, MapInitEvent args)
    {
        Log.Info($"[EternalSummonerSystem] MapInit for Eternal Summoner monument {ToPrettyString(uid)}");
        UpdateAppearance(uid, comp, false);

        if (Transform(uid).MapUid is { } mapUid)
        {
            ApplyPlanetAtmosphere(uid, comp, mapUid);
            comp.LightingApplied = true;
        }
    }

    private void OnStartup(EntityUid uid, EternalSummonerComponent comp, ComponentStartup args)
    {
        Log.Info($"[EternalSummonerSystem] Startup for Eternal Summoner monument {ToPrettyString(uid)}");
        UpdateAppearance(uid, comp, false);

        if (Transform(uid).MapUid is { } mapUid)
        {
            ApplyPlanetAtmosphere(uid, comp, mapUid);
            comp.LightingApplied = true;
        }
    }

    private void OnAttacked(EntityUid uid, EternalSummonerComponent comp, AttackedEvent args)
    {
        Log.Debug($"[EternalSummonerSystem] OnAttacked on {ToPrettyString(uid)} by {ToPrettyString(args.User)}");
        ExecuteDivineRetribution(uid, comp, args.User);
    }

    private void OnDamageModify(EntityUid uid, EternalSummonerComponent comp, DamageModifyEvent args)
    {
        // The monument is a transcendent sacred idol - completely immune to all physical harm
        args.Damage.ClampMax(0);

        if (args.Origin is { } origin && origin != uid && Exists(origin))
        {
            Log.Debug($"[EternalSummonerSystem] OnDamageModify on {ToPrettyString(uid)}: hit by {ToPrettyString(origin)}");
            ExecuteDivineRetribution(uid, comp, origin);
        }
    }

    private void OnGlobalMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        var deadUid = args.Target;
        if (!Exists(deadUid) || HasComp<EternalSummonerComponent>(deadUid))
            return;

        var deadXform = Transform(deadUid);
        var deadMapUid = deadXform.MapUid;
        var deadMapId = deadXform.MapID;

        var query = EntityQueryEnumerator<EternalSummonerComponent, TransformComponent>();
        while (query.MoveNext(out var summonerUid, out var comp, out var xform))
        {
            // Only count deaths that occur on the same map / planet
            if (xform.MapUid != null && deadMapUid != null)
            {
                if (xform.MapUid != deadMapUid)
                    continue;
            }
            else if (xform.MapID != deadMapId)
            {
                continue;
            }

            comp.TotalDeaths++;
            Dirty(summonerUid, comp);
            Log.Info($"[EternalSummonerSystem] Death recorded ({comp.TotalDeaths} total: Mask={comp.DeathsRequiredForMask}, Skull={comp.DeathsRequiredForSkull}) on map for monument {ToPrettyString(summonerUid)}");

            // Check Stage 3 (Skull) threshold first
            if (comp.TotalDeaths >= comp.DeathsRequiredForSkull &&
                comp.CurrentHeadState == EternalHeadState.MaskIdle &&
                !comp.IsTransitioning)
            {
                StartMaskToSkullTransition(summonerUid, comp);
            }
            // Check Stage 2 (Mask) threshold
            else if (comp.TotalDeaths >= comp.DeathsRequiredForMask &&
                comp.CurrentHeadState == EternalHeadState.ElderIdle &&
                !comp.IsTransitioning)
            {
                StartElderToMaskTransition(summonerUid, comp);
            }
        }
    }

    private void StartElderToMaskTransition(EntityUid uid, EternalSummonerComponent comp)
    {
        Log.Info($"[EternalSummonerSystem] Death threshold of {comp.DeathsRequiredForMask} reached! Initiating transformation Elder -> Mask on {ToPrettyString(uid)}");
        comp.CurrentHeadState = EternalHeadState.ElderToMask;
        comp.IsTransitioning = true;
        comp.StateTimer = comp.TransitionDuration;

        if (comp.SoundHeadChange != null)
            _audio.PlayPvs(comp.SoundHeadChange, uid, AudioParams.Default.WithVolume(4.5f).WithMaxDistance(35f));

        UpdateAppearance(uid, comp, false);
        Dirty(uid, comp);
    }

    private void StartMaskToSkullTransition(EntityUid uid, EternalSummonerComponent comp)
    {
        Log.Info($"[EternalSummonerSystem] Death threshold of {comp.DeathsRequiredForSkull} reached! Initiating transformation Mask -> Skull on {ToPrettyString(uid)}");
        comp.CurrentHeadState = EternalHeadState.MaskToSkull;
        comp.IsTransitioning = true;
        comp.StateTimer = comp.TransitionDuration;

        if (comp.SoundHeadChange != null)
            _audio.PlayPvs(comp.SoundHeadChange, uid, AudioParams.Default.WithVolume(4.5f).WithMaxDistance(35f));

        UpdateAppearance(uid, comp, false);
        Dirty(uid, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<EternalSummonerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapUid is { } mapUid)
            {
                // Enforce static atmosphere if not applied yet, or if LightCycleComponent was dynamically added (e.g. by 'planet' command)
                if (!comp.LightingApplied || HasComp<LightCycleComponent>(mapUid))
                {
                    ApplyPlanetAtmosphere(uid, comp, mapUid);
                    comp.LightingApplied = true;
                }
            }

            if (comp.IsTransitioning)
            {
                comp.StateTimer -= frameTime;
                if (comp.StateTimer <= 0f)
                {
                    comp.IsTransitioning = false;
                    comp.StateTimer = 0f;

                    if (comp.CurrentHeadState == EternalHeadState.ElderToMask)
                    {
                        comp.CurrentHeadState = EternalHeadState.MaskIdle;
                        Log.Info($"[EternalSummonerSystem] Morphing complete. Mask awakened on {ToPrettyString(uid)}. World plunges into darkness.");
                        UpdateAppearance(uid, comp, false);
                        Dirty(uid, comp);

                        if (xform.MapUid is { } mUid)
                        {
                            ApplyDarknessAndRedGlow(uid, comp, mUid);
                        }

                        SpawnBuriedMonkWave(uid, 3);
                    }
                    else if (comp.CurrentHeadState == EternalHeadState.MaskToSkull)
                    {
                        comp.CurrentHeadState = EternalHeadState.SkullIdle;
                        Log.Info($"[EternalSummonerSystem] Morphing complete. Death Skull awakened on {ToPrettyString(uid)}. Apocalyptic blood eclipse begins.");
                        UpdateAppearance(uid, comp, false);
                        Dirty(uid, comp);

                        if (xform.MapUid is { } mUid)
                        {
                            ApplyBloodEclipseAndSkullGlow(uid, comp, mUid);
                        }

                        SpawnBuriedMonkWave(uid, 4);
                    }
                }
            }
        }
    }

    private void ApplyPlanetAtmosphere(EntityUid uid, EternalSummonerComponent comp, EntityUid mapUid)
    {
        Log.Info($"[EternalSummonerSystem] Enforcing planetary atmosphere on map {ToPrettyString(mapUid)} (CurrentState={comp.CurrentHeadState})");

        // Remove rotating sun and shadow cycles from the map
        RemComp<LightCycleComponent>(mapUid);
        RemComp<SunShadowCycleComponent>(mapUid);
        RemComp<SunShadowComponent>(mapUid);

        Color ambientColor = comp.SunsetColor;
        Color lightColor = comp.InitialLightColor;
        float lightRadius = comp.InitialLightRadius;
        float lightEnergy = comp.InitialLightEnergy;

        if (TryComp<MapComponent>(mapUid, out var mapComp))
        {
            if (comp.CurrentHeadState == EternalHeadState.SkullIdle)
            {
                if (comp.Stage3Weather != null && _proto.TryIndex(comp.Stage3Weather.Value, out var weatherProto))
                {
                    Log.Info($"[EternalSummonerSystem] Enforcing Stage 3 weather {comp.Stage3Weather.Value} on map {mapComp.MapId}");
                    _weather.SetWeather(mapComp.MapId, weatherProto, null);
                }
            }
            else
            {
                _weather.SetWeather(mapComp.MapId, null, null);
            }
        }

        if (comp.CurrentHeadState == EternalHeadState.SkullIdle)
        {
            ambientColor = comp.BloodEclipseColor;
            lightColor = comp.SkullLightColor;
            lightRadius = comp.SkullLightRadius;
            lightEnergy = comp.SkullLightEnergy;
        }
        else if (comp.CurrentHeadState == EternalHeadState.MaskIdle)
        {
            ambientColor = comp.DarknessColor;
            lightColor = comp.MaskLightColor;
            lightRadius = comp.MaskLightRadius;
            lightEnergy = comp.MaskLightEnergy;
        }

        if (TryComp<MapLightComponent>(mapUid, out var mapLight))
        {
            mapLight.AmbientLightColor = ambientColor;
            Dirty(mapUid, mapLight);
            Log.Info($"[EternalSummonerSystem] Map {ToPrettyString(mapUid)} AmbientLightColor set to {mapLight.AmbientLightColor}");
        }

        _pointLight.SetColor(uid, lightColor);
        _pointLight.SetRadius(uid, lightRadius);
        _pointLight.SetEnergy(uid, lightEnergy);
        _pointLight.SetEnabled(uid, true);
    }

    private void ApplyDarknessAndRedGlow(EntityUid uid, EternalSummonerComponent comp, EntityUid mapUid)
    {
        Log.Info($"[EternalSummonerSystem] Plunging map {ToPrettyString(mapUid)} into darkness and activating bloody red glow on {ToPrettyString(uid)}");

        RemComp<LightCycleComponent>(mapUid);
        RemComp<SunShadowCycleComponent>(mapUid);
        RemComp<SunShadowComponent>(mapUid);

        if (TryComp<MapLightComponent>(mapUid, out var mapLight))
        {
            mapLight.AmbientLightColor = comp.DarknessColor;
            Dirty(mapUid, mapLight);
        }

        _pointLight.SetColor(uid, comp.MaskLightColor);
        _pointLight.SetRadius(uid, comp.MaskLightRadius);
        _pointLight.SetEnergy(uid, comp.MaskLightEnergy);
        _pointLight.SetEnabled(uid, true);

        // Turn off corrupt weather if entering Stage 2
        if (TryComp<MapComponent>(mapUid, out var maskMapComp))
        {
            _weather.SetWeather(maskMapComp.MapId, null, null);
        }

        if (comp.SoundStage2Transition != null)
        {
            _audio.PlayPvs(comp.SoundStage2Transition, uid, AudioParams.Default.WithVolume(4.5f).WithMaxDistance(45f));
        }

        // Global voice broadcast across the entire server
        if (comp.SoundMaskAwakeningGlobal != null)
        {
            Log.Info($"[EternalSummonerSystem] Broadcasting global Mask awakening voice ({comp.SoundMaskAwakeningGlobal}) to all players on server!");
            _audio.PlayGlobal(comp.SoundMaskAwakeningGlobal, Filter.Broadcast(), recordReplay: true, AudioParams.Default.WithVolume(5.5f));
        }

        _popup.PopupEntity(Loc.GetString("eternal-summoner-mask-awakened"), uid, PopupType.LargeCaution);
    }

    private void ApplyBloodEclipseAndSkullGlow(EntityUid uid, EternalSummonerComponent comp, EntityUid mapUid)
    {
        Log.Info($"[EternalSummonerSystem] Plunging map {ToPrettyString(mapUid)} into blood eclipse and activating crimson skull blaze on {ToPrettyString(uid)}");

        RemComp<LightCycleComponent>(mapUid);
        RemComp<SunShadowCycleComponent>(mapUid);
        RemComp<SunShadowComponent>(mapUid);

        if (TryComp<MapLightComponent>(mapUid, out var mapLight))
        {
            mapLight.AmbientLightColor = comp.BloodEclipseColor;
            Dirty(mapUid, mapLight);
        }

        _pointLight.SetColor(uid, comp.SkullLightColor);
        _pointLight.SetRadius(uid, comp.SkullLightRadius);
        _pointLight.SetEnergy(uid, comp.SkullLightEnergy);
        _pointLight.SetEnabled(uid, true);

        // Activate corrupt blood-ash storm on Stage 3
        if (TryComp<MapComponent>(mapUid, out var skullMapComp) && comp.Stage3Weather != null)
        {
            if (_proto.TryIndex(comp.Stage3Weather.Value, out var weatherProto))
            {
                Log.Info($"[EternalSummonerSystem] Activating Stage 3 corrupt weather {comp.Stage3Weather.Value} on map {skullMapComp.MapId}");
                _weather.SetWeather(skullMapComp.MapId, weatherProto, null);
            }
        }

        if (comp.SoundStage3Transition != null)
        {
            _audio.PlayPvs(comp.SoundStage3Transition, uid, AudioParams.Default.WithVolume(5.0f).WithMaxDistance(50f));
        }

        // Global voice broadcast for Stage 3 (Skull)
        if (comp.SoundSkullAwakeningGlobal != null)
        {
            Log.Info($"[EternalSummonerSystem] Broadcasting global Skull awakening voice ({comp.SoundSkullAwakeningGlobal}) to all players on server!");
            _audio.PlayGlobal(comp.SoundSkullAwakeningGlobal, Filter.Broadcast(), recordReplay: true, AudioParams.Default.WithVolume(6.0f));
        }

        _popup.PopupEntity(Loc.GetString("eternal-summoner-skull-awakened"), uid, PopupType.LargeCaution);
    }

    private void OnGetVerbs(EntityUid uid, EternalSummonerComponent comp, GetVerbsEvent<Verb> args)
    {
        args.Verbs.Add(new Verb
        {
            Text = "Debug: Пробудить Маску (Stage 2)",
            Category = VerbCategory.Debug,
            Act = () =>
            {
                Log.Info($"[EternalSummonerSystem] Debug trigger: manual Mask awakening by {ToPrettyString(args.User)}");
                StartElderToMaskTransition(uid, comp);
            }
        });

        args.Verbs.Add(new Verb
        {
            Text = "Debug: Пробудить Череп (Stage 3)",
            Category = VerbCategory.Debug,
            Act = () =>
            {
                Log.Info($"[EternalSummonerSystem] Debug trigger: manual Skull awakening by {ToPrettyString(args.User)}");
                StartMaskToSkullTransition(uid, comp);
            }
        });

        args.Verbs.Add(new Verb
        {
            Text = "Debug: Переключить бурю скверны (Stage 3 Weather)",
            Category = VerbCategory.Debug,
            Act = () =>
            {
                if (Transform(uid).MapUid is { } mUid && TryComp<MapComponent>(mUid, out var mapComp))
                {
                    if (comp.Stage3Weather != null && _proto.TryIndex(comp.Stage3Weather.Value, out var weatherProto))
                    {
                        var weatherComp = EnsureComp<WeatherComponent>(mUid);
                        if (weatherComp.Weather.ContainsKey(comp.Stage3Weather.Value))
                        {
                            Log.Info($"[EternalSummonerSystem] Debug trigger: stopping weather on map {mapComp.MapId}");
                            _weather.SetWeather(mapComp.MapId, null, null);
                        }
                        else
                        {
                            Log.Info($"[EternalSummonerSystem] Debug trigger: starting weather on map {mapComp.MapId}");
                            _weather.SetWeather(mapComp.MapId, weatherProto, null);
                        }
                    }
                }
            }
        });

        args.Verbs.Add(new Verb
        {
            Text = "Debug: Сбросить к Старцу (Stage 1)",
            Category = VerbCategory.Debug,
            Act = () =>
            {
                Log.Info($"[EternalSummonerSystem] Debug trigger: reset to Elder by {ToPrettyString(args.User)}");
                comp.CurrentHeadState = EternalHeadState.ElderIdle;
                comp.IsTransitioning = false;
                comp.TotalDeaths = 0;
                UpdateAppearance(uid, comp, false);
                Dirty(uid, comp);
                if (Transform(uid).MapUid is { } mUid)
                    ApplyPlanetAtmosphere(uid, comp, mUid);
            }
        });

        args.Verbs.Add(new Verb
        {
            Text = "Debug: Призвать Погребённого монаха (EN18)",
            Category = VerbCategory.Debug,
            Act = () =>
            {
                Log.Info($"[EternalSummonerSystem] Debug trigger: spawn Buried Monk by {ToPrettyString(args.User)}");
                var userCoords = Transform(args.User).Coordinates;
                var monk = Spawn("MobBuriedMonk", userCoords);
                Log.Info($"[EternalSummonerSystem] Spawned Buried Monk {ToPrettyString(monk)}");
            }
        });
    }

    private void SpawnBuriedMonkWave(EntityUid summonerUid, int count)
    {
        var xform = Transform(summonerUid);
        var coords = xform.Coordinates;
        var offsets = new[]
        {
            new System.Numerics.Vector2(-3f, -2f),
            new System.Numerics.Vector2(3f, -2f),
            new System.Numerics.Vector2(0f, 3f),
            new System.Numerics.Vector2(-4f, 2f),
            new System.Numerics.Vector2(4f, 2f),
        };

        for (var i = 0; i < count && i < offsets.Length; i++)
        {
            var spawnCoords = coords.Offset(offsets[i]);
            var monk = Spawn("MobBuriedMonk", spawnCoords);
            Log.Info($"[EternalSummonerSystem] Summoned Buried Monk {ToPrettyString(monk)} at {spawnCoords}");
        }
    }

    private void ExecuteDivineRetribution(EntityUid summonerUid, EternalSummonerComponent comp, EntityUid attacker)
    {
        if (!Exists(attacker) || attacker == summonerUid)
            return;

        if (TryComp<MobStateComponent>(attacker, out var mobState) && mobState.CurrentState == MobState.Dead)
            return;

        var curTime = _timing.CurTime;
        if (curTime < comp.NextRetributionTime)
            return;

        comp.NextRetributionTime = curTime + comp.RetributionCooldown;

        Log.Info($"[EternalSummonerSystem] DIVINE RETRIBUTION struck {ToPrettyString(attacker)} for assaulting {ToPrettyString(summonerUid)}");

        // 1. Stun & Paralyze
        _stun.KnockdownOrStun(attacker, TimeSpan.FromSeconds(5));
        _stun.TryAddParalyzeDuration(attacker, TimeSpan.FromSeconds(4));

        // 2. Divine Holy Damage (Heat + Shock)
        var damage = new DamageSpecifier();
        damage.DamageDict["Heat"] = 25;
        damage.DamageDict["Shock"] = 25;
        _damageable.TryChangeDamage(attacker, damage, ignoreResistances: true);

        // 3. Audio retribution
        if (comp.SoundRetribution != null)
            _audio.PlayPvs(comp.SoundRetribution, attacker, AudioParams.Default.WithVolume(3.5f).WithMaxDistance(28f));

        // 4. Blood burst on attacker from divine smite
        _bloodEffect.EmitBlood(attacker, Vector2.UnitY);

        // 4. Popups
        _popup.PopupEntity(Loc.GetString("eternal-summoner-retribution-attacker"), attacker, attacker, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("eternal-summoner-retribution-others", ("attacker", attacker)), summonerUid, PopupType.MediumCaution);
    }

    private void UpdateAppearance(EntityUid uid, EternalSummonerComponent comp, bool isDead)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        var state = new EternalSummonerVisualState(comp.CurrentHeadState, isDead);
        _appearance.SetData(uid, EternalSummonerVisuals.VisualState, state, appearance);
        Log.Debug($"[EternalSummonerSystem] Updated appearance on {ToPrettyString(uid)}: Head={comp.CurrentHeadState}, Dead={isDead}");
    }
}
