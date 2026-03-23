namespace PicoGK_Run.Physics;

/// <summary>
/// Central place for first-order chamber/vortex/ejector coefficients — tune vs experiment or CFD.
/// Not CFD-derived; explicit engineering defaults.
/// </summary>
public static class ChamberPhysicsCoefficients
{
    // --- Radial vortex pressure (RadialVortexPressureModel) ---
    public static double RadialCoreRadiusFractionOfWall { get; set; } = 0.24;
    public static double RadialPressureCapPa { get; set; } = 85_000.0;

    // --- Swirl decay (SwirlDecayModel): k_total = k_wall + k_mix + k_ent + k_inst ---
    public static double DecayCWall { get; set; } = 0.038;
    public static double DecayFRoughness { get; set; } = 1.0;
    public static double DecayCMix { get; set; } = 0.055;
    public static double DecayCEnt { get; set; } = 0.062;
    public static double DecayCInstability { get; set; } = 0.11;

    // --- Vortex structure (VortexStructureModel) ---
    public static double FluxSwirlGeometryFactorK { get; set; } = 0.78;
    public static double StructureQualityWCoreDrop { get; set; } = 0.18;
    public static double StructureQualityWEntrainment { get; set; } = 0.26;
    public static double StructureQualityWRecoverableSwirl { get; set; } = 0.22;
    public static double StructureQualityWBreakdown { get; set; } = 0.20;
    public static double StructureQualityWExcessDecay { get; set; } = 0.10;
    public static double StructureQualityWLowAxial { get; set; } = 0.14;

    // --- Diffuser (SwirlDiffuserRecoveryModel) ---
    public static double DiffuserCpMax { get; set; } = 0.42;
    public static double DiffuserSwirlHelpMax { get; set; } = 0.12;
    public static double DiffuserSeparationAngleRefDeg { get; set; } = 11.0;

    // --- Injector / stator losses ---
    public static double InjectorDischargeCoefficient { get; set; } = 0.92;
    public static double InjectorTurningLossK { get; set; } = 0.18;
    public static double StatorIncidenceRefDeg { get; set; } = 12.0;
    public static double StatorTurningLossK { get; set; } = 0.14;

    // --- Ejector regime scoring ---
    public static double EjectorShortfallCriticalRatio { get; set; } = 0.35;
}
