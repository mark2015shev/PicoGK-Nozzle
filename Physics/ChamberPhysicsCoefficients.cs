namespace PicoGK_Run.Physics;

/// <summary>
/// Central place for first-order chamber/vortex/ejector coefficients — tune vs experiment or CFD.
/// Not CFD-derived; explicit engineering defaults.
/// </summary>
public static class ChamberPhysicsCoefficients
{
    // --- Radial vortex pressure (RadialVortexPressureModel) — secondary shaping only; never bulk authority ---
    public static double RadialCoreRadiusFractionOfWall { get; set; } = 0.24;
    public static double RadialPressureCapPa { get; set; } = 120_000.0;

    /// <summary>Upper bound on radial integral pressure scale before bulk-relative shaping clamps (Pa).</summary>
    public static double RadialPressureCapAbsoluteMaxPa { get; set; } = 380_000.0;

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

    // --- Stator recovery (StatorRecoveryModel) — inspectable caps, swirl-nozzle defaults ---
    /// <summary>Fraction of recovered specific energy booked to static pressure rise [-].</summary>
    public static double StatorRecoveryFractionToPressure { get; set; } = 0.65;

    /// <summary>Cap on Δp vs ρ·Δ(½V_t²) [-] (legacy 0.25×2).</summary>
    public static double StatorRecoveryPressureRiseCapFactor { get; set; } = 0.50;

    /// <summary>Cap axial gain vs |ΔV_t| — higher helps tangential-dominated injector cases without inventing V_a at injectors.</summary>
    public static double StatorRecoveryAxialGainCapPerDeltaVt { get; set; } = 0.55;

    // --- Ejector regime scoring ---
    public static double EjectorShortfallCriticalRatio { get; set; } = 0.35;

    // --- SI path: couple diagnostics into thrust / march (first-order, bounded) ---
    /// <summary>B_vortex = 1 + C·Δp_core_useful/P_amb; C [-].</summary>
    public static double CouplingVortexEntrainmentC { get; set; } = 0.22;

    /// <summary>Absolute ceiling for B_vortex [-] (mass/energy path still limited by choked intake).</summary>
    public static double CouplingVortexEntrainmentBoostMax { get; set; } = 2.75;

    /// <summary>Soft cap: B_vortex ≤ 1 + γ·(½ρ|V|²/P_amb) ties boost to dynamic head [-].</summary>
    public static double CouplingVortexEntrainmentDynamicHeadGamma { get; set; } = 1.2;

    /// <summary>Cap on Δp_core passed into entrainment boost [Pa] (avoid over-pull).</summary>
    public static double CouplingInletCorePressureUseCapPa { get; set; } = 35_000.0;

    // --- Entrainment: lumped axial mixing effectiveness η_mix(L/D, Re) only (pressure deficit is primary driver) ---
    public static double EntrainmentLdRef { get; set; } = 2.5;
    public static double EntrainmentLdGain { get; set; } = 0.15;
    public static double EntrainmentReRef { get; set; } = 50_000.0;
    public static double EntrainmentReGain { get; set; } = 0.08;

    // --- Angular-momentum march (Ġ_θ = ṁ r V_θ): explicit lumped loss terms per axial step [kg·m²/s²] ---
    /// <summary>Wall / friction-like loss ∝ (Δx/D_h)·|Ġ_θ| (reduced-order).</summary>
    public static double AngularMomentumWallLossPerDxOverD { get; set; } = 0.14;

    /// <summary>Mixing irreversibility ∝ (Δṁ/ṁ_old)·|Ġ_θ| when secondary joins the mixed stream.</summary>
    public static double AngularMomentumMixingLossPerDeltaMOverM { get; set; } = 0.055;

    /// <summary>Organized-swirl erosion when entrained mass carries ~0 tangential momentum (lumped dilution).</summary>
    public static double AngularMomentumEntrainmentDilutionPerDeltaMOverM { get; set; } = 0.048;

    /// <summary>Upper clamp (legacy) on multiplicative entrainment scaling — march now uses capture pressure deficit [Pa].</summary>
    public static double EntrainmentMassDemandBoostClampMax { get; set; } = 4.0;

    /// <summary>
    /// Discharge coefficient C_d on √(2 ΔP/ρ) for capture-boundary entrainment velocity (lumped intake; not full port CFD).
    /// </summary>
    public static double CaptureEntrainmentDischargeCoefficient { get; set; } = 0.62;

    /// <summary>Small bounded shear-entrainment addition to pressure-driven increment (stability) [-].</summary>
    public static double EntrainmentShearAugmentationFraction { get; set; } = 0.12;

    // --- Chamber march: first-order total-pressure losses (see ChamberMarchLossModel) ---
    /// <summary>Ė_mix loss ∝ (Δṁ/ṁ) · q̄ applied to P₀ [-].</summary>
    public static double MarchMixingDp0OverDynamicHead { get; set; } = 0.42;

    /// <summary>Wall / wetted-duct loss ∝ (Δx/D_h)·q̄ on P₀ [-].</summary>
    public static double MarchWallDp0OverDynamicHeadPerDxOverD { get; set; } = 0.18;

    /// <summary>Angular-momentum P₀ loss ∝ (fractional |Ġ_θ| loss this step)·q_θ on P₀ [-].</summary>
    public static double MarchSwirlDecayDp0OverSwirlDynamicHead { get; set; } = 0.22;

    /// <summary>
    /// Explicit first-order ceiling on mixed static P in the chamber march: P ≤ this factor × P_ambient (cold-side receiver bound).
    /// Prevents isentropic closure from pinning against <see cref="SiPressureGuards.MaxStaticPressurePa"/> when T₀ from the hot primary is large.
    /// </summary>
    public static double MarchMixedStaticPressureMaxTimesAmbient { get; set; } = 9.2;

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
    /// <summary>Upper cap on effective stator η in hub coupling — swirl-vortex nozzle uses a slightly higher ceiling than generic ejector defaults.</summary>
    public static double HubStatorMaxEtaCap { get; set; } = 0.58;
}
