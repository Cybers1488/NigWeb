using Content.Shared.Damage;
using Content.Shared.Body.Components;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Fluids.Components;
using Robust.Shared.Random;
using Robust.Shared.Map;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.StatusEffect;
using Content.Server.Decals;
using Robust.Shared.Prototypes;

namespace Content.Server._NigWeb.BloodSplatter;

public sealed class BloodSplatterSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;

    private readonly string[] _splatterDecals = new string[] { "BloodSplatter_mfloor1", "BloodSplatter_mfloor10", "BloodSplatter_mfloor11", "BloodSplatter_mfloor12", "BloodSplatter_mfloor2", "BloodSplatter_mfloor3", "BloodSplatter_mfloor4", "BloodSplatter_mfloor5", "BloodSplatter_mfloor6", "BloodSplatter_mfloor7", "BloodSplatter_mfloor8", "BloodSplatter_mfloor9", "BloodSplatter_mgibbl1", "BloodSplatter_mgibbl2", "BloodSplatter_mgibbl3", "BloodSplatter_mgibbl4", "BloodSplatter_mgibbl5" };

    private readonly string[] _gibDecals = new string[] { "BloodGib_gib1", "BloodGib_gib1_flesh", "BloodGib_gib2", "BloodGib_gib2_flesh", "BloodGib_gib3_0_0", "BloodGib_gib3_1_0", "BloodGib_gib3_2_0", "BloodGib_gib3_3_0", "BloodGib_gib3_flesh_0_0", "BloodGib_gib3_flesh_1_0", "BloodGib_gib3_flesh_2_0", "BloodGib_gib3_flesh_3_0", "BloodGib_gib5_0_0", "BloodGib_gib5_1_0", "BloodGib_gib5_2_0", "BloodGib_gib5_3_0", "BloodGib_gib5_flesh_0_0", "BloodGib_gib5_flesh_1_0", "BloodGib_gib5_flesh_2_0", "BloodGib_gib5_flesh_3_0", "BloodGib_gib6_0_0", "BloodGib_gib6_1_0", "BloodGib_gib6_2_0", "BloodGib_gib6_3_0", "BloodGib_gib6_flesh_0_0", "BloodGib_gib6_flesh_1_0", "BloodGib_gib6_flesh_2_0", "BloodGib_gib6_flesh_3_0", "BloodGib_gibarm_0_0", "BloodGib_gibarm_1_0", "BloodGib_gibarm_2_0", "BloodGib_gibarm_3_0", "BloodGib_gibarm_flesh_0_0", "BloodGib_gibarm_flesh_1_0", "BloodGib_gibarm_flesh_2_0", "BloodGib_gibarm_flesh_3_0", "BloodGib_gibbear1", "BloodGib_gibbear2", "BloodGib_gibbear3", "BloodGib_gibbear4", "BloodGib_gibbear5", "BloodGib_gibbearcore", "BloodGib_gibbearhead_0_0", "BloodGib_gibbearhead_1_0", "BloodGib_gibbearhead_2_0", "BloodGib_gibbearhead_3_0", "BloodGib_gibdown1", "BloodGib_gibdown1_flesh", "BloodGib_gibhead_0_0", "BloodGib_gibhead_1_0", "BloodGib_gibhead_2_0", "BloodGib_gibhead_3_0", "BloodGib_gibleg_0_0", "BloodGib_gibleg_1_0", "BloodGib_gibleg_2_0", "BloodGib_gibleg_3_0", "BloodGib_gibleg_flesh_0_0", "BloodGib_gibleg_flesh_1_0", "BloodGib_gibleg_flesh_2_0", "BloodGib_gibleg_flesh_3_0", "BloodGib_gibmid1", "BloodGib_gibmid2", "BloodGib_gibmid2_flesh", "BloodGib_gibmid3", "BloodGib_gibmid3_flesh", "BloodGib_gibtorso_0_0", "BloodGib_gibtorso_1_0", "BloodGib_gibtorso_2_0", "BloodGib_gibtorso_3_0", "BloodGib_gibtorso_flesh_0_0", "BloodGib_gibtorso_flesh_1_0", "BloodGib_gibtorso_flesh_2_0", "BloodGib_gibtorso_flesh_3_0", "BloodGib_gibup1", "BloodGib_gibup1_flesh" };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StatusEffectsComponent, DamageChangedEvent>(OnDamage);
        SubscribeLocalEvent<StatusEffectsComponent, BeingGibbedEvent>(OnGib);
        SubscribeLocalEvent<StatusEffectsComponent, BodyPartRemovedEvent>(OnPartRemoved);
    }

    private void OnDamage(EntityUid uid, StatusEffectsComponent status, DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !args.DamageIncreased)
            return;
            
        if (!TryComp<BloodstreamComponent>(uid, out var comp))
            return;

        float splatterAmount = 0;
        if (args.DamageDelta.DamageDict.TryGetValue("Slash", out var slash)) splatterAmount += slash.Float();
        if (args.DamageDelta.DamageDict.TryGetValue("Piercing", out var pierce)) splatterAmount += pierce.Float();

        if (splatterAmount > 5)
        {
            int splatCount = System.Math.Clamp((int)(splatterAmount / 10), 1, 4);
            SplatterBlood(uid, comp, splatCount, 1.5f, false);
        }
    }

    private void OnGib(EntityUid uid, StatusEffectsComponent status, ref BeingGibbedEvent args)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var comp))
            return;
        SplatterBlood(uid, comp, 12, 3.5f, true);
    }

    private void OnPartRemoved(EntityUid uid, StatusEffectsComponent status, ref BodyPartRemovedEvent args)
    {
        if (TryComp<BloodstreamComponent>(uid, out var comp))
        {
            SplatterBlood(uid, comp, 5, 2.0f, true);
        }
        
        if (!TryComp<BodyPartComponent>(args.Part, out var partComp))
            return;
            
        if (!TryComp<TransformComponent>(args.Part, out var partXform))
            return;

        var coords = partXform.Coordinates;
        string gibSpawn = "";
        
        if (partComp.PartType == BodyPartType.Head)
            gibSpawn = "NigWebMeatGibHead";
        else if (partComp.PartType == BodyPartType.Arm)
            gibSpawn = "NigWebMeatGibArm";
        else if (partComp.PartType == BodyPartType.Leg)
            gibSpawn = "NigWebMeatGibLeg";
            
        if (!string.IsNullOrEmpty(gibSpawn))
        {
            //Spawn(gibSpawn, coords);
            
            int chunks = _random.Next(1, 3);
            for (int i = 0; i < chunks; i++) {
                //Spawn("NigWebMeatGib", coords);
            }
            
            //QueueDel(args.Part);
        }
    }

    public void SplatterBlood(EntityUid uid, BloodstreamComponent comp, int splatCount, float radius, bool spawnGibs)
    {
        if (!_solutionContainer.TryGetSolution(uid, BloodstreamComponent.DefaultBloodSolutionName, out var soln, out var bloodSolution))
            return;
            
        var xform = Transform(uid);
        var coords = xform.Coordinates;
        
        var solution = new Solution();
        foreach (var reagent in bloodSolution.Contents)
        {
            solution.AddReagent(reagent.Reagent, FixedPoint2.New(1));
            break;
        }
        if (solution.Volume == 0)
            solution.AddReagent("Blood", FixedPoint2.New(1));
            
        var bloodColor = solution.GetColor(_prototypeManager);

        for (int i = 0; i < splatCount; i++)
        {
            var offset = new System.Numerics.Vector2(_random.NextFloat(-radius, radius), _random.NextFloat(-radius, radius));
            var newCoords = coords.Offset(offset);
            
            var angle = Angle.FromDegrees(_random.Next(0, 360));
            
            string decalId = _splatterDecals[_random.Next(_splatterDecals.Length)];
            _decals.TryAddDecal(decalId, newCoords, out _, bloodColor, angle, 0, true);
            
            if (spawnGibs && _random.Prob(0.5f))
            {
                string gibDecal = _gibDecals[_random.Next(_gibDecals.Length)];
                var gibOffset = new System.Numerics.Vector2(_random.NextFloat(-radius, radius), _random.NextFloat(-radius, radius));
                var gibCoords = coords.Offset(gibOffset);
                var gibAngle = Angle.FromDegrees(_random.Next(0, 360));
                _decals.TryAddDecal(gibDecal, gibCoords, out _, bloodColor, gibAngle, 0, true);
            }
        }
    }
}
