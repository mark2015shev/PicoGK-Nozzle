namespace PicoGK_Run.Infrastructure.PhysicsAutotune;

/// <summary>The five tuned geometry scalars (SI path search only).</summary>
public readonly record struct CandidateGeometry(
    double SwirlChamberDiameterMm,
    double SwirlChamberLengthMm,
    double InletCaptureDiameterMm,
    double ExpanderHalfAngleDeg,
    double StatorVaneAngleDeg);
