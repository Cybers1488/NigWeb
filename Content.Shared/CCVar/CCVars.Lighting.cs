using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> AmbientOcclusion =
        CVarDef.Create("light.ambient_occlusion", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Distance in world-pixels of ambient occlusion.
    /// </summary>
    public static readonly CVarDef<string> AmbientOcclusionColor =
        CVarDef.Create("light.ambient_occlusion_color", "#04080FAA", CVar.CLIENTONLY);

    /// <summary>
    /// Distance in world-pixels of ambient occlusion.
    /// </summary>
    public static readonly CVarDef<float> AmbientOcclusionDistance =
        CVarDef.Create("light.ambient_occlusion_distance", 4f, CVar.CLIENTONLY);

    /// <summary>
    /// Enables the grimdark atmospheric lighting and visual post-processing overhaul.
    /// </summary>
    public static readonly CVarDef<bool> GrimAtmosphere =
        CVarDef.Create("light.grim_atmosphere", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Intensity multiplier for grimdark atmospheric grading, mist, and vignette (0.0 to 1.5).
    /// </summary>
    public static readonly CVarDef<float> GrimAtmosphereIntensity =
        CVarDef.Create("light.grim_atmosphere_intensity", 1.0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Enables dynamic realistic entity shadows (ground contact shadows and directional light shadows).
    /// </summary>
    public static readonly CVarDef<bool> EntityShadows =
        CVarDef.Create("light.entity_shadows", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Opacity multiplier for dynamic entity shadows (0.0 to 1.0).
    /// </summary>
    public static readonly CVarDef<float> EntityShadowsOpacity =
        CVarDef.Create("light.entity_shadows_opacity", 0.7f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
