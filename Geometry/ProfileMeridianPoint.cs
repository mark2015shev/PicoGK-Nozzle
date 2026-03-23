namespace PicoGK_Run.Geometry;

/// <summary>Meridian-plane sample (axis X, radius in YZ) for lattice-derived solids.</summary>
public sealed record ProfileMeridianPoint(double XMm, double RInnerMm, double ROuterMm, string Label);
