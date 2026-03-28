namespace PicoGK_Run.Physics;

/// <summary>
/// Named first-order limits for chamber bulk thermo, swirl metrics, capacity bands, and mixed-flow sizing — not CFD.
/// Mach thresholds for entrance capacity are also on <see cref="SwirlEntranceCapacityLimits"/> (keep in sync when tuning).
/// </summary>
public static class ChamberAerodynamicsConfiguration
{
    /// <summary>Below this |Va|, flux swirl S = Ġθ/(R·ṁ·Va) is ill-conditioned; use bulk / directive surrogates.</summary>
    public const double VaFloorForBulkSwirlMps = 2.0;

    /// <summary>Radial wall static may exceed bulk static by at most this fraction of the P₀ ceiling used for clamping.</summary>
    public const double WallStaticExcessOverBulkMaxFractionOfP0 = 0.015;

    /// <summary>Accept |P_s − P_s(Mach)| / P_s from dual isentropic checks.</summary>
    public const double IsentropicPressureConsistencyRelativeTolerance = 0.03;

    /// <summary>Target subsonic Mach for mixed annulus sizing: A_free ≥ ṁ_total / (ρ_mix · M_target · a_mix).</summary>
    public const double ChamberSizingTargetMachMixedFlow = 0.35;

    /// <summary>P_static must not exceed P_total by more than this relative amount (lab frame).</summary>
    public const double StaticMustNotExceedTotalRelativeTolerance = 2e-4;

    /// <summary>
    /// Radial integral cap Pa = max(<see cref="ChamberPhysicsCoefficients.RadialPressureCapPa"/>, K·½ρV_t²) before P₀/ambient shaping clamps
    /// — keeps secondary core/wall deltas visible when swirl dynamic pressure is large (does not change bulk P_static).
    /// </summary>
    public const double RadialIntegralCapTimesSwirlDynamicPressure = 6.0;
}
