namespace PicoGK_Run.Physics.SwirlSegment;

/// <summary>
/// Chamber inlet from explicit injector velocity decomposition + entry-plane thermo (authoritative SI initialization).
/// </summary>
public sealed class ChamberInletState
{
    public double MassFlowPrimaryKgS { get; init; }
    public double StaticPressurePa { get; init; }
    public double StaticDensityKgM3 { get; init; }
    public double StaticTemperatureK { get; init; }

    public double AxialVelocityMps { get; init; }
    public double TangentialVelocityMps { get; init; }
    public double RadialVelocityMps { get; init; }
    public double VelocityMagnitudeMps { get; init; }
    public double FlowAngleDeg { get; init; }
    public double SwirlRatioVtOverVx { get; init; }

    /// <summary>Build from <see cref="InjectorVelocityState"/> and first march / injector-plane jet state.</summary>
    public static ChamberInletState FromInjectorAndJetEntry(
        InjectorVelocityState? injector,
        double massFlowPrimaryKgS,
        double staticPressurePa,
        double staticDensityKgM3,
        double staticTemperatureK)
    {
        if (injector == null)
        {
            return new ChamberInletState
            {
                MassFlowPrimaryKgS = massFlowPrimaryKgS,
                StaticPressurePa = staticPressurePa,
                StaticDensityKgM3 = staticDensityKgM3,
                StaticTemperatureK = staticTemperatureK,
                AxialVelocityMps = 0.0,
                TangentialVelocityMps = 0.0,
                RadialVelocityMps = 0.0,
                VelocityMagnitudeMps = 0.0,
                FlowAngleDeg = 0.0,
                SwirlRatioVtOverVx = 0.0
            };
        }

        return new ChamberInletState
        {
            MassFlowPrimaryKgS = massFlowPrimaryKgS,
            StaticPressurePa = staticPressurePa,
            StaticDensityKgM3 = staticDensityKgM3,
            StaticTemperatureK = staticTemperatureK,
            AxialVelocityMps = injector.AxialVelocityMps,
            TangentialVelocityMps = injector.TangentialVelocityMps,
            RadialVelocityMps = injector.RadialVelocityMps,
            VelocityMagnitudeMps = injector.VelocityMagnitudeMps,
            FlowAngleDeg = injector.FlowAngleDeg,
            SwirlRatioVtOverVx = injector.SwirlRatioVtOverVx
        };
    }
}

/// <summary>
/// Authoritative swirl-chamber exit / expander-inlet handoff (pressures, velocities, Ġ_θ, margins).
/// </summary>
public sealed class ChamberExitState
{
    public double MdotTotalKgS { get; init; }
    public double StaticDensityKgM3 { get; init; }

    public double AxialVelocityMps { get; init; }
    public double TangentialVelocityMps { get; init; }
    public double StaticPressurePa { get; init; }
    public double TotalPressurePa { get; init; }
    public double WallStaticPressurePa { get; init; }

    public double ResidualSwirlRatioVtOverVx { get; init; }
    public double AngularMomentumFluxKgM2PerS2 { get; init; }

    public double InletSpillPressureMarginPa { get; init; }
    public double ExitDrivePressureMarginPa { get; init; }
}
