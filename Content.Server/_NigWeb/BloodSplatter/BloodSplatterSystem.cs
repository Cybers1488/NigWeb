using System.Linq;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Decals;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Decals;
using Content.Shared.Fluids;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Rotation;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared._NigWeb.Melee;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._NigWeb.BloodSplatter;

public sealed class BloodSplatterSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly BloodyItemSystem _bloodyItems = default!;

    private ISawmill _sawmill = default!;

    private readonly SoundCollectionSpecifier _soundBloodSplatter = new("NigWebBloodSplatter");
    private readonly SoundCollectionSpecifier _soundDismember = new("NigWebDismember");
    private readonly SoundCollectionSpecifier _soundDecapitate = new("NigWebDecapitate");

    private readonly string[] _dirSplatterDecals = new string[]
    {
        "BloodDirSplatter1_0_0", "BloodDirSplatter1_1_0", "BloodDirSplatter1_2_0", "BloodDirSplatter1_3_0",
        "BloodDirSplatter1_4_0", "BloodDirSplatter1_5_0", "BloodDirSplatter1_6_0", "BloodDirSplatter1_7_0",
        "BloodDirSplatter2_0_0", "BloodDirSplatter2_1_0", "BloodDirSplatter2_2_0", "BloodDirSplatter2_3_0",
        "BloodDirSplatter2_4_0", "BloodDirSplatter2_5_0", "BloodDirSplatter2_6_0", "BloodDirSplatter2_7_0"
    };

    private string GetDirSplatterDecal(Direction dir)
    {
        int set = _random.Prob(0.5f) ? 1 : 2;
        int dirIdx = dir switch
        {
            Direction.South => 0,
            Direction.North => 1,
            Direction.East => 2,
            Direction.West => 3,
            Direction.SouthEast => 4,
            Direction.SouthWest => 5,
            Direction.NorthEast => 6,
            Direction.NorthWest => 7,
            _ => 0
        };
        return $"BloodDirSplatter{set}_{dirIdx}_0";
    }

    private string GetSquirtDecal(Direction dir)
    {
        int dirIdx = dir switch
        {
            Direction.South => 0,
            Direction.North => 1,
            Direction.East => 2,
            Direction.West => 3,
            Direction.SouthEast => 4,
            Direction.SouthWest => 5,
            Direction.NorthEast => 6,
            Direction.NorthWest => 7,
            _ => 0
        };
        return $"BloodSquirt_{dirIdx}_0";
    }

    private readonly string[] _splatterDecals = new string[]
    {
        "BloodSplatter1_0_0", "BloodSplatter1_2_0", "BloodSplatter1_4_0", "BloodSplatter1_6_0",
        "BloodSplatter2_0_0", "BloodSplatter2_2_0", "BloodSplatter2_4_0", "BloodSplatter2_6_0",
        "BloodSplatter3_0_0", "BloodSplatter3_1_0", "BloodSplatter3_2_0", "BloodSplatter3_3_0",
        "BloodSplatter4_0_0", "BloodSplatter4_1_0", "BloodSplatter4_2_0", "BloodSplatter4_3_0",
        "BloodSplatter5_0_0", "BloodSplatter5_1_0", "BloodSplatter5_2_0", "BloodSplatter5_3_0",
        "BloodSplatter6_0_0", "BloodSplatter6_2_0", "BloodSplatter6_4_0", "BloodSplatter6_6_0"
    };

    private readonly string[] _mfloorDecals = new string[]
    {
        "BloodSplatter_mfloor1", "BloodSplatter_mfloor2", "BloodSplatter_mfloor3", "BloodSplatter_mfloor4",
        "BloodSplatter_mfloor5", "BloodSplatter_mfloor6", "BloodSplatter_mfloor7", "BloodSplatter_mfloor8",
        "BloodSplatter_mfloor9", "BloodSplatter_mfloor10", "BloodSplatter_mfloor11", "BloodSplatter_mfloor12"
    };

    private readonly string[] _bluntDecals = new string[]
    {
        "BloodSplatter_mgibbl1", "BloodSplatter_mgibbl2", "BloodSplatter_mgibbl3", "BloodSplatter_mgibbl4", "BloodSplatter_mgibbl5",
        "BloodSplatter_mfloor1", "BloodSplatter_mfloor6", "BloodSplatter_mfloor8"
    };

    private readonly string[] _headGibDecals = new string[]
    {
        "BloodGib_gibhead_0_0", "BloodGib_gibhead_1_0", "BloodGib_gibhead_2_0", "BloodGib_gibhead_3_0",
        "BloodGib_gibup1", "BloodGib_gibup1_flesh"
    };

    private readonly string[] _armGibDecals = new string[]
    {
        "BloodGib_gibarm_0_0", "BloodGib_gibarm_1_0", "BloodGib_gibarm_2_0", "BloodGib_gibarm_3_0",
        "BloodGib_gibarm_flesh_0_0", "BloodGib_gibarm_flesh_1_0", "BloodGib_gibarm_flesh_2_0", "BloodGib_gibarm_flesh_3_0"
    };

    private readonly string[] _legGibDecals = new string[]
    {
        "BloodGib_gibleg_0_0", "BloodGib_gibleg_1_0", "BloodGib_gibleg_2_0", "BloodGib_gibleg_3_0",
        "BloodGib_gibleg_flesh_0_0", "BloodGib_gibleg_flesh_1_0", "BloodGib_gibleg_flesh_2_0", "BloodGib_gibleg_flesh_3_0"
    };

    private readonly string[] _torsoGibDecals = new string[]
    {
        "BloodGib_gibtorso_0_0", "BloodGib_gibtorso_1_0", "BloodGib_gibtorso_2_0", "BloodGib_gibtorso_3_0",
        "BloodGib_gibtorso_flesh_0_0", "BloodGib_gibtorso_flesh_1_0", "BloodGib_gibtorso_flesh_2_0", "BloodGib_gibtorso_flesh_3_0",
        "BloodGib_gibmid1", "BloodGib_gibmid2", "BloodGib_gibmid2_flesh", "BloodGib_gibmid3", "BloodGib_gibmid3_flesh"
    };

    private readonly string[] _wallBloodPrototypes = new string[]
    {
        "NigWebWallBloodSmall", "NigWebWallBloodMedium", "NigWebWallBloodLarge"
    };

    private static readonly ProtoId<ReagentPrototype> BloodReagent = "Blood";

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("lifeweb.blood");
        _sawmill.Info("[BloodSplatterSystem] Initializing BloodSplatterSystem...");

        SubscribeLocalEvent<StatusEffectsComponent, DamageChangedEvent>(OnDamage);
        SubscribeLocalEvent<StatusEffectsComponent, BeingGibbedEvent>(OnGib);
        SubscribeLocalEvent<StatusEffectsComponent, BodyPartRemovedEvent>(OnPartRemoved);
        SubscribeLocalEvent<WallBloodComponent, InteractUsingEvent>(OnCleanWallBlood);
    }

    /// <summary>
    /// Checks if a micro-area on the grid already contains enough blood decals.
    /// This prevents stacking dozens of decals on the exact same pixel while keeping performance optimal (0 lag).
    /// </summary>
    private bool IsDecalDenseAt(EntityUid gridUid, Vector2 localPos, float maxDistance = 0.28f, int maxCount = 2)
    {
        if (!TryComp<DecalGridComponent>(gridUid, out var decalGrid))
            return false;

        var chunkIndices = SharedDecalSystem.GetChunkIndices(localPos);
        if (!decalGrid.ChunkCollection.ChunkCollection.TryGetValue(chunkIndices, out var chunk))
            return false;

        int count = 0;
        var maxDistSq = maxDistance * maxDistance;
        foreach (var (_, decal) in chunk.Decals)
        {
            var decalPos = decal.Coordinates + new Vector2(0.5f, 0.5f);
            if ((localPos - decalPos).LengthSquared() <= maxDistSq)
            {
                count++;
                if (count >= maxCount)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Calculates the dynamic world offset and spray vector for a stump, automatically
    /// factoring in the mob's current facing angle, standing state, and laying-down rotation.
    /// </summary>
    private void GetStumpGeometry(
        EntityUid uid,
        ArterialBleedSpurtComponent spurt,
        TransformComponent xform,
        out Vector2 worldStumpOffset,
        out Angle worldSprayAngle,
        out Vector2 worldSprayDir)
    {
        Vector2 localPos;
        Vector2 localDir;

        if (spurt.HasCustomGeometry)
        {
            localPos = spurt.CustomLocalOffset;
            localDir = spurt.CustomLocalDir;
        }
        else
        {
            switch (spurt.PartType)
            {
                case BodyPartType.Head:
                    localPos = new Vector2(0f, 0.42f);
                    localDir = new Vector2(0f, 1.0f);
                    break;
                case BodyPartType.Arm:
                case BodyPartType.Hand:
                    if (spurt.Symmetry == BodyPartSymmetry.Left)
                    {
                        localPos = new Vector2(0.28f, 0.20f);
                        localDir = new Vector2(1.0f, 0.15f);
                    }
                    else
                    {
                        localPos = new Vector2(-0.28f, 0.20f);
                        localDir = new Vector2(-1.0f, 0.15f);
                    }
                    break;
                case BodyPartType.Leg:
                case BodyPartType.Foot:
                    if (spurt.Symmetry == BodyPartSymmetry.Left)
                    {
                        localPos = new Vector2(0.16f, -0.28f);
                        localDir = new Vector2(0.35f, -1.0f);
                    }
                    else
                    {
                        localPos = new Vector2(-0.16f, -0.28f);
                        localDir = new Vector2(-0.35f, -1.0f);
                    }
                    break;
                case BodyPartType.Chest:
                case BodyPartType.Groin:
                case BodyPartType.Other:
                default:
                    localPos = Vector2.Zero;
                    localDir = new Vector2(0f, 1.0f);
                    break;
            }
        }

        localDir = localDir.LengthSquared() > 0.001f ? Vector2.Normalize(localDir) : Vector2.UnitY;

        var facing = xform.LocalRotation;
        var isDown = TryComp<StandingStateComponent>(uid, out var standing) && !standing.Standing;
        var horizontalRot = Angle.FromDegrees(90);
        if (TryComp<RotationVisualsComponent>(uid, out var rotVis))
        {
            horizontalRot = rotVis.HorizontalRotation;
        }

        var totalRot = isDown ? facing + horizontalRot : facing;
        worldStumpOffset = totalRot.RotateVec(localPos);
        worldSprayDir = totalRot.RotateVec(localDir).Normalized();
        worldSprayAngle = worldSprayDir.ToWorldAngle();
    }

    private void GetStumpGeometry(
        EntityUid uid,
        BodyPartType partType,
        BodyPartSymmetry symmetry,
        TransformComponent xform,
        out Vector2 worldStumpOffset,
        out Angle worldSprayAngle,
        out Vector2 worldSprayDir)
    {
        var dummy = new ArterialBleedSpurtComponent
        {
            PartType = partType,
            Symmetry = symmetry,
            HasCustomGeometry = false
        };
        GetStumpGeometry(uid, dummy, xform, out worldStumpOffset, out worldSprayAngle, out worldSprayDir);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var spurtQuery = EntityQueryEnumerator<ArterialBleedSpurtComponent, BloodstreamComponent, TransformComponent>();
        while (spurtQuery.MoveNext(out var uid, out var spurt, out var blood, out var xform))
        {
            spurt.Timer -= frameTime;
            if (spurt.Timer > 0f)
                continue;

            spurt.Timer = spurt.Interval;
            spurt.SpurtsRemaining--;

            // Dynamically recalculate stump position and spray direction based on current orientation
            GetStumpGeometry(uid, spurt, xform, out var stumpOffset, out var sprayAngle, out var sprayDir);
            var stumpCoords = xform.Coordinates.Offset(stumpOffset);
            var netStumpCoords = GetNetCoordinates(stumpCoords);

            float pressure = (float)spurt.SpurtsRemaining / Math.Max(1, spurt.TotalSpurts);

            var jitterAngle = sprayAngle + Angle.FromDegrees(_random.NextFloat(-18f, 18f));
            var sprayVec = jitterAngle.ToWorldVec();

            SpawnArterialBleed(uid, blood, stumpCoords, jitterAngle, sprayVec, spurt.IsFountain, pressure);

            // Broadcast arterial blood spray particles to nearby clients originating from the stump
            var hits = new List<BloodHitData>
            {
                new(netStumpCoords, sprayVec),
            };

            if (spurt.IsFountain)
            {
                // Multi-stream arterial fountain erupting from severed neck
                var jetLeft = Angle.FromDegrees(jitterAngle.Degrees - 24f).ToWorldVec();
                var jetRight = Angle.FromDegrees(jitterAngle.Degrees + 24f).ToWorldVec();
                hits.Add(new(netStumpCoords, jetLeft));
                hits.Add(new(netStumpCoords, jetRight));

                _audio.PlayPvs(_soundBloodSplatter, uid, AudioParams.Default.WithVolume(-4f).WithVariation(0.12f));

                // Stain clothes of nearby entities caught in the arterial fountain spray
                var nearby = _lookup.GetEntitiesInRange<InventoryComponent>(stumpCoords, 1.5f);
                var bleedColor = GetBloodColor(uid);
                foreach (var mob in nearby)
                {
                    _bloodyItems.BloodyEquippedItems(mob, 0.12f, SlotFlags.WITHOUT_POCKET, 0.25f, bleedColor);
                }
            }
            else
            {
                _audio.PlayPvs(_soundBloodSplatter, uid, AudioParams.Default.WithVolume(-6.5f).WithVariation(0.1f));
            }

            RaiseNetworkEvent(new MeleeBloodHitEffectEvent(hits), Filter.Pvs(uid));

            if (spurt.SpurtsRemaining <= 0)
            {
                RemCompDeferred<ArterialBleedSpurtComponent>(uid);
            }
        }
    }

    private void OnCleanWallBlood(EntityUid uid, WallBloodComponent component, InteractUsingEvent args)
    {
        if (HasComp<AbsorbentComponent>(args.Used))
        {
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Fluids/clean.ogg"), uid);
            _popup.PopupEntity(Loc.GetString("Кровь со стены отмыта."), uid, args.User);
            QueueDel(uid);
            args.Handled = true;
        }
    }

    private void OnDamage(EntityUid uid, StatusEffectsComponent status, DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !args.DamageIncreased)
            return;

        if (!TryComp<BloodstreamComponent>(uid, out var comp))
            return;

        float slash = 0f;
        float pierce = 0f;
        float blunt = 0f;

        if (args.DamageDelta.DamageDict.TryGetValue("Slash", out var s)) slash = s.Float();
        if (args.DamageDelta.DamageDict.TryGetValue("Piercing", out var p)) pierce = p.Float();
        if (args.DamageDelta.DamageDict.TryGetValue("Blunt", out var b)) blunt = b.Float();

        if (slash <= 0f && pierce <= 0f && blunt <= 0f)
            return;

        _sawmill.Debug($"[BloodSplatterSystem] OnDamage on {uid}: slash={slash}, pierce={pierce}, blunt={blunt}");

        // Determine continuous strike vector and angle
        Vector2 strikeVec;
        Angle strikeAngle;
        if (args.Origin != null && TryComp(args.Origin.Value, out TransformComponent? originXform))
        {
            var victimPos = _transform.GetMapCoordinates(uid).Position;
            var attackerPos = _transform.GetMapCoordinates(args.Origin.Value, originXform).Position;
            var diff = victimPos - attackerPos;
            if (diff.LengthSquared() > 0.001f)
            {
                strikeVec = diff.Normalized();
                strikeAngle = diff.ToWorldAngle();
            }
            else
            {
                strikeAngle = Transform(uid).LocalRotation;
                strikeVec = strikeAngle.ToWorldVec();
            }
        }
        else
        {
            strikeAngle = Transform(uid).LocalRotation;
            strikeVec = strikeAngle.ToWorldVec();
        }

        // 1. Piercing (Колющее - укол / пронзание со струей и брызгами вокруг)
        if (pierce > 5f)
        {
            SpawnPiercingSquirt(uid, comp, strikeVec, pierce > 20f);
        }

        // 2. Slashing (Режущее - направленный веер брызг + круговые брызги вокруг бойни)
        if (slash > 5f)
        {
            int coneCount = Math.Clamp((int)(slash / 6), 3, 8);
            int circleCount = Math.Clamp((int)(slash / 8), 2, 6);
            SpawnSlashingSplatter(uid, comp, strikeAngle, coneCount, circleCount);

            if (slash > 20f && _random.Prob(0.5f))
            {
                var spurt = EnsureComp<ArterialBleedSpurtComponent>(uid);
                spurt.SpurtsRemaining = Math.Max(spurt.SpurtsRemaining, 4);
                spurt.TotalSpurts = 4;
                spurt.Interval = 0.22f;
                spurt.Timer = 0.05f;
                spurt.IsFountain = false;
                spurt.HasCustomGeometry = true;

                var facing = Transform(uid).LocalRotation;
                var isDown = TryComp<StandingStateComponent>(uid, out var standing) && !standing.Standing;
                var horizontalRot = Angle.FromDegrees(90);
                if (TryComp<RotationVisualsComponent>(uid, out var rotVis))
                    horizontalRot = rotVis.HorizontalRotation;

                var totalRot = isDown ? facing + horizontalRot : facing;
                spurt.CustomLocalDir = (-totalRot).RotateVec(strikeVec).Normalized();
                spurt.CustomLocalOffset = spurt.CustomLocalDir * 0.18f;
            }
        }

        // 3. Blunt (Дробящее - тяжелые кляксы в направлении удара + круговые брызги)
        if (blunt > 8f)
        {
            int count = Math.Clamp((int)(blunt / 8), 2, 6);
            int circleCount = Math.Clamp((int)(blunt / 10), 2, 5);
            SpawnBluntSplatter(uid, comp, strikeAngle, count, circleCount, blunt > 20f);
        }
    }

    private void OnGib(EntityUid uid, StatusEffectsComponent status, ref BeingGibbedEvent args)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var comp))
            return;

        _sawmill.Info($"[BloodSplatterSystem] Entity {uid} gibbed violently!");
        _audio.PlayPvs(_soundDismember, uid, AudioParams.Default.WithVolume(-3f).WithVariation(0.1f));
        _audio.PlayPvs(_soundBloodSplatter, uid, AudioParams.Default.WithVolume(-4f).WithVariation(0.1f));
        SplatterCircularGibs(uid, comp, 32, 4.2f);
    }

    private void OnPartRemoved(EntityUid uid, StatusEffectsComponent status, ref BodyPartRemovedEvent args)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var comp))
            return;

        if (!TryComp<BodyPartComponent>(args.Part, out var partComp))
            return;

        var xform = Transform(uid);
        GetStumpGeometry(uid, partComp.PartType, partComp.Symmetry, xform, out var stumpOffset, out var sprayAngle, out var sprayDir);
        var stumpCoords = xform.Coordinates.Offset(stumpOffset);

        if (partComp.PartType == BodyPartType.Head)
        {
            // Decapitation!
            _sawmill.Info($"[BloodSplatterSystem] Decapitation on {uid}! Stump at {stumpCoords}, dir={sprayDir}");

            SpawnDecapitationSplatter(uid, comp, stumpCoords, sprayAngle, sprayDir);

            // 5 SECONDS ARTERIAL FOUNTAIN (25 spurts * 0.20s = 5.0 seconds)
            var spurt = EnsureComp<ArterialBleedSpurtComponent>(uid);
            spurt.PartType = BodyPartType.Head;
            spurt.Symmetry = BodyPartSymmetry.None;
            spurt.HasCustomGeometry = false;
            spurt.SpurtsRemaining = 25;
            spurt.TotalSpurts = 25;
            spurt.Interval = 0.20f;
            spurt.Timer = 0.05f;
            spurt.IsFountain = true;
        }
        else if (partComp.PartType == BodyPartType.Arm || partComp.PartType == BodyPartType.Hand)
        {
            _sawmill.Debug($"[BloodSplatterSystem] Arm/Hand severed on {uid} (sym={partComp.Symmetry}). Stump at {stumpCoords}, dir={sprayDir}");
            SpawnLimbSplatter(uid, comp, stumpCoords, sprayAngle, sprayDir, _armGibDecals);

            // 1.2 SECONDS ARTERIAL SPURT (6 spurts * 0.20s = 1.2 seconds)
            var spurt = EnsureComp<ArterialBleedSpurtComponent>(uid);
            spurt.PartType = partComp.PartType;
            spurt.Symmetry = partComp.Symmetry;
            spurt.HasCustomGeometry = false;
            spurt.SpurtsRemaining = 6;
            spurt.TotalSpurts = 6;
            spurt.Interval = 0.20f;
            spurt.Timer = 0.05f;
            spurt.IsFountain = false;
        }
        else if (partComp.PartType == BodyPartType.Leg || partComp.PartType == BodyPartType.Foot)
        {
            _sawmill.Debug($"[BloodSplatterSystem] Leg/Foot severed on {uid} (sym={partComp.Symmetry}). Stump at {stumpCoords}, dir={sprayDir}");
            SpawnLimbSplatter(uid, comp, stumpCoords, sprayAngle, sprayDir, _legGibDecals);

            // 1.2 SECONDS ARTERIAL SPURT (6 spurts * 0.20s = 1.2 seconds)
            var spurt = EnsureComp<ArterialBleedSpurtComponent>(uid);
            spurt.PartType = partComp.PartType;
            spurt.Symmetry = partComp.Symmetry;
            spurt.HasCustomGeometry = false;
            spurt.SpurtsRemaining = 6;
            spurt.TotalSpurts = 6;
            spurt.Interval = 0.20f;
            spurt.Timer = 0.05f;
            spurt.IsFountain = false;
        }
        else
        {
            SpawnLimbSplatter(uid, comp, stumpCoords, sprayAngle, sprayDir, _torsoGibDecals);
        }
    }

    private void SpawnPiercingSquirt(EntityUid victim, BloodstreamComponent comp, Vector2 strikeVector, bool isHeavy)
    {
        var xform = Transform(victim);
        if (xform.GridUid == null)
            return;

        var bloodColor = GetBloodColor(victim);

        _sawmill.Debug($"[BloodSplatterSystem] Piercing strike on {victim}");
        _audio.PlayPvs(_soundBloodSplatter, victim, AudioParams.Default.WithVolume(-5.5f).WithVariation(0.12f));

        // Squirt jet in strike direction
        var squirtDist = _random.NextFloat(0.7f, 1.6f);
        var squirtOffset = strikeVector * squirtDist;
        var dir = strikeVector.ToWorldAngle().GetDir();
        SafeAddDecal(victim, xform.Coordinates, squirtOffset, GetSquirtDecal(dir), bloodColor, Angle.Zero);

        // Circular droplets around the puncture
        int drops = isHeavy ? _random.Next(3, 6) : _random.Next(2, 4);
        for (int i = 0; i < drops; i++)
        {
            var dropAngle = Angle.FromDegrees(_random.Next(0, 360));
            var dropDist = _random.NextFloat(0.2f, 0.9f);
            var dropOffset = dropAngle.ToWorldVec() * dropDist;
            SafeAddDecal(victim, xform.Coordinates, dropOffset, _mfloorDecals[_random.Next(_mfloorDecals.Length)], bloodColor, Angle.FromDegrees(_random.Next(0, 360)));
        }
    }

    private void SpawnArterialBleed(
        EntityUid uid,
        BloodstreamComponent comp,
        EntityCoordinates stumpCoords,
        Angle sprayAngle,
        Vector2 sprayDir,
        bool isFountain,
        float pressure)
    {
        var bloodColor = GetBloodColor(uid);

        _sawmill.Debug($"[BloodSplatterSystem] Arterial bleed spurt on {uid} dir={sprayAngle} fountain={isFountain} pressure={pressure:F2}");

        // Focused cone: 2-4 splatters per pulse reaching further out
        int decalCount = isFountain ? _random.Next(2, 5) : _random.Next(1, 3);
        float minSpread = isFountain ? 0.8f : 0.5f;
        float maxSpread = (isFountain ? 3.4f : 2.2f) * (0.6f + 0.4f * pressure);

        for (int i = 0; i < decalCount; i++)
        {
            var dist = _random.NextFloat(minSpread, maxSpread);
            var jitter = sprayAngle + Angle.FromDegrees(_random.NextFloat(-20f, 20f));
            var offset = jitter.ToWorldVec() * dist;

            bool isDir = _random.Prob(0.75f);
            string decalId = isDir
                ? GetDirSplatterDecal(jitter.GetDir())
                : _splatterDecals[_random.Next(_splatterDecals.Length)];
            Angle decalAngle = decalId.StartsWith("BloodDirSplatter") ? Angle.Zero : Angle.FromDegrees(_random.Next(0, 360));

            SafeAddDecal(uid, stumpCoords, offset, decalId, bloodColor, decalAngle);
        }

        // Radial splashing droplets (blood mist and droplets landing around the victim)
        if (isFountain || _random.Prob(0.6f))
        {
            var radialAngle = Angle.FromDegrees(_random.Next(0, 360));
            var radialDist = _random.NextFloat(0.4f, 1.8f);
            var radialOffset = radialAngle.ToWorldVec() * radialDist;
            SafeAddDecal(uid, stumpCoords, radialOffset, _mfloorDecals[_random.Next(_mfloorDecals.Length)], bloodColor, Angle.FromDegrees(_random.Next(0, 360)));
        }

        // Concentrated pooling droplet directly under the severed stump
        var dropOffset = new Vector2(_random.NextFloat(-0.25f, 0.25f), _random.NextFloat(-0.25f, 0.25f));
        SafeAddDecal(uid, stumpCoords, dropOffset, _mfloorDecals[_random.Next(_mfloorDecals.Length)], bloodColor, Angle.FromDegrees(_random.Next(0, 360)));
    }

    private void SpawnSlashingSplatter(EntityUid uid, BloodstreamComponent comp, Angle strikeAngle, int coneCount, int circleCount)
    {
        var bloodColor = GetBloodColor(uid);
        var coords = Transform(uid).Coordinates;

        _sawmill.Debug($"[BloodSplatterSystem] Slashing splatter on {uid} cone={coneCount} circle={circleCount} angle={strikeAngle}");
        _audio.PlayPvs(_soundBloodSplatter, uid, AudioParams.Default.WithVolume(-5f).WithVariation(0.1f));

        // 1. Directed cone of slashes in the direction of the blade swing (reaching far out!)
        for (int i = 0; i < coneCount; i++)
        {
            var dist = _random.NextFloat(0.8f, 2.8f);
            var angleJitter = strikeAngle + Angle.FromDegrees(_random.NextFloat(-28f, 28f));
            var offset = angleJitter.ToWorldVec() * dist;

            bool isDir = _random.Prob(0.75f);
            string decalId = isDir
                ? GetDirSplatterDecal(angleJitter.GetDir())
                : _splatterDecals[_random.Next(_splatterDecals.Length)];
            Angle decalAngle = decalId.StartsWith("BloodDirSplatter") ? Angle.Zero : Angle.FromDegrees(_random.Next(0, 360));

            SafeAddDecal(uid, coords, offset, decalId, bloodColor, decalAngle);
        }

        // 2. Circular 360° spray around the fight (blood flying off blade and wound)
        for (int i = 0; i < circleCount; i++)
        {
            var angle = Angle.FromDegrees(_random.Next(0, 360));
            var dist = _random.NextFloat(0.4f, 2.2f);
            var offset = angle.ToWorldVec() * dist;

            string decalId = _random.Prob(0.4f)
                ? GetDirSplatterDecal(angle.GetDir())
                : _mfloorDecals[_random.Next(_mfloorDecals.Length)];
            Angle decalAngle = decalId.StartsWith("BloodDirSplatter") ? Angle.Zero : Angle.FromDegrees(_random.Next(0, 360));

            SafeAddDecal(uid, coords, offset, decalId, bloodColor, decalAngle);
        }

        // Droplets directly around the victim
        int dropCount = _random.Next(2, 4);
        for (int i = 0; i < dropCount; i++)
        {
            var dropOffset = new Vector2(_random.NextFloat(-0.4f, 0.4f), _random.NextFloat(-0.4f, 0.4f));
            SafeAddDecal(uid, coords, dropOffset, _mfloorDecals[_random.Next(_mfloorDecals.Length)], bloodColor, Angle.FromDegrees(_random.Next(0, 360)));
        }
    }

    private void SpawnBluntSplatter(EntityUid uid, BloodstreamComponent comp, Angle strikeAngle, int count, int circleCount, bool spawnGibs)
    {
        var bloodColor = GetBloodColor(uid);
        var coords = Transform(uid).Coordinates;

        _sawmill.Debug($"[BloodSplatterSystem] Blunt splatter on {uid} count={count}");
        _audio.PlayPvs(_soundBloodSplatter, uid, AudioParams.Default.WithVolume(-5f).WithVariation(0.1f));

        // 1. Blunt impact cone
        for (int i = 0; i < count; i++)
        {
            var dist = _random.NextFloat(0.4f, 2.0f);
            var angleJitter = strikeAngle + Angle.FromDegrees(_random.NextFloat(-50f, 50f));
            var offset = angleJitter.ToWorldVec() * dist;

            string decalId = _bluntDecals[_random.Next(_bluntDecals.Length)];
            SafeAddDecal(uid, coords, offset, decalId, bloodColor, Angle.FromDegrees(_random.Next(0, 360)));
        }

        // 2. 360° crush droplets and splatters
        for (int i = 0; i < circleCount; i++)
        {
            var angle = Angle.FromDegrees(_random.Next(0, 360));
            var dist = _random.NextFloat(0.3f, 1.8f);
            var offset = angle.ToWorldVec() * dist;

            string decalId = _random.Prob(0.5f)
                ? _mfloorDecals[_random.Next(_mfloorDecals.Length)]
                : _splatterDecals[_random.Next(_splatterDecals.Length)];

            SafeAddDecal(uid, coords, offset, decalId, bloodColor, Angle.FromDegrees(_random.Next(0, 360)));
        }

        if (spawnGibs)
        {
            var gibCount = _random.Next(1, 3);
            for (int i = 0; i < gibCount; i++)
            {
                var gibAngle = Angle.FromDegrees(_random.Next(0, 360));
                var gibDist = _random.NextFloat(0.3f, 1.4f);
                var gibOffset = gibAngle.ToWorldVec() * gibDist;
                SafeAddDecal(uid, coords, gibOffset, _torsoGibDecals[_random.Next(_torsoGibDecals.Length)], bloodColor, Angle.FromDegrees(_random.Next(0, 360)));
            }
        }
    }

    private void SpawnDecapitationSplatter(
        EntityUid uid,
        BloodstreamComponent comp,
        EntityCoordinates stumpCoords,
        Angle sprayAngle,
        Vector2 sprayDir)
    {
        var bloodColor = GetBloodColor(uid);
        var xform = Transform(uid);
        if (xform.GridUid == null)
            return;

        // Sounds: slightly softer as requested
        _audio.PlayPvs(_soundDecapitate, uid, AudioParams.Default.WithVolume(-2.5f).WithVariation(0.1f));
        _audio.PlayPvs(_soundBloodSplatter, uid, AudioParams.Default.WithVolume(-4f).WithVariation(0.1f));

        // 1. High-pressure directional squirt lines reaching far out (up to 3.2m!)
        for (int step = 1; step <= 3; step++)
        {
            var dist = step * 0.85f;
            var offset = sprayDir * dist;
            var squirtDecal = GetSquirtDecal(sprayAngle.GetDir());
            SafeAddDecal(uid, stumpCoords, offset, squirtDecal, bloodColor, Angle.Zero);
        }

        // 2. Massive directional fan in front of neck (8-10 directed decals reaching far out!)
        int directedCount = _random.Next(8, 11);
        for (int i = 0; i < directedCount; i++)
        {
            var dist = _random.NextFloat(0.6f, 3.2f);
            var coneAngle = sprayAngle + Angle.FromDegrees(_random.NextFloat(-32f, 32f));
            var offset = coneAngle.ToWorldVec() * dist;

            string decalId = _random.Prob(0.8f)
                ? GetDirSplatterDecal(coneAngle.GetDir())
                : _splatterDecals[_random.Next(_splatterDecals.Length)];
            Angle decalAngle = decalId.StartsWith("BloodDirSplatter") ? Angle.Zero : Angle.FromDegrees(_random.Next(0, 360));

            SafeAddDecal(uid, stumpCoords, offset, decalId, bloodColor, decalAngle);
        }

        // 3. 360° violent blood burst around the body (8-12 decals scattered all around the body up to 2.8m!)
        int radialCount = _random.Next(8, 13);
        for (int i = 0; i < radialCount; i++)
        {
            var radialAngle = Angle.FromDegrees(_random.Next(0, 360));
            var dist = _random.NextFloat(0.5f, 2.8f);
            var offset = radialAngle.ToWorldVec() * dist;

            string decalId = _random.Prob(0.5f)
                ? GetDirSplatterDecal(radialAngle.GetDir())
                : _splatterDecals[_random.Next(_splatterDecals.Length)];
            Angle decalAngle = decalId.StartsWith("BloodDirSplatter") ? Angle.Zero : Angle.FromDegrees(_random.Next(0, 360));

            SafeAddDecal(uid, xform.Coordinates, offset, decalId, bloodColor, decalAngle);
        }

        // 4. Flying neck flesh & skull fragments (3-4 gibs)
        for (int i = 0; i < 4; i++)
        {
            var dist = _random.NextFloat(0.5f, 2.2f);
            var gibAngle = sprayAngle + Angle.FromDegrees(_random.NextFloat(-35f, 35f));
            var offset = gibAngle.ToWorldVec() * dist;
            string gibId = _headGibDecals[_random.Next(_headGibDecals.Length)];
            SafeAddDecal(uid, stumpCoords, offset, gibId, bloodColor, Angle.FromDegrees(_random.Next(0, 360)));
        }

        // 5. Pooling blood directly under the neck and torso (4 decals)
        for (int i = 0; i < 4; i++)
        {
            var dropOffset = new Vector2(_random.NextFloat(-0.35f, 0.35f), _random.NextFloat(-0.35f, 0.35f));
            SafeAddDecal(uid, stumpCoords, dropOffset, _mfloorDecals[_random.Next(_mfloorDecals.Length)], bloodColor, Angle.FromDegrees(_random.Next(0, 360)));
        }

        // 6. Broadcast instant massive blood hit VFX particles from the stump
        var netStumpCoords = GetNetCoordinates(stumpCoords);
        var hits = new List<BloodHitData>
        {
            new(netStumpCoords, sprayDir),
            new(netStumpCoords, Angle.FromDegrees(sprayAngle.Degrees - 22f).ToWorldVec()),
            new(netStumpCoords, Angle.FromDegrees(sprayAngle.Degrees + 22f).ToWorldVec()),
            new(netStumpCoords, Angle.FromDegrees(sprayAngle.Degrees - 40f).ToWorldVec()),
            new(netStumpCoords, Angle.FromDegrees(sprayAngle.Degrees + 40f).ToWorldVec()),
        };
        RaiseNetworkEvent(new MeleeBloodHitEffectEvent(hits), Filter.Pvs(uid));

        // Stain nearby bystanders caught in the decapitation eruption
        var nearbyDecapBystanders = _lookup.GetEntitiesInRange<InventoryComponent>(stumpCoords, 1.8f);
        foreach (var mob in nearbyDecapBystanders)
        {
            _bloodyItems.BloodyEquippedItems(mob, 0.18f, SlotFlags.OUTERCLOTHING | SlotFlags.HEAD | SlotFlags.MASK, 0.35f, bloodColor);
        }
    }

    private void SpawnLimbSplatter(
        EntityUid uid,
        BloodstreamComponent comp,
        EntityCoordinates stumpCoords,
        Angle sprayAngle,
        Vector2 sprayDir,
        string[] specificGibs)
    {
        var bloodColor = GetBloodColor(uid);
        var xform = Transform(uid);

        _audio.PlayPvs(_soundDismember, uid, AudioParams.Default.WithVolume(-3.5f).WithVariation(0.1f));
        _audio.PlayPvs(_soundBloodSplatter, uid, AudioParams.Default.WithVolume(-5f).WithVariation(0.1f));

        // 1. Directed arterial spray cone (5-7 decals extending up to 2.6m)
        int directedCount = _random.Next(5, 8);
        for (int i = 0; i < directedCount; i++)
        {
            var dist = _random.NextFloat(0.6f, 2.6f);
            var coneAngle = sprayAngle + Angle.FromDegrees(_random.NextFloat(-28f, 28f));
            var offset = coneAngle.ToWorldVec() * dist;

            string decalId = _random.Prob(0.75f)
                ? GetDirSplatterDecal(coneAngle.GetDir())
                : _splatterDecals[_random.Next(_splatterDecals.Length)];
            Angle decalAngle = decalId.StartsWith("BloodDirSplatter") ? Angle.Zero : Angle.FromDegrees(_random.Next(0, 360));

            SafeAddDecal(uid, stumpCoords, offset, decalId, bloodColor, decalAngle);
        }

        // 2. Circular 360° splatter around the dismembered mob (5-8 decals)
        int radialCount = _random.Next(5, 9);
        for (int i = 0; i < radialCount; i++)
        {
            var radialAngle = Angle.FromDegrees(_random.Next(0, 360));
            var dist = _random.NextFloat(0.4f, 2.2f);
            var offset = radialAngle.ToWorldVec() * dist;

            string decalId = _random.Prob(0.5f)
                ? GetDirSplatterDecal(radialAngle.GetDir())
                : _splatterDecals[_random.Next(_splatterDecals.Length)];
            Angle decalAngle = decalId.StartsWith("BloodDirSplatter") ? Angle.Zero : Angle.FromDegrees(_random.Next(0, 360));

            SafeAddDecal(uid, xform.Coordinates, offset, decalId, bloodColor, decalAngle);
        }

        // 3. Gib pieces flung outward
        if (specificGibs.Length > 0)
        {
            int gibsToSpawn = _random.Next(1, 3);
            for (int i = 0; i < gibsToSpawn; i++)
            {
                var gibDist = _random.NextFloat(0.4f, 1.8f);
                var gibAngle = sprayAngle + Angle.FromDegrees(_random.NextFloat(-30f, 30f));
                var gibOffset = gibAngle.ToWorldVec() * gibDist;
                string gibId = specificGibs[_random.Next(specificGibs.Length)];
                SafeAddDecal(uid, stumpCoords, gibOffset, gibId, bloodColor, Angle.FromDegrees(_random.Next(0, 360)));
            }
        }

        // 4. Pooling decals under stump (2-3 decals)
        for (int i = 0; i < 3; i++)
        {
            var dropOffset = new Vector2(_random.NextFloat(-0.25f, 0.25f), _random.NextFloat(-0.25f, 0.25f));
            SafeAddDecal(uid, stumpCoords, dropOffset, _mfloorDecals[_random.Next(_mfloorDecals.Length)], bloodColor, Angle.FromDegrees(_random.Next(0, 360)));
        }

        // Broadcast initial spray VFX particles from stump
        var netStumpCoords = GetNetCoordinates(stumpCoords);
        var hits = new List<BloodHitData>
        {
            new(netStumpCoords, sprayDir),
            new(netStumpCoords, Angle.FromDegrees(sprayAngle.Degrees - 20f).ToWorldVec()),
            new(netStumpCoords, Angle.FromDegrees(sprayAngle.Degrees + 20f).ToWorldVec()),
        };
        RaiseNetworkEvent(new MeleeBloodHitEffectEvent(hits), Filter.Pvs(uid));

        // Stain nearby bystanders caught in the dismemberment spray
        var nearbyDismemberBystanders = _lookup.GetEntitiesInRange<InventoryComponent>(stumpCoords, 1.5f);
        foreach (var mob in nearbyDismemberBystanders)
        {
            _bloodyItems.BloodyEquippedItems(mob, 0.12f, SlotFlags.OUTERCLOTHING | SlotFlags.FEET | SlotFlags.LEGS, 0.25f, bloodColor);
        }
    }

    public void SplatterBlood(EntityUid uid, BloodstreamComponent comp, int splatCount, float radius, bool spawnGibs = false)
    {
        var bloodColor = GetBloodColor(uid);
        var coords = Transform(uid).Coordinates;

        _sawmill.Debug($"[BloodSplatterSystem] SplatterBlood on {uid} count={splatCount} radius={radius}");

        for (int i = 0; i < splatCount; i++)
        {
            var angle = Angle.FromDegrees(_random.Next(0, 360));
            var dist = _random.NextFloat(0.2f, radius);
            var offset = angle.ToWorldVec() * dist;

            string decalId;
            Angle decalAngle;
            if (_random.Prob(0.5f))
            {
                decalId = GetDirSplatterDecal(angle.GetDir());
                decalAngle = Angle.Zero;
            }
            else
            {
                decalId = _mfloorDecals[_random.Next(_mfloorDecals.Length)];
                decalAngle = Angle.FromDegrees(_random.Next(0, 360));
            }

            SafeAddDecal(uid, coords, offset, decalId, bloodColor, decalAngle);

            if (spawnGibs && _random.Prob(0.5f))
            {
                string gibId = _torsoGibDecals[_random.Next(_torsoGibDecals.Length)];
                var gibDist = _random.NextFloat(0.2f, radius);
                var gibAngle = Angle.FromDegrees(_random.Next(0, 360));
                SafeAddDecal(uid, coords, gibAngle.ToWorldVec() * gibDist, gibId, bloodColor, gibAngle);
            }
        }
    }

    private void SplatterCircularGibs(EntityUid uid, BloodstreamComponent comp, int count, float radius)
    {
        SplatterBlood(uid, comp, count, radius, spawnGibs: true);
    }

    private void SafeAddDecal(
        EntityUid victim,
        EntityCoordinates originCoords,
        Vector2 offset,
        string decalId,
        Color bloodColor,
        Angle decalAngle)
    {
        var xform = Transform(victim);
        if (xform.GridUid == null)
            return;

        var gridUid = xform.GridUid.Value;
        var originMap = _transform.ToMapCoordinates(originCoords);
        var targetPos = originMap.Position + offset;
        var len = offset.Length();

        if (len > 0.05f)
        {
            var norm = offset / len;
            var ray = new CollisionRay(originMap.Position, norm, (int)CollisionGroup.Impassable);
            var results = _physics.IntersectRay(originMap.MapId, ray, len, ignoredEnt: victim, returnOnFirstHit: true);
            var hit = results.FirstOrDefault();

            if (hit.HitEntity != EntityUid.Invalid && hit.Distance < len)
            {
                // Wall hit: clamp floor decal before wall
                var clampedDist = MathF.Max(0.05f, hit.Distance - 0.25f);
                var clampedPos = originMap.Position + norm * clampedDist;
                var clampedLocal = _transform.ToCoordinates(gridUid, new MapCoordinates(clampedPos, originMap.MapId));
                var clampedDecalCoords = clampedLocal.Offset(new Vector2(-0.5f, -0.5f));

                if (!IsDecalDenseAt(gridUid, clampedLocal.Position))
                {
                    _decals.TryAddDecal(decalId, clampedDecalCoords, out _, bloodColor, decalAngle, 0, cleanable: true);
                }

                // Spawn blood on wall
                TrySpawnWallBlood(hit.HitEntity);
                return;
            }
        }

        var localCoords = _transform.ToCoordinates(gridUid, new MapCoordinates(targetPos, originMap.MapId));
        if (IsDecalDenseAt(gridUid, localCoords.Position))
            return;

        var decalCoords = localCoords.Offset(new Vector2(-0.5f, -0.5f));
        _decals.TryAddDecal(decalId, decalCoords, out _, bloodColor, decalAngle, 0, cleanable: true);
    }

    private void TrySpawnWallBlood(EntityUid wallUid)
    {
        if (!Exists(wallUid) || Deleted(wallUid) || Terminating(wallUid))
            return;

        if (!TryComp(wallUid, out TransformComponent? wallXform) || !wallXform.Anchored)
            return;

        var wallCoords = wallXform.Coordinates;
        var nearby = _lookup.GetEntitiesInRange<WallBloodComponent>(wallCoords, 0.4f);
        if (nearby.Count > 0)
            return;

        var proto = _random.Pick(_wallBloodPrototypes);
        var spawned = Spawn(proto, wallCoords);
        _transform.SetLocalRotation(spawned, Angle.FromDegrees(_random.Next(0, 360)), Transform(spawned));
    }

    private Color GetBloodColor(EntityUid uid)
    {
        if (_solutionContainer.TryGetSolution(uid, BloodstreamComponent.DefaultBloodSolutionName, out var soln, out var bloodSolution))
        {
            var solution = new Solution();
            foreach (var reagent in bloodSolution.Contents)
            {
                solution.AddReagent(reagent.Reagent, FixedPoint2.New(1));
                break;
            }
            if (solution.Volume > 0)
            {
                var reagentColor = solution.GetColor(_prototypeManager);
                // If it's normal reddish blood, enforce our grim deep dark blood color
                if (reagentColor.R > reagentColor.G + 0.1f && reagentColor.R > reagentColor.B + 0.1f)
                    return Color.FromHex("#780000");

                return reagentColor;
            }
        }

        return Color.FromHex("#780000");
    }
}
