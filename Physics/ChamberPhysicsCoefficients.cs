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

    // --- SI path: couple diagnostics into thrust / march (first-order, bounded) ---
    /// <summary>B_vortex = 1 + C·Δp_core_useful/P_amb; C [-].</summary>
    public static double CouplingVortexEntrainmentC { get; set; } = 0.22;

    /// <summary>Clamp upper bound for B_vortex [-].</summary>
    public static double CouplingVortexEntrainmentBoostMax { get; set; } = 1.18;

    /// <summary>Cap on Δp_core passed into entrainment boost [Pa] (avoid over-pull).</summary>
    public static double CouplingInletCorePressureUseCapPa { get; set; } = 35_000.0;

    /// <summary>Scales incidence term in η_stator,eff = η_base·clamp(1 − w_i·K_inc − w_t·K_turn, …).</summary>
    public static double StatorCouplingKIncidenceWeight { get; set; } = 1.0;

    /// <summary>Scales turning term in stator η coupling [-].</summary>
    public static double StatorCouplingKTurnWeight { get; set; } = 1.0;

    /// <summary>Minimum η factor applied to base stator η [-].</summary>
    public static double StatorCouplingEtaFactorFloor { get; set; } = 0.12;

    /// <summary>Reference <see cref="SwirlDiffuserRecoveryResult.EffectivePressureRecoveryEfficiency"/> for scaling ΔP_exp [-].</summary>
    public static double DiffuserCouplingReferenceEfficiency { get; set; } = 0.22;

    public static double DiffuserCouplingScaleMin { get; set; } = 0.35;
    public static double DiffuserCouplingScaleMax { get; set; } = 1.12;

    /// <summary>k_sep in V_ax,eff = V_ax·clamp(1 − k_sep·SeparationRisk, …).</summary>
    public static double CouplingDiffuserSeparationAxialPenaltyK { get; set; } = 0.28;

    public static double CouplingDiffuserSeparationAxialFloor { get; set; } = 0.58;

    /// <summary>Small swirl-energy bookkeeping weight for diffuser coupling [−].</summary>
    public static double SwirlLedgerDiffuserBookkeepingK { get; set; } = 0.08;

    // --- Hub-based stator (HubStatorFirstOrderModel) ---
    public static double HubStatorBlockagePenaltyScale { get; set; } = 1.45;
    public static double HubStatorBlockagePenaltyCap { get; set; } = 0.52;
    public static double HubStatorMaxEtaCap { get; set; } = 0.50;
}
