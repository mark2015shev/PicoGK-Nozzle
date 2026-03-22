namespace PicoGK_Run.Physics;

/// <summary>
/// Geometry-driving outputs from the SI flow + thrust post-process (convert to mm only at geometry boundary).
/// </summary>
public sealed class NozzleDesignResult
{
    public double InletAreaM2 { get; init; }
    public double OutletAreaM2 { get; init; }
    public double SuggestedMixingLengthM { get; init; }
    public double SuggestedOutletRadiusM { get; init; }
    public double SuggestedInletRadiusM { get; init; }
    public double EstimatedExitVelocityMps { get; init; }
    public double EstimatedTotalMassFlowKgS { get; init; }
    public double EstimatedThrustN { get; init; }
}
