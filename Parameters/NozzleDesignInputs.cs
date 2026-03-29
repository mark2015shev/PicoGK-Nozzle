namespace PicoGK_Run.Parameters;

/// <summary>
/// User-controlled nozzle design intent. No physics inside this type.
/// </summary>
public sealed class NozzleDesignInputs
{
    public double InletDiameterMm { get; init; }
    public double SwirlChamberDiameterMm { get; init; }
    public double SwirlChamberLengthMm { get; init; }

    /// <summary>
    /// Requested fraction along the <b>physical</b> main chamber [0–1]; geometry clamps to
    /// <see cref="RunConfiguration.InjectorAxialPositionMin"/>/<see cref="RunConfiguration.InjectorAxialPositionMax"/> for build + SI.
    /// </summary>
    public double InjectorAxialPositionRatio { get; init; }

    /// <summary>
    /// Optional separate cylindrical segment upstream of the main chamber (same bore), default 0.
    /// Does not change <see cref="SwirlChamberLengthMm"/>; unioned as additional voxel volume only.
    /// </summary>
    public double InjectorUpstreamGuardLengthMm { get; init; }

    public double TotalInjectorAreaMm2 { get; init; }
    public int InjectorCount { get; init; }
    public double InjectorWidthMm { get; init; }
    public double InjectorHeightMm { get; init; }
    public double InjectorYawAngleDeg { get; init; }
    public double InjectorPitchAngleDeg { get; init; }
    public double InjectorRollAngleDeg { get; init; }
    public double ExpanderLengthMm { get; init; }
    public double ExpanderHalfAngleDeg { get; init; }
    public double ExitDiameterMm { get; init; }
    public double StatorVaneAngleDeg { get; init; }
    public int StatorVaneCount { get; init; }

    /// <summary>Centerbody / hub OD at stator row [mm]. If ≤ 0.5, synthesis/heuristics may derive ~0.28× chamber OD.</summary>
    public double StatorHubDiameterMm { get; init; }

    /// <summary>Axial extent of stator row (hub + blade passage) [mm]. If ≤ 1, builders use a short default.</summary>
    public double StatorAxialLengthMm { get; init; }

    /// <summary>Blade chord scale [mm] (sets vane beam thickness). If ≤ 0.5, derived from span.</summary>
    public double StatorBladeChordMm { get; init; }

    public double WallThicknessMm { get; init; }

    /// <summary>Same design with a new <see cref="InjectorAxialPositionRatio"/> (e.g. after run clamp for SI + CAD alignment).</summary>
    public NozzleDesignInputs WithInjectorAxialPositionRatio(double ratio) => new()
    {
        InletDiameterMm = InletDiameterMm,
        SwirlChamberDiameterMm = SwirlChamberDiameterMm,
        SwirlChamberLengthMm = SwirlChamberLengthMm,
        InjectorAxialPositionRatio = ratio,
        InjectorUpstreamGuardLengthMm = InjectorUpstreamGuardLengthMm,
        TotalInjectorAreaMm2 = TotalInjectorAreaMm2,
        InjectorCount = InjectorCount,
        InjectorWidthMm = InjectorWidthMm,
        InjectorHeightMm = InjectorHeightMm,
        InjectorYawAngleDeg = InjectorYawAngleDeg,
        InjectorPitchAngleDeg = InjectorPitchAngleDeg,
        InjectorRollAngleDeg = InjectorRollAngleDeg,
        ExpanderLengthMm = ExpanderLengthMm,
        ExpanderHalfAngleDeg = ExpanderHalfAngleDeg,
        ExitDiameterMm = ExitDiameterMm,
        StatorVaneAngleDeg = StatorVaneAngleDeg,
        StatorVaneCount = StatorVaneCount,
        StatorHubDiameterMm = StatorHubDiameterMm,
        StatorAxialLengthMm = StatorAxialLengthMm,
        StatorBladeChordMm = StatorBladeChordMm,
        WallThicknessMm = WallThicknessMm
    };
}
