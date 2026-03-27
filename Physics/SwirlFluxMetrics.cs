namespace PicoGK_Run.Physics;

/// <summary>Ġ_θ [kg·m²/s²], Ġ_x = ṁ V_ax [kg·m²/s²], S = Ġ_θ / (R Ġ_x).</summary>
public readonly record struct SwirlFluxMetrics(
    double AngularMomentumFluxKgM2PerS2,
    double AxialMomentumFluxKgM2PerS2,
    double ReferenceRadiusM,
    double FluxSwirlNumber)
{
    public static SwirlFluxMetrics Compute(
        double angularMomentumFluxKgM2PerS2,
        double axialMomentumFluxKgM2PerS2,
        double referenceRadiusM)
    {
        double r = System.Math.Max(referenceRadiusM, 1e-9);
        double s = SwirlMath.FluxSwirlNumber(angularMomentumFluxKgM2PerS2, axialMomentumFluxKgM2PerS2, r);
        return new SwirlFluxMetrics(angularMomentumFluxKgM2PerS2, axialMomentumFluxKgM2PerS2, r, s);
    }
}
